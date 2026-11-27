[CmdletBinding()]
param(
    [switch]$SkipOptionalWindowsTools
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$globalJson = Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw | ConvertFrom-Json
$expectedSdk = $globalJson.sdk.version
$expectedSdkVersion = [version]$expectedSdk
$expectedFeatureBand = [math]::Floor($expectedSdkVersion.Build / 100) * 100
$expectedToolVersion = '10.0.750501'
$expectedOpenCvVersion = '5.1.0.95'
$expectedNumpyVersion = '2.4.5'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) {
    $failures.Add($Message)
}

function Get-CommandOrNull([string]$Name) {
    return Get-Command $Name -ErrorAction SilentlyContinue
}

if ($env:OS -ne 'Windows_NT') { Add-Failure 'Windows_NT is required' }
if ($PSVersionTable.PSVersion.Major -lt 7) { Add-Failure 'PowerShell 7 or newer is required' }

$git = Get-CommandOrNull 'git'
$dotnet = Get-CommandOrNull 'dotnet'
$python = Get-CommandOrNull 'python'
if ($null -eq $git) { Add-Failure 'git is missing' }
if ($null -eq $dotnet) { Add-Failure 'dotnet is missing' }
if ($null -eq $python) { Add-Failure 'python is missing' }

$previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
try {
    Push-Location -LiteralPath $root
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

    if ($null -ne $dotnet) {
        $sdkVersion = (& $dotnet.Source --version 2>&1 | Out-String).Trim()
    $actualSdkVersion = $null
    $sdkCompatible = $false
    if ($LASTEXITCODE -eq 0 -and [version]::TryParse($sdkVersion, [ref]$actualSdkVersion)) {
        $sdkCompatible = $actualSdkVersion.Major -eq $expectedSdkVersion.Major -and
            $actualSdkVersion.Minor -eq $expectedSdkVersion.Minor -and
            $actualSdkVersion.Build -ge $expectedFeatureBand -and
            $actualSdkVersion.Build -lt ($expectedFeatureBand + 100)
    }
    if (-not $sdkCompatible) {
        Add-Failure "Expected .NET SDK feature band $expectedSdk; found '$sdkVersion'"
        }

        $manifestPath = Join-Path $root '.config/dotnet-tools.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            Add-Failure 'Local .NET tool manifest is missing'
        } else {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            foreach ($toolName in @('dotnet-trace', 'dotnet-counters')) {
                $tool = $manifest.tools.$toolName
                if ($null -eq $tool -or $tool.version -ne $expectedToolVersion) {
                    Add-Failure "$toolName is not pinned to $expectedToolVersion"
                } else {
                    $toolOutput = (& $dotnet.Source tool run $toolName -- --version 2>&1 | Out-String).Trim()
                    if ($LASTEXITCODE -ne 0 -or $toolOutput -notmatch [regex]::Escape($expectedToolVersion)) {
                        Add-Failure "$toolName is not restored or has an unexpected version: $toolOutput"
                    }
                }
            }
        }
    }

    if ($null -ne $python) {
        $pythonVersion = (& $python.Source -c 'import sys; print("%d.%d.%d" % sys.version_info[:3])' 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [version]$pythonVersion -lt [version]'3.11.0') {
            Add-Failure "Python 3.11 or newer is required; found '$pythonVersion'"
        }
        $pythonImports = (& $python.Source -c 'import cv2, importlib.metadata as m; print(cv2.__version__ + " / " + m.version("opencv-python-headless") + " / " + m.version("numpy"))' 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $pythonImports -ne "$expectedOpenCvVersion / $expectedOpenCvVersion / $expectedNumpyVersion") {
            Add-Failure "Pinned Python/OpenCV distributions unavailable: $pythonImports"
        }
    }
} finally {
    Pop-Location
    if ($null -eq $previousTelemetry) {
        Remove-Item Env:DOTNET_CLI_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue
    } else {
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
    }
}

$optional = [ordered]@{}
if (-not $SkipOptionalWindowsTools) {
    foreach ($toolName in @('wpr.exe', 'wpa.exe', 'PerfView.exe')) {
        $optional[$toolName] = $null -ne (Get-CommandOrNull $toolName)
    }
}

$result = [ordered]@{
    ok = $failures.Count -eq 0
    root = $root
    expected_sdk = $expectedSdk
    expected_dotnet_tools = $expectedToolVersion
    expected_python_distributions = "$expectedOpenCvVersion / $expectedNumpyVersion"
    optional_tools = $optional
    failures = @($failures)
}
$result | ConvertTo-Json -Depth 5
if ($failures.Count -gt 0) { exit 1 }
