import uuid
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Header, HTTPException
from google import genai
from pydantic import BaseModel
from sqlalchemy.orm import Session

from app.config import GEMINI_API_KEY, INTERNAL_API_KEY
from app.db import get_db
from app.embeddings import embed_texts
from app.models import AIMessage, Alarm, Note, NoteEmbedding, Task

router = APIRouter(prefix="/internal", tags=["chat"])

CHAT_MODEL = "gemini-3.6-flash"  # mismo modelo probado en producción en uma-backend
TOP_K_CHUNKS = 5
HISTORY_MESSAGES = 10

_client = genai.Client(api_key=GEMINI_API_KEY)


def require_internal_key(x_internal_key: str = Header(...)) -> None:
    if x_internal_key != INTERNAL_API_KEY:
        raise HTTPException(status_code=401, detail="Clave interna inválida")


class ChatRequest(BaseModel):
    user_id: uuid.UUID
    session_id: uuid.UUID
    message: str


class ChatResponse(BaseModel):
    reply: str


def _retrieve_note_chunks(db: Session, user_id: uuid.UUID, message: str) -> list[str]:
    [query_vector] = embed_texts([message], task_type="RETRIEVAL_QUERY")
    rows = (
        db.query(NoteEmbedding)
        .filter(NoteEmbedding.user_id == user_id)
        .order_by(NoteEmbedding.embedding.cosine_distance(query_vector))
        .limit(TOP_K_CHUNKS)
        .all()
    )
    return [row.chunk_text for row in rows]


def _upcoming_tasks_summary(db: Session, user_id: uuid.UUID) -> str:
    now = datetime.now(timezone.utc)
    week_from_now = now + timedelta(days=7)

    rows = (
        db.query(Task.Title, Alarm.ScheduleAt)
        .join(Alarm, Task.AlarmId == Alarm.Id)
        .filter(
            Task.UserId == user_id,
            Task.Status.in_([0, 1]),  # Pending, InProgress
            Alarm.IsActive.is_(True),
            Alarm.ScheduleAt.isnot(None),
            Alarm.ScheduleAt >= now,
            Alarm.ScheduleAt <= week_from_now,
        )
        .order_by(Alarm.ScheduleAt)
        .all()
    )

    if not rows:
        return "El usuario no tiene tareas programadas (con alarma) para los próximos 7 días."

    lines = [f"- {title} (programada para {schedule_at:%Y-%m-%d %H:%M} UTC)" for title, schedule_at in rows]
    return "Tareas del usuario programadas para los próximos 7 días:\n" + "\n".join(lines)


def _recent_history(db: Session, session_id: uuid.UUID) -> list[AIMessage]:
    rows = (
        db.query(AIMessage)
        .filter(AIMessage.SessionId == session_id)
        .order_by(AIMessage.CreatedAt.desc())
        .limit(HISTORY_MESSAGES)
        .all()
    )
    return list(reversed(rows))


def _build_prompt(note_chunks: list[str], tasks_summary: str, history: list[AIMessage], message: str) -> str:
    notes_section = (
        "\n\n".join(f"- {chunk}" for chunk in note_chunks)
        if note_chunks
        else "No se encontraron notas relevantes para esta pregunta."
    )
    history_section = "\n".join(f"{m.Role}: {m.Content}" for m in history) if history else "(sin mensajes previos)"

    return f"""Sos el asistente personal de IA de AsistenteArtemis. Respondé en español, de forma directa y concreta, basándote solo en el contexto real provisto abajo -- si el contexto no alcanza para responder, decilo en vez de inventar.

# Notas relevantes del usuario
{notes_section}

# {tasks_summary}

# Historial reciente de la conversación
{history_section}

# Mensaje actual del usuario
{message}"""


@router.post("/chat", response_model=ChatResponse, dependencies=[Depends(require_internal_key)])
def chat(request: ChatRequest, db: Session = Depends(get_db)) -> ChatResponse:
    note_chunks = _retrieve_note_chunks(db, request.user_id, request.message)
    tasks_summary = _upcoming_tasks_summary(db, request.user_id)
    history = _recent_history(db, request.session_id)

    prompt = _build_prompt(note_chunks, tasks_summary, history, request.message)

    try:
        interaction = _client.interactions.create(
            model=CHAT_MODEL,
            input=prompt,
            generation_config={"thinking_level": "low"},
        )
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"No se pudo generar la respuesta (falló la API de Gemini): {e}")

    return ChatResponse(reply=interaction.output_text)
