# Diseño de Arquitectura Frontend V2 - TodoAppRoll

## 1. Stack Tecnológico
- **Framework:** React 18+ (Vite).
- **Lenguaje:** TypeScript (Tipado estricto).
- **Gestión de Estado:** 
    - **Zustand:** Para estado global ligero (Usuario, Temas, Mascota).
    - **TanStack Query (React Query):** Para sincronización de datos con la API de .NET.
- **Estilos:** TailwindCSS + Framer Motion (para animaciones de la mascota).
- **Comunicación:** Axios (API REST) + @microsoft/signalr (Tiempo real).

## 2. Estructura de Carpetas Sugerida
```text
/src
  /api              # Clientes Axios y endpoints
  /assets           # Imágenes, fuentes, sonidos
  /components       # Componentes reutilizables (UI)
    /common         # Buttons, Inputs, Modals
    /layout         # Sidebar, Header, PageWrapper
    /pet            # Componentes de la Mascota Virtual (Artemis)
  /hooks            # Custom hooks (useAuth, useTasks, useSignalR)
  /pages            # Componentes de página (Login, Dashboard, Alarms)
  /store            # Definiciones de Zustand
  /types            # Interfaces y tipos TS (Espejo de las entidades C#)
  /utils            # Helpers de fechas, validaciones
```

## 3. Integración con Mascota Virtual (Estrategia)
La mascota no será solo un componente visual, sino una capa interactiva:
- **Estado de Ánimo:** Vinculado a la productividad del usuario (Zustand).
- **Interacciones:** Notificaciones de alarmas a través de "globos de texto" de la mascota.
- **Animaciones:** Framer Motion para movimientos fluidos.

## 4. Plan de Ejecución Frontend
1. **Configuración de Proyecto:** Inicialización con Vite y Tailwind.
2. **Capa de Tipos:** Replicar las entidades de C# en interfaces de TypeScript.
3. **Servicios de API:** Configurar Axios con interceptores para el token JWT.
4. **Módulo de Autenticación:** Vistas de Login y Registro.
5. **Dashboard de Tareas:** Implementación de la gestión de tareas con React Query.
6. **Sistema de Alarmas (SignalR):** Hook especializado para escuchar el Hub de .NET.
