extends RefCounted

## Tema de Godot con los tokens del sistema de diseño "Luna de caza" (los mismos que
## AsistenteArtemis.Client.Maui/wwwroot/css/app.css). Se arma por código y las fuentes se
## cargan en tiempo de ejecución: al correr la mascota sin abrir el editor, los .woff2 no
## pasan por el importador de Godot.

const BG := Color("#0E1220")
const BG_SUNKEN := Color("#0B0F1B")
const SURFACE := Color("#161B2E")
const SURFACE_RAISED := Color("#1E2A44")
const BORDER := Color("#262D45")
const BORDER_STRONG := Color("#3A4566")
const TEXT := Color("#E6E8F0")
const TEXT_SECONDARY := Color("#9AA3BD")
const SILVER := Color("#C9D1E8")
const ACCENT := Color("#5CC8D6")
const ACCENT_HOVER := Color("#8FDCE6")
const WARNING := Color("#F2B45A")
const DANGER := Color("#F0716B")

static var _theme: Theme
static var _mono: FontFile


static func get_theme() -> Theme:
	if _theme == null:
		_theme = _build()
	return _theme


static func mono_font() -> FontFile:
	if _mono == null:
		_mono = _load_font("res://fonts/ibm-plex-mono-latin-400.woff2")
	return _mono


static func _load_font(path: String) -> FontFile:
	var font := FontFile.new()
	if font.load_dynamic_font(path) != OK:
		push_warning("No se pudo cargar la fuente %s" % path)
	return font


static func _box(bg: Color, border: Color, radius: int, pad_x := 12, pad_y := 8, border_width := 1) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = bg
	box.border_color = border
	box.set_border_width_all(border_width)
	box.set_corner_radius_all(radius)
	box.content_margin_left = pad_x
	box.content_margin_right = pad_x
	box.content_margin_top = pad_y
	box.content_margin_bottom = pad_y
	box.anti_aliasing = true
	return box


static func _build() -> Theme:
	var t := Theme.new()
	t.default_font = _load_font("res://fonts/sora-latin-variable.woff2")
	t.default_font_size = 14

	# Fondo de las ventanitas.
	t.set_stylebox("panel", "PanelContainer", _box(BG, BORDER, 16, 18, 16))
	t.set_stylebox("panel", "Panel", _box(BG, BORDER, 16))

	t.set_color("font_color", "Label", TEXT)

	# Campos de texto.
	for type in ["LineEdit", "TextEdit", "SpinBox"]:
		t.set_stylebox("normal", type, _box(BG_SUNKEN, BORDER_STRONG, 10, 10, 8))
		t.set_stylebox("focus", type, _box(BG_SUNKEN, ACCENT, 10, 10, 8, 2))
		t.set_color("font_color", type, TEXT)
		t.set_color("font_placeholder_color", type, TEXT_SECONDARY)
		t.set_color("caret_color", type, ACCENT)
		t.set_color("selection_color", type, Color(ACCENT, 0.35))

	# Botones: secundarios por defecto; el primario usa la variación "PrimaryButton".
	t.set_stylebox("normal", "Button", _box(Color(0, 0, 0, 0), BORDER_STRONG, 10, 14, 8))
	t.set_stylebox("hover", "Button", _box(SURFACE_RAISED, BORDER_STRONG, 10, 14, 8))
	t.set_stylebox("pressed", "Button", _box(SURFACE_RAISED, ACCENT, 10, 14, 8))
	t.set_stylebox("focus", "Button", _box(Color(0, 0, 0, 0), ACCENT, 10, 14, 8, 2))
	t.set_stylebox("disabled", "Button", _box(Color(0, 0, 0, 0), BORDER, 10, 14, 8))
	t.set_color("font_color", "Button", SILVER)
	t.set_color("font_hover_color", "Button", TEXT)
	t.set_color("font_pressed_color", "Button", TEXT)
	t.set_color("font_disabled_color", "Button", TEXT_SECONDARY)

	t.add_type("PrimaryButton")
	t.set_type_variation("PrimaryButton", "Button")
	t.set_stylebox("normal", "PrimaryButton", _box(ACCENT, ACCENT, 10, 14, 8))
	t.set_stylebox("hover", "PrimaryButton", _box(ACCENT_HOVER, ACCENT_HOVER, 10, 14, 8))
	t.set_stylebox("pressed", "PrimaryButton", _box(ACCENT_HOVER, ACCENT_HOVER, 10, 14, 8))
	for state in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		t.set_color(state, "PrimaryButton", BG)

	# Selectores y casillas.
	t.set_stylebox("normal", "OptionButton", _box(BG_SUNKEN, BORDER_STRONG, 10, 10, 8))
	t.set_stylebox("hover", "OptionButton", _box(SURFACE_RAISED, BORDER_STRONG, 10, 10, 8))
	t.set_stylebox("pressed", "OptionButton", _box(SURFACE_RAISED, ACCENT, 10, 10, 8))
	t.set_stylebox("focus", "OptionButton", _box(BG_SUNKEN, ACCENT, 10, 10, 8, 2))
	# CheckBox hereda los fondos de Button: marcada se veía como un botón presionado.
	for state in ["normal", "pressed", "hover", "hover_pressed", "focus", "disabled"]:
		t.set_stylebox(state, "CheckBox", StyleBoxEmpty.new())
	t.set_color("font_color", "CheckBox", SILVER)
	t.set_color("font_hover_color", "CheckBox", TEXT)
	t.set_color("font_pressed_color", "CheckBox", TEXT)

	# Pestañas (alarma de fecha / Pomodoro). Sin fondo propio: el de la ventana basta.
	t.set_stylebox("panel", "TabContainer", StyleBoxEmpty.new())
	for type in ["TabBar", "TabContainer"]:
		t.set_stylebox("tab_selected", type, _box(SURFACE_RAISED, SURFACE_RAISED, 10, 14, 6))
		t.set_stylebox("tab_unselected", type, _box(Color(0, 0, 0, 0), Color(0, 0, 0, 0), 10, 14, 6))
		t.set_stylebox("tab_hovered", type, _box(SURFACE, SURFACE, 10, 14, 6))
		t.set_color("font_selected_color", type, ACCENT)
		t.set_color("font_unselected_color", type, TEXT_SECONDARY)
		t.set_color("font_hovered_color", type, TEXT)

	# Menú contextual de la mascota.
	t.set_stylebox("panel", "PopupMenu", _box(SURFACE, BORDER_STRONG, 12, 6, 6))
	t.set_stylebox("hover", "PopupMenu", _box(SURFACE_RAISED, SURFACE_RAISED, 8, 10, 4))
	t.set_color("font_color", "PopupMenu", TEXT)
	t.set_color("font_hover_color", "PopupMenu", ACCENT)
	t.set_color("font_disabled_color", "PopupMenu", TEXT_SECONDARY)
	t.set_color("font_separator_color", "PopupMenu", TEXT_SECONDARY)
	t.set_constant("v_separation", "PopupMenu", 8)
	return t
