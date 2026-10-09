extends Node3D

## Mascota de escritorio de Artemis: ventana transparente, sin bordes y siempre
## encima, con un avatar VRM animado por código (el modelo no trae animaciones).
## Solo la silueta del personaje recibe el mouse; el resto de la ventana deja
## pasar los clics al escritorio.

const AVATAR_PATH := "res://models/sample.vrm"
const CONFIG_PATH := "user://mascot.cfg"
const WINDOW_SIZE := Vector2i(260, 400)
const SCREEN_MARGIN := 12

# Huesos y expresiones del esqueleto de VRoid (el VRM se carga como glTF puro,
# sin la extensión VRM, así que se usan los nombres originales del archivo).
const BONE_HIPS := "J_Bip_C_Hips"
const BONE_SPINE := "J_Bip_C_Spine"
const BONE_CHEST := "J_Bip_C_Chest"
const BONE_NECK := "J_Bip_C_Neck"
const BONE_HEAD := "J_Bip_C_Head"
const BONE_SHOULDER_L := "J_Bip_L_Shoulder"
const BONE_SHOULDER_R := "J_Bip_R_Shoulder"
const BONE_UPPER_ARM_L := "J_Bip_L_UpperArm"
const BONE_UPPER_ARM_R := "J_Bip_R_UpperArm"
# Huesos auxiliares de VRoid (manga de la polera y corrección del brazo) que
# cuelgan del hombro y en Unity siguen al brazo por constraints; sin la
# extensión VRM no hay constraints, así que se les copia la rotación del brazo.
const ARM_FOLLOWERS_L := ["J_Aim_L_TopsUpperArm", "J_Aim_L_Shoulder"]
const ARM_FOLLOWERS_R := ["J_Aim_R_TopsUpperArm", "J_Aim_R_Shoulder"]
const BONE_LOWER_ARM_L := "J_Bip_L_LowerArm"
const BONE_LOWER_ARM_R := "J_Bip_R_LowerArm"
const BLINK_SHAPE := "Fcl_EYE_Close"

const SHOULDER_DOWN_DEGREES := 12.0
const ARM_DOWN_DEGREES := 62.0
const ELBOW_BEND_DEGREES := 12.0
const LOOK_MAX_YAW := deg_to_rad(35.0)
const LOOK_MAX_PITCH := deg_to_rad(20.0)
const LOOK_SMOOTHING := 6.0
const BREATH_PERIOD := 4.2
const BLINK_DURATION := 0.16

enum MenuItem { RESET_POSITION, QUIT }

@onready var _camera: Camera3D = $Camera3D
@onready var _menu: PopupMenu = $ContextMenu

var _skeleton: Skeleton3D
var _bones := {}
var _face: MeshInstance3D
var _blink_index := -1
var _avatar_meshes: Array[MeshInstance3D] = []

var _time := 0.0
var _look := Vector2.ZERO  # x = yaw, y = pitch (radianes), suavizado
var _next_blink := 2.0
var _blink_t := -1.0

var _fixed_look: Variant = null  # Vector2 en [-1, 1] para pruebas (--look=x,y)

var _dragging := false
var _drag_offset := Vector2i.ZERO


func _ready() -> void:
	_setup_window()
	_load_avatar()
	_setup_menu()
	_handle_screenshot_arg()


# --- Ventana -----------------------------------------------------------------

func _setup_window() -> void:
	var window := get_window()
	window.size = WINDOW_SIZE
	window.always_on_top = true
	window.transparent = true
	window.borderless = true
	get_viewport().transparent_bg = true
	window.position = _load_saved_position()


func _corner_position() -> Vector2i:
	var area := DisplayServer.screen_get_usable_rect(DisplayServer.SCREEN_PRIMARY)
	return area.end - WINDOW_SIZE - Vector2i(SCREEN_MARGIN, 0)


func _load_saved_position() -> Vector2i:
	var cfg := ConfigFile.new()
	if cfg.load(CONFIG_PATH) != OK:
		return _corner_position()
	var saved: Vector2i = cfg.get_value("window", "position", _corner_position())
	# Si la pantalla cambió (otro monitor, otra resolución), volver a la esquina.
	for screen in DisplayServer.get_screen_count():
		if DisplayServer.screen_get_usable_rect(screen).has_point(saved + WINDOW_SIZE / 2):
			return saved
	return _corner_position()


func _save_position() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("window", "position", get_window().position)
	cfg.save(CONFIG_PATH)


# Solo la caja del avatar proyectada en pantalla recibe el mouse.
func _update_mouse_passthrough() -> void:
	if _avatar_meshes.is_empty():
		return
	var rect := Rect2()
	var first := true
	for mesh in _avatar_meshes:
		var aabb := mesh.global_transform * mesh.get_aabb()
		for i in 8:
			var p := _camera.unproject_position(aabb.get_endpoint(i))
			if first:
				rect = Rect2(p, Vector2.ZERO)
				first = false
			else:
				rect = rect.expand(p)
	var inset := rect.size.x * 0.12
	rect = rect.grow_individual(-inset, 0, -inset, 0).intersection(Rect2(Vector2.ZERO, Vector2(WINDOW_SIZE)))
	DisplayServer.window_set_mouse_passthrough(PackedVector2Array([
		rect.position,
		Vector2(rect.end.x, rect.position.y),
		rect.end,
		Vector2(rect.position.x, rect.end.y),
	]))


# --- Avatar ------------------------------------------------------------------

func _load_avatar() -> void:
	var gltf_doc := GLTFDocument.new()
	var gltf_state := GLTFState.new()
	var err := gltf_doc.append_from_file(AVATAR_PATH, gltf_state)
	if err != OK:
		push_error("Error cargando el VRM (append_from_file): %s" % err)
		return
	var avatar := gltf_doc.generate_scene(gltf_state)
	if avatar == null:
		push_error("generate_scene devolvió null")
		return
	add_child(avatar)

	var skeletons := avatar.find_children("*", "Skeleton3D", true, false)
	if not skeletons.is_empty():
		_skeleton = skeletons[0]
		for bone_name in [BONE_HIPS, BONE_SPINE, BONE_CHEST, BONE_NECK, BONE_HEAD,
				BONE_SHOULDER_L, BONE_SHOULDER_R, BONE_UPPER_ARM_L, BONE_UPPER_ARM_R,
				BONE_LOWER_ARM_L, BONE_LOWER_ARM_R] + ARM_FOLLOWERS_L + ARM_FOLLOWERS_R:
			_bones[bone_name] = _skeleton.find_bone(bone_name)

	for node in avatar.find_children("*", "MeshInstance3D", true, false):
		var mesh := node as MeshInstance3D
		_avatar_meshes.append(mesh)
		if mesh.mesh is ArrayMesh:
			var array_mesh := mesh.mesh as ArrayMesh
			for i in array_mesh.get_blend_shape_count():
				if array_mesh.get_blend_shape_name(i).ends_with(BLINK_SHAPE):
					_face = mesh
					_blink_index = i

	_update_mouse_passthrough()


func _process(delta: float) -> void:
	_time += delta
	if _skeleton:
		_animate_body(delta)
	_animate_blink(delta)


func _animate_body(delta: float) -> void:
	var breath := sin(_time * TAU / BREATH_PERIOD)
	var sway := sin(_time * TAU / (BREATH_PERIOD * 2.3))

	_pose(BONE_HIPS, Quaternion(Vector3.BACK, deg_to_rad(0.8) * sway))
	_pose(BONE_SPINE, Quaternion(Vector3.RIGHT, deg_to_rad(-0.8) * breath))
	_pose(BONE_CHEST, Quaternion(Vector3.RIGHT, deg_to_rad(-1.2) * breath))

	# Brazos abajo desde la pose T, con los codos un poco doblados y un
	# movimiento mínimo al respirar. (Vector3.BACK es +Z: el avatar mira hacia
	# la cámara, y su brazo izquierdo apunta a +X.)
	var shoulder := deg_to_rad(SHOULDER_DOWN_DEGREES)
	_pose(BONE_SHOULDER_L, Quaternion(Vector3.BACK, -shoulder))
	_pose(BONE_SHOULDER_R, Quaternion(Vector3.BACK, shoulder))
	var arm := deg_to_rad(ARM_DOWN_DEGREES + 1.5 * breath)
	for bone_name in [BONE_UPPER_ARM_L] + ARM_FOLLOWERS_L:
		_pose(bone_name, Quaternion(Vector3.BACK, -arm))
	for bone_name in [BONE_UPPER_ARM_R] + ARM_FOLLOWERS_R:
		_pose(bone_name, Quaternion(Vector3.BACK, arm))
	_pose(BONE_LOWER_ARM_L, Quaternion(Vector3.UP, deg_to_rad(ELBOW_BEND_DEGREES)))
	_pose(BONE_LOWER_ARM_R, Quaternion(Vector3.UP, deg_to_rad(-ELBOW_BEND_DEGREES)))

	# Mirar al cursor: posición del mouse en pantalla relativa a la cabeza.
	var head_screen := Vector2(get_window().position) + Vector2(WINDOW_SIZE) * Vector2(0.5, 0.25)
	var to_mouse := Vector2(DisplayServer.mouse_get_position()) - head_screen
	var target := Vector2(
		clampf(to_mouse.x / 600.0, -1.0, 1.0),
		clampf(-to_mouse.y / 400.0, -1.0, 1.0))
	if _fixed_look != null:
		target = _fixed_look
	target *= Vector2(LOOK_MAX_YAW, LOOK_MAX_PITCH)
	_look = _look.lerp(target, clampf(LOOK_SMOOTHING * delta, 0.0, 1.0))
	var look_rot := Quaternion(Vector3.UP, _look.x) * Quaternion(Vector3.RIGHT, -_look.y)
	_pose(BONE_NECK, Quaternion.IDENTITY.slerp(look_rot, 0.4))
	_pose(BONE_HEAD, Quaternion.IDENTITY.slerp(look_rot, 0.6))


# Aplica una rotación expresada en ejes del esqueleto (X derecha de la
# pantalla, Y arriba, Z hacia la cámara) sobre la pose de reposo del hueso.
# Hay que llevarla a los ejes locales del hueso: en este modelo algunos huesos
# (la cabeza) tienen el reposo rotado y girar sobre su "Y" local la inclinaba.
func _pose(bone_name: String, rotation_delta: Quaternion) -> void:
	var idx: int = _bones.get(bone_name, -1)
	if idx < 0:
		return
	var rest := _skeleton.get_bone_rest(idx).basis.get_rotation_quaternion()
	var global_rest := _skeleton.get_bone_global_rest(idx).basis.get_rotation_quaternion()
	var local_delta := global_rest.inverse() * rotation_delta * global_rest
	_skeleton.set_bone_pose_rotation(idx, rest * local_delta)


func _animate_blink(delta: float) -> void:
	if _blink_index < 0:
		return
	if _blink_t < 0.0:
		_next_blink -= delta
		if _next_blink <= 0.0:
			_blink_t = 0.0
		return
	_blink_t += delta
	_face.set_blend_shape_value(_blink_index, sin(clampf(_blink_t / BLINK_DURATION, 0.0, 1.0) * PI))
	if _blink_t >= BLINK_DURATION:
		_face.set_blend_shape_value(_blink_index, 0.0)
		_blink_t = -1.0
		_next_blink = randf_range(2.5, 6.0)


# --- Mouse: arrastrar y menú -------------------------------------------------

func _setup_menu() -> void:
	_menu.add_item("Volver a la esquina", MenuItem.RESET_POSITION)
	_menu.add_separator()
	_menu.add_item("Salir", MenuItem.QUIT)
	_menu.id_pressed.connect(_on_menu_id_pressed)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button := event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_LEFT:
			if button.pressed:
				_dragging = true
				_drag_offset = DisplayServer.mouse_get_position() - get_window().position
			elif _dragging:
				_dragging = false
				_save_position()
		elif button.button_index == MOUSE_BUTTON_RIGHT and button.pressed:
			_menu.popup(Rect2i(DisplayServer.mouse_get_position(), Vector2i.ZERO))
	elif event is InputEventMouseMotion and _dragging:
		get_window().position = DisplayServer.mouse_get_position() - _drag_offset


func _on_menu_id_pressed(id: int) -> void:
	match id:
		MenuItem.RESET_POSITION:
			get_window().position = _corner_position()
			_save_position()
		MenuItem.QUIT:
			get_tree().quit()


# --- Pruebas: captura del encuadre y mirada fija -----------------------------
# Uso: Godot --path DesktopMascot -- --screenshot=C:\ruta\captura.png --look=1,0
# (--look fija la mirada en [-1, 1]: x a la derecha de la pantalla, y arriba.)

func _handle_screenshot_arg() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--look="):
			var parts := arg.trim_prefix("--look=").split(",")
			_fixed_look = Vector2(parts[0].to_float(), parts[1].to_float())
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--screenshot="):
			await get_tree().create_timer(1.5).timeout
			await RenderingServer.frame_post_draw
			var path := arg.trim_prefix("--screenshot=")
			get_viewport().get_texture().get_image().save_png(path)
			print("Captura guardada en ", path)
			get_tree().quit()
