"""Negative controls in disposable source copies; never mutates the checkout.
Usage: python scripts/test_work6_mutations.py /absolute/path/to/dotnet
No installer/uninstaller is launched. Windows-native cases remain in CI.
"""
import os, pathlib, shutil, subprocess, sys, tempfile
root = pathlib.Path(__file__).resolve().parents[1]
dotnet = sys.argv[1] if len(sys.argv) > 1 else 'dotnet'
mutants = [
    ('cancel-before-launch', 'UpdateLaunchGate.cs',
     '            token.ThrowIfCancellationRequested();\n            // A partir deste ponto a chamada pode ter criado um processo externo.\n            progress?.Invoke(new(UpdateStage.Installing));\n            token.ThrowIfCancellationRequested();',
     '            // MUTANT: as duas barreiras anteriores ao lancamento removidas\n            progress?.Invoke(new(UpdateStage.Installing));',
     'TestesAtualizador', '--work6-flow', 'WORK6 fluxo: cancelamento entre'),
    ('duplicate-launch', 'UpdateLaunchGate.cs',
     'if (Interlocked.Exchange(ref attempted, 1) != 0)', 'if (false)',
     'TestesAtualizador', '--work6-flow', 'tentativa concorrente rejeitada'),
    ('stale-authorized', 'UpdateResultHistory.cs',
     'RequiresReview = true, IsStale = true', 'RequiresReview = false, IsStale = true',
     'TestesMultifontes', '', 'preserva oito evid'),
]
mutants.extend([
    ('partial-uninstall-authorized','UninstallBatch.cs',
     'if(!before.Complete || matching.Length!=1', 'if(matching.Length!=1',
     'TestesResiduos','--work6-uninstall-flow','W6-U-PARTIAL'),
    ('transient-absence-confirmed','UninstallBatch.cs',
     'complete && exit==0 && absent>=3', 'complete && exit==0 && sawAbsence',
     'TestesResiduos','--work6-uninstall-flow','W6-U-STABLE'),
])
mutants.extend([
    ('pending-source-discarded-early','PendingDeployment.cs',
     'else _=ReleaseWhenDone(operation,source,released);','else Release(source,released);',
     'TestesAtualizador','--work6-flow','W6-MSIX-LEASE'),
    ('quoted-msi-regression','UninstallCommand.cs',
     '(?:""(?<product>"+GuidPattern+@")""|(?<product>"+GuidPattern+@"))',
     '(?<product>"+GuidPattern+@")',
     'TestesResiduos','--work6-uninstall-flow','W6-U-QUOTED'),
])
if os.name == 'nt':
    mutants.append(('hardlink-accepted', 'InstallerFileGuard.cs',
        ' ||\n            (!directory && info.Links != 1)', '',
        'TestesAtualizador', '--work6-flow', 'hardlink externo'))
    mutants.extend([
        ('w12-inconclusive-as-absence', 'IntegratedResidualPolicy.cs',
         'internal bool Conclusive => Complete && Problems.All(', 'internal bool Conclusive => Complete || Problems.All(',
         'TestesResiduos', '--work12', 'W12 erro 5 sem alvo elegivel'),
        ('w12-late-registry-ignored', 'IntegratedResidualCleanup.cs',
         'if (scope.Policy != "registered-install") keys.Add(address);', 'if (false) keys.Add(address);',
         'TestesResiduos', '--work12', 'W12 chave da politica criada'),
        ('w12-quiet-product-bypassed', 'SilentUninstallPolicy.cs',
         '(!string.Equals(raw, app.QuietUninstallString, StringComparison.Ordinal) || RegisteredQuietMatchesProduct(app, registered)) &&', 'true &&',
         'TestesResiduos', '--work12', 'W12 fronteira de execucao rejeita'),
        ('w12-inno-reboot-unrestrained', 'SilentUninstallPolicy.cs',
         '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '/VERYSILENT /SUPPRESSMSGBOXES',
         'TestesResiduos', '--work12', 'W12 Inno comprovado'),
    ])
    mutants.extend([
        ('residual-consent-bypassed','IntegratedResidualCleanup.cs',
         'if (!consent || !ValidPlan(plan)', 'if (!ValidPlan(plan)',
         'TestesResiduos','--work10-core','W10 NO preserves file'),
        ('residual-content-hash-bypassed','IntegratedResidualCleanup.cs',
         'Convert.ToHexString(SHA256.HashData(handle)) != file.Digest', 'false',
         'TestesResiduos','--work10-core','W10 same metadata with changed content'),
        ('residual-registry-hash-bypassed','ResidualRegistryGuard.cs',
         'digest != item.Digest || entries != item.Entries', 'false',
         'TestesResiduos','--work10-core','W10 key changed after consent'),
        ('residual-parent-identity-bypassed','IntegratedResidualCleanup.cs',
         '!parents.Identities().SequenceEqual(file.Parents) ||', 'false ||',
         'TestesResiduos','--work10-core','W10 moved identical file in replaced directory'),
        ('directory-replacement-accepted','ResidualPreview.cs',
         'accepted.DirectoryChain.SequenceEqual(current.DirectoryChain)', 'true',
         'TestesResiduos','--work6-preview','W6-R-ROOT'),
        ('duplicate-preview-state-accepted','ResidualPreview.cs',
         ' ||\n            accepted.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != accepted.Files.Count ||\n            !accepted.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(accepted.Candidate.Files)', '',
         'TestesResiduos','--work6-preview','W6-R-DUP'),
    ])
mutants.extend([
    ('w11-truncated-download-accepted', 'UpdateOperationProgress.cs',
     'if (bytes < minimum || expected is long total && total != bytes)', 'if (bytes < minimum)',
     'TestesAtualizador', '--work11', 'W11 EOF antecipado'),
    ('w11-pending-retried', 'UpdateOperationProgress.cs',
     '(snapshot.Active || snapshot.Progress.Stage == UpdateStage.Pending ||', '(snapshot.Active ||',
     'TestesAtualizador', '--work11', 'W11 cancelamento depois de lancar'),
    ('w11-uncertain-success', 'UpdateOperationProgress.cs',
     '_ => UpdateStage.Pending\n    }, result.ExitCode', '_ => UpdateStage.Confirmed\n    }, result.ExitCode',
     'TestesAtualizador', '--work11', 'W11 retorno zero sem versao'),
])
mutants.extend([
    ('w13-final-cancel-bypassed', 'UpdateLaunchGate.cs',
     '            token.ThrowIfCancellationRequested();\n            launched = true;',
     '            launched = true;',
     'TestesAtualizador', '--work13', 'W13 cancelamento no ultimo callback'),
    ('w13-msix-publisher-prefix', 'WindowsTerminalUpdater.cs',
     'publisher != "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"',
     '!publisher.StartsWith("CN=Microsoft Corporation", StringComparison.Ordinal)',
     'TestesAtualizador', '--work13', 'W13 MSIX publisher com prefixo'),
])
if os.name == 'nt':
    mutants.append(('w13-vc-dll-bypassed', 'UpdateInstallationContract.cs',
        'version >= target && BinaryMatches(VcBinary(x64), recipe.Version, x64, exact: false, sameFamily: true)',
        'version >= target',
        'TestesAtualizador', '--work13', 'W13 GUID novo com DLL antiga'))
mutants.extend([
    ('auto-copy-authorized','AutomaticUpdateDiscovery.cs',
     'ReferenceEquals(recipe,permit.Recipe)', 'recipe==permit.Recipe',
     'TestesAtualizador','--automatic','AUTO copia ou adulteracao'),
    ('auto-forged-scan-authorized','AutomaticUpdateDiscovery.cs',
     'if(!IssuedScans.TryGetValue(scan,out var issued) || issued.Expires<=DateTimeOffset.UtcNow)return;',
     'if(!IssuedScans.TryGetValue(scan,out var issued))issued=new(scan.Plans.ToArray(),DateTimeOffset.UtcNow.AddMinutes(2));',
     'TestesAtualizador','--automatic','AUTO dados ou catalogo forjado'),
])
if os.name == 'nt':
    mutants.append(('auto-architecture-change-ignored','AutomaticAppInstallation.cs',
        'VerifiedUpdaters.GetPortableExecutableMachine(Binary)!=(Architecture=="x64"?0x8664:0x14c) ||', '',
        'TestesAtualizador','--automatic','AUTO arquitetura alterada depois'))
files = subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
with tempfile.TemporaryDirectory(prefix='GF-W6-Mutants-') as temporary:
    target=pathlib.Path(temporary)
    for name in files:
        path=pathlib.Path(name)
        if not name or path.suffix not in ('.cs','.csproj','.props','.json'): continue
        if len(path.parts)>1 and path.parts[0] not in ('TestesAtualizador','TestesMultifontes','TestesResiduos'): continue
        (target/path).parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(root/path,target/path)
    def run(project, argument):
        return subprocess.run([dotnet,'run','--project',project,'-p:EnableWindowsTargeting=true','-p:UseSharedCompilation=false','--',argument],
            cwd=target, text=True, encoding="utf-8", errors="replace", stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=150)
    for project,argument in [('TestesAtualizador','--work6-flow'),('TestesMultifontes',''),('TestesResiduos','--work6-uninstall-flow'),('TestesAtualizador','--work11'),('TestesAtualizador','--work13'),('TestesAtualizador','--automatic')]:
        baseline=run(project,argument)
        if baseline.returncode: raise RuntimeError('baseline failed: '+baseline.stdout[-2000:])
        print('BASELINE PASS',project,flush=True)
    if os.name == 'nt':
        baseline=run('TestesResiduos','--work10-core')
        if baseline.returncode: raise RuntimeError('WORK10 baseline failed: '+baseline.stdout[-3000:])
        print('BASELINE PASS native integrated cleanup',flush=True)
        baseline=run('TestesResiduos','--work12')
        if baseline.returncode: raise RuntimeError('WORK12 baseline failed: '+baseline.stdout[-3000:])
        print('BASELINE PASS real failure reproductions WORK12',flush=True)
        baseline=run('TestesResiduos','--work6-preview')
        if baseline.returncode: raise RuntimeError('preview baseline failed: '+baseline.stdout[-2000:])
        print('BASELINE PASS native preview',flush=True)
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
print(f'{len(mutants)}/{len(mutants)} nontrivial mutations rejected; disposable sources removed')
