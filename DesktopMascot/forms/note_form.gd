extends "res://quick_window.gd"

## Nota rápida desde la mascota (POST api/Note, NoteCreateDto). Acepta enlaces [[Título]]
## igual que la pantalla Notas de la app.

var _title: LineEdit
var _content: TextEdit
var _save: Button


func open(api_client: ApiClient, anchor: Rect2i, _extra := {}) -> void:
	setup("Nueva nota", Vector2i(380, 380), api_client)

	_title = LineEdit.new()
	_title.placeholder_text = "Título"
	add_field("Título", _title)

	_content = TextEdit.new()
	_content.placeholder_text = "Escribe aquí… (puedes enlazar otras notas con [[Título]])"
	_content.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	_content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_field("Contenido", _content)

	_save = add_buttons("Guardar nota", _submit)
	popup_next_to(anchor)
	_title.grab_focus()


func _submit() -> void:
	var note_title := _title.text.strip_edges()
	if note_title.is_empty():
		set_status("Ponle un título.", "error")
		return
	_save.disabled = true
	set_status("Guardando…")
	var result: Dictionary = await api.post_json("Note", { title = note_title, content = _content.text })
	if not result.ok:
		_save.disabled = false
		set_status(result.error, "error")
		return
	saved.emit("Listo: guardé la nota «%s»." % note_title)
	queue_free()
