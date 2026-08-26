import asyncio
import logging
import uuid

from app.db import SessionLocal
from app.embeddings import chunk_note, embed_texts
from app.models import Note, NoteEmbedding

logger = logging.getLogger("brain.sync")

POLL_INTERVAL_SECONDS = 5 * 60


def _needs_reembed(db, note: Note) -> bool:
    latest = (
        db.query(NoteEmbedding.note_updated_at)
        .filter(NoteEmbedding.note_id == note.Id)
        .order_by(NoteEmbedding.note_updated_at.desc())
        .first()
    )
    return latest is None or latest[0] < note.UpdatedAt


def _reembed_note(db, note: Note) -> None:
    db.query(NoteEmbedding).filter(NoteEmbedding.note_id == note.Id).delete()

    chunks = chunk_note(note.Content)
    if not chunks:
        db.commit()
        return

    vectors = embed_texts(chunks, task_type="RETRIEVAL_DOCUMENT")
    for index, (text, vector) in enumerate(zip(chunks, vectors)):
        db.add(
            NoteEmbedding(
                id=uuid.uuid4(),
                note_id=note.Id,
                user_id=note.UserId,
                chunk_index=index,
                chunk_text=text,
                embedding=vector,
                note_updated_at=note.UpdatedAt,
            )
        )
    db.commit()


def sync_once() -> None:
    db = SessionLocal()
    try:
        notes = db.query(Note).all()
        for note in notes:
            if _needs_reembed(db, note):
                try:
                    _reembed_note(db, note)
                    logger.info("Nota '%s' (%s) re-embebida", note.Title, note.Id)
                except Exception:
                    db.rollback()
                    logger.exception("Error embebiendo la nota %s", note.Id)
    finally:
        db.close()


async def sync_loop() -> None:
    while True:
        try:
            await asyncio.to_thread(sync_once)
        except Exception:
            logger.exception("Error en el ciclo de sincronización de embeddings")
        await asyncio.sleep(POLL_INTERVAL_SECONDS)
