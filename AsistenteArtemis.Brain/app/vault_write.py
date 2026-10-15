"""Escritura acotada en el cerebro de proyectos (iteración 2 del contrato con Artemis,
99-sistema/contrato-artemis.md en BrainObsidian).

Operaciones:
- agregar un pendiente a `## Abiertos` de 01-proyectos/<slug>/pendientes.md;
- agregar una entrada "(artemis)" al final de 01-proyectos/<slug>/bitacora.md;
- crear un proyecto nuevo en estado `idea` (ficha, pendientes y bitácora);
- dejar una captura en `00-inbox/`.

Cada operación deja además una línea en la nota del día (`03-diario/`), en el mismo commit.

Ambas actualizan `actualizado` de la ficha del proyecto y hacen un commit local con autor
Artemis y prefijo `artemis:`. Nunca hacen push (desde este equipo el push lo corre Kevin) y
nunca reescriben ni borran líneas existentes.
"""
import json
import logging
import re
import shutil
import unicodedata
import threading
from datetime import date, datetime
from pathlib import Path

from app import vault

logger = logging.getLogger("brain.vault_write")

_SLUG_RE = re.compile(r"^[a-z0-9][a-z0-9_-]*$")
_MAX_TEXT = 1000
_GIT_AUTHOR = ["-c", "user.name=Artemis", "-c", "user.email=artemis@asistenteartemis.local"]

# Un solo escritor a la vez: dos escrituras concurrentes podrían pisarse el archivo o el commit.
_lock = threading.Lock()


class VaultWriteError(Exception):
    """Error de validación o de escritura, con un mensaje apto para mostrarle a Kevin."""


def _project_dir(slug: str) -> Path:
    if not _SLUG_RE.match(slug or ""):
        raise VaultWriteError(f"Proyecto inválido: {slug!r}")
    folder = vault.vault_root() / "01-proyectos" / slug
    card = folder / f"{slug}.md"
    if not card.is_file():
        raise VaultWriteError(f"No existe el proyecto «{slug}» en el cerebro")
    meta, _ = vault.parse_frontmatter(card.read_text(encoding="utf-8"))
    if meta.get("tipo") != "proyecto":
        raise VaultWriteError(f"«{slug}» no es una ficha de proyecto")
    return folder


def _clean(text: str, single_line: bool) -> str:
    text = (text or "").strip()
    if single_line:
        text = " ".join(text.split())
    if not text:
        raise VaultWriteError("El texto está vacío")
    if len(text) > _MAX_TEXT:
        raise VaultWriteError(f"El texto supera los {_MAX_TEXT} caracteres")
    return text


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _write(path: Path, text: str) -> None:
    # newline="\n": el vault usa LF (ver .gitattributes de BrainObsidian).
    path.write_text(text, encoding="utf-8", newline="\n")


def _touch_actualizado(text: str, today: str) -> str:
    """Actualiza `actualizado:` solo dentro del frontmatter."""
    match = re.match(r"\A---\s*\n(.*?)\n---\s*\n", text, re.S)
    if not match:
        return text
    front = re.sub(r"(?m)^actualizado:.*$", f"actualizado: {today}", match.group(1), count=1)
    return text[:match.start(1)] + front + text[match.end(1):]


def _commit(paths: list[Path], message: str) -> str | None:
    rel = [str(p.relative_to(vault.vault_root())).replace("\\", "/") for p in paths]
    add = vault._git("add", "--", *rel)
    if add.returncode != 0:
        raise VaultWriteError(f"git add falló: {add.stderr.strip()}")
    # "-- <rutas>": se commitean solo estos archivos, aunque Kevin tenga otros cambios preparados.
    commit = vault._git(*_GIT_AUTHOR, "commit", "-m", message, "--", *rel)
    if commit.returncode != 0:
        raise VaultWriteError(f"git commit falló: {(commit.stderr or commit.stdout).strip()}")
    return vault.head_commit()


def _bitacora_template(slug: str, nombre: str, today: str) -> str:
    return (
        f"---\ntipo: bitacora\nproyecto: {slug}\nactualizado: {today}\ntags: [\"bitacora\"]\n---\n\n"
        f"# Bitácora — {nombre}\n"
    )


def add_pendiente(slug: str, text: str) -> dict:
    """Agrega `- [ ] <texto> · desde AAAA-MM-DD (artemis)` al final de `## Abiertos`."""
    text = _clean(text, single_line=True)
    today = date.today().isoformat()
    with _lock:
        folder = _project_dir(slug)
        path = folder / "pendientes.md"
        if not path.is_file():
            raise VaultWriteError(f"El proyecto «{slug}» no tiene pendientes.md")
        lines = _read(path).split("\n")
        try:
            start = next(i for i, line in enumerate(lines) if line.strip() == "## Abiertos")
        except StopIteration:
            raise VaultWriteError(f"pendientes.md de «{slug}» no tiene la sección ## Abiertos")
        end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("## ")), len(lines))
        # Después del último ítem de la sección (o del encabezado si está vacía).
        insert_at = start + 1
        for i in range(start + 1, end):
            if lines[i].lstrip().startswith("- ["):
                insert_at = i + 1
        new_line = f"- [ ] {text} · desde {today} (artemis)"
        if insert_at == start + 1:
            lines[insert_at:insert_at] = ["", new_line]
        else:
            lines.insert(insert_at, new_line)
        _write(path, _touch_actualizado("\n".join(lines), today))

        card = folder / f"{slug}.md"
        _write(card, _touch_actualizado(_read(card), today))
        diario = _log_diario(slug, f"pendiente agregado: {text}")
        commit = _commit([path, card, *diario], f"artemis: pendiente en {slug}")
    logger.info("Pendiente agregado en %s (%s)", slug, commit)
    return {"project": slug, "file": f"01-proyectos/{slug}/pendientes.md", "line": new_line, "commit": commit}


def add_comentario(slug: str, text: str) -> dict:
    """Agrega `## [AAAA-MM-DD] Comentario (artemis)` + el texto al final de la bitácora."""
    text = _clean(text, single_line=False)
    today = date.today().isoformat()
    with _lock:
        folder = _project_dir(slug)
        card = folder / f"{slug}.md"
        path = folder / "bitacora.md"
        if path.is_file():
            current = _read(path).rstrip("\n")
        else:
            meta, _ = vault.parse_frontmatter(_read(card))
            current = _bitacora_template(slug, meta.get("nombre") or slug, today).rstrip("\n")
        entry = f"## [{today}] Comentario (artemis)\n\n{text}"
        _write(path, _touch_actualizado(f"{current}\n\n{entry}\n", today))
        _write(card, _touch_actualizado(_read(card), today))
        summary = " ".join(text.split())
        diario = _log_diario(slug, f"comentario en la bitácora: {summary[:200]}{'…' if len(summary) > 200 else ''}")
        commit = _commit([path, card, *diario], f"artemis: comentario en {slug}")
    logger.info("Comentario agregado en %s (%s)", slug, commit)
    return {"project": slug, "file": f"01-proyectos/{slug}/bitacora.md", "commit": commit}


def slugify(name: str) -> str:
    """kebab-case sin tildes, como exige el vault para el slug (= `id`) del proyecto."""
    text = unicodedata.normalize("NFD", name.lower())
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]+", "-", text).strip("-")[:60].strip("-")


_STACK_RE = re.compile(r"^[a-z0-9][a-z0-9.+-]*$")
_AMBITOS = ("personal", "trabajo")

_CARD_TEMPLATE = """---
tipo: proyecto
id: {slug}
nombre: {nombre_yaml}
estado: idea
ambito: {ambito}
fase:
inicio: {today}
actualizado: {today}
resumen: {resumen_yaml}
stack: {stack_yaml}
repo: []
repo_local: []
relacionados: []
tags: ["proyecto", "{ambito}"]
---

# {nombre}

> {resumen}

[[{base}/pendientes|Pendientes]] · [[{base}/decisiones|Decisiones]] · [[{base}/recursos|Recursos]] · [[{base}/bitacora|Bitácora]]

## Estado actual

Idea registrada el {today} por Artemis. Todavía sin trabajo iniciado.

## Objetivo

## Stack y recursos principales

## Lecciones aprendidas

## Próximos pasos

Viven en [[{base}/pendientes|pendientes]].
"""

_PENDIENTES_TEMPLATE = """---
tipo: pendientes
proyecto: {slug}
actualizado: {today}
tags: ["pendientes"]
---

# Pendientes — {nombre}

## Abiertos

## Resueltos
"""


def create_project(nombre: str, resumen: str, ambito: str = "personal", stack: list[str] | None = None) -> dict:
    """Crea `01-proyectos/<slug>/` con ficha (`estado: idea`), pendientes y bitácora.
    Nunca pisa un proyecto existente."""
    nombre = _clean(nombre, single_line=True)
    resumen = _clean(resumen, single_line=True)
    if ambito not in _AMBITOS:
        raise VaultWriteError(f"Ámbito inválido: {ambito!r} (personal o trabajo)")
    slug = slugify(nombre)
    if not _SLUG_RE.match(slug):
        raise VaultWriteError(f"No pude armar un slug válido a partir de «{nombre}»")
    stack = [t.strip().lower() for t in (stack or []) if t and t.strip()]
    bad = [t for t in stack if not _STACK_RE.match(t)]
    if bad:
        raise VaultWriteError(f"Tecnologías inválidas en el stack (minúscula kebab-case): {bad}")
    today = date.today().isoformat()
    base = f"01-proyectos/{slug}"
    with _lock:
        folder = vault.vault_root() / base
        if folder.exists():
            raise VaultWriteError(f"Ya existe un proyecto con el slug «{slug}»")
        folder.mkdir(parents=True)
        try:
            card = folder / f"{slug}.md"
            pendientes = folder / "pendientes.md"
            bitacora = folder / "bitacora.md"
            _write(card, _CARD_TEMPLATE.format(
                slug=slug, nombre=nombre, resumen=resumen, ambito=ambito, today=today, base=base,
                nombre_yaml=json.dumps(nombre, ensure_ascii=False),
                resumen_yaml=json.dumps(resumen, ensure_ascii=False),
                stack_yaml=json.dumps(stack, ensure_ascii=False),
            ))
            _write(pendientes, _PENDIENTES_TEMPLATE.format(slug=slug, nombre=nombre, today=today))
            _write(bitacora, _bitacora_template(slug, nombre, today) + f"\n## [{today}] Idea registrada (artemis)\n\n{resumen}\n")
            diario = _log_diario(slug, f"proyecto idea creado: {resumen}")
            commit = _commit([card, pendientes, bitacora, *diario], f"artemis: proyecto idea {slug}")
        except BaseException:
            # Sin dejar una carpeta a medias si falló la escritura o el commit.
            vault._git("reset", "-q", "--", base)
            shutil.rmtree(folder, ignore_errors=True)
            raise
    logger.info("Proyecto idea creado: %s (%s)", slug, commit)
    return {"project": slug, "file": f"{base}/{slug}.md", "commit": commit}


# ---------------------------------------------------------------------------
# Diario (03-diario/AAAA/AAAA-MM-DD.md) y capturas (00-inbox/)
# ---------------------------------------------------------------------------

_DIARIO_TEMPLATE = """---
tipo: diario
fecha: {today}
proyectos: []
autor: artemis
tags: ["diario"]
---

# {today}

## Registro

## Pendientes del día
"""


def _add_project_to_diario_front(text: str, slug: str) -> str:
    """Suma `slug` a `proyectos: [...]` del frontmatter si no está."""
    match = re.search(r"(?m)^proyectos:\s*(\[.*\])\s*$", text)
    if not match:
        return text
    try:
        slugs = json.loads(match.group(1))
    except ValueError:
        return text  # formato que no entiendo: no lo toco
    if slug in slugs:
        return text
    return text[:match.start(1)] + json.dumps(slugs + [slug], ensure_ascii=False) + text[match.end(1):]


def _log_diario(slug: str | None, entry: str, heading: str | None = None) -> list[Path]:
    """Agrega un punto `- HH:MM entry` bajo `### <heading> (artemis)` de la nota de hoy, al final de
    `## Registro`, y devuelve el archivo para sumarlo al commit.

    Si ya hay una sección idéntica de Artemis de hoy, agrega el punto ahí; nunca edita secciones
    de otros autores. Es de mejor esfuerzo: si falla, la escritura principal sigue (devuelve []).
    """
    try:
        now = datetime.now()
        today = now.date().isoformat()
        path = vault.vault_root() / "03-diario" / today[:4] / f"{today}.md"
        if heading is None:
            card = vault.vault_root() / "01-proyectos" / slug / f"{slug}.md"
            meta, _ = vault.parse_frontmatter(_read(card))
            heading = f"proyecto · [[01-proyectos/{slug}/{slug}|{meta.get('nombre') or slug}]]"
        header = f"### {heading} (artemis)"
        bullet = f"- {now:%H:%M} {entry}"

        if path.is_file():
            text = _read(path)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            text = _DIARIO_TEMPLATE.format(today=today)
        lines = text.split("\n")

        reg = next((i for i, l in enumerate(lines) if l.strip() == "## Registro"), None)
        if reg is None:
            lines += ["", "## Registro", ""]
            reg = len(lines) - 2
        end = next((i for i in range(reg + 1, len(lines)) if lines[i].startswith("## ")), len(lines))

        section = next((i for i in range(reg + 1, end) if lines[i].strip() == header), None)
        if section is not None:
            sec_end = next((i for i in range(section + 1, end) if lines[i].startswith("### ")), end)
            insert_at = sec_end
            while insert_at > section + 1 and not lines[insert_at - 1].strip():
                insert_at -= 1
            lines.insert(insert_at, bullet)
        else:
            insert_at = end
            while insert_at > reg + 1 and not lines[insert_at - 1].strip():
                insert_at -= 1
            lines[insert_at:insert_at] = ["", header, "", bullet]
            if insert_at + 4 < len(lines) and lines[insert_at + 4].strip():
                lines.insert(insert_at + 4, "")  # línea en blanco antes del siguiente encabezado
        text = "\n".join(lines)
        if slug:
            text = _add_project_to_diario_front(text, slug)
        _write(path, text)
        return [path]
    except Exception:
        logger.exception("No se pudo registrar en el diario (la escritura principal sigue)")
        return []


def create_captura(text: str, slug: str | None = None) -> dict:
    """Deja una nota `tipo: captura` en `00-inbox/` (autor artemis, sin procesar) para que Kevin o el
    skill `inbox` la rutee. `slug` es opcional: si se da, debe existir."""
    text = _clean(text, single_line=False)
    today = date.today().isoformat()
    with _lock:
        if slug:
            _project_dir(slug)
        now = datetime.now()
        theme = slugify(" ".join(text.split()[:6]))[:40].strip("-") or "captura"
        inbox = vault.vault_root() / "00-inbox"
        inbox.mkdir(exist_ok=True)
        name = f"{now:%Y-%m-%d-%H%M}-{theme}"
        path = inbox / f"{name}.md"
        n = 2
        while path.exists():
            path = inbox / f"{name}-{n}.md"
            n += 1
        title = " ".join(text.split())[:80]
        _write(path, (
            f"---\ntipo: captura\ncreado: {today}\nautor: artemis\nproyecto: {slug or ''}\n"
            f"procesado: false\ntags: [\"inbox\"]\n---\n\n# {title}\n\n{text}\n"
        ))
        rel = path.relative_to(vault.vault_root()).as_posix()
        diario = _log_diario(slug, f"captura en el inbox: [[{rel[:-3]}|{title}]]", heading="inbox")
        commit = _commit([path, *diario], f"artemis: captura {theme}")
    logger.info("Captura creada: %s (%s)", rel, commit)
    return {"project": slug, "file": rel, "commit": commit}
