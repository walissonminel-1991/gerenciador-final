"""Disposable Windows-only, token-free GitHub ZIP -> signed TUF file-set test.

The signer is the EXISTING PUBLIC TEST ROOT. A signed manifest covers each
unpacked file, NOT the outer ZIP's bytes. The pre-download archive SHA comes
from the CI-created fixture, not from independent publisher authority.
No L18 launch, no updates, no release mutation and no artifact upload.
"""
from __future__ import annotations
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys

REPO = "walissonminel-1991/gerenciador-final"
ARCHIVE = "gf-e2e-new-win-x64.zip"
NAMES = (ARCHIVE, "GF-LAB-REAL-PACKAGE-MANIFEST.json",
         "targets.json", "snapshot.json", "timestamp.json")


def require(ok: bool, why: str) -> None:
    if not ok:
        raise RuntimeError("GF_GITHUB_TUF_" + why)


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        while part := handle.read(1024 * 1024):
            h.update(part)
    return h.hexdigest()


def run(argv: list[str], *, success: bool) -> str:
    result = subprocess.run(argv, capture_output=True, text=True, timeout=270,
                            check=False, encoding="utf-8", errors="replace")
    if (result.returncode == 0) != success:
        # Only test-only scripts/binaries; cap diagnostic output.
        print("GF_TUF_LAB_SUBPROCESS_UNEXPECTED " +
              "return_code=" + str(result.returncode) +
              " output_tail=" + (result.stdout + result.stderr)[-1200:],
              flush=True)
        raise RuntimeError("UNEXPECTED_TEST_RESULT")
    return result.stdout


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--old", type=Path, required=True)
    p.add_argument("--new", type=Path, required=True)
    p.add_argument("--probe", type=Path, required=True)
    a = p.parse_args()
    require(os.name == "nt" and os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO and
            os.getenv("GF_GITHUB_TUF_ONLY") == "1", "DISPOSABLE_WINDOWS_ONLY")
    runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    repo = Path(os.environ["GITHUB_WORKSPACE"]).resolve(strict=True)
    script = repo / "prototipos/autoatualizador-seguro/phase1-composed/github_draft_package.py"
    require(script.is_file() and a.probe.is_file(), "EXISTING_SOURCES_MISSING")

    def scoped(path: Path) -> Path:
        resolved = path.resolve(strict=True)
        require(resolved.is_relative_to(runner) and resolved != runner,
                "RUNNER_SCOPE")
        return resolved

    old, new, probe = scoped(a.old), scoped(a.new), scoped(a.probe)
    scratch = runner / "gf-github-tuf-byte-bridge-only"
    require(not scratch.exists(), "SCRATCH_NOT_FRESH")
    scratch.mkdir()
    prepared = scratch / "prepared"

    # Reuse existing test TUF signing/validation, with a newly built .65/.66
    # Windows package. This modifies only RUNNER_TEMP, never a GitHub Draft.
    result = run([sys.executable, str(script), "prepare",
                  "--repo", str(repo), "--old", str(old), "--new", str(new),
                  "--target", str(prepared)], success=True)
    require("GF_DRAFT_PREPARED " in result, "TEST_TUF_PREPARE_FAILED")
    upload = prepared / "upload"
    source = upload / ARCHIVE
    require(all((upload / name).is_file() for name in NAMES),
            "SIGNED_SOURCE_INCOMPLETE")

    # The identical test archive is presented under the exact official asset
    # name by the existing, socketless C# GitHub handler. The C# project links
    # the original FindReleaseAsync and DownloadAssetWithClientAsync.
    served = scratch / "GerenciadorFinal-win-x64.zip"
    received = scratch / "received-from-github-protocol.zip"
    shutil.copyfile(source, served)
    expected_zip_hash = digest(source)
    require(digest(served) == expected_zip_hash, "TEST_SOURCE_BYTES_CHANGED")
    probe_log = run([str(probe), str(served), str(received)], success=True)
    require("GF_GITHUB_TUF_DOWNLOADER_PASS " in probe_log and
            "wrong_digest=REJECTED" in probe_log, "GITHUB_CSHARP_PROOF_MISSING")
    require(digest(received) == expected_zip_hash and
            received.stat().st_size == source.stat().st_size,
            "DOWNLOADED_ZIP_BYTES_NOT_IDENTICAL")

    # TUF authenticates the manifest; the manifest authenticates the complete
    # extracted file set. The SHA equality above binds the delivered archive
    # to exactly the locally prepared ZIP. NO L18 handoff occurs.
    def make_bundle(name: str, omit: tuple[str, ...] = ()) -> Path:
        dest = scratch / name
        require(not dest.exists(), "NEGATIVE_BUNDLE_NOT_FRESH")
        dest.mkdir()
        for item in NAMES:
            if item not in omit:
                shutil.copyfile(received if item == ARCHIVE else upload / item,
                                dest / item)
        return dest

    def verify(bundle: Path, extracted_name: str,
               *, success: bool, expected_sha: str = expected_zip_hash) -> bool:
        # This preflight is a test-fixture SHA comparison, NOT a TUF claim
        # about the outer archive: TUF signs only the decompressed file set.
        if not (bundle / ARCHIVE).is_file() or digest(bundle / ARCHIVE) != expected_sha:
            require(not success, "NEGATIVE_ARCHIVE_HASH_EXPECTED")
            return False
        result = run([sys.executable, str(script), "verify",
                      "--repo", str(repo), "--old", str(old),
                      "--downloaded", str(bundle),
                      "--extracted", str(scratch / extracted_name)],
                     success=success)
        if success:
            require("GF_DRAFT_AUTHENTICATED " in result,
                    "AUTHENTICATED_FILESET_MISSING")
        return success

    verified = make_bundle("positive-signed-package")
    require(verify(verified, "positive-extracted", success=True),
            "SIGNED_FILESET_NOT_BOUND")
    extracted = scratch / "positive-extracted"
    require((extracted / "GerenciadorFinal.exe").is_file() and
            (extracted / "GerenciadorFinal.dll").is_file(),
            "VERIFIED_NEW_NOT_COMPLETE")

    # 1. Different archive: fail BEFORE TUF extraction or L18.
    changed = make_bundle("negative-different-zip")
    bad_path = changed / ARCHIVE
    with bad_path.open("r+b") as file:
        file.seek(100)
        byte = file.read(1)
        require(len(byte) == 1, "NEGATIVE_SOURCE_EMPTY")
        file.seek(100)
        file.write(bytes([byte[0] ^ 0x01]))
    require(not verify(changed, "negative-different-extracted", success=False),
            "BAD_ZIP_NOT_REJECTED")

    # 2. Manifest missing: TUF verifier must refuse despite intact ZIP.
    no_manifest = make_bundle("negative-no-manifest",
                              ("GF-LAB-REAL-PACKAGE-MANIFEST.json",))
    require(not verify(no_manifest, "negative-no-manifest-extracted", success=False),
            "MISSING_MANIFEST_NOT_REJECTED")

    # 3. No signed targets authority: ZIP digest remains correct, but the
    # existing TUF verifier must refuse before any executable handoff.
    no_targets = make_bundle("negative-no-target-authority", ("targets.json",))
    require(not verify(no_targets, "negative-no-authority-extracted",
                       success=False), "NO_AUTHORITY_NOT_REJECTED")

    # 4. Signed target metadata altered: reject the untrusted role.
    forged = make_bundle("negative-forged-targets")
    targets = forged / "targets.json"
    with targets.open("r+b") as file:
        file.seek(10)
        byte = file.read(1)
        require(len(byte) == 1, "NEGATIVE_TARGETS_EMPTY")
        file.seek(10)
        file.write(bytes([byte[0] ^ 0x01]))
    require(not verify(forged, "negative-forged-extracted", success=False),
            "BAD_SIGNED_TARGETS_NOT_REJECTED")

    # Never start OLD, NEW, L18 or a worker. Even local receipts must be absent.
    require(not any(scratch.rglob("attached.flag")) and
            not any(scratch.rglob("applied.json")) and
            not any(scratch.rglob("l18-real-active.fixture.json")),
            "UNAUTHORIZED_INSTALL_MARKER")
    print("GF_GITHUB_TUF_BINDING_PASS " +
          "same_downloaded_zip_sha=true signed_file_set=true " +
          "tuf_test_root_only=true zip_sha_in_tuf_target=false " +
          "bad_zip=REJECTED missing_manifest=REJECTED " +
          "missing_targets=REJECTED forged_targets=REJECTED " +
          "actual_github_network=false worker=false l18=false " +
          "OLD_unchanged=true READY=false", flush=True)


if __name__ == "__main__":
    main()
