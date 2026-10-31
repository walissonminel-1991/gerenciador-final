"""Normal GUI: authentic Draft download completes; OLD remains interactive.
Lab-only Win32 witness; downloader and TUF verifier remain unchanged.
"""
import ctypes, hashlib, json, os, subprocess, sys, time
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
    l18=sys.argv[1:] in (["--l18"],["--l18","--deny-close"])
    deny_close=sys.argv[1:]==["--l18","--deny-close"]
    must(not sys.argv[1:] or l18,"NORMAL_GUI_INVALID_ARGS")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    prefix="gf-e2e-github-normal-l18" if l18 else "gf-e2e-github-normal"
    exe=(runner/(prefix+"-old")/"GerenciadorFinal.exe").resolve(strict=True)
    must(exe.parent==runner/(prefix+"-old"),"NORMAL_SUCCESS_WRONG_OLD")
    shown=runner/(prefix+"-startup-shown.json")
    downloaded=runner/(prefix+"-acquire-report.json")
    failure=runner/(prefix+"-failure.json")
    result=runner/(prefix+"-ui-observed.json")
    l18_result=runner/"gf-e2e-github-normal-l18-flow-report.json"
    l18_proof=runner/"gf-e2e-github-normal-l18-handoff-report.json"
    denial=runner/"gf-e2e-github-normal-l18-close-denied.json"
    if l18:
        must(not l18_result.exists() and not l18_proof.exists(),
             "NORMAL_L18_PROOF_NOT_FRESH")
    if deny_close:
        must(not denial.exists(),"NORMAL_L18_DENIAL_NOT_FRESH")
    must(not any(p.exists() for p in (shown,downloaded,failure,result)),"NORMAL_SUCCESS_RECEIPTS_NOT_FRESH")
    consent=Path(os.environ["LOCALAPPDATA"])/"GerenciadorFinal"/"Privacidade"/"consentimento-v1.json"
    must(not consent.exists(),"NORMAL_SUCCESS_EXISTING_CONSENT")
    image_hash=hashlib.sha256(exe.read_bytes()).hexdigest()
    env=dict(os.environ)
    env.update({"GF_E2E_STARTUP_DRAFT_PROBE":"1",
                "GF_E2E_GUI_NORMAL_SUCCESS":"1",
                "GF_E2E_GUI_NORMAL_L18_PROOF":"1" if l18 else "0",
                "GF_E2E_GUI_NORMAL_L18_DENY_CLOSE":"1" if deny_close else "0",
                "GF_E2E_HTTP_CUT_NEGATIVE":"0",
                "GF_E2E_HTTP_UI_NEGATIVE":"0",
                "GF_E2E_HTTP_UI_NORMAL_NEGATIVE":"0",
                "GF_E2E_HANDOFF_L18":"1" if l18 else "0",
                "GF_E2E_SELF_GRACEFUL_CLOSE":"1" if deny_close else "0",
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
        if l18:
            # Observe the same OLD window during the EXISTING L18 focused
            # coordinator. No automatic closing, installation swap or READY.
            deadline=time.monotonic()+235
            while time.monotonic()<deadline and not l18_proof.exists():
                must(proc.poll() is None and not failure.exists(),
                     "NORMAL_L18_OLD_EXITED_OR_DOWNLOAD_FAILED")
                windows=own_main_windows(proc.pid)
                must(len(windows)==1 and windows[0]["visible"]
                     and message_pump_ping(windows[0]["hwnd"]),
                     "NORMAL_L18_MAINFORM_UNRESPONSIVE_DURING_COORDINATION")
                must(not consent_dialogs_for_pid(proc.pid) and not consent.exists(),
                     "NORMAL_L18_CONSENT_CHANGED")
                time.sleep(0.3)
            must(l18_result.exists() and l18_proof.exists() and not failure.exists(),
                 "NORMAL_L18_COORDINATOR_NOT_COMPLETED")
            record=json.loads(l18_result.read_bytes())
            proof=json.loads(l18_proof.read_bytes())
            normal=record.get("result",{})
            must(record.get("schema")=="gf-real-single-normal-lab/1"
                 and record.get("scenario")=="normal"
                 and record.get("passed")==1 and record.get("failed")==0
                 and record.get("operational_ready") is False
                 and record.get("recovery_authorized") is False
                 and record.get("phase1_complete") is False
                 and normal.get("classification")=="PASS_CONTAINMENT"
                 and normal.get("outcome")=="UnknownBlocked"
                 and normal.get("replay")=="UnknownBlocked"
                 and normal.get("normal_owner_completed_without_crash") is True
                 and normal.get("manifest_and_object_identities_rechecked") is True
                 and proof.get("schema")=="gf-e2e-old-owns-l18-handoff/2"
                 and proof.get("initiated_by_gf_old_mainform") is True
                 and proof.get("same_l18_coordinator_invoked_by_old") is True
                 and proof.get("tuf_child_verified_download") is True
                 and proof.get("normal_outcome")=="UnknownBlocked"
                 and proof.get("operational_ready") is False
                 and proof.get("automatic_recovery_authorized") is False,
                 "NORMAL_L18_SAME_COORDINATOR_CONTAINMENT_UNPROVEN")
        if deny_close:
            deadline=time.monotonic()+12
            while time.monotonic()<deadline and not denial.exists():
                must(proc.poll() is None,"NORMAL_L18_OLD_CLOSED_ON_UNKNOWN")
                time.sleep(0.1)
            must(denial.exists(),"NORMAL_L18_EXPLICIT_DENIAL_MISSING")
            denied=json.loads(denial.read_bytes())
            must(denied=={
                "schema":"gf-e2e-l18-unknown-close-denied/1",
                "explicitly_requested":True,"outcome":"UnknownBlocked",
                "close_callback_invoked":False,
                "reason":"UNKNOWN_IS_CONTAINMENT_NOT_CLOSE_AUTHORITY",
                "test_only":True,"operational_ready":False
            },"NORMAL_L18_DENIAL_INVALID")
            # The receipt is not a GUI-health proof: independently observe
            # multiple message-pump cycles after denial on the same HWND.
            for _ in range(12):
                must(proc.poll() is None,"NORMAL_L18_OLD_CLOSED_AFTER_DENIAL")
                windows=own_main_windows(proc.pid)
                must(len(windows)==1 and windows[0]["visible"]
                     and message_pump_ping(windows[0]["hwnd"]),
                     "NORMAL_L18_OLD_NOT_HEALTHY_AFTER_DENIAL")
                time.sleep(0.25)
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
        record={"schema":("gf-e2e-github-normal-l18-mainform/1" if l18
                          else "gf-e2e-github-normal-mainform-healthy/1"),
                "normal_open_no_tray":True,"no_showwindow":True,
                "visible_during_download":True,"original_mainform_shown":True,
                "draft_download_completed":True,"old_alive_after_download":True,
                "image_verified":True,"mainform_visible":True,
                "message_pump_responsive":True,"inventory_nonzero":True,
                "search_empty_and_restored":True,"consent_unchanged":True,
                "new_executed":False,"l18_handoff":l18,
                "unknown_close_requested_and_denied":deny_close,"retry":False,
                "rollback":False,"cleanup":False,"operational_ready":False,
                "test_only":True}
        with result.open("x",encoding="utf-8") as out:
            json.dump(record,out,sort_keys=True)
            out.flush();os.fsync(out.fileno())
        ok=True
        print(("GF_E2E_GITHUB_NORMAL_L18_HEALTH_PASS " if l18 else
               "GF_E2E_GITHUB_NORMAL_MAINFORM_HEALTH_PASS ") +
              "real_draft=COMPLETE normal_gui=VISIBLE+RESPONSIVE "
              "inventory=NONZERO search=EMPTY+RESTORED " +
              ("OLD_running=true L18=UnknownBlocked close_request=DENIED installed_NEW_not_executed=true READY=false"
               if deny_close else
               "OLD_running=true L18=UnknownBlocked installed_NEW_not_executed=true READY=false"
               if l18 else "OLD_running=true NEW_executed=false L18_not_invoked=true READY=false"),
              flush=True)
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
