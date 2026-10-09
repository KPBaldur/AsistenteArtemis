import asyncio
import logging
import time
import uuid
from datetime import datetime, timezone

from google.genai import errors as genai_errors

from app import vault
from app.db import SessionLocal
from app.embeddings import embed_texts
from app.models import VaultChunk

logger = logging.getLogger("brain.vault_sync")

POLL_INTERVAL_SECONDS = 10 * 60
_EMBED_BATCH = 50  # por debajo del tope de contenidos por request de embed_content
_RATE_LIMIT_WAIT_SECONDS = 60  # la ventana de la cuota es de 1 minuto
_MAX_RATE_LIMIT_RETRIES = 3

# Estado del último sync, en memoria -- lo expone GET /internal/vault/status.
last_sync: dict = {"at": None, "commit": None, "reembedded": [], "deleted": [], "error": None}


def _stored_hashes(db) -> dict[str, str]:
    return dict(db.query(VaultChunk.path, VaultChunk.content_hash).distinct().all())


def _embed_with_backoff(texts: list[str]) -> list[list[float]]:
    """La capa gratuita de Gemini limita los embeddings a 100 textos/minuto: un
    sync inicial (~500 chunks) choca con el 429 enseguida. El sync corre en un
    thread aparte, así que esperar acá no bloquea al resto del Brain."""
    for attempt in range(_MAX_RATE_LIMIT_RETRIES + 1):
        try:
            return embed_texts(texts, task_type="RETRIEVAL_DOCUMENT")
        except genai_errors.ClientError as e:
            if e.code != 429 or attempt == _MAX_RATE_LIMIT_RETRIES:
                raise
            logger.info("Cuota de embeddings agotada, esperando %ds", _RATE_LIMIT_WAIT_SECONDS)
            time.sleep(_RATE_LIMIT_WAIT_SECONDS)


def _reembed_file(db, rel_path: str, text: str, file_hash: str) -> None:
    db.query(VaultChunk).filter(VaultChunk.path == rel_path).delete()
    chunks = vault.chunk_markdown(rel_path, text)
    for start in range(0, len(chunks), _EMBED_BATCH):
        batch = chunks[start:start + _EMBED_BATCH]
        vectors = _embed_with_backoff([c.text for c in batch])
        for chunk, vector in zip(batch, vectors):
            db.add(VaultChunk(
                id=uuid.uuid4(), path=chunk.path, doc_type=chunk.doc_type, project=chunk.project,
                heading=chunk.heading, chunk_index=chunk.chunk_index, chunk_text=chunk.text,
                embedding=vector, content_hash=file_hash,
            ))
    db.commit()


def vault_sync_once() -> dict:
    """Pull + re-embebe solo los archivos cuyo hash cambió + borra los que ya no
    existen. Un error en un archivo no frena al resto."""
    vault.pull()
    reembedded, deleted, failed = [], [], []
    db = SessionLocal()
    try:
        stored = _stored_hashes(db)
        seen = set()
        for rel_path, text in vault.iter_documents():
            seen.add(rel_path)
            file_hash = vault.content_hash(text)
            if stored.get(rel_path) == file_hash:
                continue
            try:
                _reembed_file(db, rel_path, text, file_hash)
                reembedded.append(rel_path)
            except Exception:
                db.rollback()
                failed.append(rel_path)
                logger.exception("Error embebiendo %s del vault", rel_path)
        for rel_path in set(stored) - seen:
            db.query(VaultChunk).filter(VaultChunk.path == rel_path).delete()
            deleted.append(rel_path)
        db.commit()
    finally:
        db.close()

    if reembedded or deleted:
        logger.info("Vault sincronizado: %d re-embebidos, %d borrados", len(reembedded), len(deleted))
    last_sync.update(
        at=datetime.now(timezone.utc).isoformat(), commit=vault.head_commit(),
        reembedded=reembedded, deleted=deleted, error=f"Fallaron: {', '.join(failed)}" if failed else None,
    )
    return dict(last_sync)


async def vault_sync_loop() -> None:
    while True:
        try:
            await asyncio.to_thread(vault_sync_once)
        except Exception as e:
            last_sync["error"] = str(e)
            logger.exception("Error en el ciclo de sincronización del vault")
        await asyncio.sleep(POLL_INTERVAL_SECONDS)
