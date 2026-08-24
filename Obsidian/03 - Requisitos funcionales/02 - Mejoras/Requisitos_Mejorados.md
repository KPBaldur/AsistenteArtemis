# Requisitos Funcionales Mejorados (V2 - IA & Multiplataforma)

## 1. Soporte Multiplataforma (Core V2)
- **RF-M01 (Desktop):** La aplicación debe funcionar de forma nativa en Windows mediante **MAUI** o empaquetado web avanzado con notificaciones nativas de Windows.
- **RF-M02 (Mobile):** Aplicación móvil desarrollada en **React Native** para Android e iOS.
- **RF-M03 (Sincronización):** Los datos deben estar sincronizados en tiempo real entre .NET Backend y los clientes mediante **SignalR**.

## 2. Inteligencia Artificial (Motor Artemis)
- **RF-IA01 (NLP Task Creation):** El usuario debe poder dictar o escribir "Recuérdame comprar pan a las 5pm" y la IA debe crear la tarea y la alarma automáticamente.
- **RF-IA02 (Smart Scheduling):** La IA debe sugerir horarios óptimos para tareas basados en el historial de productividad del usuario.
- **RF-IA03 (Análisis de Productividad):** Generación de informes semanales analizando los ciclos Pomodoro completados vs. tareas pendientes.
- **RF-IA04 (Integración API):** Consumo seguro de modelos mediante API Key, con sistema de caché para optimizar costos de tokens.

## 3. Mejoras en el Sistema de Alarmas
- **RF-M04 (Notificaciones Push):** Envío de alertas a móviles incluso si la app está cerrada (Firebase/APNs).
- **RF-M05 (SignalR Integration):** El disparo de alarmas debe ser instantáneo entre dispositivos activos.

## 4. Gestión de Datos y Seguridad
- **RF-M06 (EF Core Migrations):** Gestión estructurada de la base de datos PostgreSQL mediante migraciones de Entity Framework.
- **RF-M07 (Vault/Secrets):** Las API Keys de la IA deben almacenarse de forma segura (User Secrets en desarrollo / Azure Key Vault en producción).
