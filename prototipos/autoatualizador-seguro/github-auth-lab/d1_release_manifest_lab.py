"""D1: test-only GitHub release binding through existing TUF lab trust root.

A separately signed TUF targets entry authenticates the manifest bytes. The
manifest binds repository, channel, release tag, architecture, one fixed asset
name, byte count and SHA256. The specimen asset is INERT, not an executable.
This code and the PUBLIC TEST signer must NEVER be used in production.
"""
from __future__ import annotations

import copy
from datetime import datetime, timedelta, timezone
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import re
import sys

from securesystemslib.signer import CryptoSigner, SSlibKey
from tuf.api.metadata import Metadata

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
ANCHOR = ROOT / "fixtures" / "bootstrap.test-only.json"
GENERATOR = ROOT / "generate_fixtures.py"
PINNED_ROOT_SHA = "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e"
TARGET_NAME = "GF-RELEASE-MANIFEST.json"
REPOSITORY = "walissonminel-1991/gerenciador-final"
ASSET = "GerenciadorFinal-portable-win-x64.zip"
ARCH = "win-x64"
SHA_RE = re.compile(r"[0-9a-f]{64}\Z", re.ASCII)
TAG_RE = re.compile(r"v?(?:0|[1-9][0-9]{0,4})(?:\.(?:0|[1-9][0-9]{0,4})){2,3}\Z", re.ASCII)
PAYLOAD = b"GF-D1-INERT-PAYLOAD-NOT-A-ZIP-OR-EXECUTABLE\n"

class D1Rejected(Exception):
    pass

def require(value, reason):
    if not value:
        raise D1Rejected(reason)

def digest(value: bytes):
    return sha256(value).hexdigest()

def load_generator():
    spec = importlib.util.spec_from_file_location("gf_public_lab_signer_only", GENERATOR)
    require(spec is not None and spec.loader is not None, "PUBLIC_TEST_GENERATOR_MISSING")
    g = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(g)
    return g

def strict_pairs(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "DUPLICATE_JSON_KEY")
        result[key] = value
    return result

def parse_object(raw: bytes):
    require(isinstance(raw, bytes) and 0 < len(raw) <= 65536, "MANIFEST_LENGTH")
    try:
        data = json.loads(raw.decode("utf-8", errors="strict"),
                          object_pairs_hook=strict_pairs)
    except (ValueError, UnicodeError) as exc:
        raise D1Rejected("JSON_INVALID") from exc
    require(type(data) is dict, "OBJECT_REQUIRED")
    return data

def version_value(value):
    require(type(value) is str and 5 <= len(value) <= 24 and
            TAG_RE.fullmatch(value) is not None, "VERSION_FORMAT")
    parts = [int(s) for s in value.lstrip("v").split(".")]
    require(all(0 <= i <= 65535 for i in parts), "VERSION_RANGE")
    return tuple(parts) + (0,) * (4 - len(parts))

def create_manifest(tag: str, payload=PAYLOAD):
    ver = version_value(tag)
    return {
        "schema": "gf-github-release-auth-lab/1",
        "repository": REPOSITORY,
        "channel": "stable",
        "tag": tag,
        "version": ".".join(str(x) for x in ver),
        "architecture": ARCH,
        "asset": ASSET,
        "length": len(payload),
        "sha256": digest(payload),
        "test_only": True,
        "operational_installable": False
    }

def encode_manifest(data):
    return (json.dumps(data, sort_keys=True, separators=(",", ":")) + "\n").encode()

def validate_contract(raw: bytes, discovered_tag: str, installed_tag: str,
                      expected_arch: str, inert_payload: bytes):
    data = parse_object(raw)
    require(set(data) == {"schema","repository","channel","tag","version",
                         "architecture","asset","length","sha256",
                         "test_only","operational_installable"}, "MANIFEST_EXACT_KEYS")
    require(data["schema"] == "gf-github-release-auth-lab/1" and
            type(data["schema"]) is str, "MANIFEST_SCHEMA")
    require(type(data["repository"]) is str and data["repository"] == REPOSITORY,
            "REPOSITORY_IDENTITY")
    require(type(data["channel"]) is str and data["channel"] == "stable",
            "STABLE_CHANNEL_ONLY")
    tag = data["tag"]
    require(type(tag) is str and tag == discovered_tag, "DISCOVERY_TAG_MISMATCH")
    v, current = version_value(tag), version_value(installed_tag)
    require(v > current, "NO_UPGRADE_OR_DOWNGRADE")
    require(type(data["version"]) is str and
            data["version"] == ".".join(str(x) for x in v), "VERSION_BINDING")
    require(type(data["architecture"]) is str and
            expected_arch == ARCH and data["architecture"] == expected_arch,
            "ARCH_BINDING")
    require(type(data["asset"]) is str and data["asset"] == ASSET, "ASSET_NAME")
    require(type(data["length"]) is int and
            0 < data["length"] <= 100 * 1024 * 1024 and
            len(inert_payload) == data["length"], "ASSET_SIZE")
    require(type(data["sha256"]) is str and SHA_RE.fullmatch(data["sha256"])
            and digest(inert_payload) == data["sha256"], "ASSET_SHA256")
    require(data["test_only"] is True and
            data["operational_installable"] is False, "TEST_ONLY")
    return {
        "contract_valid": True, "tuf_verified": False,
        "candidate_identified": True, "can_install": False,
        "operational_ready": False, "automatic_recovery_authorized": False
    }

def sign(role_data, role, g):
    # Reuses exactly the public deterministic Stage11 Ed25519 test keys and
    # python-tuf Metadata signing, not an invented crypto protocol.
    md = Metadata.from_bytes(g.wire(g.envelope(role_data, [role])))
    md.sign(CryptoSigner(g.PRIVATE[role],
             SSlibKey.from_dict(g.IDS[role], copy.deepcopy(g.KEYS[role]))))
    return md.to_bytes()

def make_chain(manifest: bytes, g, *, now=None):
    now = (now or datetime.now(timezone.utc)).replace(microsecond=0)
    base = {"spec_version": "1.0.36", "version": 1,
            "expires": g.utc(now + timedelta(minutes=15))}
    targets = dict(base, _type="targets", targets={
        TARGET_NAME: {
            "length": len(manifest), "hashes": {"sha256": digest(manifest)},
            "custom": {"gf_d1": {"schema": "gf-github-d1-target/1",
                                  "test_only": True,
                                  "operational_installable": False}}
        }
    })
    t = sign(targets, "targets", g)
    snapshot = dict(base, _type="snapshot", meta={
        "targets.json": {"version": 1, "length": len(t),
                         "hashes": {"sha256": digest(t)}}
    })
    s = sign(snapshot, "snapshot", g)
    timestamp = dict(base, _type="timestamp", meta={
        "snapshot.json": {"version": 1, "length": len(s),
                          "hashes": {"sha256": digest(s)}}
    })
    return {"targets": t, "snapshot": s, "timestamp": sign(timestamp, "timestamp", g)}

def trusted_chain(anchor: bytes, chain: dict, manifest: bytes, *, now=None):
    now = now or datetime.now(timezone.utc)
    require(digest(anchor) == PINNED_ROOT_SHA, "ROOT_PIN_MISMATCH")
    require(set(chain) == {"timestamp","snapshot","targets"}, "ROLE_SET")
    try:
        root = Metadata.from_bytes(anchor)
        root.verify_delegate("root", root)
        require(root.signed.expires > now, "ROOT_EXPIRED")
        roles = {}
        for role in ("timestamp","snapshot","targets"):
            raw = chain[role]
            require(type(raw) is bytes and 0 < len(raw) <= 524288, "ROLE_SIZE")
            md = Metadata.from_bytes(raw)
            root.verify_delegate(role, md)
            require(md.signed.expires > now and md.signed.version == 1,
                    "ROLE_EXPIRY_VERSION_" + role)
            roles[role] = md
        stamp = roles["timestamp"].signed.snapshot_meta
        snap = roles["snapshot"].signed.meta["targets.json"]
        require(stamp.version == 1 and stamp.length == len(chain["snapshot"]) and
                stamp.hashes["sha256"] == digest(chain["snapshot"]),
                "TIMESTAMP_BINDING")
        require(snap.version == 1 and snap.length == len(chain["targets"]) and
                snap.hashes["sha256"] == digest(chain["targets"]), "SNAPSHOT_BINDING")
        tg = roles["targets"].signed.targets
        require(set(tg) == {TARGET_NAME}, "TARGET_SET")
        info = tg[TARGET_NAME]
        require(type(manifest) is bytes and len(manifest) <= 65536 and
                info.length == len(manifest) and
                info.hashes["sha256"] == digest(manifest), "SIGNED_MANIFEST_BYTES")
        require(info.custom["gf_d1"] == {
            "schema": "gf-github-d1-target/1",
            "test_only": True, "operational_installable": False
        }, "TUF_TARGET_SCOPE")
    except D1Rejected:
        raise
    except Exception as exc:
        # TUF parser/signature errors fail closed. Do not reinterpret a
        # broken metadata chain as unavailability/no-update.
        raise D1Rejected("TUF_CHAIN_INVALID") from exc

def check_release(anchor, chain, manifest, discovered_tag, installed_tag,
                  arch, payload):
    # No parse/claim based on the manifest until the TUF chain pins its bytes.
    trusted_chain(anchor, chain, manifest)
    result = validate_contract(manifest, discovered_tag, installed_tag, arch, payload)
    result["tuf_verified"] = True
    result["production_authority"] = False
    return result

def run():
    require(sys.platform == "win32" and os.environ.get("GITHUB_ACTIONS") == "true",
            "DISPOSABLE_WINDOWS_ONLY")
    g = load_generator()
    anchor = ANCHOR.read_bytes()
    tag, old = "v5.13.11.66", "v5.13.11.65"
    manifest = encode_manifest(create_manifest(tag))
    good_chain = make_chain(manifest, g)
    original = check_release(anchor, good_chain, manifest, tag, old, ARCH, PAYLOAD)
    require(original["tuf_verified"] and not original["can_install"] and
            not original["operational_ready"] and
            not original["automatic_recovery_authorized"] and
            not original["production_authority"], "POSITIVE_LAB_CONTRACT")

    cases = []
    def reject(name, **changes):
        args = {"anchor": anchor, "chain": good_chain, "manifest": manifest,
                "discovered_tag": tag, "installed_tag": old, "arch": ARCH,
                "payload": PAYLOAD}
        args.update(changes)
        try:
            check_release(**args)
        except D1Rejected:
            cases.append(name)
            return
        raise AssertionError("FALSE_ACCEPT_" + name)

    reject("tampered-manifest-bytes", manifest=manifest.replace(b"win-x64", b"win-x86"))
    reject("different-root", anchor=anchor + b"\n")
    bad_targets = dict(good_chain)
    bad_targets["targets"] = good_chain["targets"].replace(b"GF-RELEASE", b"XG-RELEASE")
    reject("tampered-signed-targets", chain=bad_targets)
    bad_timestamp = dict(good_chain)
    bad_timestamp["timestamp"] = good_chain["timestamp"].replace(b"snapshot", b"snapshotX", 1)
    reject("tampered-timestamp", chain=bad_timestamp)
    bad_snapshot = dict(good_chain)
    bad_snapshot["snapshot"] = good_chain["snapshot"] + b" "
    reject("snapshot-length-digest", chain=bad_snapshot)
    reject("absent-role", chain={"timestamp":good_chain["timestamp"],
                                "targets":good_chain["targets"]})
    reject("different-discovery-tag", discovered_tag="v5.13.11.67")
    reject("same-installed-version", installed_tag=tag)
    reject("downgrade", installed_tag="v5.13.11.67")
    reject("wrong-platform", arch="win-arm64")
    reject("modified-asset-bytes", payload=PAYLOAD+b"X")
    reject("truncated-asset-bytes", payload=PAYLOAD[:-1])

    # Adversarial fields are re-signed with PUBLIC TEST keys; this proves
    # that signature validity alone cannot override the strict contract.
    changes = [
        ("wrong-repository", {"repository":"another/owner"}),
        ("wrong-channel", {"channel":"beta"}),
        ("wrong-tag", {"tag":"v5.13.11.67"}),
        ("wrong-version", {"version":"5.13.11.99"}),
        ("wrong-rid", {"architecture":"win-arm64"}),
        ("asset-path-traversal", {"asset":"../GerenciadorFinal-portable-win-x64.zip"}),
        ("asset-foreign-url", {"asset":"https://evil.invalid/a.zip"}),
        ("asset-too-large", {"length":2**42}),
        ("asset-size-bool", {"length":True}),
        ("asset-sha-mismatch", {"sha256":"0"*64}),
        ("asset-sha-uppercase", {"sha256":digest(PAYLOAD).upper()}),
        ("test-flag-false", {"test_only":False}),
        ("operational-flag-true", {"operational_installable":True}),
        ("schema-change", {"schema":"gf-github-release-auth-lab/2"}),
        ("missing-sha", {"sha256":None}),
        ("leading-zero-tag", {"tag":"v5.13.11.066"}),
        ("prerelease-tag", {"tag":"v5.13.11.67-beta"}),
        ("extra-field", {"download_url":"https://evil.invalid/x"}),
    ]
    for name, updates in changes:
        datum = create_manifest(tag)
        datum.update(updates)
        signed = encode_manifest(datum)
        reject(name, manifest=signed, chain=make_chain(signed, g))

    dup = manifest.replace(b'"sha256":', b'"sha256":"0", "sha256":')
    reject("duplicate-json-key", manifest=dup, chain=make_chain(dup, g))

    # A genuinely expired timestamp signed by the same LAB key must be refused.
    expired = dict(good_chain)
    ts = {"spec_version":"1.0.36","version":1,
          "expires":"2020-01-01T00:00:00Z", "_type":"timestamp",
          "meta":{"snapshot.json":{"version":1,"length":len(good_chain["snapshot"]),
                                  "hashes":{"sha256":digest(good_chain["snapshot"])}}}}
    expired["timestamp"] = sign(ts, "timestamp", g)
    reject("expired-timestamp-even-valid-signature", chain=expired)

    # A signed target using intruder key instead of authorized targets key fails.
    intruder_data = {
        "spec_version":"1.0.36","version":1,
        "expires":g.utc(datetime.now(timezone.utc)+timedelta(minutes=15)),
        "_type":"targets","targets":{TARGET_NAME:{
            "length":len(manifest),"hashes":{"sha256":digest(manifest)},
            "custom":{"gf_d1":{"schema":"gf-github-d1-target/1",
                               "test_only":True,"operational_installable":False}}}}}
    intruder = dict(good_chain)
    intruder["targets"] = sign(intruder_data, "intruder", g)
    # Must preserve timestamp/snapshot references so intruder signature is
    # rejected even before hash linkage; either outcome is a hard deny.
    reject("untrusted-test-signer", chain=intruder)

    result={"schema":"gf-github-auth-d1-lab/1","status":"passed",
            "positive_inert_contract":True,
            "adversarial_rejected":len(cases),
            "unique_adversarial_cases":len(set(cases)),
            "scope":"PUBLIC_TEST_TUF_CHAIN_AND_INERT_PAYLOAD_ONLY",
            "metadata_source":"PINNED_LOCAL_TEST_ROOT",
            "github_live_requests":0,
            "product_changes":False,"downloaded_real_executable":False,
            "installer_launched":False,"can_install":False,
            "operational_ready":False,"automatic_recovery_authorized":False}
    require(result["adversarial_rejected"] == 33 and
            result["adversarial_rejected"] == result["unique_adversarial_cases"],
            "EXPECTED_NEGATIVE_MATRIX")
    print("GF_GITHUB_AUTH_D1_LAB "+json.dumps(result,sort_keys=True))
    return 0

if __name__=="__main__":
    raise SystemExit(run())
