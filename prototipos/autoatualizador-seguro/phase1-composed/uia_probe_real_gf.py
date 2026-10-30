"""External UIA/MSAA research against real GF, disposable CI only.
No recovery authority, screenshot, export of installed app names or product edits.
"""
from __future__ import annotations
import ctypes
from ctypes import wintypes as w
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from phase1_lab import trial_registry_fixture, require
from win32_process_observation import (
    _enum_child, _pid_for, _get_title, _get_class, _get_parent,
    _send, _post, own_main_windows, message_pump_ping, process_image_path,
    WINFUNCTYPE,
)

def owned_controls(hwnd, pid):
    found = []
    cbtype = WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    @cbtype
    def callback(child, _):
        owner = w.DWORD()
        _pid_for(child, ctypes.byref(owner))
        if owner.value != pid:
            return True
        name = ctypes.create_unicode_buffer(512)
        klass = ctypes.create_unicode_buffer(180)
        _get_title(child, name, len(name))
        _get_class(child, klass, len(klass))
        found.append((int(child), name.value, klass.value,
                      int(_get_parent(child) or 0)))
        return True
    _enum_child(hwnd, callback, 0)
    return found

def search_edit(controls):
    labels = [c for c in controls if c[1] == "Pesquisar:"]
    if len(labels) != 1:
        return None
    editors = [c for c in controls if c[3] == labels[0][3]
               and "EDIT" in c[2].upper()]
    return editors[0][0] if len(editors) == 1 else None

def set_search(edit, query):
    buff = ctypes.create_unicode_buffer(query)
    result = ctypes.c_size_t()
    return bool(_send(edit, 0x000C, 0,
                      ctypes.cast(buff, ctypes.c_void_p).value,
                      0x0002, 2000, ctypes.byref(result)))

def get_search(edit):
    buff = ctypes.create_unicode_buffer(128)
    result = ctypes.c_size_t()
    sent = _send(edit, 0x000D, len(buff),
                 ctypes.cast(buff, ctypes.c_void_p).value,
                 0x0002, 2000, ctypes.byref(result))
    return buff.value if sent else None

def observe_uia(hwnd, pid, query):
    ps = Path(__file__).with_name("uia_item_discovery.ps1")
    exe = (Path(os.environ.get("SystemRoot", r"C:\Windows"))
           / "System32" / "WindowsPowerShell" / "v1.0" / "powershell.exe")
    fallback = {"probe_completed": False, "positive_ui_proof": False,
                "operational_ready": False, "automatic_recovery_authorized": False,
                "reason": "PROBE_NOT_AVAILABLE"}
    try:
        child = subprocess.run([
            str(exe), "-NoLogo", "-NoProfile", "-NonInteractive",
            "-File", str(ps), "-WindowHandle", str(hwnd),
            "-ExpectedPid", str(pid), "-ExpectedName", query,
        ], stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
           stderr=subprocess.DEVNULL, text=True, timeout=14)
        if child.returncode == 0:
            value = json.loads(child.stdout.strip().splitlines()[-1])
            if (value.get("schema") == "gf-phase1-uia-discovery/1"
                    and value.get("positive_ui_proof") is False
                    and value.get("operational_ready") is False
                    and value.get("automatic_recovery_authorized") is False):
                value["probe_completed"] = True
                return value
    except (OSError, ValueError, IndexError, subprocess.TimeoutExpired):
        pass
    return fallback

def investigate(exe):
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true"
            and os.environ.get("RUNNER_TEMP"), "UIA_DISPOSABLE_SCOPE")
    exe = Path(exe).resolve(strict=True)
    temp = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    require(exe.is_relative_to(temp) and exe.name == "GerenciadorFinal.exe",
            "UIA_EXECUTABLE_SCOPE")
    digest = hashlib.sha256(exe.read_bytes()).hexdigest()
    report = {
        "schema": "gf-phase1-uia-real-gf-feasibility/1",
        "scope": "DISPOSABLE_WINDOWS_REAL_GF_TRIAL_NOT_PRODUCTION",
        "gf_image_and_process_verified": False,
        "probe_on_real_gf": False,
        "positive_query_echo_verified": False,
        "negative_query_echo_verified": False,
        "positive_uia": None, "negative_uia": None,
        "experimental_positive_ui_proof": False,
        "operational_ready": False, "automatic_recovery_authorized": False,
        "reason": "NOT_OBSERVED",
    }
    with trial_registry_fixture() as name:
        env = dict(os.environ)
        env["GfBetaEndpoint"] = ""
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        proc = subprocess.Popen(
            [str(exe), "--tray"], cwd=str(exe.parent),
            env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        main = None
        try:
            deadline = time.monotonic() + 22
            while proc.poll() is None and time.monotonic() < deadline:
                windows = own_main_windows(proc.pid)
                if windows:
                    main = windows[0]["hwnd"]
                    break
                time.sleep(.25)
            if main is None:
                report["reason"] = "MAIN_WINDOW_UNAVAILABLE"
                return report
            time.sleep(10)  # asynchronous inventory startup
            if proc.poll() is not None or not message_pump_ping(main):
                report["reason"] = "MAIN_WINDOW_NOT_RESPONDING"
                return report
            try:
                path_ok = os.path.samefile(process_image_path(proc.pid), exe)
            except (OSError, ValueError, TypeError):
                path_ok = False
            report["gf_image_and_process_verified"] = (
                path_ok and hashlib.sha256(exe.read_bytes()).hexdigest() == digest)
            if not report["gf_image_and_process_verified"]:
                report["reason"] = "IMAGE_IDENTITY_NOT_VERIFIED"
                return report
            edit = search_edit(owned_controls(main, proc.pid))
            if edit is None:
                report["reason"] = "OWNED_SEARCH_NOT_FOUND"
                return report
            report["probe_on_real_gf"] = True
            try:
                if set_search(edit, name):
                    time.sleep(1.5)
                    report["positive_query_echo_verified"] = get_search(edit) == name
                    if report["positive_query_echo_verified"]:
                        report["positive_uia"] = observe_uia(main, proc.pid, name)
                absent = name + "-ABSENT"
                if set_search(edit, absent):
                    time.sleep(1.5)
                    report["negative_query_echo_verified"] = get_search(edit) == absent
                    if report["negative_query_echo_verified"]:
                        report["negative_uia"] = observe_uia(main, proc.pid, absent)
                report["reason"] = "POSITIVE_UI_ITEM_NOT_QUALIFIED"
            finally:
                set_search(edit, "")
        finally:
            if main is not None and proc.poll() is None:
                _post(main, 0x0010, 0, 0)
                try:
                    proc.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    pass
            if proc.poll() is None:
                proc.kill()  # retained process handle only
            proc.wait(timeout=10)
    return report

if __name__ == "__main__":
    require(len(sys.argv) == 2, "UIA_ARGUMENTS")
    result = investigate(sys.argv[1])
    print("GF_PHASE1_UIA_REAL_GF_FEASIBILITY " + json.dumps(result, sort_keys=True))
