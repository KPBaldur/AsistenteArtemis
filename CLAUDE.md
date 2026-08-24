# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This repo tracks the migration of **TodoAppRoll** — a task/todo manager with a Pomodoro-style alarm system and a virtual "pet" assistant called **Artemis** — from a legacy Node.js/React prototype to a new .NET-based multiplatform architecture. It contains three coexisting parts plus the design docs that connect them:

- **`TodoApprRoll/`** — the **legacy** app (its own nested git repo, remote `KPBaldur/TodoApprRoll`). Node/Express/TypeScript + Prisma/PostgreSQL backend, React (Vite) frontend. This is the currently deployed/working version (Vercel + Render), used as the functional reference for the rewrite.
- **`TodoAppRoll.Api/`** — the **new backend**: ASP.NET Core 8 Web API + EF Core (Npgsql/PostgreSQL) + SignalR, replacing the Node backend.
- **`TodoAppRoll.Web/`** — the **new frontend**: React 18 + Vite + TypeScript + Zustand + TanStack Query, meant to consume `TodoAppRoll.Api` instead of the legacy backend.
- **`Obsidian/`** — an Obsidian vault with the planning/design docs for this migration (legacy audit, roadmap, requirements, architecture proposal, entity design, frontend design, task log). Read `Obsidian/README.md` for the index; start with `Obsidian/01 - Informe Legacy/` and `Obsidian/04 - Propuesta Nueva Arquitectura/` for background before making architectural decisions in `.Api`/`.Web`.

The root folder itself is a git repo (remote `KPBaldur/AsistenteArtemis`) with **no commits yet** — everything is currently untracked. `TodoApprRoll/` is a separately-initialized git repo nested inside (not a submodule; there is no `.gitmodules`), so root-level git commands will not see its history.

Target end-state per the design docs (not yet implemented): a Python (FastAPI) AI microservice providing the "Artemis" chatbot / smart suggestions, and a React Native mobile client, both consuming the same .NET API.

## Development Commands

### New frontend — `TodoAppRoll.Web/`
```
npm install
npm run dev        # Vite dev server
npm run build       # tsc -b && vite build
npm run lint         # eslint .
npm run preview
```
Backend URL is read from `VITE_API_URL` in `.env.local` (currently `https://localhost:7214/api`). No test script is configured.

### New backend — `TodoAppRoll.Api/`
.NET 8 Web API. Build/run with the standard SDK (`dotnet build`, `dotnet run` from `TodoAppRoll.Api/`, or open `TodoAppRoll.Api.sln` in Visual Studio). No test project exists in the solution. Requires a PostgreSQL connection string in `appsettings.json` (`ConnectionStrings:DefaultConnection`) and JWT settings (`Jwt:Key/Issuer/Audience`) — currently committed as placeholder/dev values, treat as needing rotation before any real deployment.

### Legacy app — `TodoApprRoll/backend/` and `TodoApprRoll/frontend/`
Backend:
```
npm install
npm run dev          # tsx src/server.ts
npm run build         # tsc
npm start              # node dist/server.js
```
Frontend: same `dev`/`build`/`lint`/`preview` scripts as the new frontend (Vite + eslint). Backend uses Prisma; schema at `TodoApprRoll/backend/prisma/schema.prisma`, seed via `prisma db seed` (configured as `ts-node prisma/seed.ts`). No test scripts in either package.json.

## Architecture

### Legacy backend (`TodoApprRoll/backend/src/`)
Express + TypeScript, layered as `controllers/` → `services/` → Prisma client, with `routes/`, `middleware/` (JWT auth), `utils/`, `types/`. Key model in `prisma/schema.prisma`: `User` → `Task` (with `Subtask[]`, priority/status as free-text strings, ordering via `order`) and `Alarm` (type `"pomodoro"` or `"datetime"`, optional linked `Media` for custom audio/image). Real-time alarm delivery is via **SSE**, driven by an in-memory `schedulerService` that sets timeouts and re-initializes pending alarms on server start (`initializeAlarms`). Media files are stored in Cloudinary; only metadata lives in Postgres. See `Obsidian/01 - Informe Legacy/Informe_Funcionamiento_Legacy.md` for the full data flow and known limitations (scheduler is memory-bound, SSE scaling, monolithic controllers).

### New backend (`TodoAppRoll.Api/`)
Standard ASP.NET Core controller-based API:
- `Models/Entities/` — EF Core entities: `User`, `Task`/Subtask (in `Task.cs`), `Alarm`, `Media`, `AuditLog`, plus AI-related entities in `AIEntities.cs` (`AIChatSession`, `AIMessage`, `AISmartSuggestion` — scaffolding for the future Artemis AI service, not yet wired to any AI backend).
- `Models/DTOs/` — request/response DTOs per feature area (Auth, Task, Alarm, Media, User).
- `Models/Enums/AppEnums.cs` — shared enums (`TaskPriority`, `TaskStatus`, `AlarmType`) replacing the legacy free-text fields.
- `Data/AppDbContext.cs` — EF Core context, Fluent API relationship config.
- `Controllers/` — one per resource (`AuthController`, `TaskController`, `AlarmController`, `MediaController`, `UserController`, `AuditController`); auth uses JWT + BCrypt, most controllers are `[Authorize]`-protected.
- `Services/CloudinaryService.cs` (`ICloudinaryService`) — same Cloudinary-backed media storage pattern as legacy.
- `Hubs/AlarmHub.cs` — SignalR hub replacing SSE for alarm delivery, grouped by `UserId`; JWT is accepted via `access_token` query string for the `/hubs/alarms` path (see the `OnMessageReceived` handler in `Program.cs`) since SignalR clients can't set an Authorization header on the initial handshake.
- `Program.cs` wires: EF Core + Npgsql, JWT bearer auth, SignalR, Swagger (dev only).

### New frontend (`TodoAppRoll.Web/src/`)
`api/` (Axios client + per-resource services) → `hooks/` (TanStack Query hooks, e.g. `useTasks`) → `pages/`/`components/`. `store/authStore.ts` is a persisted Zustand store for auth state. `types/index.ts` is meant to mirror the C# entities/DTOs — when changing a DTO in `TodoAppRoll.Api`, update this file to match. The intended fuller structure (per `Obsidian/08 - Diseño Frontend/Arquitectura_Frontend.md`, not all present yet) adds `components/pet/` for the Artemis virtual-pet UI (Framer Motion) and a `useSignalR` hook for the alarm hub — check whether these exist before assuming legacy SSE patterns still apply.

## Conventions / notes observed

- New backend and frontend deliberately replace legacy free-text `status`/`priority`/alarm-`type` strings with typed enums (`.Api/Models/Enums/AppEnums.cs`) — don't reintroduce string-typed status fields when porting legacy features.
- Both frontend projects (legacy and new) use the same script names (`dev`/`build`/`lint`/`preview`); the new one is on Tailwind v4 + React 18, the legacy one on Tailwind v3 + React 19 — check `package.json` in the specific folder before assuming a dependency version.
- No automated tests exist anywhere in the repo (backend or frontend, legacy or new). Don't assume a test runner/config exists.
- Per the migration task log (`Obsidian/06 - Tareas y Puntos Desarrollados/Tareas_2026-05-08.md`), the new frontend had an unresolved "blank screen" bug on the Login page as of the last recorded session — backend was reported fully working, frontend routing/services were the suspect area.
