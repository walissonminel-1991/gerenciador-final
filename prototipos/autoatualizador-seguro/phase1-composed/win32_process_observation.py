"""External Win32 observation of an actual GF process in disposable CI.

Cannot authenticate WinForms application health. A HWND/ping is not READY.
Only tries WM_CLOSE on the real process/window, never on arbitrary PIDs.
"""
from __future__ import annotations

import ctypes
from ctypes import wintypes as w
import hashlib
import os
import secrets
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
_is_enabled = _u.IsWindowEnabled
_is_enabled.argtypes = [w.HWND]
_is_enabled.restype = w.BOOL
_get_parent = _u.GetParent
_get_parent.argtypes = [w.HWND]
_get_parent.restype = w.HWND
_get_class = _u.GetClassNameW
_get_class.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
_get_class.restype = ctypes.c_int
_get_style = _u.GetWindowLongW
_get_style.argtypes = [w.HWND, ctypes.c_int]
_get_style.restype = ctypes.c_long

_enum_child = _u.EnumChildWindows
_enum_child.argtypes = [w.HWND, WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM), w.LPARAM]
_enum_child.restype = w.BOOL
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


def observe_read_only_ui_controls(main_hwnd: int, expected_pid: int):
    """Classify only non-sensitive known GF UI labels from the real process.

    Never enumerate or report installed application names, click controls,
    fill inputs, read arbitrary user text or use a local log as READY.
    """
    findings = {"header": False, "search_label": False,
                "apps_summary_rendered": False,
                "update_control_present": False,
                "update_control_enabled": False,
                "child_windows_owned": 0,
                "functional_inventory_ui_observed": False}
    callback_type = WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)

    @callback_type
    def inspect(hwnd, _):
        owner = w.DWORD()
        _pid_for(hwnd, ctypes.byref(owner))
        if owner.value != expected_pid:
            return True
        findings["child_windows_owned"] += 1
        title = ctypes.create_unicode_buffer(260)
        _get_title(hwnd, title, len(title))
        value = title.value.strip()
        if value == "GERENCIADOR FINAL":
            findings["header"] = True
        if value == "Pesquisar:":
            findings["search_label"] = True
        if value.startswith("Apps: ") and any(c.isdigit() for c in value[6:]):
            findings["apps_summary_rendered"] = True
        if value == "Atualizar e verificar":
            findings["update_control_present"] = True
            findings["update_control_enabled"] = bool(_is_enabled(hwnd))
        return True

    _enum_child(main_hwnd, inspect, 0)
    findings["functional_inventory_ui_observed"] = all((
        findings["header"], findings["search_label"],
        findings["apps_summary_rendered"],
        findings["update_control_present"],
        findings["update_control_enabled"],
    ))
    return findings


def search_snapshot_proves(rows, actual_text, query, want_empty, edit_hwnd,
                           search_parent, empty_label, inventory_list_hwnd=None):
    """A1/A2: fail closed on missing/contradictory item-level evidence.

    In the current owner-drawn GF list, no independently identified item
    container exposing individual rows exists. The live caller therefore
    passes list_hwnd=None and must return UNPROVEN for positive presence.
    A test fixture can supply a known list HWND to refute the predicate.
    Unrelated STATIC labels cannot be mistaken for inventory rows.
    """
    if actual_text != query:
        return False
    empty = [r for r in rows if r[1] == empty_label]
    if len(empty) != 1:
        return False
    direct = [r for r in rows
              if inventory_list_hwnd is not None
              and r[1] == query and r[4] and r[0] != edit_hwnd
              and "STATIC" in r[2].upper()
              and r[3] == inventory_list_hwnd
              and inventory_list_hwnd != search_parent]
    # A text echo or a missing empty-state control is never item presence.
    return (bool(empty[0][4]) and not direct) if want_empty else (
        not empty[0][4] and len(direct) == 1)


def read_only_search_round_trip(main_hwnd: int, expected_pid: int, expected_inventory_name=None):
    """Exercise the REAL WinForms inventory search, without maintenance.

    This observes a functional state transition (search -> empty match ->
    clear search), not merely PID/window presence or a same-user receipt.
    The nonce is never sent to the network; only boolean outcomes are logged.
    All text except static GF labels stays local and is not reported.
    """
    result = {"search_edit_identified": False,
              "search_nonmatch_empty_state_observed": False,
              "empty_state_label_found": False,
              "empty_state_style_visible": False,
              "nonce_echo_observed": False,
              "search_restored": False,
              "baseline_inventory_nonzero": False,
              "minimum_readonly_health_contract_observed": False}
    callback_type = WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    labels = []
    edits = []
    before_empty = False
    empty_label = "Nenhum aplicativo corresponde à pesquisa."

    def scan():
        current = []
        @callback_type
        def inspect(hwnd, _):
            owner = w.DWORD()
            _pid_for(hwnd, ctypes.byref(owner))
            if owner.value != expected_pid:
                return True
            title = ctypes.create_unicode_buffer(512)
            _get_title(hwnd, title, len(title))
            class_text = ctypes.create_unicode_buffer(180)
            _get_class(hwnd, class_text, len(class_text))
            current.append((int(hwnd), title.value, class_text.value,
                            int(_get_parent(hwnd) or 0),
                            bool(_get_style(hwnd, -16) & 0x10000000)))
            return True
        _enum_child(main_hwnd, inspect, 0)
        return current

    before = scan()
    summary = next((x[1] for x in before if x[1].startswith("Apps: ")), "")
    try:
        count = int(summary.split("Apps: ", 1)[1].split()[0])
        result["baseline_inventory_nonzero"] = count > 0
    except (IndexError, ValueError):
        return result
    label = next((x for x in before if x[1] == "Pesquisar:"), None)
    if not label or not result["baseline_inventory_nonzero"]:
        return result
    # Search for the ONLY WinForms EDIT with the SAME parent as "Pesquisar:".
    # Never type into an unrelated text box or other user's process.
    choices = [x for x in before if x[3] == label[3] and
               "EDIT" in x[2].upper()]
    if len(choices) != 1:
        return result
    edit = choices[0][0]
    result["search_edit_identified"] = True
    nonce = "GF-PHASE1-NO-MATCH-" + secrets.token_hex(12)
    def set_search(text):
        b = ctypes.create_unicode_buffer(text)
        out = ctypes.c_size_t()
        return bool(_send(edit, 0x000C, 0,
                          ctypes.cast(b, ctypes.c_void_p).value,
                          0x0002, 2500, ctypes.byref(out)))
    def read_search():
        # GetWindowTextW does not retrieve text of another process's EDIT
        # control reliably. WM_GETTEXT is a system message marshaled by
        # user32, with SMTO_ABORTIFHUNG and a bounded receiver buffer.
        b = ctypes.create_unicode_buffer(128)
        out = ctypes.c_size_t()
        delivered = bool(_send(edit, 0x000D, len(b),
                               ctypes.cast(b, ctypes.c_void_p).value,
                               0x0002, 2000, ctypes.byref(out)))
        return b.value if delivered else None
    try:
        if not set_search(nonce):
            return result
        until = time.monotonic() + 3.0
        while time.monotonic() < until:
            current = scan()
            empty = next((x for x in current if x[1] == empty_label), None)
            edit_value = read_search()
            result["empty_state_label_found"] = result["empty_state_label_found"] or bool(empty)
            result["empty_state_style_visible"] = (
                result["empty_state_style_visible"] or bool(empty and empty[4]))
            result["nonce_echo_observed"] = (
                result["nonce_echo_observed"] or edit_value == nonce)
            if empty and empty[4] and edit_value == nonce:
                result["search_nonmatch_empty_state_observed"] = True
                break
            time.sleep(0.1)
    finally:
        if set_search(""):
            until = time.monotonic() + 3.0
            while time.monotonic() < until:
                current = scan()
                empty = next((x for x in current if x[1] == empty_label), None)
                edit_value = read_search()
                if (edit_value == "" and
                        (not empty or not empty[4])):
                    result["search_restored"] = True
                    break
                time.sleep(0.1)
    # Independently seeded HKCU uninstall entry -> GF scan -> WinForms filter.
    # Report boolean observations, never installed app names.
    if expected_inventory_name is not None:
        result["seeded_inventory_positive_observed"] = False
        result["seeded_inventory_negative_control_observed"] = False
        result["inventory_item_level_witness_required"] = True
        result["owner_drawn_list_may_not_expose_witness"] = True
        def inspect_query(query, want_empty):
            if not set_search(query):
                return False
            deadline = time.monotonic() + 5.0
            while time.monotonic() < deadline:
                current = scan()
                empty = next((x for x in current if x[1] == empty_label), None)
                value = read_search()
                if search_snapshot_proves(
                        current, value, query, want_empty, edit,
                        label[3], empty_label, inventory_list_hwnd=None):
                    return True
                time.sleep(0.1)
            return False
        try:
            result["seeded_inventory_positive_observed"] = inspect_query(
                expected_inventory_name, False)
            # A2: run the negative control with the SAME evaluator even if
            # positive item evidence was missing. Neither path short-circuits
            # because of trial mode or seed-created booleans.
            result["seeded_inventory_negative_control_observed"] = inspect_query(
                expected_inventory_name + "-ABSENT", True)
        finally:
            if not set_search(""):
                result["search_restored"] = False
            else:
                deadline = time.monotonic() + 3.0
                restored = False
                while time.monotonic() < deadline:
                    if read_search() == "":
                        restored = True
                        break
                    time.sleep(0.1)
                result["search_restored"] = result["search_restored"] and restored
    result["minimum_readonly_health_contract_observed"] = all((
        result["baseline_inventory_nonzero"],
        result["search_edit_identified"],
        result["search_nonmatch_empty_state_observed"],
        result["search_restored"],
    ))
    return result


def message_pump_ping(hwnd: int):
    if not _is_window(hwnd):
        return False
    outcome = ctypes.c_size_t()
    # Hung window must never cause the coordinator to hang indefinitely.
    return bool(_send(hwnd, 0, 0, 0, 0x0002, 1500, ctypes.byref(outcome)))


def observe_real_gf(proc, executable: Path, expected_sha: str,
                    seconds: float = 12.0, expected_inventory_name=None):
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
        # MainForm.Shown starts asynchronous inventory. Its FormClosing
        # explicitly cancels a close while _busy is true. Allow startup to
        # settle instead of immediately dismissing a newly created window.
        settle_until = time.monotonic() + 10.0
        while proc.poll() is None and time.monotonic() < settle_until:
            time.sleep(0.25)
        report["startup_settle_wait_seconds"] = 10
        report["main_form_message_loop_responded"] = (
            proc.poll() is None and message_pump_ping(hwnd))
        if (report["main_form_message_loop_responded"] and
                report["actual_image_path_matches"] and
                report["image_on_disk_sha_matches_manifest"]):
            report["read_only_ui_controls"] = observe_read_only_ui_controls(
                hwnd, proc.pid)
            # A real UI exercise is stronger than a static control inventory.
            # It is still NOT operational self-updater READY.
            report["readonly_inventory_search_health"] = read_only_search_round_trip(
                hwnd, proc.pid, expected_inventory_name=expected_inventory_name)
        report["normal_close_attempts"] = 0
        # Only the verified child HWND receives WM_CLOSE, bounded twice to
        # handle the real GF's legitimate busy/async cancellation. The second
        # attempt may follow a first canceled close, but no indefinite loops.
        for _ in range(2):
            if (not report["main_form_message_loop_responded"] or
                    not report["actual_image_path_matches"] or
                    not report["image_on_disk_sha_matches_manifest"] or
                    proc.poll() is not None or not _is_window(hwnd)):
                break
            if not message_pump_ping(hwnd):
                break
            if not _post(hwnd, 0x0010, 0, 0):
                break
            report["graceful_close_requested"] = True
            report["normal_close_attempts"] += 1
            try:
                proc.wait(timeout=12)
            except subprocess.TimeoutExpired:
                continue
            report["graceful_exit_observed"] = proc.returncode == 0
            break
    report["early_exit_code"] = proc.poll()
    report["reason"] = ("GRACEFUL_MAIN_FORM_EXIT_OBSERVED"
                        if report["graceful_exit_observed"] else
                        "NO_MAIN_HWND_OR_GRACEFUL_CLOSE_UNPROVEN")
    return report
