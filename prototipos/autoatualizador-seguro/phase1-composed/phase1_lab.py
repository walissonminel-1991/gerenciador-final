"""GF Phase 1: REAL GF package bytes; DISPOSABLE Windows runner ONLY.

Do NOT import as a production updater. The signer uses deterministic public
Stage11 test seeds and the bootstrap root distributed in this repository.
No operational trust root, product integration, keys, service or READY here.
"""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

from tuf.api.metadata import Metadata
from securesystemslib.signer import CryptoSigner, SSlibKey


def require(condition, why):
    if not condition:
        raise RuntimeError("GF_PHASE1_" + why)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_hash(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def checked_files(root):
    root = root.resolve(strict=True)
    require(root.is_dir() and not root.is_symlink(), "NOT_A_DIRECTORY")
    seen = set()
    files = []
    for directory, subdirs, names in os.walk(root, followlinks=False):
        for name in subdirs + names:
            path = Path(directory) / name
            require(not path.is_symlink(), "LINK_REJECTED")
            require((path.stat().st_file_attributes & 0x400) == 0, "REPARSE_REJECTED")
        for name in names:
            f = Path(directory) / name
            relative = f.relative_to(root).as_posix()
            require(".." not in relative.split("/") and ":" not in relative, "UNSAFE_RELATIVE")
            norm = relative.lower()
            require(norm not in seen, "CASE_COLLISION")
            seen.add(norm)
            require(f.is_file(), "NONREGULAR_FILE")
            files.append((relative, f))
    return sorted(files)


def manifest_for(directory):
    files = checked_files(directory)
    require(any(name == "GerenciadorFinal.exe" for name, _ in files), "GF_EXE_MISSING")
    require(any(name == "GerenciadorFinal.dll" for name, _ in files), "GF_DLL_MISSING")
    require(any(name.startswith("coreclr.") or name == "hostfxr.dll" for name, _ in files),
            "RUNTIME_NOT_SELF_CONTAINED")
    require(len(files) > 40, "PACKAGE_INCOMPLETE")
    return {name: {"sha256": file_hash(path), "length": path.stat().st_size}
            for name, path in files}


def canonical_manifest(files):
    return (json.dumps({"schema": "gf-real-package-lab/1",
                        "operational_installable": False,
                        "files": files},
                       sort_keys=True, separators=(",", ":")) + "\n").encode()


def load_keys(source):
    spec = importlib.util.spec_from_file_location("gf_phase1_public_test_seeds", source)
    require(spec is not None and spec.loader is not None, "TEST_SEEDS_NOT_FOUND")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def tuf_sign_and_verify(repo, candidate, target_root):
    """Sign exactly the new GF manifest and verify it using Python-TUF 7.0.1."""
    g = load_keys(repo / "prototipos/autoatualizador-seguro/generate_fixtures.py")
    anchor_path = repo / "prototipos/autoatualizador-seguro/fixtures/bootstrap.test-only.json"
    anchor = anchor_path.read_bytes()
    require(digest(anchor) == "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "ROOT_BOOTSTRAP_SHA")
    require(datetime.now(timezone.utc) < datetime(2027, 1, 1, tzinfo=timezone.utc),
            "PUBLIC_TEST_ROOT_EXPIRED")
    root_meta = Metadata.from_bytes(anchor)
    root_meta.verify_delegate("root", root_meta)
    now = datetime.now(timezone.utc).replace(microsecond=0)

    def sign(data, role):
        metadata = Metadata.from_bytes(g.wire(g.envelope(data, [role])))
        metadata.sign(CryptoSigner(
            g.PRIVATE[role], SSlibKey.from_dict(g.IDS[role], copy.deepcopy(g.KEYS[role]))))
        return metadata.to_bytes()

    base = {"spec_version": "1.0.36", "version": 1,
            "expires": g.utc(now + timedelta(minutes=30))}
    target_name = "GF-LAB-REAL-PACKAGE-MANIFEST.json"
    targets = dict(base, _type="targets", targets={
        target_name: {"length": len(candidate),
                      "hashes": {"sha256": digest(candidate)},
                      "custom": {"gf_lab": {"schema": "gf-real-gf-lab/1",
                                            "operational_installable": False}}}})
    tb = sign(targets, "targets")
    snapshot = dict(base, _type="snapshot", meta={
        "targets.json": {"version": 1, "length": len(tb), "hashes": {"sha256": digest(tb)}}})
    sb = sign(snapshot, "snapshot")
    timestamp = dict(base, _type="timestamp", meta={
        "snapshot.json": {"version": 1, "length": len(sb), "hashes": {"sha256": digest(sb)}}})
    ts = sign(timestamp, "timestamp")
    target_root.mkdir(exist_ok=False)
    for name, content in ((target_name, candidate), ("targets.json", tb),
                          ("snapshot.json", sb), ("timestamp.json", ts)):
        (target_root / name).write_bytes(content)

    def verify(role_name, raw):
        meta = Metadata.from_bytes(raw)
        root_meta.verify_delegate(role_name, meta)
        require(meta.signed.expires > datetime.now(timezone.utc), "TUF_EXPIRED_" + role_name)
        return meta
    verify("timestamp", ts)
    verify("snapshot", sb)
    targets_meta = verify("targets", tb)
    snapshot_meta = Metadata.from_bytes(sb)
    timestamp_meta = Metadata.from_bytes(ts)
    require(timestamp_meta.signed.meta["snapshot.json"].length == len(sb) and
            timestamp_meta.signed.meta["snapshot.json"].hashes["sha256"] == digest(sb),
            "TUF_TIMESTAMP_SNAPSHOT_MISMATCH")
    require(snapshot_meta.signed.meta["targets.json"].length == len(tb) and
            snapshot_meta.signed.meta["targets.json"].hashes["sha256"] == digest(tb),
            "TUF_SNAPSHOT_TARGETS_MISMATCH")
    target = targets_meta.signed.targets[target_name]
    require(target.length == len(candidate) and
            target.hashes["sha256"] == digest(candidate),
            "TUF_SIGNED_MANIFEST_MISMATCH")
    require(target.custom["gf_lab"]["operational_installable"] is False,
            "TUF_OPERATIONAL_FORBIDDEN")
    return {"manifest_sha256": digest(candidate), "root_sha256": digest(anchor),
            "targets_sha256": digest(tb), "snapshot_sha256": digest(sb),
            "timestamp_sha256": digest(ts), "test_only": True}


def validate_package(directory, verified_manifest):
    contents = manifest_for(directory)
    require(contents == verified_manifest, "PACKAGE_HASH_OR_FILE_SET_MISMATCH")
    return True


def write_selector(root, selected):
    require(selected in ("old", "new"), "SELECTOR")
    path = root / "active.fixture.json"
    tmp = root / "selector.pending.fixture"
    tmp.write_text(json.dumps({"schema": "gf-real-candidate-lab/1",
                               "selected": selected,
                               "operational_installable": False}) + "\n",
                   encoding="utf-8")
    os.replace(tmp, path)
    require(json.loads(path.read_text(encoding="utf-8"))["selected"] == selected,
            "SELECTOR_WRITE_FAILED")


def observed_process(directory, seconds):
    """Launch real GF, bound observation, then kill isolated owned process tree.
    PID existence/elapsed time is NOT WinForms READY or independent health."""
    exe = directory / "GerenciadorFinal.exe"
    start = time.monotonic()
    env = dict(os.environ)
    env["GfBetaEndpoint"] = ""
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    proc = None
    result = {"launch_attempted": True, "created": False,
              "still_alive_at_bound": False, "ready": False,
              "independent_health": False, "terminated": False}
    try:
        proc = subprocess.Popen([str(exe), "--tray"], cwd=str(directory),
                                env=env, stdin=subprocess.DEVNULL,
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        result["created"] = True
        result["pid_observed"] = proc.pid
        until = time.monotonic() + seconds
        while time.monotonic() < until and proc.poll() is None:
            time.sleep(0.25)
        result["still_alive_at_bound"] = proc.poll() is None
        result["exit_if_early"] = proc.poll()
    except OSError as exc:
        result["error"] = type(exc).__name__
    finally:
        if proc is not None:
            if proc.poll() is None:
                subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                               timeout=12, check=False)
            try:
                proc.wait(timeout=12)
            except subprocess.TimeoutExpired:
                result["termination_unverified"] = True
        result["terminated"] = proc is None or proc.poll() is not None
        result["duration_seconds"] = round(time.monotonic() - start, 2)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--old", required=True, type=Path)
    parser.add_argument("--new", required=True, type=Path)
    parser.add_argument("--sandbox", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()
    require(os.name == "nt", "WINDOWS_REQUIRED")
    require(os.environ.get("GITHUB_ACTIONS") == "true" and
            os.environ.get("RUNNER_TEMP"), "DISPOSABLE_GITHUB_WINDOWS_REQUIRED")
    sandbox = args.sandbox.resolve(strict=False)
    tmp = Path(os.environ["RUNNER_TEMP"]).resolve()
    require(sandbox.is_relative_to(tmp) and sandbox != tmp and not sandbox.exists(),
            "SANDBOX_MUST_BE_FRESH_UNDER_RUNNER_TEMP")
    require(args.old.resolve().is_relative_to(tmp) and
            args.new.resolve().is_relative_to(tmp), "PRODUCT_OUTPUT_MUST_BE_TEMP")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    result = {"schema": "gf-phase1-real-package-observation/1",
              "status": "blocked_until_real_ready_and_independent_review",
              "phase1_complete": False,
              "real_gf_built": False, "real_gf_process_attempted": False,
              "tuf_laboratory_test_keys_only": True,
              "operational_gates_closed": 0,
              "no_deploy_no_production_credentials": True,
              "ready_verified": False,
              "automatic_update_authorized": False,
              "real_power_loss_tested": False}
    try:
        old_manifest = manifest_for(args.old)
        new_manifest = manifest_for(args.new)
        require(old_manifest != new_manifest, "OLD_NEW_BUILDS_IDENTICAL")
        require(old_manifest["GerenciadorFinal.dll"]["sha256"] !=
                new_manifest["GerenciadorFinal.dll"]["sha256"], "GF_DLL_VERSION_NOT_DIFFERENT")
        result["real_gf_built"] = True
        result["old_files"] = len(old_manifest)
        result["new_files"] = len(new_manifest)
        sandbox.mkdir()
        (sandbox / ".gf-real-lab-only").write_text("NEVER-INSTALL\n", encoding="utf-8")
        canary = sandbox / "PERSONAL-KEEP-UNCHANGED.txt"
        canary.write_text("DO-NOT-TOUCH-MY-DATA\n", encoding="utf-8")
        versions = sandbox / "versions"
        versions.mkdir()
        old = versions / "old"
        new = versions / "new"
        shutil.copytree(args.old, old)
        shutil.copytree(args.new, new)
        require(validate_package(old, old_manifest), "OLD_NOT_PREPARED")
        signed = tuf_sign_and_verify(args.repository, canonical_manifest(new_manifest),
                                     sandbox / "tuf-public-test-only")
        result["signed_test_metadata"] = signed
        require(validate_package(new, new_manifest), "NEW_NOT_AUTHENTICATED")
        result["authenticated_real_package"] = True
        write_selector(sandbox, "old")

        # Negative: tamper an existing DLL IN PLACE without changing its path.
        dll = new / "GerenciadorFinal.dll"
        with dll.open("r+b") as handle:
            original = handle.read(1)
            require(len(original) == 1, "EMPTY_DLL")
            handle.seek(0)
            handle.write(bytes([original[0] ^ 1]))
        rejected = False
        try:
            validate_package(new, new_manifest)
        except RuntimeError as exc:
            rejected = "PACKAGE_HASH_OR_FILE_SET_MISMATCH" in str(exc)
        require(rejected, "TAMPERED_DLL_ACCEPTED")
        result["tampered_dll_rejected_before_selection"] = True
        # Restore using exact authenticated publish bytes. Do not transfer the
        # candidate while it is corrupted.
        shutil.copyfile(args.new / "GerenciadorFinal.dll", dll)
        validate_package(new, new_manifest)

        old_observation = observed_process(old, 3)
        result["old_process"] = old_observation
        require(old_observation["terminated"], "OLD_PROCESS_NOT_TERMINATED")
        require(validate_package(old, old_manifest), "OLD_DRIFT_BEFORE_SWAP")
        require(validate_package(new, new_manifest), "NEW_DRIFT_BEFORE_SWAP")
        write_selector(sandbox, "new")
        result["candidate_selected_in_disposable_sandbox"] = True
        candidate = observed_process(new, 3)
        result["new_process"] = candidate
        result["real_gf_process_attempted"] = candidate["launch_attempted"]
        require(candidate["terminated"], "NEW_PROCESS_NOT_TERMINATED")
        # Crucial: the GitHub Windows service runner cannot attest a real
        # WinForms READY. Regardless of PID, ALWAYS revert to OLD.
        write_selector(sandbox, "old")
        result["rolled_back_due_to_no_ready"] = True
        require(validate_package(old, old_manifest), "OLD_HASHES_LOST")
        require(canary.read_text(encoding="utf-8") == "DO-NOT-TOUCH-MY-DATA\n",
                "PERSONAL_CANARY_CHANGED")
        require(json.loads((sandbox/"active.fixture.json").read_text())["selected"]=="old",
                "OLD_NOT_RESELECTED")
        result["old_and_canary_preserved"] = True
        result["verified_result"] = "REAL_BYTES_TUF_AUTHENTICATED_PROCESS_BOUND_OBSERVED_ROLLBACK_OLD"
        print("GF_PHASE1_LAB: real package signed and verified, tamper rejected; "
              "bounded actual process observation; rollback OLD; READY NOT PROVEN")
    except BaseException as e:
        # Recover OLD conservatively if it exists and looks intact; never
        # promote NEW on an exception or mark successful readiness.
        result["failure"] = type(e).__name__ + ": " + str(e)[:200]
        if sandbox.is_dir() and (sandbox/"versions"/"old").is_dir():
            try:
                if validate_package(sandbox/"versions"/"old", old_manifest):
                    write_selector(sandbox, "old")
                    result["rolled_back_after_failure"] = True
            except Exception:
                result["recovery_unknown"] = True
        raise
    finally:
        args.report.write_text(json.dumps(result, indent=2, sort_keys=True)+"\n",
                               encoding="utf-8")
        print("GF_PHASE1_REPORT " + json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
