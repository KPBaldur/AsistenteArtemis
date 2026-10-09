"""Escritura acotada en el cerebro de proyectos (iteración 2 del contrato con Artemis,
99-sistema/contrato-artemis.md en BrainObsidian).

Solo dos operaciones, las que usa la mascota:
- agregar un pendiente a `## Abiertos` de 01-proyectos/<slug>/pendientes.md;
- agregar una entrada "(artemis)" al final de 01-proyectos/<slug>/bitacora.md.

Ambas actualizan `actualizado` de la ficha del proyecto y hacen un commit local con autor
Artemis y prefijo `artemis:`. Nunca hacen push (desde este equipo el push lo corre Kevin) y
nunca reescriben ni borran líneas existentes.
"""
import logging
import re
import threading
from datetime import date
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
        commit = _commit([path, card], f"artemis: pendiente en {slug}")
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
        commit = _commit([path, card], f"artemis: comentario en {slug}")
    logger.info("Comentario agregado en %s (%s)", slug, commit)
    return {"project": slug, "file": f"01-proyectos/{slug}/bitacora.md", "commit": commit}
