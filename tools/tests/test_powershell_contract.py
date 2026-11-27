import pathlib
import unittest


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


if __name__ == "__main__":
    unittest.main()
