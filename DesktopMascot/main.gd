extends Node3D

const AVATAR_PATH := "res://models/sample.vrm"

func _ready() -> void:
	_setup_desktop_mascot_window()
	_load_avatar()

func _setup_desktop_mascot_window() -> void:
	var window := get_window()
	window.size = Vector2i(280, 360)

	var screen_size := DisplayServer.screen_get_size()
	window.position = Vector2i(
		screen_size.x - window.size.x - 12,
		screen_size.y - window.size.y - 60
	)

	get_viewport().transparent_bg = true

func _load_avatar() -> void:
	var gltf_doc := GLTFDocument.new()
	var gltf_state := GLTFState.new()

	var err := gltf_doc.append_from_file(AVATAR_PATH, gltf_state)
	if err != OK:
		print("Error cargando el VRM (append_from_file): ", err)
		return

	var avatar := gltf_doc.generate_scene(gltf_state)
	if avatar == null:
		print("generate_scene devolvió null")
		return

	add_child(avatar)
	print("Avatar cargado correctamente: ", avatar.name)
