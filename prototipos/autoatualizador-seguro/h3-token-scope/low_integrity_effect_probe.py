#!/usr/bin/env python3
"""H3 scoped, inert low-integrity process experiment. NEVER GF update authority."""
import ctypes
from ctypes import wintypes as W
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

MARKER=b"GF-PHASE1-H3-INERT-LOW-TOKEN-PROBE\n"
ORIGINAL=b"UNMODIFIED-MEDIUM-OR-HIGH-INTEGRITY-OBJECT\n"
TOKEN_ACCESS=0x0008|0x0002|0x0001|0x0080
DISABLE_MAX_PRIVILEGE=1
TOKEN_INTEGRITY_LEVEL=25
SE_GROUP_INTEGRITY=0x20
CREATE_NO_WINDOW=0x08000000
LOW_RID=4096

class SID_AND_ATTRIBUTES(ctypes.Structure):
    _fields_=[("Sid",ctypes.c_void_p),("Attributes",W.DWORD)]

class TOKEN_MANDATORY_LABEL(ctypes.Structure):
    _fields_=[("Label",SID_AND_ATTRIBUTES)]

class STARTUPINFOW(ctypes.Structure):
    _fields_=[("cb",W.DWORD),("lpReserved",W.LPWSTR),("lpDesktop",W.LPWSTR),
              ("lpTitle",W.LPWSTR),("dwX",W.DWORD),("dwY",W.DWORD),
              ("dwXSize",W.DWORD),("dwYSize",W.DWORD),("dwXCountChars",W.DWORD),
              ("dwYCountChars",W.DWORD),("dwFillAttribute",W.DWORD),
              ("dwFlags",W.DWORD),("wShowWindow",W.WORD),("cbReserved2",W.WORD),
              ("lpReserved2",ctypes.POINTER(ctypes.c_byte)),("hStdInput",W.HANDLE),
              ("hStdOutput",W.HANDLE),("hStdError",W.HANDLE)]

class PROCESS_INFORMATION(ctypes.Structure):
    _fields_=[("hProcess",W.HANDLE),("hThread",W.HANDLE),
              ("dwProcessId",W.DWORD),("dwThreadId",W.DWORD)]

def require(ok,code):
    if not ok: raise RuntimeError(code)

def win_require(ok,code):
    if not ok: raise RuntimeError(f"{code}:Win32={ctypes.get_last_error()}")

def winapi():
    k=ctypes.WinDLL("kernel32",use_last_error=True)
    a=ctypes.WinDLL("advapi32",use_last_error=True)
    def bind(lib,name,args,ret):
        fn=getattr(lib,name);fn.argtypes=args;fn.restype=ret
    H=W.HANDLE;P=ctypes.POINTER
    bind(k,"GetCurrentProcess",[],H)
    bind(k,"CloseHandle",[H],W.BOOL)
    bind(k,"LocalFree",[ctypes.c_void_p],ctypes.c_void_p)
    bind(k,"WaitForSingleObject",[H,W.DWORD],W.DWORD)
    bind(k,"TerminateProcess",[H,W.UINT],W.BOOL)
    bind(k,"GetExitCodeProcess",[H,P(W.DWORD)],W.BOOL)
    bind(a,"OpenProcessToken",[H,W.DWORD,P(H)],W.BOOL)
    bind(a,"CreateRestrictedToken",[H,W.DWORD,W.DWORD,ctypes.c_void_p,
        W.DWORD,ctypes.c_void_p,W.DWORD,ctypes.c_void_p,P(H)],W.BOOL)
    bind(a,"GetTokenInformation",[H,ctypes.c_int,ctypes.c_void_p,
        W.DWORD,P(W.DWORD)],W.BOOL)
    bind(a,"SetTokenInformation",[H,ctypes.c_int,ctypes.c_void_p,W.DWORD],W.BOOL)
    bind(a,"ConvertStringSidToSidW",[W.LPCWSTR,P(ctypes.c_void_p)],W.BOOL)
    bind(a,"ConvertSidToStringSidW",[ctypes.c_void_p,P(ctypes.c_void_p)],W.BOOL)
    bind(a,"GetLengthSid",[ctypes.c_void_p],W.DWORD)
    bind(a,"CreateProcessAsUserW",[H,W.LPCWSTR,W.LPWSTR,ctypes.c_void_p,
        ctypes.c_void_p,W.BOOL,W.DWORD,ctypes.c_void_p,W.LPCWSTR,
        P(STARTUPINFOW),P(PROCESS_INFORMATION)],W.BOOL)
    return k,a

def own_token(k,a):
    token=W.HANDLE()
    win_require(a.OpenProcessToken(k.GetCurrentProcess(),TOKEN_ACCESS,
                ctypes.byref(token)),"OPEN_OWN_TOKEN")
    return token

def integrity_rid(k,a,token):
    needed=W.DWORD()
    a.GetTokenInformation(token,TOKEN_INTEGRITY_LEVEL,None,0,ctypes.byref(needed))
    require(needed.value>=ctypes.sizeof(TOKEN_MANDATORY_LABEL),
            "TOKEN_IL_BUFFER_UNAVAILABLE")
    buf=ctypes.create_string_buffer(needed.value)
    win_require(a.GetTokenInformation(token,TOKEN_INTEGRITY_LEVEL,buf,
                needed,ctypes.byref(needed)),"TOKEN_IL_QUERY")
    label=ctypes.cast(buf,ctypes.POINTER(TOKEN_MANDATORY_LABEL)).contents
    value=ctypes.c_void_p()
    win_require(a.ConvertSidToStringSidW(label.Label.Sid,ctypes.byref(value)),
                "TOKEN_IL_SID_TO_TEXT")
    try:
        sid=ctypes.wstring_at(value.value)
        require(sid.startswith("S-1-16-"),"TOKEN_IL_NOT_MANDATORY_SID")
        return int(sid.rsplit("-",1)[1])
    finally:
        k.LocalFree(value)

def set_low(k,a,token):
    sid=ctypes.c_void_p()
    win_require(a.ConvertStringSidToSidW("S-1-16-4096",ctypes.byref(sid)),
                "LOW_IL_SID")
    try:
        label=TOKEN_MANDATORY_LABEL(SID_AND_ATTRIBUTES(sid,SE_GROUP_INTEGRITY))
        size=ctypes.sizeof(label)+a.GetLengthSid(sid)
        win_require(a.SetTokenInformation(token,TOKEN_INTEGRITY_LEVEL,
                    ctypes.byref(label),size),"SET_LOW_IL_ON_CHILD_TOKEN_ONLY")
    finally:
        k.LocalFree(sid)

def run_child(k,a,source,mode,target,parent_il):
    token=W.HANDLE()
    win_require(a.CreateRestrictedToken(source,DISABLE_MAX_PRIVILEGE,
                0,None,0,None,0,None,ctypes.byref(token)),
                "CREATE_RESTRICTED_TOKEN")
    try:
        if mode=="low":
            set_low(k,a,token)
            require(integrity_rid(k,a,token)==LOW_RID,"LOW_IL_TOKEN_NOT_SET")
        else:
            require(integrity_rid(k,a,token)==parent_il,"CONTROL_IL_CHANGED")
        exe=str(Path(sys.executable).resolve())
        command=subprocess.list2cmdline([exe,"-I","-B",
            str(Path(__file__).resolve()),"--child",mode,str(target),
            str(LOW_RID if mode=="low" else parent_il)])
        mutable=ctypes.create_unicode_buffer(command)
        startup=STARTUPINFOW();startup.cb=ctypes.sizeof(startup)
        info=PROCESS_INFORMATION()
        # Failure to launch is NEVER counted as successful containment.
        win_require(a.CreateProcessAsUserW(token,exe,mutable,None,None,False,
                    CREATE_NO_WINDOW,None,None,ctypes.byref(startup),
                    ctypes.byref(info)),"RESTRICTED_CHILD_NOT_CREATED")
        try:
            wait=k.WaitForSingleObject(info.hProcess,30000)
            if wait!=0:
                k.TerminateProcess(info.hProcess,97)
                k.WaitForSingleObject(info.hProcess,30000)
                raise RuntimeError("RESTRICTED_CHILD_TIMEOUT_OR_WAIT_ERROR")
            exit_code=W.DWORD()
            win_require(k.GetExitCodeProcess(info.hProcess,
                        ctypes.byref(exit_code)),"CHILD_EXIT_CODE")
            require(exit_code.value==0,f"CHILD_FAILED_{mode}_{exit_code.value}")
        finally:
            k.CloseHandle(info.hThread);k.CloseHandle(info.hProcess)
    finally:
        k.CloseHandle(token)

def require_fixture(path):
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    parent=path.parent.resolve(strict=True)
    require(parent.parent==runner and parent.name.startswith("gf-phase1-h3-il-"),
            "NOT_DISPOSABLE_FIXTURE")
    require((parent/"GF-PHASE1-H3-INERT.marker").read_bytes()==MARKER,
            "FIXTURE_MARKER_INVALID")
    require(path.name=="protected-medium.fixture.txt" and path.is_file(),
            "WRONG_FIXTURE_TARGET")

def child(mode,path,expected_il):
    require(mode in ("control","low"),"WRONG_MODE")
    require_fixture(path)
    k,a=winapi();token=own_token(k,a)
    try:
        require(integrity_rid(k,a,token)==expected_il,
                "CHILD_TOKEN_IL_NOT_CONFIRMED")
    finally:k.CloseHandle(token)
    require(path.read_bytes()==ORIGINAL,"CHILD_READ_FAILED")
    if mode=="control":
        try:
            with path.open("r+b") as f:f.write(b"C")
            return 0
        except OSError:return 82
    try:
        with path.open("r+b") as f:f.write(b"X")
        return 83 # Security FAILURE: low-integrity child actually wrote.
    except OSError as ex:
        # Python CRT file-open may map ERROR_ACCESS_DENIED to EACCES
        # without preserving winerror. Only explicit permission denial counts.
        denied = getattr(ex,"winerror",None)==5 or (
            isinstance(ex,PermissionError) and ex.errno==13)
        return 0 if denied else 84

def main():
    require(os.name=="nt" and os.environ.get("GITHUB_ACTIONS")=="true",
            "DISPOSABLE_WINDOWS_ONLY")
    if len(sys.argv)==5 and sys.argv[1]=="--child":
        return child(sys.argv[2],Path(sys.argv[3]),int(sys.argv[4]))
    require(len(sys.argv)==1,"UNEXPECTED_ARGUMENTS")
    k,a=winapi();source=own_token(k,a)
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    root=Path(tempfile.mkdtemp(prefix="gf-phase1-h3-il-",dir=runner))
    try:
        require(root.parent.resolve(strict=True)==runner,"ROOT_NOT_RUNNER_TEMP")
        (root/"GF-PHASE1-H3-INERT.marker").write_bytes(MARKER)
        target=root/"protected-medium.fixture.txt"
        target.write_bytes(ORIGINAL)
        initial_il=integrity_rid(k,a,source)
        require(initial_il>=8192,"PARENT_IL_BELOW_MEDIUM")
        run_child(k,a,source,"control",target,initial_il)
        require(target.read_bytes()==b"C"+ORIGINAL[1:],
                "CONTROL_RESTRICTED_TOKEN_CANNOT_WRITE")
        target.write_bytes(ORIGINAL)
        run_child(k,a,source,"low",target,initial_il)
        require(target.read_bytes()==ORIGINAL,"LOW_IL_MODIFIED_MEDIUM_FILE")
        run_child(k,a,source,"control",target,initial_il)
        require(target.read_bytes()==b"C"+ORIGINAL[1:],
                "SAME_USER_CONTROL_AFTER_LOW_COULD_NOT_WRITE")
        report={
            "schema":"gf-phase1-h3-low-token-effects/1",
            "status":"PARTIAL_LOCAL_EFFECT_BOUNDARY_PROVEN",
            "scope":"DISPOSABLE_WINDOWS_INERT_FILE_AND_RESTRICTED_CHILD_ONLY",
            "restricted_token_created":True,
            "low_integrity_sid_verified_in_child":True,
            "same_restricted_token_without_il_reduction_wrote_target":True,
            "low_integrity_child_read_target":True,
            "low_integrity_write_denied_by_os":True,
            "protected_target_unchanged_after_denied_write":True,
            "unrestricted_or_higher_integrity_alternative_still_possible":True,
            "same_user_external_launch_blocked":False,
            "provider_broker_effects_bounded":False,
            "h1_proven":False,"h2_proven":False,"h3_global_proven":False,
            "auto_recovery_allowed":False,"operational_ready":False,
            "windows_policy_changed":False,"acl_changed":False,
            "real_gf_executed":False,"service_or_installer_executed":False}
        print("GF_PHASE1_H3_LOW_TOKEN_REPORT "+json.dumps(report,sort_keys=True))
        return 0
    finally:
        k.CloseHandle(source)
        if root.parent.resolve(strict=True)==runner and root.name.startswith("gf-phase1-h3-il-"):
            shutil.rmtree(root)

if __name__=="__main__":
    try:sys.exit(main())
    except Exception as error:
        print("GF_PHASE1_H3_LOW_TOKEN_FAILURE "+repr(error),file=sys.stderr)
        sys.exit(1)
