"""Run isolated inert fixtures only; all build outputs stay outside the repository."""
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
        raise SystemExit("Stale in-repository prototype obj/bin: remove only those generated test directories before running.")
    dotnet = os.environ.get("GF_DOTNET") or shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("SDK .NET 10.0.402 is required in the disposable development environment.")
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1"}
    version = subprocess.check_output([dotnet, "--version"], cwd=project, env=env, text=True).strip()
    if version != "10.0.402":
        raise SystemExit("Unexpected SDK: " + version)
    with tempfile.TemporaryDirectory(prefix="gf-u1-fixtures-") as temporary:
        work = Path(temporary).resolve()
        if work.is_relative_to(repository):
            raise SystemExit("Build/test workspace must be outside the repository.")
        fixtures = work / "fixtures"
        artifacts = work / "artifacts"
        subprocess.run([sys.executable, str(project / "generate_fixtures.py"), str(fixtures)], check=True, cwd=project, env=env)
        common = [str(project / "Prototype.csproj"), "-p:ArtifactsPath=" + str(artifacts)]
        subprocess.run([dotnet, "restore", *common, "--locked-mode", "--nologo"], check=True, cwd=project, env=env)
        subprocess.run([dotnet, "build", *common, "-c", "Release", "--no-restore", "--nologo"], check=True, cwd=project, env=env)
        assemblies = list((artifacts / "bin").rglob("GF.Updater.IsolatedSecurityTests.dll"))
        if len(assemblies) != 1:
            raise SystemExit("Ambiguous fixture test assembly.")
        report = report.resolve()
        report.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([dotnet, str(assemblies[0]), "--self-test", str(fixtures), str(report)], check=True, cwd=project, env=env)
        evaluation = json.loads(subprocess.check_output(
            [dotnet, "msbuild", "GerenciadorAplicativos.csproj", "-getItem:Compile"], cwd=repository, env=env, text=True))
        identities = [item["Identity"].replace("\\", "/") for item in evaluation["Items"]["Compile"]]
        if any("prototipos/autoatualizador-seguro/" in item for item in identities):
            raise SystemExit("Prototype entered product Compile items.")
        result = json.loads(report.read_text())
        result["isolation"] = {"product_compile_items": len(identities), "prototype_compile_items": 0,
                               "outputs_outside_repository": True, "sdk": version,
                               "restore_locked_mode": True, "product_built": False}
        report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
        print("Verified isolated prototype; GF product was evaluated only, never built or executed.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    main(parser.parse_args().report)
