"""Integrated inert ZIPs, signed with the PREEXISTING PUBLIC TEST seeds only."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
import json
from pathlib import Path


def generate(out):
    out.mkdir()
    source = Path(__file__).resolve().parent.parent / "generate_fixtures.py"
    spec = importlib.util.spec_from_file_location("gf_u9_test_signer", source)
    g = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(g)
    now = datetime.now(timezone.utc).replace(microsecond=0)
    anchor = (source.parent / "fixtures/bootstrap.test-only.json").read_bytes()
    assert g.digest(anchor) == "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e"
    (out / "anchor.json").write_bytes(anchor)
    catalog = {}
    def package(seq):
        rows = [("bundle.fixture.txt", f"GF-U9-INERT-BUNDLE-{seq}-NO-EXE-NO-DLL\n".encode()), ("LICENSE.fixture.txt", b"Synthetic inert test data; no third-party binaries.\n")]
        manifest = "".join(g.digest(b) + "  " + name + "\n" for name, b in rows).encode()
        return g.make_zip(rows + [("SHA256SUMS.txt", manifest)]), rows, manifest
    def build(name, seq, meta, *, mix=False, wrong_identity=False):
        payload, rows, manifest = package(seq)
        descriptor = {"schema": "gf-integrated-fixture/9", "fixture_only": True, "sequence": seq,
            "version": f"0.0.0.{seq + (1 if wrong_identity else 0)}", "entrypoint": "bundle.fixture.txt",
            "entry_sha256": g.digest(rows[0][1]), "manifest_sha256": g.digest(manifest),
            "file_count": 3, "expanded_bytes": sum(len(b) for _, b in rows) + len(manifest)}
        targets = {"_type": "targets", "spec_version": "1.0.36", "version": meta,
            "expires": g.utc(now + timedelta(hours=8)), "targets": {"fixture-package.zip": {
                "length": len(payload), "hashes": {"sha256": g.digest(payload)}, "custom": {"fixture": descriptor}}}}
        tb = g.wire(g.envelope(targets, ["targets"]))
        snapshot = {"_type": "snapshot", "spec_version": "1.0.36", "version": meta, "expires": g.utc(now + timedelta(hours=8)),
            "meta": {"targets.json": {"version": meta, "length": len(tb), "hashes": {"sha256": g.digest(tb)}}}}
        sb = g.wire(g.envelope(snapshot, ["snapshot"]))
        timestamp = {"_type": "timestamp", "spec_version": "1.0.36", "version": meta, "expires": g.utc(now + timedelta(hours=1)),
            "meta": {"snapshot.json": {"version": meta, "length": len(sb), "hashes": {"sha256": g.digest(sb)}}}}
        files = {"2.root.json": g.wire(g.root(2, ["r4", "r5", "r6"], g.utc(now + timedelta(days=30)), ["r1", "r2", "r4", "r5"])),
            "timestamp.json": g.wire(g.envelope(timestamp, ["timestamp"])), "snapshot.json": sb, "targets.json": tb,
            "fixture-package.zip": package(3)[0] if mix else payload}
        if meta >= 3: files["3.root.json"] = g.wire(g.root(3, ["r4", "r5", "r6"], g.utc(now + timedelta(days=30)), ["r4", "r5"]))
        dest = out / name
        dest.mkdir()
        for p, b in files.items(): (dest / p).write_bytes(b)
        catalog[name] = {p: g.digest(b) for p, b in files.items()}
    build("release2", 2, 2)
    build("release3", 3, 3)
    build("lower", 1, 1)
    build("replay2fresh3", 2, 3)
    build("mixed", 2, 2, mix=True)
    build("wrong-identity", 2, 2, wrong_identity=True)
    data = {"fixture_only": True, "installable": False, "reference_utc": g.utc(now), "public_test_anchor": g.digest(anchor), "cases": catalog}
    (out / "catalog.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return {"cases": len(catalog), "catalog_sha256": g.digest((out / "catalog.json").read_bytes()), "reference_utc": data["reference_utc"], "public_test_keys_only": True}
