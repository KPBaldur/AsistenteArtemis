extends "res://quick_window.gd"

## Comentario para un proyecto: queda como entrada "(artemis)" al final de su bitácora en el
## cerebro (POST api/Vault/comentario).

const ProjectPicker := preload("res://forms/project_picker.gd")

var _project: ProjectPicker
var _text: TextEdit
var _save: Button


func open(api_client: ApiClient, anchor: Rect2i, _extra := {}) -> void:
	setup("Comentario de proyecto", Vector2i(380, 380), api_client)

	_project = ProjectPicker.new()
	_project.optional = false
	add_field("Proyecto", _project)

	_text = TextEdit.new()
	_text.placeholder_text = "Qué pasó, qué decidiste o qué no hay que olvidar…"
	_text.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	_text.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_field("Comentario", _text)

	_save = add_buttons("Anotar en la bitácora", _submit)
	popup_next_to(anchor)
	_text.grab_focus()
	var error: String = await _project.load_projects(api)
	if not error.is_empty():
		set_status(error, "error")
		_save.disabled = true


func _submit() -> void:
	var slug := _project.selected_slug()
	var text := _text.text.strip_edges()
	if slug.is_empty():
		set_status("Elige un proyecto.", "error")
		return
	if text.is_empty():
		set_status("Escribe el comentario.", "error")
		return
	_save.disabled = true
	set_status("Anotando…")
	var result: Dictionary = await api.post_json("Vault/comentario", { project = slug, text = text })
	if not result.ok:
		_save.disabled = false
		set_status(result.error, "error")
		return
	saved.emit("Listo: lo anoté en la bitácora de %s." % _project.selected_name())
	queue_free()
