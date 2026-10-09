extends OptionButton

## Selector de proyecto del cerebro (BrainObsidian) para los formularios de la mascota.
## Primer ítem opcional "Sin proyecto"; los demás traen el `id` (slug) como metadata.

const ApiClient := preload("res://api_client.gd")

var optional := true


func load_projects(api: ApiClient) -> String:
	clear()
	add_item("Cargando proyectos…")
	disabled = true
	var result: Dictionary = await api.get_projects()
	clear()
	if optional:
		add_item("Sin proyecto")
		set_item_metadata(0, "")
	if not result.ok:
		disabled = not optional
		return result.error
	for p in result.data:
		var label := str(p.get("nombre", p.get("id")))
		if p.get("estado") != "activo":
			label += "  · %s" % p.get("estado", "")
		add_item(label)
		set_item_metadata(item_count - 1, str(p.get("id")))
	disabled = false
	return ""


## Slug del proyecto elegido, o "" si no hay.
func selected_slug() -> String:
	if selected < 0:
		return ""
	var meta = get_item_metadata(selected)
	return str(meta) if meta != null else ""


func selected_name() -> String:
	return get_item_text(selected).split("  · ")[0] if selected >= 0 else ""
