"""Acciones que Artemis puede hacer sobre el cerebro de proyectos desde el chat.

Flujo con confirmación (decisión del 2026-10-15):
1. Gemini recibe herramientas `proponer_*`. Si el mensaje pide anotar algo, llama a una.
2. El Brain NO escribe: valida el proyecto, guarda la propuesta de esa sesión y responde
   mostrándole a Kevin qué va a anotar y dónde, pidiendo confirmación.
3. En el mensaje siguiente, si Kevin confirma ("sí", "dale"…), recién ahí se escribe con
   `vault_write`. Si cancela, o escribe otra cosa, la propuesta se descarta.

La confirmación se resuelve en código y no con el modelo: así una alucinación de Gemini no
puede escribir en el vault sin que Kevin haya dicho que sí en un mensaje posterior.
Las propuestas viven en memoria (se pierden si el Brain se reinicia, a propósito).
"""
import logging
import re
import threading
import time
import unicodedata
import uuid
from dataclasses import dataclass, field

from app import research, vault, vault_write

logger = logging.getLogger("brain.actions")

PROPOSAL_TTL_SECONDS = 15 * 60

# Herramientas en el formato de la API Interactions de Gemini.
TOOLS = [
    {
        "type": "function",
        "name": "proponer_pendiente",
        "description": (
            "Propone agregar una tarea accionable a los pendientes de un proyecto del cerebro "
            "(por ejemplo: «agrega una nota/tarea al proyecto X para hacer Y»). No escribe nada: "
            "el sistema le pedirá confirmación al usuario."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "proyecto": {"type": "string", "description": "Slug (id) del proyecto, exactamente como figura en la lista de proyectos."},
                "texto": {"type": "string", "description": "La tarea, en una línea, en infinitivo y concreta."},
            },
            "required": ["proyecto", "texto"],
        },
    },
    {
        "type": "function",
        "name": "proponer_comentario",
        "description": (
            "Propone dejar un comentario libre (idea, contexto, observación que no es una tarea) "
            "en la bitácora de un proyecto. No escribe nada: el sistema pedirá confirmación."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "proyecto": {"type": "string", "description": "Slug (id) del proyecto."},
                "texto": {"type": "string", "description": "El comentario, tal como debe quedar en la bitácora."},
            },
            "required": ["proyecto", "texto"],
        },
    },
    {
        "type": "function",
        "name": "proponer_captura",
        "description": (
            "Propone dejar una nota suelta en el inbox del cerebro (00-inbox) cuando lo que el usuario quiere "
            "anotar NO encaja en un proyecto concreto ni es una idea de proyecto nuevo: un recordatorio, un "
            "pensamiento, un dato. Kevin la rutea después. No escribe nada: el sistema pedirá confirmación."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "texto": {"type": "string", "description": "La nota, tal como la dijo el usuario, sin inventar nada."},
                "proyecto": {"type": "string", "description": "Slug del proyecto relacionado, solo si es claro; vacío si no."},
            },
            "required": ["texto"],
        },
    },
    {
        "type": "function",
        "name": "investigar",
        "description": (
            "Investiga en la web una duda sobre un proyecto o una idea: legalidad (leyes, licencias, políticas "
            "de tiendas de apps), rentabilidad o tamaño de un nicho, competencia, viabilidad. Úsala cuando el "
            "usuario pregunte «¿esto toparía con algo legal?», «¿qué tan rentable sería?» y similares. "
            "No escribe nada en el cerebro."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "pregunta": {"type": "string", "description": "La pregunta completa y autocontenida, con la funcionalidad o el nicho concretos."},
                "proyecto": {"type": "string", "description": "Slug del proyecto al que se refiere, si corresponde a uno de la lista; vacío si no."},
            },
            "required": ["pregunta"],
        },
    },
    {
        "type": "function",
        "name": "proponer_proyecto_idea",
        "description": (
            "Propone registrar una IDEA de proyecto nuevo (por ejemplo: «se me ocurrió un videojuego de X, "
            "anótalo como proyecto futuro»). Crea un proyecto en estado «idea». No escribe nada: el sistema "
            "pedirá confirmación. Úsala solo si la idea NO corresponde a un proyecto que ya existe en la lista."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "nombre": {"type": "string", "description": "Nombre corto del proyecto (el que diga el usuario o uno descriptivo)."},
                "resumen": {"type": "string", "description": "1–2 frases: qué es la idea, con lo que dijo el usuario, sin inventar detalles."},
                "ambito": {"type": "string", "enum": ["personal", "trabajo"], "description": "«personal» salvo que el usuario diga que es de trabajo."},
                "stack": {"type": "array", "items": {"type": "string"}, "description": "Tecnologías mencionadas por el usuario, en minúscula kebab-case (ej. «unity»). Vacío si no mencionó ninguna."},
            },
            "required": ["nombre", "resumen"],
        },
    },
]

TOOLS_INSTRUCTION = """# Acciones sobre el cerebro de proyectos
Si el usuario te pide anotar algo en un proyecto, usá la herramienta `proponer_pendiente` (si es una tarea por hacer) o `proponer_comentario` (si es contexto u observación de un proyecto que ya existe). El slug del proyecto debe ser uno de los de la lista de abajo; si no queda claro a cuál se refiere, preguntale en vez de adivinar. Si pregunta por legalidad, rentabilidad, competencia o viabilidad de algo, usá `investigar` (no respondas de memoria). Si quiere anotar algo suelto que no es de ningún proyecto, usá `proponer_captura`. Si cuenta una idea de proyecto nuevo, usá `proponer_proyecto_idea`. No digas que ya anotaste nada: el sistema le mostrará al usuario lo que se va a anotar y le pedirá confirmación."""

_KINDS = {
    "proponer_pendiente": "pendiente",
    "proponer_comentario": "comentario",
    "proponer_proyecto_idea": "proyecto",
    "proponer_captura": "captura",
}


@dataclass
class Proposal:
    kind: str  # "pendiente" | "comentario" | "proyecto" | "captura"
    project: str  # slug (en "proyecto", el que se va a crear)
    text: str  # en "proyecto", el resumen
    nombre: str = ""
    ambito: str = "personal"
    stack: list[str] = field(default_factory=list)

    def execute(self) -> None:
        if self.kind == "pendiente":
            vault_write.add_pendiente(self.project, self.text)
        elif self.kind == "comentario":
            vault_write.add_comentario(self.project, self.text)
        elif self.kind == "captura":
            vault_write.create_captura(self.text, self.project or None)
        else:
            vault_write.create_project(self.nombre, self.text, self.ambito, self.stack)


@dataclass
class _Pending:
    proposals: list[Proposal]
    created: float = field(default_factory=time.monotonic)


_pending: dict[uuid.UUID, _Pending] = {}
_lock = threading.Lock()


def tools_instruction_with_projects() -> str:
    ids = ", ".join(p["id"] for p in vault.project_status())
    return f"{TOOLS_INSTRUCTION}\nProyectos válidos (slug): {ids}"


def _function_calls(interaction) -> list[tuple[str, dict]]:
    """Llamadas a herramientas que pidió el modelo, sin depender de la forma exacta del objeto."""
    calls = []
    for step in getattr(interaction, "steps", None) or []:
        if getattr(step, "type", None) == "function_call":
            calls.append((step.name, dict(step.arguments or {})))
    return calls


def research_requests(interaction) -> list[dict]:
    """Pedidos de `investigar` del modelo: [{"pregunta", "proyecto"}]. El proyecto se descarta si no existe."""
    valid = {p["id"] for p in vault.project_status()}
    requests = []
    for name, args in _function_calls(interaction):
        if name != "investigar":
            continue
        question = " ".join(str(args.get("pregunta") or "").split())
        slug = str(args.get("proyecto") or "").strip()
        if question:
            requests.append({"pregunta": question, "proyecto": slug if slug in valid else None})
    return requests


def store_proposals(session_id: uuid.UUID, proposals: list[Proposal]) -> None:
    with _lock:
        _pending[session_id] = _Pending(proposals)


def research_reply(session_id: uuid.UUID, requests: list[dict], client, model: str) -> str:
    """Hace la primera investigación pedida y arma la respuesta. Si fue sobre un proyecto, deja una
    propuesta (a confirmar) para guardar el resumen en su bitácora."""
    request = requests[0]
    try:
        result = research.investigate(client, model, request["pregunta"], request["proyecto"])
    except research.ResearchLimitError as e:
        return str(e)
    except Exception:
        logger.exception("Falló la investigación")
        return "No pude completar la investigación (falló la búsqueda o la API de Gemini). Intenta de nuevo en un rato."

    reply = research.format_reply(result)
    slug = request["proyecto"]
    if slug and result.summary:
        nombre = next((p["nombre"] for p in vault.project_status() if p["id"] == slug), slug)
        store_proposals(session_id, [Proposal("comentario", slug, research.comment_text(request["pregunta"], result))])
        reply += f"\n\n¿Guardo un resumen en la bitácora de **{nombre}**? (sí / no)"
    return reply


def propose_from_interaction(session_id: uuid.UUID, interaction) -> str | None:
    """Si el modelo pidió proponer algo, guarda las propuestas válidas y devuelve el texto
    de confirmación (o el de error). Devuelve None si no pidió ninguna herramienta."""
    calls = [(n, a) for n, a in _function_calls(interaction) if n in _KINDS]
    if not calls:
        return None

    valid_ids = {p["id"]: p["nombre"] for p in vault.project_status()}
    proposals: list[Proposal] = []
    problems: list[str] = []
    for name, args in calls:
        kind = _KINDS[name]
        text = " ".join(str(args.get("texto" if kind != "proyecto" else "resumen") or "").split())
        if kind == "proyecto":
            nombre = " ".join(str(args.get("nombre") or "").split())
            slug = vault_write.slugify(nombre)
            stack = [str(t) for t in (args.get("stack") or []) if isinstance(t, str)]
            ambito = args.get("ambito") if args.get("ambito") in ("personal", "trabajo") else "personal"
            if not slug or not text:
                problems.append("Me falta el nombre o la descripción de la idea.")
            elif slug in valid_ids or any(p.kind == "proyecto" and p.project == slug for p in proposals):
                problems.append(f"Ya existe un proyecto «{slug}» en el cerebro.")
            else:
                proposals.append(Proposal(kind, slug, text, nombre, ambito, stack))
            continue
        slug = str(args.get("proyecto") or "").strip()
        if kind == "captura":
            if not text:
                problems.append("Me quedó vacía la nota.")
            else:
                proposals.append(Proposal(kind, slug if slug in valid_ids else "", text))
        elif slug not in valid_ids:
            problems.append(f"No encuentro el proyecto «{slug}» en el cerebro.")
        elif not text:
            problems.append("Me quedó vacío el texto a anotar.")
        else:
            proposals.append(Proposal(kind, slug, text))

    lines = []
    if proposals:
        with _lock:
            _pending[session_id] = _Pending(proposals)
        lines.append("Esto es lo que voy a anotar en el cerebro:")
        for p in proposals:
            if p.kind == "captura":
                rel = f" (relacionada con **{valid_ids[p.project]}**)" if p.project else ""
                lines.append(f"- **Nota en el inbox**{rel}: {p.text}")
            elif p.kind == "proyecto":
                tech = f" · stack: {', '.join(p.stack)}" if p.stack else ""
                lines.append(f"- **Proyecto nuevo «{p.nombre}»** (idea, {p.ambito}{tech}): {p.text}")
            else:
                donde = "pendientes" if p.kind == "pendiente" else "bitácora"
                lines.append(f"- **{valid_ids[p.project]}** ({donde}): {p.text}")
        lines.append("¿Confirmas? (sí / no)")
    else:
        with _lock:
            _pending.pop(session_id, None)
    if problems:
        lines.extend(problems)
        lines.append("Dime a qué proyecto va y lo preparo de nuevo.")
    return "\n".join(lines)


def _normalize(message: str) -> str:
    text = unicodedata.normalize("NFD", message.lower())
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return " ".join(re.sub(r"[^a-z0-9 ]", " ", text).split())


# Estrictas a propósito: «sí, pero cámbialo» NO es una confirmación.
_COURTESY = r"( por favor| gracias| nomas| no mas)*"
_YES = re.compile(
    r"^(si|sip|sii+|dale|ok|okey|okay|vale|confirmo|confirmado|confirma|hazlo|anotalo|anotala|adelante|de una|claro|listo|perfecto|correcto|exacto)"
    + _COURTESY + "$"
)
_NO = re.compile(r"^(no|nop|cancela|cancelar|cancelalo|olvidalo|mejor no|nada|dejalo)" + _COURTESY + "$")


def resolve_pending(session_id: uuid.UUID, message: str) -> str | None:
    """Si hay una propuesta vigente en la sesión: confirma → escribe y responde; cancela →
    descarta y responde. Cualquier otro mensaje descarta la propuesta y devuelve None, para
    que el mensaje se procese normalmente (el modelo puede proponer algo nuevo)."""
    with _lock:
        pending = _pending.pop(session_id, None)
    if pending is None or time.monotonic() - pending.created > PROPOSAL_TTL_SECONDS:
        return None

    text = _normalize(message)
    if _NO.match(text):
        return "Listo, no anoté nada."
    if not _YES.match(text):
        return None

    done, failed = [], []
    for p in pending.proposals:
        try:
            p.execute()
            done.append(p)
        except vault_write.VaultWriteError as e:
            failed.append(f"- No pude anotar en {p.project}: {e}")
        except Exception:
            logger.exception("Falló la escritura confirmada en %s", p.project)
            failed.append(f"- No pude anotar en {p.project} (error inesperado, revisa el log del Brain).")

    lines = []
    if done:
        lines.append("Anotado ✅ " + "; ".join((f"proyecto idea {p.project}" if p.kind == "proyecto" else "nota en el inbox" if p.kind == "captura" else f"{p.kind} en {p.project}") for p in done) + ". Quedó en un commit local, sin push.")
    lines.extend(failed)
    return "\n".join(lines)
