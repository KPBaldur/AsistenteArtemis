import uuid

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy import func
from sqlalchemy.orm import Session

from app import vault, vault_write
from app.chat import require_internal_key
from app.config import VAULT_ENABLED, VAULT_OWNER_USER_ID, VAULT_PATH
from app.db import get_db
from app.models import VaultChunk
from app.vault_sync import last_sync, vault_sync_once

router = APIRouter(prefix="/internal/vault", tags=["vault"], dependencies=[Depends(require_internal_key)])


def _require_enabled() -> None:
    if not VAULT_ENABLED:
        raise HTTPException(status_code=409, detail="Vault desactivado: falta VAULT_PATH o VAULT_OWNER_USER_ID")


@router.get("/status")
def status(db: Session = Depends(get_db)) -> dict:
    if not VAULT_ENABLED:
        return {"enabled": False}
    documents, chunks = db.query(func.count(func.distinct(VaultChunk.path)), func.count(VaultChunk.id)).one()
    return {"enabled": True, "path": VAULT_PATH, "documents": documents, "chunks": chunks, "last_sync": last_sync}


@router.post("/sync", dependencies=[Depends(_require_enabled)])
def sync() -> dict:
    return vault_sync_once()


def _require_owner(user_id: uuid.UUID | None) -> None:
    """El vault es de una sola persona: lo que llega en nombre de otro usuario se rechaza."""
    if user_id is not None and str(user_id) != VAULT_OWNER_USER_ID:
        raise HTTPException(status_code=403, detail="El cerebro de proyectos no pertenece a este usuario")


@router.get("/projects", dependencies=[Depends(_require_enabled)])
def projects(user_id: uuid.UUID | None = None) -> list[dict]:
    _require_owner(user_id)
    return vault.project_status()


class VaultWriteRequest(BaseModel):
    user_id: uuid.UUID
    project: str
    text: str


def _write(operation, request: VaultWriteRequest) -> dict:
    _require_owner(request.user_id)
    try:
        return operation(request.project, request.text)
    except vault_write.VaultWriteError as e:
        raise HTTPException(status_code=400, detail=str(e))


@router.post("/pendiente", dependencies=[Depends(_require_enabled)])
def add_pendiente(request: VaultWriteRequest) -> dict:
    return _write(vault_write.add_pendiente, request)


@router.post("/comentario", dependencies=[Depends(_require_enabled)])
def add_comentario(request: VaultWriteRequest) -> dict:
    return _write(vault_write.add_comentario, request)
