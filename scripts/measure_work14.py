"""Compare the same bounded installation fixtures against two immutable refs.

No payload is executed. Both copies receive the exact same simulated signature
oracle, limited to a GUID fixture root. That evidence is explicitly not counted
as real Authenticode or physical homologation. Production files are never edited.
"""
import argparse
import io
import json
import pathlib
import shutil
import subprocess
import tarfile
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = "954949eef8ee84785ee6434f964b6f1e3c9e0b50"


def run(args, cwd):
    result = subprocess.run(args, cwd=cwd, text=True, capture_output=True, timeout=180)
    if result.returncode:
        raise RuntimeError("Command failed: " + " ".join(args) + "\n" + result.stdout[-8000:] + result.stderr[-4000:])
    return result.stdout


def checkout(ref, destination):
    if subprocess.run(["git", "cat-file", "-e", ref + "^{commit}"], cwd=ROOT, capture_output=True).returncode:
        run(["git", "fetch", "--no-tags", "--depth=1", "origin", ref], ROOT)
    data = subprocess.check_output(["git", "archive", ref], cwd=ROOT)
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        archive.extractall(destination, filter="data")


def measure(ref, dest):
    checkout(ref, dest)
    probes = [("TestesAtualizador", "Work14CoverageProbe.cs", "Work14CoverageProbe"),
              ("TestesResiduos", "Work14SilentCoverageProbe.cs", "Work14SilentCoverageProbe")]
    for project, filename, cls in probes:
        shutil.copyfile(ROOT / project / filename, dest / project / filename)
        main = dest / project / "Program.cs"
        source = main.read_text(encoding="utf-8-sig")
        # Keep using directives before the inserted top-level statement.
        lines = source.splitlines(keepends=True)
        index = 0
        while index < len(lines) and (lines[index].startswith("using ") or not lines[index].strip()):
            index += 1
        lines.insert(index, f'if (args.Contains("--work14-coverage")) {{ {cls}.Run(); return; }}\n')
        main.write_text("".join(lines), encoding="utf-8")
    signer = dest / "TrustedFileSignature.cs"
    source = signer.read_text(encoding="utf-8-sig")
    anchor = '        issue = "";'
    assert source.count(anchor) == 1
    source = source.replace(anchor, anchor + '''
#if GF_PROGRESSIVE
        string? fixtureRoot = Environment.GetEnvironmentVariable("GF_WORK14_FIXTURE_SIGNATURE_ROOT");
        if (fixtureRoot is not null && path.StartsWith(fixtureRoot + "\\\\", StringComparison.OrdinalIgnoreCase))
        { signer = "GF Synthetic Tests"; issue = "SIMULATED_FIXTURE_ONLY"; return true; }
#endif
''', 1)
    signer.write_text(source, encoding="utf-8")
    run(["dotnet", "build", "TestesResiduos/SilentFixture/SilentFixture.csproj", "-c", "Release", "--nologo"], dest)
    results = {}
    for project, _, _ in probes:
        output = run(["dotnet", "run", "--project", f"{project}/{project}.csproj", "-c", "Release", "--", "--work14-coverage"], dest)
        for line in output.splitlines():
            if line.startswith("WORK14_") and "=" in line:
                key, value = line.split("=", 1)
                results[key] = json.loads(value)
    assert len(results) == 3, results
    return results


def summary(rows):
    return {"denominator": len(rows), "eligible": sum(r["silent_eligible"] for r in rows),
            "legitimate_denominator": sum(r["expected_eligible"] for r in rows),
            "legitimate_eligible": sum(r["expected_eligible"] and r["silent_eligible"] for r in rows),
            "negative_denominator": sum(not r["expected_eligible"] for r in rows),
            "negative_rejected": sum(not r["expected_eligible"] and not r["silent_eligible"] for r in rows)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default=BASE)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", default="Work14Coverage.json")
    args = parser.parse_args()
    candidate = run(["git", "rev-parse", args.candidate], ROOT).strip()
    with tempfile.TemporaryDirectory(prefix="GF-W14-comparison-") as temp:
        results = {}
        for label, ref in [("before", args.base), ("after", candidate)]:
            results[label] = measure(ref, pathlib.Path(temp) / label)
            results[label]["update_summary"] = summary(results[label]["WORK14_UPDATE_MATRIX"])
            results[label]["uninstall_summary"] = summary(results[label]["WORK14_UNINSTALL_MATRIX"])
        artifact = {"schema": 1, "base": args.base, "candidate": candidate, "scope": "bounded synthetic installation scenarios; not PC inventory",
                    "signature_evidence": "same simulated oracle in both disposable copies; no real execution authorization", **results}
        after = results["after"]
        for key in ["update_summary", "uninstall_summary"]:
            assert after[key]["negative_rejected"] == after[key]["negative_denominator"], (key, after[key])
            assert after[key]["legitimate_eligible"] == after[key]["legitimate_denominator"], (key, after[key])
        assert after["update_summary"]["legitimate_eligible"] > results["before"]["update_summary"]["legitimate_eligible"]
        assert after["uninstall_summary"]["legitimate_eligible"] > results["before"]["uninstall_summary"]["legitimate_eligible"]
        pathlib.Path(args.output).write_text(json.dumps(artifact, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({label: {k: v for k, v in data.items() if k.endswith("summary") or k.endswith("POLICIES")} for label, data in results.items()}, ensure_ascii=False))


if __name__ == "__main__":
    main()
