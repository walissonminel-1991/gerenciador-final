"""Only new Stage11 trust/rotation/recovery/interop deltas. No GF build or old-suite rerun."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
REPO=HERE.parents[2]
ENTRY='2cf5a8fb8addb94da4050667dc8af16d71e413b1'


def module(path,name):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main(args):
    report=args.report.resolve();report.parent.mkdir(parents=True,exist_ok=True)
    saved={'schema':'gf-stage11-controller/1','contract':'gf-self-update/1','entry_head':ENTRY,'status':'not-executed','product_built':False,'product_executed':False,
           'old_suites_reexecuted':False,'physical_tests':0,'operational_G2_closed':False,'artifacts_uploaded':0,'new_production_keys':False}
    try:
        if os.name!='nt': saved.update(status='blocked',reason='Disposable Windows/NTFS required');return 2
        dotnet=shutil.which('dotnet');assert dotnet
        env={**os.environ,'DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_SKIP_FIRST_TIME_EXPERIENCE':'1','DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','PYTHONUTF8':'1'}
        sdk=subprocess.check_output([dotnet,'--version'],cwd=REPO,env=env,text=True).strip();assert sdk=='10.0.402'
        changed=subprocess.check_output(['git','diff','--name-only',ENTRY,'HEAD'],cwd=REPO,text=True).splitlines()
        allowed=('prototipos/autoatualizador-seguro/stage11/','.github/workflows/autoatualizador-etapa11-isolado.yml','docs/WORK-AUTOATUALIZADOR-ETAPA11.md','docs/evidencias/autoatualizador-etapa11','docs/WORK-AUTOATUALIZADOR-CHECKPOINT.md','docs/PROGRESSO.json')
        assert all(p.startswith(allowed) for p in changed),changed
        with tempfile.TemporaryDirectory(prefix='gf-u11-controller-') as d:
            work=Path(d).resolve();assert not work.is_relative_to(REPO)
            source,provenance=module(HERE.parent/'stage8/prepare_source.py','u11_source').prepare(work)
            catalog=module(HERE/'make_fixtures.py','u11_vectors').generate(work/'fixtures')
            common=['-p:ArtifactsPath='+str(work/'artifacts'),'-p:Stage8Source='+str(source)]
            subprocess.run([dotnet,'restore',str(HERE/'Stage11.csproj'),*common,'--locked-mode','--nologo'],cwd=REPO,env=env,check=True)
            subprocess.run([dotnet,'build',str(HERE/'Stage11.csproj'),*common,'-c','Release','--no-restore','--nologo'],cwd=REPO,env=env,check=True)
            assemblies=list((work/'artifacts/bin').rglob('GF.Updater.Stage11.Tests.dll'));assert len(assemblies)==1
            output=work/'windows.json'
            completed=subprocess.run([dotnet,str(assemblies[0]),str(work/'fixtures'),str(output),args.cases],cwd=REPO,env=env)
            if output.is_file(): saved['windows']=json.loads(output.read_text())
            else: raise RuntimeError('No complete Windows report; no test approval inferred')
            saved['tuf_source']=provenance;saved['fixtures']=catalog
            # Independent oracle is version/source pinned; no production packages are changed.
            oracle_result=None
            try:
                import tuf
                package_root=Path(tuf.__file__).resolve().parent.parent
                pin=json.loads((HERE/'fixtures/oracle-source-pin.json').read_text())
                for path,expected in pin['files'].items(): assert hashlib.sha256((package_root/path).read_bytes()).hexdigest()==expected,path
                oracle_result=module(HERE/'reference_interop.py','u11_reference').run(work/'fixtures',work/'oracle.json')
                saved['reference_oracle']=oracle_result
                own={r['scenario']:r for r in saved['windows']['interop']}
                rows=[]
                for row in oracle_result['results']:
                    name=row['scenario']
                    if name not in own: continue
                    same=own[name]['status']==row['oracle']['status']
                    rows.append({'scenario':name,'kind':row['kind'],'dotnet_status':own[name]['status'],'python_status':row['oracle']['status'],
                                 'status':'passed' if same else 'blocked','limitation':None if same else 'Cross-implementation divergence: investigate independently; not conformance approval'})
                saved['interoperability_comparison']={'total':len(rows),'passed':sum(r['status']=='passed' for r in rows),'blocked':sum(r['status']=='blocked' for r in rows),'results':rows,'full_conformance':False}
            except Exception as error:
                saved['reference_oracle']={'status':'blocked','error':type(error).__name__+':'+str(error),'full_conformance':False}
            evaluated=json.loads(subprocess.check_output([dotnet,'msbuild','GerenciadorAplicativos.csproj','-getItem:Compile'],cwd=REPO,env=env,text=True))
            files=[p['Identity'].replace('\\','/') for p in evaluated['Items']['Compile']];assert len(files)==142 and not any('prototipos/' in p for p in files)
            shared=['src/StrictMetadata.cs.txt','src/PersistentState.cs.txt','src/TransactionPrototype.cs.txt','src/WindowsFileGuard.cs.txt','src/CycleClock.cs.txt','stage8/src/OwnedAttempt.cs.txt','stage8/tuf-delta.patch','stage8/prepare_source.py','stage9/src/IntegratedPipeline.cs.txt','stage9/src/OperationJournal.cs.txt','stage10/src/LabPackage.cs.txt']
            saved['unchanged_reference_sources_sha256']={p:hashlib.sha256((HERE.parent/p).read_text().encode()).hexdigest() for p in shared}
            saved['isolation']={'sdk':sdk,'locked_restore':True,'product_compile_items':len(files),'prototype_items_in_product':0,'outputs_outside_repo':True,'legacy_product_code_changed':False}
            saved['status']='passed' if completed.returncode==0 and saved['windows']['failed']==0 else 'failed'
        saved['controller_tree_deleted']=not work.exists()
    except Exception as error:
        saved['status']='failed';saved['controller_error']=type(error).__name__+':'+str(error)
        raise
    finally:
        report.write_text(json.dumps(saved,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('GF_U11_FINAL_REPORT '+json.dumps(saved,ensure_ascii=True,separators=(',',':')),flush=True)
    return 0 if saved['status']=='passed' else 1


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--report',required=True,type=Path);p.add_argument('--cases',default='');raise SystemExit(main(p.parse_args()))
