"""Guardrails estaticos: nao substituem revisao de codigo nem testes Windows."""
from pathlib import Path
import unittest
import re
import xml.etree.ElementTree as ET
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]

# Lista revisada, nunca descoberta dinamicamente como fonte de autorizacao.
# Os 9 ensaios do autoatualizador sao somente push em branches de laboratorio;
# nao publicam pacotes e nao recebem credenciais persistentes. A revisao de
# gatilho, escopo e permissoes fica documentada no teste abaixo.
CORE_WORKFLOWS = frozenset({
    "build-windows.yml",
    "integridade-externa.yml",
    "validar-candidata-windows.yml",
    "triagem-diagnostico-github.yml",
})

# Nome -> (branch push exclusiva, pasta de fixtures permitida no filtro paths).
# Qualquer novo fluxo, trigger ou mudanca de branch exige nova revisao explicita.
EXPERIMENTAL_UPDATER_WORKFLOWS = {
    "autoatualizador-etapa2-isolado.yml": (
        "work-autoatualizador-etapa2-validacao-20261028",
        "prototipos/autoatualizador-seguro/**"),
    "autoatualizador-etapa3-isolado.yml": (
        "work-autoatualizador-etapa3-validacao-20261028",
        "prototipos/autoatualizador-seguro/stage3/**"),
    "autoatualizador-etapa4-isolado.yml": (
        "work-autoatualizador-etapa4-validacao-20261028",
        "prototipos/autoatualizador-seguro/stage4/**"),
    "autoatualizador-etapa5-isolado.yml": (
        "work-autoatualizador-etapa5-validacao-20261028",
        "prototipos/autoatualizador-seguro/stage5/**"),
    "autoatualizador-etapa8-isolado.yml": (
        "work-autoatualizador-etapa8-validacao-20261028",
        "prototipos/autoatualizador-seguro/stage8/**"),
    "autoatualizador-etapa9-isolado.yml": (
        "work-autoatualizador-etapa9-validacao-20261028",
        "prototipos/autoatualizador-seguro/stage9/**"),
    "autoatualizador-etapa10-laboratorio.yml": (
        "work-autoatualizador-etapa10-validacao-20261029",
        "prototipos/autoatualizador-seguro/stage10/**"),
    "autoatualizador-etapa11-isolado.yml": (
        "work-autoatualizador-etapa11-validacao-20261029",
        "prototipos/autoatualizador-seguro/stage11/**"),
    "autoatualizador-etapa12-isolado.yml": (
        "work-autoatualizador-etapa12-validacao-20261029",
        "prototipos/autoatualizador-seguro/stage12/**"),
}
APPROVED_WORKFLOWS = CORE_WORKFLOWS | frozenset(EXPERIMENTAL_UPDATER_WORKFLOWS)


def workflow_inventory(folder: Path) -> set[str]:
    return {p.name for p in folder.iterdir()
            if p.is_file() and p.suffix.lower() in {".yml", ".yaml"}}


def workflow_inventory_matches(folder: Path, approved: frozenset[str]) -> bool:
    return workflow_inventory(folder) == approved


def experimental_push_is_restricted(code: str, filename: str,
                                    branch: str, source_scope: str) -> bool:
    # Examina apenas o bloco on: top-level; inserir pull_request, schedule,
    # workflow_dispatch ou ampliar paths nao passa silenciosamente.
    trigger = re.search(r"(?ms)^on:\r?\n(.*?)(?=^[A-Za-z][\w-]*:|\Z)", code)
    expected = (
        "  push:\n"
        f"    branches: [{branch}]\n"
        "    paths:\n"
        f"      - {source_scope}\n"
        f"      - .github/workflows/{filename}"
    )
    return trigger is not None and trigger.group(1).strip() == expected.strip()


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
    def test_work7_perfis_nao_autorizam_exclusao_por_resumo(self):
        for name in ("FoobarProfileCleanupFlow.cs", "ScopedAppProfileCleanup.cs"):
            code = read(name)
            for forbidden in ("DeleteSubKeyTree(", "DeleteCheckedFolder(", "Directory.Delete("):
                self.assertNotIn(forbidden, code, name)
        self.assertIn("CanExecute => false", read("FoobarProfileCleanup.cs"))

    def test_work7_windows_update_exige_resultado_completo(self):
        code = read("AdvancedFeatures.cs")
        self.assertGreaterEqual(code.count("WindowsUpdateSearchPolicy.RequireSuccessfulSearch((int)found.ResultCode)"), 2)

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
                # Fluxos locais revisados usam o mesmo commit do chamador.
                if action.startswith("./.github/workflows/"):
                    self.assertEqual(file.name, "build-windows.yml")
                    self.assertIn(action, {
                        "./.github/workflows/validar-candidata-windows.yml",
                        "./.github/workflows/triagem-diagnostico-github.yml",
                    })
                    continue
                self.assertRegex(action, r"^actions/[a-z-]+@[a-f0-9]{40}$", str(file))
            if "actions/checkout@" in code:
                self.assertIn("persist-credentials: false", code, str(file))
            for write in ("contents: write", "issues: write", "pull-requests: write", "write-all"):
                self.assertNotIn(write, code, str(file))
        self.assertIn("name: Regressões Windows", read(".github/workflows/validar-candidata-windows.yml"))
        self.assertIn("name: Compilação Windows x64", read(".github/workflows/build-windows.yml"))
        self.assertIn("name: Política de segurança e privacidade", read(".github/workflows/triagem-diagnostico-github.yml"))

    def test_workflows_autoatualizador_aprovados_continuam_isolados(self):
        self.assertEqual(len(EXPERIMENTAL_UPDATER_WORKFLOWS), 9)
        for name, (branch, source_scope) in EXPERIMENTAL_UPDATER_WORKFLOWS.items():
            with self.subTest(workflow=name):
                code = read(".github/workflows/" + name)
                self.assertTrue(experimental_push_is_restricted(
                    code, name, branch, source_scope),
                    f"{name}: trigger/branch/paths mudaram sem revisao")
                self.assertIn("permissions:\n  contents: read", code)
                self.assertIn("persist-credentials: false", code)
                for forbidden in (
                    "secrets.", "GITHUB_TOKEN", "id-token: write",
                    "attestations: write", "contents: write", "issues: write",
                    "pull-requests: write", "write-all",
                    "actions/upload-artifact@", "actions/download-artifact@",
                    "actions/attest@", "gh release ", "git push ",
                ):
                    self.assertNotIn(forbidden, code, name)

                # Controles negativos: um trigger novo, branch diferente ou
                # filtro de pasta ampliado exigem nova aprovacão explícita.
                self.assertFalse(experimental_push_is_restricted(
                    code.replace("  push:\n", "  pull_request:\n", 1),
                    name, branch, source_scope))
                self.assertFalse(experimental_push_is_restricted(
                    code.replace(f"branches: [{branch}]",
                                 "branches: [main]", 1),
                    name, branch, source_scope))
                self.assertFalse(experimental_push_is_restricted(
                    code.replace(f"      - {source_scope}\n",
                                 "      - **\n", 1),
                    name, branch, source_scope))
                self.assertFalse(experimental_push_is_restricted(
                    code.replace("  push:\n",
                                 "  workflow_dispatch:\n  push:\n", 1),
                    name, branch, source_scope))

    def test_work7_pacote_exige_regressoes_e_politicas(self):
        build = read(".github/workflows/build-windows.yml")
        self.assertRegex(build, r"(?m)^  build:\n    name: Compilação Windows x64\n    needs: \[validar, politica\]")
        self.assertIn("validar:\n    uses: ./.github/workflows/validar-candidata-windows.yml", build)
        self.assertIn("politica:\n    uses: ./.github/workflows/triagem-diagnostico-github.yml", build)
        for name in ("validar-candidata-windows.yml", "triagem-diagnostico-github.yml"):
            code = read(".github/workflows/" + name)
            self.assertIn("\n  workflow_call:", code)
            self.assertNotIn("group: ${{ github.workflow }}-${{ github.ref }}", code)

    def test_beta_integridade_externa_continua_obrigatoria_no_fluxo_proprio(self):
        deterministic = read(".github/workflows/validar-candidata-windows.yml")
        external = read(".github/workflows/integridade-externa.yml")
        self.assertNotIn("--verify-engine-downloads", deterministic)
        self.assertNotIn("--verify-downloads", deterministic)
        for argument in ("--verify-engine-downloads", "--verify-work13", "--verify-sevenzip", "--verify-terminal"):
            self.assertIn(argument, external)
        self.assertIn("  pull_request:", external)
        self.assertNotIn("continue-on-error", external)
        self.assertIn("needs: [validar, politica]", read(".github/workflows/build-windows.yml"))
        self.assertIn("steps.pacote.outputs.beta != 'true'", read(".github/workflows/build-windows.yml"))

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
                     "ResidualReportMerger.cs", "ResidualResultIndex.cs",
                     "ResidualInspection.cs", "ResidualPathIndex.cs"):
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
