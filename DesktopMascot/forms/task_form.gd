extends "res://quick_window.gd"

## Tarea o recordatorio desde la mascota (POST api/Task/reminder). Un recordatorio es una
## tarea con hora: si se marca "Con hora", la Api crea además una alarma de fecha vinculada.
## Con proyecto, la tarea queda en la app (ProjectSlug) y también como pendiente "(artemis)"
## en el cerebro (POST api/Vault/pendiente) — decisión del 2026-10-09: en ambos lados.

const DateTimeField := preload("res://forms/datetime_field.gd")
const ProjectPicker := preload("res://forms/project_picker.gd")

var _title: LineEdit
var _with_time: CheckBox
var _when: DateTimeField
var _project: ProjectPicker
var _save: Button


func open(api_client: ApiClient, anchor: Rect2i, extra := {}) -> void:
	var reminder: bool = extra.get("reminder", false)
	setup("Nuevo recordatorio" if reminder else "Nueva tarea", Vector2i(360, 400), api_client)

	_title = LineEdit.new()
	_title.placeholder_text = "Ej.: Mandar el correo a proveedores" if reminder else "Ej.: Revisar el informe"
	_title.text_submitted.connect(func(_t): _submit())
	add_field("¿Qué hay que hacer?", _title)

	_with_time = CheckBox.new()
	_with_time.text = "Con hora (suena una alarma)"
	_with_time.button_pressed = reminder
	body.add_child(_with_time)

	_when = DateTimeField.new()
	_when.visible = reminder
	body.add_child(_when)
	_with_time.toggled.connect(func(on: bool): _when.visible = on)

	_project = ProjectPicker.new()
	add_field("Proyecto (opcional)", _project)
	_load_projects()

	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_child(spacer)

	_save = add_buttons("Crear", _submit)
	popup_next_to(anchor)
	_title.grab_focus()


func _submit() -> void:
	var task_title := _title.text.strip_edges()
	if task_title.is_empty():
		set_status("Escribe qué hay que hacer.", "error")
		return
	var payload := { title = task_title }
	var summary := "Listo: anoté la tarea «%s»." % task_title
	if _with_time.button_pressed:
		if not _when.is_valid():
			set_status("Revisa la fecha (dd/mm/aaaa) y la hora (HH:MM).", "error")
			return
		if not _when.is_future():
			set_status("La hora tiene que ser en el futuro.", "error")
			return
		payload.remindAt = _when.get_utc_iso()
		summary = "Listo: te recuerdo «%s» %s." % [task_title, _when.describe()]

	var slug := _project.selected_slug()
	if not slug.is_empty():
		payload.projectSlug = slug

	_save.disabled = true
	set_status("Guardando…")
	var result: Dictionary = await api.post_json("Task/reminder", payload)
	if not result.ok:
		_save.disabled = false
		set_status(result.error, "error")
		return

	if not slug.is_empty():
		var vault_result: Dictionary = await api.post_json("Vault/pendiente", { project = slug, text = task_title })
		if not vault_result.ok:
			# La tarea ya quedó en la app: no se reintenta para no duplicarla.
			saved.emit("%s Pero no pude anotarla en el cerebro: %s" % [summary, vault_result.error])
			queue_free()
			return
		summary += " También quedó en los pendientes de %s." % _project.selected_name()
	saved.emit(summary)
	queue_free()


func _load_projects() -> void:
	var error: String = await _project.load_projects(api)
	if not error.is_empty():
		set_status("Sin lista de proyectos: %s" % error)
