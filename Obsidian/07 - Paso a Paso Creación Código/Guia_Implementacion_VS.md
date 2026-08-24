# Guía Paso a Paso: Implementación TodoAppRoll V2 (.NET Core)

## 1. Configuración del Entorno y Proyecto
1.  **Abrir Visual Studio 2022** (Asegurarse de tener instalado el SDK de .NET 8).
2.  **Crear un nuevo proyecto**: Seleccionar la plantilla **ASP.NET Core Web API**.
3.  **Configuración inicial**:
    *   Nombre del proyecto: `TodoAppRoll.Api`
    *   Framework: `.NET 8.0 (Long-term support)`.
    *   Autenticación: `None` (La implementaremos manualmente con JWT).
    *   Habilitar `OpenAPI support` (Swagger).
4.  **Estructura de Carpetas Sugerida**:
    *   `/Data`: Contexto de base de datos y migraciones.
    *   `/Models/Entities`: Clases POCO (las definidas en el diseño técnico).
    *   `/Models/DTOs`: Objetos de transferencia de datos para las peticiones API.
    *   `/Controllers`: Endpoints de la API.
    *   `/Services`: Lógica de negocio e integraciones (IA, Cloudinary, SignalR).
    *   `/Middleware`: Manejo de errores y autenticación.
    *   `/Hubs`: Definiciones de SignalR para tiempo real.

## 2. Implementación del Acceso a Datos (EntityFramework Core)
1.  **Instalar paquetes NuGet**:
    *   `Microsoft.EntityFrameworkCore.Design`
    *   `Npgsql.EntityFrameworkCore.PostgreSQL`
    *   `Microsoft.EntityFrameworkCore.Tools`
2.  **Crear el AppDbContext**: En la carpeta `/Data`, crear una clase que herede de `DbContext`.
3.  **Configurar la Connection String**: Añadir la URL de PostgreSQL en `appsettings.json`.
4.  **Primera Migración**:
    *   Abrir la Consola del Administrador de Paquetes.
    *   Ejecutar: `Add-Migration InitialCreate`
    *   Ejecutar: `Update-Database`

## 3. Desarrollo por Módulos (Orden Recomendado)

### Módulo 1: Autenticación y Usuarios
- **Clases**: `UserController`, `IAuthService`, `JwtMiddleware`.
- **Acción**: Implementar registro, login y generación de tokens JWT.

### Módulo 2: Tareas y Subtareas
- **Clases**: `TaskController`, `TaskService`, `ITaskRepository`.
- **Acción**: CRUD de tareas con el mapeo de prioridades y estados.

### Módulo 3: Alarmas y Tiempo Real (SignalR)
- **Clases**: `AlarmHub` (SignalR), `AlarmController`, `SchedulerService`.
- **Acción**: Configurar SignalR para que el backend pueda "empujar" la activación de alarmas a los clientes.

### Módulo 4: Multimedia e Historial
- **Clases**: `MediaController`, `CloudinaryService`.
- **Acción**: Integrar el SDK de Cloudinary para .NET.

### Módulo 5: Integración con IA (Python Bridge)
- **Clases**: `AIService`, `AIController`.
- **Acción**: Implementar un `HttpClient` en C# que consuma el microservicio de Python.

## 4. Pruebas y Validación
1.  Utilizar **Swagger** para probar los endpoints de forma aislada.
2.  Configurar **Postman** para flujos que requieran autenticación JWT.
3.  Verificar la persistencia en PostgreSQL mediante herramientas como pgAdmin o Azure Data Studio.
