"""Pinned source and small stage 8 patch. All generated .cs stay outside product tree."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

COMMIT = "c0f5ff9747aa9b00c45a964dca275be4066cd489"


def prepare(work):
    here = Path(__file__).resolve().parent
    source = Path(os.environ["GF_TUF_SOURCE"]) if os.environ.get("GF_TUF_SOURCE") else work / "upstream"
    if not os.environ.get("GF_TUF_SOURCE"):
        source.mkdir()
        subprocess.run(["git", "init", "-q", str(source)], check=True)
        subprocess.run(["git", "-C", str(source), "config", "core.autocrlf", "false"], check=True)
        subprocess.run(["git", "-C", str(source), "fetch", "--depth=1", "https://github.com/mitchdenny/sigstore-dotnet.git", COMMIT], check=True)
        subprocess.run(["git", "-C", str(source), "checkout", "--detach", "FETCH_HEAD"], check=True)
    assert subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip() == COMMIT
    manifest = json.loads((here.parent / "stage3/tuf-source-manifest.json").read_text())
    assert {p.relative_to(source).as_posix() for p in (source / "src/Tuf").rglob("*.cs")} == {p for p in manifest["files"] if p.endswith(".cs")}
    target = work / "tuf-source"
    target.mkdir()
    for name, expected in manifest["files"].items():
        data = (source / name).read_bytes()
        assert hashlib.sha256(data).hexdigest() == expected, name
        out = target / name
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(data)
    patch = work / "tuf-delta.patch"
    patch.write_bytes((here / "tuf-delta.patch").read_text(encoding="utf-8").encode("utf-8"))
    subprocess.run(["git", "apply", "--check", str(patch)], cwd=target, check=True)
    subprocess.run(["git", "apply", str(patch)], cwd=target, check=True)
    changed = {name: hashlib.sha256((target / name).read_bytes()).hexdigest() for name in manifest["files"]
               if hashlib.sha256((target / name).read_bytes()).hexdigest() != manifest["files"][name]}
    assert set(changed) == {"src/Tuf/TufClient.cs", "src/Tuf/TufClientOptions.cs", "src/Tuf/TufResults.cs"}
    return target, {"upstream_commit": COMMIT, "verified_files": len(manifest["files"]),
                    "patch_canonical_lf_sha256": hashlib.sha256(patch.read_bytes()).hexdigest(), "modified_source_sha256": changed,
                    "crypto_parser_sources_unchanged": True, "original_nuget_package_changed": False, "production_adoption": False}
