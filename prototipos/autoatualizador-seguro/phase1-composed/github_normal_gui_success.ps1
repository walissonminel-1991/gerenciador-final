# Normal startup positive path, disposable Windows only. No L18, retry or READY.
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:GITHUB_REPOSITORY -ne 'walissonminel-1991/gerenciador-final') {
  throw 'GF_NORMAL_SUCCESS_DISPOSABLE_ONLY'
}
$root=$env:RUNNER_TEMP
$old=Join-Path $root 'gf-e2e-old-preserved'
$copy=Join-Path $root 'gf-e2e-github-normal-old'
$download=Join-Path $root 'gf-e2e-github-normal-download'
$extracted=Join-Path $root 'gf-e2e-github-normal-extracted'
$shown=Join-Path $root 'gf-e2e-github-normal-startup-shown.json'
$receipt=Join-Path $root 'gf-e2e-github-normal-acquire-report.json'
$failure=Join-Path $root 'gf-e2e-github-normal-failure.json'
$ui=Join-Path $root 'gf-e2e-github-normal-ui-observed.json'
$canary=Join-Path $root 'gf-e2e-install-data\PERSONAL-KEEP-UNCHANGED.txt'
$fixture=(Get-Content (Join-Path $root 'gf-e2e-real-flow-report.json') -Raw | ConvertFrom-Json).root
$selector=Join-Path $fixture 'l18-real-active.fixture.json'
if (!(Test-Path $old) -or !(Test-Path $canary) -or !(Test-Path $selector) -or
    @($copy,$download,$extracted,$shown,$receipt,$failure,$ui |
      Where-Object { Test-Path $_ }).Count -ne 0) {
  throw 'GF_NORMAL_SUCCESS_MUST_BE_FRESH'
}
$canaryHash=(Get-FileHash $canary -Algorithm SHA256).Hash
$selectorHash=(Get-FileHash $selector -Algorithm SHA256).Hash
$copyAndAudit=@'
import hashlib,json,os,shutil
from pathlib import Path
r=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
old=r/"gf-e2e-old-preserved"
copy=r/"gf-e2e-github-normal-old"
baseline=json.loads((r/"gf-e2e-old-prestart-files.json").read_bytes())
def digest(path):
    result={}
    for directory,dirs,files in os.walk(path,followlinks=False):
        for name in dirs+files:
            item=Path(directory)/name
            assert not item.is_symlink() and not(item.stat().st_file_attributes&0x400)
        for name in files:
            p=Path(directory)/name
            result[p.relative_to(path).as_posix()]={"length":p.stat().st_size,
                   "sha256":hashlib.sha256(p.read_bytes()).hexdigest()}
    return result
assert not copy.exists() and digest(old)==baseline and len(baseline)==284
shutil.copytree(old,copy,symlinks=True)
assert digest(copy)==baseline
print("GF_E2E_GITHUB_NORMAL_OLD_COPY_PASS files=284")
'@
$copyAndAudit | python -
if ($LASTEXITCODE -ne 0) { throw 'GF_NORMAL_SUCCESS_COPY_INTEGRITY_FAILURE' }
$env:GF_E2E_HANDOFF_L18='0'
$env:GF_E2E_SELF_GRACEFUL_CLOSE='0'
$env:GF_E2E_SELECTED_NEW_CONTINUATION='0'
$env:GF_E2E_LIVE_INSTALL_SWAP='0'
$env:GF_E2E_SWAP_CUT_AFTER_OLD_MOVE='0'
python -u 'prototipos/autoatualizador-seguro/phase1-composed/github_normal_gui_success_observer.py'
if ($LASTEXITCODE -ne 0) { throw 'GF_NORMAL_SUCCESS_REAL_GUI_NOT_HEALTHY' }
$verify=@'
import hashlib,json,os
from pathlib import Path
r=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
baseline=json.loads((r/"gf-e2e-old-prestart-files.json").read_bytes())
def digest(path):
    found={}
    for directory,dirs,files in os.walk(path,followlinks=False):
        for name in dirs+files:
            item=Path(directory)/name
            assert not item.is_symlink() and not(item.stat().st_file_attributes&0x400)
        for name in files:
            p=Path(directory)/name
            found[p.relative_to(path).as_posix()]={"length":p.stat().st_size,
                 "sha256":hashlib.sha256(p.read_bytes()).hexdigest()}
    return found
assert digest(r/"gf-e2e-old-preserved")==baseline
assert digest(r/"gf-e2e-github-normal-old")==baseline
assert len(baseline)==284
dl=r/"gf-e2e-github-normal-download"
assert len(list(dl.iterdir()))==5
for name,size,sha in (
 ("gf-e2e-new-win-x64.zip",57695630,"3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e"),
 ("GF-LAB-REAL-PACKAGE-MANIFEST.json",72618,"a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d")):
    item=dl/name
    assert item.is_file() and item.stat().st_size==size
    assert hashlib.sha256(item.read_bytes()).hexdigest()==sha
ui=json.loads((r/"gf-e2e-github-normal-ui-observed.json").read_bytes())
assert ui["schema"]=="gf-e2e-github-normal-mainform-healthy/1"
for key in ("normal_open_no_tray","no_showwindow","visible_during_download",
 "original_mainform_shown","draft_download_completed","old_alive_after_download",
 "image_verified","mainform_visible","message_pump_responsive",
 "inventory_nonzero","search_empty_and_restored","consent_unchanged","test_only"):
    assert ui[key] is True,key
for key in ("new_executed","l18_handoff","retry","rollback","cleanup","operational_ready"):
    assert ui[key] is False,key
assert (r/"gf-e2e-install-data"/"PERSONAL-KEEP-UNCHANGED.txt").read_bytes()==b"DO-NOT-TOUCH-MY-DATA\n"
assert not (r/"gf-e2e-old").exists()
assert not (r/"gf-e2e-live-install-transition.json").exists()
fixture=Path(json.loads((r/"gf-e2e-real-flow-report.json").read_bytes())["root"])
selector=json.loads((fixture/"l18-real-active.fixture.json").read_bytes())
assert selector["selected"]=="new" and selector["generation"]==2
assert not (fixture/"real-observed-new.fixture.json").exists()
print("GF_E2E_GITHUB_NORMAL_BYTES_PASS pinned_zip=true pinned_manifest=true "
      "old_files=284 canary_unchanged=true NEW_not_executed=true READY=false")
'@
$verify | python -
if ($LASTEXITCODE -ne 0) { throw 'GF_NORMAL_SUCCESS_PINNED_BYTES_NOT_VERIFIED' }
python 'prototipos/autoatualizador-seguro/phase1-composed/github_draft_package.py' verify --repo . --old $copy --downloaded $download --extracted $extracted --lab-old-variant
if ($LASTEXITCODE -ne 0) { throw 'GF_NORMAL_SUCCESS_EXISTING_TUF_NOT_VERIFIED' }
if ((Get-FileHash $selector -Algorithm SHA256).Hash -ne $selectorHash -or
    (Get-FileHash $canary -Algorithm SHA256).Hash -ne $canaryHash) {
  throw 'GF_NORMAL_SUCCESS_SELECTOR_OR_CANARY_MUTATED'
}
Write-Host 'GF_E2E_GITHUB_NORMAL_MAINFORM_PASS complete_remote_draft=true TUF_verified=true normal_UI_healthy=true L18_not_invoked=true READY=false'
Add-Content $env:GITHUB_STEP_SUMMARY 'GF OLD 5.13.11.65 normal native MainForm.Shown downloaded authentic private Draft ZIP/manifest via same .NET HttpClient. Real UI stayed visible before completion and responsive with inventory/search after. Independent ZIP/manifest SHA-256 and existing Python TUF verified 284 extracted files. OLD 284-file tree, canary and L18 selector unchanged; no NEW execution, handoff, retry, rollback, cleanup or READY. BETA LOCAL Windows runner only, no consent override or production release.'
