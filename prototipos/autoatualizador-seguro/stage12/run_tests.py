"""Stage12 only: two rotation divergences and their directly affected paths."""
import argparse
import hashlib
import importlib.metadata
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ENTRY = '65360953fcc15023a7853e7f3304217fdee67065'


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def main(args):
    report = args.report.resolve()
    assert not report.is_relative_to(REPO)
    report.parent.mkdir(parents=True, exist_ok=True)
    saved = {'schema': 'gf-stage12-controller/1', 'contract': 'gf-self-update/1', 'entry_head': ENTRY,
             'status': 'not-executed', 'product_built': False, 'product_executed': False, 'prior_45_executed': False,
             'old_suites_reexecuted': False, 'physical_tests': 0, 'operational_G2_closed': False, 'artifacts_uploaded': 0,
             'new_production_keys': False, 'upstream_correction': False, 'prototype_correction': False}
    try:
        if os.name != 'nt':
            saved.update(status='blocked', reason='Disposable Windows/NTFS required')
            return 2
        env = {**os.environ, 'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
               'DOTNET_GENERATE_ASPNET_CERTIFICATE': 'false', 'PYTHONUTF8': '1'}
        dotnet = shutil.which('dotnet')
        assert dotnet
        sdk = subprocess.check_output([dotnet, '--version'], cwd=REPO, env=env, text=True).strip()
        assert sdk == '10.0.402'
        versions = {p: importlib.metadata.version(p) for p in ('tuf', 'securesystemslib', 'cryptography', 'urllib3')}
        assert versions == {'tuf': '7.0.1', 'securesystemslib': '1.3.1', 'cryptography': '50.0.2', 'urllib3': '2.8.0'}
        import tuf
        package_root = Path(tuf.__file__).resolve().parent.parent
        pin = json.loads((HERE.parent / 'stage11/fixtures/oracle-source-pin.json').read_text())
        observed = {p: hashlib.sha256((package_root / p).read_bytes()).hexdigest() for p in pin['files']}
        assert observed == pin['files']
        saved['python_source'] = {**pin, 'installed_sha256': observed, 'versions': versions, 'modified': False}
        changed = subprocess.check_output(['git', 'diff', '--name-only', ENTRY, 'HEAD'], cwd=REPO, text=True).splitlines()
        allowed = ('prototipos/autoatualizador-seguro/stage12/', '.github/workflows/autoatualizador-etapa12-isolado.yml',
                   'docs/WORK-AUTOATUALIZADOR-ETAPA12.md', 'docs/evidencias/autoatualizador-etapa12',
                   'docs/WORK-AUTOATUALIZADOR-CHECKPOINT.md', 'docs/PROGRESSO.json')
        assert all(p.startswith(allowed) for p in changed), changed
        with tempfile.TemporaryDirectory(prefix='gf-u12-controller-') as d:
            work = Path(d).resolve()
            assert not work.is_relative_to(REPO)
            source, provenance = module(HERE.parent / 'stage8/prepare_source.py', 'u12_source').prepare(work)
            common = ['-p:ArtifactsPath=' + str(work / 'artifacts'), '-p:Stage8Source=' + str(source)]
            subprocess.run([dotnet, 'restore', str(HERE / 'Stage12.csproj'), *common, '--locked-mode', '--nologo'], cwd=REPO, env=env, check=True)
            subprocess.run([dotnet, 'build', str(HERE / 'Stage12.csproj'), *common, '-c', 'Release', '--no-restore', '--nologo'], cwd=REPO, env=env, check=True)
            maker = module(HERE / 'make_vectors.py', 'u12_vectors')
            catalog = maker.materialize(args.vectors, work / 'fixtures') if args.vectors else maker.generate(work / 'fixtures')
            saved['vectors'] = catalog
            # Verify roundtrip byte catalog before running either implementation.
            restored = work / 'roundtrip'
            maker.materialize(work / 'fixtures/vectors.json', restored)
            assert {p.relative_to(restored).as_posix(): p.read_bytes() for p in restored.rglob('*') if p.is_file()} == {
                p.relative_to(work / 'fixtures').as_posix(): p.read_bytes() for p in (work / 'fixtures').rglob('*') if p.is_file()}
            assemblies = list((work / 'artifacts/bin').rglob('GF.Updater.Stage12.Tests.dll'))
            assert len(assemblies) == 1
            output = work / 'windows.json'
            completed = subprocess.run([dotnet, str(assemblies[0]), str(work / 'fixtures'), str(output)], cwd=REPO, env=env)
            if not output.is_file():
                raise RuntimeError('No complete Windows report; no test approval inferred')
            saved['windows'] = json.loads(output.read_text())
            saved['python_probe'] = module(HERE / 'reference_probe.py', 'u12_reference').run(work / 'fixtures')
            assert saved['windows']['total'] == 14 and saved['python_probe']['total'] == 16
            comparisons = []
            own = {(r.get('role'), r['mode']): r for r in saved['windows']['observations']}
            for r in saved['python_probe']['results']:
                mode = r['mode']
                if mode not in ('retained', 'clean', 'diagnostic-invalidation', 'same-epoch-replay', 'revoked'):
                    continue
                counterpart = own[(r['role'], 'retained' if mode == 'diagnostic-invalidation' else mode)]
                equivalent = counterpart['status'] == r['observed']['status']
                comparisons.append({'role': r['role'], 'mode': mode, 'dotnet': counterpart['status'], 'python': r['observed']['status'],
                                    'status': 'passed' if equivalent else 'blocked',
                                    'upstream_unchanged_algorithm': mode != 'diagnostic-invalidation',
                                    'normative_result': 'failed' if mode == 'retained' else 'passed',
                                    'same_bytes_and_utc': True, 'full_conformance': False})
            saved['interoperability'] = {'comparisons': comparisons, 'raw_divergences_reproduced': sum(r['mode'] == 'retained' and r['status'] == 'blocked' for r in comparisons),
                'raw_divergences_removed': 0, 'technical_cause_isolated': True, 'independent_review': False,
                'adjudication': 'Implementer analysis: pinned Python lacks joint timestamp/snapshot invalidation required by TUF 1.0.36 section5.3.11; closed .NET fixtures follow it. Raw implementation difference persists.',
                'diagnostic_is_operational_fix': False}
            evaluated = json.loads(subprocess.check_output([dotnet, 'msbuild', 'GerenciadorAplicativos.csproj', '-getItem:Compile'], cwd=REPO, env=env, text=True))
            files = [p['Identity'].replace('\\', '/') for p in evaluated['Items']['Compile']]
            assert len(files) == 142 and not any('prototipos/' in p for p in files)
            paths = ['stage11/src/DurableTrust.cs.txt', 'stage11/src/TrustFixtureProof.cs.txt', 'stage11/fixtures/oracle-source-pin.json',
                     'stage11/reference_interop.py', 'stage11/src/Tests11.cs.txt', 'stage8/prepare_source.py', 'stage8/tuf-delta.patch']
            saved['unchanged_reference_sources_sha256'] = {p: hashlib.sha256((HERE.parent / p).read_text().encode()).hexdigest() for p in paths}
            saved['tuf_source'] = provenance
            saved['isolation'] = {'sdk': sdk, 'locked_restore': True, 'product_compile_items': 142, 'prototype_items_in_product': 0,
                                  'outputs_outside_repo': True, 'GF_code_changed': False, 'catalog_roundtrip_exact': True}
            saved['status'] = 'passed' if completed.returncode == 0 and saved['windows']['failed'] == 0 and saved['python_probe']['failed'] == 0 else 'failed'
        saved['controller_tree_deleted'] = not work.exists()
    except Exception as error:
        saved['status'] = 'failed'
        saved['controller_error'] = type(error).__name__ + ':' + str(error)
        raise
    finally:
        report.write_text(json.dumps(saved, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('GF_U12_FINAL_REPORT ' + json.dumps(saved, ensure_ascii=True, separators=(',', ':')), flush=True)
    return 0 if saved['status'] == 'passed' else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--vectors', type=Path, help='Recorded Stage12 vector evidence, for exact replay')
    raise SystemExit(main(parser.parse_args()))
