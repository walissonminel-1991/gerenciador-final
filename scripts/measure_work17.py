"""Compare identical inert installation fixtures to immutable WORK16.

Cached MSI metadata and Authenticode signer are synthetic; no payload is run.
This measures pre-payload eligibility, never final authorization or physical PCs.
"""
import io
import json
import pathlib
import shutil
import subprocess
import tarfile
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = '680ba3134b5191628ebc9ed83f163fa33b85dda4'


def run(args, cwd):
    result = subprocess.run(args, cwd=cwd, text=True, capture_output=True, timeout=300)
    if result.returncode:
        raise RuntimeError(' '.join(args) + '\n' + result.stdout[-12000:] + result.stderr[-5000:])
    return result.stdout


def measure(ref, dest):
    if subprocess.run(['git', 'cat-file', '-e', ref + '^{commit}'], cwd=ROOT, capture_output=True).returncode:
        run(['git', 'fetch', '--no-tags', '--depth=1', 'origin', ref], ROOT)
    with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git', 'archive', ref], cwd=ROOT))) as archive:
        archive.extractall(dest, filter='data')
    shutil.copyfile(ROOT / 'TestesAtualizador/Work17CoverageProbe.cs', dest / 'TestesAtualizador/Work17CoverageProbe.cs')
    main = dest / 'TestesAtualizador/Program.cs'
    source = main.read_text(encoding='utf-8-sig')
    if '--work17-coverage' not in source:
        lines = source.splitlines(keepends=True)
        index = next(i for i, line in enumerate(lines) if line.strip() and not line.startswith('using '))
        lines.insert(index, 'if(args.Contains("--work17-coverage")){Work17CoverageProbe.Run();return;}\n')
        main.write_text(''.join(lines), encoding='utf-8')
    output = run(['dotnet', 'run', '--project', 'TestesAtualizador/TestesAtualizador.csproj', '-c', 'Release', '--', '--work17-coverage'], dest)
    return json.loads(next(line.split('=', 1)[1] for line in output.splitlines() if line.startswith('WORK17_IDENTITY=')))


if __name__ == '__main__':
    head = run(['git', 'rev-parse', 'HEAD'], ROOT).strip()
    with tempfile.TemporaryDirectory(prefix='GF-W17-Measure-') as folder:
        before = measure(BASE, pathlib.Path(folder) / 'before')
        after = measure(head, pathlib.Path(folder) / 'after')
    assert [r['id'] for r in before] == [r['id'] for r in after]
    assert len(before) == len(after) == 22
    assert sum(r['eligible_before_payload'] for r in before) == 3
    assert sum(r['eligible_before_payload'] for r in after) == 6
    assert all(r['eligible_before_payload'] == r['expected_eligible'] for r in after)
    assert all(not r['eligible_before_payload'] for r in before if not r['expected_eligible'])
    report = dict(baseline=BASE, candidate=head, sample_kind='inert MSI/NSIS fixtures; MSI cache and signer simulated',
                  before=before, after=after, payloads_authorized=0, third_party_operations=0, physical_homologations=0)
    (ROOT / 'Work17Coverage.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
