from google import genai
from google.genai import types

from app.config import GEMINI_API_KEY
from app.models import EMBEDDING_DIM

# Modelo de embeddings unificado de Gemini. Si Google lo deprecia para cuentas
# nuevas (como pasó con gemini-2.5-flash en uma-backend, ver decisions.md), el
# error 404 de la API lo va a decir explícitamente -- ajustar acá nomás.
EMBEDDING_MODEL = "gemini-embedding-001"

_client = genai.Client(api_key=GEMINI_API_KEY)

_MAX_CHUNK_CHARS = 1500


def chunk_note(content: str) -> list[str]:
    """Una nota corta (el caso típico) es un solo chunk. Si es larga, se separa
    por párrafos en trozos de hasta ~1500 caracteres -- sin librería de
    text-splitting, no hace falta a la escala de una nota personal."""
    content = content.strip()
    if not content:
        return []
    if len(content) <= _MAX_CHUNK_CHARS:
        return [content]

    chunks: list[str] = []
    current = ""
    for paragraph in content.split("\n\n"):
        if len(current) + len(paragraph) + 2 <= _MAX_CHUNK_CHARS:
            current = f"{current}\n\n{paragraph}" if current else paragraph
        else:
            if current:
                chunks.append(current)
            current = paragraph
    if current:
        chunks.append(current)
    return chunks


def embed_texts(texts: list[str], task_type: str) -> list[list[float]]:
    """task_type: "RETRIEVAL_DOCUMENT" al embeber notas, "RETRIEVAL_QUERY" al
    embeber el mensaje entrante del chat -- Gemini optimiza el vector distinto
    según el rol, mejora la calidad de la búsqueda por similitud."""
    if not texts:
        return []
    response = _client.models.embed_content(
        model=EMBEDDING_MODEL,
        contents=texts,
        config=types.EmbedContentConfig(task_type=task_type, output_dimensionality=EMBEDDING_DIM),
    )
    return [embedding.values for embedding in response.embeddings]
