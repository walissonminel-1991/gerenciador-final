# Windows disposable GitHub Actions only. Tests a genuine TCP disconnect during
# GF OLD's existing HttpClient ZIP stream, without invoking L18 or changing OLD.
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_REPOSITORY -ne 'walissonminel-1991/gerenciador-final') {
  throw 'GF_E2E_HTTP_CUT_WINDOWS_RUNNER_ONLY'
}
$runner=$env:RUNNER_TEMP
$zipDir=Join-Path $runner 'gf-e2e-http-cut-source'
$zip=Join-Path $zipDir 'gf-e2e-new-win-x64.zip'
$ready=Join-Path $runner 'gf-e2e-http-cut-ready.json'
$receipt=Join-Path $runner 'gf-e2e-http-cut-server.json'
$failure=Join-Path $runner 'gf-e2e-http-cut-failure.json'
$partial=Join-Path $runner 'gf-e2e-http-cut-download\gf-e2e-new-win-x64.zip'
$exe=Join-Path $runner 'gf-e2e-old-preserved\GerenciadorFinal.exe'
$canary=Join-Path $runner 'gf-e2e-install-data\PERSONAL-KEEP-UNCHANGED.txt'
$selectedRoot=(Get-Content (Join-Path $runner 'gf-e2e-real-flow-report.json') -Raw | ConvertFrom-Json).root
$selector=Join-Path $selectedRoot 'l18-real-active.fixture.json'
if ((Test-Path $zipDir) -or (Test-Path $ready) -or (Test-Path $receipt) -or
    (Test-Path $failure) -or (Test-Path (Join-Path $runner 'gf-e2e-http-cut-download')) -or
    !(Test-Path $exe) -or !(Test-Path $canary) -or !(Test-Path $selector)) {
  throw 'GF_E2E_HTTP_CUT_NOT_FRESH_OR_ORIGINAL_MISSING_HOLD'
}
[System.IO.Directory]::CreateDirectory($zipDir) | Out-Null
gh release download 'gf-e2e-c5057bbbaf1d' --repo $env:GITHUB_REPOSITORY --dir $zipDir --pattern 'gf-e2e-new-win-x64.zip'
if ($LASTEXITCODE -ne 0 -or !(Test-Path $zip) -or
    (Get-Item $zip).Length -ne 57695630 -or
    (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      '3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e') {
  throw 'GF_E2E_HTTP_CUT_DRAFT_ZIP_IDENTITY_FAILED'
}
$canaryBefore=(Get-FileHash $canary -Algorithm SHA256).Hash
$selectorBefore=(Get-FileHash $selector -Algorithm SHA256).Hash
$serverScript='prototipos/autoatualizador-seguro/phase1-composed/http_cut_server.py'
$server=Start-Process -FilePath (Get-Command python).Source -ArgumentList @(
  '-u',$serverScript,'--source',$zip,'--ready',$ready,'--report',$receipt
) -WorkingDirectory $env:GITHUB_WORKSPACE -PassThru -WindowStyle Hidden
try {
  $deadline=[DateTime]::UtcNow.AddSeconds(30)
  while (!(Test-Path $ready) -and [DateTime]::UtcNow -lt $deadline) {
    $server.Refresh()
    if ($server.HasExited) { throw 'GF_E2E_HTTP_CUT_SERVER_PREMATURE_EXIT' }
    Start-Sleep -Milliseconds 200
  }
  if (!(Test-Path $ready)) { throw 'GF_E2E_HTTP_CUT_SERVER_START_TIMEOUT' }
  $r=Get-Content $ready -Raw | ConvertFrom-Json
  if ($r.schema -ne 'gf-e2e-http-disconnect-ready/1' -or !$r.loopback_only -or
      !$r.test_only -or $r.port -lt 1024 -or $r.port -gt 65535) {
    throw 'GF_E2E_HTTP_CUT_LOOPBACK_NOT_CONFIRMED'
  }
  # The SAME GF OLD executable and existing .NET HttpClient.
  # Negative CLI entry, no WinForms and no L18 invocation.
  $env:GF_E2E_HTTP_CUT_NEGATIVE='1'
  $env:GF_E2E_HTTP_CUT_PORT=[string]$r.port
  $env:GF_E2E_HANDOFF_L18='0'
  $env:GF_E2E_SELF_GRACEFUL_CLOSE='0'
  $env:GF_E2E_SELECTED_NEW_CONTINUATION='0'
  $env:GF_E2E_SWAP_CUT_AFTER_OLD_MOVE='0'
  $p=Start-Process -FilePath $exe -ArgumentList '--gf-e2e-draft-acquire' -WorkingDirectory (Split-Path $exe) -PassThru -Wait
  if ($p.ExitCode -ne 8) { throw "GF_E2E_HTTP_CUT_OLD_NOT_FAIL_CLOSED_$($p.ExitCode)" }
  $deadline=[DateTime]::UtcNow.AddSeconds(15)
  while (!(Test-Path $receipt) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 200
  }
  if (!(Test-Path $receipt) -or !(Test-Path $failure) -or !(Test-Path $partial)) {
    throw 'GF_E2E_HTTP_CUT_REPORT_OR_PARTIAL_MISSING'
  }
  $f=Get-Content $failure -Raw | ConvertFrom-Json
  $t=Get-Content $receipt -Raw | ConvertFrom-Json
  $size=(Get-Item $partial).Length
  if ($f.schema -ne 'gf-e2e-old-blocked/2' -or $f.stage -ne 'asset_download' -or
      $f.reason -notin @('HttpIOException','IOException','HttpRequestException') -or
      !$f.no_token_logged -or !$f.no_auto_recovery -or
      $t.schema -ne 'gf-e2e-http-disconnect/1' -or
      $t.http_status_sent -ne 200 -or $t.content_length_declared -ne 57695630 -or
      $t.bytes_sent_before_tcp_disconnect -ne 1048576 -or
      !$t.socket_shutdown_in_body -or !$t.loopback_only -or
      !$t.bearer_not_received -or $t.operational_ready -or
      $size -le 0 -or $size -ge 57695630) {
    throw "GF_E2E_HTTP_CUT_NOT_GENUINE_MID_STREAM_DISCONNECT_$($f.reason)"
  }
  $audit=@'
import hashlib,json,os
from pathlib import Path
root=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
old=root/"gf-e2e-old-preserved"
baseline=json.loads((root/"gf-e2e-old-prestart-files.json").read_bytes())
actual={}
for cur,dirs,files in os.walk(old,followlinks=False):
    for name in dirs+files:
        p=Path(cur)/name
        assert not p.is_symlink() and not p.stat().st_file_attributes&0x400
    for name in files:
        p=Path(cur)/name
        actual[p.relative_to(old).as_posix()]={
            "length":p.stat().st_size,
            "sha256":hashlib.sha256(p.read_bytes()).hexdigest()}
assert actual==baseline and len(actual)==284
assert (root/"gf-e2e-install-data"/"PERSONAL-KEEP-UNCHANGED.txt").read_bytes()==b"DO-NOT-TOUCH-MY-DATA\n"
assert not (root/"gf-e2e-old").exists()
assert not (root/"gf-e2e-live-install-transition.json").exists()
assert not (root/"gf-e2e-old-l18-handoff-report.json").exists()
assert not (root/"gf-e2e-http-cut-download"/"targets.json").exists()
assert len(list((root/"gf-e2e-http-cut-download").iterdir()))==1
fixture=Path(json.loads((root/"gf-e2e-real-flow-report.json").read_bytes())["root"])
sel=json.loads((fixture/"l18-real-active.fixture.json").read_bytes())
assert sel["selected"]=="new" and sel["generation"]==2
assert not (fixture/"real-observed-new.fixture.json").exists()
print("GF_E2E_REAL_HTTP_CUT_OLD_AND_DATA_INTACT old_files=284 canary=UNCHANGED new_executed=false READY=false")
'@
  $audit | python -
  if ($LASTEXITCODE -ne 0 -or
      (Get-FileHash $canary -Algorithm SHA256).Hash -ne $canaryBefore -or
      (Get-FileHash $selector -Algorithm SHA256).Hash -ne $selectorBefore) {
    throw 'GF_E2E_HTTP_CUT_OLD_DATA_OR_SELECTOR_MUTATED'
  }
  Write-Host "GF_E2E_REAL_HTTP_CONNECTION_CUT_PASS pinned_zip=true declared=57695630 sent=$($t.bytes_sent_before_tcp_disconnect) received=$size dotnet_httpclient=true old_exit=8 L18_not_invoked=true READY=false"
  Add-Content $env:GITHUB_STEP_SUMMARY 'Pinned 57 MB Draft ZIP transferred via test-controlled loopback HTTP. TCP closed after 1 MB during genuine GF OLD .NET HttpClient streaming, unlike synthetic truncated ZIP. OLD, canary, selector remain intact; no NEW executed. This is NOT a real GitHub CDN outage.'
}
finally {
  Remove-Item Env:GF_E2E_HTTP_CUT_NEGATIVE -ErrorAction SilentlyContinue
  Remove-Item Env:GF_E2E_HTTP_CUT_PORT -ErrorAction SilentlyContinue
  if ($null -ne $server) {
    $server.Refresh()
    if (!$server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    $server.Dispose()
  }
}
