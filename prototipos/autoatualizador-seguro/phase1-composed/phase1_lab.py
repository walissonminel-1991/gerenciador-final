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
import tempfile

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


def canonical_manifest(files, installation, transaction, old_files):
    require(len(installation) == 32 and len(transaction) == 32 and
            all(c in "0123456789abcdef" for c in installation + transaction),
            "LAB_INSTALLATION_TRANSACTION_FORMAT")
    return (json.dumps({"schema": "gf-real-package-lab/3",
                        "old_files": old_files,
                        "installation": installation, "transaction": transaction,
                        "selected_before": "old",
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
    require(timestamp_meta.signed.snapshot_meta.length == len(sb) and
            timestamp_meta.signed.snapshot_meta.hashes["sha256"] == digest(sb),
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


def tuf_verify_prepared(repo, candidate, target_root):
    """Verify previously produced test-only TUF metadata; DO NOT re-sign.

    The real target bytes and scope are compared to the signed target AFTER
    validating the Root/Targets/Snapshot/Timestamp chain and expiry.
    """
    anchor_path = repo / "prototipos/autoatualizador-seguro/fixtures/bootstrap.test-only.json"
    anchor = anchor_path.read_bytes()
    require(digest(anchor) == "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "ROOT_BOOTSTRAP_SHA")
    root = Metadata.from_bytes(anchor)
    root.verify_delegate("root", root)
    now = datetime.now(timezone.utc)
    require(root.signed.expires > now, "TUF_ROOT_EXPIRED")
    content = {}
    for role in ("timestamp", "snapshot", "targets"):
        raw = (target_root / (role + ".json")).read_bytes()
        require(len(raw) <= 524288, "TUF_ROLE_TOO_LARGE")
        metadata = Metadata.from_bytes(raw)
        root.verify_delegate(role, metadata)
        require(metadata.signed.expires > now and
                metadata.signed.version == 1, "TUF_ROLE_EXPIRED_OR_VERSION_" + role)
        content[role] = (metadata, raw)
    snap = content["snapshot"][1]
    target_bytes = content["targets"][1]
    time_meta = content["timestamp"][0].signed.snapshot_meta
    snap_ref = content["snapshot"][0].signed.meta["targets.json"]
    require(time_meta.length == len(snap) and
            time_meta.hashes["sha256"] == digest(snap) and
            time_meta.version == 1, "TUF_TIMESTAMP_LINK")
    require(snap_ref.length == len(target_bytes) and
            snap_ref.hashes["sha256"] == digest(target_bytes) and
            snap_ref.version == 1, "TUF_SNAPSHOT_LINK")
    named = "GF-LAB-REAL-PACKAGE-MANIFEST.json"
    require(set(content["targets"][0].signed.targets.keys()) == {named},
            "TUF_TARGET_SET")
    target = content["targets"][0].signed.targets[named]
    actual = (target_root / named).read_bytes()
    require(actual == candidate and len(actual) <= 2097152 and
            target.length == len(candidate) and
            target.hashes["sha256"] == digest(candidate), "TUF_REAL_MANIFEST_LINK")
    require(target.custom["gf_lab"]["operational_installable"] is False,
            "TUF_NOT_OPERATIONAL")
    return {"manifest_sha256": digest(candidate),
            "root_sha256": digest(anchor),
            "targets_sha256": digest(target_bytes),
            "snapshot_sha256": digest(snap),
            "timestamp_sha256": digest(content["timestamp"][1]),
            "test_only": True}


def validate_package(directory, verified_manifest):
    contents = manifest_for(directory)
    require(contents == verified_manifest, "PACKAGE_HASH_OR_FILE_SET_MISMATCH")
    return True


def read_coordinator_selector(root, selected, generation, installation, transaction, signed_target):
    """Only C# L18Coordinator writes the real selector; this observer only reads."""
    path = root / "l18-real-active.fixture.json"
    require(path.is_file() and not path.is_symlink(), "COORDINATOR_SELECTOR_MISSING")
    require((path.stat().st_file_attributes & 0x400) == 0, "COORDINATOR_SELECTOR_REPARSE")
    require(path.stat().st_size <= 2048, "COORDINATOR_SELECTOR_TOO_LARGE")
    item = json.loads(path.read_bytes())
    require(item == {"schema": "gf-l18-real-active-lab/1",
                     "selected": selected, "generation": generation,
                     "installation": installation, "transaction": transaction,
                     "manifest_sha256": signed_target, "test_only": True,
                     "operational_installable": False},
            "COORDINATOR_SELECTOR_DIVERGED")
    return item["selected"]


def selected_real_package(selector_root, versions, expected, generation,
                          installation, transaction, signed_target):
    """Derive the executable slot from the L18 selector, not an observer branch.

    The expected slot is only an assertion. Source publish paths are used for
    staging/authentication, never as authority to choose the executable.
    """
    selected = read_coordinator_selector(
        selector_root, expected, generation, installation, transaction, signed_target)
    root = versions.resolve(strict=True)
    chosen = (root / selected).resolve(strict=True)
    require(chosen.parent == root and chosen.name == selected and
            chosen.is_dir() and not chosen.is_symlink(),
            "SELECTED_PACKAGE_NOT_A_CANONICAL_SLOT")
    return chosen


def observed_process(directory, expected_exe_sha, seconds=12):
    """Measure HWND owner, UI message loop, graceful WM_CLOSE of *this* GF.

    Observation is process-bound, not application READY. If Windows runner
    lacks interactive desktop, keep that limitation explicit and roll OLD.
    """
    from win32_process_observation import observe_real_gf
    exe = directory / "GerenciadorFinal.exe"
    start = time.monotonic()
    env = dict(os.environ)
    env["GfBetaEndpoint"] = ""
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    proc = None
    result = {"launch_attempted": True, "created": False,
              "still_alive_at_bound": False, "ready": False,
              "independent_health": False, "terminated": False,
              "graceful_exit_observed": False,
              "main_form_message_loop_responded": False}
    try:
        # The real GF's --tray mode suppresses first-start modal consent.
        # The HWND may be hidden, but a responsive message loop is observable.
        # Never use a diagnostic log/terminal receipt as a health witness.
        proc = subprocess.Popen([str(exe), "--tray"], cwd=str(directory),
                                env=env, stdin=subprocess.DEVNULL,
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        result["created"] = True
        result["pid_observed"] = proc.pid
        observed = observe_real_gf(proc, exe, expected_exe_sha, seconds=seconds)
        result.update(observed)
        result["still_alive_at_bound"] = proc.poll() is None
        result["exit_if_early"] = proc.poll()
        result["graceful_shutdown_verified_for_owned_process"] = (
            result["graceful_exit_observed"]
            and observed.get("actual_image_path_matches", False)
            and observed.get("image_on_disk_sha_matches_manifest", False)
            and observed.get("main_form_message_loop_responded", False))
        # A process whose window responds is not a verified healthy GF.
        result["ready"] = False
        result["independent_health"] = False
    except OSError as exc:
        result["error"] = type(exc).__name__
    finally:
        if proc is not None:
            if proc.poll() is None:
                # Fallback only in disposable CI, after graceful path has been
                # attempted or explicitly unavailable. Force-kill is NOT proof
                # of controlled shutdown and never authorizes NEW.
                result["forced_cleanup_required"] = True
                # Use Popen's retained process HANDLE, never a PID tree kill
                # vulnerable to exit/reuse between poll and taskkill. The
                # recovery suite's C# Job Object contains any descendants.
                proc.kill()
                result["forced_cleanup_scope"] = "retained_owned_process_handle_only"
            else:
                result["forced_cleanup_required"] = False
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
    parser.add_argument("--auth-dir", required=True, type=Path)
    parser.add_argument("--auth-report", required=True, type=Path)
    parser.add_argument("--installation", required=True)
    parser.add_argument("--transaction", required=True)
    parser.add_argument("--preflight-only", action="store_true")
    parser.add_argument("--recovery-preflight-root", type=Path)
    parser.add_argument("--selector-root", type=Path)
    parser.add_argument("--handoff-token")
    args = parser.parse_args()
    require(os.name == "nt", "WINDOWS_REQUIRED")
    require(os.environ.get("GITHUB_ACTIONS") == "true" and
            os.environ.get("RUNNER_TEMP"), "DISPOSABLE_GITHUB_WINDOWS_REQUIRED")
    sandbox = args.sandbox.resolve(strict=False)
    tmp = Path(os.environ["RUNNER_TEMP"]).resolve()
    require(sandbox.is_relative_to(tmp) and sandbox != tmp and not sandbox.exists(),
            "SANDBOX_MUST_BE_FRESH_UNDER_RUNNER_TEMP")
    if args.recovery_preflight_root is not None:
        src = args.recovery_preflight_root.resolve(strict=True)
        private_temp = Path(tempfile.gettempdir()).resolve()
        require(args.preflight_only and src.is_relative_to(private_temp) and
                src.parent.name.startswith("gf-u2-") and
                src.is_dir() and not src.is_symlink() and
                (src.stat().st_file_attributes & 0x400) == 0 and
                (src / ".gf-u2-fixture").read_text(encoding="utf-8") ==
                "GF-U2-INERT-STATE-TESTS-NO-PRODUCT\n" and
                args.old.resolve() == src / "real/old" and
                args.new.resolve() == src / "real/new",
                "RECOVERY_PREFLIGHT_NOT_CANONICAL_PRIVATE_FIXTURE")
    else:
        require(args.old.resolve().is_relative_to(tmp) and
                args.new.resolve().is_relative_to(tmp), "PRODUCT_OUTPUT_MUST_BE_TEMP")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    auth_dir = args.auth_dir.resolve(strict=False)
    require(auth_dir.is_relative_to(tmp) and auth_dir != tmp,
            "AUTH_DIRECTORY_MUST_BE_RUNNER_TEMP")
    # Sign and authenticate the exact GF build BEFORE any coordinator
    # transaction event or selector change. This cannot grant production trust.
    if args.preflight_only:
        require(not auth_dir.exists() and not args.auth_report.exists() and
                not sandbox.exists(), "AUTH_PREFLIGHT_NOT_FRESH")
        old_manifest = manifest_for(args.old)
        new_manifest = manifest_for(args.new)
        require(old_manifest != new_manifest and
                old_manifest["GerenciadorFinal.dll"]["sha256"] !=
                new_manifest["GerenciadorFinal.dll"]["sha256"], "SAME_REAL_PACKAGE")
        candidate = canonical_manifest(new_manifest, args.installation, args.transaction, old_manifest)
        signed = tuf_sign_and_verify(args.repository, candidate, auth_dir)
        verified = tuf_verify_prepared(args.repository, candidate, auth_dir)
        require(verified["manifest_sha256"] == signed["manifest_sha256"],
                "AUTH_PREFLIGHT_VERIFIER_DIVERGED")
        payload = {"schema": "gf-real-lab-auth-preflight/1",
                   "installation": args.installation,
                   "transaction": args.transaction,
                   "selected_before": "old",
                   "manifest_sha256": signed["manifest_sha256"],
                   "test_only": True,
                   "tuf_verified": True, "no_real_process_launched": True,
                   "operational_installable": False}
        args.auth_report.write_text(json.dumps(payload, sort_keys=True) + "\n",
                                    encoding="utf-8")
        print("GF_PHASE1_REAL_TUF_PREFLIGHT " + json.dumps(payload, sort_keys=True))
        return 0

    # This Python process is a TUF preflight/recovery observer only.
    # Any legacy cooperative normal invocation must fail explicitly.
    require(False, "LEGACY_COOPERATIVE_NORMAL_DISABLED")


def recovery_tool(mode, source):
    """Subordinate re-verifier/observer. Never changes the C# selector."""
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true",
            "RECOVERY_DISPOSABLE_WINDOWS_ONLY")
    src = Path(source).resolve(strict=True)
    tmp = Path(tempfile.gettempdir()).resolve()
    require(src.is_relative_to(tmp) and src.parent.name.startswith("gf-u2-") and
            (src / ".gf-u2-fixture").read_text(encoding="utf-8") ==
            "GF-U2-INERT-STATE-TESTS-NO-PRODUCT\n", "RECOVERY_SOURCE_SCOPE")
    context_path = src / "l18-real-context.fixture.json"
    require(context_path.stat().st_size <= 524288, "RECOVERY_CONTEXT_SIZE")
    c = json.loads(context_path.read_bytes())
    auth_dir = src / "real-auth"
    raw = (auth_dir / "GF-LAB-REAL-PACKAGE-MANIFEST.json").read_bytes()
    m = json.loads(raw)
    require(m["schema"] == "gf-real-package-lab/3" and
            m["installation"] == c["installation"] and
            m["transaction"] == c["transaction"] and
            digest(raw) == c["manifestSha"] and
            m["operational_installable"] is False, "RECOVERY_MANIFEST_SCOPE")
    tuf_verify_prepared(Path(c["repository"]), raw, auth_dir)
    validate_package(src / "real/old", m["old_files"])
    validate_package(src / "real/new", m["files"])
    if mode == "--recovery-verify":
        return 0
    require(mode in ("--recovery-observe", "--out-of-band-new-observe"), "RECOVERY_MODE")
    if mode == "--out-of-band-new-observe":
        # Adversarial TEST ONLY: the real NEW is run directly from the
        # disposable supervisor's Python process, not from C# RealOwner.
        # Proves only a coordinator bypass in this scoped lab, NOT that
        # Windows globally allows every equivalent image to run.
        read_coordinator_selector(src, "old", 1, c["installation"],
                                  c["transaction"], c["manifestSha"])
        fence = src / "l18-real-new-may-have-effects.fixture.txt"
        require(not fence.exists() and not fence.is_symlink(),
                "OUT_OF_BAND_NEW_FENCE_ALREADY_PRESENT")
        directory = (src / "real/new").resolve(strict=True)
        require(directory == src / "real/new" and directory.is_dir() and
                not directory.is_symlink(), "OUT_OF_BAND_NEW_NOT_CANONICAL")
        files = m["files"]
        report_slot = "out-of-band-new"
    else:
        item = json.loads((src / "l18-real-active.fixture.json").read_bytes())
        selected, generation = item["selected"], item["generation"]
        require((selected, generation) in (("old", 1), ("new", 2), ("old", 3)),
                "RECOVERY_SELECTOR_GENERATION")
        directory = selected_real_package(src, src / "real", selected, generation,
                                          c["installation"], c["transaction"], c["manifestSha"])
        files = m["old_files"] if selected == "old" else m["files"]
        report_slot = selected
    observation = observed_process(directory, files["GerenciadorFinal.exe"]["sha256"])
    # Diagnostic report does not confer recovery/READY authority on Python.
    observation.pop("pid_observed", None)
    (src / ("real-observed-" + report_slot + ".fixture.json")).write_text(
        json.dumps(observation, sort_keys=True) + "\n", encoding="utf-8")
    require(observation.get("created") and observation.get("terminated") and
            observation.get("actual_image_path_matches") and
            observation.get("image_on_disk_sha_matches_manifest") and
            observation.get("graceful_shutdown_verified_for_owned_process") and
            not observation.get("forced_cleanup_required") and
            observation.get("readonly_inventory_search_health", {})
            .get("minimum_readonly_health_contract_observed") is True and
            not observation.get("ready_verified") and
            not observation.get("application_functional_health_verified"),
            "RECOVERY_FUNCTIONAL_OBSERVATION_UNPROVEN")
    validate_package(directory, files)
    return 0


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] in (
            "--recovery-verify", "--recovery-observe", "--out-of-band-new-observe"):
        sys.exit(recovery_tool(sys.argv[1], sys.argv[2]))
    sys.exit(main())
