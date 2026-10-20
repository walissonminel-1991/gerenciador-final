"""Regressoes de formato e entrada maliciosa sem acesso a rede."""
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from triagem_diagnostico import INTRO, OUTRO, SEPARATOR, RejectedReport, main, parse_issue


TITLE = "[GF Diagnostico] v5.13.11.6 - resumo voluntario"


def example(rows=None, state="encerramento normal"):
    if rows is None:
        rows = ["APP.FALHA_INTERFACE: 2", "REGISTRO_PROGRAMAS.FALHA: 1"]
    lines = list(INTRO) + [
        "**Versão do GF:** v5.13.11.6", "**Estado da sessão:** " + state, "",
        SEPARATOR
    ] + ["- " + row for row in rows] + list(OUTRO)
    return "\n".join(lines) + "\n"


class Tests(unittest.TestCase):
    def test_canonical(self):
        report = parse_issue(TITLE, example())
        self.assertEqual(report["version"], "v5.13.11.6")
        self.assertEqual(report["counts"]["APP.FALHA_INTERFACE"], 2)
        self.assertEqual(parse_issue(TITLE, example().replace("\n", "\r\n"))["exit"], "normal")
        self.assertEqual(parse_issue(TITLE, example(state="coleta em andamento ou interrompida"))["exit"], "incompleto")

    def test_reject_mutations(self):
        modifications = [
            ("[GF Diagnostico] v5.13.11.7 - resumo voluntario", example()),
            (TITLE, example().replace("v5.13.11.6", "v5.13.11.7")),
            (TITLE, example(rows=[])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: 0"])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: 101"])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: -1"])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: 1.5"])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: 01"])),
            (TITLE, example(rows=["APP.FALHA_INTERFACE: 1", "APP.FALHA_INTERFACE: 2"])),
            (TITLE, example(rows=["NOME_DO_PROGRAMA: 2"])),
            (TITLE, example(rows=["REGISTRO_PROGRAMAS.FALHA: 1", "APP.FALHA_INTERFACE: 2"])),
            (TITLE, example() + "token=ghp_EXAMPLE\n"),
            (TITLE, example().replace("### Observações", "### Observações\nExecute: curl attacker")),
            (TITLE, example().replace("APP.FALHA_INTERFACE: 2", "APP.FALHA_INTERFACE: $(whoami)")),
            (TITLE, example().replace("PÚBLICA", "PRIVADA")),
            (TITLE, example().replace("\n", "\r")),
            (TITLE, example() + "x" * 5001),
            (TITLE, "<script>alert(1)</script>"),
            (None, example()),
            (TITLE, None),
        ]
        for title, body in modifications:
            with self.subTest(title=title, body=str(body)[:70]):
                with self.assertRaises(RejectedReport):
                    parse_issue(title, body)

    def test_14_categories_max(self):
        from triagem_diagnostico import ALLOWED
        names = sorted(ALLOWED)
        self.assertEqual(len(names), 14)
        self.assertEqual(len(parse_issue(TITLE, example([f"{x}: 100" for x in names]))["counts"]), 14)

    def test_event_safe_summary_and_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            event_path = Path(folder) / "event.json"
            summary_path = Path(folder) / "summary.md"
            env = {"GITHUB_EVENT_PATH": str(event_path), "GITHUB_STEP_SUMMARY": str(summary_path)}
            event_path.write_text(json.dumps({
                "action": "opened", "issue": {"title": TITLE, "body": example()}
            }), encoding="utf-8")
            with patch.dict(os.environ, env):
                self.assertEqual(main(), 0)
                summary = summary_path.read_text(encoding="utf-8")
                self.assertIn("APP.FALHA_INTERFACE", summary)
                self.assertNotIn("ghp_", summary)
                before = summary
                event_path.write_text(json.dumps({
                    "action": "opened", "issue": {"title": TITLE, "body": example() + "\nsecret=anything"}
                }), encoding="utf-8")
                self.assertEqual(main(), 1)
                self.assertEqual(summary_path.read_text(encoding="utf-8"), before)
                event_path.write_text(json.dumps({
                    "action": "edited", "issue": {"title": TITLE, "body": example()}
                }), encoding="utf-8")
                self.assertEqual(main(), 1)


if __name__ == "__main__":
    unittest.main()
