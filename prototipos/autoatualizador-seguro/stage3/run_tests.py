"""Stage 3 isolated experiments; never builds/executes/packages Gerenciador Final."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timedelta, timezone
sys.dont_write_bytecode = True
from prepare_clock import prepare


def clock_fixtures(project, output):
    spec = importlib.util.spec_from_file_location("gf_public_fixtures", project.parent / "generate_fixtures.py")
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    reference = datetime.now(timezone.utc).replace(microsecond=0)
    for short_role in ["root", "timestamp", "snapshot", "targets"]:
        folder = output / (short_role + "-soon")
        folder.mkdir()
        files = {p.name: p.read_bytes() for p in (output / "valid").iterdir()}
        roles = {"root": json.loads(files["2.root.json"])["signed"],
                 **{r: json.loads(files[r + ".json"])["signed"] for r in ["targets", "snapshot", "timestamp"]}}
        for role, signed in roles.items():
            signed["expires"] = generator.utc(reference + timedelta(seconds=5) if role == short_role else reference + timedelta(hours=48))
        root = generator.wire(generator.envelope(roles["root"], ["r1", "r2", "r4", "r5"]))
        targets = generator.wire(generator.envelope(roles["targets"], ["targets"]))
        roles["snapshot"]["meta"]["targets.json"].update(length=len(targets), hashes={"sha256": generator.digest(targets)})
        snapshot = generator.wire(generator.envelope(roles["snapshot"], ["snapshot"]))
        roles["timestamp"]["meta"]["snapshot.json"].update(length=len(snapshot), hashes={"sha256": generator.digest(snapshot)})
        timestamp = generator.wire(generator.envelope(roles["timestamp"], ["timestamp"]))
        files.update({"2.root.json": root, "targets.json": targets, "snapshot.json": snapshot, "timestamp.json": timestamp})
        for name, data in files.items():
            (folder / name).write_bytes(data)
    (output / "stage3-clock.json").write_text(json.dumps({"reference": generator.utc(reference)}))


def main(args):
    project = Path(__file__).resolve().parent
    repository = project.parents[2]
    dotnet = os.environ.get("GF_DOTNET") or shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("SDK 10.0.402 required in disposable development environment.")
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "PYTHONUTF8": "1"}
    env["DOTNET_ROOT"] = str(Path(dotnet).resolve().parent)
    sdk = subprocess.check_output([dotnet, "--version"], cwd=project, env=env, text=True).strip()
    if sdk != "10.0.402":
        raise SystemExit("Unexpected SDK: " + sdk)
    if any(p.name in {"obj", "bin"} and p.is_dir() for p in project.parent.rglob("*")):
        raise SystemExit("Generated output inside the prototype tree is forbidden.")
    with tempfile.TemporaryDirectory(prefix="gf-u3-runner-") as directory:
        work = Path(directory).resolve()
        assert not work.is_relative_to(repository)
        clock, provenance = prepare(work)
        subprocess.run([sys.executable, str(project.parent / "generate_fixtures.py"), str(work / "fixtures")], check=True, env=env)
        clock_fixtures(project, work / "fixtures")
        common = ["-p:ArtifactsPath=" + str(work / "artifacts"), "-p:ClockExperimentProject=" + str(clock)]
        locked = [] if args.write_locks else ["--locked-mode"]
        subprocess.run([dotnet, "restore", str(project / "Stage3.csproj"), *common, *locked, "--nologo"], check=True, env=env, cwd=project)
        if args.write_locks:
            shutil.copyfile(clock.parent / "packages.lock.json", project / "clock-experiment.packages.lock.json")
        subprocess.run([dotnet, "build", str(project / "Stage3.csproj"), *common, "-c", "Release", "--no-restore", "--nologo"], check=True, env=env, cwd=project)
        probe = "-"
        if sys.platform == "win32":
            probe = work / "probe"
            subprocess.run([dotnet, "restore", str(project / "Probe.csproj"), *common, *locked, "--nologo"], check=True, env=env, cwd=project)
            subprocess.run([dotnet, "publish", str(project / "Probe.csproj"), *common, "-c", "Release", "--no-restore", "-p:DebugType=None", "-p:DebugSymbols=false", "-o", str(probe)], check=True, env=env, cwd=project)
            cache_line = subprocess.check_output([dotnet, "nuget", "locals", "global-packages", "--list"], env=env, text=True).strip()
            cache = Path(cache_line.split(": ", 1)[1])
            for package, label in [("microsoft.netcore.app.runtime.win-x64", "DotNET"), ("microsoft.windowsdesktop.app.runtime.win-x64", "WindowsDesktop")]:
                package_root = cache / package / "10.0.13"
                candidates = {p.name.upper(): p for p in package_root.iterdir() if p.is_file()}
                documents = [(["LICENSE.TXT", "LICENSE"], "LICENSE.txt")]
                if label == "DotNET":
                    documents.append((["THIRD-PARTY-NOTICES.TXT", "THIRD-PARTY-NOTICES"], "THIRD-PARTY-NOTICES.txt"))
                else:
                    # Official WindowsDesktop 10.0.13 ships LICENSE + MIT nuspec, no separate notices file.
                    documents.append((["MICROSOFT.WINDOWSDESKTOP.APP.RUNTIME.WIN-X64.NUSPEC"], "PACKAGE-METADATA.nuspec"))
                for source_names, target in documents:
                    chosen = next((candidates[n] for n in source_names if n in candidates), None)
                    if chosen is None:
                        raise SystemExit("Missing real SDK package license: " + package + " " + target)
                    destination = probe / "Licenses" / label / target
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(chosen, destination)
            recipe = {p.relative_to(probe).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in probe.rglob("*") if p.is_file()}
            (work / "probe-recipe.json").write_text(json.dumps(recipe))
        assemblies = list((work / "artifacts" / "bin").rglob("GF.Updater.Stage3.Tests.dll"))
        assert len(assemblies) == 1
        executable = assemblies[0].with_suffix(".exe" if sys.platform == "win32" else "")
        report = args.report.resolve()
        report.parent.mkdir(parents=True, exist_ok=True)
        result = subprocess.run([str(executable), str(work / "fixtures"), str(probe), str(work / "probe-recipe.json"), str(report)], env=env, cwd=project)
        evaluation = json.loads(subprocess.check_output([dotnet, "msbuild", "GerenciadorAplicativos.csproj", "-getItem:Compile"], cwd=repository, env=env, text=True))
        identities = [p["Identity"].replace("\\", "/") for p in evaluation["Items"]["Compile"]]
        assert not any("prototipos/autoatualizador-seguro/" in name for name in identities)
        saved = json.loads(report.read_text(encoding="utf-8"))
        saved["clock_experiment_provenance"] = provenance
        saved["isolation"] = {"sdk": sdk, "restore_locked_mode": not args.write_locks, "product_compile_items": len(identities),
                              "prototype_compile_items": 0, "outputs_outside_repository": True, "product_built": False}
        report.write_text(json.dumps(saved, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("GF_U3_FINAL_REPORT " + json.dumps(saved, ensure_ascii=True, separators=(",", ":")))
        if result.returncode:
            raise SystemExit(result.returncode)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--write-locks", action="store_true", help="Development-only initial lock generation; final validation must use locked mode.")
    main(parser.parse_args())
