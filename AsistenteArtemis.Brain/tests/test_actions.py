"""Pruebas de app/actions.py contra un vault temporal (nunca el real).

Correr desde AsistenteArtemis.Brain:  venv/Scripts/python.exe -m unittest discover -s tests -v
"""
import os
import subprocess
import tempfile
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace

# Antes de importar app.*: config lee el entorno al importarse y load_dotenv no pisa lo ya definido.
_TMP = tempfile.TemporaryDirectory()
os.environ.update(
    DATABASE_URL="postgresql://x:x@localhost/x", GEMINI_API_KEY="x", INTERNAL_API_KEY="x",
    VAULT_PATH=_TMP.name, VAULT_OWNER_USER_ID=str(uuid.uuid4()), VAULT_GIT_PULL="false",
)

from app import actions  # noqa: E402


def _git(*args):
    subprocess.run(["git", "-C", _TMP.name, *args], check=True, capture_output=True)


def _call(name, **args):
    return SimpleNamespace(type="function_call", name=name, arguments=args)


def _interaction(*calls):
    return SimpleNamespace(steps=list(calls), output_text="")


class ActionsTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        root = Path(_TMP.name)
        folder = root / "01-proyectos" / "finanzashogar"
        folder.mkdir(parents=True)
        (folder / "finanzashogar.md").write_text(
            "---\ntipo: proyecto\nid: finanzashogar\nnombre: FinanzasHogar\nestado: activo\nambito: personal\n"
            "inicio: 2026-01-01\nactualizado: 2026-01-01\nresumen: Gastos del hogar.\nstack: []\nrepo: []\n---\n# F\n",
            encoding="utf-8")
        (folder / "pendientes.md").write_text(
            "---\ntipo: pendientes\nproyecto: finanzashogar\nactualizado: 2026-01-01\n---\n\n## Abiertos\n\n## Resueltos\n",
            encoding="utf-8")
        _git("init", "-q")
        _git("add", ".")
        _git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-qm", "init")

    def setUp(self):
        self.session = uuid.uuid4()

    def _pendientes(self):
        return (Path(_TMP.name) / "01-proyectos/finanzashogar/pendientes.md").read_text(encoding="utf-8")

    def test_proposal_does_not_write(self):
        reply = actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="finanzashogar", texto="Foto de boleta")))
        self.assertIn("¿Confirmas?", reply)
        self.assertIn("FinanzasHogar", reply)
        self.assertNotIn("Foto de boleta", self._pendientes())

    def test_yes_writes_and_commits(self):
        actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="finanzashogar", texto="Escanear boleta con la cámara")))
        reply = actions.resolve_pending(self.session, "Sí, dale")
        self.assertIsNone(reply)  # "si dale" no es una confirmación estricta: se descarta
        actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="finanzashogar", texto="Escanear boleta con la cámara")))
        reply = actions.resolve_pending(self.session, "Sí")
        self.assertIn("Anotado", reply)
        self.assertIn("Escanear boleta con la cámara", self._pendientes())
        log = subprocess.run(["git", "-C", _TMP.name, "log", "-1", "--format=%an|%s"], capture_output=True, text=True).stdout
        self.assertEqual(log.strip(), "Artemis|artemis: pendiente en finanzashogar")

    def test_no_cancels(self):
        actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_comentario", proyecto="finanzashogar", texto="Idea suelta")))
        self.assertIn("no anoté", actions.resolve_pending(self.session, "no"))
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))  # ya no queda nada pendiente

    def test_unrelated_message_discards(self):
        actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="finanzashogar", texto="Algo")))
        self.assertIsNone(actions.resolve_pending(self.session, "mejor cámbialo a otra cosa"))
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))

    def test_unknown_project_is_not_stored(self):
        reply = actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="inventado", texto="Algo")))
        self.assertIn("No encuentro el proyecto", reply)
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))

    def test_no_function_call_returns_none(self):
        self.assertIsNone(actions.propose_from_interaction(self.session, _interaction()))

    def test_expired_proposal_is_ignored(self):
        actions.propose_from_interaction(
            self.session, _interaction(_call("proponer_pendiente", proyecto="finanzashogar", texto="Vieja")))
        actions._pending[self.session].created -= actions.PROPOSAL_TTL_SECONDS + 1
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))
        self.assertNotIn("Vieja", self._pendientes())


if __name__ == "__main__":
    unittest.main()
