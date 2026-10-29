"""Public deterministic TEST keys only. Never run this to create production keys.

Independent signer: Python cryptography/OpenSSL, not the .NET verifier.
Generated payloads are inert text, never GF executables. Output stays under obj/.
"""
from datetime import datetime, timedelta, timezone
from io import BytesIO
from pathlib import Path
import argparse
import copy
import hashlib
import json
import zipfile
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat


def canonical(value):
    """OLPC/TUF canonical JSON; vectors include Unicode, quotes and newlines."""
    if isinstance(value, dict):
        return b"{" + b",".join(canonical(k) + b":" + canonical(v) for k, v in sorted(value.items())) + b"}"
    if isinstance(value, list):
        return b"[" + b",".join(map(canonical, value)) + b"]"
    if isinstance(value, str):
        return ('"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"').encode()
    return json.dumps(value, separators=(",", ":"), allow_nan=False).encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def utc(value):
    return value.strftime("%Y-%m-%dT%H:%M:%SZ")


SEEDS = {name: hashlib.sha256(("GF-U1-PUBLIC-TEST-KEY-NEVER-PRODUCTION:" + name).encode()).digest()
         for name in ["r1", "r2", "r3", "r4", "r5", "r6", "targets", "snapshot", "timestamp", "intruder"]}
PRIVATE = {name: Ed25519PrivateKey.from_private_bytes(seed) for name, seed in SEEDS.items()}
KEYS = {name: {"keytype": "ed25519", "scheme": "ed25519", "keyval": {
    "public": key.public_key().public_bytes(Encoding.Raw, PublicFormat.Raw).hex()}}
        for name, key in PRIVATE.items()}
IDS = {name: digest(canonical(key)) for name, key in KEYS.items()}


def envelope(signed, signers):
    return {"signed": signed, "signatures": [
        {"keyid": IDS[name], "sig": PRIVATE[name].sign(canonical(signed)).hex()} for name in signers]}


def wire(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode()


def root(version, roots, expires, signers):
    roles = {"root": {"keyids": [IDS[name] for name in roots], "threshold": 2}}
    roles.update({role: {"keyids": [IDS[role]], "threshold": 1}
                  for role in ["targets", "snapshot", "timestamp"]})
    names = roots + ["targets", "snapshot", "timestamp"]
    return envelope({"_type": "root", "spec_version": "1.0.36", "version": version,
                     "expires": expires, "consistent_snapshot": False,
                     "keys": {IDS[name]: copy.deepcopy(KEYS[name]) for name in names}, "roles": roles}, signers)


def make_zip(entries, symlink=False):
    target = BytesIO()
    with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, data in entries:
            item = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
            item.compress_type = zipfile.ZIP_DEFLATED
            item.create_system = 3
            item.external_attr = ((0o120777 if symlink else 0o100600) << 16)
            archive.writestr(item, data)
    return target.getvalue()


def main(output):
    output.mkdir(parents=True, exist_ok=True)
    now = datetime.now(timezone.utc).replace(microsecond=0)
    bootstrap = root(1, ["r1", "r2", "r3"], "2027-01-01T00:00:00Z", ["r1", "r2"])
    anchor = wire(bootstrap)
    reviewed_anchor = Path(__file__).resolve().parent / "fixtures/bootstrap.test-only.json"
    if reviewed_anchor.is_file() and reviewed_anchor.read_bytes() != anchor:
        raise ValueError("Public fixture anchor differs from the reviewed test data.")
    (output / "trusted-root-v1.json").write_bytes(anchor)
    config = {"fixture_only": True, "reference_time": utc(now),
              "bootstrap_sha256": digest(anchor), "public_key_ids": IDS}
    base_entries = [("fixture-marker.txt", b"GF-U1-INERT-TEXT-NOT-AN-EXECUTABLE\n"),
                    ("LICENSE.fixture.txt", b"Synthetic fixture data; no third-party binaries.\n")]
    manifest = "".join(digest(data) + "  " + name + "\n" for name, data in base_entries).encode()
    default_entries = base_entries + [("SHA256SUMS.txt", manifest)]
    cases = {}

    def build(name, *, mutate_gf=None, entries=None, mutate_targets=None,
              mutate_root=None, mutate_snapshot=None, mutate_timestamp=None,
              signature_mode=None, change_archive=None, root_mode=None, ascii_targets=False):
        rows = default_entries if entries is None else entries
        payload = make_zip(rows, symlink=name == "zip-symlink")
        container = make_zip([("fixture-package.zip", payload),
                              ("ENTREGA-PRIVADA.json", b'{"fixture_only":true}'),
                              ("ZIP-SHA256SUMS.txt", (digest(payload) + "  fixture-package.zip\n").encode()),
                              ("Antivirus-evidencias.json", b'{"fixture_only":true,"scan":false}')])
        gf = {"schema": "gf-self-update/1", "product": "gerenciador-final", "channel": "beta",
              "architecture": "win-x64", "version": "5.13.11.67", "release_sequence": 67,
              "source_commit": "a" * 40, "controller_commit": "b" * 40,
              "run_id": "100", "artifact_id": "200", "run_attempt": 1,
              "runtime_profile": "local", "remote_diagnostics": False,
              "requires_user_confirmation": True, "qualification": "beta-test-not-homologated",
              "min_updater_protocol": 1, "data_schema": 1, "entrypoint": "GerenciadorFinal.exe",
              "content_manifest_sha256": digest(manifest), "file_count": len(rows),
              "expanded_bytes": sum(len(data) for _, data in rows),
              "delivery_repository": "walissonminel-1991/gf-beta-entregas",
              "container_length": len(container), "container_sha256": digest(container),
              "payload_member": "fixture-package.zip", "artifact_expires_at": utc(now + timedelta(days=4)),
              "authenticode_mode": "beta-unsigned",
              "release_notes": 'FIXTURE somente: açã o 😀; "aspas"; \\; linha\nseguinte.'}
        if mutate_gf:
            mutate_gf(gf)
        targets = {"_type": "targets", "spec_version": "1.0.36", "version": 1,
                   "expires": utc(now + timedelta(days=6)), "targets": {
                       "fixture-package.zip": {"length": len(payload), "hashes": {"sha256": digest(payload)},
                                               "custom": {"gf": gf}}}}
        if mutate_targets:
            mutate_targets(targets)
        te = envelope(targets, ["targets"])
        if signature_mode == "duplicate":
            te["signatures"].append(copy.deepcopy(te["signatures"][0]))
        if signature_mode == "wrong-role":
            te = envelope(targets, ["intruder"])
        if signature_mode == "corrupt":
            te["signatures"][0]["sig"] = "00" * 64
        tb = json.dumps(te, ensure_ascii=ascii_targets, separators=(",", ":")).encode()
        snapshot = {"_type": "snapshot", "spec_version": "1.0.36", "version": 1,
                    "expires": utc(now + timedelta(days=2)), "meta": {
                        "targets.json": {"version": 1, "length": len(tb), "hashes": {"sha256": digest(tb)}}}}
        if mutate_snapshot:
            mutate_snapshot(snapshot)
        sb = wire(envelope(snapshot, ["snapshot"]))
        timestamp = {"_type": "timestamp", "spec_version": "1.0.36", "version": 1,
                     "expires": utc(now + timedelta(hours=24)), "meta": {
                         "snapshot.json": {"version": 1, "length": len(sb), "hashes": {"sha256": digest(sb)}}}}
        if mutate_timestamp:
            mutate_timestamp(timestamp)
        refreshed_root = root(2, ["r4", "r5", "r6"], utc(now + timedelta(days=90)),
                              ["r1", "r2", "r4", "r5"])
        if root_mode == "old-only":
            refreshed_root = envelope(refreshed_root["signed"], ["r1", "r2"])
        if root_mode == "new-only":
            refreshed_root = envelope(refreshed_root["signed"], ["r4", "r5"])
        if mutate_root:
            mutate_root(refreshed_root["signed"])
            refreshed_root = envelope(refreshed_root["signed"], ["r1", "r2", "r4", "r5"])
        files = {"2.root.json": wire(refreshed_root), "timestamp.json": wire(envelope(timestamp, ["timestamp"])),
                 "snapshot.json": sb, "targets.json": tb, "fixture-package.zip": payload,
                 "container.zip": change_archive(container) if change_archive else container}
        folder = output / name
        folder.mkdir(exist_ok=True)
        for filename, data in files.items():
            (folder / filename).write_bytes(data)
        cases[name] = {"sha256": {filename: digest(data) for filename, data in files.items()}}

    build("valid")
    build("valid-escaped-unicode", ascii_targets=True)
    build("metadata-v2", mutate_gf=lambda g: g.update(version="5.13.11.68", release_sequence=68),
          mutate_targets=lambda t: t.update(version=2),
          mutate_snapshot=lambda s: (s.update(version=2), s["meta"]["targets.json"].update(version=2)),
          mutate_timestamp=lambda t: (t.update(version=2), t["meta"]["snapshot.json"].update(version=2)))
    build("bad-signature", signature_mode="corrupt")
    build("wrong-role", signature_mode="wrong-role")
    build("duplicate-signature", signature_mode="duplicate")
    build("downgrade", mutate_gf=lambda g: g.update(version="5.13.11.9", release_sequence=9))
    build("replayed-sequence", mutate_gf=lambda g: g.update(release_sequence=66))
    build("same-version-conflict", mutate_gf=lambda g: g.update(version="5.13.11.66"))
    build("expired", mutate_timestamp=lambda t: t.update(expires=utc(now - timedelta(seconds=1))))
    build("long-timestamp", mutate_timestamp=lambda t: t.update(expires=utc(now + timedelta(days=20))))
    build("expired-artifact", mutate_gf=lambda g: g.update(artifact_expires_at=utc(now - timedelta(seconds=1))))
    build("wrong-architecture", mutate_gf=lambda g: g.update(architecture="win-x86"))
    build("connected", mutate_gf=lambda g: g.update(runtime_profile="internal", remote_diagnostics=True))
    build("wrong-repository", mutate_gf=lambda g: g.update(delivery_repository="someone/else"))
    build("unsupported-protocol", mutate_gf=lambda g: g.update(min_updater_protocol=2))
    build("unknown-field", mutate_gf=lambda g: g.update(download_url="https://invalid.example/private"))
    build("overflow-sequence", mutate_gf=lambda g: g.update(release_sequence=2**53))
    build("fractional-sequence", mutate_gf=lambda g: g.update(release_sequence=67.0))
    build("duplicate-target", mutate_targets=lambda t: t["targets"].update({"other.zip": copy.deepcopy(t["targets"]["fixture-package.zip"])}))
    build("snapshot-mismatch", mutate_snapshot=lambda s: s["meta"]["targets.json"].update(hashes={"sha256": "0"*64}))
    build("timestamp-version-mismatch", mutate_timestamp=lambda t: t["meta"]["snapshot.json"].update(version=2))
    build("rotation-old-only", root_mode="old-only")
    build("rotation-new-only", root_mode="new-only")
    build("root-wrong-threshold", mutate_root=lambda r: r["roles"]["root"].update(threshold=1))
    build("root-unknown-algorithm", mutate_root=lambda r: r["keys"][IDS["targets"]].update(scheme="unknown"))
    build("bad-container", change_archive=lambda b: b[:-1]+bytes([b[-1]^1]))
    build("content-manifest-hash", mutate_gf=lambda g: g.update(content_manifest_sha256="0"*64))
    build("notes-markup", mutate_gf=lambda g: g.update(release_notes="<script>not executable</script>"))
    build("leading-zero-version", mutate_gf=lambda g: g.update(version="05.13.11.67"))
    build("version-component-range", mutate_gf=lambda g: g.update(version="5.13.11.65536"))
    build("root-skip", mutate_root=lambda r: r.update(version=3))
    for name in ["../escape.txt", "/absolute.txt", "C:/drive.txt", "a:b.txt", "NUL.txt", "trailing. ", "a\\b.txt"]:
        key = "zip-path-" + digest(name.encode())[:8]
        build(key, entries=default_entries + [(name, b"hostile")])
    build("zip-case-collision", entries=default_entries + [("FIXTURE-MARKER.TXT", b"collision")])
    build("zip-symlink")
    build("zip-bomb", entries=default_entries + [("large.txt", b"0" * (2 * 1024 * 1024))])
    config["cases"] = cases
    (output / "catalog.json").write_text(json.dumps(config, indent=2)+"\n")
    print(json.dumps({"fixture_only": True, "cases": len(cases), "bootstrap_sha256": config["bootstrap_sha256"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    main(parser.parse_args().output)
