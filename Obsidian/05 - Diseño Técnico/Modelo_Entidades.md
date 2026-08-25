# Modelo de Entidades - TodoAppRoll V2 (.NET Core)

## 1. Arquitectura de Datos
El sistema utilizará **Entity Framework Core (EF Core)** con una aproximación *Code-First*. Se emplearán Data Annotations y Fluent API para definir restricciones y relaciones.

## 2. Entidades Principales

### 2.1 User
Representa al usuario dentro del sistema y sus preferencias globales.
```csharp
public class User {
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Preferencias de Usuario (V2)
    public string Theme { get; set; } // Light, Dark, System
    public bool NotificationsEnabled { get; set; }

    // Relaciones
    public ICollection<Task> Tasks { get; set; }
    public ICollection<Alarm> Alarms { get; set; }
    public ICollection<Media> MediaFiles { get; set; }
    public ICollection<AIChatSession> ChatSessions { get; set; }
}
```

### 2.2 Task & Subtask
Gestión de actividades con soporte para jerarquía.
```csharp
public enum TaskPriority { Low, Medium, High }
public enum TaskStatus { Pending, InProgress, Completed, Archived }

public class Task {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public int Order { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Relaciones
    public User User { get; set; }
    public ICollection<Subtask> Subtasks { get; set; }
    public Guid? AlarmId { get; set; }
    public Alarm Alarm { get; set; }
}

public class Subtask {
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Title { get; set; }
    public bool IsDone { get; set; }
    public int Order { get; set; }
    
    public Task Task { get; set; }
}
```

### 2.3 Alarm
Soporte para Pomodoro y alarmas programadas.
```csharp
public enum AlarmType { Pomodoro, DateTime }

public class Alarm {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public AlarmType Type { get; set; }
    public int? DurationMinutes { get; set; } // Para Pomodoro
    public DateTime? ScheduleAt { get; set; } // Para DateTime
    public bool IsActive { get; set; }

    // Personalización Multimedia
    public Guid? AudioId { get; set; }
    public Media Audio { get; set; }
    public Guid? ImageId { get; set; }
    public Media Image { get; set; }

    public User User { get; set; }
}
```

### 2.4 Media
Metadatos de archivos gestionados en Cloudinary.
```csharp
public class Media {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string PublicId { get; set; } // ID en Cloudinary
    public string FileType { get; set; } // "audio" o "image"
    public DateTime CreatedAt { get; set; }

    public User User { get; set; }
}
```

## 3. Entidades de Inteligencia Artificial (Nuevas)

### 3.1 AIChatSession & Message
Para el asistente "Artemis".
```csharp
public class AIChatSession {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AIMessage> Messages { get; set; }
    public User User { get; set; }
}

public class AIMessage {
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } // "user" o "assistant"
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }

    public AIChatSession Session { get; set; }
}
```

### 3.2 AISmartSuggestion
Sugerencias proactivas generadas por el motor de Python.
```csharp
public class AISmartSuggestion {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SuggestionType { get; set; } // "Rescheduling", "Priority", "Habit"
    public string Content { get; set; }
    public bool IsApplied { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; }
}
```

## 3.3 Note & NoteLink (Base de Conocimiento — RF-KB)
Notas markdown con propiedades estructuradas (frontmatter) y enlaces bidireccionales.
```csharp
public class Note {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; } // Cuerpo markdown
    public string PropertiesJson { get; set; } // Frontmatter: tags, tipo, estado, etc. (jsonb)
    public Guid? TaskId { get; set; } // Asociación opcional a una tarea/proyecto existente
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; }
    public Task? Task { get; set; }
    public ICollection<NoteLink> OutgoingLinks { get; set; }
    public ICollection<NoteLink> IncomingLinks { get; set; }
}

public class NoteLink {
    public Guid Id { get; set; }
    public Guid SourceNoteId { get; set; }
    public Guid TargetNoteId { get; set; }

    public Note SourceNote { get; set; }
    public Note TargetNote { get; set; }
}
```
Nota: el embedding para búsqueda semántica (RF-KB05, pgvector) se agrega recién cuando exista el microservicio Python que lo genere y consuma (Fase 3 del roadmap) — no antes, para no dejar una columna sin uso real.

## 4. Auditoría
```csharp
public class AuditLog {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; }
    public string EntityName { get; set; }
    public string PayloadJson { get; set; }
    public DateTime Timestamp { get; set; }

    public User User { get; set; }
}
```
