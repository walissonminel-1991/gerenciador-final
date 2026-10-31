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
import secrets
import ctypes
from ctypes import wintypes as w
import winreg
from contextlib import contextmanager, nullcontext

from tuf.api.metadata import Metadata
from securesystemslib.signer import CryptoSigner, SSlibKey


def require(condition, why):
    if not condition:
        raise RuntimeError("GF_PHASE1_" + why)


def guard_data_only_launch_apis():
    """Reject accidental launches via these Python APIs in this verifier.

    Regression guard only: NOT a sandbox, native-code barrier, durable
    witness, or statement about other processes/the whole machine.
    """
    def reject(event, _args):
        if event in ("subprocess.Popen", "os.system", "os.startfile",
                     "os.startfile/2", "os.exec", "os.posix_spawn", "os.spawn"):
            raise RuntimeError("GF_PHASE1_DATA_ONLY_LAUNCH_API_DENIED")
    sys.addaudithook(reject)


def test_data_only_launch_guard():
    guard_data_only_launch_apis()
    # Would create a Python child if the guard were absent; no shell, GF,
    # maintenance or network operation. The hook runs before process creation.
    try:
        child = subprocess.Popen([sys.executable, "-c", "pass"])
    except RuntimeError as exc:
        require(str(exc) == "GF_PHASE1_DATA_ONLY_LAUNCH_API_DENIED", "WRONG_LAUNCH_GUARD")
        print("GF_PHASE1_DATA_ONLY_LAUNCH_GUARD PASS_API_DENIAL_NOT_SANDBOX")
        return 0
    child.wait(timeout=10)
    require(False, "DATA_ONLY_LAUNCH_GUARD_BYPASSED")


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



class _NtfsByHandleInformation(ctypes.Structure):
    _fields_ = [
        ("dwFileAttributes", w.DWORD), ("ftCreationTime", w.FILETIME),
        ("ftLastAccessTime", w.FILETIME), ("ftLastWriteTime", w.FILETIME),
        ("dwVolumeSerialNumber", w.DWORD), ("nFileSizeHigh", w.DWORD),
        ("nFileSizeLow", w.DWORD), ("nNumberOfLinks", w.DWORD),
        ("nFileIndexHigh", w.DWORD), ("nFileIndexLow", w.DWORD)]


def _ntfs_handle_identity(handle):
    """Actual file-object volume/index from the still-held Windows HANDLE."""
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.GetFileInformationByHandle.argtypes = [
        w.HANDLE, ctypes.POINTER(_NtfsByHandleInformation)]
    kernel.GetFileInformationByHandle.restype = w.BOOL
    item = _NtfsByHandleInformation()
    require(kernel.GetFileInformationByHandle(w.HANDLE(handle),
        ctypes.byref(item)), "GF_PIN_NTFS_FILE_ID_READ_FAILED")
    return [int(item.dwVolumeSerialNumber),
            (int(item.nFileIndexHigh) << 32) | int(item.nFileIndexLow)]


@contextmanager
def _ntfs_read_pin(exe, expected_sha):
    """LAB ONLY: deny new WRITE and DELETE opens while holding the same file
    object through hash, mutation attempts and the original Popen call.
    A Windows FILE_SHARE_READ handle is NOT proof of mapped image bytes,
    preexisting writable handles or a solution to all TOCTOU cases.
    """
    import msvcrt
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true"
            and os.environ.get("GITHUB_REPOSITORY") ==
            "walissonminel-1991/gerenciador-final"
            and os.environ.get("GF_E2E_L18_OBSERVE_INSTALLED_NEW") == "1"
            and os.environ.get("GF_E2E_LIVE_INSTALL_SWAP") == "1"
            and os.environ.get("GF_E2E_PRELAUNCH_TAMPER_TEST") != "1"
            and os.environ.get("GF_E2E_POSTHASH_TAMPER_TEST") != "1"
            and exe.is_file() and not exe.is_symlink(),
            "GF_NTFS_PIN_NOT_ALLOWED_OR_NOT_REGULAR_INSTALLED_NEW")
    api = ctypes.WinDLL("kernel32", use_last_error=True)
    api.CreateFileW.argtypes = [w.LPCWSTR,w.DWORD,w.DWORD,w.LPVOID,w.DWORD,w.DWORD,w.HANDLE]
    api.CreateFileW.restype = w.HANDLE
    api.CloseHandle.argtypes = [w.HANDLE]
    api.CloseHandle.restype = w.BOOL
    # GENERIC_READ, FILE_SHARE_READ ONLY (no FILE_SHARE_WRITE or DELETE),
    # OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL.
    raw = api.CreateFileW(str(exe), 0x80000000, 0x00000001, None, 3, 0x80, None)
    require(raw is not None and raw != ctypes.c_void_p(-1).value,
            "GF_NTFS_PIN_CREATEFILE_FAILED")
    try:
        fd = msvcrt.open_osfhandle(raw, os.O_RDONLY | os.O_BINARY)
    except BaseException:
        api.CloseHandle(w.HANDLE(raw))
        raise
    with os.fdopen(fd, "rb") as pinned:
        start_id = _ntfs_handle_identity(msvcrt.get_osfhandle(pinned.fileno()))
        def pinned_hash():
            pinned.seek(0)
            digest = hashlib.sha256()
            while True:
                chunk = pinned.read(1024*1024)
                if not chunk:
                    break
                digest.update(chunk)
            return digest.hexdigest()
        require(pinned_hash() == expected_sha,
                "GF_NTFS_PIN_HASH_OF_OPEN_OBJECT_MISMATCH")
        evidence = {"schema":"gf-e2e-ntfs-read-pin-launch/1",
                    "file_id": start_id, "original_exe_sha256":expected_sha,
                    "pinned_file_object_hash_verified":True,
                    "prepin_append_open_allowed":True,
                    "share_mode":"FILE_SHARE_READ_ONLY",
                    "pin_covers_popen":False,
                    "write_denied":False, "rename_denied":False,
                    "test_only":True,"operational_ready":False}
        def adversarial_attempts():
            require(_ntfs_handle_identity(msvcrt.get_osfhandle(pinned.fileno()))
                    == start_id, "GF_NTFS_PIN_ID_CHANGED_BEFORE_ATTACK")
            write_denied=False
            rename_denied=False
            write_error=None
            rename_error=None
            try:
                with exe.open("ab") as f:
                    f.write(b"\xa5")
                    f.flush()
            except OSError as exc:
                # Python 3.13 on Windows reports CRT append-open
                # sharing denial as PermissionError(errno=EACCES=13) with
                # winerror=None, while os.rename supplies WinError 32.
                # Accept ONLY this observed specific condition, and ONLY
                # with pre-pin append-open success and unchanged hashes.
                code=getattr(exc,"winerror",None)
                write_denied=(code in (5,32) or
                    (code is None and isinstance(exc,PermissionError) and
                     getattr(exc,"errno",None)==13))
                write_error={"class":type(exc).__name__,
                             "winerror":getattr(exc,"winerror",None),
                             "errno":getattr(exc,"errno",None)}
            # The sole negative test never creates a replacement directory
            # or performs any updater cleanup/recovery.
            try:
                os.rename(exe, exe.with_name("GerenciadorFinal.exe.rename-probe"))
            except OSError as exc:
                rename_denied=getattr(exc,"winerror",None) in (5,32)
                rename_error={"class":type(exc).__name__,
                              "winerror":getattr(exc,"winerror",None),
                              "errno":getattr(exc,"errno",None)}
            evidence["write_denied"]=write_denied
            evidence["rename_denied"]=rename_denied
            # Read-only, bounded evidence written BEFORE the existing
            # conservative Require. A denial is not inferred from a marker.
            # Error strings and absolute/private paths are never written.
            obj_unchanged=pinned_hash()==expected_sha
            try:
                path_unchanged=file_hash(exe)==expected_sha
            except OSError:
                path_unchanged=False
            runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
            with (runner/"gf-e2e-ntfs-pin-attack-diagnostic.json").open(
                    "x",encoding="utf-8") as diagnostic:
                json.dump({
                    "schema":"gf-e2e-ntfs-pin-attack-diagnostic/1",
                    "write_denied_by_existing_winerror_rule":write_denied,
                    "rename_denied_by_existing_winerror_rule":rename_denied,
                    "write_error":write_error,"rename_error":rename_error,
                    "pinned_hash_unchanged":obj_unchanged,
                    "path_hash_unchanged":path_unchanged,
                    "rename_probe_exists":exe.with_name(
                        "GerenciadorFinal.exe.rename-probe").exists(),
                    "test_only":True,"operational_ready":False
                },diagnostic,sort_keys=True)
                diagnostic.flush()
                os.fsync(diagnostic.fileno())
            require(write_denied and rename_denied and
                    obj_unchanged and path_unchanged,
                    "GF_NTFS_PIN_WRITE_OR_RENAME_NOT_DENIED")
            evidence["file_id_after_attempts"] = _ntfs_handle_identity(
                msvcrt.get_osfhandle(pinned.fileno()))
        yield evidence, adversarial_attempts, pinned_hash, start_id, pinned
        require(pinned_hash()==expected_sha and
                _ntfs_handle_identity(msvcrt.get_osfhandle(pinned.fileno()))==
                start_id, "GF_NTFS_PIN_OBJECT_CHANGED_BEFORE_RELEASE")


def observed_process(directory, expected_exe_sha, seconds=12,
                     expected_inventory_name=None):
    """Same original observer, optionally holding a native NTFS file handle."""
    selected_new = (
        os.environ.get("GF_E2E_NTFS_PIN_LAUNCH")=="1" and
        os.environ.get("GF_E2E_L18_OBSERVE_INSTALLED_NEW")=="1" and
        directory.resolve(strict=True) ==
        (Path(os.environ["RUNNER_TEMP"]) / "gf-e2e-old").resolve(strict=True))
    if not selected_new:
        return _observed_process_core(
            directory, expected_exe_sha, seconds, expected_inventory_name)
    # Negative-control authorization: prove that append-open (NO bytes
    # written) is permitted before pinning, on this same installed EXE.
    # Any denial during the pin cannot be credited to an existing read-only
    # ACL. Preserve hashes and do not modify the signed package.
    unpinned_exe=directory / "GerenciadorFinal.exe"
    require(unpinned_exe.is_file() and not unpinned_exe.is_symlink(),
            "GF_NTFS_PIN_PRECHECK_NOT_REGULAR_EXE")
    with unpinned_exe.open("ab"):
        pass
    require(file_hash(unpinned_exe)==expected_exe_sha,
            "GF_NTFS_PIN_PRECHECK_MUTATED_EXE")
    with _ntfs_read_pin(unpinned_exe, expected_exe_sha) as pins:
        report, attack, pinned_hash, start_id, pinned = pins
        observed = _observed_process_core(
            directory,expected_exe_sha,seconds,expected_inventory_name,
            pre_popen_pin_probe=attack)
        require(observed.get("created") and observed.get("terminated") and
                observed.get("graceful_shutdown_verified_for_owned_process") and
                observed.get("actual_image_path_matches") and
                observed.get("image_on_disk_sha_matches_manifest") and
                observed.get("readonly_inventory_search_health",{}).get(
                    "minimum_readonly_health_contract_observed") is True and
                observed.get("pin_probe_attempts_done") is True and
                pinned_hash()==expected_exe_sha and
                _ntfs_handle_identity(
                    __import__("msvcrt").get_osfhandle(pinned.fileno()))==start_id,
                "GF_NTFS_PIN_BLOCKED_LEGITIMATE_GF_OR_CHANGED_BYTES")
        report.update({"pin_covers_popen":True,
            "process_created_by_original_popen":True,
            "functional_observation_succeeded":True,
            "pinned_id_unchanged_after_process":True,
            "pinned_hash_unchanged_after_process":True})
        require(report["write_denied"] and report["rename_denied"],
                "GF_NTFS_PIN_DID_NOT_DENY_BOTH_ATTACKS")
        runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
        with (runner/"gf-e2e-ntfs-read-pin-launch.json").open("x",encoding="utf-8") as f:
            json.dump(report,f,sort_keys=True)
            f.flush();os.fsync(f.fileno())
        observed.update({"ntfs_pin_object_id_unchanged":True,
                         "ntfs_pin_write_denied":True,
                         "ntfs_pin_rename_denied":True,
                         "ntfs_pin_held_across_popen":True})
        print("GF_E2E_NTFS_PIN_LAUNCH_PASS "
              "pinned_object=true share=READ_ONLY write_denied=true "
              "rename_denied=true original_Popen_created=true "
              "same_pinned_object=true functional_NEW=true READY=false",
              flush=True)
        return observed


def _observed_process_core(directory, expected_exe_sha, seconds=12,
                     expected_inventory_name=None, pre_popen_pin_probe=None):
    """Measure HWND owner, UI message loop, graceful WM_CLOSE of *this* GF.

    Observation is process-bound, not application READY. If Windows runner
    lacks interactive desktop, keep that limitation explicit and roll OLD.
    """
    from win32_process_observation import observe_real_gf
    exe = directory / "GerenciadorFinal.exe"
    # Revalidate exact expected executable bytes immediately before Popen.
    # This is a bounded laboratory prelaunch check, NOT a TOCTOU guarantee:
    # same-user replacement after this hash is still an open concern.
    require(exe.is_file() and not exe.is_symlink() and
            file_hash(exe) == expected_exe_sha,
            "GF_EXECUTABLE_BYTES_CHANGED_BEFORE_SPAWN")
    if (os.environ.get("GF_E2E_POSTHASH_TAMPER_TEST") == "1" and
            directory.resolve(strict=True) ==
            (Path(os.environ["RUNNER_TEMP"]) / "gf-e2e-old").resolve(strict=True)):
        # Adversarial Windows CI ONLY. This runs AFTER the last pre-Popen
        # SHA256 validation above and BEFORE the existing Popen below.
        # Deliberately demonstrate the remaining filename TOCTOU gap;
        # NOT an installation/recovery operation or new launcher.
        require(os.environ.get("GITHUB_ACTIONS") == "true" and
                os.environ.get("GITHUB_REPOSITORY") ==
                "walissonminel-1991/gerenciador-final" and
                os.environ.get("GF_E2E_L18_OBSERVE_INSTALLED_NEW") == "1" and
                os.environ.get("GF_E2E_PRELAUNCH_TAMPER_TEST") != "1",
                "GF_POSTHASH_MUTATION_NOT_DISPOSABLE_NEW")
        with exe.open("ab") as changed:
            changed.write(b"\xa5")
            changed.flush()
            os.fsync(changed.fileno())
        actual_sha = file_hash(exe)
        require(actual_sha != expected_exe_sha and
                exe.is_file() and not exe.is_symlink(),
                "GF_POSTHASH_MUTATION_NOT_VERIFIED")
        runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
        with (runner / "gf-e2e-installed-new-posthash-mutated.json").open("x", encoding="utf-8") as f:
            json.dump({"schema":"gf-e2e-posthash-pre-Popen-mutation/1",
                       "last_prelaunch_hash_was_original":True,
                       "change_after_last_hash_before_popen":True,
                       "mutation":"append-one-byte",
                       "actual_sha256":actual_sha,"expected_sha256":expected_exe_sha,
                       "test_only":True,"operational_ready":False},f)
            f.flush()
            os.fsync(f.fileno())
    if pre_popen_pin_probe is not None:
        # Must run AFTER original last SHA256, BEFORE original Popen,
        # while the wrapper owns the native handle of the pinned object.
        pre_popen_pin_probe()
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
        if (os.environ.get("GF_E2E_PRELAUNCH_TAMPER_TEST") == "1" and
                directory.resolve(strict=True) ==
                (Path(os.environ["RUNNER_TEMP"]) / "gf-e2e-old").resolve(strict=True)):
            # Monitor ONLY installed NEW. Initial OLD fixture observation is
            # not the adversarial target and must remain functional.
            # The failed prelaunch-hash gate must prevent this point entirely.
            # A marker is NOT permission to launch or infer READY.
            runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
            require(os.environ.get("GITHUB_ACTIONS") == "true" and
                    os.environ.get("GITHUB_REPOSITORY") ==
                    "walissonminel-1991/gerenciador-final",
                    "GF_TAMPER_PROBE_NOT_DISPOSABLE")
            with (runner / "gf-e2e-new-popen-reached.json").open("x", encoding="utf-8") as f:
                json.dump({"schema":"gf-e2e-prelaunch-new-popen-reached/1",
                           "test_only":True,"operational_ready":False}, f)
                f.flush()
                os.fsync(f.fileno())
        proc = subprocess.Popen([str(exe), "--tray"], cwd=str(directory),
                                env=env, stdin=subprocess.DEVNULL,
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        result["created"] = True
        result["pid_observed"] = proc.pid
        if pre_popen_pin_probe is not None:
            result["pin_probe_attempts_done"] = True
        observed = observe_real_gf(proc, exe, expected_exe_sha, seconds=seconds,
                                   expected_inventory_name=expected_inventory_name)
        result.update(observed)
        if os.environ.get("GF_E2E_POSTHASH_TAMPER_TEST") == "1" and (
                directory.resolve(strict=True) ==
                (Path(os.environ["RUNNER_TEMP"]) / "gf-e2e-old").resolve(strict=True)):
            # Evidence is bound to the real Popen process handle and the
            # external Win32 image path check, not a local receipt alone.
            require(result["created"] and
                    observed.get("actual_image_path_matches") is True and
                    observed.get("image_on_disk_sha_matches_manifest") is False,
                    "GF_POSTHASH_PROCESS_IDENTITY_OR_BYTE_MISMATCH_NOT_PROVEN")
            result["posthash_windows_process_created"] = True
            result["posthash_image_path_from_win32_matches"] = True
            result["posthash_disk_hash_not_authenticated"] = True
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
    if args.preflight_only:
        guard_data_only_launch_apis()
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


def trial_marker_path(src: Path):
    return src / "trial-hkcu-owner.fixture.json"


def _trial_key_values(name, token):
    return {"DisplayName": name, "DisplayVersion": "1.0",
            "Publisher": "GF Phase1 inert CI",
            "GFPhase1Owner": token}


def _trial_scope():
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true",
            "TRIAL_REGISTRY_SCOPE")


def cleanup_trial_marker(source: Path):
    """A4: supervisor cleanup, ONLY after observer exit and exact checks.

    A local same-user marker is not trusted cryptographic provenance; a
    handle-based deletion avoids deleting a newly replaced *name*, but
    cannot prevent a same-user writer changing the opened object itself.
    On any observable ambiguity, retain the key and record a HOLD.
    """
    _trial_scope()
    source = Path(source).resolve(strict=True)
    root = Path(tempfile.gettempdir()).resolve()
    require(source.is_relative_to(root) and
            source.parent.name.startswith("gf-u2-") and
            (source / ".gf-u2-fixture").read_text(encoding="utf-8") ==
            "GF-U2-INERT-STATE-TESTS-NO-PRODUCT\n",
            "TRIAL_CLEANUP_SOURCE_SCOPE")
    marker_path = trial_marker_path(source)
    if not marker_path.exists():
        return "NO_MARKER"
    require(marker_path.is_file() and not marker_path.is_symlink()
            and marker_path.stat().st_size <= 4096, "TRIAL_MARKER_UNTRUSTED")
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    require(set(marker) == {"schema", "name", "token"} and
            marker["schema"] == "gf-trial-hkcu-owner/1" and
            isinstance(marker["name"], str) and
            marker["name"].startswith("GF-PHASE1-INVENTORY-") and
            len(marker["name"]) == len("GF-PHASE1-INVENTORY-") + 24 and
            all(ch in "0123456789ABCDEF" for ch in marker["name"][len("GF-PHASE1-INVENTORY-"):]) and
            isinstance(marker["token"], str) and len(marker["token"]) == 64 and
            all(ch in "0123456789abcdef" for ch in marker["token"]),
            "TRIAL_MARKER_IDENTITY_INVALID")
    subkey = ("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\"
              + marker["name"])
    rights = winreg.KEY_READ | winreg.KEY_WOW64_64KEY
    try:
        k = winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey, 0, rights)
    except FileNotFoundError:
        marker_path.unlink()
        return "NOT_CREATED"
    with k:
        count_subkeys, count_values, _ = winreg.QueryInfoKey(k)
        observed = dict((name, (data, kind))
                        for name, data, kind in
                        (winreg.EnumValue(k, i) for i in range(count_values)))
        expected = {k: (v, winreg.REG_SZ)
                    for k, v in _trial_key_values(
                        marker["name"], marker["token"]).items()}
        if count_subkeys != 0 or observed != expected:
            return "HOLD_MUTATED_FIXTURE_PRESERVED"
        # Delete the opened registry OBJECT, not its reusable path.
        # Equivalent to .NET RegistryKey.DeleteSubKeyTree final self-delete
        # (RegDeleteKeyEx(openHkey, L"", view, 0)); fail closed if Windows
        # refuses the operation. A same-user writer modifying THIS SAME
        # object during the last check is still outside Phase 1 authority.
        winreg.DeleteKeyEx(k, "", access=winreg.KEY_WOW64_64KEY, reserved=0)
    marker_path.unlink()
    return "CLEANED_EXACT_FIXTURE"


@contextmanager
def _create_new_trial_registry_key(subkey):
    """Own one native HKEY; winreg accepts its borrowed integer value.

    PyHKEY is not publicly constructible in Python 3.13. Never wrap a raw
    HKEY with competing ownership: RegCloseKey below is its only closer.
    The disposition check precedes every caller write, including tests.
    """
    _trial_scope()
    rights = winreg.KEY_READ | winreg.KEY_WRITE | winreg.KEY_WOW64_64KEY
    native = ctypes.WinDLL("advapi32", use_last_error=True)
    create = native.RegCreateKeyExW
    create.argtypes = [w.HANDLE, w.LPCWSTR, w.DWORD, w.LPWSTR, w.DWORD,
                       w.DWORD, ctypes.c_void_p, ctypes.POINTER(w.HANDLE),
                       ctypes.POINTER(w.DWORD)]
    create.restype = w.LONG
    close = native.RegCloseKey
    close.argtypes = [w.HANDLE]
    close.restype = w.LONG
    key_handle = w.HANDLE()
    disposition = w.DWORD()
    rc = create(w.HANDLE(int(winreg.HKEY_CURRENT_USER)), subkey,
                0, None, 0, rights, None,
                ctypes.byref(key_handle), ctypes.byref(disposition))
    require(rc == 0 and bool(key_handle),
            "TRIAL_REGISTRY_NATIVE_CREATE_FAILED:" + str(rc))
    try:
        require(disposition.value == 1, "TRIAL_REGISTRY_ALREADY_EXISTS")
        yield key_handle.value  # borrowed by winreg; no PyHKEY finalizer
    finally:
        rc = close(key_handle)
        key_handle.value = None
        require(rc == 0, "TRIAL_REGISTRY_NATIVE_CLOSE_FAILED:" + str(rc))


@contextmanager
def trial_registry_fixture(marker_path=None):
    """Disposable HKCU ARP record, create-new only, not a same-user sandbox.

    A3: RegCreateKeyExW returns REG_CREATED_NEW_KEY atomically. Never assume
    ownership from a preceding OpenKey absence check (race). Registry cleanup
    by a retained handle blocks name-replacement deletion; on observable
    same-object mutation refuse deletion and retain the fixture.
    """
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true",
            "TRIAL_REGISTRY_SCOPE")
    name = "GF-PHASE1-INVENTORY-" + secrets.token_hex(12).upper()
    subkey = ("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\"
              + name)
    token = secrets.token_hex(32)
    if marker_path is not None:
        # Stage ownership BEFORE the first Registry mutation. File flushed
        # locally; not an independent durable witness against same-user U.
        with Path(marker_path).open("x", encoding="utf-8") as marker:
            json.dump({"schema": "gf-trial-hkcu-owner/1",
                       "name": name, "token": token}, marker)
            marker.flush()
            os.fsync(marker.fileno())
    rights = winreg.KEY_READ | winreg.KEY_WOW64_64KEY
    with _create_new_trial_registry_key(subkey) as key:
        values = _trial_key_values(name, token)
        try:
            for field, value in values.items():
                winreg.SetValueEx(key, field, 0, winreg.REG_SZ, value)
            yield name
        finally:
            # Original handle remains open; delete the same object, not a
            # potentially substituted name. Reject observable deviations.
            # This is not a global defense against hostile writes inside the
            # same opened object between the last check and deletion.
            try:
                subkeys, value_count, _ = winreg.QueryInfoKey(key)
                observed = {winreg.EnumValue(key, i)[0]:
                            (winreg.EnumValue(key, i)[1],
                             winreg.EnumValue(key, i)[2])
                            for i in range(value_count)}
                require(subkeys == 0 and
                        observed == {k: (v, winreg.REG_SZ)
                                     for k, v in values.items()},
                        "TRIAL_REGISTRY_MUTATED_PRESERVE_FOR_DIAGNOSIS")
                # Fresh re-open is a second consistency check, not an
                # anti-replacement authority; do not claim otherwise.
                with winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                                    subkey, 0, rights) as by_name:
                    actual = {winreg.EnumValue(by_name, i)[0]:
                              (winreg.EnumValue(by_name, i)[1],
                               winreg.EnumValue(by_name, i)[2])
                              for i in range(winreg.QueryInfoKey(by_name)[1])}
                    require(actual == observed,
                            "TRIAL_REGISTRY_REPLACED_PRESERVE_FOR_DIAGNOSIS")
                # Use the originally created, still-open object handle;
                # never re-resolve a potentially replaced subkey name.
                winreg.DeleteKeyEx(key, "", access=winreg.KEY_WOW64_64KEY,
                                   reserved=0)
            except OSError as exc:
                raise RuntimeError("GF_PHASE1_TRIAL_REGISTRY_CLEANUP_UNPROVEN") from exc


def recovery_tool(mode, source):
    """Subordinate re-verifier/observer. Never changes the C# selector."""
    if mode == "--recovery-verify":
        guard_data_only_launch_apis()
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
    require(mode in ("--recovery-observe", "--out-of-band-new-observe",
                     "--recovery-observe-trial", "--recovery-observe-trial-negative"), "RECOVERY_MODE")
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
    observed_installed_new = False
    if os.environ.get("GF_E2E_L18_OBSERVE_INSTALLED_NEW") == "1":
        # SAME L18 selected-package observer; no independent executable.
        require(mode == "--recovery-observe" and report_slot == "new"
                and generation == 2 and
                os.environ.get("GITHUB_REPOSITORY") ==
                "walissonminel-1991/gerenciador-final",
                "LIVE_INSTALL_OBSERVER_NOT_SELECTED_NEW")
        read_coordinator_selector(src, "new", 2, c["installation"],
                                  c["transaction"], c["manifestSha"])
        runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
        live = runner / "gf-e2e-old"
        backup = runner / "gf-e2e-old-preserved"
        receipt = runner / "gf-e2e-live-install-transition.json"
        canary = runner / "gf-e2e-install-data" / "PERSONAL-KEEP-UNCHANGED.txt"
        require(live.is_dir() and not live.is_symlink() and
                live.resolve(strict=True) == live and
                backup.is_dir() and not backup.is_symlink() and
                backup.resolve(strict=True) == backup and
                receipt.is_file() and not receipt.is_symlink() and
                canary.read_bytes() == b"DO-NOT-TOUCH-MY-DATA\n",
                "LIVE_INSTALL_DIRECTORY_OR_DATA_MISSING")
        staged = json.loads(receipt.read_bytes())
        require(staged == {
            "schema": "gf-e2e-live-portable-install/1",
            "physically_installed_new_at_original_old_path": True,
            "retained_old_bytes": True,
            "private_data_canary_unchanged": True,
            "selected": "new", "generation": 2,
            "install_path_under_runner_temp": True,
            "no_rollback_performed": True, "test_only": True,
            "operational_ready": False,
        }, "LIVE_INSTALL_RECEIPT_CONTRADICTORY")
        validate_package(backup, m["old_files"])
        validate_package(live, m["files"])
        directory = live
        observed_installed_new = True
    post_l18_selected_observation = (
        os.environ.get("GF_E2E_POST_EXIT_SELECTED_OBSERVE") == "1")
    if post_l18_selected_observation:
        # POST-EXIT laboratory check uses the EXISTING, selector-gated
        # observer. It is NOT a free-form launcher and MUST reject any
        # selected slot other than the authenticated NEW/2. It runs one
        # bounded UI session after the original coordinator exits.
        require(observed_installed_new and mode == "--recovery-observe"
                and report_slot == "new" and generation == 2,
                "POST_L18_NOT_NEW2_SELECTED")
        runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
        p = runner / "gf-e2e-postexit-selected-root.json"
        require(p.is_file() and not p.is_symlink(),
                "POST_L18_ORIGINAL_SELECTOR_POINTER_MISSING")
        retained = json.loads(p.read_bytes())
        require(retained == {
            "schema": "gf-e2e-postexit-selected-root/1",
            "root": str(src), "selector": "new", "generation": 2,
            "retained_from_same_l18": True,
            "recovery_authorized": False, "operational_ready": False,
            "test_only": True
        }, "POST_L18_SELECTED_SOURCE_MISMATCH")
    if (os.environ.get("GF_E2E_PRELAUNCH_TAMPER_TEST") == "1" and
            observed_installed_new):
        # One adversarial single-byte mutation AFTER signed full-package
        # validation of the live installation, BEFORE the same selected NEW
        # observer gets to Popen. Never run on a personal Windows computer.
        require(observed_installed_new and mode == "--recovery-observe" and
                report_slot == "new" and generation == 2 and
                os.environ.get("GITHUB_ACTIONS") == "true" and
                os.environ.get("GITHUB_REPOSITORY") ==
                "walissonminel-1991/gerenciador-final",
                "GF_PRELAUNCH_MUTATION_SCOPE_DENIED")
        target = directory / "GerenciadorFinal.exe"
        expected = files["GerenciadorFinal.exe"]["sha256"]
        require(target.is_file() and not target.is_symlink() and
                file_hash(target) == expected,
                "GF_PRELAUNCH_INITIAL_INSTALLED_EXE_UNTRUSTED")
        with target.open("ab") as f:
            f.write(b"\xa5")
            f.flush()
            os.fsync(f.fileno())
        actual = file_hash(target)
        require(actual != expected, "GF_PRELAUNCH_MUTATION_DID_NOT_CHANGE_BYTES")
        runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
        with (runner / "gf-e2e-installed-new-prelaunch-mutated.json").open("x", encoding="utf-8") as f:
            json.dump({"schema":"gf-e2e-installed-prelaunch-tampered/1",
                       "validated_installed_package_before_mutation":True,
                       "mutation":"append-one-byte","actual_sha256":actual,
                       "expected_sha256":expected,
                       "same_selected_new_observer":True,
                       "test_only":True,"operational_ready":False},f)
            f.flush()
            os.fsync(f.fileno())
    trial_mode = mode in ("--recovery-observe-trial", "--recovery-observe-trial-negative")
    if trial_mode:
        require(report_slot == "new" and generation == 2,
                "TRIAL_REQUIRES_L18_SELECTED_NEW")
    trial_context = (trial_registry_fixture(trial_marker_path(src))
                     if mode == "--recovery-observe-trial"
                     else nullcontext("GF-PHASE1-INVENTORY-" + secrets.token_hex(12).upper()))
    with trial_context as expected_name:
        observation = observed_process(
            directory, files["GerenciadorFinal.exe"]["sha256"],
            expected_inventory_name=expected_name if trial_mode else None)
        observation["trial_registry_seed_created"] = (mode == "--recovery-observe-trial")
        observation["observed_from_live_installation"] = observed_installed_new
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
    if trial_mode:
        health = observation.get("readonly_inventory_search_health", {})
        # A2: the SAME functional oracle applies to seeded and unseeded
        # trials; the setup flag never grants/denies the health verdict.
        # Reject the unseeded positive as inconsistent, not as healthy.
        if (mode == "--recovery-observe-trial-negative" and
                health.get("seeded_inventory_positive_observed") is True):
            require(False, "TRIAL_UNSEEDED_ITEM_UNEXPECTEDLY_VISIBLE")
        require(health.get("seeded_inventory_positive_observed") is True,
                "TRIAL_EXPECTED_ITEM_NOT_OBSERVED")
        require(health.get("seeded_inventory_negative_control_observed") is True,
                "TRIAL_NEGATIVE_SEARCH_CONTROL_NOT_OBSERVED")
    validate_package(directory, files)
    if post_l18_selected_observation:
        result_path = Path(os.environ["RUNNER_TEMP"]) / (
            "gf-e2e-postexit-selected-new-ui.json")
        require(not result_path.exists() and
                observation.get("observed_from_live_installation") is True and
                observation.get("actual_image_path_matches") is True and
                observation.get("image_on_disk_sha_matches_manifest") is True and
                observation.get("graceful_shutdown_verified_for_owned_process") is True and
                observation.get("readonly_inventory_search_health", {}).get(
                    "minimum_readonly_health_contract_observed") is True,
                "POST_L18_FUNCTIONAL_HEALTH_NOT_OBSERVED")
        result_path.write_text(json.dumps({
            "schema": "gf-e2e-postexit-selected-new-ui/1",
            "selector": "new", "generation": 2,
            "new_started_from_original_portable_install_path": True,
            "exact_new_executable_hash_checked": True,
            "readonly_functional_ui_observed": True,
            "graceful_exit_observed": True,
            "test_only": True, "operational_ready": False,
            "automatic_recovery_authorized": False,
        }, sort_keys=True), encoding="utf-8")
        print("GF_E2E_POST_L18_SELECTED_NEW_FUNCTIONAL "
              "selector=new/2 install=NEW ui=READONLY_HEALTHY "
              "independent_unselected_launch=false HOLD=true READY=false")
    return 0


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--trial-fixture-cleanup":
        result = cleanup_trial_marker(Path(sys.argv[2]))
        print("GF_PHASE1_TRIAL_CLEANUP " + result)
        sys.exit(0 if result in ("NO_MARKER", "NOT_CREATED",
                                "CLEANED_EXACT_FIXTURE") else 11)
    if sys.argv[1:] == ["--data-only-launch-guard-test"]:
        sys.exit(test_data_only_launch_guard())
    if len(sys.argv) == 3 and sys.argv[1] in (
            "--recovery-verify", "--recovery-observe", "--out-of-band-new-observe",
        "--recovery-observe-trial", "--recovery-observe-trial-negative"):
        sys.exit(recovery_tool(sys.argv[1], sys.argv[2]))
    sys.exit(main())
