"""New directed vectors; python-tuf serializes/signs PREEXISTING public test seeds only."""
import base64
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
from tuf.api.metadata import Metadata
from securesystemslib.signer import CryptoSigner, SSlibKey


def generate(out):
    out.mkdir()
    source = Path(__file__).resolve().parent.parent / "generate_fixtures.py"
    spec = importlib.util.spec_from_file_location("u11_existing_public_test_seeds", source)
    g = importlib.util.module_from_spec(spec); spec.loader.exec_module(g)
    now = datetime.now(timezone.utc).replace(microsecond=0)
    anchor = (source.parent / "fixtures/bootstrap.test-only.json").read_bytes()
    assert g.digest(anchor) == "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e"
    (out / "anchor.json").write_bytes(anchor)
    signers = {name: CryptoSigner(key, SSlibKey.from_dict(g.IDS[name], g.KEYS[name])) for name, key in g.PRIVATE.items()}
    def signed(data, names):
        metadata = Metadata.from_bytes(g.wire(g.envelope(data, names)))
        for i, name in enumerate(names): metadata.sign(signers[name], append=i != 0)
        return metadata.to_bytes()
    def root(version, *, roots=("r1", "r2", "r3"), sign=("r1", "r2"), changes=None, expired=False):
        r = g.root(version, list(roots), g.utc(now + timedelta(days=-1 if expired else 30)), list(sign))["signed"]
        for role, key in (changes or {}).items():
            del r["keys"][g.IDS[role]]; r["keys"][g.IDS[key]] = copy.deepcopy(g.KEYS[key]); r["roles"][role]["keyids"] = [g.IDS[key]]
        return signed(r, sign)
    def package(seq):
        rows = [("bundle.fixture.txt", f"GF-U9-INERT-BUNDLE-{seq}-NO-EXE-NO-DLL\n".encode()), ("LICENSE.fixture.txt", b"Synthetic inert test data; no third-party binaries.\n")]
        manifest = "".join(g.digest(b) + "  " + name + "\n" for name, b in rows).encode()
        return g.make_zip(rows + [("SHA256SUMS.txt", manifest)]), rows, manifest
    cases = {}
    def build(name, version, seq=2, roots=None, keys=None, bad_timestamp=False, no_snapshot=False, expires=False, wrong_snapshot_hash=False):
        keys = keys or {}; payload, rows, manifest = package(seq)
        descriptor = {"schema":"gf-integrated-fixture/9","fixture_only":True,"sequence":seq,"version":f"0.0.0.{seq}","entrypoint":"bundle.fixture.txt",
                      "entry_sha256":g.digest(rows[0][1]),"manifest_sha256":g.digest(manifest),"file_count":3,"expanded_bytes":sum(len(b) for _,b in rows)+len(manifest)}
        base = {"spec_version":"1.0.36","version":version,"expires":g.utc(now+timedelta(hours=4))}
        targets = dict(base,_type="targets",targets={"fixture-package.zip":{"length":len(payload),"hashes":{"sha256":g.digest(payload)},"custom":{"fixture":descriptor}}})
        tb = signed(targets,[keys.get("targets","targets")])
        snapshot = dict(base,_type="snapshot",meta={"targets.json":{"version":version,"length":len(tb),"hashes":{"sha256":g.digest(tb)}}})
        sb = signed(snapshot,[keys.get("snapshot","snapshot")])
        timestamp = dict(base,_type="timestamp",expires=g.utc(now+timedelta(seconds=0 if expires else 3600)),meta={"snapshot.json":{"version":version,"length":len(sb),"hashes":{"sha256":"0"*64 if wrong_snapshot_hash else g.digest(sb)}}})
        ts = signed(timestamp,["timestamp" if bad_timestamp else keys.get("timestamp","timestamp")])
        files = {"timestamp.json":ts,"snapshot.json":sb,"targets.json":tb,"fixture-package.zip":payload,**(roots or {})}
        if no_snapshot: files.pop("snapshot.json")
        folder = out/name; folder.mkdir()
        for p,b in files.items(): (folder/p).write_bytes(b)
        cases[name] = {p:g.digest(b) for p,b in files.items()}
    r2 = root(2, roots=("r4","r5","r6"), sign=("r1","r2","r4","r5"))
    build("release2",2,roots={"2.root.json":r2})
    r3 = root(3,roots=("r4","r5","r6"),sign=("r4","r5"))
    build("release3",3,seq=3,roots={"2.root.json":r2,"3.root.json":r3})
    build("high",100)
    build("old1",1)
    for role in ("timestamp","snapshot"):
        rotated = root(2,changes={role:"intruder"})
        build("rotate-"+role,1,seq=3,roots={"2.root.json":rotated},keys={role:"intruder"})
    build("rotate-targets-high",101,seq=3,roots={"2.root.json":root(2,changes={"targets":"intruder"})},keys={"targets":"intruder"})
    build("rotate-targets-low",1,seq=3,roots={"2.root.json":root(2,changes={"targets":"intruder"})},keys={"targets":"intruder"})
    build("revoked-timestamp",1,roots={"2.root.json":root(2,changes={"timestamp":"intruder"})},keys={"timestamp":"intruder"},bad_timestamp=True)
    build("root-old-only",2,roots={"2.root.json":root(2,roots=("r4","r5","r6"),sign=("r1","r2"))})
    build("root-insufficient",2,roots={"2.root.json":root(2,sign=("r1",))})
    build("root-unauthorized",2,roots={"2.root.json":root(2,sign=("intruder",))})
    expired = root(2,roots=("r4","r5","r6"),sign=("r1","r2","r4","r5"),expired=True)
    build("expired-final-root",3,roots={"2.root.json":expired})
    build("expired-intermediate",3,roots={"2.root.json":expired,"3.root.json":r3})
    gap3 = root(3,roots=("r4","r5","r6"),sign=("r4","r5"),changes={"timestamp":"intruder"})
    build("root-gap",3,roots={"3.root.json":gap3},keys={"timestamp":"intruder"})
    build("missing-snapshot",2,roots={"2.root.json":r2},no_snapshot=True)
    build("bad-reference",2,roots={"2.root.json":r2},wrong_snapshot_hash=True)
    build("expired-timestamp",2,roots={"2.root.json":r2},expires=True)
    # Same timestamp version is a normal end-of-cycle, even if the re-signed payload changes.
    changed=json.loads((out/'release2/timestamp.json').read_bytes()); changed['signed']['expires']=g.utc(now+timedelta(hours=2))
    folder=out/'same-timestamp-changed'; folder.mkdir()
    for p in (out/'release2').iterdir(): (folder/p.name).write_bytes(p.read_bytes())
    (folder/'timestamp.json').write_bytes(signed(changed['signed'],['timestamp']))
    cases['same-timestamp-changed']={p.name:g.digest(p.read_bytes()) for p in folder.iterdir()}
    # Invalid role assignment remains disallowed by the existing closed POUF.
    malformed=json.loads(root(2)); malformed['signed']['roles']['targets']['keyids']=malformed['signed']['roles']['snapshot']['keyids']
    folder=out/'role-reuse'; folder.mkdir(); (folder/'2.root.json').write_bytes(signed(malformed['signed'],['r1','r2']))
    cases['role-reuse']={'2.root.json':g.digest((folder/'2.root.json').read_bytes())}
    # Public, independently curated upstream byte vectors, not created by our signer.
    catalog=json.loads((Path(__file__).parent/'fixtures/upstream-vectors.json').read_text())
    external=out/'external';external.mkdir()
    for path,row in catalog['files'].items():
        b=base64.b64decode(row['base64']);assert hashlib.sha256(b).hexdigest()==row['sha256']
        (external/Path(path).name).write_bytes(b)
    result={'schema':'gf-stage11-fixtures/1','reference_utc':g.utc(now),'cases':cases,'native_serializer_signer':'python-tuf 7.0.1 / securesystemslib 1.3.1 / existing public seeds',
            'new_production_keys':False,'real_GF_bytes':False,'upstream_vectors_commit':catalog['source_commit']}
    (out/'catalog.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    return result
