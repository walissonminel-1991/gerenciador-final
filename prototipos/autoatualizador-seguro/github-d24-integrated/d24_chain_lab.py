"""D2.4 orchestrator: actual D0 C# parser -> Python-TUF -> shared .NET D2.3.

All signatures are made by PUBLIC test-only keys already in this repo.
The interprocess handoff is NOT secure from same-user tampering. In particular
the standalone C# helper can be invoked with public known bytes and SHA without
calling TUF; the matrix deliberately demonstrates this counterexample.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

from tuf.api import exceptions

HERE=Path(__file__).resolve().parent
for folder in ("github-d2-inert","github-trust-lab","github-crossprocess-lab",
               "github-auth-lab"):
    sys.path.insert(0,str(HERE.parent/folder))
from d2_inert_lab import verify_d0_tuf
from d11_persistent_lab import release_roles
from d12_crossprocess_lab import release_fixture
from d1_release_manifest_lab import (
    ANCHOR, ASSET, ARCH, PAYLOAD, D1Rejected, create_manifest, digest,
    encode_manifest, load_generator, require
)

NAME="GF-RELEASE-MANIFEST.json"
TAG="v5.13.11.66"
OLD="5.13.11.65"

def run_bridge(exe, root, data, approved_hash, tag=TAG, installed=OLD, label="x"):
    child=root/("bridge-"+label)
    child.mkdir()
    manifest=child/NAME
    manifest.write_bytes(data)
    process=subprocess.run([str(exe),"--d24",str(manifest),approved_hash,tag,installed],
        check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
        stdin=subprocess.DEVNULL,text=True,timeout=20)
    lines=[s for s in process.stdout.splitlines()
           if s.startswith("GF_D24_BRIDGE ")]
    require(len(lines)==1,"BRIDGE_RESULT_COUNT_"+label)
    result=json.loads(lines[0][len("GF_D24_BRIDGE "):])
    require(set(result)=={"schema","authority","status","reason",
             "loopback_connections","staged_inert_bytes","production_authority",
             "handoff_secure_against_same_user","can_install",
             "operational_ready","automatic_recovery_authorized"} and
            result["schema"]=="gf-d24-dotnet-handoff/1" and
            result["authority"]=="PUBLIC_TEST_ONLY" and
            result["production_authority"] is False and
            result["handoff_secure_against_same_user"] is False and
            result["can_install"] is False and
            result["operational_ready"] is False and
            result["automatic_recovery_authorized"] is False,
            "UNSAFE_BRIDGE_REPORT_"+label)
    return result

def main(d0_exe,d24_exe):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),"D24_WINDOWS_RUNNER_ONLY")
    tmp=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    d0_exe=Path(d0_exe).resolve(strict=True)
    d24_exe=Path(d24_exe).resolve(strict=True)
    require(all(x.is_relative_to(tmp) for x in (d0_exe,d24_exe)) and
            d0_exe.name=="GF.D11.D0Probe.exe" and
            d24_exe.name=="GF.D24.InertChainLab.exe",
            "D24_EXECUTABLE_SCOPE")
    require(digest(ANCHOR.read_bytes())==
            "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "D24_PUBLIC_TEST_ROOT_PIN")
    sign=load_generator()
    original=encode_manifest(create_manifest(TAG))
    roles=release_roles(sign,1,original)
    published=release_fixture(TAG)
    preflight_denials=[]
    bridge_denials=[]
    with tempfile.TemporaryDirectory(dir=tmp,prefix="gf-d24-") as directory:
        root=Path(directory)
        def preflight(label,raw=original,chain=roles,release=published,installed=OLD,
                      expect_success=False):
            location=root/("verify-"+label)
            location.mkdir()
            try:
                value=verify_d0_tuf(d0_exe,location,raw,chain,release,installed)
                require(value==TAG,"D0_D1_TUF_TAG_MISMATCH")
            except (D1Rejected,exceptions.RepositoryError,
                    exceptions.DownloadError,OSError,ValueError):
                require(not expect_success,"TRUE_FIXTURE_DENIED_"+label)
                preflight_denials.append(label)
                return False
            require(expect_success,"FALSE_AUTH_ACCEPT_"+label)
            return True

        # The bridge is only called after a valid Python-TUF + D0 gate.
        require(preflight("main",expect_success=True),
                "PUBLIC_TEST_TUF_NOT_AUTHENTICATED")
        positive=run_bridge(d24_exe,root,original,digest(original),
                            label="authenticated")
        require(positive["status"]=="accepted_inert_test_only" and
                positive["staged_inert_bytes"] is True and
                positive["loopback_connections"]==1,
                "D24_AUTHENTICATED_INERT_TRANSFER_FAILED")

        # Negative matrix for the TUF/D0 stage: must NOT call .NET.
        preflight("wrong-release-tag",release=release_fixture("v5.13.11.67"))
        preflight("draft-release",release=release_fixture(TAG,draft=True))
        base_asset=json.loads(published)["assets"][0]
        preflight("asset-name",release=release_fixture(TAG,assets=[
            {**base_asset,"name":"evil.zip"}]))
        preflight("asset-size",release=release_fixture(TAG,assets=[
            {**base_asset,"size":99999}]))
        preflight("asset-external-url",release=release_fixture(TAG,assets=[
            {**base_asset,"browser_download_url":"https://evil.invalid/asset"}]))
        preflight("manifest-bytes-modified",raw=original+b" ")
        altered=dict(roles)
        altered["targets.json"]=roles["targets.json"]+b"X"
        preflight("targets-corrupted",chain=altered)
        preflight("timestamp-expired",
                  chain=release_roles(sign,1,original,expired=True))
        preflight("bad-platform-signed",raw=encode_manifest({
            **create_manifest(TAG),"architecture":"win-arm64"}),
            chain=release_roles(sign,1,encode_manifest({
                **create_manifest(TAG),"architecture":"win-arm64"})))
        preflight("wrong-repository-signed",raw=encode_manifest({
            **create_manifest(TAG),"repository":"evil/elsewhere"}),
            chain=release_roles(sign,1,encode_manifest({
                **create_manifest(TAG),"repository":"evil/elsewhere"})))
        preflight("no-upgrade",installed=TAG)
        without_time=dict(roles);without_time.pop("timestamp.json")
        preflight("timestamp-absent",chain=without_time)

        def reject_bridge(label,manifest,approved_hash=None,tag=TAG,installed=OLD):
            result=run_bridge(d24_exe,root,manifest,
                              approved_hash if approved_hash is not None else digest(manifest),
                              tag,installed,label)
            require(result["status"]=="rejected" and
                    result["staged_inert_bytes"] is False and
                    result["loopback_connections"]==0,
                    "D24_BRIDGE_FALSE_ACCEPT_"+label)
            bridge_denials.append(label)

        reject_bridge("handoff-sha-pin-wrong",original,approved_hash="0"*64)
        reject_bridge("manifest-modified-after-tuf",original+b" ",
                      approved_hash=digest(original))
        reject_bridge("tag-mismatch",original,tag="v5.13.11.67")
        reject_bridge("installed-version-mismatch",original,installed=TAG)
        reject_bridge("manifest-wrong-architecture",encode_manifest({
            **create_manifest(TAG),"architecture":"win-arm64"}))
        reject_bridge("manifest-wrong-repository",encode_manifest({
            **create_manifest(TAG),"repository":"evil/elsewhere"}))
        reject_bridge("manifest-wrong-asset",encode_manifest({
            **create_manifest(TAG),"asset":"../../evil.exe"}))
        reject_bridge("manifest-wrong-sha",encode_manifest({
            **create_manifest(TAG),"sha256":"0"*64}))
        reject_bridge("manifest-wrong-length",encode_manifest({
            **create_manifest(TAG),"length":len(PAYLOAD)+1}))
        reject_bridge("manifest-test-flag-disabled",encode_manifest({
            **create_manifest(TAG),"test_only":False}))
        reject_bridge("manifest-install-flag-enabled",encode_manifest({
            **create_manifest(TAG),"operational_installable":True}))
        reject_bridge("manifest-extra-field",encode_manifest({
            **create_manifest(TAG),"download_url":"https://evil.invalid/a"}))
        reject_bridge("manifest-missing-field",encode_manifest({
            k:v for k,v in create_manifest(TAG).items() if k!="sha256"}))
        dup=original.replace(b'"sha256":',b'"sha256":"0","sha256":',1)
        reject_bridge("manifest-duplicate-field",dup)

        # Explicit counterexample: A same-user caller can directly present
        # the publicly known lab manifest + its hash to .NET, skipping Python-TUF.
        bypass=run_bridge(d24_exe,root,original,digest(original),label="bypass-tuf")
        require(bypass["status"]=="accepted_inert_test_only" and
                bypass["staged_inert_bytes"] is True and
                bypass["loopback_connections"]==1,
                "D24_HANDOFF_BYPASS_NOT_DEMONSTRATED")
        require(len(preflight_denials)==12 and
                len(bridge_denials)==14 and
                len(set(preflight_denials))==12 and
                len(set(bridge_denials))==14,"D24_CASE_MATRIX")
        report={
            "schema":"gf-github-d24-inert-integrated/1",
            "status":"passed",
            "orchestrated_d0_tuf_dotnet_inert_staging":True,
            "authenticated_inert_preflight_positive":1,
            "tuf_d0_negative_cases_rejected":len(preflight_denials),
            "dotnet_handoff_negative_cases_rejected":len(bridge_denials),
            "shared_dotnet_d23_transport_source":True,
            "standalone_handoff_without_tuf_accepted":True,
            "standalone_bypass_is_security_counterexample":True,
            "secure_cross_process_authority_proven":False,
            "same_user_rollback_protection_proven":False,
            "same_user_toctou_resistance_proven":False,
            "public_test_signer_only":True,
            "real_network_requests":0,
            "real_executable_downloads":0,
            "installations":0,
            "product_modified":False,
            "operational_ready":False,
            "automatic_recovery_authorized":False
        }
        print("GF_D24_RESULT "+json.dumps(report,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==3,"D24_CLI")
    raise SystemExit(main(sys.argv[1],sys.argv[2]))
