extends Node3D

## Mascota de escritorio de Artemis: ventana transparente, sin bordes y siempre
## encima, con un avatar VRM. Dos capas de animación que se mezclan:
## - procedural: brazos abajo, respiración, balanceo, parpadeo y mirada al cursor;
## - clips (.vrma o .fbx de Mixamo, ver anim_clip.gd) que se disparan por categoría de evento.
## El avatar activo es un perfil (avatars/<id>/avatar.json): qué modelo cargar y qué
## animaciones de la biblioteca compartida (res://animations) usar en cada categoría.
## Solo la silueta del personaje recibe el mouse; el resto de la ventana deja
## pasar los clics al escritorio.

# De fábrica en el repo (res://) y lo que el usuario configura desde la app en sus datos
# (user://): un avatar.json en user://avatars/<id>/ reemplaza al de res://avatars/<id>/, y
# las animaciones importadas viven en user://animations. Ver AsistenteArtemis.Client.Maui
# (MascotStudio.cs y la pantalla Identidad).
const AVATARS_DIR := "res://avatars"
const USER_AVATARS_DIR := "user://avatars"
const DEFAULT_AVATAR := "muestra"
const ANIMATIONS_DIR := "res://animations"
const USER_ANIMATIONS_DIR := "user://animations"
const CONFIG_PATH := "user://mascot.cfg"
# La app deja aquí pedidos para la mascota ({"id": n, "play": "<animación>"}).
const COMMAND_PATH := "user://command.json"
const WATCH_INTERVAL := 1.0
const WINDOW_SIZE := Vector2i(260, 400)
const SCREEN_MARGIN := 12

const AXES := {
	"PositiveX": Vector3.RIGHT, "NegativeX": Vector3.LEFT,
	"PositiveY": Vector3.UP, "NegativeY": Vector3.DOWN,
	"PositiveZ": Vector3.BACK, "NegativeZ": Vector3.FORWARD,
	"X": Vector3.RIGHT, "Y": Vector3.UP, "Z": Vector3.BACK,
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
const HIPS_HORIZONTAL_FACTOR := 0.3

# Categorías de animación: el catálogo (id, nombre y si se dispara sola) está en
# res://avatars/categories.json, compartido con la app. Cada avatar.json asigna a cada
# categoría una lista de animaciones (por nombre de archivo, sin extensión) y se elige una al
# azar. Las "pending" esperan que la Api avise del evento (PresenceHub, R5); las que agrega el
# usuario no se disparan solas (por ahora, solo se prueban a mano).
# Si una categoría no tiene clips, se prueba con esta.
const CATEGORY_FALLBACK := {
	"task_created": "celebrate", "reminder_created": "celebrate", "alarm_created": "celebrate",
	"note_saved": "celebrate", "comment_saved": "celebrate", "task_done": "celebrate",
}
const IDLE_MIN_SECONDS := 90.0
const IDLE_MAX_SECONDS := 240.0

enum MenuItem { NEW_ALARM, NEW_NOTE, NEW_TASK, NEW_REMINDER, PROJECT_COMMENT, SERVERS, RESET_POSITION, QUIT }
const ANIMATION_MENU_BASE := 1000  # ids del submenú "Animaciones": base + índice del clip

const LunaTheme := preload("res://luna_theme.gd")
const ApiClient := preload("res://api_client.gd")
const Bubble := preload("res://bubble.gd")
const ServersWindow := preload("res://servers_window.gd")
const AlarmForm := preload("res://forms/alarm_form.gd")
const NoteForm := preload("res://forms/note_form.gd")
const TaskForm := preload("res://forms/task_form.gd")
const CommentForm := preload("res://forms/comment_form.gd")
const AnimClip := preload("res://anim_clip.gd")

@onready var _camera: Camera3D = $Camera3D
@onready var _menu: PopupMenu = $ContextMenu
var _animations_menu: PopupMenu

var _avatar := {}       # perfil activo (avatar.json)

var _api: ApiClient
var _bubble: Bubble

var _skeleton: Skeleton3D
var _human := {}        # hueso humanoide VRM ("hips", "leftUpperArm"…) -> índice en el esqueleto
var _constraints := []  # VRMC_node_constraint (aim/roll) ordenadas de padres a hijos
var _expressions := {}  # preset VRM ("blink", "happy"…) -> [[MeshInstance3D, blend shape, peso], …]
var _hips_height := 1.0
var _posed := {}        # huesos tocados en el último frame (para devolverlos al reposo)
var _avatar_meshes: Array[MeshInstance3D] = []
var _avatar_root: Node3D
var _model_path := ""
var _vrm0 := false      # VRM 0.x: mira hacia -Z y sus expresiones se mapean distinto

var _clips := {}        # nombre de archivo (sin extensión) -> AnimClip
var _clip: AnimClip
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
	_avatar = _load_avatar_profile()
	_load_avatar(_avatar.model)
	_load_clips()
	_api = ApiClient.new()
	add_child(_api)
	_bubble = Bubble.new()
	_bubble.visible = false
	add_child(_bubble)
	_setup_menu()
	if not _has_test_args():
		get_tree().create_timer(1.0).timeout.connect(play_role.bind("greet"))
		_schedule_idle()
		_start_watching()
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
	# Cargar antes de guardar: mascot.cfg también guarda el avatar elegido.
	var cfg := ConfigFile.new()
	cfg.load(CONFIG_PATH)
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

# Perfil del avatar activo: [avatar] id en mascot.cfg (o "muestra"). Gana el avatar.json de
# user:// (lo que se configuró en la app) sobre el de fábrica. "model" puede ser res://, una
# ruta absoluta o relativa a la carpeta del avatar (la de fábrica).
func _load_avatar_profile() -> Dictionary:
	var cfg := ConfigFile.new()
	cfg.load(CONFIG_PATH)
	var avatar_id: String = cfg.get_value("avatar", "id", DEFAULT_AVATAR)
	var folder := AVATARS_DIR.path_join(avatar_id)
	var profile_path := _profile_path(avatar_id)
	var profile = JSON.parse_string(FileAccess.get_file_as_string(profile_path)) if FileAccess.file_exists(profile_path) else null
	if not profile is Dictionary:
		if avatar_id != DEFAULT_AVATAR:
			push_warning("Avatar «%s» sin avatar.json válido; uso «%s»" % [avatar_id, DEFAULT_AVATAR])
			cfg.set_value("avatar", "id", DEFAULT_AVATAR)
			cfg.save(CONFIG_PATH)
			return _load_avatar_profile()
		profile = {}
	var model: String = profile.get("model", "res://models/sample.vrm")
	if model.is_relative_path() and not model.begins_with("res://"):
		model = folder.path_join(model)
	profile.model = model
	profile.id = avatar_id
	if not profile.get("animations") is Dictionary:
		profile.animations = {}
	return profile


func _profile_path(avatar_id: String) -> String:
	var user_path := USER_AVATARS_DIR.path_join(avatar_id).path_join("avatar.json")
	return user_path if FileAccess.file_exists(user_path) else AVATARS_DIR.path_join(avatar_id).path_join("avatar.json")


## Carga (o reemplaza) el modelo. Devuelve el GLTFState para el informe de compatibilidad, o
## null si no se pudo leer (en ese caso queda el modelo anterior).
func _load_avatar(model_path: String) -> GLTFState:
	var gltf_doc := GLTFDocument.new()
	var gltf_state := GLTFState.new()
	var err := gltf_doc.append_from_file(model_path, gltf_state)
	if err != OK:
		push_error("Error cargando el modelo %s: %s" % [model_path, error_string(err)])
		return null
	var avatar := gltf_doc.generate_scene(gltf_state)
	if avatar == null:
		push_error("generate_scene devolvió null para %s" % model_path)
		return null

	if _avatar_root:
		_avatar_root.queue_free()
	_avatar_root = avatar
	_skeleton = null
	_human = {}
	_expressions = {}
	_constraints = []
	_posed = {}
	_avatar_meshes = []
	_clip = null
	_model_path = model_path

	var ext: Dictionary = gltf_state.json.get("extensions", {})
	_vrm0 = not ext.has("VRMC_vrm") and ext.has("VRM")
	# VRM 0.x mira hacia -Z: se gira para que mire a la cámara, y las rotaciones normalizadas
	# (convención +Z de VRM 1.0) se convierten en _pose_bone.
	if _vrm0:
		avatar.rotate_y(PI)
	add_child(avatar)

	var skeletons := avatar.find_children("*", "Skeleton3D", true, false)
	if not skeletons.is_empty():
		_skeleton = skeletons[0]
		_map_humanoid(gltf_state)
	for node in avatar.find_children("*", "MeshInstance3D", true, false):
		_avatar_meshes.append(node as MeshInstance3D)
	_map_expressions(gltf_state)
	_frame_camera()
	_update_mouse_passthrough()
	return gltf_state


# Encuadra el cuerpo entero en la ventana, sea cual sea la altura del modelo.
func _frame_camera() -> void:
	if _avatar_meshes.is_empty():
		return
	var box := _avatar_aabb()
	var half_fov := deg_to_rad(_camera.fov) / 2.0
	var height := box.size.y * 1.08
	var distance := (height / 2.0) / tan(half_fov)
	var center := box.get_center()
	_camera.rotation = Vector3.ZERO
	_camera.position = Vector3(center.x, center.y, center.z + distance + box.size.z / 2.0)


func _avatar_aabb() -> AABB:
	var box := AABB()
	var first := true
	for mesh in _avatar_meshes:
		var world := mesh.global_transform * mesh.get_aabb()
		box = world if first else box.merge(world)
		first = false
	return box


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

	_map_constraints(state)

	if _human.has("hips"):
		_hips_height = maxf(_skeleton.get_bone_global_rest(_human.hips).origin.y, 0.01)


# VRMC_node_constraint: huesos auxiliares que en el modelo original siguen a otros (la manga
# apunta al codo, el short al muslo, el antebrazo copia parte del giro de la mano). Sin esto la
# ropa se queda quieta mientras el cuerpo se mueve.
func _map_constraints(state: GLTFState) -> void:
	var json_nodes: Array = state.json.get("nodes", [])
	var gltf_nodes := state.get_nodes()
	var bone_of := func(node_idx: int) -> int:
		if node_idx < 0 or node_idx >= gltf_nodes.size():
			return -1
		return _skeleton.find_bone((gltf_nodes[node_idx] as GLTFNode).resource_name)

	for i in json_nodes.size():
		var constraint: Dictionary = json_nodes[i].get("extensions", {}).get("VRMC_node_constraint", {}).get("constraint", {})
		for kind in ["aim", "roll"]:
			if not constraint.has(kind):
				continue
			var spec: Dictionary = constraint[kind]
			var dst: int = bone_of.call(i)
			var src: int = bone_of.call(int(spec.get("source", -1)))
			var axis: Vector3 = AXES.get(spec.get("aimAxis" if kind == "aim" else "rollAxis", ""), Vector3.ZERO)
			if dst < 0 or src < 0 or axis == Vector3.ZERO:
				continue
			_constraints.append({
				kind = kind, dst = dst, src = src, axis = axis,
				weight = float(spec.get("weight", 1.0)),
				dst_rest = _skeleton.get_bone_rest(dst).basis.get_rotation_quaternion(),
				src_rest = _skeleton.get_bone_rest(src).basis.get_rotation_quaternion(),
				depth = _bone_depth(dst),
			})
	# Padres primero: una restricción puede colgar de otro hueso restringido.
	_constraints.sort_custom(func(a, b): return a.depth < b.depth)


func _bone_depth(idx: int) -> int:
	var depth := 0
	while _skeleton.get_bone_parent(idx) >= 0:
		idx = _skeleton.get_bone_parent(idx)
		depth += 1
	return depth


# Misma matemática que three-vrm (VRMAimConstraint / VRMRollConstraint).
func _apply_constraints() -> void:
	for c in _constraints:
		var dst_rest: Quaternion = c.dst_rest
		var pose: Quaternion
		if c.kind == "aim":
			# Girar el hueso para que su eje "aimAxis" apunte hacia el hueso fuente.
			var parent := _skeleton.get_bone_parent(c.dst)
			var parent_pose := _skeleton.get_bone_global_pose(parent) if parent >= 0 else Transform3D.IDENTITY
			var parent_rot := parent_pose.basis.get_rotation_quaternion()
			var dst_pos := (parent_pose * _skeleton.get_bone_rest(c.dst)).origin
			var to := _skeleton.get_bone_global_pose(c.src).origin - dst_pos
			var from: Vector3 = (parent_rot * dst_rest) * c.axis
			if to.length_squared() < 1e-10:
				continue
			var turn := Quaternion(from.normalized(), to.normalized())
			var target := parent_rot.inverse() * (turn * parent_rot * dst_rest)
			pose = dst_rest.slerp(target, c.weight)
		else:
			# Copiar solo el giro de la fuente alrededor de "rollAxis" (sin el balanceo).
			var src_delta := dst_rest.inverse() * _skeleton.get_bone_pose_rotation(c.src) * (c.src_rest as Quaternion).inverse() * dst_rest
			var swing_inv := Quaternion((src_delta * c.axis).normalized(), c.axis)
			var twist := swing_inv * src_delta
			pose = dst_rest * Quaternion.IDENTITY.slerp(twist, c.weight)
		_skeleton.set_bone_pose_rotation(c.dst, pose)


# Expresiones VRM 1.0: cada preset mueve uno o más blend shapes con un peso. Las mallas se
# buscan por nombre: get_scene_node() devuelve las ImporterMeshInstance3D originales, que
# generate_scene() ya reemplazó (y liberó) por MeshInstance3D.
func _map_expressions(state: GLTFState) -> void:
	var mesh_by_name := {}
	for mesh in _avatar_meshes:
		mesh_by_name[str(mesh.name)] = mesh
	if _vrm0:
		_map_expressions_vrm0(state, mesh_by_name)
		return
	var vrm: Dictionary = state.json.get("extensions", {}).get("VRMC_vrm", {})
	var presets: Dictionary = vrm.get("expressions", {}).get("preset", {})
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


# VRM 0.x: blendShapeMaster con nombres de preset propios (joy, sorrow, a…) y binds por índice
# de malla con peso 0-100. Se traducen a los nombres de VRM 1.0, que son los que usan las
# animaciones VRMA.
const VRM0_PRESETS := {
	"joy": "happy", "angry": "angry", "sorrow": "sad", "fun": "relaxed", "neutral": "neutral",
	"blink": "blink", "blink_l": "blinkLeft", "blink_r": "blinkRight",
	"a": "aa", "i": "ih", "u": "ou", "e": "ee", "o": "oh",
	"lookup": "lookUp", "lookdown": "lookDown", "lookleft": "lookLeft", "lookright": "lookRight",
}

func _map_expressions_vrm0(state: GLTFState, mesh_by_name: Dictionary) -> void:
	var json: Dictionary = state.json
	var groups: Array = json.get("extensions", {}).get("VRM", {}).get("blendShapeMaster", {}).get("blendShapeGroups", [])
	var json_nodes: Array = json.get("nodes", [])
	for group in groups:
		var preset: String = VRM0_PRESETS.get(str(group.get("presetName", "")).to_lower(), "")
		if preset.is_empty():
			continue
		var binds := []
		for bind in group.get("binds", []):
			# Todas las instancias de esa malla (una malla puede estar en varios nodos).
			for node in json_nodes:
				if int(node.get("mesh", -1)) == int(bind.get("mesh", -2)):
					var mesh: MeshInstance3D = mesh_by_name.get(str(node.get("name", "")))
					if mesh:
						binds.append([mesh, int(bind.get("index", 0)), float(bind.get("weight", 100.0)) / 100.0])
		if not binds.is_empty():
			_expressions[preset] = binds


func _set_expression(preset: String, value: float) -> void:
	for bind in _expressions.get(preset, []):
		var mesh: MeshInstance3D = bind[0]
		if bind[1] < mesh.get_blend_shape_count():
			mesh.set_blend_shape_value(bind[1], value * bind[2])


# --- Clips VRMA -----------------------------------------------------------------

# Biblioteca compartida (sirve para cualquier avatar VRM) + animaciones propias del avatar,
# si su carpeta trae una subcarpeta animations/.
func _load_clips() -> void:
	_clips.clear()
	for dir_path in [ANIMATIONS_DIR, USER_ANIMATIONS_DIR, AVATARS_DIR.path_join(_avatar.id).path_join("animations")]:
		var dir := DirAccess.open(dir_path)
		if dir == null:
			continue
		for file in dir.get_files():
			if not AnimClip.is_supported(file):
				continue
			var clip = AnimClip.load_file(dir_path.path_join(file))
			if clip:
				_clips[clip.name] = clip
	var names := _clips.keys()
	names.sort()
	print("Animaciones cargadas: ", names)


func play_clip(clip_name: String) -> void:
	if not _clips.has(clip_name):
		return
	_clip = _clips[clip_name]
	_clip_t = 0.0


## Dispara una animación de la categoría (ver ANIMATION_CATEGORIES), al azar entre las que el
## avatar asigna; si no tiene ninguna cargada, prueba la categoría de respaldo.
func play_role(category: String) -> void:
	var assigned: Array = _avatar.animations.get(category, [])
	var candidates := assigned.filter(func(n): return _clips.has(n))
	if candidates.is_empty():
		if CATEGORY_FALLBACK.has(category):
			play_role(CATEGORY_FALLBACK[category])
		return
	play_clip(candidates.pick_random())


# La app configura la mascota escribiendo archivos en user:// (avatar.json, animaciones
# importadas, command.json). Se revisan cada segundo: es barato (solo fechas y nombres) y
# evita montar un canal de comunicación para algo que cambia pocas veces.
var _watch_signature := ""
var _last_command_id := -1

func _start_watching() -> void:
	_watch_signature = _config_signature()
	_last_command_id = int(_read_command().get("id", -1))
	var timer := Timer.new()
	timer.wait_time = WATCH_INTERVAL
	timer.timeout.connect(_on_watch)
	add_child(timer)
	timer.start()


func _config_signature() -> String:
	var parts := [str(FileAccess.get_modified_time(_profile_path(_avatar.get("id", DEFAULT_AVATAR))))]
	var dir := DirAccess.open(USER_ANIMATIONS_DIR)
	if dir:
		for file in dir.get_files():
			parts.append(file)
	return "|".join(parts)


func _read_command() -> Dictionary:
	if not FileAccess.file_exists(COMMAND_PATH):
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(COMMAND_PATH))
	return parsed if parsed is Dictionary else {}


func _on_watch() -> void:
	var signature := _config_signature()
	if signature != _watch_signature:
		_watch_signature = signature
		# El modelo no se recarga en caliente (cambiar de avatar requiere reiniciar la mascota);
		# las categorías y la biblioteca de animaciones sí.
		var profile := _load_avatar_profile()
		_avatar.animations = profile.animations
		_load_clips()
		_fill_animations_menu()
	var command := _read_command()
	var command_id := int(command.get("id", -1))
	if command_id != _last_command_id:
		_last_command_id = command_id
		if command.has("play"):
			play_clip(str(command.play))
		elif command.has("preview_model"):
			_switch_model(str(command.preview_model), command_id)
			say("Probando modelo: %s" % str(command.preview_model).get_file(), "info", 4.0)
		elif command.has("restore_model") or command.has("reload_avatar"):
			_avatar = _load_avatar_profile()
			_load_clips()
			_fill_animations_menu()
			_switch_model(_avatar.model, command_id)


# --- Probador de modelos ------------------------------------------------------------------
# La app pide cargar un modelo (preview_model) y lee el informe en user://model_report.json:
# formato, huesos humanoides, expresiones, peso frente al presupuesto del equipo y, por cada
# animación cargada, qué huesos usa que el modelo no tiene.

const REPORT_PATH := "user://model_report.json"
const REQUIRED_BONES := ["hips", "spine", "head",
	"leftUpperArm", "leftLowerArm", "leftHand", "rightUpperArm", "rightLowerArm", "rightHand",
	"leftUpperLeg", "leftLowerLeg", "leftFoot", "rightUpperLeg", "rightLowerLeg", "rightFoot"]
const RECOMMENDED_BONES := ["neck", "chest", "leftShoulder", "rightShoulder",
	"leftIndexProximal", "rightIndexProximal", "leftThumbProximal", "rightThumbProximal"]
# Presupuesto para este equipo (Intel integrada, texturas sin comprimir en tiempo de ejecución).
const BUDGET_TRIANGLES := 70000
const BUDGET_MATERIALS := 20
const BUDGET_MAX_TEXTURE := 2048
const BUDGET_BIG_TEXTURES := 4

func _switch_model(path: String, command_id: int) -> void:
	var state := _load_avatar(path)
	var report := _build_report(state, path) if state else {
		ok = false, error = "No se pudo leer el archivo como glTF/VRM.", path = path }
	report.command_id = command_id
	var file := FileAccess.open(REPORT_PATH, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(report, "\t"))


func _build_report(state: GLTFState, path: String) -> Dictionary:
	var json: Dictionary = state.json
	var ext: Dictionary = json.get("extensions", {})
	var meta: Dictionary = ext.get("VRMC_vrm", {}).get("meta", ext.get("VRM", {}).get("meta", {}))

	var triangles := 0
	var vertices := 0
	var accessors: Array = json.get("accessors", [])
	for mesh in json.get("meshes", []):
		for prim in mesh.get("primitives", []):
			var count := int(accessors[int(prim.attributes.POSITION)].get("count", 0))
			vertices += count
			triangles += int(accessors[int(prim.indices)].get("count", 0)) / 3 if prim.has("indices") else count / 3

	var max_texture := 0
	var big_textures := 0
	for image in state.get_images():
		if image:
			var side := maxi(image.get_width(), image.get_height())
			max_texture = maxi(max_texture, side)
			if side >= BUDGET_MAX_TEXTURE:
				big_textures += 1

	var spring_joints := 0
	for spring in ext.get("VRMC_springBone", {}).get("springs", []):
		spring_joints += spring.get("joints", []).size()
	var uses_mtoon: bool = json.get("extensionsUsed", []).has("VRMC_materials_mtoon") or ext.has("VRM")

	var missing_required := REQUIRED_BONES.filter(func(b): return not _human.has(b))
	var missing_recommended := RECOMMENDED_BONES.filter(func(b): return not _human.has(b))
	var materials: int = json.get("materials", []).size()
	var vrm_version := "1.0" if ext.has("VRMC_vrm") else ("0.x" if ext.has("VRM") else "")

	var warnings := []
	var errors := []
	if vrm_version.is_empty():
		errors.append("No es un VRM: falta el esqueleto humanoide, así que no se puede animar. Conviértelo con Blender + VRM Add-on o UniVRM.")
	elif not missing_required.is_empty():
		errors.append("Faltan huesos humanoides obligatorios: %s." % ", ".join(missing_required))
	if _skeleton == null:
		errors.append("El modelo no tiene esqueleto (no está riggeado).")
	if vrm_version == "0.x":
		warnings.append("VRM 0.x: compatible (se gira y se traducen las expresiones), pero VRM 1.0 es lo recomendado.")
	if not _expressions.has("blink"):
		warnings.append("Sin expresión «blink»: no va a parpadear.")
	if not missing_recommended.is_empty():
		warnings.append("Huesos recomendados que faltan: %s (esas partes quedan rígidas)." % ", ".join(missing_recommended))
	if triangles > BUDGET_TRIANGLES:
		warnings.append("%d triángulos: más que el presupuesto (%d); puede consumir más." % [triangles, BUDGET_TRIANGLES])
	if materials > BUDGET_MATERIALS:
		warnings.append("%d materiales: más que el presupuesto (%d)." % [materials, BUDGET_MATERIALS])
	if max_texture > BUDGET_MAX_TEXTURE:
		warnings.append("Texturas de %d px: más que el máximo recomendado (%d)." % [max_texture, BUDGET_MAX_TEXTURE])
	if big_textures > BUDGET_BIG_TEXTURES:
		warnings.append("%d texturas de %d px o más: conviene bajar algunas a 1024." % [big_textures, BUDGET_MAX_TEXTURE])
	if spring_joints > 0:
		warnings.append("Tiene física de pelo/ropa (%d huesos), que la mascota aún no simula: se moverán rígidos." % spring_joints)
	if uses_mtoon:
		warnings.append("Usa sombreado toon (MToon), que la mascota aún no aplica: se verá con sombreado plano.")

	var animations := []
	var clip_names := _clips.keys()
	clip_names.sort()
	for clip_name in clip_names:
		var clip: AnimClip = _clips[clip_name]
		var bones: Array = clip.bone_names()
		var missing := bones.filter(func(b): return not _human.has(b))
		var missing_expr := clip.expression_names().filter(func(e): return not _expressions.has(e))
		animations.append({
			name = clip_name,
			bones = bones.size(),
			missing_bones = missing,
			missing_expressions = missing_expr,
			coverage = 100 if bones.is_empty() else roundi(100.0 * (bones.size() - missing.size()) / bones.size()),
		})

	return {
		ok = errors.is_empty(),
		error = " ".join(errors),
		path = path,
		file_mb = snappedf(FileAccess.get_file_as_bytes(path).size() / 1e6, 0.1) if not path.begins_with("res://") else 0.0,
		model = {
			name = str(meta.get("name", meta.get("title", path.get_file().get_basename()))),
			authors = meta.get("authors", [meta.get("author", "")]),
			license = str(meta.get("licenseUrl", meta.get("licenseName", ""))),
			vrm = vrm_version,
			triangles = triangles,
			vertices = vertices,
			materials = materials,
			max_texture = max_texture,
			big_textures = big_textures,
			humanoid_bones = _human.size(),
			missing_required = missing_required,
			missing_recommended = missing_recommended,
			expressions = _expressions.keys(),
			constraints = _constraints.size(),
			spring_joints = spring_joints,
		},
		warnings = warnings,
		animations = animations,
	}


# Cada tanto, si no está haciendo nada, una animación de reposo.
func _schedule_idle() -> void:
	get_tree().create_timer(randf_range(IDLE_MIN_SECONDS, IDLE_MAX_SECONDS)).timeout.connect(func():
		if _clip == null and not _dragging:
			play_role("idle")
		_schedule_idle())


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
		# La ventana es angosta: el desplazamiento lateral y de profundidad se atenúa para que
		# un baile no saque al avatar del cuadro; el vertical (saltos, agacharse) queda entero.
		hips_offset *= Vector3(HIPS_HORIZONTAL_FACTOR, 1.0, HIPS_HORIZONTAL_FACTOR)
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
	# Lo que movió el clip y ya no se mueve (piernas, dedos…) vuelve al reposo.
	for bone_name in _posed:
		if not posed_now.has(bone_name) and _human.has(bone_name):
			_pose_bone(_human[bone_name], Quaternion.IDENTITY)
	_posed = posed_now

	if _human.has("hips"):
		var idx: int = _human.hips
		var parent := _skeleton.get_bone_parent(idx)
		var parent_basis := _skeleton.get_bone_global_rest(parent).basis if parent >= 0 else Basis.IDENTITY
		if _vrm0:
			hips_offset = Vector3(-hips_offset.x, hips_offset.y, -hips_offset.z)
		_skeleton.set_bone_pose_position(idx, _skeleton.get_bone_rest(idx).origin + parent_basis.inverse() * hips_offset)

	# Al final: los huesos auxiliares (ropa, giros de antebrazo) siguen a la pose ya armada.
	_apply_constraints()


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
	if _vrm0:
		# Girar 180° en Y la convención (+Z de VRM 1.0 -> -Z de VRM 0.x), como three-vrm.
		rotation_delta = Quaternion(-rotation_delta.x, rotation_delta.y, -rotation_delta.z, rotation_delta.w)
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
	_animations_menu = PopupMenu.new()
	_animations_menu.theme = LunaTheme.get_theme()
	_menu.add_child(_animations_menu)
	_animations_menu.id_pressed.connect(_on_animation_menu_pressed)
	_menu.add_submenu_node_item("Animaciones", _animations_menu)
	_fill_animations_menu()
	_menu.add_separator()
	_menu.add_item("Volver a la esquina", MenuItem.RESET_POSITION)
	_menu.add_item("Salir", MenuItem.QUIT)
	_menu.id_pressed.connect(_on_menu_id_pressed)


# Submenú para probar cualquier animación cargada (con su duración).
func _fill_animations_menu() -> void:
	_animations_menu.clear()
	var names := _clips.keys()
	names.sort()
	if names.is_empty():
		_animations_menu.add_item("(no hay animaciones en DesktopMascot/animations)")
		_animations_menu.set_item_disabled(0, true)
		return
	for i in names.size():
		var clip: AnimClip = _clips[names[i]]
		_animations_menu.add_item("%s  ·  %.1f s" % [names[i], clip.duration], ANIMATION_MENU_BASE + i)
		_animations_menu.set_item_metadata(i, names[i])


func _on_animation_menu_pressed(id: int) -> void:
	var index := _animations_menu.get_item_index(id)
	if index >= 0:
		play_clip(str(_animations_menu.get_item_metadata(index)))


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
		window.saved.connect(func(message: String, category: String):
			say(message, "ok")
			play_role(category))
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
		elif arg.begins_with("--model="):
			# Igual que el probador de la app: carga el modelo y escribe user://model_report.json.
			_switch_model(arg.trim_prefix("--model="), 0)
			print("Informe: ", ProjectSettings.globalize_path(REPORT_PATH))
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
