"""Normal GUI: authentic Draft download completes; OLD remains interactive.
Lab-only Win32 witness; downloader and TUF verifier remain unchanged.
"""
import ctypes, hashlib, json, os, subprocess, time
from pathlib import Path
from win32_process_observation import (
    own_main_windows, process_image_path, message_pump_ping,
    read_only_search_round_trip, desktop_observation
)
from http_cut_gui_observer import consent_dialogs_for_pid

def must(ok, code):
    if not ok: raise RuntimeError(code)

def main():
    must(os.name=="nt" and os.environ.get("GITHUB_ACTIONS")=="true"
         and os.environ.get("GITHUB_REPOSITORY")=="walissonminel-1991/gerenciador-final",
         "NORMAL_SUCCESS_RUNNER_ONLY")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe=(runner/"gf-e2e-github-normal-old"/"GerenciadorFinal.exe").resolve(strict=True)
    must(exe.parent==runner/"gf-e2e-github-normal-old","NORMAL_SUCCESS_WRONG_OLD")
    shown=runner/"gf-e2e-github-normal-startup-shown.json"
    downloaded=runner/"gf-e2e-github-normal-acquire-report.json"
    failure=runner/"gf-e2e-github-normal-failure.json"
    result=runner/"gf-e2e-github-normal-ui-observed.json"
    must(not any(p.exists() for p in (shown,downloaded,failure,result)),"NORMAL_SUCCESS_RECEIPTS_NOT_FRESH")
    consent=Path(os.environ["LOCALAPPDATA"])/"GerenciadorFinal"/"Privacidade"/"consentimento-v1.json"
    must(not consent.exists(),"NORMAL_SUCCESS_EXISTING_CONSENT")
    image_hash=hashlib.sha256(exe.read_bytes()).hexdigest()
    env=dict(os.environ)
    env.update({"GF_E2E_STARTUP_DRAFT_PROBE":"1",
                "GF_E2E_GUI_NORMAL_SUCCESS":"1",
                "GF_E2E_HTTP_CUT_NEGATIVE":"0",
                "GF_E2E_HTTP_UI_NEGATIVE":"0",
                "GF_E2E_HTTP_UI_NORMAL_NEGATIVE":"0",
                "GF_E2E_HANDOFF_L18":"0",
                "GF_E2E_SELF_GRACEFUL_CLOSE":"0",
                "GF_E2E_SELECTED_NEW_CONTINUATION":"0",
                "GF_E2E_SWAP_CUT_AFTER_OLD_MOVE":"0",
                "GF_E2E_LIVE_INSTALL_SWAP":"0"})
    must(len(env.get("GH_TOKEN",""))>=20,"NORMAL_SUCCESS_NO_EPHEMERAL_TOKEN")
    proc=subprocess.Popen([str(exe)],cwd=str(exe.parent),env=env,
                          stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,
                          stderr=subprocess.DEVNULL,
                          creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
    ok=False
    try:
        visible_while_downloading=False
        deadline=time.monotonic()+195
        while time.monotonic()<deadline and not downloaded.exists():
            must(proc.poll() is None,"NORMAL_SUCCESS_OLD_EXITED_DURING_DOWNLOAD")
            must(not failure.exists(),"NORMAL_SUCCESS_HTTP_DOWNLOAD_FAILED")
            must(not consent_dialogs_for_pid(proc.pid),"NORMAL_SUCCESS_CONSENT_MODAL_NOT_DOWNLOAD_FAULT")
            must(not consent.exists(),"NORMAL_SUCCESS_CONSENT_WRITTEN")
            windows=own_main_windows(proc.pid)
            if len(windows)==1 and windows[0]["visible"]:
                visible_while_downloading=True
            time.sleep(0.15)
        must(shown.exists() and downloaded.exists() and not failure.exists(),
             "NORMAL_SUCCESS_NO_COMPLETE_DRAFT_RECEIPT")
        must(visible_while_downloading,"NORMAL_SUCCESS_NOT_VISIBLY_OPEN_DURING_DOWNLOAD")
        main=json.loads(shown.read_bytes())
        receipt=json.loads(downloaded.read_bytes())
        must(main=={"schema":"gf-e2e-startup-shown/1",
                    "startup_event":"MainForm.Shown",
                    "process_image":"GerenciadorFinal.exe",
                    "real_mainform_handle_created":True,
                    "test_only":True,"operational_ready":False},
             "NORMAL_SUCCESS_NOT_REAL_MAINFORM_SHOWN")
        must(receipt.get("schema")=="gf-e2e-old-github-draft/1"
             and receipt.get("draft_release_id")==429695537
             and receipt.get("draft_tag")=="gf-e2e-c5057bbbaf1d"
             and receipt.get("downloaded_file_count")==5
             and receipt.get("real_dotnet_httpclient") is True
             and receipt.get("initiated_by_gf_old_executable") is True
             and receipt.get("test_only") is True
             and receipt.get("operational_ready") is False
             and receipt.get("automatic_recovery_authorized") is False,
             "NORMAL_SUCCESS_DOWNLOAD_RECEIPT_INVALID")
        must(proc.poll() is None,"NORMAL_SUCCESS_OLD_EXITED_AFTER_DOWNLOAD")
        windows=own_main_windows(proc.pid)
        must(len(windows)==1 and windows[0]["visible"],"NORMAL_SUCCESS_MAINFORM_NOT_NATIVE_VISIBLE")
        hwnd=windows[0]["hwnd"]
        source=process_image_path(proc.pid)
        must(source is not None and os.path.samefile(source,exe)
             and hashlib.sha256(exe.read_bytes()).hexdigest()==image_hash,
             "NORMAL_SUCCESS_PROCESS_IMAGE_NOT_VERIFIED")
        must(message_pump_ping(hwnd),"NORMAL_SUCCESS_MAINFORM_NOT_RESPONSIVE")
        health=None
        deadline=time.monotonic()+25
        while time.monotonic()<deadline:
            must(proc.poll() is None and message_pump_ping(hwnd),"NORMAL_SUCCESS_UI_STALLED")
            health=read_only_search_round_trip(hwnd,proc.pid)
            if health.get("minimum_readonly_health_contract_observed"):break
            time.sleep(0.75)
        must(health is not None and
             health.get("minimum_readonly_health_contract_observed") is True and
             health.get("baseline_inventory_nonzero") is True and
             health.get("search_nonmatch_empty_state_observed") is True and
             health.get("search_restored") is True,
             "NORMAL_SUCCESS_INVENTORY_SEARCH_UNHEALTHY")
        must(proc.poll() is None and message_pump_ping(hwnd)
             and len(own_main_windows(proc.pid))==1
             and own_main_windows(proc.pid)[0]["visible"],
             "NORMAL_SUCCESS_MAINFORM_NOT_STILL_VISIBLE")
        must(not consent_dialogs_for_pid(proc.pid) and not consent.exists(),
             "NORMAL_SUCCESS_CONSENT_CHANGED")
        must(desktop_observation()["interactive_user_desktop_verified"],
             "NORMAL_SUCCESS_DESKTOP_NOT_INTERACTIVE")
        record={"schema":"gf-e2e-github-normal-mainform-healthy/1",
                "normal_open_no_tray":True,"no_showwindow":True,
                "visible_during_download":True,"original_mainform_shown":True,
                "draft_download_completed":True,"old_alive_after_download":True,
                "image_verified":True,"mainform_visible":True,
                "message_pump_responsive":True,"inventory_nonzero":True,
                "search_empty_and_restored":True,"consent_unchanged":True,
                "new_executed":False,"l18_handoff":False,"retry":False,
                "rollback":False,"cleanup":False,"operational_ready":False,
                "test_only":True}
        with result.open("x",encoding="utf-8") as out:
            json.dump(record,out,sort_keys=True)
            out.flush();os.fsync(out.fileno())
        ok=True
        print("GF_E2E_GITHUB_NORMAL_MAINFORM_HEALTH_PASS "
              "real_draft=COMPLETE normal_gui=VISIBLE+RESPONSIVE "
              "inventory=NONZERO search=EMPTY+RESTORED "
              "OLD_running=true NEW_executed=false L18_not_invoked=true READY=false",flush=True)
    finally:
        if proc.poll() is None:
            windows=own_main_windows(proc.pid)
            if len(windows)==1:
                user32=ctypes.WinDLL("user32",use_last_error=True)
                post=user32.PostMessageW
                post.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_size_t,ctypes.c_ssize_t]
                post.restype=ctypes.c_bool
                post(windows[0]["hwnd"],0x0010,0,0)
                try:proc.wait(timeout=10)
                except subprocess.TimeoutExpired:pass
        if proc.poll() is None:proc.kill() # only the disposable test child
        proc.wait(timeout=15)
        if not ok:print("GF_E2E_GITHUB_NORMAL_MAINFORM_NOT_PROVEN READY=false",flush=True)

if __name__=="__main__":main()
