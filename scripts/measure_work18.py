"""Measure real selection/permit/UI gates with identical inert fixtures.

Baseline is PR59's immutable .64. This never authorizes a real payload and
never runs any third-party installer. It is not a measurement of a personal PC.
"""
import io
import json
import pathlib
import shutil
import subprocess
import tarfile
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = '28beb9607e85d208af4c0864d768a6db7fb5296a'


def run(args, cwd):
    result = subprocess.run(args, cwd=cwd, text=True, encoding='utf-8', errors='replace', capture_output=True, timeout=300)
    if result.returncode:
        raise RuntimeError(' '.join(args) + '\n' + result.stdout[-12000:] + result.stderr[-5000:])
    return result.stdout


def measure(ref, dest):
    if subprocess.run(['git', 'cat-file', '-e', ref + '^{commit}'], cwd=ROOT, capture_output=True).returncode:
        run(['git', 'fetch', '--no-tags', '--depth=1', 'origin', ref], ROOT)
    with tarfile.open(fileobj=io.BytesIO(subprocess.check_output(['git', 'archive', ref], cwd=ROOT))) as archive:
        archive.extractall(dest, filter='data')
    shutil.copyfile(ROOT / 'TestesAtualizador/Work18CoverageProbe.cs', dest / 'TestesAtualizador/Work18CoverageProbe.cs')
    main = dest / 'TestesAtualizador/Program.cs'
    source = main.read_text(encoding='utf-8-sig')
    if '--work18-coverage' not in source:
        source = source.replace('if(args.Contains("--work17-coverage"))',
            'if(args.Contains("--work18-coverage")){Work18CoverageProbe.Run();return;}\nif(args.Contains("--work17-coverage"))', 1)
        main.write_text(source, encoding='utf-8')
    output = run(['dotnet', 'run', '--project', 'TestesAtualizador/TestesAtualizador.csproj', '-c', 'Release', '--', '--work18-coverage'], dest)
    return json.loads(next(line.split('=', 1)[1] for line in output.splitlines() if line.startswith('WORK18_COVERAGE=')))


if __name__ == '__main__':
    head = run(['git', 'rev-parse', 'HEAD'], ROOT).strip()
    with tempfile.TemporaryDirectory(prefix='GF-W18-Measure-') as folder:
        before = measure(BASE, pathlib.Path(folder) / 'before')
        after = measure(head, pathlib.Path(folder) / 'after')
    assert [r['id'] for r in before] == [r['id'] for r in after]
    assert len(before) == len(after) == 28
    assert sum(r['action'] != 'none' for r in before) == 5
    assert sum(r['action'] != 'none' for r in after) == 13
    assert all(r['action'] == r['expected_action'] for r in after)
    assert all(r['action'] == 'none' for r in before if r['expected_action'] == 'none')
    additions = [r['id'] for b, r in zip(before, after) if b['action'] == 'none' and r['action'] != 'none']
    assert len(additions) == 8
    report = dict(baseline=BASE, candidate=head, sample_kind='28 inert MSI/WiX/NSIS/Inno fixtures; pre-payload eligibility only',
                  before=before, after=after, additional_scenarios=additions,
                  payloads_authorized=0, third_party_operations=0, physical_homologations=0)
    (ROOT / 'Work18Coverage.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2, ensure_ascii=False))
