"""Pruebas de resolver pendientes (mover de ## Abiertos a ## Resueltos) contra un vault temporal."""
import subprocess
import unittest
import uuid
from datetime import date
from pathlib import Path
from types import SimpleNamespace

import test_actions  # prepara el entorno y el vault temporal
from app import actions, vault_write


def _root():
    return Path(test_actions._TMP.name)


def _git(*args):
    return subprocess.run(["git", "-C", str(_root()), *args], capture_output=True, text=True).stdout


def _call(**args):
    return SimpleNamespace(steps=[SimpleNamespace(type="function_call", name="proponer_resolver_pendiente", arguments=args)])


class ResolverTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not (_root() / "01-proyectos/finanzashogar").exists():
            test_actions.ActionsTest.setUpClass()

    def setUp(self):
        self.session = uuid.uuid4()
        name = f"Proyecto Resolver {uuid.uuid4().hex[:6]}"
        self.slug = vault_write.create_project(name, "Para probar.")["project"]
        self.path = _root() / "01-proyectos" / self.slug / "pendientes.md"
        self.path.write_text(
            f"---\ntipo: pendientes\nproyecto: {self.slug}\nactualizado: 2026-01-01\n---\n\n# P\n\n## Abiertos\n\n"
            "- [ ] Agregar la foto de la boleta a gastos hormiga · desde 2026-09-01\n"
            "  - detalle indentado que viaja con el pendiente\n"
            "- [ ] Revisar el seguro del auto · desde 2026-09-02\n"
            "- [ ] Revisar el seguro de la casa · desde 2026-09-03\n\n"
            "## Resueltos\n\n- [x] Ya hecho antes · desde 2026-08-01 · resuelto 2026-08-02\n",
            encoding="utf-8", newline="\n")
        _git("add", "."); _git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-qm", "fixture")

    def _text(self):
        return self.path.read_text(encoding="utf-8")

    def test_find_by_substring_words_none_and_ambiguous(self):
        find = lambda f: vault_write.find_open_pendientes(self.slug, f)
        self.assertEqual(find("Foto de la BOLETA"), ["Agregar la foto de la boleta a gastos hormiga · desde 2026-09-01"])
        self.assertEqual(len(find("boleta gastos hormiga foto")), 1)  # por palabras en común
        self.assertEqual(find("lavar el auto"), [])
        self.assertEqual(len(find("revisar el seguro")), 2)

    def test_resolve_moves_line_with_continuation(self):
        item = "Agregar la foto de la boleta a gastos hormiga · desde 2026-09-01"
        vault_write.resolve_pendiente(self.slug, item)
        text = self._text()
        abiertos, resueltos = text.split("## Resueltos")
        self.assertNotIn("foto de la boleta", abiertos)
        today = date.today().isoformat()
        self.assertIn(f"- [x] {item} · resuelto {today}\n  - detalle indentado que viaja con el pendiente", resueltos)
        self.assertIn("Ya hecho antes", resueltos)
        self.assertIn(f"actualizado: {today}", text)
        self.assertEqual(_git("log", "-1", "--format=%an|%s").strip(), f"Artemis|artemis: pendiente resuelto en {self.slug}")
        self.assertEqual(_git("status", "--porcelain").strip(), "")

    def test_resolve_creates_resueltos_section_if_missing(self):
        self.path.write_text("---\ntipo: pendientes\nproyecto: x\nactualizado: 2026-01-01\n---\n\n## Abiertos\n\n- [ ] Única tarea\n",
                             encoding="utf-8", newline="\n")
        vault_write.resolve_pendiente(self.slug, "Única tarea")
        text = self._text()
        self.assertIn("## Resueltos\n\n- [x] Única tarea · resuelto", text)
        self.assertNotIn("- [ ]", text)

    def test_resolve_stale_item_fails(self):
        with self.assertRaises(vault_write.VaultWriteError):
            vault_write.resolve_pendiente(self.slug, "Algo que no está")

    def test_tool_flow_confirm(self):
        reply = actions.propose_from_interaction(self.session, _call(proyecto=self.slug, pendiente="foto de la boleta"))
        self.assertIn("marcar como resuelto", reply)
        self.assertIn("- [ ] Agregar la foto", self._text())  # aún no escribió
        self.assertIn("Anotado", actions.resolve_pending(self.session, "sí"))
        self.assertNotIn("- [ ] Agregar la foto", self._text())

    def test_tool_flow_ambiguous_and_missing_store_nothing(self):
        reply = actions.propose_from_interaction(self.session, _call(proyecto=self.slug, pendiente="seguro"))
        self.assertIn("varios pendientes", reply)
        self.assertIn("seguro del auto", reply)
        self.assertIn("seguro de la casa", reply)
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))
        reply = actions.propose_from_interaction(self.session, _call(proyecto=self.slug, pendiente="lavar el auto"))
        self.assertIn("No encontré un pendiente abierto", reply)
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))


if __name__ == "__main__":
    unittest.main()
