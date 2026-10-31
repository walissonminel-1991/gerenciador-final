"""D1.1: offline D0 actual C# decision plus python-tuf persisted role history.

LAB ONLY. Test signing keys are public; no product updater authorization.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from urllib.parse import urlsplit

from tuf.ngclient import Updater
from tuf.ngclient.fetcher import FetcherInterface
from tuf.api import exceptions

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "github-auth-lab"))
from d1_release_manifest_lab import (
    ANCHOR, ARCH, PAYLOAD, TARGET_NAME, D1Rejected, create_manifest,
    encode_manifest, load_generator, sign, digest, check_release, require
)
URL = "https://gf-d11-test.invalid/"
D0_SOURCE = "https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest"

class FixtureFetcher(FetcherInterface):
    def __init__(self, items):
        self.items = items
        self.requested = []
    def _fetch(self, url):
        info = urlsplit(url)
        name = info.path.lstrip("/")
        require(info.scheme == "https" and info.hostname == "gf-d11-test.invalid"
                and info.port is None and not info.query and not info.fragment
                and name in {"timestamp.json", "snapshot.json", "targets.json"},
                "FETCHER_OUT_OF_SCOPE")
        self.requested.append(name)
        if name not in self.items:
            raise exceptions.DownloadHTTPError("missing inert metadata", 404)
        yield self.items[name]

def release_roles(g, version, manifest, expired=False):
    now = datetime.now(timezone.utc).replace(microsecond=0)
    base = {"spec_version":"1.0.36","version":version,
            "expires":g.utc(now+timedelta(minutes=20))}
    targets = dict(base,_type="targets",targets={TARGET_NAME:{
        "length":len(manifest),"hashes":{"sha256":digest(manifest)},
        "custom":{"gf_d1":{"schema":"gf-github-d1-target/1",
                           "test_only":True,"operational_installable":False}}}})
    tgt = sign(targets,"targets",g)
    snap = sign(dict(base,_type="snapshot",meta={"targets.json":{
        "version":version,"length":len(tgt),"hashes":{"sha256":digest(tgt)}}}),
        "snapshot",g)
    timestamp = dict(base,_type="timestamp",meta={"snapshot.json":{
        "version":version,"length":len(snap),"hashes":{"sha256":digest(snap)}}})
    if expired:
        timestamp["expires"]="2020-01-01T00:00:00Z"
    return {"timestamp.json":sign(timestamp,"timestamp",g),
            "snapshot.json":snap,"targets.json":tgt}

def discover(exe,work,tag,old="5.13.11.65", *, draft=False, duplicate=False):
    release={"tag_name":tag,"draft":draft,"prerelease":False,
             "assets":[{"browser_download_url":"https://evil.invalid/ignored"}]}
    data=json.dumps(release,separators=(",",":")).encode()
    if duplicate: data=data.replace(b'"tag_name":',
                          b'"tag_name":"v99.0.0.0","tag_name":',1)
    filename=work/"release-offline.json"
    filename.write_bytes(data)
    p=subprocess.run([str(exe),str(filename),old],check=True,
         stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,
         stderr=subprocess.DEVNULL,text=True,timeout=20)
    matches=[s for s in p.stdout.splitlines() if s.startswith("GF_D11_D0 ")]
    require(len(matches)==1,"D0_RESULT_COUNT")
    result=json.loads(matches[0][len("GF_D11_D0 "):])
    require(set(result)=={"schema","source","state","tag","read_only",
                         "can_install","ready","recovery"} and
            result["schema"]=="gf-d11-d0/1" and
            result["source"]==D0_SOURCE and
            result["read_only"] is True and result["can_install"] is False and
            result["ready"] is False and result["recovery"] is False,
            "D0_READ_ONLY_CONTRACT")
    return result

def observe(trust,anchor,roles,manifest,discovery,installed):
    fetcher=FixtureFetcher(roles)
    u=Updater(str(trust), URL,target_dir=str(trust/"inert-targets"),
              target_base_url=URL,fetcher=fetcher,bootstrap=anchor)
    u.refresh()
    info=u.get_targetinfo(TARGET_NAME)
    require(info is not None and info.length==len(manifest) and
            info.hashes.get("sha256")==digest(manifest),
            "PERSISTENT_TUF_TARGET_MISMATCH")
    require(discovery["state"]=="NewVersionMetadataOnly" and
            type(discovery["tag"]) is str,"D0_NOT_CANDIDATE")
    result=check_release(ANCHOR.read_bytes(),
          {"timestamp":roles["timestamp.json"],"snapshot":roles["snapshot.json"],
           "targets":roles["targets.json"]},manifest,
          discovery["tag"],installed,ARCH,PAYLOAD)
    require(result["tuf_verified"] is True and
            result["production_authority"] is False and
            result["can_install"] is False and
            result["operational_ready"] is False and
            result["automatic_recovery_authorized"] is False,"D1_NEVER_INSTALL")
    return [getattr(u._trusted_set,role).version
            for role in ("root","timestamp","snapshot","targets")]

def reject(fn,name,rejections):
    try:
        fn()
    except (D1Rejected,exceptions.RepositoryError,exceptions.DownloadError,
            OSError,ValueError):
        rejections.append(name)
        return
    raise AssertionError("FALSE_ACCEPT_"+name)

def run(exe):
    require(sys.platform=="win32" and os.environ.get("GITHUB_ACTIONS")=="true"
            and os.environ.get("RUNNER_TEMP"),"WINDOWS_DISPOSABLE_ONLY")
    tmp=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe=Path(exe).resolve(strict=True)
    require(exe.is_relative_to(tmp) and exe.name=="GF.D11.D0Probe.exe",
            "EXECUTABLE_NOT_LAB")
    g=load_generator()
    anchor=ANCHOR.read_bytes()
    require(digest(anchor)=="a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "PINNED_TEST_ROOT")
    with tempfile.TemporaryDirectory(dir=tmp,prefix="gf-d11-") as d:
        work=Path(d)
        trust=work/"trusted";trust.mkdir()
        p66=encode_manifest(create_manifest("v5.13.11.66"))
        p67=encode_manifest(create_manifest("v5.13.11.67"))
        r1=release_roles(g,1,p66)
        r2=release_roles(g,2,p67)
        d66=discover(exe,work,"v5.13.11.66")
        d67=discover(exe,work,"v5.13.11.67")
        versions1=observe(trust,anchor,r1,p66,d66,"5.13.11.65")
        versions2=observe(trust,None,r2,p67,d67,"5.13.11.66")
        require(versions1==[1,1,1,1] and versions2==[1,2,2,2],
                "ROLE_VERSIONS_DID_NOT_ADVANCE")
        require(all((trust/(k+".json")).is_file()
                    for k in ("root","timestamp","snapshot","targets")),
                "TRUST_METADATA_NOT_PERSISTED")

        denied=[]
        reject(lambda:observe(trust,None,r1,p66,d66,"5.13.11.65"),
               "timestamp-rollback",denied)
        mixed=dict(r2);mixed["snapshot.json"]=r1["snapshot.json"]
        reject(lambda:observe(trust,None,mixed,p67,d67,"5.13.11.65"),
               "snapshot-mix-and-match",denied)
        mixed=dict(r2);mixed["targets.json"]=r1["targets.json"]
        reject(lambda:observe(trust,None,mixed,p67,d67,"5.13.11.65"),
               "targets-mix-and-match",denied)
        reject(lambda:observe(trust,None,r2,p66,d67,"5.13.11.65"),
               "signed-manifest-stale",denied)
        wrong=discover(exe,work,"v5.13.11.68")
        reject(lambda:observe(trust,None,r2,p67,wrong,"5.13.11.65"),
               "D0-D1-tag-mismatch",denied)
        draft=discover(exe,work,"v5.13.11.67",draft=True)
        reject(lambda:observe(trust,None,r2,p67,draft,"5.13.11.65"),
               "D0-draft",denied)
        dup=discover(exe,work,"v5.13.11.67",duplicate=True)
        reject(lambda:observe(trust,None,r2,p67,dup,"5.13.11.65"),
               "D0-duplicate-key",denied)
        same=discover(exe,work,"v5.13.11.65")
        reject(lambda:observe(trust,None,r2,p67,same,"5.13.11.65"),
               "D0-not-new",denied)
        expired=release_roles(g,3,p67,expired=True)
        reject(lambda:observe(trust,None,expired,p67,d67,"5.13.11.65"),
               "timestamp-expired",denied)
        absent=dict(r2);absent.pop("timestamp.json")
        reject(lambda:observe(trust,None,absent,p67,d67,"5.13.11.65"),
               "timestamp-missing",denied)
        altered=dict(r2);altered["targets.json"]+=b"x"
        reject(lambda:observe(trust,None,altered,p67,d67,"5.13.11.65"),
               "targets-altered",denied)
        a=encode_manifest({**create_manifest("v5.13.11.67"),
                           "architecture":"win-arm64"})
        reject(lambda:observe(trust,None,release_roles(g,3,a),a,d67,"5.13.11.65"),
               "signed-wrong-architecture",denied)
        b=encode_manifest({**create_manifest("v5.13.11.67"),
                           "repository":"evil/other"})
        reject(lambda:observe(trust,None,release_roles(g,3,b),b,d67,"5.13.11.65"),
               "signed-wrong-repo",denied)
        reject(lambda:observe(trust,None,r2,p67,d67,"v5.13.11.68"),
               "signed-downgrade",denied)
        require(len(denied)==14 and len(set(denied))==14,"NEGATIVE_COUNT")
        out={"schema":"gf-github-d11-persist/1","status":"passed",
             "scope":"D0_REAL_CSHARP_FIXTURE_PLUS_PYTHON_TUF",
             "positive_cycles":2,"adversarial_rejected":14,
             "persisted_role_versions":versions2,
             "same_user_global_rollback_resistant":False,
             "cross_process_recovery_proven":False,
             "production_root":False,"network_requests":0,
             "executables_downloaded":0,"product_modified":False,
             "operational_ready":False,"automatic_recovery_authorized":False}
        print("GF_D11_RESULT "+json.dumps(out,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==2,"CLI_ARGUMENT_COUNT")
    raise SystemExit(run(sys.argv[1]))
