"""Pruebas de app/research.py y de la respuesta de investigación en app/actions.py (sin llamar a Gemini)."""
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace

import test_actions  # prepara el entorno y el vault temporal (como módulo, para no re-correr sus pruebas)
from app import actions, research


def _citation(url, title):
    return SimpleNamespace(type="url_citation", url=url, title=title)


def _fake_interaction(text, citations=()):
    content = SimpleNamespace(type="text", annotations=list(citations))
    return SimpleNamespace(output_text=text, steps=[SimpleNamespace(type="model_output", content=[content])])


class FakeClient:
    def __init__(self, interaction):
        self.calls = []
        self.interactions = SimpleNamespace(create=self._create)
        self._interaction = interaction

    def _create(self, **kwargs):
        self.calls.append(kwargs)
        return self._interaction


ANSWER = "## Legal\nRevisar la ley de datos personales.\n\nRESUMEN: Hay que cumplir la ley de datos y las reglas de la tienda."


class ResearchTest(unittest.TestCase):
    def setUp(self):
        research._usage.clear()
        self.session = uuid.uuid4()
        if not (Path(test_actions._TMP.name) / "01-proyectos/finanzashogar").exists():
            test_actions.ActionsTest.setUpClass()

    def test_split_summary(self):
        answer, summary = research.split_summary(ANSWER)
        self.assertNotIn("RESUMEN", answer)
        self.assertTrue(summary.startswith("Hay que cumplir"))

    def test_split_summary_without_marker(self):
        answer, summary = research.split_summary("Solo texto sin marca.")
        self.assertEqual((answer, summary), ("Solo texto sin marca.", "Solo texto sin marca."))

    def test_extract_sources_dedupes(self):
        interaction = _fake_interaction("x", [_citation("https://a", "A"), _citation("https://a", "A"), _citation("https://b", None)])
        self.assertEqual(research.extract_sources(interaction), [("A", "https://a"), ("https://b", "https://b")])

    def test_investigate_uses_search_and_project_context(self):
        client = FakeClient(_fake_interaction(ANSWER, [_citation("https://ley.cl", "Ley")]))
        result = research.investigate(client, "m", "¿Es legal guardar fotos de boletas?", "finanzashogar")
        call = client.calls[0]
        self.assertEqual(call["tools"], [{"type": "google_search"}])
        self.assertIn("FinanzasHogar", call["input"])
        self.assertIn("Chile", call["input"])
        self.assertEqual(result.sources, [("Ley", "https://ley.cl")])

    def test_daily_limit(self):
        client = FakeClient(_fake_interaction(ANSWER))
        for _ in range(research.MAX_RESEARCH_PER_DAY):
            research.investigate(client, "m", "pregunta", None)
        with self.assertRaises(research.ResearchLimitError):
            research.investigate(client, "m", "pregunta", None)

    def test_reply_offers_and_saves_summary_on_confirm(self):
        client = FakeClient(_fake_interaction(ANSWER, [_citation("https://ley.cl", "Ley")]))
        reply = actions.research_reply(
            self.session, [{"pregunta": "¿Es legal guardar fotos de boletas?", "proyecto": "finanzashogar"}], client, "m")
        self.assertIn("[Ley](https://ley.cl)", reply)
        self.assertIn("¿Guardo un resumen en la bitácora de **FinanzasHogar**?", reply)
        bitacora = Path(test_actions._TMP.name) / "01-proyectos/finanzashogar/bitacora.md"
        self.assertFalse(bitacora.exists() and "Investigación" in bitacora.read_text(encoding="utf-8"))
        self.assertIn("Anotado", actions.resolve_pending(self.session, "sí"))
        text = bitacora.read_text(encoding="utf-8")
        self.assertIn("Investigación — ¿Es legal guardar fotos de boletas?", text)
        self.assertIn("(artemis)", text)

    def test_reply_without_project_does_not_offer(self):
        client = FakeClient(_fake_interaction(ANSWER))
        reply = actions.research_reply(self.session, [{"pregunta": "¿Rentable un juego de pingüinos?", "proyecto": None}], client, "m")
        self.assertNotIn("¿Guardo", reply)
        self.assertIsNone(actions.resolve_pending(self.session, "sí"))

    def test_gemini_failure_gives_friendly_message(self):
        class Boom:
            interactions = SimpleNamespace(create=lambda **k: (_ for _ in ()).throw(RuntimeError("429")))
        reply = actions.research_reply(self.session, [{"pregunta": "algo", "proyecto": None}], Boom(), "m")
        self.assertIn("No pude completar la investigación", reply)

    def test_research_requests_parse(self):
        interaction = SimpleNamespace(steps=[
            SimpleNamespace(type="function_call", name="investigar", arguments={"pregunta": " ¿Legal? ", "proyecto": "inventado"}),
            SimpleNamespace(type="function_call", name="investigar", arguments={"pregunta": "", "proyecto": "finanzashogar"}),
        ])
        self.assertEqual(actions.research_requests(interaction), [{"pregunta": "¿Legal?", "proyecto": None}])


if __name__ == "__main__":
    unittest.main()
