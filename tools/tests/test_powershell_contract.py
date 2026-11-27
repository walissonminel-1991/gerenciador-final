import pathlib
import unittest
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).parents[1]


class PowerShellToolContractTests(unittest.TestCase):
    def test_health_check_is_non_installing_and_checks_pinned_tools(self):
        source = (ROOT / "Check-GfEngineeringTools.ps1").read_text(encoding="utf-8")
        self.assertIn("dotnet-trace", source)
        self.assertIn("dotnet-counters", source)
        self.assertIn("10.0.750501", source)
        self.assertNotIn("dotnet tool install", source)
        self.assertNotIn("pip install", source)

    def test_collection_requires_explicit_pid_sha_and_scenario(self):
        source = (ROOT / "Collect-GfDiagnostics.ps1").read_text(encoding="utf-8")
        self.assertIn("$ProcessId", source)
        self.assertIn("$ExpectedCommit", source)
        self.assertIn("$Scenario", source)
        self.assertIn("dotnet-trace", source)
        self.assertIn("dotnet-counters", source)
        self.assertNotIn("Start-Process (Join-Path", source)

    def test_collection_binds_outputs_and_collectors_to_explicit_run(self):
        source = (ROOT / "Collect-GfDiagnostics.ps1").read_text(encoding="utf-8")
        self.assertIn("Commit Git:[\\t ]*", source)
        self.assertIn("'{0:dd\\:hh\\:mm\\:ss}'", source)
        self.assertIn("--duration", source)
        self.assertIn("--resume-runtime:false", source)
        self.assertIn("$collectionRoot", source)
        self.assertIn("Network", source)
        self.assertIn("repositoryDrive", source)
        self.assertIn("$Process.Kill($true)", source)
        self.assertIn("GetDirectoryName", source)
        self.assertNotIn("Split-Path -LiteralPath", source)
        self.assertIn("BUILD-INFO.txt", source)
        self.assertIn("IsPathFullyQualified($OutputDirectory)", source)
        self.assertIn("WorkingDirectory = $repoRoot", source)

    def test_health_check_runs_from_repo_and_checks_distributions(self):
        source = (ROOT / "Check-GfEngineeringTools.ps1").read_text(encoding="utf-8")
        self.assertIn("Push-Location -LiteralPath $root", source)
        self.assertIn("opencv-python-headless", source)
        self.assertIn("importlib.metadata", source)
        self.assertIn("import cv2", source)
        self.assertIn("TryParse", source)

    def test_probe_is_excluded_from_product_project(self):
        project_path = ROOT.parent / "GerenciadorAplicativos.csproj"
        if not project_path.exists():
            self.skipTest("full repository is not mounted in this scratch workspace")
        project = ET.fromstring(project_path.read_text(encoding="utf-8"))
        removes = [node.attrib.get("Remove", "") for node in project.iter() if node.tag.endswith("Compile")]
        self.assertTrue(any("tools/**/*.cs" in value.split(";") for value in removes))


if __name__ == "__main__":
    unittest.main()
