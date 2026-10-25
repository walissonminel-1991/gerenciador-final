"""Run identical component probes against immutable #51 and this candidate.

These are installation/manifest fixtures, not physical compatibility counts.
No downloaded installer, maintenance script or real uninstaller is executed.
"""
import io
import json
import pathlib
import shutil
import subprocess
import tarfile
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = "f179dbabe78d0d42c4112a283040c5c781f109f0"


def run(args, cwd):
    p = subprocess.run(args, cwd=cwd, text=True, capture_output=True, timeout=300)
    if p.returncode:
        raise RuntimeError(" ".join(args) + "\n" + p.stdout[-10000:] + p.stderr[-5000:])
    return p.stdout


def measure(ref, destination):
    if subprocess.run(["git", "cat-file", "-e", ref + "^{commit}"], cwd=ROOT, capture_output=True).returncode:
        run(["git", "fetch", "--no-tags", "--depth=1", "origin", ref], ROOT)
    data = subprocess.check_output(["git", "archive", ref], cwd=ROOT)
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        archive.extractall(destination, filter="data")
    shutil.copytree(ROOT / "TestesResiduos/SquirrelFixture", destination / "TestesResiduos/SquirrelFixture", dirs_exist_ok=True,
                    ignore=shutil.ignore_patterns("bin", "obj"))
    project = destination / "TestesResiduos/TestesResiduos.csproj"
    source = project.read_text(encoding="utf-8-sig")
    if 'ProjectReference Include="SquirrelFixture/' not in source:
        source = source.replace('<Compile Remove="SilentFixture/**/*.cs" />', '<Compile Remove="SilentFixture/**/*.cs;SquirrelFixture/**/*.cs" />\n'
                                '<ProjectReference Include="SquirrelFixture/SquirrelFixture.csproj" ReferenceOutputAssembly="false" />')
        source = source.replace('<Copy SourceFiles="@(_SilentFixture)" DestinationFolder="$(OutDir)SyntheticSilent" />',
                                '<Copy SourceFiles="@(_SilentFixture)" DestinationFolder="$(OutDir)SyntheticSilent" />\n'
                                '<Copy SourceFiles="$(MSBuildProjectDirectory)/SquirrelFixture/bin/$(Configuration)/net10.0-windows/Update.dll" DestinationFolder="$(OutDir)SyntheticSquirrel" />')
        project.write_text(source, encoding="utf-8")
    results = {}
    for project_name, filename, cls in [("TestesAtualizador", "Work16CoverageProbe.cs", "Work16CoverageProbe"),
                                         ("TestesResiduos", "Work16SquirrelProbe.cs", "Work16SquirrelProbe")]:
        shutil.copyfile(ROOT / project_name / filename, destination / project_name / filename)
        main = destination / project_name / "Program.cs"
        lines = main.read_text(encoding="utf-8-sig").splitlines(keepends=True)
        index = 0
        while index < len(lines) and (lines[index].startswith("using ") or not lines[index].strip()):
            index += 1
        lines.insert(index, f'if(args.Contains("--work16-coverage")){{ {cls}.Run(); return; }}\n')
        main.write_text("".join(lines), encoding="utf-8")
        output = run(["dotnet", "run", "--project", f"{project_name}/{project_name}.csproj", "-c", "Release", "--", "--work16-coverage"], destination)
        for line in output.splitlines():
            if line.startswith("WORK16_") and "=" in line:
                key, raw = line.split("=", 1)
                results[key] = json.loads(raw)
    assert len(results) == 2, results
    return results


if __name__ == "__main__":
    head = run(["git", "rev-parse", "HEAD"], ROOT).strip()
    with tempfile.TemporaryDirectory(prefix="GF-W16-Measure-") as folder:
        before = measure(BASE, pathlib.Path(folder) / "before")
        after = measure(head, pathlib.Path(folder) / "after")
    b, a = before["WORK16_UPDATES"], after["WORK16_UPDATES"]
    assert b["scenarios"] == a["scenarios"] == 6
    assert b["silent_contracts"] == a["silent_contracts"] == 3
    assert b["supervised_contracts"] == 0 and a["supervised_contracts"] == 3
    assert b["managed_entries_inventoried"] == 0 and a["managed_entries_inventoried"] == 2
    assert b["pfn_version_discoveries"] == 0 and a["pfn_version_discoveries"] == 1
    assert b["tracked_package_id_lookups"] == 0 and a["tracked_package_id_lookups"] == 1
    assert b["negatives_rejected"] == a["negatives_rejected"] == 6
    assert before["WORK16_UNINSTALL"]["silent_contracts"] == 0 and after["WORK16_UNINSTALL"]["silent_contracts"] == 1
    assert before["WORK16_UNINSTALL"]["negatives_rejected"] == after["WORK16_UNINSTALL"]["negatives_rejected"] == 6
    report = {"baseline": BASE, "candidate": head, "sample_kind": "component fixtures; distinct denominators, never physical coverage",
              "before": before, "after": after, "third_party_executions": 0, "physical_homologations": 0}
    (ROOT / "Work16Coverage.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
