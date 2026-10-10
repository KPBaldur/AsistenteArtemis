extends "res://quick_window.gd"

## Crear alarma desde la mascota: de fecha o Pomodoro (POST api/Alarm, AlarmCreateDto).

const DateTimeField := preload("res://forms/datetime_field.gd")
const TYPE_POMODORO := 0
const TYPE_DATETIME := 1

var _tabs: TabContainer
var _date_name: LineEdit
var _when: DateTimeField
var _pomo_name: LineEdit
var _work: SpinBox
var _break: SpinBox
var _save: Button


func open(api_client: ApiClient, anchor: Rect2i, _extra := {}) -> void:
	setup("Nueva alarma", Vector2i(360, 400), api_client)

	_tabs = TabContainer.new()
	_tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_child(_tabs)

	var date_tab := _tab("Fecha")
	_date_name = _line(date_tab, "Nombre", "Ej.: Llamar al banco")
	_label(date_tab, "Cuándo")
	_when = DateTimeField.new()
	date_tab.add_child(_when)

	var pomo_tab := _tab("Pomodoro")
	_pomo_name = _line(pomo_tab, "Nombre", "Ej.: Bloque de foco")
	_work = _spin(pomo_tab, "Minutos de trabajo", 25, 1, 240)
	_break = _spin(pomo_tab, "Pausa activa (minutos, 0 = sin pausa)", 5, 0, 120)

	_save = add_buttons("Crear alarma", _submit)
	popup_next_to(anchor)
	_date_name.grab_focus()


func _submit() -> void:
	var payload := {}
	var summary := ""
	if _tabs.current_tab == 0:
		var alarm_name := _date_name.text.strip_edges()
		if alarm_name.is_empty():
			set_status("Ponle un nombre.", "error")
			return
		if not _when.is_valid():
			set_status("Revisa la fecha (dd/mm/aaaa) y la hora (HH:MM).", "error")
			return
		if not _when.is_future():
			set_status("La alarma tiene que ser en el futuro.", "error")
			return
		payload = { name = alarm_name, type = TYPE_DATETIME, scheduleAt = _when.get_utc_iso() }
		summary = "Listo: «%s» suena %s." % [alarm_name, _when.describe()]
	else:
		var pomo_name := _pomo_name.text.strip_edges()
		if pomo_name.is_empty():
			set_status("Ponle un nombre.", "error")
			return
		payload = { name = pomo_name, type = TYPE_POMODORO, durationMinutes = int(_work.value) }
		if _break.value > 0:
			payload.snoozeMinutes = int(_break.value)
		summary = "Listo: Pomodoro «%s» de %d min%s." % [pomo_name, int(_work.value),
			" con %d de pausa" % int(_break.value) if _break.value > 0 else ""]

	_save.disabled = true
	set_status("Guardando…")
	var result: Dictionary = await api.post_json("Alarm", payload)
	if not result.ok:
		_save.disabled = false
		set_status(result.error, "error")
		return
	saved.emit(summary, "alarm_created")
	queue_free()


# --- Helpers de esta ventana ----------------------------------------------------------

func _tab(tab_name: String) -> VBoxContainer:
	var margin := MarginContainer.new()
	margin.name = tab_name
	margin.add_theme_constant_override("margin_top", 12)
	_tabs.add_child(margin)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	margin.add_child(box)
	return box


func _label(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_color_override("font_color", LunaTheme.TEXT_SECONDARY)
	label.add_theme_font_size_override("font_size", 13)
	parent.add_child(label)


func _line(parent: Control, label_text: String, placeholder: String) -> LineEdit:
	_label(parent, label_text)
	var edit := LineEdit.new()
	edit.placeholder_text = placeholder
	edit.text_submitted.connect(func(_t): _submit())
	parent.add_child(edit)
	return edit


func _spin(parent: Control, label_text: String, value: int, min_value: int, max_value: int) -> SpinBox:
	_label(parent, label_text)
	var spin := SpinBox.new()
	spin.min_value = min_value
	spin.max_value = max_value
	spin.value = value
	parent.add_child(spin)
	return spin
