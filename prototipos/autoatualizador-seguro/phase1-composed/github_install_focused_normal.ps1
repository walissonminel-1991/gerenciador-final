# ONE independent positive scenario: normal GF OLD -> original L18 install-focused.
# Only windows-2022 disposable runner. No rollback/retry/cleanup/READY.
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:GITHUB_REPOSITORY -ne 'walissonminel-1991/gerenciador-final') {
  throw 'GF_INSTALL_FOCUSED_DISPOSABLE_ONLY'
}
$root=$env:RUNNER_TEMP
$old=Join-Path $root 'gf-e2e-old'
$private=Join-Path $root 'gf-e2e-install-data'
$baseline=Join-Path $root 'gf-e2e-install-focused-old-baseline.json'
$report=Join-Path $root 'gf-e2e-real-flow-report.json'
$failure=Join-Path $root 'gf-e2e-old-acquire-failure.json'
$selected=Join-Path $root 'gf-e2e-selected-new-ready.json'
$closed=Join-Path $root 'gf-e2e-old-ui-closed.json'
$loop=Join-Path $root 'gf-e2e-old-message-loop-exited.json'
$window=Join-Path $root 'gf-e2e-install-focused-normal-window.json'
if (@($old,$private,$baseline,$report,$failure,$selected,$closed,$loop,$window |
  Where-Object { Test-Path $_ }).Count -ne 0) { throw 'GF_INSTALL_FOCUSED_DIR_NOT_FRESH' }
dotnet publish GerenciadorAplicativos.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:GfBetaProfile=local -p:GfE2eLab=true -p:Version=5.13.11.65 -o $old --nologo
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_PUBLISH_FAILED' }
$version=[System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $old 'GerenciadorFinal.dll')).Version.ToString()
if ($version -ne '5.13.11.65') { throw 'GF_INSTALL_FOCUSED_OLD_VERSION_WRONG' }
$sourceWork=Join-Path $root 'gf-e2e-pinned-source'
$fixtures=Join-Path $root 'gf-e2e-signed-fixtures'
$outputs=Join-Path $root 'gf-e2e-csharp-artifacts'
$project='prototipos/autoatualizador-seguro/phase1-composed/Phase1RealBridge.csproj'
$stageSource=(python prototipos/autoatualizador-seguro/m3-composed/prepare_pinned_source.py $sourceWork | Select-Object -Last 1).Trim()
if ($LASTEXITCODE -ne 0 -or !(Test-Path $stageSource)) { throw 'GF_INSTALL_FOCUSED_PINNED_SOURCE_FAILED' }
python prototipos/autoatualizador-seguro/m3-composed-3/generate_signed_fixtures.py $fixtures
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_SIGNED_FIXTURES_FAILED' }
dotnet restore $project "-p:ArtifactsPath=$outputs" "-p:Stage8Source=$stageSource" --locked-mode --nologo
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_RESTORE_FAILED' }
dotnet build $project "-p:ArtifactsPath=$outputs" "-p:Stage8Source=$stageSource" -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_L18_BUILD_FAILED' }
$l18=@(Get-ChildItem $outputs -Recurse -Filter 'GF.Updater.M3.L18Tests.exe' -File)
if ($l18.Count -ne 1) { throw 'GF_INSTALL_FOCUSED_L18_AMBIGUOUS' }
$env:GF_E2E_L18_EXE=$l18[0].FullName
New-Item -ItemType Directory -Path $private | Out-Null
[System.IO.File]::WriteAllText((Join-Path $private 'PERSONAL-KEEP-UNCHANGED.txt'),
  ("DO-NOT-TOUCH-MY-DATA" + [char]10),[System.Text.UTF8Encoding]::new($false))
$env:GF_E2E_STARTUP_DRAFT_PROBE='1'
$env:GF_E2E_HANDOFF_L18='1'
$env:GF_E2E_SELF_GRACEFUL_CLOSE='1'
$env:GF_E2E_SELECTED_NEW_CONTINUATION='1'
$env:GF_E2E_LIVE_INSTALL_SWAP='1'
$env:GF_E2E_SWAP_CUT_AFTER_OLD_MOVE='0'
$env:GF_E2E_POST_OWNER_PERSISTENCE='1'
$env:GF_E2E_GUI_NORMAL_SUCCESS='0'
$env:GF_E2E_GUI_NORMAL_L18_PROOF='0'
$env:GF_E2E_GUI_NORMAL_L18_DENY_CLOSE='0'
$env:GF_E2E_HTTP_CUT_NEGATIVE='0'
$baselineCode=@'
import hashlib,json,os
from pathlib import Path
r=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
def snap(folder):
    found={}
    for directory,dirs,files in os.walk(folder,followlinks=False):
        for name in files+dirs:
            p=Path(directory)/name
            assert not p.is_symlink() and not(p.stat().st_file_attributes&0x400)
        for name in files:
            p=Path(directory)/name
            found[p.relative_to(folder).as_posix()]=[p.stat().st_size,hashlib.sha256(p.read_bytes()).hexdigest()]
    return found
v=snap(r/"gf-e2e-old")
assert len(v)==284 and "GerenciadorFinal.exe" in v
with (r/"gf-e2e-install-focused-old-baseline.json").open("x",encoding="utf8") as f:
    json.dump(v,f,sort_keys=True)
print("GF_E2E_INSTALL_FOCUSED_OLD_BASELINE files=284",flush=True)
'@
$baselineCode | python -u -
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_OLD_BASELINE_FAILED' }
# Original normal WinForms startup: NO --tray and no ShowWindow forced.
$proc=Start-Process -FilePath (Join-Path $old 'GerenciadorFinal.exe') -WorkingDirectory $old -PassThru
try {
  $env:GF_E2E_INSTALL_OLD_PID="$($proc.Id)"
  $observe=@'
import json,os,sys,time
from pathlib import Path
repo=Path(os.environ["GITHUB_WORKSPACE"]).resolve(strict=True)
sys.path.insert(0,str(repo/"prototipos"/"autoatualizador-seguro"/"phase1-composed"))
from win32_process_observation import own_main_windows,process_image_path,message_pump_ping,desktop_observation
from http_cut_gui_observer import consent_dialogs_for_pid
runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
exe=(runner/"gf-e2e-old"/"GerenciadorFinal.exe").resolve(strict=True)
pid=int(os.environ["GF_E2E_INSTALL_OLD_PID"])
until=time.monotonic()+45
ok=False
while time.monotonic()<until:
    wins=own_main_windows(pid)
    if len(wins)==1 and wins[0]["visible"]:
        image=process_image_path(pid)
        if image and os.path.samefile(image,exe) and message_pump_ping(wins[0]["hwnd"]) and not consent_dialogs_for_pid(pid):
            ok=True;break
    time.sleep(.12)
assert ok and desktop_observation()["interactive_user_desktop_verified"]
with (runner/"gf-e2e-install-focused-normal-window.json").open("x",encoding="utf8") as f:
    json.dump({"schema":"gf-e2e-normal-old-preinstalled-new/1",
      "old_pid":pid,"native_visible":True,"responsive":True,"image_verified":True,
      "no_tray":True,"no_showwindow":True,"test_only":True,"operational_ready":False},f)
    f.flush();os.fsync(f.fileno())
print("GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false",flush=True)
'@
  $observe | python -u -
  if ($LASTEXITCODE -ne 0 -or !(Test-Path $window)) { throw 'GF_INSTALL_FOCUSED_NORMAL_WINDOW_UNPROVEN' }
  $deadline=[DateTime]::UtcNow.AddMinutes(9)
  while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-Path $failure) { throw "GF_INSTALL_FOCUSED_OLD_FAILURE $(Get-Content $failure -Raw)" }
    $proc.Refresh()
    if ($proc.HasExited -and (Test-Path $report)) { break }
    Start-Sleep -Milliseconds 250
  }
  if (!$proc.HasExited -or !(Test-Path $report) -or $proc.ExitCode -ne 0 -or
      !(Test-Path $selected) -or !(Test-Path $closed) -or !(Test-Path $loop)) {
    throw 'GF_INSTALL_FOCUSED_HANDOFF_NOT_PROVEN'
  }
  $barrier=Get-Content $selected -Raw | ConvertFrom-Json
  $closedProof=Get-Content $closed -Raw | ConvertFrom-Json
  $loopProof=Get-Content $loop -Raw | ConvertFrom-Json
  if ($barrier.schema -ne 'gf-e2e-selected-new-ready/1' -or
      $barrier.selector -ne 'new' -or $barrier.generation -ne 2 -or
      !$barrier.launch_fenced -or !$barrier.l18_live_owner -or $barrier.operational_ready -or
      !$closedProof.form_closed_event -or $closedProof.close_reason -ne 'UserClosing' -or
      !$loopProof.application_run_returned -or
      !$loopProof.selected_new_fenced_before_close -or $loopProof.operational_ready) {
    throw 'GF_INSTALL_FOCUSED_GRACEFUL_FENCE_INVALID'
  }
  Write-Host 'GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false'
}
finally {
  $proc.Refresh()
  if (!$proc.HasExited) {
    # Disposal of this owned TEST process on failure is not update recovery.
    $proc.CloseMainWindow() | Out-Null
    if (!$proc.WaitForExit(12000)) {
      $proc.Kill()
      $proc.WaitForExit(12000) | Out-Null
      Write-Host 'GF_INSTALL_FOCUSED_FAILURE_TEST_CHILD_TERMINATED_NO_SUCCESS'
    }
  }
  $proc.Dispose()
}
$flow=Get-Content $report -Raw | ConvertFrom-Json
if ($flow.schema -ne 'gf-real-l18-installed-new-handoff/1' -or
    $flow.scenario -ne 'handoff-install' -or $flow.passed -ne 1 -or $flow.failed -ne 0 -or
    !$flow.result.physically_updated_original_installation -or
    !$flow.result.selected_new_after_original_old_exit -or
    $flow.result.outcome -ne 'UnknownBlocked' -or $flow.result.replay -ne 'UnknownBlocked' -or
    $flow.operational_ready -or $flow.recovery_authorized) {
  throw 'GF_INSTALL_FOCUSED_EXISTING_L18_REPORT_INVALID'
}
python prototipos/autoatualizador-seguro/phase1-composed/github_draft_package.py verify --repo . --old (Join-Path $root 'gf-e2e-old-preserved') --downloaded (Join-Path $root 'gf-e2e-github-download') --extracted (Join-Path $root 'gf-e2e-install-focused-independent-new') --lab-old-variant
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_INDEPENDENT_TUF_FAILED' }
$independent=@'
import hashlib,json,os
from pathlib import Path
r=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
def snap(folder):
    assert folder.is_dir() and not folder.is_symlink()
    found={}
    for directory,dirs,files in os.walk(folder,followlinks=False):
        for name in files+dirs:
            p=Path(directory)/name
            assert not p.is_symlink() and not(p.stat().st_file_attributes&0x400)
        for name in files:
            p=Path(directory)/name
            found[p.relative_to(folder).as_posix()]=[p.stat().st_size,hashlib.sha256(p.read_bytes()).hexdigest()]
    return found
old=json.loads((r/"gf-e2e-install-focused-old-baseline.json").read_bytes())
assert snap(r/"gf-e2e-old-preserved")==old and len(old)==284
assert snap(r/"gf-e2e-old")==snap(r/"gf-e2e-install-focused-independent-new")
assert len(snap(r/"gf-e2e-old"))==284
assert (r/"gf-e2e-install-data"/"PERSONAL-KEEP-UNCHANGED.txt").read_bytes()==b"DO-NOT-TOUCH-MY-DATA\n"
download=r/"gf-e2e-github-download"
assert len(list(download.iterdir()))==5
for name,size,sha in [
    ("gf-e2e-new-win-x64.zip",57695630,"3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e"),
    ("GF-LAB-REAL-PACKAGE-MANIFEST.json",72618,"a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d")]:
    p=download/name
    assert p.stat().st_size==size and hashlib.sha256(p.read_bytes()).hexdigest()==sha
flow=json.loads((r/"gf-e2e-real-flow-report.json").read_bytes())
assert flow["schema"]=="gf-real-l18-installed-new-handoff/1"
assert flow["result"]["outcome"]=="UnknownBlocked"
assert flow["result"]["replay"]=="UnknownBlocked"
assert flow["result"]["physically_updated_original_installation"] is True
assert flow["operational_ready"] is False and flow["recovery_authorized"] is False
stage=json.loads((r/"gf-e2e-live-install-transition.json").read_bytes())
assert stage["schema"]=="gf-e2e-live-portable-install/1"
assert stage["physically_installed_new_at_original_old_path"] is True
assert stage["retained_old_bytes"] is True
assert stage["private_data_canary_unchanged"] is True
assert stage["no_rollback_performed"] is True
assert stage["selected"]=="new" and stage["generation"]==2
assert stage["operational_ready"] is False
root=json.loads((r/"gf-e2e-postexit-selected-root.json").read_bytes())
assert root["schema"]=="gf-e2e-postexit-selected-root/1"
assert root["retained_from_same_l18"] is True
assert root["recovery_authorized"] is False and root["operational_ready"] is False
source=Path(root["root"]).resolve(strict=True)
selector=json.loads((source/"l18-real-active.fixture.json").read_bytes())
assert selector["selected"]=="new" and selector["generation"]==2
# L18 TargetSha is the digest of its separately signed, transaction-bound
# REAL-LAB manifest, NOT the immutable original Draft package manifest.
# Pin the actual canonical transaction manifest bytes to L18 selector.
lab_bytes=(source/"real-auth"/"GF-LAB-REAL-PACKAGE-MANIFEST.json").read_bytes()
lab_sha=hashlib.sha256(lab_bytes).hexdigest()
lab=json.loads(lab_bytes)
assert lab["schema"]=="gf-real-package-lab/3"
assert lab["selected_before"]=="old" and lab["operational_installable"] is False
assert lab["installation"]==selector["installation"]
assert lab["transaction"]==selector["transaction"]
assert len(lab["files"])==284 and len(lab["old_files"])==284
assert selector["manifest_sha256"]==lab_sha
assert (source/"l18-real-new-may-have-effects.fixture.txt").is_file()
observe=json.loads((source/"real-observed-new.fixture.json").read_bytes())
assert observe["observed_from_live_installation"] is True
assert observe["created"] is True and observe["terminated"] is True
assert observe["readonly_inventory_search_health"]["minimum_readonly_health_contract_observed"] is True
assert (r/"gf-e2e-old-exit-before-new-observation.json").is_file()
native=json.loads((r/"gf-e2e-install-focused-normal-window.json").read_bytes())
assert native["native_visible"] and native["responsive"] and native["no_tray"] and native["no_showwindow"]
print("GF_E2E_NORMAL_INSTALLED_NEW_BYTES_PASS "
    "old_normal=true old_graceful_exit=true installed_new_path=true "
    "files_old=284 files_new=284 installed_equals_tuf=true "
    "new_executed_installed_path=true readonly_health=true "
    "canary_preserved=true HOLD=UnknownBlocked "
    "recovery=false rollback=false cleanup=false retry=false READY=false",flush=True)
'@
$independent | python -u -
if ($LASTEXITCODE -ne 0) { throw 'GF_INSTALL_FOCUSED_INDEPENDENT_BYTES_FAILED' }
$newVersion=[System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $old 'GerenciadorFinal.dll')).Version.ToString()
if ($newVersion -ne '5.13.11.66') { throw 'GF_INSTALL_FOCUSED_INSTALLED_VERSION_WRONG' }
Write-Host 'GF_E2E_NORMAL_INSTALLED_NEW_PASS old_normal=true new_version=5.13.11.66 installed_executed=true L18=same HOLD=true READY=false'
Add-Content $env:GITHUB_STEP_SUMMARY 'Fresh second Windows runner (after existing cut negative): real OLD opened normally, downloaded and TUF-authenticated same private Draft; existing L18 --real-install-focused fenced NEW/2, gracefully closed OLD, installed authenticated NEW at original portable path, launched and functionally observed installed NEW, preserved OLD backup and canary, returned UnknownBlocked/HOLD. Independent TUF/file-set comparison (284+284). No recovery, rollback, cleanup, retry, READY or production promotion.'
