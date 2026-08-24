# Informe de Funcionamiento Legacy - TodoAppRoll

## 1. Introducción
El proyecto **TodoAppRoll** es una aplicación de gestión de tareas y alarmas diseñada con un enfoque en la productividad (Pomodoro) y la personalización multimedia. Actualmente opera como una Single Page Application (SPA) para el frontend y una API RESTful para el backend.

## 2. Arquitectura del Sistema
El sistema se divide en dos componentes principales:

### 2.1 Backend (Servidor)
- **Tecnología:** Node.js con Express y TypeScript.
- **Base de Datos:** PostgreSQL gestionada a través de Prisma ORM.
- **Autenticación:** JWT (JSON Web Tokens) con soporte para Refresh Tokens.
- **Comunicación en Tiempo Real:** Implementación de SSE (Server-Sent Events) para el disparo de alarmas.
- **Servicios Externos:** Cloudinary para el almacenamiento de archivos multimedia (audio e imágenes).

### 2.2 Frontend (Cliente)
- **Tecnología:** React con Vite y TypeScript.
- **Estilos:** TailwindCSS para el diseño base y CSS personalizado para componentes específicos (como popups de alarmas).
- **Estado:** React Hooks y Context API para la gestión de alarmas.

## 3. Módulos Operativos

### 3.1 Gestión de Tareas (Tasks)
- Permite crear, editar, eliminar y listar tareas.
- Soporta sub-tareas con estado de completado.
- Priorización de tareas (Baja, Media, Alta).
- Integración opcional con alarmas.

### 3.2 Sistema de Alarmas (Alarms)
- **Modos:** Pomodoro (intervalos de minutos) y Fecha/Hora exacta.
- **Funcionamiento Backend:** Un servicio de `scheduler` gestiona timeouts en memoria para disparar las alarmas en el momento preciso.
- **Sincronización:** Cuando una alarma se activa, el backend emite un evento vía SSE que el frontend captura para mostrar un popup visual y reproducir audio.

### 3.3 Administrador Multimedia (Media)
- Los usuarios pueden subir archivos de audio e imagen.
- Almacenamiento real en Cloudinary, metadatos en la base de datos local.
- Los archivos pueden ser vinculados a alarmas para personalización sonora y visual.

### 3.4 Historial y Logs
- Registro automático de acciones críticas (creación de tareas, cambios en alarmas) para auditoría del usuario.

## 4. Flujo de Datos Crítico (Alarma Pomodoro)
1. El usuario crea una alarma de 25 minutos.
2. El backend guarda la alarma y calcula `scheduleAt = ahora + 25m`.
3. El `schedulerService` inicia un cronómetro.
4. Al cumplirse el tiempo, el backend envía un evento SSE: `{ type: "ALARM_TRIGGERED", id: "..." }`.
5. El frontend recibe el evento, despliega el modal de alarma y reproduce el audio en loop.
6. El usuario decide "Postponer" (reinicia el ciclo) o "Detener" (desactiva la alarma).

## 5. Limitaciones Identificadas (Estado Legacy)
- **Acoplamiento de Scheduler:** El scheduler depende de la memoria del proceso del servidor; si el servidor se reinicia, los timeouts activos se pierden (aunque hay una función `initializeAlarms` para recuperarlos al arranque).
- **Escalabilidad SSE:** SSE es eficiente pero requiere conexiones persistentes que pueden ser limitadas en ciertos entornos de hosting.
- **Diseño Monolítico:** Aunque separado en carpetas, el backend maneja mucha lógica de negocio directamente en controladores.
- **Ausencia de Desktop/Mobile Nativo:** Actualmente solo accesible vía navegador web.
