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

    def test_varredura_estatica_de_todos_arquivos_fonte(self):
        # Percorre toda a arvore de codigo, nao somente uma amostra manual.
        # Padroes textuais sao guardrails, nao detector universal de malware.
        extensions = {".cs", ".ps1", ".cmd", ".bat", ".mjs", ".iss", ".yml", ".py"}
        excluded_dirs = {".git", "bin", "obj", "Distribuicao", ".venv"}
        scanned = []
        for file in ROOT.rglob("*"):
            if not file.is_file() or file.suffix.lower() not in extensions or (
                set(file.relative_to(ROOT).parts) & excluded_dirs
            ):
                continue
            contents = file.read_text(encoding="utf-8")
            scanned.append(file.relative_to(ROOT).as_posix())
            if file.suffix == ".cs" and file.parent == ROOT:
                self.assertNotIn("OpenFileDialog", contents, str(file))
                self.assertNotIn("GitHubDiagnosticShare", contents, str(file))
            if file.suffix == ".yml" and "/workflows/" in file.as_posix():
                self.assertNotIn("pull_request_target:", contents, str(file))
                self.assertNotIn("repository_dispatch:", contents, str(file))
                self.assertNotIn("\\n  issues:", contents, str(file))
                self.assertNotIn("\\n  issue_comment:", contents, str(file))
        self.assertGreaterEqual(len(scanned), 80,
            "Inventario de codigo incompleto: a auditoria nao deve aceitar arvore vazia")
        print(f"Auditoria estatica: {len(scanned)} arquivos-fonte examinados.")

    def test_instaladores_exigem_sha_fixado_e_assinatura(self):
        source = read("VerifiedUpdaters.cs")
        self.assertIn("approved.Sha256 is { Length: 64 }", source)
        self.assertIn("VerifyAuthenticode(path, recipe.RequiredSigner", source)
        self.assertIn("RequireDigestAsync(locked, receipt.Digest)", source)
        self.assertIn("IsApprovedRecipe(recipe)", source)

    def test_simulacao_e_isolada_de_rede_dispositivos_e_consentimento(self):
        code = read("AnonymousTelemetry.cs")
        simulation = code.split("internal static string SimulateContributionWithoutSending", 1)[1]
        simulation = simulation.split("private static string HumanCategory", 1)[0]
        for forbidden in ("HttpClient", "Process.Start(", "File.", "Directory.",
                          "SetConsent(", "_events", "_folder", "ReadAllText(", "WebRequest"):
            self.assertNotIn(forbidden, simulation,
                f"Simulacao local nao pode conter operacao com efeito: {forbidden}")
        self.assertIn("DiagnosticPayloadPolicy.IsValidReport(syntheticReport)", simulation)

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
