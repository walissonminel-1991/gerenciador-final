#!/usr/bin/env python3
"""Read-only, narrow source contract for real GF delegated effect routes.

This gate does NOT claim exhaustive discovery, effect quiescence, a complete
process tree, provider trust, or operational READY. It connects selected
product call sites to a conservative Windows lab regression gate.
"""
import json
import os
from pathlib import Path

SCHEMA = "gf-phase1-delegated-effect-surfaces/1"
RULES = (
    ("native-child-process", "Program.cs", "UNBOUNDED",
     ("public static async Task<CommandResult> RunCommandAsync",
      "if (!process.Start())", "await process.WaitForExitAsync",
      "process.Kill(entireProcessTree: true)")),
    ("elevated-worker-shell", "Program.cs", "UNBOUNDED",
     ('Verb = "runas"', 'start.ArgumentList.Add("--worker")',
      "PipePeerVerifier.IsExpectedClient")),
    ("vendor-uninstaller-shell", "RegisteredUninstaller.cs", "UNBOUNDED",
     ("process=Process.Start(start);", "UseShellExecute=true",
      "WaitForExitAsync")),
    ("vendor-installer-msi-shell", "VerifiedUpdaters.cs", "UNBOUNDED",
     ("Process.Start(new ProcessStartInfo", '"msiexec.exe"',
      "UseShellExecute = true", "WaitForInstallerExitAsync")),
    ("msix-deployment-broker", "WindowsTerminalUpdater.cs", "UNBOUNDED",
     ("new PackageManager().AddPackageAsync",
      "operation.Completed", "Deployment.RunAsync")),
    ("windows-update-agent", "Program.cs", "UNBOUNDED",
     ("WindowsUpdateClient.InstallAsync(updateIds, log)",
      'task.StartsWith("wu-install:"', "TryParseUpdateTask")),
    ("system-restore-service", "RestorePointService.cs", "UNBOUNDED",
     ('EntryPoint = "SRSetRestorePointW"',
      "private static extern bool SRSetRestorePoint(",
      "public static int Request(Action<string> log)")),
    ("winget-local-catalog-com", "WinGetComReader.cs", "READ_ONLY_SCOPE",
     ("new Microsoft.Management.Deployment.PackageManager()",
      "GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages)",
      "InstalledPackageInformationOnly = true")),
    ("beta-telemetry-network", "BetaTelemetryUploader.cs", "OUT_OF_SCOPE_REMOTE",
     ("new HttpClient(handler)", "HttpClientHandler", "AllowAutoRedirect = false")),
)

def require(condition, code):
    if not condition:
        raise RuntimeError("GF_PHASE1_SURFACE_GATE_" + code)

def main():
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true"
            and os.environ.get("RUNNER_TEMP"), "DISPOSABLE_WINDOWS_ONLY")
    root = Path(__file__).resolve().parents[3]
    require((root / "GerenciadorAplicativos.csproj").is_file(),
            "REPOSITORY_ROOT")
    rows = []
    for name, file_name, scope, anchors in RULES:
        path = root / file_name
        require(path.is_file() and not path.is_symlink(),
                "SOURCE_MISSING_" + name)
        source = path.read_text(encoding="utf-8-sig")
        for anchor in anchors:
            require(anchor in source, "SOURCE_CHANGED_" + name)
        rows.append({"name": name, "source_file": file_name,
                     "scope": scope, "required_source_anchors": len(anchors)})
    recovery = (root / "prototipos/autoatualizador-seguro/phase1-composed/"
                "Phase1Recovery.cs.txt").read_text(encoding="utf-8-sig")
    require('Require(false,"REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY");'
            in recovery, "NO_LAUNCH_FAIL_CLOSED_REMOVED")
    require(sum(r["scope"] == "UNBOUNDED" for r in rows) == 7,
            "UNBOUNDED_ROUTE_COUNT")
    report = {
        "schema": SCHEMA,
        "scope": "SELECTED_REAL_PRODUCT_SOURCE_ANCHORS_ONLY",
        "status": "SOURCE_ANCHORS_REVIEWED_NOT_EFFECTS_BOUNDED",
        "source_routes": rows,
        "unbounded_delegated_effect_routes": 7,
        "new_windows_operations_executed": False,
        "quiescence_proven": False,
        "job_empty_proves_all_effects_complete": False,
        "provider_exit_proves_all_effects_complete": False,
        "operational_ready": False,
        "fail_closed_barrier_present": True,
        "complete_call_graph_audited": False,
        "out_of_scope_brokers_may_exist": True,
        "production_authority": False
    }
    print("GF_PHASE1_DELEGATED_EFFECT_SURFACE_REPORT " +
          json.dumps(report, sort_keys=True, separators=(",", ":")))

if __name__ == "__main__":
    main()
