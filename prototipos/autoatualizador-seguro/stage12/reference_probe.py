"""Pinned Python Updater, observed unchanged, plus explicitly TEST-ONLY ablations.

The diagnostic invalidation is not a library patch or a product adapter. It has
no durability protocol and is never proposed for operational use.
"""
from datetime import datetime
import hashlib
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.parse import urlsplit
from tuf.ngclient import Updater
from tuf.ngclient.fetcher import FetcherInterface
from tuf.api import exceptions


def sha(b):
    return hashlib.sha256(b).hexdigest()


class Cut(Exception):
    pass


class InertFetcher(FetcherInterface):
    def __init__(self, files):
        self.files, self.requests = files, []

    def _fetch(self, url):
        assert urlsplit(url).hostname == 'gf-u12-fixture.invalid'
        name = urlsplit(url).path.rsplit('/', 1)[-1]
        self.requests.append(name)
        if name not in self.files:
            raise exceptions.DownloadHTTPError('inert missing fixture', 404)
        yield self.files[name]


class BoundaryProbe(Updater):
    def __init__(self, *args, mode, catalog, scenario, **kwargs):
        self.trace, self.mode, self.catalog, self.scenario = [], mode, catalog, scenario
        super().__init__(*args, **kwargs)

    def versions(self):
        t = self._trusted_set
        return {r: getattr(t, r).version if r in t else 0 for r in ('root', 'timestamp', 'snapshot', 'targets')}

    def _load_local_metadata(self, role):
        try:
            b = super()._load_local_metadata(role)
            self.trace.append({'event': 'local_load', 'role': role, 'sha256': sha(b)})
            return b
        except OSError:
            self.trace.append({'event': 'local_missing', 'role': role})
            raise

    def _load_root(self):
        before = self._trusted_set.root
        super()._load_root()  # actual unmodified upstream algorithm/authentication/persistence
        after = self._trusted_set.root
        changed = [r for r in ('timestamp', 'snapshot') if set(before.roles[r].keyids) != set(after.roles[r].keyids)]
        self.trace.append({'event': 'after_upstream_load_root', 'old_root': before.version,
                           'new_root': after.version, 'key_changes': changed, 'state': self.versions()})
        if self.mode == 'cut-before-invalidation':
            raise Cut('controlled exception after upstream root persistence; before lower-role invalidation')
        if self.mode.startswith('diagnostic') and changed:
            # An ablation bound to THESE TWO FIXTURES only. No general reset API.
            assert before.version == 1 and after.version == 2
            assert not after.is_expired(self._trusted_set.reference_time)
            root_path = Path(self._dir) / 'root_history/2.root.json'
            assert sha(root_path.read_bytes()) == self.catalog['scenarios'][self.scenario]['2.root.json']
            vector = next(v for v in self.catalog['vectors'] if v['rotated_role'] == changed[0])
            assert changed == [vector['rotated_role']]
            assert {r: sorted(before.roles[r].keyids) for r in ('root', 'timestamp', 'snapshot', 'targets')} == {
                r: sorted(v['keyids']) for r, v in vector['old_roles'].items()}
            assert {r: sorted(after.roles[r].keyids) for r in ('root', 'timestamp', 'snapshot', 'targets')} == {
                r: sorted(v['keyids']) for r, v in vector['new_roles'].items()}
            deleted = {}
            for role in ('timestamp', 'snapshot'):
                path = Path(self._dir) / (role + '.json')
                if path.is_file():
                    deleted[role] = sha(path.read_bytes())
                    path.unlink()
            self.trace.append({'event': 'TEST_ONLY_normative_invalidation', 'deleted_sha256': deleted,
                               'authenticated_root_sha256': sha(root_path.read_bytes()), 'section': '5.3.11'})
            if self.mode == 'diagnostic-cut-after-invalidation':
                raise Cut('controlled exception after diagnostic invalidation; not a process/power test')

    def _load_timestamp(self):
        try:
            super()._load_timestamp()
        finally:
            self.trace.append({'event': 'after_load_timestamp', 'state': self.versions()})

    def _load_snapshot(self):
        try:
            super()._load_snapshot()
        finally:
            self.trace.append({'event': 'after_load_snapshot', 'state': self.versions()})


def run(fixtures):
    c = json.loads((fixtures / 'vectors.json').read_text())
    utc = datetime.fromisoformat(c['reference_utc'].replace('Z', '+00:00'))
    anchor = (fixtures / 'anchor.json').read_bytes()
    rows = []

    def state(folder):
        return {r: sha((folder / (r + '.json')).read_bytes()) if (folder / (r + '.json')).is_file() else None
                for r in ('root', 'timestamp', 'snapshot', 'targets')}

    def attempt(folder, scenario, bootstrap=None, mode='observe-unchanged'):
        files = {n: (fixtures / scenario / n).read_bytes() for n in c['scenarios'][scenario]}
        assert {n: sha(b) for n, b in files.items()} == c['scenarios'][scenario]
        before = state(folder)
        fetch = InertFetcher(files)
        u = BoundaryProbe(str(folder), 'https://gf-u12-fixture.invalid/', target_dir=str(folder / 'targets'),
                          target_base_url='https://gf-u12-fixture.invalid/', fetcher=fetch, bootstrap=bootstrap,
                          mode=mode, catalog=c, scenario=scenario)
        # Clock seam only: no algorithm or signed bytes changed. Both clients get exactly this UTC.
        u._trusted_set.reference_time = utc
        try:
            u.refresh()
            info = u.get_targetinfo('fixture-package.zip')
            assert info is not None
            path = u.download_target(info)
            assert sha(Path(path).read_bytes()) == c['scenarios'][scenario]['fixture-package.zip']
            outcome, error, message = 'accepted', None, None
        except Cut as e:
            outcome, error, message = 'cut', type(e).__name__, str(e)
        except (exceptions.RepositoryError, exceptions.DownloadError) as e:
            outcome, error, message = 'rejected', type(e).__name__, str(e)
        return {'status': outcome, 'error': error, 'message': message, 'versions': u.versions(), 'mode': mode,
                'fixed_utc': c['reference_utc'], 'before_cache_sha256': before, 'after_cache_sha256': state(folder),
                'trace': u.trace, 'requests': fetch.requests, 'served_sha256': {n: sha(files[n]) for n in fetch.requests if n in files}}

    def record(role, mode, observed, expected, normative=None, preparation=None):
        ok = observed['status'] == expected
        if expected == 'rejected' and mode in ('retained', 'same-epoch-replay', 'recover-before-invalidation'):
            ok = ok and observed['error'] == 'BadVersionNumberError'
        rows.append({'id': 'U12-PY-' + role.upper() + '-' + mode.upper(), 'role': role, 'mode': mode,
                     'status': 'passed' if ok else 'failed', 'expected_observation': expected,
                     'normative_behavior': normative, 'observed': observed, 'preparation': preparation})

    for role in ('timestamp', 'snapshot'):
        scenario = 'rotate-' + role
        for mode in ('retained', 'clean', 'diagnostic-invalidation', 'recover-before-invalidation', 'recover-after-invalidation', 'same-epoch-replay', 'revoked'):
            with TemporaryDirectory(prefix='gf-u12-py-') as d:
                folder = Path(d)
                if mode != 'clean':
                    baseline = attempt(folder, 'high', anchor)
                    assert baseline['status'] == 'accepted' and baseline['versions'] == {'root': 1, 'timestamp': 100, 'snapshot': 100, 'targets': 100}
                if mode == 'retained':
                    record(role, mode, attempt(folder, scenario), 'rejected', 'failed: required 5.3.11 invalidation absent; expected acceptance')
                elif mode == 'clean':
                    record(role, mode, attempt(folder, scenario, anchor), 'accepted', 'passed for this clean fixture state')
                elif mode == 'diagnostic-invalidation':
                    record(role, mode, attempt(folder, scenario, mode='diagnostic-invalidation'), 'accepted', 'passed only with test-only invalidation; upstream remains unchanged')
                elif mode.startswith('recover-'):
                    after = mode == 'recover-after-invalidation'
                    cut = attempt(folder, scenario, mode='diagnostic-cut-after-invalidation' if after else 'cut-before-invalidation')
                    assert cut['status'] == 'cut' and cut['versions']['root'] == 2
                    record(role, mode, attempt(folder, scenario), 'accepted' if after else 'rejected',
                           'diagnostic recovery only; Python cuts are exceptions, not process crashes', cut)
                elif mode == 'same-epoch-replay':
                    rotated = attempt(folder, scenario, mode='diagnostic-invalidation')
                    assert rotated['status'] == 'accepted'
                    advanced = attempt(folder, 'next-' + role)
                    assert advanced['status'] == 'accepted' and advanced['versions'] == {'root': 2, 'timestamp': 2, 'snapshot': 2, 'targets': 2}
                    record(role, mode, attempt(folder, scenario), 'rejected', 'passed: no reset in the same authenticated epoch', {'rotation': rotated, 'advance': advanced})
                else:
                    record(role, mode, attempt(folder, 'revoked-' + role, mode='diagnostic-invalidation'), 'rejected', 'passed: revoked signer rejected after authenticated invalidation')
    for scenario in ('no-rotation-lower', 'bad-root-threshold'):
        with TemporaryDirectory(prefix='gf-u12-py-negative-') as d:
            folder = Path(d)
            assert attempt(folder, 'high', anchor)['status'] == 'accepted'
            result = attempt(folder, scenario, mode='diagnostic-invalidation')
            assert not any(e['event'] == 'TEST_ONLY_normative_invalidation' for e in result['trace'])
            record('control', scenario, result, 'rejected', 'passed: diagnostic must not invalidate without an authenticated role key change')
    return {'schema': 'gf-stage12-python-probe/1', 'source_commit': '49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15',
            'total': len(rows), 'passed': sum(r['status'] == 'passed' for r in rows), 'failed': sum(r['status'] == 'failed' for r in rows),
            'results': rows, 'upstream_source_modified': False, 'clock_seam': 'set reference_time once before refresh, identical recorded UTC',
            'diagnostic_adapter_operational': False, 'full_conformance': False, 'external_independent_review': False}
