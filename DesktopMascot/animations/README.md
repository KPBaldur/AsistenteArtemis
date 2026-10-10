# Animaciones de la mascota (VRMA)

Archivos `.vrma` (VRM Animation) que `main.gd` carga al arrancar con `vrma_clip.gd`. Funcionan con cualquier avatar VRM, sin pasar por el importador del editor de Godot.

## Qué nombre usa cada evento

`CLIP_ROLES` en `main.gd` elige el clip por **nombre de archivo** (sin `.vrma`), en orden de preferencia:

| Evento | Archivos que busca |
|---|---|
| Al abrir la mascota | `greeting`, `wave` |
| Al guardar algo desde un formulario | `peace`, `celebrate`, `spin`, `model_pose`, `wave` |
| Clic sobre la mascota (uno al azar) | `peace`, `greeting`, `model_pose`, `show_body`, `spin`, `wave` |

Si ninguno existe, se usa cualquier clip disponible. Para probar un clip suelto:
`Godot --path DesktopMascot -- --clip=<nombre> --at=<segundos> --screenshot=<png>`.

## Origen y licencia

| Archivo | Origen | Licencia |
|---|---|---|
| `wave.vrma` | `test.vrma` de [pixiv/three-vrm](https://github.com/pixiv/three-vrm) (`packages/three-vrm-animation/examples/models/`): animación de prueba (brazo derecho + expresión `happy`) | MIT |

Pack oficial gratuito de 7 animaciones de VRoid Project (saludo, pose de paz, giro, pose de modelo, mostrar cuerpo, sentadilla, disparo): tienda oficial de VRoid Project en BOOTH (requiere cuenta pixiv). Al agregarlos, renombrarlos según la tabla de arriba y anotar aquí su licencia: revisar sus términos antes de publicar el repo.
