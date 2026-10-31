# Normal startup success, optional existing L18 focused containment; no READY.
param([switch]$L18,[switch]$DenyClose)
$ErrorActionPreference='Stop'
if ($DenyClose -and !$L18) { throw 'GF_L18_DENY_CLOSE_REQUIRES_L18' }
$env:GF_E2E_TEST_L18=if ($L18) { '1' } else { '0' }
if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:GITHUB_REPOSITORY -ne 'walissonminel-1991/gerenciador-final') {
  throw 'GF_NORMAL_SUCCESS_DISPOSABLE_ONLY'
}
$root=$env:RUNNER_TEMP
$old=Join-Path $root 'gf-e2e-old-preserved'
$prefix=if ($L18) { 'gf-e2e-github-normal-l18' } else { 'gf-e2e-github-normal' }
$copy=Join-Path $root ($prefix+'-old')
$download=Join-Path $root ($prefix+'-download')
$extracted=Join-Path $root ($prefix+'-extracted')
$shown=Join-Path $root ($prefix+'-startup-shown.json')
$receipt=Join-Path $root ($prefix+'-acquire-report.json')
$failure=Join-Path $root ($prefix+'-failure.json')
$ui=Join-Path $root ($prefix+'-ui-observed.json')
$l18Flow=Join-Path $root 'gf-e2e-github-normal-l18-flow-report.json'
$l18Handoff=Join-Path $root 'gf-e2e-github-normal-l18-handoff-report.json'
$closeDenied=Join-Path $root 'gf-e2e-github-normal-l18-close-denied.json'
if ($DenyClose -and (Test-Path $closeDenied)) { throw 'GF_L18_DENIAL_NOT_FRESH' }
if ($L18 -and !(Test-Path $env:GF_E2E_L18_EXE)) {
  throw 'GF_NORMAL_L18_EXISTING_COORDINATOR_MISSING'
}
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
copy=r/("gf-e2e-github-normal-l18-old" if os.environ.get("GF_E2E_TEST_L18")=="1" else "gf-e2e-github-normal-old")
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
$env:GF_E2E_HANDOFF_L18=if ($L18) { '1' } else { '0' }
$env:GF_E2E_SELF_GRACEFUL_CLOSE=if ($DenyClose) { '1' } else { '0' }
$env:GF_E2E_GUI_NORMAL_L18_DENY_CLOSE=if ($DenyClose) { '1' } else { '0' }
$env:GF_E2E_SELECTED_NEW_CONTINUATION='0'
$env:GF_E2E_LIVE_INSTALL_SWAP='0'
$env:GF_E2E_SWAP_CUT_AFTER_OLD_MOVE='0'
$observerArgs=@('prototipos/autoatualizador-seguro/phase1-composed/github_normal_gui_success_observer.py')
if ($L18) { $observerArgs+='--l18' }
if ($DenyClose) { $observerArgs+='--deny-close' }
python -u @observerArgs
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
assert digest(r/(("gf-e2e-github-normal-l18" if os.environ.get("GF_E2E_TEST_L18")=="1" else "gf-e2e-github-normal")+"-old"))==baseline
assert len(baseline)==284
dl=r/(("gf-e2e-github-normal-l18" if os.environ.get("GF_E2E_TEST_L18")=="1" else "gf-e2e-github-normal")+"-download")
assert len(list(dl.iterdir()))==5
for name,size,sha in (
 ("gf-e2e-new-win-x64.zip",57695630,"3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e"),
 ("GF-LAB-REAL-PACKAGE-MANIFEST.json",72618,"a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d")):
    item=dl/name
    assert item.is_file() and item.stat().st_size==size
    assert hashlib.sha256(item.read_bytes()).hexdigest()==sha
l18=os.environ.get("GF_E2E_TEST_L18")=="1"
ui=json.loads((r/(("gf-e2e-github-normal-l18" if l18 else "gf-e2e-github-normal")+"-ui-observed.json")).read_bytes())
assert ui["schema"]==("gf-e2e-github-normal-l18-mainform/1" if l18 else "gf-e2e-github-normal-mainform-healthy/1")
for key in ("normal_open_no_tray","no_showwindow","visible_during_download",
 "original_mainform_shown","draft_download_completed","old_alive_after_download",
 "image_verified","mainform_visible","message_pump_responsive",
 "inventory_nonzero","search_empty_and_restored","consent_unchanged","test_only"):
    assert ui[key] is True,key
for key in ("new_executed","retry","rollback","cleanup","operational_ready"):
    assert ui[key] is False,key
assert ui["l18_handoff"] is l18
deny=os.environ.get("GF_E2E_GUI_NORMAL_L18_DENY_CLOSE")=="1"
assert ui["unknown_close_requested_and_denied"] is deny
if deny:
    denied=json.loads((r/"gf-e2e-github-normal-l18-close-denied.json").read_bytes())
    assert denied["outcome"]=="UnknownBlocked" and denied["explicitly_requested"] is True
    assert denied["close_callback_invoked"] is False and denied["operational_ready"] is False
if l18:
    flow=json.loads((r/"gf-e2e-github-normal-l18-flow-report.json").read_bytes())
    handoff=json.loads((r/"gf-e2e-github-normal-l18-handoff-report.json").read_bytes())
    assert flow["schema"]=="gf-real-single-normal-lab/1"
    assert flow["passed"]==1 and flow["failed"]==0
    assert flow["result"]["outcome"]=="UnknownBlocked"
    assert flow["result"]["replay"]=="UnknownBlocked"
    assert flow["operational_ready"] is False
    assert flow["recovery_authorized"] is False
    assert handoff["same_l18_coordinator_invoked_by_old"] is True
    assert handoff["tuf_child_verified_download"] is True
    assert handoff["operational_ready"] is False
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
if ($L18) {
  $flow=(Get-Content $l18Flow -Raw | ConvertFrom-Json)
  $handoff=(Get-Content $l18Handoff -Raw | ConvertFrom-Json)
  if ($flow.schema -ne 'gf-real-single-normal-lab/1' -or
      $flow.result.outcome -ne 'UnknownBlocked' -or $flow.result.replay -ne 'UnknownBlocked' -or
      $flow.operational_ready -or $flow.recovery_authorized -or
      !$handoff.same_l18_coordinator_invoked_by_old -or
      !$handoff.tuf_child_verified_download -or
      $handoff.operational_ready -or $handoff.automatic_recovery_authorized) {
    throw 'GF_NORMAL_L18_SAME_COORDINATOR_NOT_PROVEN'
  }
  if ($DenyClose) {
    $denied=(Get-Content $closeDenied -Raw | ConvertFrom-Json)
    if (!$denied.explicitly_requested -or $denied.outcome -ne 'UnknownBlocked' -or
        $denied.close_callback_invoked -or $denied.operational_ready) {
      throw 'GF_L18_DENY_CLOSE_RECEIPT_INVALID'
    }
    Write-Host 'GF_E2E_GITHUB_NORMAL_L18_DENY_CLOSE_PASS requested=true outcome=UnknownBlocked close_denied=true old_healthy_after_denial=true NEW_not_installed=true READY=false'
  } else {
    Write-Host 'GF_E2E_GITHUB_NORMAL_L18_PASS normal_ui=true authenticated_zip=true existing_l18=true outcome=UnknownBlocked old_open=true rollback=false cleanup=false READY=false'
  }
} else {
  Write-Host 'GF_E2E_GITHUB_NORMAL_MAINFORM_PASS complete_remote_draft=true TUF_verified=true normal_UI_healthy=true L18_not_invoked=true READY=false'
}
Add-Content $env:GITHUB_STEP_SUMMARY 'GF OLD 5.13.11.65 normal native MainForm.Shown downloaded authentic private Draft ZIP/manifest via same .NET HttpClient. Real UI stayed visible before completion and responsive with inventory/search after. Independent ZIP/manifest SHA-256 and existing Python TUF verified 284 extracted files. OLD 284-file tree, canary and L18 selector unchanged; no NEW execution, handoff, retry, rollback, cleanup or READY. BETA LOCAL Windows runner only, no consent override or production release.'
