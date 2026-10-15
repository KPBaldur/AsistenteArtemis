import uuid
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Header, HTTPException
from google import genai
from pydantic import BaseModel
from sqlalchemy.orm import Session

from app import actions, vault
from app.config import (
    GEMINI_API_KEY, GEMINI_CHAT_MODEL, GEMINI_RESEARCH_MODEL, INTERNAL_API_KEY, VAULT_ENABLED, VAULT_OWNER_USER_ID,
)
from app.db import get_db
from app.embeddings import embed_texts
from app.identity import build_system_instruction
from app.models import AIMessage, Alarm, Note, NoteEmbedding, Task, VaultChunk

router = APIRouter(prefix="/internal", tags=["chat"])

CHAT_MODEL = GEMINI_CHAT_MODEL  # por defecto gemini-3.6-flash, el probado en producción en uma-backend
TOP_K_CHUNKS = 5
TOP_K_VAULT_CHUNKS = 6
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


def _retrieve_note_chunks(db: Session, user_id: uuid.UUID, query_vector: list[float]) -> list[str]:
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


def _vault_section(db: Session, user_id: uuid.UUID, query_vector: list[float]) -> str | None:
    """Contexto del cerebro de proyectos (BrainObsidian), solo para el dueño
    del vault. La tabla de estado va siempre (es chica); los fragmentos, por
    similitud con el mensaje."""
    if not VAULT_ENABLED or str(user_id) != VAULT_OWNER_USER_ID:
        return None
    try:
        status = vault.project_status_text()
    except Exception:
        status = "(no se pudo leer el estado de los proyectos del vault)"
    rows = (
        db.query(VaultChunk.chunk_text)
        .order_by(VaultChunk.embedding.cosine_distance(query_vector))
        .limit(TOP_K_VAULT_CHUNKS)
        .all()
    )
    fragments = "\n\n".join(text for (text,) in rows) if rows else "(el vault todavía no está indexado)"
    return f"""{actions.tools_instruction_with_projects()}

# Estado de los proyectos del usuario (cerebro de proyectos)
{status}

# Fragmentos relevantes del cerebro de proyectos
Cada fragmento empieza con [ruta · proyecto · tipo]. Si usás uno, mencioná de qué nota sale.

{fragments}"""


def _recent_history(db: Session, session_id: uuid.UUID) -> list[AIMessage]:
    rows = (
        db.query(AIMessage)
        .filter(AIMessage.SessionId == session_id)
        .order_by(AIMessage.CreatedAt.desc())
        .limit(HISTORY_MESSAGES)
        .all()
    )
    return list(reversed(rows))


def _build_prompt(
    system_instruction: str,
    note_chunks: list[str],
    tasks_summary: str,
    history: list[AIMessage],
    message: str,
    vault_section: str | None = None,
) -> str:
    notes_section = (
        "\n\n".join(f"- {chunk}" for chunk in note_chunks)
        if note_chunks
        else "No se encontraron notas relevantes para esta pregunta."
    )
    history_section = "\n".join(f"{m.Role}: {m.Content}" for m in history) if history else "(sin mensajes previos)"
    vault_block = f"\n\n{vault_section}" if vault_section else ""

    return f"""{system_instruction}

# Notas relevantes del usuario
{notes_section}

# {tasks_summary}{vault_block}

# Historial reciente de la conversación
{history_section}

# Mensaje actual del usuario
{message}"""


@router.post("/chat", response_model=ChatResponse, dependencies=[Depends(require_internal_key)])
def chat(request: ChatRequest, db: Session = Depends(get_db)) -> ChatResponse:
    is_vault_owner = VAULT_ENABLED and str(request.user_id) == VAULT_OWNER_USER_ID
    if is_vault_owner:
        # Respuesta a una propuesta de escritura del turno anterior (sí/no): no gasta Gemini.
        pending_reply = actions.resolve_pending(request.session_id, request.message)
        if pending_reply is not None:
            return ChatResponse(reply=pending_reply)

    system_instruction = build_system_instruction(db, request.user_id)
    [query_vector] = embed_texts([request.message], task_type="RETRIEVAL_QUERY")
    note_chunks = _retrieve_note_chunks(db, request.user_id, query_vector)
    tasks_summary = _upcoming_tasks_summary(db, request.user_id)
    vault_section = _vault_section(db, request.user_id, query_vector)
    history = _recent_history(db, request.session_id)

    prompt = _build_prompt(
        system_instruction, note_chunks, tasks_summary, history, request.message, vault_section
    )

    try:
        interaction = _client.interactions.create(
            model=CHAT_MODEL,
            input=prompt,
            generation_config={"thinking_level": "low"},
            **({"tools": actions.TOOLS} if is_vault_owner else {}),
        )
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"No se pudo generar la respuesta (falló la API de Gemini): {e}")

    if is_vault_owner:
        research_requests = actions.research_requests(interaction)
        if research_requests:
            return ChatResponse(
                reply=actions.research_reply(request.session_id, research_requests, _client, GEMINI_RESEARCH_MODEL)
            )
        confirmation = actions.propose_from_interaction(request.session_id, interaction)
        if confirmation is not None:
            return ChatResponse(reply=confirmation)

    return ChatResponse(reply=interaction.output_text)
