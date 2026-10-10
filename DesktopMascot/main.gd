extends Node3D

## Mascota de escritorio de Artemis: ventana transparente, sin bordes y siempre
## encima, con un avatar VRM. Dos capas de animación que se mezclan:
## - procedural: brazos abajo, respiración, balanceo, parpadeo y mirada al cursor;
## - clips VRMA (res://animations/*.vrma, ver vrma_clip.gd) que se disparan con eventos:
##   saludar al abrir, reaccionar al clic, celebrar al guardar algo.
## Solo la silueta del personaje recibe el mouse; el resto de la ventana deja
## pasar los clics al escritorio.

const AVATAR_PATH := "res://models/sample.vrm"
const ANIMATIONS_DIR := "res://animations"
const CONFIG_PATH := "user://mascot.cfg"
const WINDOW_SIZE := Vector2i(260, 400)
const SCREEN_MARGIN := 12

# Huesos auxiliares de este modelo de VRoid (manga de la polera y corrección del brazo) que
# cuelgan del hombro y siguen al brazo por VRMC_node_constraint, que aquí no se evalúa: se
# les copia la rotación del brazo. En otro modelo simplemente no existen y se ignoran.
const ARM_FOLLOWERS := {
	"leftUpperArm": ["J_Aim_L_TopsUpperArm", "J_Aim_L_Shoulder"],
	"rightUpperArm": ["J_Aim_R_TopsUpperArm", "J_Aim_R_Shoulder"],
}

const SHOULDER_DOWN_DEGREES := 12.0
const ARM_DOWN_DEGREES := 62.0
const ELBOW_BEND_DEGREES := 12.0
const LOOK_MAX_YAW := deg_to_rad(35.0)
const LOOK_MAX_PITCH := deg_to_rad(20.0)
const LOOK_SMOOTHING := 6.0
const BREATH_PERIOD := 4.2
const BLINK_DURATION := 0.16

const CLIP_FADE_IN := 0.3
const CLIP_FADE_OUT := 0.4
# Qué clips se usan para cada evento, por nombre de archivo (sin .vrma), en orden de
# preferencia; "click" elige uno al azar entre los que existan. Si ninguno existe, se usa
# cualquier clip disponible.
const CLIP_ROLES := {
	"greet": ["greeting", "wave"],
	"celebrate": ["peace", "celebrate", "spin", "model_pose", "wave"],
	"click": ["peace", "greeting", "model_pose", "show_body", "spin", "wave"],
}

enum MenuItem { NEW_ALARM, NEW_NOTE, NEW_TASK, NEW_REMINDER, PROJECT_COMMENT, SERVERS, RESET_POSITION, QUIT }

const LunaTheme := preload("res://luna_theme.gd")
const ApiClient := preload("res://api_client.gd")
const Bubble := preload("res://bubble.gd")
const ServersWindow := preload("res://servers_window.gd")
const AlarmForm := preload("res://forms/alarm_form.gd")
const NoteForm := preload("res://forms/note_form.gd")
const TaskForm := preload("res://forms/task_form.gd")
const CommentForm := preload("res://forms/comment_form.gd")
const VrmaClip := preload("res://vrma_clip.gd")

@onready var _camera: Camera3D = $Camera3D
@onready var _menu: PopupMenu = $ContextMenu

var _api: ApiClient
var _bubble: Bubble

var _skeleton: Skeleton3D
var _human := {}        # hueso humanoide VRM ("hips", "leftUpperArm"…) -> índice en el esqueleto
var _followers := {}    # hueso humanoide -> índices de huesos que copian su rotación
var _expressions := {}  # preset VRM ("blink", "happy"…) -> [[MeshInstance3D, blend shape, peso], …]
var _hips_height := 1.0
var _posed := {}        # huesos tocados en el último frame (para devolverlos al reposo)
var _avatar_meshes: Array[MeshInstance3D] = []

var _clips := {}        # nombre de archivo -> VrmaClip
var _clip: VrmaClip
var _clip_t := 0.0
var _clip_expressions := {}

var _time := 0.0
var _look := Vector2.ZERO  # x = yaw, y = pitch (radianes), suavizado
var _next_blink := 2.0
var _blink_t := -1.0

var _fixed_look: Variant = null  # Vector2 en [-1, 1] para pruebas (--look=x,y)

var _dragging := false
var _drag_offset := Vector2i.ZERO
var _press_at := Vector2i.ZERO


func _ready() -> void:
	_write_pid_file()
	_setup_window()
	_load_avatar()
	_load_clips()
	_api = ApiClient.new()
	add_child(_api)
	_bubble = Bubble.new()
	_bubble.visible = false
	add_child(_bubble)
	_setup_menu()
	if not _has_test_args():
		get_tree().create_timer(1.0).timeout.connect(play_role.bind("greet"))
	_handle_screenshot_arg()


# La app MAUI lee este archivo para saber si la mascota ya está abierta (botón "Abrir
# mascota") sin confundirla con otro proceso de Godot, como el editor.
const PID_PATH := "user://mascot.pid"

func _write_pid_file() -> void:
	var file := FileAccess.open(PID_PATH, FileAccess.WRITE)
	if file:
		file.store_string(str(OS.get_process_id()))


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST or what == NOTIFICATION_PREDELETE:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(PID_PATH))


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
		_map_humanoid(gltf_state)
	for node in avatar.find_children("*", "MeshInstance3D", true, false):
		_avatar_meshes.append(node as MeshInstance3D)
	_map_expressions(gltf_state)
	_update_mouse_passthrough()


# Huesos humanoides desde la extensión VRM del modelo (VRMC_vrm en 1.0; VRM en 0.x).
func _map_humanoid(state: GLTFState) -> void:
	var ext: Dictionary = state.json.get("extensions", {})
	var bones := {}
	if ext.has("VRMC_vrm"):
		var human: Dictionary = ext.VRMC_vrm.get("humanoid", {}).get("humanBones", {})
		for bone_name in human:
			bones[bone_name] = int(human[bone_name].get("node", -1))
	elif ext.has("VRM"):
		for entry in ext.VRM.get("humanoid", {}).get("humanBones", []):
			bones[entry.get("bone")] = int(entry.get("node", -1))

	var nodes := state.get_nodes()
	for bone_name in bones:
		var node_idx: int = bones[bone_name]
		if node_idx < 0 or node_idx >= nodes.size():
			continue
		var idx := _skeleton.find_bone((nodes[node_idx] as GLTFNode).resource_name)
		if idx < 0 and state.get_scene_node(node_idx):
			idx = _skeleton.find_bone(state.get_scene_node(node_idx).name)
		if idx >= 0:
			_human[bone_name] = idx

	for bone_name in ARM_FOLLOWERS:
		var found := []
		for follower in ARM_FOLLOWERS[bone_name]:
			var idx := _skeleton.find_bone(follower)
			if idx >= 0:
				found.append(idx)
		_followers[bone_name] = found

	if _human.has("hips"):
		_hips_height = maxf(_skeleton.get_bone_global_rest(_human.hips).origin.y, 0.01)


# Expresiones VRM 1.0: cada preset mueve uno o más blend shapes con un peso. Las mallas se
# buscan por nombre: get_scene_node() devuelve las ImporterMeshInstance3D originales, que
# generate_scene() ya reemplazó (y liberó) por MeshInstance3D.
func _map_expressions(state: GLTFState) -> void:
	var vrm: Dictionary = state.json.get("extensions", {}).get("VRMC_vrm", {})
	var presets: Dictionary = vrm.get("expressions", {}).get("preset", {})
	var mesh_by_name := {}
	for mesh in _avatar_meshes:
		mesh_by_name[str(mesh.name)] = mesh
	var nodes := state.get_nodes()
	for preset in presets:
		var binds := []
		for bind in presets[preset].get("morphTargetBinds", []):
			var node_idx := int(bind.get("node", -1))
			if node_idx < 0 or node_idx >= nodes.size():
				continue
			var mesh: MeshInstance3D = mesh_by_name.get((nodes[node_idx] as GLTFNode).resource_name)
			if mesh:
				binds.append([mesh, int(bind.get("index", 0)), float(bind.get("weight", 1.0))])
		if not binds.is_empty():
			_expressions[preset] = binds


func _set_expression(preset: String, value: float) -> void:
	for bind in _expressions.get(preset, []):
		var mesh: MeshInstance3D = bind[0]
		if bind[1] < mesh.get_blend_shape_count():
			mesh.set_blend_shape_value(bind[1], value * bind[2])


# --- Clips VRMA -----------------------------------------------------------------

func _load_clips() -> void:
	var dir := DirAccess.open(ANIMATIONS_DIR)
	if dir == null:
		return
	for file in dir.get_files():
		if file.get_extension().to_lower() != "vrma":
			continue
		var clip = VrmaClip.load_file(ANIMATIONS_DIR.path_join(file))
		if clip:
			_clips[clip.name] = clip


func play_clip(clip_name: String) -> void:
	if not _clips.has(clip_name):
		return
	_clip = _clips[clip_name]
	_clip_t = 0.0


## Dispara la animación de un evento ("greet", "celebrate", "click"); ver CLIP_ROLES.
func play_role(role: String) -> void:
	var candidates: Array = CLIP_ROLES.get(role, []).filter(func(n): return _clips.has(n))
	if candidates.is_empty():
		candidates = _clips.keys()
	if candidates.is_empty():
		return
	play_clip(candidates.pick_random() if role == "click" else candidates[0])


# Peso del clip activo: entra y sale con fundido para no saltar desde la pose procedural.
func _clip_weight() -> float:
	if _clip == null:
		return 0.0
	var fade_in := clampf(_clip_t / CLIP_FADE_IN, 0.0, 1.0)
	var fade_out := clampf((_clip.duration - _clip_t) / CLIP_FADE_OUT, 0.0, 1.0)
	return smoothstep(0.0, 1.0, minf(fade_in, fade_out))


func _process(delta: float) -> void:
	_time += delta
	if _clip:
		_clip_t += delta
		if _clip_t >= _clip.duration:
			_clip = null
	if _skeleton:
		_animate_body(delta)
	_animate_face(delta)


func _animate_body(delta: float) -> void:
	var pose := _procedural_pose()
	var weight := _clip_weight()
	var hips_offset := Vector3.ZERO
	if weight > 0.0:
		var clip_pose: Dictionary = _clip.sample_bones(_clip_t)
		for bone_name in clip_pose:
			var base: Quaternion = pose.get(bone_name, Quaternion.IDENTITY)
			pose[bone_name] = base.slerp(clip_pose[bone_name], weight)
		hips_offset = _clip.sample_hips_offset(_clip_t) * (_hips_height / _clip.hips_rest_height) * weight
		_clip_expressions = _clip.sample_expressions(_clip_t)
	else:
		_clip_expressions = {}

	# Mirar al cursor encima de todo; mientras suena un clip, la mirada cede casi todo el
	# control para que se note el gesto de la animación.
	var look_rot := _look_rotation(delta)
	var look_amount := 1.0 - 0.75 * weight
	pose.neck = Quaternion.IDENTITY.slerp(look_rot, 0.4 * look_amount) * pose.get("neck", Quaternion.IDENTITY)
	pose.head = Quaternion.IDENTITY.slerp(look_rot, 0.6 * look_amount) * pose.get("head", Quaternion.IDENTITY)

	var posed_now := {}
	for bone_name in pose:
		if _human.has(bone_name):
			_pose_bone(_human[bone_name], pose[bone_name])
			posed_now[bone_name] = true
			for follower in _followers.get(bone_name, []):
				_pose_bone(follower, pose[bone_name])
	# Lo que movió el clip y ya no se mueve (piernas, dedos…) vuelve al reposo.
	for bone_name in _posed:
		if not posed_now.has(bone_name) and _human.has(bone_name):
			_pose_bone(_human[bone_name], Quaternion.IDENTITY)
	_posed = posed_now

	if _human.has("hips"):
		var idx: int = _human.hips
		var parent := _skeleton.get_bone_parent(idx)
		var parent_basis := _skeleton.get_bone_global_rest(parent).basis if parent >= 0 else Basis.IDENTITY
		_skeleton.set_bone_pose_position(idx, _skeleton.get_bone_rest(idx).origin + parent_basis.inverse() * hips_offset)


# Pose procedural en el espacio normalizado (rotaciones en ejes del mundo respecto del
# reposo en T): brazos abajo, respiración y balanceo.
func _procedural_pose() -> Dictionary:
	var breath := sin(_time * TAU / BREATH_PERIOD)
	var sway := sin(_time * TAU / (BREATH_PERIOD * 2.3))
	# Vector3.BACK es +Z: el avatar mira hacia la cámara y su brazo izquierdo apunta a +X.
	var shoulder := deg_to_rad(SHOULDER_DOWN_DEGREES)
	var arm := deg_to_rad(ARM_DOWN_DEGREES + 1.5 * breath)
	return {
		hips = Quaternion(Vector3.BACK, deg_to_rad(0.8) * sway),
		spine = Quaternion(Vector3.RIGHT, deg_to_rad(-0.8) * breath),
		chest = Quaternion(Vector3.RIGHT, deg_to_rad(-1.2) * breath),
		leftShoulder = Quaternion(Vector3.BACK, -shoulder),
		rightShoulder = Quaternion(Vector3.BACK, shoulder),
		leftUpperArm = Quaternion(Vector3.BACK, -arm),
		rightUpperArm = Quaternion(Vector3.BACK, arm),
		leftLowerArm = Quaternion(Vector3.UP, deg_to_rad(ELBOW_BEND_DEGREES)),
		rightLowerArm = Quaternion(Vector3.UP, deg_to_rad(-ELBOW_BEND_DEGREES)),
	}


# Mirar al cursor: posición del mouse en pantalla relativa a la cabeza.
func _look_rotation(delta: float) -> Quaternion:
	var head_screen := Vector2(get_window().position) + Vector2(WINDOW_SIZE) * Vector2(0.5, 0.25)
	var to_mouse := Vector2(DisplayServer.mouse_get_position()) - head_screen
	var target := Vector2(
		clampf(to_mouse.x / 600.0, -1.0, 1.0),
		clampf(-to_mouse.y / 400.0, -1.0, 1.0))
	if _fixed_look != null:
		target = _fixed_look
	target *= Vector2(LOOK_MAX_YAW, LOOK_MAX_PITCH)
	_look = _look.lerp(target, clampf(LOOK_SMOOTHING * delta, 0.0, 1.0))
	return Quaternion(Vector3.UP, _look.x) * Quaternion(Vector3.RIGHT, -_look.y)


# Aplica una rotación normalizada (en ejes del esqueleto: X derecha de la pantalla, Y arriba,
# Z hacia la cámara, respecto del reposo) llevándola a los ejes locales del hueso: algunos
# huesos (la cabeza) tienen el reposo rotado y girar sobre su "Y" local la inclinaba.
# Encadenado por la jerarquía, esto reproduce exactamente el esqueleto normalizado de VRM.
func _pose_bone(idx: int, rotation_delta: Quaternion) -> void:
	var rest := _skeleton.get_bone_rest(idx).basis.get_rotation_quaternion()
	var global_rest := _skeleton.get_bone_global_rest(idx).basis.get_rotation_quaternion()
	_skeleton.set_bone_pose_rotation(idx, rest * (global_rest.inverse() * rotation_delta * global_rest))


# Parpadeo procedural + expresiones del clip activo (sonrisa, sorpresa…).
func _animate_face(delta: float) -> void:
	var blink := 0.0
	if _blink_t < 0.0:
		_next_blink -= delta
		if _next_blink <= 0.0:
			_blink_t = 0.0
	else:
		_blink_t += delta
		blink = sin(clampf(_blink_t / BLINK_DURATION, 0.0, 1.0) * PI)
		if _blink_t >= BLINK_DURATION:
			blink = 0.0
			_blink_t = -1.0
			_next_blink = randf_range(2.5, 6.0)

	var weight := _clip_weight()
	for preset in _expressions:
		var value: float = _clip_expressions.get(preset, 0.0) * weight
		if preset == "blink":
			value = maxf(value, blink)
		_set_expression(preset, value)


# --- Mouse: arrastrar y menú -------------------------------------------------

func _setup_menu() -> void:
	_menu.theme = LunaTheme.get_theme()
	_menu.add_separator("Crear")
	_menu.add_item("Alarma…", MenuItem.NEW_ALARM)
	_menu.add_item("Recordatorio…", MenuItem.NEW_REMINDER)
	_menu.add_item("Tarea…", MenuItem.NEW_TASK)
	_menu.add_item("Nota…", MenuItem.NEW_NOTE)
	_menu.add_item("Comentario de proyecto…", MenuItem.PROJECT_COMMENT)
	_menu.add_separator("Ver")
	_menu.add_item("Estado de servidores", MenuItem.SERVERS)
	_menu.add_separator()
	_menu.add_item("Volver a la esquina", MenuItem.RESET_POSITION)
	_menu.add_item("Salir", MenuItem.QUIT)
	_menu.id_pressed.connect(_on_menu_id_pressed)


func _window_rect() -> Rect2i:
	return Rect2i(get_window().position, get_window().size)


func say(text: String, kind := "info", seconds := 5.0) -> void:
	_bubble.say(text, _window_rect(), kind, seconds)


# Abre una ventanita (formulario o vista). Sin conexión con la Api, avisa en la burbuja en
# vez de abrir un formulario que no podría guardar.
func _open(window_script: GDScript, extra: Dictionary = {}) -> void:
	if not _api.is_linked():
		say(ApiClient.NOT_LINKED_MESSAGE, "error", 7.0)
		return
	var window: Window = window_script.new()
	# Oculta hasta terminar de configurarla (popup_next_to la muestra): una Window entra a la
	# escena visible por defecto y Godot crea la ventana nativa con la configuración de fábrica.
	window.visible = false
	add_child(window)
	if window.has_signal("saved"):
		window.saved.connect(func(message: String):
			say(message, "ok")
			play_role("celebrate"))
	window.open(_api, _window_rect(), extra)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button := event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_LEFT:
			if button.pressed:
				_dragging = true
				_press_at = DisplayServer.mouse_get_position()
				_drag_offset = _press_at - get_window().position
			elif _dragging:
				_dragging = false
				# Un clic sin arrastre es una caricia: reacciona con un gesto.
				if (DisplayServer.mouse_get_position() - _press_at).length() < 5:
					play_role("click")
				else:
					_save_position()
		elif button.button_index == MOUSE_BUTTON_RIGHT and button.pressed:
			_menu.popup(Rect2i(DisplayServer.mouse_get_position(), Vector2i.ZERO))
	elif event is InputEventMouseMotion and _dragging:
		get_window().position = DisplayServer.mouse_get_position() - _drag_offset


func _on_menu_id_pressed(id: int) -> void:
	match id:
		MenuItem.NEW_ALARM:
			_open(AlarmForm)
		MenuItem.NEW_REMINDER:
			_open(TaskForm, { reminder = true })
		MenuItem.NEW_TASK:
			_open(TaskForm, { reminder = false })
		MenuItem.NEW_NOTE:
			_open(NoteForm)
		MenuItem.PROJECT_COMMENT:
			_open(CommentForm)
		MenuItem.SERVERS:
			_open(ServersWindow)
		MenuItem.RESET_POSITION:
			get_window().position = _corner_position()
			_save_position()
		MenuItem.QUIT:
			get_tree().quit()


# --- Pruebas: captura del encuadre y mirada fija -----------------------------
# Uso: Godot --path DesktopMascot -- --screenshot=C:\ruta\captura.png --look=1,0
# (--look fija la mirada en [-1, 1]: x a la derecha de la pantalla, y arriba.)
# --clip=<nombre> --at=<segundos>: reproduce ese clip y captura en ese instante.

func _has_test_args() -> bool:
	return Array(OS.get_cmdline_user_args()).any(func(a): return a.begins_with("--"))


func _handle_screenshot_arg() -> void:
	var capture_delay := 1.5
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--look="):
			var parts := arg.trim_prefix("--look=").split(",")
			_fixed_look = Vector2(parts[0].to_float(), parts[1].to_float())
		elif arg.begins_with("--clip="):
			play_clip(arg.trim_prefix("--clip="))
			print("Clips disponibles: ", _clips.keys())
		elif arg.begins_with("--at="):
			capture_delay = arg.trim_prefix("--at=").to_float()
	# --open=alarm|reminder|task|note|servers abre esa ventanita (aunque no haya conexión con
	# la Api) y la captura es de esa ventana en vez de la mascota.
	var target: Viewport = get_viewport()
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--open="):
			var forms := { alarm = [AlarmForm, {}], reminder = [TaskForm, { reminder = true }],
				task = [TaskForm, { reminder = false }], note = [NoteForm, {}], servers = [ServersWindow, {}],
				comment = [CommentForm, {}] }
			var entry: Array = forms.get(arg.trim_prefix("--open="), [])
			if not entry.is_empty():
				var window: Window = entry[0].new()
				window.visible = false
				add_child(window)
				window.open(_api, _window_rect(), entry[1])
				target = window
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--screenshot="):
			await get_tree().create_timer(capture_delay).timeout
			await RenderingServer.frame_post_draw
			var path := arg.trim_prefix("--screenshot=")
			target.get_texture().get_image().save_png(path)
			print("Captura guardada en ", path)
			get_tree().quit()
