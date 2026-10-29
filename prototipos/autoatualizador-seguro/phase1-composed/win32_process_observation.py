"""External Win32 observation of an actual GF process in disposable CI.

Cannot authenticate WinForms application health. A HWND/ping is not READY.
Only tries WM_CLOSE on the real process/window, never on arbitrary PIDs.
"""
from __future__ import annotations

import ctypes
from ctypes import wintypes as w
import hashlib
import os
import subprocess
from pathlib import Path
import time

_u = ctypes.WinDLL("user32", use_last_error=True)
_k = ctypes.WinDLL("kernel32", use_last_error=True)
WINFUNCTYPE = ctypes.WINFUNCTYPE

_enum = _u.EnumWindows
_enum.argtypes = [WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM), w.LPARAM]
_enum.restype = w.BOOL
_pid_for = _u.GetWindowThreadProcessId
_pid_for.argtypes = [w.HWND, ctypes.POINTER(w.DWORD)]
_pid_for.restype = w.DWORD
_get_title = _u.GetWindowTextW
_get_title.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
_get_title.restype = ctypes.c_int
_send = _u.SendMessageTimeoutW
_send.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM,
                  w.UINT, w.UINT, ctypes.POINTER(ctypes.c_size_t)]
_send.restype = w.LPARAM
_post = _u.PostMessageW
_post.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
_post.restype = w.BOOL
_is_window = _u.IsWindow
_is_window.argtypes = [w.HWND]
_is_window.restype = w.BOOL
_is_visible = _u.IsWindowVisible
_is_visible.argtypes = [w.HWND]
_is_visible.restype = w.BOOL
_open_proc = _k.OpenProcess
_open_proc.argtypes = [w.DWORD, w.BOOL, w.DWORD]
_open_proc.restype = w.HANDLE
_query_image = _k.QueryFullProcessImageNameW
_query_image.argtypes = [w.HANDLE, w.DWORD, w.LPWSTR, ctypes.POINTER(w.DWORD)]
_query_image.restype = w.BOOL
_close_handle = _k.CloseHandle
_close_handle.argtypes = [w.HANDLE]
_close_handle.restype = w.BOOL
_session = _k.ProcessIdToSessionId
_session.argtypes = [w.DWORD, ctypes.POINTER(w.DWORD)]
_session.restype = w.BOOL
_open_input = _u.OpenInputDesktop
_open_input.argtypes = [w.DWORD, w.BOOL, w.DWORD]
_open_input.restype = w.HANDLE
_close_desktop = _u.CloseDesktop
_close_desktop.argtypes = [w.HANDLE]
_close_desktop.restype = w.BOOL


def process_image_path(pid: int):
    handle = _open_proc(0x1000, False, pid)  # QUERY_LIMITED_INFORMATION
    if not handle:
        return None
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        length = w.DWORD(len(buffer))
        if not _query_image(handle, 0, buffer, ctypes.byref(length)):
            return None
        return buffer.value
    finally:
        _close_handle(handle)


def desktop_observation():
    session_id = w.DWORD()
    has_session = bool(_session(os.getpid(), ctypes.byref(session_id)))
    handle = _open_input(0, False, 0x0001)  # DESKTOP_READOBJECTS
    has_input = bool(handle)
    if handle:
        _close_desktop(handle)
    # Session zero or an inaccessible input desktop cannot establish a
    # user-visible interactive WinForms session.
    return {"runner_session_id": session_id.value if has_session else None,
            "input_desktop_available": has_input,
            "interactive_user_desktop_verified": bool(has_session and
                                                      session_id.value != 0 and has_input)}


def own_main_windows(pid: int):
    results = []
    callback_type = WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)

    @callback_type
    def inspect(hwnd, _):
        owner = w.DWORD()
        thread_id = _pid_for(hwnd, ctypes.byref(owner))
        if owner.value != pid or not thread_id:
            return True
        text = ctypes.create_unicode_buffer(512)
        _get_title(hwnd, text, len(text))
        if text.value.startswith("Gerenciador Final"):
            results.append({"hwnd": int(hwnd), "owner_pid": owner.value,
                            "owner_thread_id": int(thread_id),
                            "visible": bool(_is_visible(hwnd))})
        return True

    _enum(inspect, 0)
    return results


def message_pump_ping(hwnd: int):
    if not _is_window(hwnd):
        return False
    outcome = ctypes.c_size_t()
    # Hung window must never cause the coordinator to hang indefinitely.
    return bool(_send(hwnd, 0, 0, 0, 0x0002, 1500, ctypes.byref(outcome)))


def observe_real_gf(proc, executable: Path, expected_sha: str,
                    seconds: float = 12.0):
    """Returns process-bound observations, NOT a WinForms READY verdict.

    A noninteractive Windows service session may expose no usable HWND.
    In that case graceful shutdown is explicitly unverified and the
    caller must forcibly terminate only its own test child for cleanup.
    """
    report = desktop_observation()
    report.update({"actual_image_path_matches": False,
                   "image_on_disk_sha_matches_manifest": False,
                   "main_form_hwnd_observed": False,
                   "main_form_message_loop_responded": False,
                   "graceful_close_requested": False,
                   "graceful_exit_observed": False,
                   "ready_verified": False,
                   "application_functional_health_verified": False,
                   "health_contract": "WINDOW_MESSAGE_PUMP_NOT_APPLICATION_READY"})
    image = process_image_path(proc.pid)
    report["process_image_query_available"] = image is not None
    if image is not None:
        try:
            report["actual_image_path_matches"] = os.path.samefile(image, executable)
            if report["actual_image_path_matches"]:
                report["image_on_disk_sha_matches_manifest"] = (
                    hashlib.sha256(executable.read_bytes()).hexdigest() == expected_sha)
        except (OSError, ValueError):
            pass
    # A path/hash of disk bytes is NOT a cryptographic measurement of the
    # loaded executable image, and has a race before CreateProcess.
    deadline = time.monotonic() + seconds
    window = None
    while proc.poll() is None and time.monotonic() < deadline:
        candidates = own_main_windows(proc.pid)
        if candidates:
            window = candidates[0]
            break
        time.sleep(0.25)
    if window:
        hwnd = window["hwnd"]
        report["main_form_hwnd_observed"] = True
        report["main_form_owner_pid_matches"] = window["owner_pid"] == proc.pid
        report["main_form_visible"] = window["visible"]
        report["main_form_message_loop_responded"] = message_pump_ping(hwnd)
        # WM_CLOSE is only sent to a verified window of THIS child, not to
        # the user's other programs or an unrelated desktop window.
        if (report["main_form_message_loop_responded"] and
                report["actual_image_path_matches"] and
                report["image_on_disk_sha_matches_manifest"] and
                proc.poll() is None):
            report["graceful_close_requested"] = bool(_post(hwnd, 0x0010, 0, 0))
            if report["graceful_close_requested"]:
                try:
                    proc.wait(timeout=12)
                except subprocess.TimeoutExpired:
                    pass
                report["graceful_exit_observed"] = (
                    proc.poll() == 0)
    report["early_exit_code"] = proc.poll()
    report["reason"] = ("GRACEFUL_MAIN_FORM_EXIT_OBSERVED"
                        if report["graceful_exit_observed"] else
                        "NO_MAIN_HWND_OR_GRACEFUL_CLOSE_UNPROVEN")
    return report
