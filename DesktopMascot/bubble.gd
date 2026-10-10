extends Window

## Burbuja de diálogo de una línea sobre la mascota ("Listo: recordatorio para mañana
## 10:00"). Ventana propia, transparente y sin foco, para no tapar la cara del avatar dentro
## de la ventana de la mascota. Se esconde sola.

const LunaTheme := preload("res://luna_theme.gd")
const MAX_WIDTH := 300

var _label: Label
var _panel: PanelContainer
var _timer: Timer


func _ready() -> void:
	# Opaca: las ventanas secundarias transparentes se ven vacías con GL Compatibility en
	# Windows (ver quick_window.gd).
	borderless = true
	transparent = false
	transparent_bg = false
	always_on_top = true
	unfocusable = true
	unresizable = true
	visible = false
	theme = LunaTheme.get_theme()

	_panel = PanelContainer.new()
	_panel.add_theme_stylebox_override("panel", LunaTheme.window_panel(LunaTheme.BORDER_STRONG, 14, 10))
	add_child(_panel)

	_label = Label.new()
	_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_label.custom_minimum_size.x = 120
	_panel.add_child(_label)

	_timer = Timer.new()
	_timer.one_shot = true
	_timer.timeout.connect(hide)
	add_child(_timer)


## kind: "info" (plata), "ok" (cian) o "error" (coral), el color del borde.
func say(text: String, anchor: Rect2i, kind := "info", seconds := 5.0) -> void:
	_label.text = text
	var style := _panel.get_theme_stylebox("panel") as StyleBoxFlat
	match kind:
		"ok": style.border_color = LunaTheme.ACCENT
		"error": style.border_color = LunaTheme.DANGER
		_: style.border_color = LunaTheme.BORDER_STRONG

	# Medir con el ancho máximo y ajustar la ventana al contenido.
	_label.custom_minimum_size.x = mini(MAX_WIDTH - 28, maxi(120, text.length() * 8))
	_panel.reset_size()
	var needed := Vector2i(_panel.get_combined_minimum_size().ceil())
	size = needed
	_panel.size = needed

	var area := DisplayServer.screen_get_usable_rect(DisplayServer.window_get_current_screen())
	var pos := Vector2i(anchor.position.x + (anchor.size.x - needed.x) / 2, anchor.position.y - needed.y + 24)
	pos.x = clampi(pos.x, area.position.x, area.end.x - needed.x)
	pos.y = maxi(pos.y, area.position.y)
	position = pos
	show()
	_timer.start(seconds)
