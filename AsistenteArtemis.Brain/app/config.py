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
