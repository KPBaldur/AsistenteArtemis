import os

from dotenv import load_dotenv

load_dotenv()


def _normalize_database_url(url: str) -> str:
    # Supabase (y la mayoría de los proveedores) dan la connection string como
    # "postgresql://...". SQLAlchemy necesita el dialecto explícito para usar
    # el driver psycopg (v3), igual que en uma-backend/app/config.py.
    if url.startswith("postgresql://"):
        return url.replace("postgresql://", "postgresql+psycopg://", 1)
    return url


DATABASE_URL = _normalize_database_url(os.environ["DATABASE_URL"])
GEMINI_API_KEY = os.environ["GEMINI_API_KEY"]
# Secreto compartido con TodoAppRoll.Api (mismo valor en Brain:InternalKey del
# lado .NET) — protege /internal/* de llamadas que no vengan de la Api.
INTERNAL_API_KEY = os.environ["INTERNAL_API_KEY"]

# Cerebro de proyectos (repo BrainObsidian, vault de Obsidian en git). Opcional:
# sin VAULT_PATH el Brain funciona igual que antes, sin contexto del vault.
VAULT_PATH = os.environ.get("VAULT_PATH") or None
# El vault es de un solo usuario (Kevin) -- solo sus chats reciben ese contexto,
# aunque la app permita registrar otros usuarios.
VAULT_OWNER_USER_ID = os.environ.get("VAULT_OWNER_USER_ID") or None
VAULT_GIT_PULL = os.environ.get("VAULT_GIT_PULL", "true").lower() not in ("0", "false", "no")
VAULT_ENABLED = bool(VAULT_PATH and VAULT_OWNER_USER_ID)
