"""Eight directed fixtures; existing public TEST seeds only, no GF executable/release."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
import json
from pathlib import Path


def generate(output):
    output.mkdir()
    source = Path(__file__).resolve().parent.parent / "generate_fixtures.py"
    spec = importlib.util.spec_from_file_location("gf_public_test_signer", source)
    signer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(signer)
    t0 = datetime(2026, 11, 2, 12, 0, 0, tzinfo=timezone.utc)
    anchor = (source.parent / "fixtures/bootstrap.test-only.json").read_bytes()
    assert signer.digest(anchor) == "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e"
    (output / "anchor.json").write_bytes(anchor)
    content = b"GF-U8-INERT-TEXT-NO-PRODUCT-NO-INSTALL\n"
    common = {"spec_version": "1.0.36", "version": 1, "expires": signer.utc(t0 + timedelta(hours=1))}
    targets = dict(common, _type="targets", targets={"fixture-package.zip": {"length": len(content), "hashes": {"sha256": signer.digest(content)}}})
    catalog = {}
    def build(name, equality=None, mutated=False, badsig=False, rotation=False):
        roles = {"targets": copy.deepcopy(targets), "snapshot": dict(common, _type="snapshot", meta={}), "timestamp": dict(common, _type="timestamp", meta={})}
        for role in roles:
            if role == equality: roles[role]["expires"] = signer.utc(t0)
        tb = signer.wire(signer.envelope(roles["targets"], ["targets"]))
        roles["snapshot"]["meta"] = {"targets.json": {"version": 1, "length": len(tb), "hashes": {"sha256": signer.digest(tb)}}}
        sb = signer.wire(signer.envelope(roles["snapshot"], ["snapshot"]))
        roles["timestamp"]["meta"] = {"snapshot.json": {"version": 1, "length": len(sb), "hashes": {"sha256": signer.digest(sb)}}}
        if mutated: roles["timestamp"]["meta"]["snapshot.json"]["version"] = 2
        te = signer.envelope(roles["timestamp"], ["timestamp"])
        if badsig: te["signatures"][0]["sig"] = "00" * 64
        files = {"timestamp.json": signer.wire(te), "snapshot.json": sb, "targets.json": tb, "fixture-package.zip": content}
        if equality == "root": files["2.root.json"] = signer.wire(signer.root(2, ["r1", "r2", "r3"], signer.utc(t0), ["r1", "r2"]))
        if rotation:
            files["2.root.json"] = signer.wire(signer.root(2, ["r1", "r2", "r3"], signer.utc(t0 - timedelta(seconds=1)), ["r1", "r2"]))
            files["3.root.json"] = signer.wire(signer.root(3, ["r1", "r2", "r3"], signer.utc(t0 + timedelta(hours=1)), ["r1", "r2"]))
        folder = output / name
        folder.mkdir()
        for p, b in files.items(): (folder / p).write_bytes(b)
        catalog[name] = {p: signer.digest(b) for p, b in files.items()}
    build("base")
    for role in ("root", "timestamp", "snapshot", "targets"): build("equal-" + role, equality=role)
    build("same-version-changed", mutated=True)
    build("same-version-badsig", badsig=True)
    build("rotation", rotation=True)
    data = {"fixture_only": True, "installable": False, "test_keys_public_preexisting": True, "reference_utc": signer.utc(t0), "anchor_sha256": signer.digest(anchor), "cases": catalog}
    (output / "catalog.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return {"cases": len(catalog), "catalog_sha256": signer.digest((output / "catalog.json").read_bytes()), "reference_utc": data["reference_utc"], "test_only": True}
