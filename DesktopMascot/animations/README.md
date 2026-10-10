# Biblioteca de animaciones de la mascota

Animaciones que `main.gd` carga al arrancar con `anim_clip.gd`. Sirven para **cualquier avatar VRM**: cada rotación se convierte al esqueleto humanoide normalizado de VRM, así que no dependen del modelo. Un avatar puede traer además las suyas en `avatars/<id>/animations/`.

## Formatos

| Formato | De dónde | Notas |
|---|---|---|
| `.vrma` | Estándar VRM (VRoid, BOOTH, VRoid Hub) | Puede traer expresiones (sonrisa, etc.). |
| `.fbx` | Mixamo | Descargar con **Format: FBX Binary**, **Skin: Without Skin**, **30 fps**, **Keyframe Reduction: none** y, si aparece, **In Place**. El personaje de origen debe partir en pose T o A. |

El nombre del archivo (sin extensión) es el nombre de la animación: es el que se usa en `avatar.json` y el que aparece en el menú de la mascota (**clic derecho → Animaciones**) para probarla.

## Categorías

Cada `avatars/<id>/avatar.json` asigna a cada categoría una lista de animaciones; se elige una al azar:

| Categoría | Cuándo |
|---|---|
| `greet` | Al abrir la mascota |
| `click` | Clic sobre la mascota (sin arrastrar) |
| `celebrate` | Respaldo de las categorías de "creado/guardado" que no tengan animación propia |
| `task_created` · `reminder_created` · `alarm_created` · `note_saved` · `comment_saved` | Al guardar desde el formulario correspondiente |
| `idle` | De vez en cuando (cada 1,5–4 min) si no está haciendo nada |
| `task_done` · `alarm_ringing` · `server_down` | **Pendientes**: necesitan que la Api avise a la mascota (PresenceHub) |

## Origen y licencia

| Archivo | Origen | Licencia / en git |
|---|---|---|
| `wave.vrma` | `test.vrma` de [pixiv/three-vrm](https://github.com/pixiv/three-vrm) (`packages/three-vrm-animation/examples/models/`): animación de prueba (brazo derecho + expresión `happy`) | MIT · sí |
| `HipHopDancing.fbx` | Mixamo (descargada por Kevin, 2026-10-09) | Términos de Adobe Mixamo: se puede usar en proyectos, pero **no redistribuir el archivo suelto** → `*.fbx` está en `.gitignore` |

Pack oficial gratuito de 7 animaciones VRMA de VRoid Project (saludo, pose de paz, giro, pose de modelo, mostrar cuerpo, sentadilla, disparo): tienda oficial de VRoid Project en BOOTH (requiere cuenta pixiv). Revisar sus términos antes de commitearlas.
