"""Guardrails estaticos: nao substituem revisao de codigo nem testes Windows."""
from pathlib import Path
import unittest
import re
import xml.etree.ElementTree as ET
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]

# Cada novo fluxo exige inclusao explicita e revisao de finalidade/seguranca.
# A politica nao congela o numero de testes ou impede um fluxo legitimo futuro.
APPROVED_WORKFLOWS = frozenset({
    "build-windows.yml",
    "validar-candidata-windows.yml",
    "triagem-diagnostico-github.yml",
})


def workflow_inventory(folder: Path) -> set[str]:
    return {p.name for p in folder.iterdir()
            if p.is_file() and p.suffix.lower() in {".yml", ".yaml"}}


def workflow_inventory_matches(folder: Path, approved: frozenset[str]) -> bool:
    return workflow_inventory(folder) == approved


def missing_markdown_targets(root: Path) -> list[tuple[str, str]]:
    missing = []
    # Links Markdown relativos, inclusive imagens. URLs/ancoras nao sao arquivos.
    for file in root.rglob("*.md"):
        if set(file.relative_to(root).parts) & {".git", "bin", "obj", ".cache"}:
            continue
        for link in re.findall(r"!?\[[^\]]*\]\(([^\s)]+)(?:\s+\"[^\"]*\")?\)",
                               file.read_text(encoding="utf-8")):
            target = urlsplit(link.strip("<>"))
            if target.scheme or target.netloc or not target.path:
                continue
            if not (file.parent / unquote(target.path)).exists():
                missing.append((file.relative_to(root).as_posix(), link))
    return missing


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class PoliticasDeSeguranca(unittest.TestCase):
    def test_actions_fixadas_e_sem_credenciais_persistentes(self):
        folder = ROOT / ".github/workflows"
        workflows = [folder / name for name in sorted(workflow_inventory(folder))]
        self.assertTrue(workflow_inventory_matches(folder, APPROVED_WORKFLOWS),
            "Workflow adicionado/removido sem revisar a lista explicita de CI")
        self.assertFalse(list(ROOT.glob("LEIA-PRIMEIRO-v5.*.md")),
            "Guias de versões antigas devem ficar no histórico Git, não na raiz")
        for file in workflows:
            code = file.read_text(encoding="utf-8")
            self.assertIn("contents: read", code, str(file))
            for action in re.findall(r"uses:\s*([^\s#]+)", code):
                self.assertRegex(action, r"^actions/[a-z-]+@[a-f0-9]{40}$", str(file))
            if "actions/checkout@" in code:
                self.assertIn("persist-credentials: false", code, str(file))
            for write in ("contents: write", "issues: write", "pull-requests: write", "write-all"):
                self.assertNotIn(write, code, str(file))
        self.assertIn("name: Regressões Windows", read(".github/workflows/validar-candidata-windows.yml"))
        self.assertIn("name: Compilação Windows x64", read(".github/workflows/build-windows.yml"))
        self.assertIn("name: Política de segurança e privacidade", read(".github/workflows/triagem-diagnostico-github.yml"))

    def test_inventario_ci_nao_ignora_yaml_e_permite_expansao_revisada(self):
        import tempfile
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            for name in APPROVED_WORKFLOWS:
                (folder / name).write_text("name: fixture\n", encoding="utf-8")
            self.assertTrue(workflow_inventory_matches(folder, APPROVED_WORKFLOWS))
            added = "validar-compatibilidade.yaml"
            (folder / added).write_text("name: fixture\n", encoding="utf-8")
            self.assertFalse(workflow_inventory_matches(folder, APPROVED_WORKFLOWS))
            self.assertTrue(workflow_inventory_matches(folder,
                APPROVED_WORKFLOWS | frozenset({added})))
            (folder / "build-windows.yml").unlink()
            self.assertFalse(workflow_inventory_matches(folder,
                APPROVED_WORKFLOWS | frozenset({added})))

    def test_documentacao_nao_tem_links_relativos_quebrados(self):
        self.assertEqual(missing_markdown_targets(ROOT), [])

    def test_detector_de_links_relativos_distingue_urls_ancoras_e_arquivos(self):
        import tempfile
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            (folder / "existe.md").write_text("# Existe\n", encoding="utf-8")
            (folder / "index.md").write_text(
                "[Valido](existe.md#titulo) [URL](https://example.com/inexistente.md) "
                "[Ancora](#titulo) ![Ausente](imagem.png)", encoding="utf-8")
            self.assertEqual(missing_markdown_targets(folder), [("index.md", "imagem.png")])

    def test_versao_compativel_entre_projeto_e_manifesto(self):
        project = ET.parse(ROOT / "GerenciadorAplicativos.csproj").getroot()
        version = project.findtext("PropertyGroup/Version")
        self.assertRegex(version or "", r"^[0-9]+(?:\.[0-9]+){3}$")
        manifest = ET.parse(ROOT / "app.manifest").getroot()
        identity = manifest.find("{urn:schemas-microsoft-com:asm.v1}assemblyIdentity")
        self.assertIsNotNone(identity)
        self.assertEqual(identity.attrib["version"], version)

    def test_todos_projetos_de_regressao_tem_caminho_no_ci(self):
        code = "\n".join(read(".github/workflows/" + name) for name in APPROVED_WORKFLOWS)
        projects = {p.relative_to(ROOT).as_posix() for p in ROOT.glob("Testes*/*.csproj")}
        invoked = set(re.findall(r"dotnet run --project ([\w/.-]+\.csproj)", code))
        self.assertEqual(projects - invoked, set(), "Projeto de testes sem caminho no CI atual")

    def test_residuos_nao_alcancam_operacoes_destrutivas(self):
        for name in ("UninstallResiduals.cs", "AdvancedUninstallCleaner.cs", "ResidualAudit.cs",
                     "HistoricalResidualAudit.cs", "BroadResidualDiscovery.cs", "ResidualPreview.cs",
                     "ResidualReportMerger.cs", "ResidualAuditDialog.cs", "ResidualResultIndex.cs",
                     "ResidualInspection.cs", "ResidualPathIndex.cs", "ResidualExplorerDialog.cs", "DarkResidualList.cs"):
            code = read(name)
            for forbidden in ("File.Delete(", "Directory.Delete(", "DeleteSubKeyTree(",
                              ".DeleteSubKey(", "CleanDirectorySafely(", "Process.Start(", "HttpClient"):
                self.assertNotIn(forbidden, code, name)
        self.assertIn("CanExecute => false", read("ResidualPreview.cs"))
        self.assertIn("CanRemove => false", read("ResidualAudit.cs"))

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
        extensions = {".cs", ".ps1", ".cmd", ".bat", ".mjs", ".iss", ".yml", ".yaml", ".py"}
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
            if file.suffix.lower() in {".yml", ".yaml"} and "/workflows/" in file.as_posix():
                self.assertNotRegex(contents,
                    r'''(?m)^\s*['"]?(?:issues|issue_comment|pull_request_target|repository_dispatch)['"]?\s*:''',
                    str(file))
        self.assertGreaterEqual(len(scanned), 80,
            "Inventario de codigo incompleto: a auditoria nao deve aceitar arvore vazia")
        print(f"Auditoria estatica: {len(scanned)} arquivos-fonte examinados.")

    def test_winget_sem_executor_e_sem_fallback_em_producao(self):
        adapter = read("WingetConsultativeProvider.cs")
        context = read("WingetContextInspection.cs")
        query = read("OfficialReleaseSources.cs")
        main = read("Program.cs")
        for code in (adapter, context, query):
            for forbidden in ("Process.Start(", "ProcessStartInfo", "CoCreateInstance", "GetTypeFromCLSID",
                              "Assembly.Load", "ExecutionPolicy", 'Verb = "runas"', "InstallPackageAsync"):
                self.assertNotIn(forbidden, code)
        self.assertNotIn("ForSimulation(", main)
        self.assertNotIn(".TryOptIn(", main)
        self.assertNotIn("Microsoft.Management.Deployment", read("GerenciadorAplicativos.csproj"))
        self.assertIn("backend is null", adapter)
        self.assertIn("origin != WingetQueryOrigin.ExplicitLabTest", adapter)
        self.assertNotRegex(read("Criar-Instalador-Windows-x64.bat"), r"(?im)^\s*winget\s")

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
