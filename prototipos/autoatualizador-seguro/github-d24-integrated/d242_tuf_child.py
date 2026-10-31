"""D2.4.2 TEST-ONLY validator launched by its parent C# coordinator.

Supplied by C# as an embedded source resource via Python -c. It is NOT an
independent install authorizer and does not grant same-user process isolation.
"""
import json
import os
from pathlib import Path
import sys
from hashlib import sha256

def stop(reason):
    print("GF_D242_REJECT "+reason)
    raise SystemExit(17)

def exact_read(path,maximum):
    if path.is_symlink() or not path.is_file() or not 0 < path.stat().st_size <= maximum:
        stop("INVALID_INPUT")
    return path.read_bytes()

def run():
    if sys.platform!="win32" or os.environ.get("GITHUB_ACTIONS")!="true":
        stop("NOT_WINDOWS_LAB")
    if len(sys.argv)!=5 or sys.argv[1]!="--d242-child":
        stop("CHILD_ARGUMENTS")
    directory=Path(sys.argv[2]).resolve(strict=True)
    session=sys.argv[3]
    d0=Path(sys.argv[4]).resolve(strict=True)
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    if not directory.is_relative_to(runner) or not d0.is_relative_to(runner):
        stop("OUT_OF_RUNNER")
    if not directory.is_dir() or directory.is_symlink():
        stop("INVALID_SESSION_DIR")
    if d0.name!="GF.D11.D0Probe.exe" or not d0.is_file():
        stop("NO_D0_PARSER")
    if len(session)!=32 or any(x not in "0123456789abcdef" for x in session):
        stop("INVALID_SESSION")
    workspace=Path(os.environ["GITHUB_WORKSPACE"]).resolve(strict=True)
    root=workspace/"prototipos"/"autoatualizador-seguro"
    for folder in ("github-d2-inert","github-crossprocess-lab",
                   "github-trust-lab","github-auth-lab"):
        sys.path.insert(0,str(root/folder))
    from d2_inert_lab import verify_d0_tuf
    from d1_release_manifest_lab import ANCHOR, PINNED_ROOT_SHA, digest, require
    from d1_release_manifest_lab import D1Rejected
    from d12_crossprocess_lab import release_fixture
    from tuf.api import exceptions

    # Single bounded bootstrap read; the EXACT snapshot checked against the
    # pin is the argument passed into python-tuf. No second path read.
    bootstrap=exact_read(ANCHOR,65536)
    if sha256(bootstrap).hexdigest()!=PINNED_ROOT_SHA:
        stop("BOOTSTRAP_PIN")
    manifest=exact_read(directory/"GF-RELEASE-MANIFEST.json",65536)
    release=exact_read(directory/"release.json",524288)
    roles={name:exact_read(directory/name,524288)
           for name in ("timestamp.json","snapshot.json","targets.json")}
    verify=directory/"verified"
    if verify.exists():
        stop("REPLAYED_SESSION_DIRECTORY")
    verify.mkdir()
    try:
        tag=verify_d0_tuf(d0,verify,manifest,roles,release,
                          "5.13.11.65",bootstrap_bytes=bootstrap)
        require(tag=="v5.13.11.66","EXPECTED_TAG")
    except (D1Rejected,exceptions.RepositoryError,
            exceptions.DownloadError,OSError,ValueError):
        stop("TUF_D0_FAIL_CLOSED")
    report={
        "schema":"gf-d242-tuf-child/1",
        "session":session,
        "manifest_sha256":digest(manifest),
        "tag":tag,
        "installed":"5.13.11.65",
        "tuf_verified_test_only":True,
        "production_authority":False,
        "can_install":False,
        "operational_ready":False,
        "automatic_recovery_authorized":False
    }
    print("GF_D242_VERIFIED "+json.dumps(report,sort_keys=True))

if __name__=="__main__":
    try:
        run()
    except Exception as error:
        stop("UNHANDLED_"+type(error).__name__)
