"""Investigación con búsqueda web para el chat de Artemis (legalidad, rentabilidad de un nicho,
competencia…). Es de solo lectura: no escribe en el vault. Si el resultado vale la pena,
`chat.py` deja una propuesta de comentario para la bitácora, que Kevin confirma como cualquier
otra acción (ver `actions.py`).

Se hace en una llamada aparte a Gemini con Google Search, no mezclada con las herramientas de
escritura, para que cada llamada tenga una sola responsabilidad. Gasta cuota (la búsqueda con
grounding se cobra aparte), así que hay un tope diario.
"""
import logging
import re
import threading
from dataclasses import dataclass, field
from datetime import date

from app import vault

logger = logging.getLogger("brain.research")

MAX_RESEARCH_PER_DAY = 10
MAX_CONTEXT_CHARS = 3000
MAX_SUMMARY_CHARS = 500
MAX_SOURCES = 6

# Kevin vive en Chile: sin otra indicación, la legalidad se evalúa allá.
_DEFAULT_JURISDICTION = "Chile"

_INSTRUCTION = """Sos un analista que ayuda a Kevin a evaluar ideas y funcionalidades de sus proyectos de software. Respondé en español, de forma concreta y ordenada, usando búsqueda web.

Reglas:
- Si la pregunta es legal: indicá qué leyes, licencias o políticas de plataforma podrían aplicar (por defecto en {jurisdiction}, salvo que la pregunta indique otro país), qué riesgos concretos hay y qué haría falta para cumplir. Aclará siempre, en una línea, que es orientación general y no asesoría legal.
- Si la pregunta es de rentabilidad o nicho: indicá tamaño y señales del mercado, competidores o referentes, modelos de monetización habituales y riesgos. Distinguí datos con fuente de estimaciones tuyas, y no inventes cifras: si no encontraste el dato, decilo.
- Sé breve: lo importante primero, sin relleno.
- Terminá SIEMPRE con una última línea que empiece con «RESUMEN:» y resuma la conclusión en una o dos frases (máximo {summary_chars} caracteres), sin enlaces."""

_lock = threading.Lock()
_usage: dict[str, int] = {}


class ResearchLimitError(Exception):
    """Se alcanzó el tope diario de investigaciones."""


@dataclass
class ResearchResult:
    answer: str  # respuesta sin la línea RESUMEN
    summary: str
    sources: list[tuple[str, str]] = field(default_factory=list)  # (título, url)


def _take_daily_slot() -> None:
    today = date.today().isoformat()
    with _lock:
        for key in [k for k in _usage if k != today]:
            del _usage[key]
        if _usage.get(today, 0) >= MAX_RESEARCH_PER_DAY:
            raise ResearchLimitError(
                f"Ya hice {MAX_RESEARCH_PER_DAY} investigaciones hoy (tope para cuidar la cuota). Mañana seguimos."
            )
        _usage[today] = _usage.get(today, 0) + 1


def project_context(slug: str | None) -> str:
    """Ficha del proyecto (sin el frontmatter crudo) para que la investigación sea sobre ese proyecto."""
    if not slug:
        return ""
    card = vault.vault_root() / "01-proyectos" / slug / f"{slug}.md"
    if not card.is_file():
        return ""
    meta, body = vault.parse_frontmatter(card.read_text(encoding="utf-8"))
    stack = ", ".join(meta.get("stack") or []) or "sin definir"
    head = f"Proyecto: {meta.get('nombre', slug)} ({meta.get('estado')}, {meta.get('ambito')}). Resumen: {meta.get('resumen')}. Stack: {stack}."
    return f"{head}\n\n{body.strip()}"[:MAX_CONTEXT_CHARS]


def build_prompt(question: str, context: str) -> str:
    instruction = _INSTRUCTION.format(jurisdiction=_DEFAULT_JURISDICTION, summary_chars=MAX_SUMMARY_CHARS)
    context_block = f"\n\n# Contexto del proyecto (del cerebro de Kevin)\n{context}" if context else ""
    return f"{instruction}{context_block}\n\n# Pregunta\n{question}"


_SUMMARY_RE = re.compile(r"(?im)^\W*resumen\W*:\s*(.+?)\s*$")


def split_summary(text: str) -> tuple[str, str]:
    """Separa la línea «RESUMEN:» del resto. Sin esa línea, el resumen son las primeras palabras."""
    match = None
    for match in _SUMMARY_RE.finditer(text):
        pass
    if match:
        answer = (text[:match.start()] + text[match.end():]).strip()
        summary = match.group(1)
    else:
        answer = text.strip()
        summary = " ".join(answer.split())
    summary = " ".join(summary.split())
    if len(summary) > MAX_SUMMARY_CHARS:
        summary = summary[:MAX_SUMMARY_CHARS - 1].rstrip() + "…"
    return answer, summary


def extract_sources(interaction) -> list[tuple[str, str]]:
    """URLs citadas por el modelo (anotaciones `url_citation`), sin repetir, en orden."""
    seen: dict[str, str] = {}
    for step in getattr(interaction, "steps", None) or []:
        for content in getattr(step, "content", None) or []:
            for annotation in getattr(content, "annotations", None) or []:
                url = getattr(annotation, "url", None)
                if getattr(annotation, "type", None) == "url_citation" and url and url not in seen:
                    seen[url] = getattr(annotation, "title", None) or url
    return [(title, url) for url, title in seen.items()][:MAX_SOURCES]


def investigate(client, model: str, question: str, project: str | None) -> ResearchResult:
    """Una investigación con Google Search. `client` es el cliente de google-genai (inyectable en pruebas)."""
    question = " ".join((question or "").split())
    if not question:
        raise ValueError("La pregunta está vacía")
    _take_daily_slot()
    prompt = build_prompt(question, project_context(project))
    interaction = client.interactions.create(
        model=model,
        input=prompt,
        generation_config={"thinking_level": "low"},
        tools=[{"type": "google_search"}],
    )
    answer, summary = split_summary(interaction.output_text or "")
    if not answer:
        raise ValueError("Gemini no devolvió una respuesta")
    return ResearchResult(answer, summary, extract_sources(interaction))


def format_reply(result: ResearchResult) -> str:
    lines = [result.answer]
    if result.sources:
        lines.append("\n**Fuentes**")
        lines.extend(f"- [{title}]({url})" for title, url in result.sources)
    return "\n".join(lines)


def comment_text(question: str, result: ResearchResult) -> str:
    """Texto para la bitácora (tope de `vault_write`: 1000 caracteres)."""
    question = " ".join(question.split())
    text = f"Investigación — {question[:200]}\n\n{result.summary}"
    if result.sources:
        with_source = f"{text}\n\nFuente: {result.sources[0][1]}"
        if len(with_source) <= 1000:  # una URL cortada a medias no sirve
            text = with_source
    return text[:1000]
