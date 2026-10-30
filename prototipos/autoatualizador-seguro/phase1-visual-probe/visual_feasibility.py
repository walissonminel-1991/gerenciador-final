"""GF Phase 1 owner-drawn list pixel *feasibility*, disposable Windows CI.
Only a small verified GF list region is sampled via GetPixel; no desktop
screenshot, image files, personal names, pixel hashes or operational authority.
"""
import ctypes
from ctypes import wintypes as w
import hashlib, json, os, subprocess, sys, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "phase1-composed"))
from phase1_lab import trial_registry_fixture, require
from uia_probe_real_gf import owned_controls, search_edit, set_search, get_search
from win32_process_observation import (_pid_for, _post, own_main_windows,
                                       message_pump_ping, process_image_path)
u=ctypes.WinDLL("user32",use_last_error=True)
g=ctypes.WinDLL("gdi32",use_last_error=True)
getrect=u.GetWindowRect
getrect.argtypes=[w.HWND,ctypes.POINTER(w.RECT)]
getrect.restype=w.BOOL
u.ShowWindow.argtypes=[w.HWND,ctypes.c_int]
u.SetForegroundWindow.argtypes=[w.HWND]
u.GetForegroundWindow.restype=w.HWND
u.IsWindowVisible.argtypes=[w.HWND]
u.IsWindowVisible.restype=w.BOOL
u.WindowFromPoint.argtypes=[w.POINT]
u.WindowFromPoint.restype=w.HWND
u.GetDC.argtypes=[w.HWND]
u.GetDC.restype=w.HDC
u.ReleaseDC.argtypes=[w.HWND,w.HDC]
g.GetPixel.argtypes=[w.HDC,ctypes.c_int,ctypes.c_int]
g.GetPixel.restype=w.DWORD

def rect(hwnd):
    r=w.RECT()
    return r if getrect(hwnd,ctypes.byref(r)) else None

def list_region(main,pid):
    # Program.cs places a DarkProgramList beside the empty-state Label in
    # an isolated host Panel. Resolve only that uniquely identified sibling.
    nodes=owned_controls(main,pid)
    empty=[x for x in nodes if x[1]=="Nenhum aplicativo corresponde à pesquisa."]
    if len(empty)!=1:
        return None
    siblings=[x for x in nodes if x[3]==empty[0][3] and x[0]!=empty[0][0]]
    if len(siblings)!=1 or not any(x[3]==siblings[0][0] for x in nodes):
        return None  # scrollbar child expected
    r,outer=rect(siblings[0][0]),rect(main)
    if r is None or outer is None or r.right-r.left<240 or r.bottom-r.top<110:
        return None
    # Name text is drawn on the first row; never sample elsewhere.
    x,y=r.left+48,r.top+8
    width=min(500,r.right-x-45)
    height=min(47,r.bottom-y-15)
    if width<100 or height<30 or not (
       outer.left<=x<x+width<=outer.right and
       outer.top<=y<y+height<=outer.bottom):
        return None
    return (x,y,width,height)

def owned_and_visible(main,pid,roi):
    if not u.IsWindowVisible(main) or int(u.GetForegroundWindow() or 0)!=main:
        return False
    x,y,wid,hei=roi
    for px,py in ((x+15,y+10),(x+wid//2,y+hei//2)):
        target=u.WindowFromPoint(w.POINT(px,py))
        if not target:
            return False
        owner=w.DWORD()
        _pid_for(target,ctypes.byref(owner))
        if owner.value!=pid:
            return False
    return True

def ephemeral_pixel_sample(roi):
    # Pixel sampling of ROI directly: never capture whole screen or persist
    # a bitmap, PNG, crop, extracted name, hash, or pixel array.
    x,y,width,height=roi
    dc=u.GetDC(0)
    if not dc:
        return None
    hasher=hashlib.sha256()
    try:
        for dy in range(0,height,2):
            for dx in range(0,width,3):
                pixel=g.GetPixel(dc,x+dx,y+dy)
                if pixel==0xFFFFFFFF:
                    return None
                hasher.update(int(pixel).to_bytes(4,"little"))
        return hasher.digest()
    finally:
        u.ReleaseDC(0,dc)

def main(executable):
    require(os.name=="nt" and os.environ.get("GITHUB_ACTIONS")=="true",
            "VISUAL_WINDOWS_RUNNER_ONLY")
    exe=Path(executable).resolve(strict=True)
    temp=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    require(exe.is_relative_to(temp) and exe.name=="GerenciadorFinal.exe",
            "VISUAL_GF_EXE_SCOPE")
    identity=hashlib.sha256(exe.read_bytes()).digest()
    report={"schema":"gf-phase1-visual-list-feasibility/1",
            "scope":"DISPOSABLE_WINDOWS_VERIFIED_LIST_ROI_ONLY",
            "real_gf_verified":False,"owned_list_roi_verified":False,
            "structural_list_region_found":False,
            "verified_window_visible":False,
            "verified_window_foreground":False,
            "positive_query_echo":False,"negative_query_echo":False,
            "pixel_samples_complete":False,"seeded_vs_absent_differ":False,
            "two_empty_controls_match":False,
            "item_name_visually_proven":False,
            "operational_ready":False,"automatic_recovery_authorized":False,
            "reason":"INCONCLUSIVE"}
    with trial_registry_fixture() as name:
        env=dict(os.environ)
        env["GfBetaEndpoint"]=""
        proc=subprocess.Popen([str(exe),"--tray"],cwd=str(exe.parent),
                              env=env,stdin=subprocess.DEVNULL,
                              stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,
                              creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        main_hwnd=None
        try:
            deadline=time.monotonic()+22
            while proc.poll() is None and time.monotonic()<deadline:
                windows=own_main_windows(proc.pid)
                if windows:
                    main_hwnd=windows[0]["hwnd"]
                    break
                time.sleep(.25)
            if main_hwnd is None:
                report["reason"]="MAIN_FORM_ABSENT"
                return report
            time.sleep(10)  # asynchronous GF inventory
            if proc.poll() is not None or not message_pump_ping(main_hwnd):
                report["reason"]="MAIN_FORM_UNRESPONSIVE"
                return report
            try:
                report["real_gf_verified"]=(
                    os.path.samefile(process_image_path(proc.pid),exe) and
                    hashlib.sha256(exe.read_bytes()).digest()==identity)
            except (OSError,ValueError,TypeError):
                return report
            if not report["real_gf_verified"]:
                return report
            edit=search_edit(owned_controls(main_hwnd,proc.pid))
            if edit is None or not set_search(edit,name):
                report["reason"]="OWNED_SEARCH_MISSING"
                return report
            # Only the verified OWN GF window may be shown on disposable CI.
            u.ShowWindow(main_hwnd,5)
            u.SetForegroundWindow(main_hwnd)
            time.sleep(1.0)
            if get_search(edit)!=name:
                return report
            roi=list_region(main_hwnd,proc.pid)
            report["structural_list_region_found"]=roi is not None
            report["verified_window_visible"]=bool(u.IsWindowVisible(main_hwnd))
            report["verified_window_foreground"]=(
                int(u.GetForegroundWindow() or 0)==main_hwnd)
            if roi is None:
                report["reason"]="LIST_STRUCTURE_NOT_QUALIFIED"
                return report
            if not owned_and_visible(main_hwnd,proc.pid,roi):
                report["reason"]="ROI_VISIBILITY_OR_OCCLUSION_UNVERIFIED"
                return report
            report["owned_list_roi_verified"]=True
            def inspect(query):
                if not set_search(edit,query):
                    return None
                time.sleep(1.0)
                if get_search(edit)!=query or not owned_and_visible(
                        main_hwnd,proc.pid,roi):
                    return None
                return ephemeral_pixel_sample(roi)
            try:
                positive=inspect(name)
                report["positive_query_echo"]=positive is not None
                absent=inspect(name+"-ABSENT")
                report["negative_query_echo"]=absent is not None
                second=inspect(name+"-MISSING")
                report["pixel_samples_complete"]=all(
                    sample is not None for sample in (positive,absent,second))
                if report["pixel_samples_complete"]:
                    report["seeded_vs_absent_differ"]=positive!=absent
                    report["two_empty_controls_match"]=absent==second
                    report["reason"]="PIXEL_CHANGE_IS_NOT_ITEM_IDENTITY"
            finally:
                set_search(edit,"")
        finally:
            if main_hwnd and proc.poll() is None:
                _post(main_hwnd,0x0010,0,0)
                try: proc.wait(timeout=10)
                except subprocess.TimeoutExpired: pass
            if proc.poll() is None: proc.kill()
            proc.wait(timeout=10)
    return report

if __name__=="__main__":
    require(len(sys.argv)==2,"VISUAL_ARGUMENTS")
    print("GF_PHASE1_VISUAL_FEASIBILITY "+json.dumps(main(sys.argv[1]),sort_keys=True))
