"""Disposable Windows observation of GF OLD MainForm after real TCP cut.

Uses the existing win32_process_observation functions to inspect ONE owned
WinForms process; never confuses PID/HWND/receipt with updater READY.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from win32_process_observation import (
    own_main_windows, process_image_path, message_pump_ping,
    read_only_search_round_trip, desktop_observation
)


def require(ok, code):
    if not ok:
        raise RuntimeError(code)



def consent_dialogs_for_pid(pid):
    """Read-only check: separate real first-run privacy prompt from update failure."""
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    from ctypes import wintypes as w
    callback = ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    get_owner = user32.GetWindowThreadProcessId
    get_owner.argtypes = [w.HWND, ctypes.POINTER(w.DWORD)]
    get_owner.restype = w.DWORD
    get_text = user32.GetWindowTextW
    get_text.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
    get_text.restype = ctypes.c_int
    is_visible = user32.IsWindowVisible
    is_visible.argtypes = [w.HWND]
    is_visible.restype = w.BOOL
    enum = user32.EnumWindows
    enum.argtypes = [callback, w.LPARAM]
    enum.restype = w.BOOL
    seen = []
    @callback
    def visit(hwnd, _):
        owner = w.DWORD()
        get_owner(hwnd, ctypes.byref(owner))
        if owner.value == pid and is_visible(hwnd):
            title = ctypes.create_unicode_buffer(256)
            get_text(hwnd, title, len(title))
            if title.value == "Privacidade e diagnóstico colaborativo":
                seen.append(int(hwnd))
        return True
    enum(visit, 0)
    return seen

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--exe", type=Path, required=True)
    p.add_argument("--shown", type=Path, required=True)
    p.add_argument("--failure", type=Path, required=True)
    p.add_argument("--transport", type=Path, required=True)
    p.add_argument("--result", type=Path, required=True)
    p.add_argument("--normal", action="store_true",
                   help="Normal MainForm launch without --tray or ShowWindow")
    args = p.parse_args()
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true"
            and os.environ.get("GITHUB_REPOSITORY") ==
            "walissonminel-1991/gerenciador-final",
            "GUI_TEST_DISPOSABLE_RUNNER_ONLY")
    runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    image = args.exe.resolve(strict=True)
    require(image.parent == runner / ("gf-e2e-http-normal-old" if args.normal
                                       else "gf-e2e-http-ui-old") and
            image.name == "GerenciadorFinal.exe", "GUI_TEST_OLD_NOT_ISOLATED")
    require(args.shown.parent == runner and args.failure.parent == runner
            and args.transport.parent == runner and args.result.parent == runner,
            "GUI_TEST_RECEIPTS_NOT_IN_RUNNER")
    require(all(not x.exists() for x in (args.shown, args.failure, args.result)),
            "GUI_TEST_RECEIPTS_NOT_FRESH")
    image_hash = hashlib.sha256(image.read_bytes()).hexdigest()
    env = dict(os.environ)
    env["GF_E2E_STARTUP_DRAFT_PROBE"] = "1"
    env["GF_E2E_HTTP_CUT_NEGATIVE"] = "1"
    env["GF_E2E_HTTP_UI_NEGATIVE"] = "0" if args.normal else "1"
    env["GF_E2E_HTTP_UI_NORMAL_NEGATIVE"] = "1" if args.normal else "0"
    for key in ("GF_E2E_HANDOFF_L18", "GF_E2E_SELF_GRACEFUL_CLOSE",
                "GF_E2E_SELECTED_NEW_CONTINUATION",
                "GF_E2E_SWAP_CUT_AFTER_OLD_MOVE"):
        env[key] = "0"
    require(env.get("GF_E2E_HTTP_CUT_PORT", "").isdigit(),
            "GUI_TEST_HTTP_LOOPBACK_NOT_PINNED")
    # Do not hide or artificially show the MainForm in the normal case.
    require(not args.normal or os.environ.get("LOCALAPPDATA"),
            "NORMAL_FIRST_RUN_LOCAL_APP_DATA_UNAVAILABLE")
    consent_file = (Path(os.environ["LOCALAPPDATA"]) /
                    "GerenciadorFinal" / "Privacidade" / "consentimento-v1.json") if args.normal else None
    if args.normal:
        require(not consent_file.exists(), "NORMAL_FIRST_RUN_CONSENT_ALREADY_STORED")
    proc = subprocess.Popen([str(image)] if args.normal else
                            [str(image), "--tray"], cwd=str(image.parent),
                            env=env, stdin=subprocess.DEVNULL,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
    verified = False
    try:
        deadline = time.monotonic() + 100
        while time.monotonic() < deadline:
            require(proc.poll() is None, "GF_OLD_EXITED_DURING_DOWNLOAD_FAILURE")
            if args.normal:
                require(not consent_dialogs_for_pid(proc.pid),
                        "NORMAL_FIRST_RUN_CONSENT_MODAL_PRESENT_NOT_HTTP_FAILURE")
                require(not consent_file.exists(),
                        "NORMAL_FIRST_RUN_CONSENT_PREFERENCE_CHANGED")
            if args.failure.exists() and args.transport.exists() and args.shown.exists():
                break
            time.sleep(0.15)
        require(args.failure.exists() and args.transport.exists()
                and args.shown.exists(), "GUI_HTTP_CUT_OR_MAINFORM_NOT_OBSERVED")
        shown = json.loads(args.shown.read_bytes())
        failure = json.loads(args.failure.read_bytes())
        transport = json.loads(args.transport.read_bytes())
        require(shown == {
            "schema": "gf-e2e-startup-shown/1",
            "startup_event": "MainForm.Shown",
            "process_image": "GerenciadorFinal.exe",
            "real_mainform_handle_created": True,
            "test_only": True,
            "operational_ready": False,
        }, "GUI_NOT_ORIGINAL_MAINFORM_SHOWN")
        require(failure.get("schema") == "gf-e2e-old-blocked/2"
                and failure.get("stage") == "asset_download"
                and failure.get("reason") in (
                    "HttpIOException", "IOException", "HttpRequestException")
                and failure.get("no_auto_recovery") is True
                and failure.get("no_token_logged") is True,
                "GUI_TRANSPORT_FAILURE_NOT_FAIL_CLOSED")
        require(transport.get("schema") == "gf-e2e-http-disconnect/1"
                and transport.get("http_status_sent") == 200
                and transport.get("content_length_declared") == 57695630
                and transport.get("bytes_sent_before_tcp_disconnect") == 1048576
                and transport.get("socket_shutdown_in_body") is True
                and transport.get("bearer_not_received") is True
                and transport.get("loopback_only") is True,
                "GUI_REAL_TCP_DISCONNECT_NOT_PROVEN")
        # The receipt is only the trigger to inspect. The process/window
        # and real functional inventory search must be observed INDEPENDENTLY
        # after it, using the pre-existing Win32 observer.
        require(proc.poll() is None, "GUI_GF_OLD_EXITED_AFTER_HTTP_CUT")
        deadline = time.monotonic() + 25
        hwnd = None
        while time.monotonic() < deadline and proc.poll() is None:
            matches = own_main_windows(proc.pid)
            if len(matches) == 1:
                hwnd = matches[0]["hwnd"]
                break
            time.sleep(0.2)
        require(hwnd is not None, "GUI_ORIGINAL_MAINFORM_HWND_MISSING")
        real_image = process_image_path(proc.pid)
        require(real_image is not None
                and os.path.samefile(real_image, image)
                and hashlib.sha256(image.read_bytes()).hexdigest() == image_hash,
                "GUI_PROCESS_IMAGE_IDENTITY_UNPROVEN")
        # In normal mode the OWNED window must be natively visible;
        # do NOT use ShowWindow or tray startup. In the older lab case
        # preserve the existing presentation-only ShowWindow step.
        user32 = ctypes.WinDLL("user32", use_last_error=True)
        show = user32.ShowWindow
        show.argtypes = [ctypes.c_void_p, ctypes.c_int]
        show.restype = ctypes.c_bool
        is_visible = user32.IsWindowVisible
        is_visible.argtypes = [ctypes.c_void_p]
        is_visible.restype = ctypes.c_bool
        if not args.normal:
            show(hwnd, 5)  # Earlier --tray lab case only.
        require(bool(is_visible(hwnd)),
                "NORMAL_MAINFORM_NOT_VISIBLE_WITHOUT_INTERVENTION"
                if args.normal else "GUI_GF_OLD_NOT_VISIBLE_AFTER_ERROR")
        if args.normal:
            require(not consent_dialogs_for_pid(proc.pid) and
                    not consent_file.exists(),
                    "NORMAL_FIRST_RUN_CONSENT_MODAL_OR_PREFERENCE_CHANGED")
        require(message_pump_ping(hwnd), "GUI_GF_OLD_NOT_RESPONDING_AFTER_ERROR")
        # Allow background asynchronous registry inventory startup to settle.
        deadline = time.monotonic() + 25
        health = None
        while time.monotonic() < deadline:
            require(proc.poll() is None and message_pump_ping(hwnd),
                    "GUI_GF_OLD_EXITED_OR_UNRESPONSIVE_DURING_INVENTORY")
            health = read_only_search_round_trip(hwnd, proc.pid)
            if health.get("minimum_readonly_health_contract_observed"):
                break
            time.sleep(0.75)
        require(health is not None and
                health.get("minimum_readonly_health_contract_observed") is True
                and health.get("baseline_inventory_nonzero") is True
                and health.get("search_nonmatch_empty_state_observed") is True
                and health.get("search_restored") is True,
                "GUI_GF_OLD_INVENTORY_SEARCH_NOT_FUNCTIONAL")
        # The HWND is STILL alive and responsive AFTER the functional check.
        time.sleep(1)
        require(proc.poll() is None and bool(is_visible(hwnd))
                and message_pump_ping(hwnd),
                "GUI_GF_OLD_DID_NOT_REMAIN_OPEN_AFTER_ERROR")
        desktop = desktop_observation()
        require(desktop["interactive_user_desktop_verified"],
                "GUI_NOT_INTERACTIVE_DESKTOP")
        report = {
            "schema": ("gf-e2e-http-cut-mainform-normal/1" if args.normal
                       else "gf-e2e-http-cut-mainform-healthy/1"),
            "real_mainform_shown": True,
            "real_network_tcp_disconnect_observed": True,
            "original_old_process_alive_after_failure": True,
            "process_image_verified": True,
            "owned_mainform_hwnd_visible_after_failure": True,
            "ui_message_pump_responded_after_failure": True,
            "inventory_nonzero": True,
            "readonly_search_nonmatch_seen": True,
            "readonly_search_restored": True,
            "old_ui_healthy_after_http_cut": True,
            "normal_launch_without_tray": bool(args.normal),
            "no_forced_showwindow": bool(args.normal),
            "local_beta_consent_dialog_not_required": bool(args.normal),
            "consent_dialog_seen": False,
            "consent_preference_untouched": bool(args.normal and not consent_file.exists()) if args.normal else False,
            "new_executed": False,
            "l18_handoff_called": False,
            "retry_authorized": False,
            "rollback_authorized": False,
            "cleanup_authorized": False,
            "operational_ready": False,
            "test_only": True,
        }
        with args.result.open("x", encoding="utf-8") as output:
            json.dump(report, output, sort_keys=True)
            output.flush()
            os.fsync(output.fileno())
        verified = True
        print(("GF_E2E_NORMAL_MAINFORM_HTTP_CUT_HEALTH_PASS " if args.normal
               else "GF_E2E_MAINFORM_HTTP_CUT_HEALTH_PASS ") +
              "tcp_disconnect=REAL_LOOPBACK mainform=VISIBLE+RESPONSIVE "
              "inventory=NONZERO search=EMPTY+RESTORED "
              "GF_OLD_still_running=true NEW_executed=false "
              "L18_not_invoked=true READY=false", flush=True)
    finally:
        # Only test-process cleanup, strictly using the retained process
        # handle / verified HWND. Never report forced cleanup as recovery.
        if proc.poll() is None:
            owned = own_main_windows(proc.pid)
            if len(owned) == 1:
                user32 = ctypes.WinDLL("user32", use_last_error=True)
                post = user32.PostMessageW
                post.argtypes = [ctypes.c_void_p, ctypes.c_uint,
                                 ctypes.c_size_t, ctypes.c_ssize_t]
                post.restype = ctypes.c_bool
                post(owned[0]["hwnd"], 0x0010, 0, 0)  # WM_CLOSE
                try:
                    proc.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    pass
        if proc.poll() is None:
            proc.kill()  # Owned disposable test child only; NOT rollback.
        proc.wait(timeout=15)
        if not verified:
            print("GF_E2E_MAINFORM_HTTP_CUT_NOT_PROVEN test_only=true", flush=True)


if __name__ == "__main__":
    main()
