"""Pruebas del diario y las capturas de app/vault_write.py contra un vault temporal."""
import re
import subprocess
import unittest
import uuid
from datetime import date
from pathlib import Path

import test_actions  # prepara el entorno y el vault temporal
from app import actions, vault_write


def _root():
    return Path(test_actions._TMP.name)


def _diario():
    today = date.today().isoformat()
    return _root() / "03-diario" / today[:4] / f"{today}.md"


def _git(*args):
    return subprocess.run(["git", "-C", str(_root()), *args], capture_output=True, text=True).stdout


class DiarioTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not (_root() / "01-proyectos/finanzashogar").exists():
            test_actions.ActionsTest.setUpClass()

    def setUp(self):
        self.session = uuid.uuid4()

    def test_pendiente_creates_diario_in_same_commit(self):
        vault_write.add_pendiente("finanzashogar", "Tarea para el diario")
        text = _diario().read_text(encoding="utf-8")
        self.assertRegex(text, r"### proyecto · \[\[01-proyectos/finanzashogar/finanzashogar\|FinanzasHogar\]\] \(artemis\)")
        self.assertRegex(text, r"- \d\d:\d\d pendiente agregado: Tarea para el diario")
        self.assertIn('"finanzashogar"', re.search(r"(?m)^proyectos: .*$", text).group(0))
        self.assertIn("03-diario/", _git("show", "--stat", "--format=", "HEAD"))
        self.assertEqual(_git("status", "--porcelain").strip(), "")

    def test_second_action_same_project_reuses_section(self):
        vault_write.add_pendiente("finanzashogar", "Primera")
        vault_write.add_comentario("finanzashogar", "Un comentario\ncon dos líneas")
        text = _diario().read_text(encoding="utf-8")
        header = "### proyecto · [[01-proyectos/finanzashogar/finanzashogar|FinanzasHogar]] (artemis)"
        self.assertEqual(text.count(header), 1)
        self.assertIn("comentario en la bitácora: Un comentario con dos líneas", text)
        self.assertLess(text.index("## Registro"), text.index(header))
        self.assertLess(text.index("comentario en la bitácora"), text.index("## Pendientes del día"))

    def test_other_authors_sections_are_untouched(self):
        path = _diario()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            "---\ntipo: diario\nfecha: x\nproyectos: []\nautor: kevin\ntags: [\"diario\"]\n---\n\n# x\n\n## Registro\n\n"
            "### proyecto · [[01-proyectos/uma/uma|uma]]\n\nTexto de Kevin.\n\n## Pendientes del día\n\n- algo\n",
            encoding="utf-8")
        vault_write.add_pendiente("finanzashogar", "Con nota previa")
        text = path.read_text(encoding="utf-8")
        self.assertIn("### proyecto · [[01-proyectos/uma/uma|uma]]\n\nTexto de Kevin.\n", text)
        self.assertIn("autor: kevin", text)
        self.assertIn("## Pendientes del día\n\n- algo", text)

    def test_diario_failure_is_swallowed(self):
        # Un proyecto sin ficha hace fallar el registro del diario: debe devolver [] y no propagar.
        self.assertEqual(vault_write._log_diario("no-existe", "x"), [])

    def test_captura_file_and_diario(self):
        result = vault_write.create_captura("Comprar un cable USB-C para la mascota\ny revisar el precio")
        path = _root() / result["file"]
        self.assertRegex(result["file"], r"^00-inbox/\d{4}-\d\d-\d\d-\d{4}-comprar-un-cable-usb-c-para-la\.md$")
        text = path.read_text(encoding="utf-8")
        self.assertIn("tipo: captura", text)
        self.assertIn("autor: artemis", text)
        self.assertIn("procesado: false", text)
        self.assertIn("### inbox (artemis)", _diario().read_text(encoding="utf-8"))
        self.assertEqual(_git("status", "--porcelain").strip(), "")

    def test_two_capturas_same_minute_do_not_collide(self):
        a = vault_write.create_captura("Mismo texto")
        b = vault_write.create_captura("Mismo texto")
        self.assertNotEqual(a["file"], b["file"])

    def test_captura_with_unknown_project_is_rejected(self):
        with self.assertRaises(vault_write.VaultWriteError):
            vault_write.create_captura("Algo", "inventado")

    def test_captura_tool_flow(self):
        from types import SimpleNamespace
        call = SimpleNamespace(type="function_call", name="proponer_captura",
                               arguments={"texto": "Revisar el seguro del auto", "proyecto": "no-existe"})
        reply = actions.propose_from_interaction(self.session, SimpleNamespace(steps=[call]))
        self.assertIn("Nota en el inbox", reply)
        self.assertNotIn("relacionada con", reply)  # el proyecto inventado se descarta
        self.assertIn("nota en el inbox", actions.resolve_pending(self.session, "sí"))


if __name__ == "__main__":
    unittest.main()
