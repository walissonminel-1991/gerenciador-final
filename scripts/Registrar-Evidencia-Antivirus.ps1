param([Parameter(Mandatory=$true)][string]$Directory,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
# Evidência do binário recém-compilado no runner, sem iniciar o produto,
# enviar amostra ou ler configurações/arquivos dos computadores pessoais.
$root = (Resolve-Path -LiteralPath $Directory).Path
$files = foreach ($name in @('GerenciadorFinal.exe', 'GerenciadorFinal.dll')) {
    $path = Join-Path $root $name
    $item = Get-Item -LiteralPath $path
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    $version = $item.VersionInfo
    [ordered]@{
        name = $name
        bytes = $item.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        file_version = $version.FileVersion
        signature_status = [string]$signature.Status
        signer = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { $null }
    }
}
[ordered]@{
    schema = 1
    source = $env:GF_SOURCE_SHA
    build = $env:GITHUB_SHA
    run_id = $env:GITHUB_RUN_ID
    files = @($files)
    avg_validation = 'pending'
    scan_performed = $false
    sample_submitted = $false
    meaning = 'Inventario de integridade e assinatura. Nao e resultado de scanner nem aprovacao AVG.'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
