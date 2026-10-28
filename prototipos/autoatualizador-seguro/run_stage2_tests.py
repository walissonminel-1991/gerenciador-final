"""Stage 2 inert persistence/transaction/NTFS tests; no GF build or execution."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def main(report):
    project = Path(__file__).resolve().parent
    repository = project.parent.parent
    if (project / "obj").exists() or (project / "bin").exists():
        raise SystemExit("In-repository generated prototype outputs are forbidden.")
    dotnet = os.environ.get("GF_DOTNET") or shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("SDK .NET 10.0.402 required in disposable development environment.")
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1"}
    version = subprocess.check_output([dotnet, "--version"], cwd=project, env=env, text=True).strip()
    if version != "10.0.402":
        raise SystemExit("Unexpected SDK: " + version)
    with tempfile.TemporaryDirectory(prefix="gf-u2-runner-") as temporary:
        work = Path(temporary).resolve()
        if work.is_relative_to(repository):
            raise SystemExit("All generated output must be outside the repository.")
        subprocess.run([sys.executable, str(project / "generate_fixtures.py"), str(work / "fixtures")],
                       check=True, cwd=project, env=env)
        common = [str(project / "Prototype.csproj"), "-p:ArtifactsPath=" + str(work / "artifacts")]
        subprocess.run([dotnet, "restore", *common, "--locked-mode", "--nologo"], check=True, cwd=project, env=env)
        subprocess.run([dotnet, "build", *common, "-c", "Release", "--no-restore", "--nologo"], check=True, cwd=project, env=env)
        assemblies = list((work / "artifacts" / "bin").rglob("GF.Updater.IsolatedSecurityTests.dll"))
        if len(assemblies) != 1:
            raise SystemExit("Ambiguous isolated test assembly.")
        report = report.resolve()
        report.parent.mkdir(parents=True, exist_ok=True)
        # Save the report even on test failure; propagate exit status after isolation checks.
        result = subprocess.run([dotnet, str(assemblies[0]), "--state-self-test", str(work / "fixtures"), str(report)],
                                cwd=project, env=env)
        evaluation = json.loads(subprocess.check_output(
            [dotnet, "msbuild", "GerenciadorAplicativos.csproj", "-getItem:Compile"], cwd=repository, env=env, text=True))
        identities = [item["Identity"].replace("\\", "/") for item in evaluation["Items"]["Compile"]]
        if any("prototipos/autoatualizador-seguro/" in item for item in identities):
            raise SystemExit("Prototype entered the product Compile items.")
        saved = json.loads(report.read_text(encoding="utf-8"))
        saved["isolation"] = {"product_compile_items": len(identities), "prototype_compile_items": 0,
                              "outputs_outside_repository": True, "sdk": version,
                              "restore_locked_mode": True, "product_built": False}
        report.write_text(json.dumps(saved, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("GF_U2_FINAL_REPORT " + json.dumps(saved, ensure_ascii=True, separators=(",", ":")))
        if result.returncode:
            raise SystemExit(result.returncode)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    main(parser.parse_args().report)
