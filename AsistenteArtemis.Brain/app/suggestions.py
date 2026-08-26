import asyncio
import logging
import uuid
from datetime import datetime, timedelta, timezone

from google import genai
from sqlalchemy.orm import Session

from app.config import GEMINI_API_KEY
from app.db import SessionLocal
from app.identity import build_system_instruction
from app.models import AISmartSuggestion, Alarm, Note, Task

logger = logging.getLogger("brain.suggestions")

POLL_INTERVAL_SECONDS = 60 * 60  # cada 1h alcanza -- no son chequeos urgentes, y evita gastar cuota de Gemini de más
SUGGESTION_MODEL = "gemini-3.6-flash"
WEEKLY_SUMMARY_COOLDOWN_DAYS = 7
RESCHEDULING_COOLDOWN_DAYS = 3

_client = genai.Client(api_key=GEMINI_API_KEY)


def _generate(system_instruction: str, task_description: str) -> str:
    prompt = f"{system_instruction}\n\n{task_description}"
    interaction = _client.interactions.create(
        model=SUGGESTION_MODEL,
        input=prompt,
        generation_config={"thinking_level": "low"},
    )
    return interaction.output_text


def _active_user_ids(db: Session) -> list[uuid.UUID]:
    return [row[0] for row in db.query(Task.UserId).distinct().all()]


def _check_weekly_summary(db: Session, user_id: uuid.UUID, now: datetime) -> None:
    last = (
        db.query(AISmartSuggestion)
        .filter(AISmartSuggestion.UserId == user_id, AISmartSuggestion.SuggestionType == "WeeklySummary")
        .order_by(AISmartSuggestion.CreatedAt.desc())
        .first()
    )
    if last and (now - last.CreatedAt) < timedelta(days=WEEKLY_SUMMARY_COOLDOWN_DAYS):
        return

    week_ago = now - timedelta(days=7)

    completed = (
        db.query(Task.Title)
        .filter(Task.UserId == user_id, Task.Status == 2, Task.CompletedAt >= week_ago)
        .all()
    )
    pending = (
        db.query(Task.Title)
        .filter(Task.UserId == user_id, Task.Status.in_([0, 1]))
        .all()
    )
    notes = (
        db.query(Note.Title)
        .filter(Note.UserId == user_id, Note.UpdatedAt >= week_ago)
        .all()
    )

    if not completed and not pending and not notes:
        return  # nada que resumir, no vale la pena gastar una llamada a Gemini

    lines = ["Generá un resumen semanal corto (3-5 líneas) de la actividad del usuario, en tu voz configurada."]
    lines.append(f"Tareas completadas esta semana: {', '.join(t for (t,) in completed) or '(ninguna)'}")
    lines.append(f"Tareas todavía pendientes: {', '.join(t for (t,) in pending) or '(ninguna)'}")
    lines.append(f"Notas creadas o actualizadas esta semana: {', '.join(t for (t,) in notes) or '(ninguna)'}")

    system_instruction = build_system_instruction(db, user_id)
    try:
        content = _generate(system_instruction, "\n".join(lines))
    except Exception:
        logger.exception("Error generando el resumen semanal para %s", user_id)
        return

    db.add(
        AISmartSuggestion(
            Id=uuid.uuid4(),
            UserId=user_id,
            SuggestionType="WeeklySummary",
            Content=content,
            RelatedEntityId=None,
            IsApplied=False,
            CreatedAt=now,
        )
    )
    db.commit()
    logger.info("Resumen semanal generado para %s", user_id)


def _check_rescheduling(db: Session, user_id: uuid.UUID, now: datetime) -> None:
    # AlarmSchedulerService (lado .NET) pone ScheduleAt en null al disparar una
    # alarma de tipo DateTime (no solo IsActive=false) -- por eso acá NO se
    # puede filtrar por "ScheduleAt < now", esa columna ya no tiene valor una
    # vez que sonó. IsActive=false alcanza para identificar "sonó (o se
    # desactivó) y la tarea sigue sin completarse".
    overdue_tasks = (
        db.query(Task)
        .join(Alarm, Task.AlarmId == Alarm.Id)
        .filter(
            Task.UserId == user_id,
            Task.Status.in_([0, 1]),
            Alarm.IsActive.is_(False),
        )
        .all()
    )

    for task in overdue_tasks:
        recent = (
            db.query(AISmartSuggestion)
            .filter(
                AISmartSuggestion.UserId == user_id,
                AISmartSuggestion.SuggestionType == "Rescheduling",
                AISmartSuggestion.RelatedEntityId == task.Id,
                AISmartSuggestion.CreatedAt >= now - timedelta(days=RESCHEDULING_COOLDOWN_DAYS),
            )
            .first()
        )
        if recent:
            continue

        system_instruction = build_system_instruction(db, user_id)
        task_description = (
            f"La tarea '{task.Title}' tenía una alarma programada que ya sonó, pero la tarea sigue sin "
            "completarse. Sugerile al usuario, en una o dos líneas, que la reprograme o la marque como hecha."
        )
        try:
            content = _generate(system_instruction, task_description)
        except Exception:
            logger.exception("Error generando sugerencia de reprogramación para tarea %s", task.Id)
            continue

        db.add(
            AISmartSuggestion(
                Id=uuid.uuid4(),
                UserId=user_id,
                SuggestionType="Rescheduling",
                Content=content,
                RelatedEntityId=task.Id,
                IsApplied=False,
                CreatedAt=now,
            )
        )
        db.commit()
        logger.info("Sugerencia de reprogramación generada para tarea %s (usuario %s)", task.Id, user_id)


def check_all_users_once() -> None:
    db = SessionLocal()
    try:
        now = datetime.now(timezone.utc)
        for user_id in _active_user_ids(db):
            _check_weekly_summary(db, user_id, now)
            _check_rescheduling(db, user_id, now)
    finally:
        db.close()


async def suggestions_loop() -> None:
    while True:
        try:
            await asyncio.to_thread(check_all_users_once)
        except Exception:
            logger.exception("Error en el ciclo de sugerencias proactivas")
        await asyncio.sleep(POLL_INTERVAL_SECONDS)
