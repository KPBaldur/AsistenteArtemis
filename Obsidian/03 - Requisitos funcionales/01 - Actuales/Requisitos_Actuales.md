# Requisitos Funcionales Actuales - TodoAppRoll

## 1. Módulo de Autenticación
- **RF-01:** El sistema debe permitir el registro de nuevos usuarios mediante username y contraseña.
- **RF-02:** El sistema debe permitir el inicio de sesión generando un Access Token (JWT) y un Refresh Token.
- **RF-03:** El sistema debe proteger las rutas privadas mediante middleware de autenticación.

## 2. Módulo de Tareas (Tasks)
- **RF-04:** El usuario debe poder crear tareas con título, descripción y prioridad.
- **RF-05:** El usuario debe poder organizar tareas mediante un orden numérico.
- **RF-06:** El usuario debe poder añadir sub-tareas a una tarea principal.
- **RF-07:** El usuario debe poder marcar tareas y sub-tareas como completadas.
- **RF-08:** El usuario debe poder filtrar tareas por su estado (pendientes/completadas).

## 3. Módulo de Alarmas (Alarms)
- **RF-09:** El usuario debe poder crear alarmas de tipo "Pomodoro" (minutos) o "DateTime" (específica).
- **RF-10:** El sistema debe disparar notificaciones visuales y auditivas en el frontend cuando se cumpla el tiempo de una alarma.
- **RF-11:** El usuario debe poder postponer una alarma, lo cual reprogramará el evento para el siguiente intervalo.
- **RF-12:** El usuario debe poder desactivar (Detener) una alarma permanentemente.
- **RF-13:** El sistema debe sincronizar el estado de las alarmas en tiempo real mediante SSE.

## 4. Módulo Multimedia (Media)
- **RF-14:** El usuario debe poder subir archivos de imagen (JPG, PNG, GIF, WebP).
- **RF-15:** El usuario debe poder subir archivos de audio (MP3, WAV).
- **RF-16:** El usuario debe poder previsualizar imágenes y reproducir audios desde el panel multimedia.
- **RF-17:** El usuario debe poder asociar un audio y una imagen a una alarma específica.

## 5. Módulo de Historial
- **RF-18:** El sistema debe registrar automáticamente las acciones del usuario sobre las entidades del sistema (Tareas, Alarmas, Media).
- **RF-19:** El usuario debe poder consultar su historial de actividades.
