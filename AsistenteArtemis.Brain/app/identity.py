import uuid

from sqlalchemy.orm import Session

from app.models import AssistantIdentity

DEFAULT_NAME = "Artemis"
DEFAULT_TONE = "cercano, directo y con calma"

# Regla fija sin importar la personalidad configurada -- moldear el tono no
# debe habilitar que el asistente invente información.
_GROUNDING_RULE = (
    "Respondé en español, de forma directa y concreta, basándote solo en el "
    "contexto real provisto abajo -- si el contexto no alcanza para responder, "
    "decilo en vez de inventar."
)


def build_system_instruction(db: Session, user_id: uuid.UUID) -> str:
    identity = db.query(AssistantIdentity).filter(AssistantIdentity.UserId == user_id).first()

    name = identity.Name if identity else DEFAULT_NAME
    tone = identity.Tone if identity else DEFAULT_TONE
    style_rules = identity.StyleRules if identity else ""

    lines = [f"Sos {name}, el asistente personal de IA del usuario. Tono: {tone}."]
    if style_rules:
        lines.append(f"Reglas de estilo: {style_rules}")
    lines.append(_GROUNDING_RULE)

    return "\n".join(lines)
