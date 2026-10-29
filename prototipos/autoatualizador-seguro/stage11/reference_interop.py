"""Independent python-tuf oracle. Own scenarios and curated upstream vectors are distinguished."""
import copy
from datetime import datetime,timezone
import hashlib
import importlib.metadata
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.parse import urlsplit
from tuf.ngclient import Updater
from tuf.ngclient.fetcher import FetcherInterface
from tuf.api import exceptions


class InertFetcher(FetcherInterface):
    def __init__(self, files): self.files=files;self.requests=[]
    def _fetch(self,url):
        assert urlsplit(url).hostname=='gf-u11-fixture.invalid'
        name=urlsplit(url).path.rsplit('/',1)[-1];self.requests.append(name)
        if name not in self.files: raise exceptions.DownloadHTTPError('inert missing fixture',404)
        yield self.files[name]


def run(fixtures,out):
    versions={p:importlib.metadata.version(p) for p in ('tuf','securesystemslib','cryptography','urllib3')}
    assert versions=={'tuf':'7.0.1','securesystemslib':'1.3.1','cryptography':'50.0.2','urllib3':'2.8.0'}
    rows=[]
    def attempt(folder,name,bootstrap):
        files={p.name:p.read_bytes() for p in (fixtures/name).iterdir()}
        fetch=InertFetcher(files)
        u=Updater(str(folder),'https://gf-u11-fixture.invalid/',target_dir=str(folder/'targets'),target_base_url='https://gf-u11-fixture.invalid/',fetcher=fetch,bootstrap=bootstrap)
        try:
            u.refresh()
            info=u.get_targetinfo('fixture-package.zip' if name!='external' else 'file1.txt')
            assert info is not None
            u.download_target(info)
            status='accepted';error=None
        except (exceptions.RepositoryError,exceptions.DownloadError) as e: status='rejected';error=type(e).__name__
        ts=u._trusted_set
        return {'status':status,'error':error,'root':ts.root.version,'timestamp':ts.timestamp.version if 'timestamp' in ts else 0,'snapshot':ts.snapshot.version if 'snapshot' in ts else 0,
                'targets':ts.targets.version if 'targets' in ts else 0,'requests':fetch.requests}
    scenarios=['release2','root-insufficient','root-old-only','root-unauthorized','expired-final-root','expired-intermediate','root-gap','bad-reference','expired-timestamp']
    for name in scenarios:
        with TemporaryDirectory(prefix='gf-u11-python-') as d:
            folder=Path(d);anchor=(fixtures/'anchor.json').read_bytes()
            rows.append({'id':'U11-REF-'+name,'scenario':name,'kind':'own_native_python_tuf_vectors','oracle':attempt(folder,name,anchor)})
    for name in ('rotate-timestamp','rotate-snapshot','rotate-targets-high','rotate-targets-low','revoked-timestamp'):
        with TemporaryDirectory(prefix='gf-u11-python-') as d:
            folder=Path(d);baseline=attempt(folder,'high',(fixtures/'anchor.json').read_bytes());assert baseline['status']=='accepted'
            rows.append({'id':'U11-REF-'+name,'scenario':name,'kind':'own_cross_attempt_rotation_vectors','oracle':attempt(folder,name,None)})
    with TemporaryDirectory(prefix='gf-u11-external-') as d:
        rows.append({'id':'U11-REF-EXTERNAL','scenario':'external','kind':'unmodified_curated_upstream_bytes','oracle':attempt(Path(d),'external',(fixtures/'external/root.json').read_bytes())})
    result={'schema':'gf-stage11-reference-oracle/1','versions':versions,'results':rows,
            'source_commit':'49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15','internal_api_used':'TrustedMetadataSet version inspection only; Updater refresh/download used unchanged',
            'full_conformance_suite':False,'network_requests_to_real_service':0}
    out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');return result
