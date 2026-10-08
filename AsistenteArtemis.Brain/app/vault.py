"""Lectura del cerebro de proyectos (BrainObsidian) -- sin DB, solo archivos.

Estructura y frontmatter del vault documentados en su propio
`99-sistema/esquema.md`; qué puede leer/escribir el Brain, en
`99-sistema/contrato-artemis.md`. Esta iteración es solo lectura: nunca se
escribe en el vault ni se hace push.
"""

import hashlib
import logging
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path

import yaml

from app.config import VAULT_GIT_PULL, VAULT_PATH

logger = logging.getLogger("brain.vault")

_MAX_CHUNK_CHARS = 1500  # mismo tope que app/embeddings.py
_MAX_HEADING_CHARS = 150

# Plantillas y config no son conocimiento; 04-fuentes es material crudo cuyo
# resumen ya vive en 02-conocimiento/fuentes.
_EXCLUDED_DIRS = {".git", ".obsidian", ".claude", ".trash", "80-plantillas", "04-fuentes"}

_FRONTMATTER_RE = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.S)


@dataclass
class VaultChunk:
    path: str
    doc_type: str | None
    project: str | None
    heading: str | None
    chunk_index: int
    text: str


def vault_root() -> Path:
    return Path(VAULT_PATH)


def _git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", "-C", str(vault_root()), *args],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )


def pull() -> None:
    """`git pull --ff-only`. Si falla (sin red, conflicto local), se sigue con
    la copia que haya en disco -- el vault desactualizado es mejor que nada."""
    if not VAULT_GIT_PULL:
        return
    try:
        result = _git("pull", "--ff-only")
        if result.returncode != 0:
            logger.warning("git pull del vault falló: %s", result.stderr.strip())
    except Exception:
        logger.exception("Error corriendo git pull en el vault")


def head_commit() -> str | None:
    try:
        result = _git("rev-parse", "--short", "HEAD")
        if result.returncode != 0:
            return None
        return result.stdout.strip() or None
    except Exception:
        return None


def iter_documents():
    """(ruta relativa con '/', contenido) de cada nota indexable."""
    root = vault_root()
    for path in sorted(root.rglob("*.md")):
        rel = path.relative_to(root)
        if any(part in _EXCLUDED_DIRS for part in rel.parts):
            continue
        yield rel.as_posix(), path.read_text(encoding="utf-8")


def content_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def parse_frontmatter(text: str) -> tuple[dict, str]:
    match = _FRONTMATTER_RE.match(text)
    if not match:
        return {}, text
    try:
        meta = yaml.safe_load(match.group(1)) or {}
    except yaml.YAMLError:
        logger.warning("Frontmatter inválido, se indexa sin metadatos")
        meta = {}
    return (meta if isinstance(meta, dict) else {}), text[match.end():]


def _project_of(rel_path: str, meta: dict) -> str | None:
    parts = rel_path.split("/")
    if parts[0] == "01-proyectos" and len(parts) > 2:
        return parts[1]
    return meta.get("proyecto") or None


def chunk_markdown(rel_path: str, text: str) -> list[VaultChunk]:
    """Un chunk por sección `##` (las largas se subdividen con `chunk_note`).
    Cada chunk lleva una cabecera con ruta/proyecto/tipo: las bitácoras de
    distintos proyectos se parecen mucho entre sí, y sin ese contexto ni el
    embedding ni el LLM sabrían de qué proyecto es un fragmento."""
    meta, body = parse_frontmatter(text)
    doc_type = meta.get("tipo") or meta.get("type")
    project = _project_of(rel_path, meta)

    sections: list[tuple[str | None, str]] = []
    heading, buf = None, []
    for line in body.splitlines():
        if line.startswith("## "):
            sections.append((heading, "\n".join(buf)))
            heading, buf = line[3:].strip(), [line]
        else:
            buf.append(line)
    sections.append((heading, "\n".join(buf)))

    header = f"[{rel_path} · {project or 'general'} · {doc_type or 'nota'}]"
    if doc_type == "proyecto" and meta.get("resumen"):
        header += f" {meta.get('nombre', project)}: {meta['resumen']}"

    chunks: list[VaultChunk] = []
    for section_heading, section_text in sections:
        # Hay bitácoras migradas con la entrada entera dentro del `##`: se
        # recorta para usarla como etiqueta (en el texto original queda completa).
        short_heading = _truncate(section_heading, _MAX_HEADING_CHARS) if section_heading else None
        for piece in _split_section(section_text):
            # Solo el título `# ...` de la nota no aporta nada que la cabecera no diga.
            if not any(line.strip() and not line.startswith("#") for line in piece.splitlines()):
                continue
            # Cada pedazo de una sección larga repite su encabezado, para que
            # no quede un fragmento de pendientes sin saber que son "Abiertos".
            if short_heading and not piece.startswith("## "):
                piece = f"## {short_heading} (cont.)\n{piece}"
            chunks.append(VaultChunk(rel_path, doc_type, project, short_heading, len(chunks), f"{header}\n{piece}"))
    return chunks


def _truncate(text: str, limit: int) -> str:
    return text if len(text) <= limit else text[:limit].rsplit(" ", 1)[0] + "…"


def _split_section(text: str) -> list[str]:
    """Como `chunk_note`, pero garantizando el tope de tamaño: en el vault hay
    párrafos de una sola línea de varios miles de caracteres (entradas de
    bitácora/diario) y listas largas sin líneas en blanco, que `chunk_note`
    dejaría enteras y pasarían el límite de tokens del modelo de embeddings."""
    text = text.strip()
    if not text:
        return []
    units: list[str] = []
    for paragraph in text.split("\n\n"):
        if len(paragraph) <= _MAX_CHUNK_CHARS:
            units.append(paragraph)
            continue
        for line in paragraph.split("\n"):
            while len(line) > _MAX_CHUNK_CHARS:
                cut = line.rfind(" ", 0, _MAX_CHUNK_CHARS)
                cut = cut if cut > 0 else _MAX_CHUNK_CHARS
                units.append(line[:cut])
                line = line[cut:].lstrip()
            if line:
                units.append(line)

    pieces: list[str] = []
    current = ""
    for unit in units:
        if current and len(current) + len(unit) + 2 > _MAX_CHUNK_CHARS:
            pieces.append(current)
            current = unit
        else:
            current = f"{current}\n\n{unit}" if current else unit
    if current:
        pieces.append(current)
    return pieces


def project_status() -> list[dict]:
    """Frontmatter de cada ficha `01-proyectos/<slug>/<slug>.md`."""
    projects = []
    for folder in sorted((vault_root() / "01-proyectos").iterdir()):
        card = folder / f"{folder.name}.md"
        if not card.is_file():
            continue
        meta, _ = parse_frontmatter(card.read_text(encoding="utf-8"))
        if meta.get("tipo") != "proyecto":
            continue
        projects.append({
            key: (str(meta[key]) if key in ("inicio", "actualizado") and meta.get(key) else meta.get(key))
            for key in ("id", "nombre", "estado", "ambito", "fase", "inicio", "actualizado", "resumen", "stack", "repo")
        })
    return projects


def project_status_text() -> str:
    lines = []
    for p in project_status():
        fase = f", {p['fase']}" if p.get("fase") else ""
        lines.append(
            f"- {p['nombre']} ({p['estado']}, {p['ambito']}{fase}; última actividad {p['actualizado']}): {p['resumen']}"
        )
    return "\n".join(lines)
