extends RefCounted

## Una animación VRMA (glTF + extensión VRMC_vrm_animation) lista para aplicarse a cualquier
## avatar VRM, sin pasar por el importador del editor.
##
## Las rotaciones del archivo son locales a SU propio esqueleto, cuyo reposo no es neutro
## (las caderas traen, p. ej., [0.5, 0.5, -0.5, 0.5]). Se convierten al espacio "normalizado"
## (reposo = identidad, ejes del mundo) igual que three-vrm (VRMAnimationLoaderPlugin):
##     normalizada = rot_mundo_padre_reposo * q * rot_mundo_reposo⁻¹
## Así, en reposo la rotación normalizada es la identidad y main.gd la aplica a cada hueso
## del avatar con la misma conversión de ejes que usa para la animación procedural.

var name := ""
var duration := 0.0

var _anim: Animation
var _bones := {}        # humanoide -> { track, parent_world: Quaternion, world_inv: Quaternion }
var _expressions := {}  # preset VRM ("happy", "blink"…) -> track (posición: el peso va en x)
var _hips_track := -1
var _hips_rest_pos := Vector3.ZERO
var hips_rest_height := 1.0


static func load_file(path: String) -> RefCounted:
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	if doc.append_from_file(path, state) != OK:
		push_warning("No se pudo leer la animación %s" % path)
		return null
	var ext: Dictionary = state.json.get("extensions", {}).get("VRMC_vrm_animation", {})
	if ext.is_empty():
		push_warning("%s no es un VRMA (falta VRMC_vrm_animation)" % path)
		return null
	var root := doc.generate_scene(state)
	var players := root.find_children("*", "AnimationPlayer", true, false)
	if players.is_empty():
		root.free()
		return null
	var player: AnimationPlayer = players[0]
	var list := player.get_animation_list()
	if list.is_empty():
		root.free()
		return null

	var clip = load("res://vrma_clip.gd").new()
	clip.name = path.get_file().get_basename()
	clip._anim = player.get_animation(list[0]).duplicate()
	clip.duration = clip._anim.length
	clip._build(state, ext)
	root.free()
	return clip


func _build(state: GLTFState, ext: Dictionary) -> void:
	var nodes := state.get_nodes()

	# Rotación de reposo en el mundo de cada nodo, encadenando padres.
	var world_cache := {}
	var world_rot := func(self_ref: Callable, idx: int) -> Quaternion:
		if idx < 0:
			return Quaternion.IDENTITY
		if world_cache.has(idx):
			return world_cache[idx]
		var node: GLTFNode = nodes[idx]
		var q: Quaternion = self_ref.call(self_ref, node.parent) * node.rotation
		world_cache[idx] = q
		return q

	# Nombre de nodo en la escena generada -> índice, para resolver las rutas de las pistas.
	var track_of := {}
	for t in _anim.get_track_count():
		var path := _anim.track_get_path(t)
		track_of[[str(path.get_name(path.get_name_count() - 1)), _anim.track_get_type(t)]] = t

	var human: Dictionary = ext.get("humanoid", {}).get("humanBones", {})
	for bone_name in human:
		var idx: int = human[bone_name].get("node", -1)
		var scene_node := state.get_scene_node(idx)
		if scene_node == null:
			continue
		var node_name := str(scene_node.name)
		var rot_track: int = track_of.get([node_name, Animation.TYPE_ROTATION_3D], -1)
		if rot_track >= 0:
			var node: GLTFNode = nodes[idx]
			_bones[bone_name] = {
				track = rot_track,
				parent_world = world_rot.call(world_rot, node.parent),
				world_inv = world_rot.call(world_rot, idx).inverse(),
			}
		if bone_name == "hips":
			_hips_track = track_of.get([node_name, Animation.TYPE_POSITION_3D], -1)
			_hips_rest_pos = (nodes[idx] as GLTFNode).position
			hips_rest_height = maxf(_hips_rest_pos.y, 0.01)

	var presets: Dictionary = ext.get("expressions", {}).get("preset", {})
	for preset in presets:
		var scene_node := state.get_scene_node(presets[preset].get("node", -1))
		if scene_node:
			var t: int = track_of.get([str(scene_node.name), Animation.TYPE_POSITION_3D], -1)
			if t >= 0:
				_expressions[preset] = t


## Rotaciones normalizadas por hueso humanoide en el instante t (segundos).
func sample_bones(t: float) -> Dictionary:
	var out := {}
	for bone_name in _bones:
		var b: Dictionary = _bones[bone_name]
		var q := _anim.rotation_track_interpolate(b.track, t)
		out[bone_name] = (b.parent_world * q * b.world_inv).normalized()
	return out


## Desplazamiento de las caderas respecto del reposo, en metros del VRMA (escalar por la
## altura de caderas del avatar / hips_rest_height).
func sample_hips_offset(t: float) -> Vector3:
	if _hips_track < 0:
		return Vector3.ZERO
	return _anim.position_track_interpolate(_hips_track, t) - _hips_rest_pos


## Peso [0, 1] de cada expresión presente en el clip.
func sample_expressions(t: float) -> Dictionary:
	var out := {}
	for preset in _expressions:
		out[preset] = clampf(_anim.position_track_interpolate(_expressions[preset], t).x, 0.0, 1.0)
	return out
