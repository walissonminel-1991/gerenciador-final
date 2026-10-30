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

EMPTY_LABEL = "Nenhum aplicativo corresponde à pesquisa."

def structural_list_candidate(nodes, rectangles, empty_label=EMPTY_LABEL):
    """Only an exact same-parent, same-rectangle owner-drawn sibling qualifies.

    Caller supplies ONLY HWNDs independently enumerated under the expected
    GF PID. No text from other app windows, no pixel content.
    """
    empty = [node for node in nodes if node[1] == empty_label]
    if len(empty) != 1 or empty[0][0] not in rectangles:
        return None
    label = empty[0]
    label_bounds = rectangles[label[0]]
    candidates = []
    for item in nodes:
        if item[0] == label[0] or item[3] != label[3] or item[1]:
            continue
        klass = item[2].upper()
        if "WINDOW" not in klass or "EDIT" in klass or "STATIC" in klass:
            continue
        bounds = rectangles.get(item[0])
        if bounds is None:
            continue
        if (bounds[2]-bounds[0] < 240 or bounds[3]-bounds[1] < 110
                or any(abs(bounds[n]-label_bounds[n]) > 3 for n in range(4))):
            continue
        candidates.append(item[0])
    return candidates[0] if len(candidates) == 1 else None

def list_region(main,pid,known_empty_text=EMPTY_LABEL):
    # The empty-state label is *visible and HWND-created* only on an
    # unmatched query. This must be called AFTER observing negative search.
    # Enumerate HWNDs of this GF process only, without unrelated text logs.
    nodes=owned_controls(main,pid)
    label=[n for n in nodes if n[1]==known_empty_text]
    if len(label)!=1 or not u.IsWindowVisible(label[0][0]):
        return None,None,"VISIBLE_EMPTY_LABEL_UNAVAILABLE"
    rects={}
    for item in nodes:
        r=rect(item[0])
        if r is not None:
            rects[item[0]]=(r.left,r.top,r.right,r.bottom)
    candidate=structural_list_candidate(nodes,rects,known_empty_text)
    if candidate is None:
        return None,None,"SIBLING_LIST_TOPOLOGY_AMBIGUOUS"
    owner=w.DWORD()
    _pid_for(candidate,ctypes.byref(owner))
    root,area=rect(main),rect(candidate)
    if owner.value!=pid or root is None or area is None:
        return None,None,"LIST_OWNER_OR_RECT_UNVERIFIED"
    if (area.right-area.left<240 or area.bottom-area.top<110 or
            not (root.left<=area.left<area.right<=root.right and
                 root.top<=area.top<area.bottom<=root.bottom)):
        return None,None,"LIST_RECT_NOT_WITHIN_MAIN"
    # Only a narrow strip of the first owner-drawn row may be considered
    # for a future optical probe; THIS increment never reads the pixels.
    x,y=area.left+48,area.top+8
    width=min(500,area.right-x-45)
    height=min(47,area.bottom-y-15)
    if width<100 or height<30:
        return None,None,"FIRST_ROW_CROP_INVALID"
    return candidate,(x,y,width,height),"LIST_HWND_STRUCTURALLY_BOUND"

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
            "list_handle_persisted":False,
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
            if edit is None:
                report["reason"]="OWNED_SEARCH_MISSING"
                return report
            # Only the verified OWN GF window may be shown on disposable CI.
            u.ShowWindow(main_hwnd,5)
            u.SetForegroundWindow(main_hwnd)
            time.sleep(1.0)
            report["verified_window_visible"]=bool(u.IsWindowVisible(main_hwnd))
            report["verified_window_foreground"]=(
                int(u.GetForegroundWindow() or 0)==main_hwnd)
            if not (report["verified_window_visible"] and
                    report["verified_window_foreground"]):
                report["reason"]="OWN_WINDOW_VISIBILITY_UNPROVEN"
                return report
            # NEGATIVE FIRST: WinForms creates/shows _emptyState only for
            # an empty search. Previously the probe searched the seeded
            # positive first and tried to find an invisible label's HWND.
            try:
                absent=name+"-ABSENT"
                if not set_search(edit,absent):
                    report["reason"]="NEGATIVE_SEARCH_FAILED"
                    return report
                time.sleep(1.0)
                report["negative_query_echo"]=get_search(edit)==absent
                if not report["negative_query_echo"]:
                    report["reason"]="NEGATIVE_QUERY_NOT_OBSERVED"
                    return report
                candidate,roi,reason=list_region(main_hwnd,proc.pid)
                report["structural_list_region_found"]=candidate is not None
                report["reason"]=reason
                if candidate is None:
                    return report
                if not owned_and_visible(main_hwnd,proc.pid,roi):
                    report["reason"]="ROI_VISIBILITY_OR_OCCLUSION_UNVERIFIED"
                    return report
                report["owned_list_roi_verified"]=True
                # Re-probe after the positive search: exact handle, parent,
                # owner, class, and rectangle must remain stable. No item
                # name, pixel, or positive activation is inferred.
                original=[n for n in owned_controls(main_hwnd,proc.pid)
                          if n[0]==candidate]
                if len(original)!=1:
                    report["reason"]="LIST_HANDLE_NOT_STABLE"
                    return report
                if not set_search(edit,name):
                    report["reason"]="POSITIVE_SEARCH_FAILED"
                    return report
                time.sleep(1.0)
                report["positive_query_echo"]=get_search(edit)==name
                after=[n for n in owned_controls(main_hwnd,proc.pid)
                       if n[0]==candidate]
                if len(after)!=1 or after[0]!=original[0]:
                    report["reason"]="LIST_HANDLE_REPLACED"
                    return report
                owner=w.DWORD()
                _pid_for(candidate,ctypes.byref(owner))
                report["list_handle_persisted"]=bool(
                    owner.value==proc.pid and rect(candidate) is not None)
                if not report["list_handle_persisted"]:
                    report["reason"]="LIST_HANDLE_OWNERSHIP_LOST"
                    return report
                report["reason"]="LIST_HWND_IDENTIFIED_ITEM_IDENTITY_UNPROVEN"
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
