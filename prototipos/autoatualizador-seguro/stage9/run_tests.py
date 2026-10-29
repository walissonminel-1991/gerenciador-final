"""Only stage 9 integration deltas; no old suite or GF product build/execution/publication."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
sys.dont_write_bytecode = True
from make_fixtures import generate
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "stage8"))
from prepare_source import prepare
sys.path.pop(0)


def main(args):
    here = Path(__file__).resolve().parent
    repo = here.parents[2]
    dotnet = os.environ.get("GF_DOTNET") or shutil.which("dotnet")
    if not dotnet: raise SystemExit("Disposable SDK 10.0.402 required")
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "PYTHONUTF8": "1"}
    env["DOTNET_ROOT"] = str(Path(dotnet).resolve().parent)
    sdk = subprocess.check_output([dotnet, "--version"], cwd=here, env=env, text=True).strip()
    assert sdk == "10.0.402", sdk
    assert not any(p.is_dir() and p.name in {"bin", "obj"} for p in here.parent.rglob("*"))
    with tempfile.TemporaryDirectory(prefix="gf-u9-controller-") as directory:
        work = Path(directory).resolve()
        assert not work.is_relative_to(repo)
        source, provenance = prepare(work)
        fixtures = generate(work / "fixtures")
        common = ["-p:ArtifactsPath=" + str(work / "artifacts"), "-p:Stage8Source=" + str(source)]
        subprocess.run([dotnet, "restore", str(here / "Stage9.csproj"), *common, "--locked-mode", "--nologo"], check=True, env=env, cwd=here)
        subprocess.run([dotnet, "build", str(here / "Stage9.csproj"), *common, "-c", "Release", "--no-restore", "--nologo"], check=True, env=env, cwd=here)
        assemblies = list((work / "artifacts/bin").rglob("GF.Updater.Stage9.Tests.dll"))
        assert len(assemblies) == 1
        report = args.report.resolve(); report.parent.mkdir(parents=True, exist_ok=True)
        result = subprocess.run([dotnet, str(assemblies[0]), str(work / "fixtures"), str(report), args.prefix], env=env, cwd=here)
        saved = json.loads(report.read_text(encoding="utf-8"))
        evaluated = json.loads(subprocess.check_output([dotnet, "msbuild", "GerenciadorAplicativos.csproj", "-getItem:Compile"], cwd=repo, env=env, text=True))
        identities = [p["Identity"].replace("\\", "/") for p in evaluated["Items"]["Compile"]]
        assert not any("prototipos/autoatualizador-seguro/" in p for p in identities)
        shared = ["src/StrictMetadata.cs.txt", "src/ZipInspection.cs.txt", "src/PersistentState.cs.txt", "src/TransactionPrototype.cs.txt", "src/WindowsFileGuard.cs.txt", "src/CycleClock.cs.txt", "stage8/src/OwnedAttempt.cs.txt", "stage8/src/TufAttempt.cs.txt", "stage8/tuf-delta.patch", "stage8/prepare_source.py"]
        saved["reused_source_sha256_normalized_LF"] = {p: hashlib.sha256((here.parent / p).read_text().encode()).hexdigest() for p in shared}
        saved["isolation"] = {"sdk": sdk, "locked_restore": True, "product_compile_items": len(identities), "prototype_items_in_product": 0,
                              "outputs_outside_repository": True, "product_built": False, "previous_workflows_modified": False}
        saved["tuf_source_delta"] = provenance; saved["fixtures"] = fixtures
        report.write_text(json.dumps(saved, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("GF_U9_FINAL_REPORT " + json.dumps(saved, ensure_ascii=True, separators=(",", ":")))
        if result.returncode or saved["failed"]: raise SystemExit(result.returncode or 1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--prefix", default="")
    main(parser.parse_args())
