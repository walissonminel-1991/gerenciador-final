"""D1.2 isolated Windows test: fresh processes, D0/D1 release binding, rewind.

This is an explicitly vulnerable, PUBLIC-TEST-KEY laboratory. No product code,
network, downloads, installation, production keys, or automatic recovery.
"""
from __future__ import annotations

import base64
import copy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from urllib.parse import urlsplit

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "github-trust-lab"))
sys.path.insert(0, str(HERE.parent / "github-auth-lab"))
from d11_persistent_lab import observe, release_roles, D0_SOURCE
from d1_release_manifest_lab import (
    ANCHOR, ARCH, ASSET, D1Rejected, PAYLOAD, REPOSITORY,
    create_manifest, encode_manifest, load_generator, parse_object,
    require, digest
)
from tuf.api import exceptions

SCHEMA = "gf-github-d12-case/1"
RELEASE_URL = "https://github.com/" + REPOSITORY + "/releases/download/"
SAFE_ROLES = {"timestamp.json", "snapshot.json", "targets.json"}

def release_fixture(tag, **changes):
    release = {
        "tag_name": tag, "draft": False, "prerelease": False,
        "assets": [{
            "name": ASSET,
            "size": len(PAYLOAD),
            "browser_download_url": RELEASE_URL + tag + "/" + ASSET
        }]
    }
    release.update(changes)
    return json.dumps(release, separators=(",", ":"), sort_keys=True).encode()

def inspect_release(raw, parsed_d0, manifest):
    # All GitHub fields are untrusted hints. The TUF-authenticated manifest is
    # the source of package authority; none of these URLs are opened.
    listing = parse_object(raw)
    require(set(listing) == {"tag_name", "draft", "prerelease", "assets"},
            "RELEASE_FIXTURE_SCHEMA")
    require(listing["draft"] is False and
            listing["prerelease"] is False and
            type(listing["tag_name"]) is str and
            parsed_d0["state"] == "NewVersionMetadataOnly" and
            type(parsed_d0["tag"]) is str and
            listing["tag_name"] == parsed_d0["tag"],
            "RELEASE_D0_CANDIDATE")
    signed = parse_object(manifest)
    require(type(listing["assets"]) is list and len(listing["assets"]) == 1,
            "RELEASE_ASSET_CARDINALITY")
    item = listing["assets"][0]
    require(type(item) is dict and
            set(item) == {"name", "size", "browser_download_url"},
            "RELEASE_ASSET_FIELDS")
    require(type(item["name"]) is str and item["name"] == ASSET and
            item["name"] == signed["asset"] and
            type(item["size"]) is int and item["size"] == signed["length"] and
            type(item["browser_download_url"]) is str and
            item["browser_download_url"] == (
                RELEASE_URL + listing["tag_name"] + "/" + ASSET),
            "RELEASE_ASSET_ASSOCIATION")
    # Equality of a remote URL and a manifest is not file authentication.
    # The manifest itself must still be validated by a pinned TUF chain.

def actual_d0(exe, release_file, installed):
    raw_before = release_file.read_bytes()
    p = subprocess.run([str(exe),str(release_file),installed],
                       stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                       stderr=subprocess.DEVNULL, text=True, timeout=20,
                       check=True)
    require(release_file.read_bytes() == raw_before, "D0_RELEASE_CHANGED_DURING_PROBE")
    lines=[s for s in p.stdout.splitlines() if s.startswith("GF_D11_D0 ")]
    require(len(lines) == 1, "D0_OUTPUT_COUNT")
    result=json.loads(lines[0][len("GF_D11_D0 "):])
    require(set(result) == {"schema","source","state","tag","read_only",
                            "can_install","ready","recovery"} and
            result["schema"] == "gf-d11-d0/1" and
            result["source"] == D0_SOURCE and
            result["read_only"] is True and
            result["can_install"] is False and result["ready"] is False and
            result["recovery"] is False, "D0_READ_ONLY")
    return result, raw_before

def worker(args):
    require(sys.platform == "win32" and
            os.environ.get("GITHUB_ACTIONS") == "true" and
            os.environ.get("RUNNER_TEMP"), "DISPOSABLE_WINDOWS_REQUIRED")
    root=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    require(len(args)==4, "WORKER_ARGUMENT_COUNT")
    exe, trust, spec_path, release_file = [Path(a).resolve() for a in args]
    for item in (exe,trust,spec_path,release_file):
        require(item.is_relative_to(root), "WORKER_OUTSIDE_RUNNER")
    require(exe.is_file() and exe.name=="GF.D11.D0Probe.exe" and
            trust.is_dir() and spec_path.is_file() and release_file.is_file(),
            "WORKER_INPUT_NOT_PRESENT")
    spec=parse_object(spec_path.read_bytes())
    require(set(spec)=={"schema","bootstrap","installed","manifest","roles"} and
            spec["schema"]==SCHEMA and
            type(spec["bootstrap"]) is bool and
            type(spec["installed"]) is str and
            type(spec["manifest"]) is str and
            type(spec["roles"]) is dict and
            set(spec["roles"])==SAFE_ROLES, "WORKER_CASE_SCHEMA")
    manifest=base64.b64decode(spec["manifest"],validate=True)
    roles={role:base64.b64decode(value,validate=True)
           for role,value in spec["roles"].items()}
    require(all(0<len(value)<524288 for value in roles.values()) and
            0<len(manifest)<65536,"WORKER_INPUT_BOUNDS")
    result={"schema":"gf-github-d12-worker/1","pid":os.getpid(),
            "status":"rejected","reason":"INCONCLUSIVE",
            "versions":None,"can_install":False,
            "operational_ready":False,"automatic_recovery_authorized":False}
    try:
        d0,raw=actual_d0(exe,release_file,spec["installed"])
        # Association is verified for this exact in-memory Release response.
        inspect_release(raw,d0,manifest)
        anchor=ANCHOR.read_bytes() if spec["bootstrap"] else None
        versions=observe(trust,anchor,roles,manifest,d0,spec["installed"])
        result.update(status="accepted_test_only",reason="LAB_CONTRACT_ONLY",
                      versions=versions)
    except (D1Rejected, exceptions.RepositoryError,
            exceptions.DownloadError, ValueError, OSError) as exc:
        result["reason"]=type(exc).__name__  # never log fixture contents
    print("GF_D12_WORKER "+json.dumps(result,sort_keys=True))
    return 0

def encode_case(manifest,roles,installed,bootstrap):
    return json.dumps({
        "schema":SCHEMA,"bootstrap":bootstrap,"installed":installed,
        "manifest":base64.b64encode(manifest).decode(),
        "roles":{key:base64.b64encode(value).decode()
                 for key,value in roles.items()}
    },sort_keys=True).encode()

def controller(exe):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),"LAB_ENV_ONLY")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe=Path(exe).resolve(strict=True)
    require(exe.is_relative_to(runner) and
            exe.name=="GF.D11.D0Probe.exe", "D0_EXE_OUTSIDE_LAB")
    g=load_generator()
    require(digest(ANCHOR.read_bytes())==
        "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
        "D12_PUBLIC_TEST_BOOTSTRAP")
    visited_pids=[]
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d12-") as folder:
        root=Path(folder)
        trust=root/"persistent";trust.mkdir()
        man66=encode_manifest(create_manifest("v5.13.11.66"))
        man67=encode_manifest(create_manifest("v5.13.11.67"))
        r1=release_roles(g,1,man66)
        r2=release_roles(g,2,man67)
        r3=release_roles(g,3,man67)
        release66=release_fixture("v5.13.11.66")
        release67=release_fixture("v5.13.11.67")

        def child(label, roles, manifest, release, *, installed="5.13.11.65",
                  bootstrap=False, working=None, expect=True):
            state=working if working is not None else trust
            case=root/("case-"+label+".json")
            rf=root/("release-"+label+".json")
            require(not case.exists() and not rf.exists(),"CASE_REUSED")
            case.write_bytes(encode_case(manifest,roles,installed,bootstrap))
            rf.write_bytes(release)
            p=subprocess.run([sys.executable,str(Path(__file__).resolve()),
                              "--worker",str(exe),str(state),str(case),str(rf)],
                             cwd=str(root), stdin=subprocess.DEVNULL,
                             stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                             text=True,timeout=35)
            require(p.returncode==0,"WORKER_CRASH_"+label)
            outputs=[line for line in p.stdout.splitlines()
                     if line.startswith("GF_D12_WORKER ")]
            require(len(outputs)==1,"WORKER_REPORT_"+label)
            data=json.loads(outputs[0][len("GF_D12_WORKER "):])
            require(set(data)=={"schema","pid","status","reason","versions",
                                "can_install","operational_ready",
                                "automatic_recovery_authorized"} and
                    data["schema"]=="gf-github-d12-worker/1" and
                    type(data["pid"]) is int and data["pid"]!=os.getpid() and
                    data["pid"] not in visited_pids and
                    data["can_install"] is False and
                    data["operational_ready"] is False and
                    data["automatic_recovery_authorized"] is False,
                    "WORKER_FALSE_AUTHORITY_"+label)
            visited_pids.append(data["pid"])
            wanted="accepted_test_only" if expect else "rejected"
            require(data["status"]==wanted, "UNEXPECTED_"+label)
            return data

        first=child("v1",r1,man66,release66,bootstrap=True)
        require(first["versions"]==[1,1,1,1],"V1_NOT_AUTHENTICATED")
        preserved=root/"pre-upgrade-trust"
        shutil.copytree(trust,preserved)
        second=child("v2",r2,man67,release67,installed="5.13.11.66")
        require(second["versions"]==[1,2,2,2],"V2_NOT_AUTHENTICATED")
        require(all((trust/(role+".json")).is_file()
                    for role in ("root","timestamp","snapshot","targets")),
                "PERSISTENCE_NOT_OBSERVED")
        trusted_v2=root/"trust-v2-snapshot"
        shutil.copytree(trust,trusted_v2)
        adversarial=[]

        def deny(label,roles=r3,manifest=man67,release=release67,installed="5.13.11.65"):
            cloned=root/("clone-"+label)
            shutil.copytree(trusted_v2,cloned)
            child(label,roles,manifest,release,installed=installed,
                  working=cloned,expect=False)
            adversarial.append(label)

        deny("old-timestamp-replay",r1,man66,release66)
        mixed=dict(r3);mixed["snapshot.json"]=r1["snapshot.json"]
        deny("mixed-old-snapshot",mixed)
        mixed=dict(r3);mixed["targets.json"]=r1["targets.json"]
        deny("mixed-old-targets",mixed)
        deny("wrong-release-tag",release=release_fixture("v5.13.11.68"))
        deny("draft-release",release=release_fixture("v5.13.11.67",draft=True))
        deny("prerelease",release=release_fixture("v5.13.11.67",prerelease=True))
        deny("asset-missing",release=release_fixture("v5.13.11.67",assets=[]))
        deny("asset-duplicate",release=release_fixture("v5.13.11.67",
            assets=[json.loads(release67)["assets"][0]]*2))
        deny("asset-name-spoof",release=release_fixture("v5.13.11.67",
            assets=[{**json.loads(release67)["assets"][0],"name":"evil.zip"}]))
        deny("asset-size-spoof",release=release_fixture("v5.13.11.67",
            assets=[{**json.loads(release67)["assets"][0],"size":1}]))
        deny("asset-size-bool",release=release_fixture("v5.13.11.67",
            assets=[{**json.loads(release67)["assets"][0],"size":True}]))
        deny("asset-url-external",release=release_fixture("v5.13.11.67",
            assets=[{**json.loads(release67)["assets"][0],
                     "browser_download_url":"https://evil.invalid/a.zip"}]))
        deny("stale-manifest",manifest=man66)
        deny("signed-wrong-repository",roles=release_roles(g,3,
            encode_manifest({**create_manifest("v5.13.11.67"),
                             "repository":"attacker/elsewhere"})),
            manifest=encode_manifest({**create_manifest("v5.13.11.67"),
                                      "repository":"attacker/elsewhere"}))
        deny("signed-wrong-architecture",roles=release_roles(g,3,
            encode_manifest({**create_manifest("v5.13.11.67"),
                             "architecture":"win-arm64"})),
            manifest=encode_manifest({**create_manifest("v5.13.11.67"),
                                      "architecture":"win-arm64"}))
        deny("signed-older-than-installed",installed="v5.13.11.68")
        deny("expired-timestamp",roles=release_roles(g,3,man67,expired=True))
        altered=dict(r3);altered["targets.json"]+=b"tampered"
        deny("tampered-targets",roles=altered)

        # Deliberately demonstrate the existing H1 weakness:
        # user-writable complete trust-state rewind makes a v1 Release
        # appear valid again. This is an EXPECTED COUNTEREXAMPLE.
        rewound=root/"rewound-trust"
        shutil.copytree(preserved,rewound)
        replay=child("complete-history-rewind",r1,man66,release66,
                     working=rewound,expect=True)
        require(replay["versions"]==[1,1,1,1],"H1_COUNTEREXAMPLE_NOT_OBSERVED")
        require(len(adversarial)==18 and len(set(adversarial))==18,
                "D12_ADVERSARIAL_MATRIX")
        report={
            "schema":"gf-github-d12-crossprocess/1",
            "status":"passed",
            "independent_windows_processes":len(visited_pids),
            "trusted_generations_across_processes":2,
            "intact_history_negative_controls":len(adversarial),
            "full_history_rewind_accepted":True,
            "rewind_is_security_counterexample":True,
            "same_user_rollback_protection_proven":False,
            "cryptographic_production_authority":False,
            "process_crash_durability_proven":False,
            "test_root_public":True,
            "real_network_requests":0,
            "real_executable_downloads":0,
            "product_modified":False,
            "operational_ready":False,
            "automatic_recovery_authorized":False
        }
        print("GF_D12_RESULT "+json.dumps(report,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)>1,"D12_ARGS")
    if sys.argv[1]=="--worker":
        raise SystemExit(worker(sys.argv[2:]))
    require(len(sys.argv)==2,"D12_CONTROLLER_ARGS")
    raise SystemExit(controller(sys.argv[1]))
