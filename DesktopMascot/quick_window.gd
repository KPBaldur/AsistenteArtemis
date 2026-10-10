extends Window

## Base de las ventanitas de la mascota (formularios, estado de servidores): ventana nativa
## sin bordes, siempre encima, con un panel redondeado "Luna de caza", título, cuerpo y una
## línea de estado. Se cierra con Esc o con la ✕, y se abre junto a la mascota.

const LunaTheme := preload("res://luna_theme.gd")
const ApiClient := preload("res://api_client.gd")

signal saved(message: String, category: String)  # category: ver ANIMATION_CATEGORIES en main.gd

var api: ApiClient
var body: VBoxContainer
var status_label: Label
var _title_label: Label


func setup(window_title: String, window_size: Vector2i, api_client: ApiClient) -> void:
	api = api_client
	title = window_title
	size = window_size
	# Opaca a propósito: con GL Compatibility en Windows, una ventana secundaria transparente
	# se muestra vacía (solo la ventana principal de la mascota soporta transparencia real).
	# Por eso el panel no lleva esquinas redondeadas: llena la ventana completa.
	borderless = true
	transparent = false
	transparent_bg = false
	always_on_top = true
	unresizable = true
	theme = LunaTheme.get_theme()
	close_requested.connect(queue_free)

	var panel := PanelContainer.new()
	panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_theme_stylebox_override("panel", LunaTheme.window_panel())
	add_child(panel)

	var root := VBoxContainer.new()
	root.add_theme_constant_override("separation", 12)
	panel.add_child(root)

	var header := HBoxContainer.new()
	root.add_child(header)
	_title_label = Label.new()
	_title_label.text = window_title
	_title_label.add_theme_font_size_override("font_size", 17)
	_title_label.add_theme_color_override("font_color", LunaTheme.SILVER)
	_title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_title_label.mouse_filter = Control.MOUSE_FILTER_PASS
	header.add_child(_title_label)
	var close := Button.new()
	close.text = "✕"
	close.flat = true
	close.tooltip_text = "Cerrar (Esc)"
	close.pressed.connect(queue_free)
	header.add_child(close)
	# Arrastrar la ventana desde el título (no tiene barra del sistema).
	_title_label.gui_input.connect(_on_title_input)

	body = VBoxContainer.new()
	body.add_theme_constant_override("separation", 10)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(body)

	status_label = Label.new()
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.add_theme_color_override("font_color", LunaTheme.TEXT_SECONDARY)
	status_label.visible = false
	root.add_child(status_label)


## Abre la ventana a la izquierda de la mascota (o a la derecha si no cabe).
func popup_next_to(anchor: Rect2i) -> void:
	var area := DisplayServer.screen_get_usable_rect(DisplayServer.window_get_current_screen())
	var pos := Vector2i(anchor.position.x - size.x - 8, anchor.end.y - size.y)
	if pos.x < area.position.x:
		pos.x = anchor.end.x + 8
	pos.y = clampi(pos.y, area.position.y, area.end.y - size.y)
	position = pos
	show()
	grab_focus()


func set_status(text: String, kind := "info") -> void:
	status_label.text = text
	status_label.visible = not text.is_empty()
	var color := LunaTheme.TEXT_SECONDARY
	match kind:
		"error": color = LunaTheme.DANGER
		"ok": color = LunaTheme.ACCENT
	status_label.add_theme_color_override("font_color", color)


# --- Helpers para armar formularios ---------------------------------------------------

func add_field(label_text: String, control: Control) -> Control:
	var label := Label.new()
	label.text = label_text
	label.add_theme_color_override("font_color", LunaTheme.TEXT_SECONDARY)
	label.add_theme_font_size_override("font_size", 13)
	body.add_child(label)
	body.add_child(control)
	return control


func add_buttons(primary_text: String, on_primary: Callable) -> Button:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_END
	row.add_theme_constant_override("separation", 8)
	body.add_child(row)
	var cancel := Button.new()
	cancel.text = "Cancelar"
	cancel.pressed.connect(queue_free)
	row.add_child(cancel)
	var primary := Button.new()
	primary.text = primary_text
	primary.theme_type_variation = "PrimaryButton"
	primary.pressed.connect(on_primary)
	row.add_child(primary)
	return primary


func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		queue_free()


var _drag_from := Vector2i.ZERO

func _on_title_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		_drag_from = DisplayServer.mouse_get_position() - position
	elif event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_LEFT):
		position = DisplayServer.mouse_get_position() - _drag_from
