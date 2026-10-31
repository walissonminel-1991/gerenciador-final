"""GF D2: only INERT fixture acquisition, no real HTTP or runnable packages.

D0 C# -> D1 contract -> python-tuf metadata from existing PUBLIC TEST keys
-> memory source -> bounded temporary staging -> re-read SHA256. This experiment
is NOT a secure same-user filesystem boundary and NOT an operational updater.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import sys
import tempfile

from tuf.api import exceptions
from tuf.ngclient import Updater

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent/"github-crossprocess-lab"))
sys.path.insert(0,str(HERE.parent/"github-trust-lab"))
sys.path.insert(0,str(HERE.parent/"github-auth-lab"))

from d12_crossprocess_lab import release_fixture, inspect_release, actual_d0
from d11_persistent_lab import release_roles, FixtureFetcher, URL
from d1_release_manifest_lab import (
    ANCHOR, ARCH, ASSET, PAYLOAD, TARGET_NAME, D1Rejected,
    create_manifest, digest, encode_manifest, load_generator, parse_object,
    validate_contract, require
)

DESTINATION="inert-authorized.fixture.bin"
TEMP_NAME="inert-authorized.fixture.partial"
MAX_LAB_BYTES=131072
REPARSE=0x400
ALLOWED_URL_PREFIX="https://github.com/walissonminel-1991/gerenciador-final/releases/download/"

class InertChunkSource:
    """Fake source: source bytes and URL are inputs, never network I/O."""
    def __init__(self, url, chunks, *, redirects=()):
        self.url=url
        self.chunks=chunks
        self.redirects=redirects
        self.calls=0

    def stream(self, expected_url):
        self.calls+=1
        require(self.calls==1,"FETCH_REPLAY")
        require(type(self.url) is str and self.url==expected_url and
                self.url.startswith(ALLOWED_URL_PREFIX) and
                len(self.redirects)==0,"INERT_SOURCE_IDENTITY")
        for chunk in self.chunks:
            require(type(chunk) is bytes and 0<len(chunk)<=MAX_LAB_BYTES,
                    "UNTRUSTED_STREAM_CHUNK")
            yield chunk

def strict_dir(path, runner):
    path=Path(path)
    require(path.is_absolute() and path.parent.name.startswith("gf-d2-") and
            path.name=="stage" and path.parent.parent==runner,
            "DESTINATION_SCOPE")
    require(path.exists() and path.is_dir() and
            not path.is_symlink() and not path.parent.is_symlink(),
            "STAGE_NOT_REAL_DIR")
    for p in (path,path.parent):
        m=os.lstat(p)
        require(stat.S_ISDIR(m.st_mode) and
                not bool(getattr(m,"st_file_attributes",0)&REPARSE),
                "STAGE_REPARSE")
    require(path.resolve(strict=True)==path and
            path.parent.resolve(strict=True)==path.parent,
            "STAGE_CANONICAL")
    return path

def check_bytes(path, expected_length, expected_hash):
    h=hashlib.sha256()
    total=0
    with path.open("rb") as inp:
        while True:
            block=inp.read(8192)
            if not block:break
            total+=len(block)
            require(total<=MAX_LAB_BYTES and total<=expected_length,
                    "READBACK_OVERRUN")
            h.update(block)
    require(total==expected_length and h.hexdigest()==expected_hash,
            "READBACK_HASH_MISMATCH")

def acquire_and_stage(signed_manifest, source, stage, runner, url, *,
                      stage_name=DESTINATION, after_write=None,
                      after_readback=None):
    # This method is called ONLY after the separate D0/TUF/manifest gate
    # below has succeeded. It never opens the GitHub URL.
    data=parse_object(signed_manifest)
    require(data["test_only"] is True and
            data["operational_installable"] is False,
            "D2_TEST_ONLY")
    require(type(data["length"]) is int and
            0 < data["length"]<=MAX_LAB_BYTES and
            type(data["sha256"]) is str and
            len(data["sha256"])==64 and
            data["asset"]==ASSET, "D2_AUTHORIZED_FIELDS")
    require(type(stage_name) is str and stage_name==DESTINATION,
            "STAGE_NAME_NOT_FIXED")
    require(type(url) is str and url==(
        ALLOWED_URL_PREFIX+data["tag"]+"/"+ASSET),"D2_RELEASE_URL_SCOPE")
    stage=strict_dir(stage,runner)
    temp=stage/TEMP_NAME
    final=stage/stage_name
    require(not os.path.lexists(temp) and not os.path.lexists(final),
            "STAGE_EXCLUSIVE_CREATE")
    fd=None
    completed=False
    try:
        fd=os.open(temp,os.O_WRONLY|os.O_CREAT|os.O_EXCL|getattr(os,"O_BINARY",0),0o600)
        checksum=hashlib.sha256()
        total=0
        with os.fdopen(fd,"wb",buffering=0) as out:
            fd=None
            for block in source.stream(url):
                total+=len(block)
                require(total<=data["length"] and total<=MAX_LAB_BYTES,
                        "D2_STREAM_BUDGET")
                checksum.update(block)
                written=out.write(block)
                require(written==len(block),"D2_WRITE_SHORT")
            out.flush()
            os.fsync(out.fileno())
        require(total==data["length"] and
                checksum.hexdigest()==data["sha256"],
                "D2_DOWNLOADED_BYTES_DO_NOT_MATCH_SIGNED_MANIFEST")
        if after_write is not None:after_write(temp)
        check_bytes(temp,data["length"],data["sha256"])
        if after_readback is not None:after_readback(temp)
        # Windows os.rename will not silently overwrite an existing target.
        # No reliable same-user concurrent attacker isolation is claimed.
        require(not os.path.lexists(final),"STAGE_FINAL_PREEXISTING")
        os.rename(temp,final)
        check_bytes(final,data["length"],data["sha256"])
        completed=True
        return {"bytes_verified":total,"sha256":checksum.hexdigest(),
                "stage_name":stage_name,"staged_inert_only":True,
                "can_install":False,"operational_ready":False,
                "automatic_recovery_authorized":False}
    except (D1Rejected,OSError):
        raise
    finally:
        if fd is not None:
            os.close(fd)
        if not completed:
            for p in (temp,final):
                # Test owns a freshly allocated empty stage; don't follow links.
                try:
                    if os.path.lexists(p) and not p.is_dir():p.unlink()
                except OSError:
                    pass

def verify_d0_tuf(exe,root,manifest,roles,release,installed):
    rf=root/"release.json"
    require(not rf.exists(),"D2_RELEASE_FIXTURE_EXISTS")
    rf.write_bytes(release)
    d0,raw=actual_d0(exe,rf,installed)
    inspect_release(raw,d0,manifest)
    fetcher=FixtureFetcher(roles)
    trust=root/"tuf"
    trust.mkdir()
    u=Updater(str(trust),URL,target_dir=str(trust/"targets"),
              target_base_url=URL,fetcher=fetcher,bootstrap=ANCHOR.read_bytes())
    u.refresh()
    target=u.get_targetinfo(TARGET_NAME)
    require(target is not None and target.length==len(manifest)
            and target.hashes.get("sha256")==digest(manifest),
            "D2_TUF_MANIFEST_AUTH")
    require(d0["state"]=="NewVersionMetadataOnly" and
            type(d0["tag"]) is str,"D2_D0_NO_CANDIDATE")
    contract=validate_contract(manifest,d0["tag"],installed,ARCH,PAYLOAD)
    require(contract["contract_valid"] and not contract["can_install"] and
            not contract["operational_ready"] and
            not contract["automatic_recovery_authorized"],
            "D2_D1_AUTHORITY_INVALID")
    require([getattr(u._trusted_set,k).version for k in
             ("root","timestamp","snapshot","targets")]==[1,1,1,1],
            "D2_TUF_TRUST_CHAIN")
    return d0["tag"]

def run(exe):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),"D2_DISPOSABLE_WINDOWS")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe=Path(exe).resolve(strict=True)
    require(exe.is_relative_to(runner) and
            exe.name=="GF.D11.D0Probe.exe","D2_D0_PROGRAM")
    g=load_generator()
    require(digest(ANCHOR.read_bytes())==
            "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "D2_PUBLIC_TEST_ROOT_PIN")
    manifest=encode_manifest(create_manifest("v5.13.11.66"))
    signed=release_roles(g,1,manifest)
    release=release_fixture("v5.13.11.66")
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d2-") as d:
        root=Path(d).resolve(strict=True)
        sentinel=root/"unchanged-sentinel.txt"
        sentinel.write_bytes(b"UNCHANGED OUTSIDE STAGE\n")
        tag=verify_d0_tuf(exe,root,manifest,signed,release,"5.13.11.65")
        expected_url=ALLOWED_URL_PREFIX+tag+"/"+ASSET
        def new_stage():
            parent=Path(tempfile.mkdtemp(dir=runner,prefix="gf-d2-"))
            stage=parent/"stage"
            stage.mkdir()
            return parent,stage
        verified=[]
        def source(payload=PAYLOAD,url=expected_url,redirects=(),chunksize=9):
            return InertChunkSource(url,
                tuple(payload[i:i+chunksize]
                      for i in range(0,len(payload),chunksize)),
                redirects=redirects)
        for n in range(2):
            parent,stage=new_stage()
            try:
                result=acquire_and_stage(manifest,source(),stage,runner,expected_url)
                require(result["staged_inert_only"] and
                        result["bytes_verified"]==len(PAYLOAD) and
                        result["sha256"]==digest(PAYLOAD) and
                        not result["can_install"] and
                        not result["operational_ready"] and
                        not result["automatic_recovery_authorized"],
                        "D2_POSITIVE_INERT")
                check_bytes(stage/DESTINATION,len(PAYLOAD),digest(PAYLOAD))
                verified.append(n)
            finally:
                shutil.rmtree(parent)
        require(verified==[0,1],"D2_POSITIVE_CASES")
        rejected=[]

        def deny(name,*,payload=PAYLOAD,url=expected_url,redirects=(),
                 chunksize=9,stage_name=DESTINATION,hook_write=None,
                 hook_read=None,precreate=None,stage_override=None):
            parent,stage=new_stage()
            try:
                if precreate is not None:
                    (stage/precreate).write_bytes(b"OUTSIDE CONTENT MUST NOT BE OVERWRITTEN")
                try:
                    acquire_and_stage(manifest,source(payload,url,redirects,chunksize),
                                      stage_override or stage,runner,expected_url,
                                      stage_name=stage_name,after_write=hook_write,
                                      after_readback=hook_read)
                except (D1Rejected,OSError,ValueError):
                    require(not (stage/DESTINATION).is_file() or
                            precreate==DESTINATION,"D2_FALSE_STAGED_"+name)
                    if precreate==DESTINATION:
                        require((stage/DESTINATION).read_bytes()==
                                b"OUTSIDE CONTENT MUST NOT BE OVERWRITTEN",
                                "D2_PREEXISTING_FILE_CHANGED")
                    rejected.append(name)
                    return
                raise AssertionError("D2_FALSE_ACCEPT_"+name)
            finally:
                shutil.rmtree(parent)

        deny("tampered-source",payload=PAYLOAD+b"x")
        deny("truncated-source",payload=PAYLOAD[:-1])
        deny("same-size-different-content",
            payload=b"Z"+PAYLOAD[1:])
        deny("source-extra-prefix",payload=b"x"+PAYLOAD)
        deny("empty-source",payload=b"")
        deny("source-wrong-url",url="https://evil.invalid/a.zip")
        deny("source-wrong-tag",url=ALLOWED_URL_PREFIX+"v5.13.11.67/"+ASSET)
        deny("source-redirect",redirects=("https://evil.invalid/a",))
        deny("path-traversal",stage_name="../../outside.bin")
        deny("different-filename",stage_name="untrusted.bin")
        deny("preexisting-final",precreate=DESTINATION)
        deny("preexisting-temporary",precreate=TEMP_NAME)
        def corrupt_after_write(p):
            p.write_bytes(b"X"*len(PAYLOAD))
        deny("tamper-after-write",hook_write=corrupt_after_write)
        deny("tamper-after-readback",hook_read=corrupt_after_write)
        # A source with huge chunk must be refused before write budget.
        deny("oversized-source",payload=b"B"*(MAX_LAB_BYTES+1),
            chunksize=MAX_LAB_BYTES+1)
        deny("outside-stage-rejected",stage_override=root)
        # With successful TUF metadata, a fake newer Release cannot change
        # the signed manifest association; C# D0 is consulted again.
        def deny_binding(name,release_bytes,manifest_bytes=manifest,roles=signed):
            target=root/("binding-"+name)
            target.mkdir()
            try:
                verify_d0_tuf(exe,target,manifest_bytes,roles,
                              release_bytes,"5.13.11.65")
            except (D1Rejected,exceptions.RepositoryError,
                    exceptions.DownloadError,OSError,ValueError):
                rejected.append(name)
                return
            raise AssertionError("D2_FALSE_RELEASE_ACCEPT_"+name)
        deny_binding("release-tag-mismatch",release_fixture("v5.13.11.67"))
        deny_binding("release-asset-name",
            release_fixture("v5.13.11.66",assets=[{
                **json.loads(release)["assets"][0],"name":"another.zip"}]))
        deny_binding("release-asset-size",
            release_fixture("v5.13.11.66",assets=[{
                **json.loads(release)["assets"][0],"size":999}]))
        deny_binding("release-host-spoof",
            release_fixture("v5.13.11.66",assets=[{
                **json.loads(release)["assets"][0],
                "browser_download_url":"https://evil.invalid/a.zip"}]))
        deny_binding("manifest-content-mismatch",
                     release,manifest+b" ",signed)

        require(len(rejected)==21 and len(set(rejected))==21,"D2_NEGATIVE_MATRIX")
        require(sentinel.read_bytes()==b"UNCHANGED OUTSIDE STAGE\n",
                "D2_SENTINEL_DAMAGED")
        report={
          "schema":"gf-github-d2-inert-acquisition/1",
          "status":"passed",
          "positive_inert_acquisitions":len(verified),
          "adversarial_rejected":len(rejected),
          "persistent_tuf_lab_used":True,
          "d0_real_csharp_parser_used":True,
          "manifest_authenticated_with_public_test_key":True,
          "memory_only_source":True,
          "readback_sha_verified":True,
          "outside_stage_sentinel_preserved":True,
          "same_user_toctou_resistance_proven":False,
          "production_signing_authority":False,
          "full_history_rollback_resistance_proven":False,
          "real_network_requests":0,
          "executable_downloads":0,
          "executable_launches":0,
          "installations":0,
          "product_modified":False,
          "operational_ready":False,
          "automatic_recovery_authorized":False
        }
        print("GF_D2_RESULT "+json.dumps(report,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==2,"D2_CLI")
    raise SystemExit(run(sys.argv[1]))
