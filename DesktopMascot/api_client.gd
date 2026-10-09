extends Node

## Cliente HTTP de la Api de Artemis para la mascota. La URL y el token de dispositivo los
## deja la app MAUI en user://api.cfg (Identidad → Conectar mascota); se releen en cada
## llamada, así una conexión hecha con la mascota abierta funciona sin reiniciarla.

const CONFIG_PATH := "user://api.cfg"
const TIMEOUT_SECONDS := 20.0

const NOT_LINKED_MESSAGE := "No estoy conectada. Abre la app: Identidad → Conectar mascota."


func is_linked() -> bool:
	return _load_config().size() == 2


## Devuelve { ok: bool, status: int, data: Variant, error: String }.
func request_json(method: HTTPClient.Method, path: String, body: Variant = null) -> Dictionary:
	var config := _load_config()
	if config.size() < 2:
		return _fail(0, NOT_LINKED_MESSAGE)

	var http := HTTPRequest.new()
	http.timeout = TIMEOUT_SECONDS
	add_child(http)

	var headers := PackedStringArray([
		"Authorization: Device %s" % config.token,
		"Accept: application/json",
	])
	var payload := ""
	if body != null:
		headers.append("Content-Type: application/json")
		payload = JSON.stringify(body)

	var err := http.request(config.base_url.path_join(path), headers, method, payload)
	if err != OK:
		http.queue_free()
		return _fail(0, "No se pudo iniciar la conexión (%s)." % error_string(err))

	var response: Array = await http.request_completed
	http.queue_free()
	var result: int = response[0]
	var status: int = response[1]
	var text: String = (response[3] as PackedByteArray).get_string_from_utf8()

	if result != HTTPRequest.RESULT_SUCCESS:
		return _fail(0, "La Api no responde. ¿Está corriendo?")
	if status == 401:
		return _fail(status, "Mi conexión ya no es válida. Vuelve a conectarme desde la app.")

	var data: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if status >= 400:
		var message := "Error %d de la Api." % status
		if data is Dictionary and data.has("message"):
			message = str(data.message)
		return _fail(status, message)
	return { ok = true, status = status, data = data, error = "" }


func get_json(path: String) -> Dictionary:
	return await request_json(HTTPClient.METHOD_GET, path)


func post_json(path: String, body: Variant) -> Dictionary:
	return await request_json(HTTPClient.METHOD_POST, path, body)


## Proyectos del cerebro (GET api/Vault/projects), activos primero. Se cachean unos minutos:
## cambian poco y se piden cada vez que se abre un formulario con selector de proyecto.
const PROJECTS_TTL_MSEC := 5 * 60 * 1000
var _projects_cache: Array = []
var _projects_at := -PROJECTS_TTL_MSEC

func get_projects() -> Dictionary:
	if not _projects_cache.is_empty() and Time.get_ticks_msec() - _projects_at < PROJECTS_TTL_MSEC:
		return { ok = true, status = 200, data = _projects_cache, error = "" }
	var result := await get_json("Vault/projects")
	if result.ok and result.data is Array:
		var projects: Array = result.data
		projects.sort_custom(func(a, b):
			var a_active: bool = a.get("estado") == "activo"
			if a_active != (b.get("estado") == "activo"):
				return a_active
			return str(a.get("nombre", a.get("id"))).naturalnocasecmp_to(str(b.get("nombre", b.get("id")))) < 0)
		_projects_cache = projects
		_projects_at = Time.get_ticks_msec()
		result.data = projects
	return result


func _load_config() -> Dictionary:
	var cfg := ConfigFile.new()
	if cfg.load(CONFIG_PATH) != OK:
		return {}
	var base_url: String = cfg.get_value("api", "base_url", "")
	var token: String = cfg.get_value("api", "token", "")
	if base_url.is_empty() or token.is_empty():
		return {}
	return { base_url = base_url, token = token }


func _fail(status: int, message: String) -> Dictionary:
	return { ok = false, status = status, data = null, error = message }
