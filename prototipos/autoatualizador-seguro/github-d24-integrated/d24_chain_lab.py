"""D2.4.2: exercise single C# coordinator -> embedded Python-TUF -> same D2.3 Stage.

All input bytes and private test signer seeds are PUBLIC. This proves the
intended logical gate, not security from another process running as same user.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

HERE=Path(__file__).resolve().parent
for folder in ("github-d2-inert","github-trust-lab","github-crossprocess-lab",
               "github-auth-lab"):
    sys.path.insert(0,str(HERE.parent/folder))
from d11_persistent_lab import release_roles
from d12_crossprocess_lab import release_fixture
from d1_release_manifest_lab import (
    ANCHOR,D1Rejected,create_manifest,digest,encode_manifest,load_generator,
    require,PAYLOAD
)

TAG="v5.13.11.66"
OLD="5.13.11.65"

def invoke(exe,root,case,installed=OLD,tag=TAG,legacy=False,env=None):
    args=[str(exe)]
    if legacy:
        args += ["--d24",str(case/"GF-RELEASE-MANIFEST.json"),
                 digest((case/"GF-RELEASE-MANIFEST.json").read_bytes()),
                 tag,installed]
    else:
        args += ["--d242",str(case)]
    child=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                         stdin=subprocess.DEVNULL,text=True,timeout=28,env=env)
    if legacy:
        require(child.returncode != 0 and
                "GF_D242_COORDINATOR " not in child.stdout,
                "LEGACY_DIRECT_STAGE_WAS_NOT_DISABLED")
        return None
    require(child.returncode==0,"COORDINATOR_CRASH_"+case.name)
    lines=[s for s in child.stdout.splitlines()
           if s.startswith("GF_D242_COORDINATOR ")]
    require(len(lines)==1,"COORDINATOR_REPORT_COUNT_"+case.name)
    r=json.loads(lines[0][len("GF_D242_COORDINATOR "):])
    require(set(r)=={
        "schema","status","reason","tuf_child_launched",
        "tuf_in_session_validated","staged_inert_bytes","loopback_connections",
        "reused_dotnet_d23_stage","old_d24_direct_handoff_allowed",
        "same_user_process_isolation_proven","production_authority",
        "can_install","operational_ready","automatic_recovery_authorized"
    } and
    r["schema"]=="gf-d242-session-coordinator/1" and
    r["reused_dotnet_d23_stage"] is True and
    r["old_d24_direct_handoff_allowed"] is False and
    r["same_user_process_isolation_proven"] is False and
    r["production_authority"] is False and
    r["can_install"] is False and
    r["operational_ready"] is False and
    r["automatic_recovery_authorized"] is False,
    "COORDINATOR_FALSE_AUTHORITY_"+case.name)
    return r

def main(d0_exe,coordinator):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),"DISPOSABLE_WINDOWS_ONLY")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    d0_exe=Path(d0_exe).resolve(strict=True)
    coordinator=Path(coordinator).resolve(strict=True)
    require(all(p.is_relative_to(runner) for p in (d0_exe,coordinator)) and
            d0_exe.name=="GF.D11.D0Probe.exe" and
            coordinator.name=="GF.D24.InertChainLab.exe",
            "EXPERIMENTAL_BINARIES_ONLY")
    require(digest(ANCHOR.read_bytes())==
            "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "PUBLIC_ROOT_PIN")
    generator=load_generator()
    authentic=encode_manifest(create_manifest(TAG))
    roles=release_roles(generator,1,authentic)
    release=release_fixture(TAG)
    positive=[]
    rejected=[]
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d242-samples-") as tmp:
        root=Path(tmp)
        def fixture(name,manifest=authentic,role_files=roles,release_bytes=release):
            # Directory name is deliberately constrained by the coordinator.
            child=Path(tempfile.mkdtemp(dir=runner,prefix="gf-d242-"+name+"-"))
            (child/"GF-RELEASE-MANIFEST.json").write_bytes(manifest)
            (child/"release.json").write_bytes(release_bytes)
            for key,body in role_files.items():
                (child/key).write_bytes(body)
            return child

        def deny(name,manifest=authentic,role_files=roles,release_bytes=release,
                 env=None):
            case=fixture(name,manifest,role_files,release_bytes)
            result=invoke(coordinator,root,case,env=env)
            require(result["status"]=="rejected" and
                    result["tuf_in_session_validated"] is False and
                    result["staged_inert_bytes"] is False and
                    result["loopback_connections"]==0,
                    "FALSE_ACCEPT_"+name)
            rejected.append(name)

        valid=fixture("valid")
        good=invoke(coordinator,root,valid)
        require(good["status"]=="accepted_inert_test_only" and
                good["tuf_child_launched"] is True and
                good["tuf_in_session_validated"] is True and
                good["staged_inert_bytes"] is True and
                good["loopback_connections"]==1,"VALID_CHAIN_FAILED")
        positive.append("valid")
        # A second invocation into the same materialized trust directory
        # must not silently rebootstrap as a fresh first-use session.
        replay=invoke(coordinator,root,valid)
        require(replay["status"]=="rejected" and
                replay["loopback_connections"]==0 and
                replay["staged_inert_bytes"] is False,
                "REPLAYED_SESSION_ACCEPTED")
        rejected.append("replay-session")

        # Explicit old direct invocation, now no staging path at all.
        invoke(coordinator,root,valid,legacy=True)
        rejected.append("legacy-direct-handoff")
        bad_manifest=authentic+b" "
        deny("whitespace-rehashed",manifest=bad_manifest)
        deny("tag-mismatch",release_bytes=release_fixture("v5.13.11.67"))
        deny("release-draft",release_bytes=release_fixture(TAG,draft=True))
        asset=json.loads(release)["assets"][0]
        deny("asset-name",release_bytes=release_fixture(TAG,assets=[
            {**asset,"name":"evil.zip"}]))
        deny("asset-length",release_bytes=release_fixture(TAG,assets=[
            {**asset,"size":999}]))
        deny("asset-external",release_bytes=release_fixture(TAG,assets=[
            {**asset,"browser_download_url":"https://evil.invalid/x"}]))
        invalid=dict(roles);invalid["targets.json"]=roles["targets.json"]+b"x"
        deny("targets-tampered",role_files=invalid)
        deny("timestamp-expired",
             role_files=release_roles(generator,1,authentic,expired=True))
        missing=dict(roles);missing.pop("timestamp.json")
        deny("missing-verifier-input",role_files=missing)
        changed=encode_manifest({**create_manifest(TAG),"architecture":"win-arm64"})
        deny("signed-wrong-platform",manifest=changed,
             role_files=release_roles(generator,1,changed))
        changed=encode_manifest({**create_manifest(TAG),"repository":"evil/elsewhere"})
        deny("signed-wrong-repository",manifest=changed,
             role_files=release_roles(generator,1,changed))
        dup=authentic.replace(b'"sha256":',b'"sha256":"0","sha256":',1)
        deny("duplicate-manifest-key",manifest=dup)
        missing_py=dict(os.environ)
        missing_py["PATH"]=""
        deny("no-python-validator",env=missing_py)

        # The embedded verifier is the only one invoked; stdout from an
        # external caller is not an input. Unit-test the parser with fake,
        # stale, mismatched, absent and duplicate authorization receipts.
        test=subprocess.run([str(coordinator),"--d242-selftest"],
            stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,text=True,timeout=12)
        require(test.returncode==0,"RECEIPT_PARSER_SELFTEST_FAILED")
        line=[s for s in test.stdout.splitlines()
              if s.startswith("GF_D242_RECEIPT_SELFTEST ")]
        require(len(line)==1,"RECEIPT_SELFTEST_MISSING")
        receipt=json.loads(line[0][len("GF_D242_RECEIPT_SELFTEST "):])
        require(receipt=={
            "schema":"gf-d242-receipt-selftest/1","status":"passed",
            "synthetic_receipts_rejected":7,"stages_started":0,
            "automatic_recovery_authorized":False
        },"FAKE_OR_REPLAY_RECEIPT_WAS_ACCEPTED")
        require(len(positive)==1 and len(rejected)==15 and
                len(set(rejected))==15,"NEGATIVE_MATRIX")
        report={
            "schema":"gf-d242-csharp-owned-tuf-session/1",
            "status":"passed",
            "positive_inert_sessions":len(positive),
            "negative_sessions_rejected":len(rejected),
            "synthetic_receipts_rejected":receipt["synthetic_receipts_rejected"],
            "embedded_tuf_validator_source":True,
            "csharp_owns_child_and_stage":True,
            "old_direct_manifest_sha_handoff_rejected":True,
            "bootstrap_single_pinned_snapshot_used":True,
            "shared_dotnet_d23_transport":True,
            "session_replay_rejected":True,
            "secure_against_same_user_process_replacement":False,
            "persistent_tuf_history_rollback_proven":False,
            "same_user_ntfs_toctou_proven":False,
            "test_signer_public_only":True,
            "external_requests":0,"downloaded_executables":0,
            "installations":0,"product_modified":False,
            "operational_ready":False,"automatic_recovery_authorized":False
        }
        print("GF_D242_RESULT "+json.dumps(report,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==3,"D242_CLI")
    raise SystemExit(main(Path(sys.argv[1]),Path(sys.argv[2])))
