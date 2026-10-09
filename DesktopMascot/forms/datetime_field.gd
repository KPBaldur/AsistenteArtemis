extends VBoxContainer

## Campo de fecha y hora para los formularios de la mascota (Godot no trae selector de fecha):
## fecha dd/mm/aaaa y hora HH:MM escribibles, con atajos rápidos. Trabaja en hora local y
## entrega UTC en ISO 8601, que es lo que espera la Api.

const LunaTheme := preload("res://luna_theme.gd")
const WEEKDAYS := ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"]

var _date: LineEdit
var _time: LineEdit


func _init() -> void:
	add_theme_constant_override("separation", 8)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	add_child(row)
	_date = LineEdit.new()
	_date.placeholder_text = "dd/mm/aaaa"
	_date.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_date)
	_time = LineEdit.new()
	_time.placeholder_text = "HH:MM"
	_time.custom_minimum_size.x = 84
	_time.add_theme_font_override("font", LunaTheme.mono_font())
	row.add_child(_time)

	var quick := HBoxContainer.new()
	quick.add_theme_constant_override("separation", 6)
	add_child(quick)
	for preset in [["+15 min", 15], ["+1 h", 60], ["Mañana 9:00", -1]]:
		var b := Button.new()
		b.text = preset[0]
		b.add_theme_font_size_override("font_size", 12)
		b.pressed.connect(_apply_preset.bind(preset[1]))
		quick.add_child(b)

	_apply_preset(60)


## Minutos desde ahora; -1 = mañana a las 9:00.
func _apply_preset(minutes: int) -> void:
	var now := int(Time.get_unix_time_from_system()) + _bias_seconds()
	var target: int
	if minutes < 0:
		var tomorrow := Time.get_datetime_dict_from_unix_time(now + 86400)
		tomorrow.hour = 9
		tomorrow.minute = 0
		tomorrow.second = 0
		target = Time.get_unix_time_from_datetime_dict(tomorrow)
	else:
		target = now + minutes * 60
	var d := Time.get_datetime_dict_from_unix_time(target)
	_date.text = "%02d/%02d/%04d" % [d.day, d.month, d.year]
	_time.text = "%02d:%02d" % [d.hour, d.minute]


## Unix local (segundos, "como si" fuera UTC) o -1 si el texto no es válido.
func _local_unix() -> int:
	var dp := _date.text.strip_edges().split("/")
	var tp := _time.text.strip_edges().split(":")
	if dp.size() != 3 or tp.size() != 2:
		return -1
	for part in Array(dp) + Array(tp):
		if not part.is_valid_int():
			return -1
	var dict := { year = int(dp[2]), month = int(dp[1]), day = int(dp[0]),
		hour = int(tp[0]), minute = int(tp[1]), second = 0 }
	if dict.month < 1 or dict.month > 12 or dict.day < 1 or dict.day > 31 or dict.hour > 23 or dict.minute > 59:
		return -1
	return Time.get_unix_time_from_datetime_dict(dict)


func is_valid() -> bool:
	return _local_unix() >= 0


func is_future() -> bool:
	return _local_unix() - _bias_seconds() > int(Time.get_unix_time_from_system())


## "2026-10-10T13:00:00Z"
func get_utc_iso() -> String:
	return Time.get_datetime_string_from_unix_time(_local_unix() - _bias_seconds()) + "Z"


## Para la burbuja: "hoy 10:00", "mañana 09:00", "vie 17 10:00".
func describe() -> String:
	var local := _local_unix()
	var today := Time.get_datetime_dict_from_unix_time(int(Time.get_unix_time_from_system()) + _bias_seconds())
	today.hour = 0
	today.minute = 0
	today.second = 0
	var days := floori((local - Time.get_unix_time_from_datetime_dict(today)) / 86400.0)
	var d := Time.get_datetime_dict_from_unix_time(local)
	var hhmm := "%02d:%02d" % [d.hour, d.minute]
	match days:
		0: return "hoy " + hhmm
		1: return "mañana " + hhmm
		_: return "%s %d %s" % [WEEKDAYS[d.weekday], d.day, hhmm]


func _bias_seconds() -> int:
	return int(Time.get_time_zone_from_system().bias) * 60
