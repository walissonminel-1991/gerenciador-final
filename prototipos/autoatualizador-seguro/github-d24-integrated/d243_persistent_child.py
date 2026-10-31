"""D2.4.3 trust-history observer, embedded in the existing C# lab coordinator.

Reuses d11_persistent_lab.observe / python-tuf and the original C# D0.
No real download, staging, launch, installation, secrets or recovery.
A same-user rewind of the entire trusted directory is intentionally not solved.
"""
import json
import os
from pathlib import Path
import sys
from hashlib import sha256

def stop(code):
    print("GF_D243_REJECT "+code)
    raise SystemExit(17)

def read_bounded(path, maximum):
    if path.is_symlink() or not path.is_file() or not 0 < path.stat().st_size <= maximum:
        stop("INPUT_UNKNOWN_"+path.name)
    return path.read_bytes()

def run():
    if sys.platform!="win32" or os.environ.get("GITHUB_ACTIONS")!="true":
        stop("NOT_WINDOWS_LAB")
    if len(sys.argv)!=6 or sys.argv[1]!="--d243-child":
        stop("CHILD_ARGUMENTS")
    fixture=Path(sys.argv[2]).resolve(strict=True)
    session=sys.argv[3]
    d0=Path(sys.argv[4]).resolve(strict=True)
    trusted=Path(sys.argv[5]).resolve(strict=True)
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    if not all(x.is_relative_to(runner) for x in (fixture,d0,trusted)):
        stop("OUTSIDE_RUNNER")
    if any(x.is_symlink() for x in (fixture,trusted)) or not fixture.is_dir():
        stop("LINKED_INPUT")
    if d0.name!="GF.D11.D0Probe.exe" or not d0.is_file():
        stop("D0_UNAVAILABLE")
    if len(session)!=32 or any(c not in "0123456789abcdef" for c in session):
        stop("INVALID_SESSION")
    workspace=Path(os.environ["GITHUB_WORKSPACE"]).resolve(strict=True)
    root=workspace/"prototipos"/"autoatualizador-seguro"
    for folder in ("github-trust-lab","github-auth-lab"):
        sys.path.insert(0,str(root/folder))
    from d11_persistent_lab import observe,discover
    from d1_release_manifest_lab import (ANCHOR,PINNED_ROOT_SHA,digest,
        TARGET_NAME,D1Rejected)
    from tuf.api import exceptions

    bootstrap=read_bounded(ANCHOR,65536)
    if digest(bootstrap)!=PINNED_ROOT_SHA:
        stop("TEST_ROOT_PIN")
    manifest=read_bounded(fixture/TARGET_NAME,65536)
    tag=json.loads(manifest)["tag"]
    if tag not in ("v5.13.11.66","v5.13.11.67"):
        stop("UNEXPECTED_TEST_TAG")
    installed="5.13.11.65" if tag.endswith("66") else "5.13.11.66"
    roles={name:read_bounded(fixture/name,524288)
           for name in ("timestamp.json","snapshot.json","targets.json")}
    if not trusted.is_dir() or trusted.is_symlink():
        stop("TRUST_DIRECTORY_NOT_OWNED")
    existing=[(trusted/name).is_file() for name in
              ("root.json","timestamp.json","snapshot.json","targets.json")]
    if any(existing) and not all(existing):
        stop("PARTIAL_TRUST_HISTORY")
    if any(existing):
        for name in ("root.json","timestamp.json","snapshot.json","targets.json"):
            _=read_bounded(trusted/name,524288)
    # d11 observe -> original C# D0 parser, original python-tuf Updater,
    # strict D1 test manifest. No second TUF client.
    discovery=discover(d0,fixture,tag,old=installed)
    try:
        versions=observe(trusted,None if all(existing) else bootstrap,
                         roles,manifest,discovery,installed)
    except (D1Rejected,exceptions.RepositoryError,exceptions.DownloadError,
            OSError,ValueError):
        stop("TUF_PERSISTENT_REJECTED")
    if versions[0]!=1 or not (versions[1]==versions[2]==versions[3]):
        stop("TRUST_ROLE_INCONSISTENT")
    print("GF_D243_VERIFIED "+json.dumps({
        "schema":"gf-d243-persistent-tuf-child/1",
        "session":session,"manifest_sha256":sha256(manifest).hexdigest(),
        "tag":tag,"roles":versions,
        "tuf_observed_test_only":True,"production_authority":False,
        "can_install":False,"operational_ready":False,
        "automatic_recovery_authorized":False
    },sort_keys=True))

if __name__=="__main__":
    try:
        run()
    except Exception as error:
        stop("UNHANDLED_"+type(error).__name__)
