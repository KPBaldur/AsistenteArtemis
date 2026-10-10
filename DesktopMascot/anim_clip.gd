extends RefCounted

## Una animación lista para aplicarse a cualquier avatar VRM, cargada en tiempo de ejecución
## (sin el importador del editor). Formatos:
## - .vrma: glTF + VRMC_vrm_animation (estándar VRM; huesos humanoides en la extensión).
## - .fbx de Mixamo (descargado "Without Skin"): huesos mixamorig:* traducidos con MIXAMO_BONES.
##
## Cada formato trae las rotaciones locales a SU propio esqueleto, cuyo reposo no es neutro.
## Se convierten al espacio "normalizado" de VRM (reposo = identidad, ejes del mundo), igual
## que three-vrm (VRMAnimationLoaderPlugin):
##     normalizada = rot_mundo_padre_reposo * q * rot_mundo_reposo⁻¹
## Así, en reposo la rotación normalizada es la identidad, y main.gd la aplica a cada hueso del
## avatar con la misma conversión de ejes que usa para la animación procedural. Requisito: que
## el esqueleto de origen repose en T mirando a +Z (VRMA y Mixamo lo cumplen).

const MIXAMO_PREFIX := "mixamorig_"  # Godot cambia "mixamorig:" por "mixamorig_"
const MIXAMO_BONES := {
	"Hips": "hips", "Spine": "spine", "Spine1": "chest", "Spine2": "upperChest",
	"Neck": "neck", "Head": "head",
	"LeftShoulder": "leftShoulder", "LeftArm": "leftUpperArm", "LeftForeArm": "leftLowerArm", "LeftHand": "leftHand",
	"RightShoulder": "rightShoulder", "RightArm": "rightUpperArm", "RightForeArm": "rightLowerArm", "RightHand": "rightHand",
	"LeftUpLeg": "leftUpperLeg", "LeftLeg": "leftLowerLeg", "LeftFoot": "leftFoot", "LeftToeBase": "leftToes",
	"RightUpLeg": "rightUpperLeg", "RightLeg": "rightLowerLeg", "RightFoot": "rightFoot", "RightToeBase": "rightToes",
	"LeftHandThumb1": "leftThumbMetacarpal", "LeftHandThumb2": "leftThumbProximal", "LeftHandThumb3": "leftThumbDistal",
	"LeftHandIndex1": "leftIndexProximal", "LeftHandIndex2": "leftIndexIntermediate", "LeftHandIndex3": "leftIndexDistal",
	"LeftHandMiddle1": "leftMiddleProximal", "LeftHandMiddle2": "leftMiddleIntermediate", "LeftHandMiddle3": "leftMiddleDistal",
	"LeftHandRing1": "leftRingProximal", "LeftHandRing2": "leftRingIntermediate", "LeftHandRing3": "leftRingDistal",
	"LeftHandPinky1": "leftLittleProximal", "LeftHandPinky2": "leftLittleIntermediate", "LeftHandPinky3": "leftLittleDistal",
	"RightHandThumb1": "rightThumbMetacarpal", "RightHandThumb2": "rightThumbProximal", "RightHandThumb3": "rightThumbDistal",
	"RightHandIndex1": "rightIndexProximal", "RightHandIndex2": "rightIndexIntermediate", "RightHandIndex3": "rightIndexDistal",
	"RightHandMiddle1": "rightMiddleProximal", "RightHandMiddle2": "rightMiddleIntermediate", "RightHandMiddle3": "rightMiddleDistal",
	"RightHandRing1": "rightRingProximal", "RightHandRing2": "rightRingIntermediate", "RightHandRing3": "rightRingDistal",
	"RightHandPinky1": "rightLittleProximal", "RightHandPinky2": "rightLittleIntermediate", "RightHandPinky3": "rightLittleDistal",
}

var name := ""
var duration := 0.0
var hips_rest_height := 1.0

var _anim: Animation
var _bones := {}        # humanoide -> { track, parent_world: Quaternion, world_inv: Quaternion }
var _expressions := {}  # preset VRM ("happy", "blink"…) -> track (posición: el peso va en x)
var _hips_track := -1
var _hips_rest_pos := Vector3.ZERO


static func load_file(path: String) -> RefCounted:
	var clip = load("res://anim_clip.gd").new()
	clip.name = path.get_file().get_basename()
	var ok := false
	match path.get_extension().to_lower():
		"vrma": ok = clip._load_vrma(path)
		"fbx": ok = clip._load_fbx(path)
	return clip if ok else null


static func is_supported(file: String) -> bool:
	return file.get_extension().to_lower() in ["vrma", "fbx"]


# --- VRMA -------------------------------------------------------------------------------

func _load_vrma(path: String) -> bool:
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	if doc.append_from_file(path, state) != OK:
		push_warning("No se pudo leer la animación %s" % path)
		return false
	var ext: Dictionary = state.json.get("extensions", {}).get("VRMC_vrm_animation", {})
	if ext.is_empty():
		push_warning("%s no es un VRMA (falta VRMC_vrm_animation)" % path)
		return false
	var root := doc.generate_scene(state)
	var ok := _take_animation(root)
	if ok:
		_build_vrma(state, ext)
	root.free()
	return ok


func _build_vrma(state: GLTFState, ext: Dictionary) -> void:
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

	var track_of := _tracks_by_name()
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


# --- FBX de Mixamo ----------------------------------------------------------------------

func _load_fbx(path: String) -> bool:
	var doc := FBXDocument.new()
	var state := FBXState.new()
	if doc.append_from_file(path, state) != OK:
		push_warning("No se pudo leer la animación %s" % path)
		return false
	var root := doc.generate_scene(state)
	var skeletons := root.find_children("*", "Skeleton3D", true, false)
	var ok := not skeletons.is_empty() and _take_animation(root)
	if ok:
		ok = _build_skeleton(skeletons[0] as Skeleton3D, root)
		if not ok:
			push_warning("%s no tiene huesos de Mixamo (mixamorig:*)" % path)
	root.free()
	return ok


func _build_skeleton(skeleton: Skeleton3D, root: Node) -> bool:
	# Rotación del nodo Skeleton3D respecto de la raíz de la escena, por si el FBX trae
	# una conversión de ejes encima del esqueleto.
	var skel_rot := Quaternion.IDENTITY
	var node: Node = skeleton
	while node != null and node != root:
		if node is Node3D:
			skel_rot = (node as Node3D).quaternion * skel_rot
		node = node.get_parent()

	var track_of := _tracks_by_name()
	for i in skeleton.get_bone_count():
		var bone := skeleton.get_bone_name(i)
		if not bone.begins_with(MIXAMO_PREFIX):
			continue
		var human_name: String = MIXAMO_BONES.get(bone.trim_prefix(MIXAMO_PREFIX), "")
		if human_name.is_empty():
			continue
		var rot_track: int = track_of.get([bone, Animation.TYPE_ROTATION_3D], -1)
		if rot_track >= 0:
			var parent := skeleton.get_bone_parent(i)
			var parent_rest := skeleton.get_bone_global_rest(parent).basis.get_rotation_quaternion() if parent >= 0 else Quaternion.IDENTITY
			_bones[human_name] = {
				track = rot_track,
				parent_world = skel_rot * parent_rest,
				world_inv = (skel_rot * skeleton.get_bone_global_rest(i).basis.get_rotation_quaternion()).inverse(),
			}
		if human_name == "hips":
			_hips_track = track_of.get([bone, Animation.TYPE_POSITION_3D], -1)
			_hips_rest_pos = skeleton.get_bone_rest(i).origin
			hips_rest_height = maxf(skeleton.get_bone_global_rest(i).origin.y, 0.01)
	return not _bones.is_empty()


# --- Común --------------------------------------------------------------------------------

func _take_animation(root: Node) -> bool:
	var players := root.find_children("*", "AnimationPlayer", true, false)
	if players.is_empty():
		return false
	var player: AnimationPlayer = players[0]
	var list := player.get_animation_list()
	if list.is_empty():
		return false
	_anim = player.get_animation(list[0]).duplicate()
	duration = _anim.length
	return true


# [nombre de nodo o hueso, tipo de pista] -> índice. Las rutas son "a/b/nodo" (VRMA) o
# "Skeleton3D:hueso" (FBX): en ambos casos interesa el último nombre o subnombre.
func _tracks_by_name() -> Dictionary:
	var track_of := {}
	for t in _anim.get_track_count():
		var path := _anim.track_get_path(t)
		var key := str(path.get_subname(path.get_subname_count() - 1)) if path.get_subname_count() > 0 \
			else str(path.get_name(path.get_name_count() - 1))
		track_of[[key, _anim.track_get_type(t)]] = t
	return track_of


## Rotaciones normalizadas por hueso humanoide en el instante t (segundos).
func sample_bones(t: float) -> Dictionary:
	var out := {}
	for bone_name in _bones:
		var b: Dictionary = _bones[bone_name]
		var q := _anim.rotation_track_interpolate(b.track, t)
		out[bone_name] = (b.parent_world * q * b.world_inv).normalized()
	return out


## Desplazamiento de las caderas respecto del reposo, en metros del clip (escalar por la
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
