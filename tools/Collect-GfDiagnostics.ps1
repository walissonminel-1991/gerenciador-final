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

function Get-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Test-SameOrDescendant([string]$Candidate, [string]$Parent) {
    $parentPath = $Parent.TrimEnd([char[]]@('\', '/'))
    return $Candidate.Equals($parentPath, [StringComparison]::OrdinalIgnoreCase) -or
        $Candidate.StartsWith($parentPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

if (-not ('GfDiagnosticsNativePath' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class GfDiagnosticsNativePath
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder path,
        uint bufferLength,
        uint flags);

    public static string GetFinalPath(string path)
    {
        using SafeFileHandle handle = CreateFile(
            path,
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var buffer = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return buffer.ToString();
    }
}
'@
}

function Get-PhysicalPath([string]$Path) {
    return [GfDiagnosticsNativePath]::GetFinalPath($Path)
}

function Assert-LocalOutputRoot([string]$Path, [string]$RepositoryRoot) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) {
        throw 'GF_DIAGNOSTICS_OUTPUT_ABSOLUTE_REQUIRED: choose an absolute local directory'
    }
    if (Test-SameOrDescendant $Path $RepositoryRoot) {
        throw 'GF_DIAGNOSTICS_OUTPUT_IN_SOURCE: choose a directory outside the repository'
    }
    if ($Path.StartsWith('\\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'GF_DIAGNOSTICS_OUTPUT_NETWORK_FORBIDDEN: choose a local volume'
    }

    $existing = $Path
    while (-not (Test-Path -LiteralPath $existing)) {
        $parent = [IO.Path]::GetDirectoryName($existing)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $existing) {
            throw 'GF_DIAGNOSTICS_OUTPUT_PARENT_INVALID'
        }
        $existing = $parent
    }
    $existingItem = Get-Item -LiteralPath $existing -Force
    $cursor = $existingItem.FullName
    while ($true) {
        $cursorItem = Get-Item -LiteralPath $cursor -Force
        if ($null -ne $cursorItem.LinkType -or $null -ne $cursorItem.Target) {
            throw 'GF_DIAGNOSTICS_OUTPUT_LINK_FORBIDDEN: choose a non-linked local directory'
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $cursor) { break }
        $cursor = $parent
    }
    $physicalRepositoryRoot = Get-PhysicalPath $RepositoryRoot
    $physicalExistingRoot = Get-PhysicalPath $existingItem.FullName
    $root = [IO.Path]::GetPathRoot($physicalExistingRoot)
    if ($root.StartsWith('\\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'GF_DIAGNOSTICS_OUTPUT_NETWORK_FORBIDDEN: choose a local volume'
    }
    $drive = [IO.DriveInfo]::new($root)
    if ($drive.DriveType -eq [IO.DriveType]::Network) {
        throw 'GF_DIAGNOSTICS_OUTPUT_NETWORK_FORBIDDEN: choose a local volume'
    }
    $repositoryDrive = [IO.Path]::GetPathRoot($physicalRepositoryRoot)
    if (-not $root.Equals($repositoryDrive, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'GF_DIAGNOSTICS_OUTPUT_VOLUME_REQUIRED: choose a directory on the repository local volume'
    }
    if (Test-Path -LiteralPath $Path) {
        $outputItem = Get-Item -LiteralPath $Path -Force
        if ($null -ne $outputItem.LinkType -or $null -ne $outputItem.Target) {
            throw 'GF_DIAGNOSTICS_OUTPUT_LINK_FORBIDDEN: choose a non-linked local directory'
        }
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not [IO.Path]::IsPathFullyQualified($OutputDirectory)) {
    throw 'GF_DIAGNOSTICS_OUTPUT_ABSOLUTE_REQUIRED: choose an absolute local directory'
}
$outputRoot = Get-FullPath $OutputDirectory
Assert-LocalOutputRoot $outputRoot $repoRoot

$buildInfoFile = (Resolve-Path -LiteralPath $BuildInfoPath).Path
$buildInfoRoot = [IO.Path]::GetDirectoryName($buildInfoFile)
if ([IO.Path]::GetFileName($buildInfoFile) -ine 'BUILD-INFO.txt') {
    throw 'GF_DIAGNOSTICS_BUILD_INFO_NAME_REQUIRED: use the official BUILD-INFO.txt'
}
$buildInfoItem = Get-Item -LiteralPath $buildInfoFile -Force
if ($null -ne $buildInfoItem.LinkType -or $null -ne $buildInfoItem.Target) {
    throw 'GF_DIAGNOSTICS_BUILD_INFO_LINK_FORBIDDEN: BUILD-INFO.txt must be a regular file'
}
$expectedCommitLower = $ExpectedCommit.ToLowerInvariant()

$target = Get-Process -Id $ProcessId -ErrorAction Stop
if ($target.ProcessName -notin @('GerenciadorFinal', 'GfToolProbe')) {
    throw "GF_DIAGNOSTICS_TARGET_REJECTED: unexpected process '$($target.ProcessName)'"
}
$processPath = (Resolve-Path -LiteralPath $target.MainModule.FileName).Path
$processRoot = [IO.Path]::GetDirectoryName($processPath)
if (-not $processRoot.Equals($buildInfoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GF_DIAGNOSTICS_BUILD_BINDING_MISMATCH: BUILD-INFO must be beside the observed executable'
}
$physicalProcessRoot = [IO.Path]::GetDirectoryName((Get-PhysicalPath $processPath))
$physicalBuildInfoRoot = [IO.Path]::GetDirectoryName((Get-PhysicalPath $buildInfoFile))
if (-not $physicalProcessRoot.Equals($physicalBuildInfoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GF_DIAGNOSTICS_BUILD_BINDING_MISMATCH: BUILD-INFO physical directory differs from executable'
}
$targetStartTime = $target.StartTime.ToUniversalTime()
$buildInfo = Get-Content -LiteralPath $buildInfoFile -Raw
$commitPattern = "(?m)^[\t ]*Commit Git:[\t ]*$([regex]::Escape($expectedCommitLower))[\t ]*(?:\r?\n|$)"
if ($buildInfo.ToLowerInvariant() -notmatch $commitPattern) {
    throw 'GF_DIAGNOSTICS_COMMIT_MISMATCH: BUILD-INFO does not contain the expected commit'
}

function Get-VerifiedTarget {
    $current = Get-Process -Id $ProcessId -ErrorAction Stop
    if ($current.ProcessName -notin @('GerenciadorFinal', 'GfToolProbe')) {
        throw 'GF_DIAGNOSTICS_TARGET_CHANGED: process name changed'
    }
    $currentPath = (Resolve-Path -LiteralPath $current.MainModule.FileName).Path
    if (-not $currentPath.Equals($processPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'GF_DIAGNOSTICS_TARGET_CHANGED: process path changed'
    }
    if ($current.StartTime.ToUniversalTime() -ne $targetStartTime) {
        throw 'GF_DIAGNOSTICS_TARGET_CHANGED: process start time changed'
    }
    return $current
}

$null = New-Item -ItemType Directory -Path $outputRoot -Force
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$shortCommit = $expectedCommitLower.Substring(0, 8)
$collectionRoot = Join-Path $outputRoot "gf-$stamp-$shortCommit-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $collectionRoot -Force
$tracePath = Join-Path $collectionRoot 'trace.nettrace'
$countersPath = Join-Path $collectionRoot 'counters.csv'
$metadataPath = Join-Path $collectionRoot 'metadata.json'
$duration = [TimeSpan]::FromSeconds($DurationSeconds)
$durationText = '{0:dd\:hh\:mm\:ss}' -f $duration

function Start-DiagnosticTool([string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'dotnet'
    $info.WorkingDirectory = $repoRoot
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $false
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    if (-not $process.Start()) { throw 'GF_DIAGNOSTICS_TOOL_START_FAILED' }
    return $process
}

function Wait-DiagnosticTool([Diagnostics.Process]$Process, [string]$Name) {
    $timeoutMilliseconds = ($DurationSeconds + 120) * 1000
    if (-not $Process.WaitForExit($timeoutMilliseconds)) {
        # Only the diagnostic child is stopped; the GF process is never touched.
        $Process.Kill($true)
        $Process.WaitForExit()
        throw "GF_DIAGNOSTICS_$($Name.ToUpperInvariant())_TIMEOUT"
    }
    if ($Process.ExitCode -ne 0) {
        throw "GF_DIAGNOSTICS_$($Name.ToUpperInvariant())_FAILED: exit $($Process.ExitCode)"
    }
}

$null = Get-VerifiedTarget
$trace = Start-DiagnosticTool @(
    'tool', 'run', 'dotnet-trace', '--', 'collect',
    '--process-id', $ProcessId.ToString(),
    '--duration', $durationText,
    '--resume-runtime:false',
    '--output', $tracePath
)
Wait-DiagnosticTool $trace 'trace'

$null = Get-VerifiedTarget
$counters = Start-DiagnosticTool @(
    'tool', 'run', 'dotnet-counters', '--', 'collect',
    '--process-id', $ProcessId.ToString(),
    '--duration', $durationText,
    '--resume-runtime:false',
    '--refresh-interval', '1',
    '--format', 'csv',
    '--output', $countersPath
)
Wait-DiagnosticTool $counters 'counters'

foreach ($artifact in @($tracePath, $countersPath)) {
    if (-not (Test-Path -LiteralPath $artifact -PathType Leaf) -or (Get-Item -LiteralPath $artifact).Length -eq 0) {
        throw "GF_DIAGNOSTICS_ARTIFACT_MISSING: $artifact"
    }
}

$metadata = [ordered]@{
    tool = 'GF engineering diagnostics'
    scenario = $Scenario
    expected_commit = $expectedCommitLower
    process_id = $ProcessId
    process_name = $target.ProcessName
    process_path = $processPath
    process_start_time_utc = $targetStartTime.ToString('O')
    captured_at_utc = [DateTime]::UtcNow.ToString('O')
    duration_seconds = $DurationSeconds
    trace = [IO.Path]::GetFileName($tracePath)
    counters = [IO.Path]::GetFileName($countersPath)
    target_started_by_script = $false
    target_stopped_by_script = $false
    output_directory = $collectionRoot
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding utf8
Write-Output ($metadata | ConvertTo-Json -Depth 5)
