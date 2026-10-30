"""A3/A4 scoped disposable Windows Registry trials. Never run on personal PC."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import winreg

import phase1_lab as lab


def require(x, message):
    if not x:
        raise AssertionError("GF_PHASE1_" + message)


def subkey(name):
    return ("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + name)


def key_exists(name):
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey(name), 0,
                            winreg.KEY_READ | winreg.KEY_WOW64_64KEY):
            return True
    except FileNotFoundError:
        return False


def scoped(name):
    src = Path(tempfile.mkdtemp(prefix="gf-u2-a34-", dir=tempfile.gettempdir())) / name
    src.mkdir()
    (src / ".gf-u2-fixture").write_text(
        "GF-U2-INERT-STATE-TESTS-NO-PRODUCT\n", encoding="utf-8")
    return src


def test_atomic_existing_key_refusal():
    # Deterministically force a collision at the *native create* operation,
    # without trying to win an unstable timing race on a real user machine.
    value = "A" * 24
    name = "GF-PHASE1-INVENTORY-" + value
    src = scoped("atomic")
    marker = lab.trial_marker_path(src)
    rights = winreg.KEY_READ | winreg.KEY_WRITE | winreg.KEY_WOW64_64KEY
    with winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER, subkey(name), 0, rights) as key:
        winreg.SetValueEx(key, "UninstallString", 0, winreg.REG_SZ, "PRESERVE_ME")
    original = lab.secrets.token_hex
    lab.secrets.token_hex = lambda count: ("A" * 24 if count == 12 else "b" * 64)
    try:
        try:
            with lab.trial_registry_fixture(marker):
                raise AssertionError("collision unexpectedly accepted")
        except RuntimeError as error:
            require("TRIAL_REGISTRY_ALREADY_EXISTS" in str(error),
                    "A3_COLLISION_NOT_REFUSED")
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey(name), 0, rights) as key:
            require(winreg.QueryValueEx(key, "UninstallString")[0] ==
                    "PRESERVE_ME", "A3_EXISTING_DATA_REPLACED")
            require(winreg.QueryInfoKey(key)[1] == 1,
                    "A3_EXISTING_DATA_MODIFIED")
    finally:
        lab.secrets.token_hex = original
        # Only this test-created foreign key is removed, after checking it
        # still has the exact value written above. No prefix sweeps.
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey(name), 0, rights) as key:
            require(winreg.QueryInfoKey(key)[1] == 1 and
                    winreg.QueryValueEx(key, "UninstallString")[0] == "PRESERVE_ME",
                    "A3_TEST_KEY_CHANGED_PRESERVE")
        winreg.DeleteKeyEx(winreg.HKEY_CURRENT_USER, subkey(name),
                           access=winreg.KEY_WOW64_64KEY, reserved=0)
    # Marker remains after an intentional collision: it is not authority to
    # delete a pre-existing key, even if it knows the same name.
    require(lab.cleanup_trial_marker(src) ==
            "HOLD_MUTATED_FIXTURE_PRESERVED" or
            not key_exists(name), "A3_MARKER_AUTHORIZED_FOREIGN_CLEANUP")
    return "A3_NATIVE_EXISTING_KEY_NOT_OVERWRITTEN"


def test_delete_original_registry_handle():
    # A3: validate on REAL Windows that an opened-key HKEY, subkey=""
    # deletes that same object, rather than resolving a later path.
    name = "GF-PHASE1-INVENTORY-" + os.urandom(12).hex().upper()
    rights = winreg.KEY_READ | winreg.KEY_WRITE | winreg.KEY_WOW64_64KEY
    with winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER, subkey(name),
                            0, rights) as created:
        winreg.SetValueEx(created, "DisplayName", 0, winreg.REG_SZ, name)
        winreg.DeleteKeyEx(created, "", access=winreg.KEY_WOW64_64KEY,
                           reserved=0)
    require(not key_exists(name), "A3_RETAINED_HANDLE_DELETE_FAILED")
    return "A3_RETAINED_HANDLE_SELF_DELETE_WINDOWS"


def test_mutation_preserved():
    src = scoped("mutated")
    marker = lab.trial_marker_path(src)
    name = None
    try:
        with lab.trial_registry_fixture(marker) as active_name:
            name = active_name
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, subkey(name), 0,
                                winreg.KEY_WRITE | winreg.KEY_WOW64_64KEY) as k:
                winreg.SetValueEx(k, "ForeignValue", 0, winreg.REG_SZ, "KEEP")
    except RuntimeError as error:
        require("TRIAL_REGISTRY_MUTATED_PRESERVE_FOR_DIAGNOSIS" in str(error),
                "A3_MUTATION_NOT_REFUSED")
    require(name is not None and key_exists(name), "A3_MUTATION_DELETED")
    require(lab.cleanup_trial_marker(src) ==
            "HOLD_MUTATED_FIXTURE_PRESERVED", "A3_SUPERVISOR_CLEANED_MUTATION")
    require(key_exists(name), "A3_MUTATION_NOT_PRESERVED")
    # Intentionally retained only in disposable CI; never sweep HKCU.
    return "A3_MUTATED_KEY_PRESERVED_FOR_DIAGNOSIS"


def run_worker(src):
    with lab.trial_registry_fixture(lab.trial_marker_path(src)):
        print("GF_PHASE1_FIXTURE_ACTIVE", flush=True)
        while True:
            time.sleep(0.2)


def test_forced_kill_supervised():
    src = scoped("forced")
    process = subprocess.Popen([sys.executable, __file__, "--worker", str(src)],
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True)
    name = None
    try:
        # Wait on bounded observable state BEFORE reading the worker pipe.
        # A crash before the ACTIVE line must fail promptly, not hang CI.
        until = time.monotonic() + 20
        while time.monotonic() < until:
            if process.poll() is not None:
                raise AssertionError("A4_WORKER_EXITED_BEFORE_ACTIVE")
            if lab.trial_marker_path(src).exists():
                data = json.loads(lab.trial_marker_path(src).read_text(
                    encoding="utf-8"))
                name = data["name"]
                if key_exists(name):
                    break
            time.sleep(0.1)
        require(name is not None and key_exists(name), "A4_FIXTURE_NOT_ACTIVE")
        line = process.stdout.readline().strip()
        require(line == "GF_PHASE1_FIXTURE_ACTIVE", "A4_WORKER_NOT_ACTIVE")
        process.kill()
        process.wait(timeout=10)
        require(key_exists(name), "A4_EXPECTED_FORCED_KILL_RESIDUE_NOT_FOUND")
        result = lab.cleanup_trial_marker(src)
        require(result == "CLEANED_EXACT_FIXTURE", "A4_SUPERVISOR_CLEANUP_FAILED")
        require(not key_exists(name) and
                not lab.trial_marker_path(src).exists(),
                "A4_CLEANUP_NOT_CONFIRMED")
    finally:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=10)
    return "A4_FORCED_PROCESS_KILL_SUPERVISOR_CLEANUP"


if __name__ == "__main__":
    lab._trial_scope()
    if len(sys.argv) == 3 and sys.argv[1] == "--worker":
        run_worker(Path(sys.argv[2]))
    elif len(sys.argv) == 1:
        cases = [test_atomic_existing_key_refusal(),
                 test_delete_original_registry_handle(),
                 test_mutation_preserved(),
                 test_forced_kill_supervised()]
        print("GF_PHASE1_HKCU_ADVERSARIAL " + json.dumps(
            {"status": "passed", "cases": cases, "count": len(cases),
             "scope": "WINDOWS_DISPOSABLE_CI_ONLY",
             "same_user_global_isolation_proven": False,
             "name_based_delete_toctou_eliminated": False,
             "operational_ready": False, "automatic_recovery_authorized": False},
            sort_keys=True))
    else:
        raise SystemExit("Usage: no arguments or --worker <scope>")
