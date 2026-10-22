"""Negative controls in disposable source copies; never mutates the checkout.
Usage: python scripts/test_work6_mutations.py /absolute/path/to/dotnet
No installer/uninstaller is launched. Windows-native cases remain in CI.
"""
import pathlib, shutil, subprocess, sys, tempfile
root = pathlib.Path(__file__).resolve().parents[1]
dotnet = sys.argv[1] if len(sys.argv) > 1 else 'dotnet'
mutants = [
    ('cancel-before-launch', 'UpdateLaunchGate.cs',
     '            token.ThrowIfCancellationRequested();\n            // A partir',
     '            // MUTANT: cancellation removed\n            // A partir',
     'TestesAtualizador', '--work6-flow', 'cancelamento entre revalidação'),
    ('duplicate-launch', 'UpdateLaunchGate.cs',
     'if (Interlocked.Exchange(ref attempted, 1) != 0)', 'if (false)',
     'TestesAtualizador', '--work6-flow', 'tentativa concorrente rejeitada'),
    ('stale-authorized', 'UpdateResultHistory.cs',
     'RequiresReview = true, IsStale = true', 'RequiresReview = false, IsStale = true',
     'TestesMultifontes', '', 'preserva oito evidências antigas'),
]
files = subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
with tempfile.TemporaryDirectory(prefix='GF-W6-Mutants-') as temporary:
    target=pathlib.Path(temporary)
    for name in files:
        path=pathlib.Path(name)
        if not name or path.suffix not in ('.cs','.csproj','.props','.json'): continue
        if len(path.parts)>1 and path.parts[0] not in ('TestesAtualizador','TestesMultifontes'): continue
        (target/path).parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(root/path,target/path)
    def run(project, argument):
        return subprocess.run([dotnet,'run','--project',project,'-p:EnableWindowsTargeting=true','--',argument],
            cwd=target, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=150)
    for project,argument in [('TestesAtualizador','--work6-flow'),('TestesMultifontes','')]:
        baseline=run(project,argument)
        if baseline.returncode: raise RuntimeError('baseline failed: '+baseline.stdout[-2000:])
        print('BASELINE PASS',project,flush=True)
    for name,filename,old,new,project,argument,expected in mutants:
        path=target/filename; original=path.read_text()
        if original.count(old)!=1: raise RuntimeError('mutation target not unique: '+name)
        try:
            path.write_text(original.replace(old,new))
            result=run(project,argument)
            # A compiler error or unrelated crash is not a killed mutation.
            if result.returncode==0 or expected not in result.stdout or 'error CS' in result.stdout:
                raise RuntimeError('survived/invalid mutation '+name+': '+result.stdout[-2000:])
            print('KILLED',name,'by',expected,flush=True)
        finally: path.write_text(original)
print('3/3 nontrivial mutations rejected; disposable sources removed')
