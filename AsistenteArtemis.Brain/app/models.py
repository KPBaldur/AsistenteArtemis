import uuid
from datetime import datetime

from pgvector.sqlalchemy import Vector
from sqlalchemy import Boolean, DateTime, Integer, Text, func
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db import Base

# Dimensión del modelo de embeddings de Gemini usado en app/embeddings.py.
EMBEDDING_DIM = 768


class NoteEmbedding(Base):
    """Tabla propia del Brain. Las notas en sí (`Notes`) las sigue dueñando
    TodoAppRoll.Api (EF Core) -- esta tabla solo referencia su `Id` por valor,
    sin FK real entre los dos esquemas."""

    __tablename__ = "note_embeddings"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    note_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), index=True)
    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), index=True)
    chunk_index: Mapped[int] = mapped_column(Integer)
    chunk_text: Mapped[str] = mapped_column(Text)
    embedding: Mapped[list] = mapped_column(Vector(EMBEDDING_DIM))
    # Snapshot de Note.UpdatedAt al momento de embeber -- permite detectar
    # notas desactualizadas sin depender de un timestamp propio del Brain.
    note_updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


# --- Modelos de solo lectura sobre tablas que dueña TodoAppRoll.Api (EF Core) ---
# Nombres de tabla/columna en PascalCase exacto porque EF Core los crea así
# (confirmado contra las migraciones .NET), no se tocan desde acá con Alembic.


class Note(Base):
    __tablename__ = "Notes"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    UserId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True))
    Title: Mapped[str] = mapped_column(Text)
    Content: Mapped[str] = mapped_column(Text)
    UpdatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class Task(Base):
    __tablename__ = "Tasks"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    UserId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True))
    Title: Mapped[str] = mapped_column(Text)
    Status: Mapped[int] = mapped_column(Integer)  # TaskStatus: 0 Pending, 1 InProgress, 2 Completed, 3 Archived
    AlarmId: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)


class Alarm(Base):
    __tablename__ = "Alarms"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    # Único campo de fecha que existe para "tareas programadas" -- Task no
    # tiene columna de vencimiento propia, solo la Alarm asociada la tiene.
    ScheduleAt: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    IsActive: Mapped[bool] = mapped_column(Boolean)


class AIChatSession(Base):
    __tablename__ = "AIChatSessions"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    UserId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True))


class AIMessage(Base):
    __tablename__ = "AIMessages"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    SessionId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), index=True)
    Role: Mapped[str] = mapped_column(Text)
    Content: Mapped[str] = mapped_column(Text)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True))
