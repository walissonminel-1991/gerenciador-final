"""D2.4.3 Windows adversarial suite for persistent python-TUF and NTFS HOLD.

Uses the same C# coordinator and D11 python-tuf; fixtures only.
No real updates, no installer, no data cleanup without lab-owned fixture.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from time import monotonic

HERE=Path(__file__).resolve().parent
for name in ("github-trust-lab","github-auth-lab"):
    sys.path.insert(0,str(HERE.parent/name))
from d11_persistent_lab import release_roles
from d1_release_manifest_lab import (ANCHOR,create_manifest,encode_manifest,
                                      load_generator,digest,require)

def execute(exe,*args,env=None):
    p=subprocess.run([str(exe),*map(str,args)],input="",stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,text=True,timeout=25,env=env)
    require(p.returncode==0,"D243_PROCESS_CRASH_"+p.stderr[-1000:])
    if "--d243-probe" in args:
        prefix="GF_D243_NTFS "
    else:
        prefix="GF_D243_OBSERVATION "
    lines=[s for s in p.stdout.splitlines() if s.startswith(prefix)]
    require(len(lines)==1,"D243_REPORT_COUNT_"+p.stdout[-1000:])
    return json.loads(lines[0][len(prefix):])

def check_observation(o,ok):
    require(set(o)=={"schema","status","reason","child_launched",
        "test_tuf_observed","observed_role_generation","staged_inert_bytes",
        "network_requests","can_install","operational_ready",
        "automatic_recovery_authorized","same_user_rollback_resistant",
        "same_user_ntfs_toctou_proven","cooperative_lock_only",
        "abandoned_lock_recovery_authorized"}, "D243_RESULT_SCHEMA")
    require(o["schema"]=="gf-d243-persistent-coordinator/1" and
            o["staged_inert_bytes"] is False and o["network_requests"]==0 and
            o["can_install"] is False and o["operational_ready"] is False and
            o["automatic_recovery_authorized"] is False and
            o["same_user_rollback_resistant"] is False and
            o["same_user_ntfs_toctou_proven"] is False and
            o["cooperative_lock_only"] is True and
            o["abandoned_lock_recovery_authorized"] is False and
            o["status"]==("inert_trust_observed_blocked" if ok else "unknown_hold") and
            o["test_tuf_observed"] is ok,"D243_FALSE_TRUST_CLAIM")
    return o

def check_ntfs(r,ok):
    require(set(r)=={"schema","status","reason",
        "stable_inert_handle_observed","can_install","can_delete",
        "automatic_recovery_authorized","ntfs_same_user_resistance_proven"} and
        r["schema"]=="gf-d243-ntfs-observer/1" and
        r["stable_inert_handle_observed"] is ok and
        r["status"]==("inert_observed_blocked" if ok else "unknown_hold") and
        r["can_install"] is False and r["can_delete"] is False and
        r["automatic_recovery_authorized"] is False and
        r["ntfs_same_user_resistance_proven"] is False,
        "D243_NTFS_FALSE_PROOF")
    return r

def create_fixture(base,name,tag,roles,manifest):
    path=Path(tempfile.mkdtemp(prefix="gf-d243-fixture-"+name+"-",dir=base))
    (path/"GF-RELEASE-MANIFEST.json").write_bytes(manifest)
    for role,raw in roles.items():
        (path/role).write_bytes(raw)
    return path

def wait_marker(proc,marker):
    start=monotonic()
    while monotonic()-start<8:
        line=proc.stdout.readline()
        if line.strip()==marker:return
        if not line and proc.poll() is not None:break
    raise AssertionError("D243_MARKER_MISSING_"+marker)

def create_junction(path,target):
    result=subprocess.run(["cmd.exe","/c","mklink","/J",str(path),str(target)],
       stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,timeout=10)
    require(result.returncode==0 and path.is_dir() and path.is_symlink() is False,
            "D243_JUNCTION_NOT_CREATED")
    return path

def run(d0,coordinator):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),"D243_WINDOWS_RUNNER_ONLY")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    d0=Path(d0).resolve(strict=True)
    coordinator=Path(coordinator).resolve(strict=True)
    require(all(p.is_relative_to(runner) for p in (d0,coordinator)) and
            d0.name=="GF.D11.D0Probe.exe" and
            coordinator.name=="GF.D24.InertChainLab.exe","D243_CODE_SCOPE")
    root=ANCHOR.read_bytes()
    require(digest(root)=="a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "D243_PUBLIC_TEST_ROOT")
    g=load_generator()
    m1=encode_manifest(create_manifest("v5.13.11.66"))
    m2=encode_manifest(create_manifest("v5.13.11.67"))
    roles1=release_roles(g,1,m1)
    roles2=release_roles(g,2,m2)
    negatives=[]
    ntfs=[]
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d243-case-") as rawdir:
        case=Path(rawdir)
        trusted=Path(tempfile.mkdtemp(prefix="gf-d243-trust-",dir=case))
        v1=create_fixture(case,"v1","v5.13.11.66",roles1,m1)
        v2=create_fixture(case,"v2","v5.13.11.67",roles2,m2)
        first=check_observation(execute(coordinator,"--d243",v1,trusted),True)
        require(first["observed_role_generation"]==1,"D243_INITIAL_GENERATION")
        # Snapshot of SAME-USER trust state is intentionally available to attacker.
        snapshot=case/"saved-v1-trust"
        shutil.copytree(trusted,snapshot)
        # Lock contention: one C# process holds cooperative mutex; second
        # must not start a child nor mutate the cache.
        env=dict(os.environ,GF_D243_TEST_HOLD_MS="1800")
        proc=subprocess.Popen([str(coordinator),"--d243",str(v2),str(trusted)],
           stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
           text=True,env=env)
        try:
            wait_marker(proc,"GF_D243_LOCK_HELD")
            competitor=check_observation(execute(coordinator,"--d243",v2,trusted),False)
            require(competitor["child_launched"] is False and
                    competitor["reason"]=="D243_CONCURRENT_SESSION_HOLD",
                    "CONCURRENT_CHILD_LAUNCHED")
            negatives.append("cooperative-concurrent-lock")
            stdout,stderr=proc.communicate(timeout=28)
            require(proc.returncode==0,"D243_SECOND_GENERATION_EXIT_"+stderr[-500:])
            lines=[s for s in stdout.splitlines()
                   if s.startswith("GF_D243_OBSERVATION ")]
            require(len(lines)==1,"D243_GENERATION2_REPORT")
            second=check_observation(json.loads(lines[0].split(" ",1)[1]),True)
            require(second["observed_role_generation"]==2,
                    "D243_PERSISTED_VERSION_NOT_ADVANCED")
        finally:
            if proc.poll() is None:
                proc.kill();proc.communicate(timeout=5)

        intact=check_observation(execute(coordinator,"--d243",v1,trusted),False)
        require(intact["child_launched"] is True and
                intact["observed_role_generation"]==0,
                "D243_REPLAY_WITH_INTACT_HISTORY_ACCEPTED")
        negatives.append("intact-history-replay-blocked")
        # An incomplete cache must HOLD. No deletion/rebootstrap.
        partial=Path(tempfile.mkdtemp(prefix="gf-d243-trust-partial-",dir=case))
        shutil.copy2(snapshot/"root.json",partial/"root.json")
        incomplete=check_observation(execute(coordinator,"--d243",v1,partial),False)
        require(incomplete["child_launched"] is True,"PARTIAL_TRUST_NOT_TESTED")
        require((partial/"root.json").is_file() and
                not (partial/"timestamp.json").exists(),"PARTIAL_CACHE_MODIFIED")
        negatives.append("partial-trust-history-hold")

        # Demonstrate same-user full-rewind counterexample: replacing entire
        # trusted directory reaccepts old version. NEVER interpret as H1 proof.
        shutil.rmtree(trusted)
        shutil.copytree(snapshot,trusted)
        rewound=check_observation(execute(coordinator,"--d243",v1,trusted),True)
        require(rewound["observed_role_generation"]==1,
                "H1_EXPECTED_COUNTEREXAMPLE_NOT_REPRODUCED")

        # NTFS observations and preservation of external sentinel.
        safe=Path(tempfile.mkdtemp(prefix="gf-d243-probe-safe-",dir=case))
        filename=safe/"data.fixture.txt"
        filename.write_bytes(b"D243-ORIGINAL-INERT-NO-EXE\n")
        ext=case/"outside-personal-sentinel.fixture.txt"
        ext.write_bytes(b"PERSONAL-AREA-PRESERVE\n")
        check_ntfs(execute(coordinator,"--d243-probe",safe,"data.fixture.txt"),True)
        ntfs.append("regular-handle-identity")

        # A second hardlink can alter bytes from another path -> HOLD.
        hard=case/"hardlink-alias.fixture.txt"
        os.link(filename,hard)
        check_ntfs(execute(coordinator,"--d243-probe",safe,"data.fixture.txt"),False)
        ntfs.append("hardlink-unknown")
        hard.unlink()

        # Real NTFS junction redirects an ancestor outside nominal root.
        junction=case/"gf-d243-probe-junction"
        create_junction(junction,safe)
        try:
            check_ntfs(execute(coordinator,"--d243-probe",junction,"data.fixture.txt"),False)
            ntfs.append("real-junction-ancestor")
        finally:
            os.rmdir(junction)  # remove junction only, NEVER its target

        # File reparse point/symlink: a link to outside should be HOLD.
        if hasattr(os,"symlink"):
            target=case/"external-ntfs.fixture.txt"
            target.write_bytes(b"EXTERNAL-INERT-PRESERVE")
            filename.unlink()
            os.symlink(target,filename,target_is_directory=False)
            check_ntfs(execute(coordinator,"--d243-probe",safe,"data.fixture.txt"),False)
            ntfs.append("real-file-reparse")
            filename.unlink()
            filename.write_bytes(b"D243-ORIGINAL-INERT-NO-EXE\n")

        # Replacement while pinned original handle remains open. Both old and
        # new bytes remain in lab, coordinator returns unknown HOLD.
        env=dict(os.environ,GF_D243_PROBE_HOLD_MS="1600")
        proc=subprocess.Popen([str(coordinator),"--d243-probe",str(safe),
                  "data.fixture.txt"],stdin=subprocess.DEVNULL,
                  stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env)
        try:
            wait_marker(proc,"GF_D243_PROBE_HANDLE_OPEN")
            prior=safe/"original-preserved.fixture.txt"
            os.replace(filename,prior)
            filename.write_bytes(b"D243-ATTACKER-NEW-INERT-BYTES\n")
            output,error=proc.communicate(timeout=20)
            require(proc.returncode==0,"D243_PROBE_CRASH_"+error[-500:])
            lines=[s for s in output.splitlines() if s.startswith("GF_D243_NTFS ")]
            require(len(lines)==1,"D243_REPLACE_REPORT")
            check_ntfs(json.loads(lines[0][len("GF_D243_NTFS "):]),False)
            require(prior.read_bytes()==b"D243-ORIGINAL-INERT-NO-EXE\n" and
                    filename.read_bytes()==b"D243-ATTACKER-NEW-INERT-BYTES\n",
                    "REPLACEMENT_FILES_NOT_PRESERVED")
            ntfs.append("pinned-handle-path-swap-hold")
        finally:
            if proc.poll() is None:
                proc.kill();proc.communicate(timeout=5)

        # Same file ID can have changed contents; compare hash captured
        # BEFORE releasing attacker to catch a write through existing handle.
        filename.write_bytes(b"D243-ORIGINAL-INERT-NO-EXE\n")
        proc=subprocess.Popen([str(coordinator),"--d243-probe",str(safe),
                  "data.fixture.txt"],stdin=subprocess.DEVNULL,
                  stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env)
        try:
            wait_marker(proc,"GF_D243_PROBE_HANDLE_OPEN")
            filename.write_bytes(b"D243-SAME-ID-CHANGED-CONTENT\n")
            output,error=proc.communicate(timeout=20)
            require(proc.returncode==0,"D243_CONTENT_PROBE_CRASH_"+error[-500:])
            lines=[s for s in output.splitlines() if s.startswith("GF_D243_NTFS ")]
            require(len(lines)==1,"D243_CONTENT_PROBE_REPORT")
            check_ntfs(json.loads(lines[0][len("GF_D243_NTFS "):]),False)
            ntfs.append("same-file-modified-hash-hold")
        finally:
            if proc.poll() is None:
                proc.kill();proc.communicate(timeout=5)

        require(ext.read_bytes()==b"PERSONAL-AREA-PRESERVE\n",
                "OUTSIDE_SENTINEL_TOUCHED")
        require(len(negatives)==3 and len(ntfs)==6,"D243_MATRIX_INCOMPLETE")
        report={
            "schema":"gf-d243-persistent-ntfs-lab/1",
            "status":"passed",
            "csharp_controlled_test_tuf_generations":2,
            "independent_windows_coordinator_processes":True,
            "observed_trust_role_generations":[1,2],
            "intact_history_replay_blocked":True,
            "cooperative_concurrency_denied":True,
            "partial_history_held_without_cleanup":True,
            "full_history_rewind_reaccepted_old_version":True,
            "full_history_rewind_is_security_counterexample":True,
            "ntfs_observation_cases":len(ntfs),
            "reparse_junction_hardlink_and_race_tested":True,
            "external_sentinel_preserved":True,
            "same_user_rollback_resistance_proven":False,
            "same_user_ntfs_toctou_resistance_proven":False,
            "crash_durability_proven":False,
            "production_signing_authority":False,
            "operational_ready":False,
            "automatic_recovery_authorized":False,
            "real_external_network_requests":0,
            "real_executable_downloads":0,
            "installations":0,"product_modified":False,
            "staging_or_recovery_performed":False
        }
        print("GF_D243_RESULT "+json.dumps(report,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==3,"D243_ARG_COUNT")
    raise SystemExit(run(sys.argv[1],sys.argv[2]))
