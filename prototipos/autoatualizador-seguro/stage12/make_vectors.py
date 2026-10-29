"""Two minimal rotation vectors, controls and exact bytes for both implementations."""
import base64
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
from tuf.api.metadata import Metadata
from securesystemslib.signer import CryptoSigner, SSlibKey

HERE = Path(__file__).resolve().parent


def generate(out):
    out.mkdir()
    source = HERE.parent / 'generate_fixtures.py'
    spec = importlib.util.spec_from_file_location('u12_public_test_keys', source)
    g = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(g)
    # A recorded, fixed fixture clock, identical for all implementations/attempts.
    # The small future offset permits real-time journal bootstrap before this clock.
    utc = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(minutes=10)
    signers = {n: CryptoSigner(k, SSlibKey.from_dict(g.IDS[n], copy.deepcopy(g.KEYS[n]))) for n, k in g.PRIVATE.items()}

    def signed(value, names):
        m = Metadata.from_bytes(g.wire(g.envelope(value, names)))
        for i, n in enumerate(names):
            m.sign(signers[n], append=i != 0)
        return m.to_bytes()

    anchor = (HERE.parent / 'fixtures/bootstrap.test-only.json').read_bytes()
    assert g.digest(anchor) == 'a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e'
    blobs = {}
    scenarios = {}

    def blob(b):
        h = g.digest(b)
        blobs[h] = {'bytes': len(b), 'base64': base64.b64encode(b).decode()}
        return h

    (out / 'anchor.json').write_bytes(anchor)
    anchor_hash = blob(anchor)
    rows = [('bundle.fixture.txt', b'GF-U12-INERT-NO-EXE-NO-DLL\n'),
            ('LICENSE.fixture.txt', b'Synthetic public test data only.\n')]
    manifest = ''.join(g.digest(b) + '  ' + n + '\n' for n, b in rows).encode()
    payload = g.make_zip(rows + [('SHA256SUMS.txt', manifest)])

    def root(role=None, insufficient=False):
        value = g.root(2, ['r1', 'r2', 'r3'], g.utc(utc + timedelta(days=30)), ['r1', 'r2'])['signed']
        if role:
            del value['keys'][g.IDS[role]]
            value['keys'][g.IDS['intruder']] = copy.deepcopy(g.KEYS['intruder'])
            value['roles'][role]['keyids'] = [g.IDS['intruder']]
        return signed(value, ['r1'] if insufficient else ['r1', 'r2'])

    def build(name, version, rotated=None, root_bytes=None, revoked=False):
        common = {'spec_version': '1.0.36', 'version': version, 'expires': g.utc(utc + timedelta(hours=1))}
        target = signed(dict(common, _type='targets', targets={'fixture-package.zip': {
            'length': len(payload), 'hashes': {'sha256': g.digest(payload)}}}), ['targets'])
        snapshot = signed(dict(common, _type='snapshot', meta={'targets.json': {
            'version': version, 'length': len(target), 'hashes': {'sha256': g.digest(target)}}}),
            ['intruder' if rotated == 'snapshot' and not revoked else 'snapshot'])
        timestamp = signed(dict(common, _type='timestamp', meta={'snapshot.json': {
            'version': version, 'length': len(snapshot), 'hashes': {'sha256': g.digest(snapshot)}}}),
            ['intruder' if rotated == 'timestamp' and not revoked else 'timestamp'])
        files = {'timestamp.json': timestamp, 'snapshot.json': snapshot, 'targets.json': target, 'fixture-package.zip': payload}
        if root_bytes is not None:
            files['2.root.json'] = root_bytes
        folder = out / name
        folder.mkdir()
        for n, b in files.items():
            (folder / n).write_bytes(b)
        scenarios[name] = {n: blob(b) for n, b in files.items()}

    build('high', 100)
    vectors = []
    old = json.loads(anchor)['signed']
    for role in ('timestamp', 'snapshot'):
        r = root(role)
        build('rotate-' + role, 1, role, r)
        build('next-' + role, 2, role, r)
        build('revoked-' + role, 1, role, r, revoked=True)
        new = json.loads(r)['signed']
        vectors.append({'id': 'U12-' + role.upper(), 'rotated_role': role, 'initial_scenario': 'high',
                        'initial_versions': {'root': 1, 'timestamp': 100, 'snapshot': 100, 'targets': 100},
                        'new_versions': {'root': 2, 'timestamp': 1, 'snapshot': 1, 'targets': 1},
                        'old_roles': old['roles'], 'new_roles': new['roles'],
                        'old_keys': old['keys'], 'new_keys': new['keys'],
                        'authorized_changes': ['root version 1 -> 2', role + ' key -> existing public intruder fixture key'],
                        'invalidate': ['timestamp', 'snapshot'],
                        'targets_floor': 'Derived from the invalidated snapshot; new authenticated snapshot establishes version 1. Stored targets100 must not override the new reference.',
                        'expected': 'accept after authenticated root2 and invalidation; no reset without this key change',
                        'normative_sections': ['5.3.4', '5.3.5', '5.3.8', '5.3.10', '5.3.11', '5.4.3', '5.5.5', '5.6.4']})
    build('no-rotation-lower', 1, root_bytes=root())
    build('bad-root-threshold', 1, 'timestamp', root('timestamp', insufficient=True))
    catalog = {'schema': 'gf-stage12-minimal-vectors/1', 'contract': 'gf-self-update/1', 'reference_utc': g.utc(utc),
               'clock_kind': 'fixed test clock, shared verbatim by both implementations; not production wall time',
               'anchor_sha256': anchor_hash, 'vectors': vectors, 'scenarios': scenarios, 'blobs': blobs,
               'signer': 'python-tuf 7.0.1 / securesystemslib 1.3.1; only existing public deterministic test seeds',
               'historical_stage11_bytes_reused': False, 'historical_transition_shape_reproduced': True,
               'real_GF_bytes': False, 'production_keys_created': False, 'external_conformance_fixture': False}
    (out / 'vectors.json').write_text(json.dumps(catalog, indent=2) + '\n', encoding='utf-8')
    return catalog


def materialize(catalog_path, out):
    """Replay a recorded evidence catalog without signing or using the current clock."""
    c = json.loads(Path(catalog_path).read_text(encoding='utf-8'))
    out.mkdir()
    decoded = {}
    for h, row in c['blobs'].items():
        b = base64.b64decode(row['base64'], validate=True)
        assert len(b) == row['bytes'] and hashlib.sha256(b).hexdigest() == h
        decoded[h] = b
    (out / 'anchor.json').write_bytes(decoded[c['anchor_sha256']])
    for name, files in c['scenarios'].items():
        assert name in {'high', 'no-rotation-lower', 'bad-root-threshold', 'rotate-timestamp', 'rotate-snapshot',
                        'next-timestamp', 'next-snapshot', 'revoked-timestamp', 'revoked-snapshot'}
        folder = out / name
        folder.mkdir()
        for n, h in files.items():
            assert n in {'timestamp.json', 'snapshot.json', 'targets.json', 'fixture-package.zip', '2.root.json'}
            (folder / n).write_bytes(decoded[h])
    (out / 'vectors.json').write_text(json.dumps(c, indent=2) + '\n', encoding='utf-8')
    return c
