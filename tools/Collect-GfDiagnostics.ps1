[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 2147483647)]
    [int]$ProcessId,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommit,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Scenario,
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$BuildInfoPath,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,
    [ValidateRange(5, 600)]
    [int]$DurationSeconds = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($outputRoot.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GF_DIAGNOSTICS_OUTPUT_IN_SOURCE: choose a directory outside the repository'
}

$target = Get-Process -Id $ProcessId -ErrorAction Stop
if ($target.ProcessName -notin @('GerenciadorFinal', 'GfToolProbe')) {
    throw "GF_DIAGNOSTICS_TARGET_REJECTED: unexpected process '$($target.ProcessName)'"
}
$processPath = $target.MainModule.FileName
$buildInfo = Get-Content -LiteralPath $BuildInfoPath -Raw
if ($buildInfo -notmatch "(?m)^Commit Git:s*$([regex]::Escape($ExpectedCommit))s*$") {
    throw 'GF_DIAGNOSTICS_COMMIT_MISMATCH: BUILD-INFO does not contain the expected commit'
}

$null = New-Item -ItemType Directory -Path $outputRoot -Force
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$shortCommit = $ExpectedCommit.Substring(0, 8).ToLowerInvariant()
$tracePath = Join-Path $outputRoot "gf-$stamp-$shortCommit.nettrace"
$countersPath = Join-Path $outputRoot "gf-$stamp-$shortCommit.csv"
$metadataPath = Join-Path $outputRoot "gf-$stamp-$shortCommit.json"
$duration = [TimeSpan]::FromSeconds($DurationSeconds)
$durationText = '{0:dd:hh:mm:ss}' -f $duration

function Start-DiagnosticTool([string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'dotnet'
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    if (-not $process.Start()) { throw 'GF_DIAGNOSTICS_TOOL_START_FAILED' }
    return $process
}

$trace = Start-DiagnosticTool @(
    'tool', 'run', 'dotnet-trace', '--', 'collect',
    '--process-id', $ProcessId.ToString(),
    '--duration', $durationText,
    '--output', $tracePath
)
$trace.WaitForExit()
if ($trace.ExitCode -ne 0) { throw "GF_DIAGNOSTICS_TRACE_FAILED: exit $($trace.ExitCode)" }

$counters = Start-DiagnosticTool @(
    'tool', 'run', 'dotnet-counters', '--', 'collect',
    '--process-id', $ProcessId.ToString(),
    '--refresh-interval', '1',
    '--format', 'csv',
    '--output', $countersPath
)
Start-Sleep -Seconds $DurationSeconds
if (-not $counters.HasExited) {
    $counters.StandardInput.WriteLine('q')
    $counters.StandardInput.Flush()
    if (-not $counters.WaitForExit(30000)) {
        # Only the diagnostic child is stopped; the GF process is never touched.
        $counters.Kill()
        $counters.WaitForExit()
    }
}
if ($counters.ExitCode -ne 0) { throw "GF_DIAGNOSTICS_COUNTERS_FAILED: exit $($counters.ExitCode)" }

$metadata = [ordered]@{
    tool = 'GF engineering diagnostics'
    scenario = $Scenario
    expected_commit = $ExpectedCommit.ToLowerInvariant()
    process_id = $ProcessId
    process_name = $target.ProcessName
    process_path = $processPath
    captured_at_utc = [DateTime]::UtcNow.ToString('O')
    duration_seconds = $DurationSeconds
    trace = [IO.Path]::GetFileName($tracePath)
    counters = [IO.Path]::GetFileName($countersPath)
    target_started_by_script = $false
    target_stopped_by_script = $false
    output_directory = $outputRoot
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding utf8
Write-Output ($metadata | ConvertTo-Json -Depth 5)
