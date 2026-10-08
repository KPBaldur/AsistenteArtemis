from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func
from sqlalchemy.orm import Session

from app import vault
from app.chat import require_internal_key
from app.config import VAULT_ENABLED, VAULT_PATH
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


@router.get("/projects", dependencies=[Depends(_require_enabled)])
def projects() -> list[dict]:
    return vault.project_status()
