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

## 5. Base de Conocimiento ("Cerebro" — fusión Notion + Obsidian)

Módulo nuevo, sin precedente en V1: un sistema de notas personal, siempre disponible desde la app, que sirve como memoria externa de proyectos y procedimientos. Enfoque híbrido decidido el 2026-08-25: notas en markdown (como Obsidian) con propiedades estructuradas en frontmatter (como Notion), enlaces bidireccionales entre notas, y contenido escrito desde cero en la app (no sincroniza con vaults externos de Obsidian — descartado por complejidad de sincronización de archivos).

- **RF-KB01 (Notas con propiedades):** El usuario debe poder crear notas en markdown con propiedades estructuradas (tags, proyecto asociado, tipo, estado) a modo de frontmatter, editables desde cualquier cliente.
- **RF-KB02 (Enlaces bidireccionales):** El sistema debe soportar enlaces `[[nota]]` entre notas y mantener el grafo de conexiones resultante.
- **RF-KB03 (Vista tipo base de datos):** El usuario debe poder filtrar y navegar sus notas por propiedades (proyecto, tags, tipo, fecha), similar a una vista de base de datos de Notion.
- **RF-KB04 (Asociación a proyectos/tareas):** El usuario debe poder asociar una nota a un Proyecto o Tarea ya existente en el sistema.
- **RF-KB05 (Indexado semántico):** El sistema debe generar embeddings del contenido de cada nota (pgvector) para habilitar búsqueda semántica, no solo por texto exacto.
- **RF-KB06 (Consulta vía IA / RAG):** El motor Artemis debe poder responder preguntas de memoria usando RAG sobre esta base de conocimiento — ej. "olvidé cómo se hacía X, ayudame a recordar" o "resumime en qué quedó el proyecto Y".
- **RF-KB07 (Disponibilidad multiplataforma):** La base de conocimiento debe estar disponible con la misma experiencia en desktop, APK y web.

### Historias de usuario
- Como usuario, quiero escribir una nota con propiedades estructuradas (proyecto, tags) para poder encontrarla filtrando después, no solo buscando texto.
- Como usuario, quiero enlazar notas entre sí para construir una red de conocimiento navegable, igual que en Obsidian.
- Como usuario, quiero preguntarle a Artemis "olvidé cómo se hacía esto, ayudame a refrescar la memoria" y que me responda citando la nota relevante.
- Como usuario, quiero pedirle a Artemis un resumen de en qué quedó un proyecto que dejé pausado, basado en mis notas y tareas asociadas.
- Como usuario, quiero ver todas mis notas de un proyecto en una vista filtrable por sus propiedades, sin tener que abrir cada una.
