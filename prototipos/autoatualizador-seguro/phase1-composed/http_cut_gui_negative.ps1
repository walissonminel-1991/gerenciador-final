# The real GF OLD WinForms MainForm.Shown must survive a controlled TCP
# disconnect during the authentic NEW ZIP HttpClient stream.
# Disposable GitHub Windows runner only. No L18 invocation or retry.
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:GITHUB_REPOSITORY -ne 'walissonminel-1991/gerenciador-final') {
  throw 'GF_HTTP_GUI_TEST_NOT_DISPOSABLE_RUNNER'
}
$runner=$env:RUNNER_TEMP
$oldSource=Join-Path $runner 'gf-e2e-old-preserved'
$guiOld=Join-Path $runner 'gf-e2e-http-ui-old'
$exe=Join-Path $guiOld 'GerenciadorFinal.exe'
$zip=Join-Path $runner 'gf-e2e-http-cut-source\gf-e2e-new-win-x64.zip'
$ready=Join-Path $runner 'gf-e2e-http-ui-ready.json'
$serverReport=Join-Path $runner 'gf-e2e-http-ui-server.json'
$shown=Join-Path $runner 'gf-e2e-http-ui-startup-shown.json'
$failure=Join-Path $runner 'gf-e2e-http-ui-failure.json'
$guiReport=Join-Path $runner 'gf-e2e-http-ui-observed.json'
$partial=Join-Path $runner 'gf-e2e-http-ui-download\gf-e2e-new-win-x64.zip'
$canary=Join-Path $runner 'gf-e2e-install-data\PERSONAL-KEEP-UNCHANGED.txt'
$selectorRoot=(Get-Content (Join-Path $runner 'gf-e2e-real-flow-report.json') -Raw | ConvertFrom-Json).root
$selector=Join-Path $selectorRoot 'l18-real-active.fixture.json'
if ((Test-Path $guiOld) -or (Test-Path $ready) -or (Test-Path $serverReport) -or
    (Test-Path $shown) -or (Test-Path $failure) -or (Test-Path $guiReport) -or
    (Test-Path (Join-Path $runner 'gf-e2e-http-ui-download')) -or
    !(Test-Path $zip) -or !(Test-Path $oldSource) -or
    !(Test-Path $canary) -or !(Test-Path $selector)) {
  throw 'GF_HTTP_GUI_TEST_MUST_BE_FRESH_AND_ISOLATED'
}
if ((Get-Item $zip).Length -ne 57695630 -or
    (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      '3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e') {
  throw 'GF_HTTP_GUI_TEST_ZIP_NOT_PINNED'
}
$canaryBefore=(Get-FileHash $canary -Algorithm SHA256).Hash
$selectorBefore=(Get-FileHash $selector -Algorithm SHA256).Hash
# Create and compare an isolated ORIGINAL OLD executable tree. The actual
# OLD backup, TUF state, and canary remain read-only throughout this test.
$copy=@'
import hashlib,json,os,shutil
from pathlib import Path
root=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
original=root/"gf-e2e-old-preserved"
copy=root/"gf-e2e-http-ui-old"
baseline=json.loads((root/"gf-e2e-old-prestart-files.json").read_bytes())
assert original.is_dir() and not original.is_symlink() and not copy.exists()
def inventory(path):
    found={}
    for directory,dirs,files in os.walk(path,followlinks=False):
        for name in dirs+files:
            item=Path(directory)/name
            assert not item.is_symlink() and not item.stat().st_file_attributes&0x400
        for name in files:
            item=Path(directory)/name
            found[item.relative_to(path).as_posix()]={
                "length":item.stat().st_size,
                "sha256":hashlib.sha256(item.read_bytes()).hexdigest()}
    return found
assert inventory(original)==baseline and len(baseline)==284
shutil.copytree(original,copy,symlinks=True)
assert inventory(copy)==baseline
print("GF_E2E_HTTP_GUI_OLD_ISOLATED old_files=284 original_unchanged=true")
'@
$copy | python -
if ($LASTEXITCODE -ne 0) { throw 'GF_HTTP_GUI_COPY_OR_BASELINE_FAILED' }
$serverScript='prototipos/autoatualizador-seguro/phase1-composed/http_cut_server.py'
$observer='prototipos/autoatualizador-seguro/phase1-composed/http_cut_gui_observer.py'
$server=Start-Process -FilePath (Get-Command python).Source -ArgumentList @(
  '-u',$serverScript,'--source',$zip,'--ready',$ready,'--report',$serverReport
) -WorkingDirectory $env:GITHUB_WORKSPACE -PassThru -WindowStyle Hidden
try {
  $deadline=[DateTime]::UtcNow.AddSeconds(30)
  while (!(Test-Path $ready) -and [DateTime]::UtcNow -lt $deadline) {
    $server.Refresh()
    if ($server.HasExited) { throw 'GF_HTTP_GUI_LOOPBACK_SERVER_EXITED' }
    Start-Sleep -Milliseconds 200
  }
  if (!(Test-Path $ready)) { throw 'GF_HTTP_GUI_LOOPBACK_NOT_READY' }
  $port=(Get-Content $ready -Raw | ConvertFrom-Json).port
  if ($port -lt 1024 -or $port -gt 65535) { throw 'GF_HTTP_GUI_LOOPBACK_PORT_INVALID' }
  $env:GF_E2E_HTTP_CUT_PORT=[string]$port
  $env:GF_E2E_HANDOFF_L18='0'
  $env:GF_E2E_SELF_GRACEFUL_CLOSE='0'
  $env:GF_E2E_SELECTED_NEW_CONTINUATION='0'
  $env:GF_E2E_SWAP_CUT_AFTER_OLD_MOVE='0'
  python -u $observer --exe $exe --shown $shown --failure $failure --transport $serverReport --result $guiReport
  if ($LASTEXITCODE -ne 0) { throw 'GF_HTTP_GUI_INVENTORY_AND_WINDOW_NOT_FUNCTIONAL_HOLD' }
  if (!(Test-Path $guiReport) -or !(Test-Path $partial) -or !(Test-Path $failure)) {
    throw 'GF_HTTP_GUI_PARTIAL_OR_PROOF_MISSING'
  }
  $result=Get-Content $guiReport -Raw | ConvertFrom-Json
  $fail=Get-Content $failure -Raw | ConvertFrom-Json
  $transport=Get-Content $serverReport -Raw | ConvertFrom-Json
  $received=(Get-Item $partial).Length
  if ($result.schema -ne 'gf-e2e-http-cut-mainform-healthy/1' -or
      !$result.real_mainform_shown -or !$result.real_network_tcp_disconnect_observed -or
      !$result.original_old_process_alive_after_failure -or
      !$result.process_image_verified -or !$result.owned_mainform_hwnd_visible_after_failure -or
      !$result.ui_message_pump_responded_after_failure -or
      !$result.inventory_nonzero -or !$result.readonly_search_nonmatch_seen -or
      !$result.readonly_search_restored -or !$result.old_ui_healthy_after_http_cut -or
      $result.new_executed -or $result.l18_handoff_called -or
      $result.retry_authorized -or $result.rollback_authorized -or
      $result.cleanup_authorized -or $result.operational_ready -or
      $fail.stage -ne 'asset_download' -or
      $fail.reason -notin @('HttpIOException','IOException','HttpRequestException') -or
      $transport.bytes_sent_before_tcp_disconnect -ne 1048576 -or
      $received -le 0 -or $received -ge 57695630) {
    throw 'GF_HTTP_GUI_REAL_OLD_WINDOW_OR_FAIL_CLOSED_CONTRACT_UNPROVEN'
  }
  $audit=@'
import hashlib,json,os
from pathlib import Path
root=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
baseline=json.loads((root/"gf-e2e-old-prestart-files.json").read_bytes())
for path in (root/"gf-e2e-old-preserved",root/"gf-e2e-http-ui-old"):
    found={}
    for directory,dirs,files in os.walk(path,followlinks=False):
        for name in dirs+files:
            item=Path(directory)/name
            assert not item.is_symlink() and not item.stat().st_file_attributes&0x400
        for name in files:
            item=Path(directory)/name
            found[item.relative_to(path).as_posix()]={
                "length":item.stat().st_size,
                "sha256":hashlib.sha256(item.read_bytes()).hexdigest()}
    assert len(found)==284 and found==baseline
assert (root/"gf-e2e-install-data"/"PERSONAL-KEEP-UNCHANGED.txt").read_bytes()==(
    b"DO-NOT-TOUCH-MY-DATA\n")
assert not (root/"gf-e2e-old").exists()
assert not (root/"gf-e2e-live-install-transition.json").exists()
fixture=Path(json.loads((root/"gf-e2e-real-flow-report.json").read_bytes())["root"])
selector=json.loads((fixture/"l18-real-active.fixture.json").read_bytes())
assert selector["selected"]=="new" and selector["generation"]==2
assert not (fixture/"real-observed-new.fixture.json").exists()
cut=root/"gf-e2e-http-ui-download"
assert len(list(cut.iterdir()))==1
assert 0 < (cut/"gf-e2e-new-win-x64.zip").stat().st_size < 57695630
print("GF_E2E_MAINFORM_HTTP_CUT_INDEPENDENT_PASS old_files=284 "
      "new_executed=false canary_unchanged=true L18_not_invoked=true READY=false")
'@
  $audit | python -
  if ($LASTEXITCODE -ne 0 -or
      (Get-FileHash $canary -Algorithm SHA256).Hash -ne $canaryBefore -or
      (Get-FileHash $selector -Algorithm SHA256).Hash -ne $selectorBefore) {
    throw 'GF_HTTP_GUI_ORIGINAL_OLD_CANARY_OR_SELECTOR_CHANGED'
  }
  Write-Host "GF_E2E_MAINFORM_HTTP_CUT_PASS original_mainform_shown=true visible_after_failure=true inventory_search_healthy=true received_bytes=$received old_bytes_unchanged=true new_executed=false READY=false"
  Add-Content $env:GITHUB_STEP_SUMMARY 'GF OLD real WinForms MainForm.Shown triggered the same HttpClient midstream loopback TCP disconnect (1 MiB real Draft ZIP). Its verified owned HWND remained visible, responsive and search/inventory functional after the failure; original OLD, canary and existing L18 selector intact; no NEW, retry, rollback or READY. GUI shown by TEST-ONLY Win32 ShowWindow after --tray suppressed first-run consent, not proof of an unattended normal GUI first-start.'
}
finally {
  Remove-Item Env:GF_E2E_HTTP_CUT_PORT -ErrorAction SilentlyContinue
  if ($null -ne $server) {
    $server.Refresh()
    if (!$server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    $server.Dispose()
  }
}
