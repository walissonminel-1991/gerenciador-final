param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$start = [Diagnostics.ProcessStartInfo]::new((Resolve-Path $Executable).Path)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.ArgumentList.Add('--gf-winget-read-only')
$process = [Diagnostics.Process]::Start($start)
try {
    $output = $process.StandardOutput.ReadToEndAsync()
    $errorText = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(12000)) { $process.Kill(); throw 'Leitor COM excedeu prazo.' }
    $text = $output.GetAwaiter().GetResult()
    $null = $errorText.GetAwaiter().GetResult()
    if ($text.Length -gt 4MB) { throw 'Protocolo excedeu limite.' }
    if ($process.ExitCode -eq 0) {
        $rows = @($text | ConvertFrom-Json -Depth 12)
        if ($rows.Count -gt 2000 -or @($rows | Where-Object { $_.Identity.Provider -ne 'winget' }).Count -gt 0) {
            throw 'Resposta fora do contrato.'
        }
        Write-Host "WORK16.COM estado=ok entradas=$($rows.Count) execucao_mutavel=0"
    } elseif ($process.ExitCode -eq 3) {
        Write-Host 'WORK16.COM estado=contexto_elevado_recusado homologacao_api=pendente execucao_mutavel=0'
    } elseif ($process.ExitCode -in 4,5) {
        Write-Host 'WORK16.COM estado=api_indisponivel homologacao_api=pendente execucao_mutavel=0'
    } else { throw "Falha inesperada no protocolo do leitor: $($process.ExitCode)" }
} finally { $process.Dispose() }
