"""Guardrails estaticos: nao substituem revisao de codigo nem testes Windows."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class PoliticasDeSeguranca(unittest.TestCase):
    def test_aplicativo_sem_envio_manual_de_diagnosticos(self):
        source = "\n".join(p.read_text(encoding="utf-8")
                            for p in ROOT.glob("*.cs"))
        self.assertNotIn("ShareAnonymousDiagnosticToGitHub", source)
        self.assertNotIn("GitHubDiagnosticShare", source)
        self.assertNotIn("issues/new", source)
        self.assertNotIn("TrayGitHubDiagnostic", source)
        self.assertNotIn("OpenFileDialog", source)
        self.assertNotIn("HttpPostedFile", source)
        self.assertIn("DiagnosticPayloadPolicy.IsValidReport", source)

    def test_ci_nao_processa_issues_ou_anexos_publicos(self):
        workflow = read(".github/workflows/triagem-diagnostico-github.yml")
        self.assertNotIn("\n  issues:", workflow)
        self.assertNotIn("\n  issue_comment:", workflow)
        self.assertNotIn("\n  pull_request_target:", workflow)
        self.assertNotIn("GITHUB_EVENT_PATH", workflow)
        self.assertNotIn("repository_dispatch", workflow)
        self.assertIn("contents: read", workflow)

    def test_instaladores_exigem_sha_fixado_e_assinatura(self):
        source = read("VerifiedUpdaters.cs")
        self.assertIn("approved.Sha256 is { Length: 64 }", source)
        self.assertIn("VerifyAuthenticode(path, recipe.RequiredSigner", source)
        self.assertIn("RequireDigestAsync(locked, receipt.Digest)", source)
        self.assertIn("IsApprovedRecipe(recipe)", source)

    def test_receptor_remoto_desativado_por_padrao(self):
        source = read("prototipos/telemetria-worker/worker.mjs")
        self.assertIn("status: 503", source)
        self.assertNotIn("env.DB.batch(", source)

    def test_pr_nao_recebe_credenciais_de_atestacao(self):
        workflow = read(".github/workflows/build-windows.yml")
        build, attest = workflow.split("\n  attest:", 1)
        self.assertNotIn("id-token: write", build)
        self.assertNotIn("attestations: write", build)
        self.assertIn("github.ref == 'refs/heads/main'", attest)
        self.assertIn("id-token: write", attest)
        self.assertIn("sha256sum --check SHA256SUMS.txt", attest)


if __name__ == "__main__":
    unittest.main()
