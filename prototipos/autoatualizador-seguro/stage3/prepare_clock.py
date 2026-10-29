"""Build-only experiment from pinned MIT upstream; never replaces the Tuf package.

Seven clock sites and equality boundaries are patched, no cryptographic code.
All generated .cs/project/output remains outside the GF repository.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

COMMIT = "c0f5ff9747aa9b00c45a964dca275be4066cd489"


def prepare(work):
    here = Path(__file__).resolve().parent
    manifest = json.loads((here / "tuf-source-manifest.json").read_text())
    source = Path(os.environ["GF_TUF_SOURCE"]) if os.environ.get("GF_TUF_SOURCE") else work / "upstream"
    if not os.environ.get("GF_TUF_SOURCE"):
        source.mkdir()
        subprocess.run(["git", "init", "-q", str(source)], check=True)
        subprocess.run(["git", "-C", str(source), "config", "core.autocrlf", "false"], check=True)
        subprocess.run(["git", "-C", str(source), "fetch", "--depth=1", "https://github.com/mitchdenny/sigstore-dotnet.git", COMMIT], check=True)
        subprocess.run(["git", "-C", str(source), "checkout", "--detach", "FETCH_HEAD"], check=True)
    assert subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip() == COMMIT
    actual = {str(p.relative_to(source)).replace("\\", "/") for p in (source / "src/Tuf").rglob("*.cs")}
    assert actual == {p for p in manifest["files"] if p.endswith(".cs")}, "Unexpected upstream source set"
    destination = work / "clock-experiment"
    destination.mkdir()
    for name, expected in manifest["files"].items():
        data = (source / name).read_bytes()
        assert hashlib.sha256(data).hexdigest() == expected, name
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    client = destination / "src/Tuf/TufClient.cs"
    text = client.read_text(encoding="utf-8-sig")
    assert text.count("DateTimeOffset.UtcNow") == 7
    assert text.count(".Signed.Expires < DateTimeOffset.UtcNow") == 4
    assert text.count(".Signed.Expires >= DateTimeOffset.UtcNow") == 3
    text = text.replace(".Signed.Expires < DateTimeOffset.UtcNow", ".Signed.Expires <= _options.ValidationTimeProvider.GetUtcNow()")
    text = text.replace(".Signed.Expires >= DateTimeOffset.UtcNow", ".Signed.Expires > _options.ValidationTimeProvider.GetUtcNow()")
    assert "DateTimeOffset.UtcNow" not in text
    client.write_text(text, encoding="utf-8", newline="\n")
    options = destination / "src/Tuf/TufClientOptions.cs"
    text = options.read_text(encoding="utf-8-sig")
    needle = "public sealed class TufClientOptions\n{"
    assert text.count(needle) == 1
    text = text.replace(needle, needle + "\n    // GF STAGE 3 EXPERIMENT ONLY: caller supplies a frozen per-cycle clock.\n    public required TimeProvider ValidationTimeProvider { get; init; }\n")
    options.write_text(text, encoding="utf-8", newline="\n")
    project = destination / "ClockExperiment.csproj"
    project.write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
<AssemblyName>GF.Tuf.ClockExperiment</AssemblyName><IsPackable>false</IsPackable><IsPublishable>false</IsPublishable>
<EnableDefaultItems>false</EnableDefaultItems><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
<NuGetAudit>true</NuGetAudit><NuGetAuditMode>all</NuGetAuditMode><NuGetAuditLevel>low</NuGetAuditLevel>
<WarningsAsErrors>NU1900;NU1901;NU1902;NU1903;NU1904;NU1905</WarningsAsErrors>
</PropertyGroup><ItemGroup><Compile Include="src/Tuf/**/*.cs" />
<PackageReference Include="BouncyCastle.Cryptography" Version="[2.7.0]" /></ItemGroup></Project>''')
    shutil.copyfile(here / "nuget.config", destination / "nuget.config")
    lock = here / "clock-experiment.packages.lock.json"
    if lock.exists():
        shutil.copyfile(lock, destination / "packages.lock.json")
    return project, {"upstream_commit": COMMIT, "verified_files": len(manifest["files"]),
                     "clock_sites_replaced": 7, "expiry_boundaries_made_strict": 7,
                     "original_nuget_package_changed": False, "production_ready": False}
