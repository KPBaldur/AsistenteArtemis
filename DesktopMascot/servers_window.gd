extends "res://quick_window.gd"

## Estado de los servidores monitoreados (GET api/Monitor), igual que "Sistemas" en la app.

const STATUS_UP := 1
const STATUS_DOWN := 2

var _list: VBoxContainer


func open(api_client: ApiClient, anchor: Rect2i, _extra := {}) -> void:
	setup("Servidores", Vector2i(340, 380), api_client)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	body.add_child(scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override("separation", 10)
	scroll.add_child(_list)

	var refresh := Button.new()
	refresh.text = "Actualizar"
	refresh.pressed.connect(_load)
	body.add_child(refresh)

	popup_next_to(anchor)
	_load()


func _load() -> void:
	set_status("Consultando…")
	var result: Dictionary = await api.get_json("Monitor")
	for child in _list.get_children():
		child.queue_free()
	if not result.ok:
		set_status(result.error, "error")
		return

	var monitors: Array = result.data if result.data is Array else []
	monitors = monitors.filter(func(m): return m.get("isEnabled", true))
	if monitors.is_empty():
		set_status("Sin servidores configurados.")
		return
	# Caídos primero, después por nombre.
	monitors.sort_custom(func(a, b):
		if (a.lastStatus == STATUS_DOWN) != (b.lastStatus == STATUS_DOWN):
			return a.lastStatus == STATUS_DOWN
		return str(a.name).naturalnocasecmp_to(str(b.name)) < 0)

	var down := 0
	for m in monitors:
		_list.add_child(_row(m))
		if m.lastStatus == STATUS_DOWN:
			down += 1
	set_status("%d caído%s" % [down, "" if down == 1 else "s"] if down > 0 else "Todo en orden.",
		"error" if down > 0 else "ok")


func _row(m: Dictionary) -> Control:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)

	var dot := ColorRect.new()
	dot.custom_minimum_size = Vector2(10, 10)
	dot.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	match int(m.get("lastStatus", 0)):
		STATUS_UP: dot.color = LunaTheme.ACCENT
		STATUS_DOWN: dot.color = LunaTheme.DANGER
		_: dot.color = LunaTheme.BORDER_STRONG
	row.add_child(dot)

	var name_label := Label.new()
	name_label.text = str(m.get("name", "?"))
	name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_label.clip_text = true
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	row.add_child(name_label)

	var meta := Label.new()
	meta.add_theme_font_override("font", LunaTheme.mono_font())
	meta.add_theme_font_size_override("font_size", 12)
	meta.add_theme_color_override("font_color", LunaTheme.TEXT_SECONDARY)
	match int(m.get("lastStatus", 0)):
		STATUS_UP:
			var ms = m.get("lastResponseTimeMs")
			meta.text = "%d ms" % ms if ms != null else "activo"
		STATUS_DOWN:
			meta.text = "caído"
		_:
			meta.text = "—"
	row.add_child(meta)
	return row
