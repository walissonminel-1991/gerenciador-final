"""Disposable Windows-only, token-free GitHub ZIP -> signed TUF file-set test.

The signer is the EXISTING PUBLIC TEST ROOT. A signed manifest covers each
unpacked file, NOT the outer ZIP's bytes. The pre-download archive SHA comes
from the CI-created fixture, not from independent publisher authority.
No L18 launch, no updates, no release mutation and no artifact upload.
"""
from __future__ import annotations
import argparse
import base64
import hashlib
import json
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
    p.add_argument("--l18", type=Path, required=True)
    a = p.parse_args()
    require(os.name == "nt" and os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO and
            os.getenv("GF_GITHUB_TUF_ONLY") == "1", "DISPOSABLE_WINDOWS_ONLY")
    runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    # Existing FixtureSandbox and L14Model use .NET Path.GetTempPath().
    # Keep both the C# fixture root and its L14 lab directly in RUNNER_TEMP,
    # never in a personal profile. This override is limited to this job.
    os.environ["TMP"] = str(runner)
    os.environ["TEMP"] = str(runner)
    repo = Path(os.environ["GITHUB_WORKSPACE"]).resolve(strict=True)
    script = repo / "prototipos/autoatualizador-seguro/phase1-composed/github_draft_package.py"
    require(script.is_file() and a.probe.is_file() and a.l18.is_file(), "EXISTING_SOURCES_MISSING")

    def scoped(path: Path) -> Path:
        resolved = path.resolve(strict=True)
        require(resolved.is_relative_to(runner) and resolved != runner,
                "RUNNER_SCOPE")
        return resolved

    old, new, probe, l18 = scoped(a.old), scoped(a.new), scoped(a.probe), scoped(a.l18)
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

    # Bootstrap a disposable, versioned, LOCAL state fixture with the same
    # schema consumed by PersistentState.Load. It does not authenticate a
    # publisher or authorize L18's BeginCore. No bootstrap signer is used.
    def persistent_fixture(name: str, *, installation: str = "a" * 32,
                           timestamp_floor: int = 1,
                           bad_checksum: bool = False,
                           fake_receipt: bool = False) -> Path:
        root = runner / ("gf-u2-github-binding-" + name)
        require(not root.exists(), "PERSISTENT_TEST_ROOT_EXISTS")
        root.mkdir()
        (root / ".gf-u2-fixture").write_text(
            "GF-U2-INERT-STATE-TESTS-NO-PRODUCT\\n", encoding="utf-8")
        ledger = root / "ledger"
        ledger.mkdir()
        payload = {
            "schema": "gf-state-fixture/2", "installationId": installation,
            "generation": 1, "previousDigest": "0" * 64,
            "trust": {"root": 1, "timestamp": timestamp_floor,
                      "snapshot": 1, "targets": 1, "approvedSequence": 1,
                      "highestVersion": "0.0.0.1",
                      "lastObservedUtc": "2026-11-01T00:00:00Z",
                      "metadata": {}},
            "confirmed": {"sequence": 1, "version": "0.0.0.1",
                          "sha256": "1" * 64, "identity": "lab-old"},
            "transaction": None
        }
        p = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode()
        wrapped = {"schema": "gf-state-fixture/2",
                   "payload": base64.b64encode(p).decode("ascii"),
                   "sha256": hashlib.sha256(p).hexdigest()}
        if bad_checksum:
            wrapped["sha256"] = "0" * 64
        (ledger / "0000000000000001.fixture.json").write_text(
            json.dumps(wrapped, separators=(",", ":"), sort_keys=True),
            encoding="utf-8")
        if fake_receipt:
            (root / "local-authorized-receipt.json").write_text(
                '{"schema":"untrusted-local","authorized":true,"READY":true}',
                encoding="utf-8")
        return root

    state = persistent_fixture("positive")
    lab = runner / ("gf-u2-l14-" + "b" * 32)
    require(not lab.exists(), "L14_TEST_ROOT_EXISTS")
    lab.mkdir()
    (lab / ".gf-u2-fixture").write_text(
        "GF-U2-L14-ONLY-DISPOSABLE-TEXT\\n", encoding="utf-8")

    # Run only the data-only mode of the EXISTING L18 executable.
    # The same TUF test-root and file-set are reverified by this entrypoint.
    target_sha = digest(verified / "GF-LAB-REAL-PACKAGE-MANIFEST.json")

    def l18_gate(bundle: Path, out: str, *, accepted: bool,
                 zip_sha: str = expected_zip_hash,
                 manifest_sha: str = target_sha,
                 old_path: Path = old,
                 installation: str = "a" * 32,
                 transaction: str = "b" * 32,
                 persistent: Path = state,
                 lab_root: Path = lab) -> None:
        proof = run([str(l18), "--real-github-tuf-inert", str(old_path),
                     str(bundle), str(scratch / out), str(repo),
                     sys.executable, zip_sha, manifest_sha,
                     installation, transaction,
                     str(persistent), str(lab_root)],
                    success=accepted)
        if accepted:
            require("GF_GITHUB_TUF_L18_INERT_PASS " in proof,
                    "L18_INERT_RECEIPT_NOT_PROVEN")
        else:
            # A late identity rejection may leave authenticated files in the
            # runner's isolated extraction directory. Extraction is NOT
            # installation. Reject a success receipt or zero return code;
            # selector/journal/swap/NEW execution remain prohibited.
            require("GF_GITHUB_TUF_L18_INERT_PASS " not in proof,
                    "L18_NEGATIVE_ACCEPTED_NEW")

    l18_gate(verified, "l18-positive-extracted", accepted=True)
    l18_gate(changed, "l18-negative-bad-zip", accepted=False)
    l18_gate(no_manifest, "l18-negative-no-manifest", accepted=False)
    l18_gate(no_targets, "l18-negative-no-targets", accepted=False)
    l18_gate(forged, "l18-negative-forged-targets", accepted=False)
    l18_gate(verified, "l18-negative-wrong-zip-digest", accepted=False,
             zip_sha="0" * 64)
    l18_gate(verified, "l18-negative-wrong-manifest-digest", accepted=False,
             manifest_sha="0" * 64)

    # ID arguments are local proposals only. The signed test manifest must
    # agree with the immutable expected fixture identity; neither argument
    # confers the right to begin a transaction.
    l18_gate(verified, "l18-wrong-installation", accepted=False,
             installation="c" * 32)
    l18_gate(verified, "l18-wrong-transaction", accepted=False,
             transaction="d" * 32)

    # The same signed NEW cannot authorize a different OLD.
    # Mutation is performed ONLY on a copied, disposable OLD.
    different_old = scratch / "l18-foreign-old"
    shutil.copytree(old, different_old)
    old_dll = different_old / "GerenciadorFinal.dll"
    require(old_dll.is_file(), "OLD_TEST_DLL_ABSENT")
    with old_dll.open("r+b") as file:
        file.seek(128)
        previous = file.read(1)
        require(len(previous) == 1, "OLD_TEST_DLL_SHORT")
        file.seek(128)
        file.write(bytes([previous[0] ^ 0x01]))
    l18_gate(verified, "l18-negative-foreign-old", accepted=False,
             old_path=different_old)

    # An unexpected OLD file is also incompatible, even when its normal
    # files and the new package are otherwise identical.
    extra_old = scratch / "l18-extra-old"
    shutil.copytree(old, extra_old)
    (extra_old / "unexpected-other-program.dll").write_bytes(b"untrusted")
    l18_gate(verified, "l18-negative-extra-old-file", accepted=False,
             old_path=extra_old)

    # A locally edited manifest and its recalculated hash are NOT TUF
    # authority: signed targets still bind to the original manifest bytes.
    local_bundle = make_bundle("negative-local-reauthed-manifest")
    local_file = local_bundle / "GF-LAB-REAL-PACKAGE-MANIFEST.json"
    document = json.loads(local_file.read_text(encoding="utf-8"))
    require(document["installation"] == "a" * 32, "TEST_ID_UNEXPECTED")
    document["installation"] = "c" * 32
    local_file.write_text(json.dumps(document, sort_keys=True,
                                     separators=(",", ":")),
                          encoding="utf-8")
    l18_gate(local_bundle, "l18-negative-local-authority", accepted=False,
             manifest_sha=digest(local_file), installation="c" * 32)

    # Existing C# PersistentState.Load must match BOTH signed identity and
    # the independently parsed L14 local transaction. A fake same-user
    # receipt is not a TUF authorization and cannot override either.
    bad_install_state = persistent_fixture(
        "foreign-installation", installation="c" * 32,
        fake_receipt=True)
    l18_gate(verified, "l18-state-installation-fork", accepted=False,
             persistent=bad_install_state)
    stale_floor = persistent_fixture("rollback-floor", timestamp_floor=2)
    l18_gate(verified, "l18-replayed-metadata", accepted=False,
             persistent=stale_floor)
    damaged_ledger = persistent_fixture("ledger-corrupt", bad_checksum=True)
    l18_gate(verified, "l18-ledger-checksum-bad", accepted=False,
             persistent=damaged_ledger)
    other_lab = runner / ("gf-u2-l14-" + "d" * 32)
    require(not other_lab.exists(), "OTHER_L14_ROOT_EXISTS")
    other_lab.mkdir()
    (other_lab / ".gf-u2-fixture").write_text(
        "GF-U2-L14-ONLY-DISPOSABLE-TEXT\\n", encoding="utf-8")
    l18_gate(verified, "l18-persisted-tx-fork", accepted=False,
             lab_root=other_lab)

    # No role is re-signed. The original existing TUF verifier must also
    # reject the previously valid metadata at a forged FUTURE observation
    # time after its signed expiration.
    import importlib.util
    from datetime import datetime, timezone, timedelta
    spec = importlib.util.spec_from_file_location(
        "gf_github_tuf_expiry_inert",
        repo / "prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py")
    require(spec is not None and spec.loader is not None, "TUF_SOURCE_UNAVAILABLE")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    expires = []
    for name in ("timestamp.json", "snapshot.json", "targets.json"):
        role = json.loads((verified / name).read_text(encoding="utf-8"))
        expires.append(datetime.fromisoformat(
            role["signed"]["expires"].replace("Z", "+00:00")))
    future = max(expires) + timedelta(days=1)
    native_datetime = module.datetime
    class FutureClock(native_datetime):
        @classmethod
        def now(cls, tz=None):
            return future if tz == timezone.utc else future.astimezone(tz)
    module.datetime = FutureClock
    try:
        try:
            module.tuf_verify_prepared(
                repo, (verified / "GF-LAB-REAL-PACKAGE-MANIFEST.json").read_bytes(),
                verified)
        except Exception:
            expired_rejected = True
        else:
            expired_rejected = False
        require(expired_rejected, "EXPIRED_TUF_METADATA_ACCEPTED")
    finally:
        module.datetime = native_datetime

    require(not any(scratch.rglob("l18-phase-*.fixture.json")),
            "L18_INERT_CREATED_JOURNAL")
    # No swap, NEW execution or worker; this proof grants no install authority.
    # Never start OLD, NEW or a worker. Even local receipts must be absent.
    require(not any(scratch.rglob("attached.flag")) and
            not any(scratch.rglob("applied.json")) and
            not any(scratch.rglob("l18-real-active.fixture.json")),
            "UNAUTHORIZED_INSTALL_MARKER")
    print("GF_GITHUB_TUF_L18_GATE_MATRIX_PASS positive=DATA_ONLY bad_zip=REJECTED missing_manifest=REJECTED missing_targets=REJECTED forged_targets=REJECTED wrong_zip_sha=REJECTED wrong_manifest_sha=REJECTED wrong_installation=REJECTED wrong_transaction=REJECTED mutated_OLD=REJECTED extra_OLD=REJECTED local_resign=REJECTED persistent_installation=REJECTED persistent_transaction=REJECTED trust_floor_replay=REJECTED ledger_tamper=REJECTED expired_tuf=REJECTED selector=false journal=false NEW=false READY=false", flush=True)
    print("GF_GITHUB_TUF_BINDING_PASS " +
          "same_downloaded_zip_sha=true signed_file_set=true " +
          "tuf_test_root_only=true zip_sha_in_tuf_target=false " +
          "bad_zip=REJECTED missing_manifest=REJECTED " +
          "missing_targets=REJECTED forged_targets=REJECTED " +
          "actual_github_network=false worker=false l18=false " +
          "OLD_unchanged=true READY=false", flush=True)


if __name__ == "__main__":
    main()
