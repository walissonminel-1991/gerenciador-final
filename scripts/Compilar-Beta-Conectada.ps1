param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-f0-9]{40}$')][string]$ExpectedSourceCommit,
    [Parameter(Mandatory=$true)][string]$Endpoint,
    [ValidateSet('Homologacao','Candidata')][string]$BuildPurpose = 'Homologacao',
    [string]$OutputDirectory = 'Distribuicao-Beta'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path $PSScriptRoot -Parent)
$source = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $source -ne $ExpectedSourceCommit) { throw 'HEAD divergente da fonte aprovada' }
if (& git status --porcelain --untracked-files=no) { throw 'Fonte rastreada modificada; commitar e validar antes de compilar' }
$approval = Get-Content 'docs/BETA-APROVACOES.json' -Raw | ConvertFrom-Json
[xml]$policy = Get-Content 'BetaTelemetryPolicy.props' -Raw
$approvedEndpoints = @($policy.SelectNodes('//GfApprovedBetaEndpoint') | ForEach-Object { $_.GetAttribute('Include') })
$reviewedHash = [string]$policy.SelectSingleNode('//GfReviewedPrivacyNoticeSha256').InnerText
if (-not $approval.privacy_approved -or $approval.private_contact -eq '' -or
    -not ($approvedEndpoints -ccontains $Endpoint) -or $approval.notice_sha256 -cne $reviewedHash -or
    $reviewedHash -notmatch '^[a-f0-9]{64}$') {
    throw 'GF_BETA_APPROVAL_PENDING: endpoint e aviso precisam de aprovacao em fonte'
}
if ($BuildPurpose -eq 'Candidata') {
    if ($approval.status -ne 'approved_for_connected_candidate_build' -or $approval.endpoint -cne $Endpoint -or
        -not $approval.connected_homologation_approved -or $approval.cloudflare_evidence_reference -eq '' -or
        $approval.github_delete_evidence_reference -eq '' -or $approval.windows_avg_evidence_reference -eq '' -or
        $approval.external_updater_evidence_reference -eq '') { throw 'GF_BETA_CONNECTED_HOMOLOGATION_PENDING' }
} elseif (-not $approval.synthetic_homologation_build_approved -or $approval.homologation_endpoint -cne $Endpoint -or
    $approval.status -notin @('approved_for_synthetic_homologation_build','approved_for_connected_candidate_build')) {
    throw 'GF_BETA_SYNTHETIC_HOMOLOGATION_BUILD_PENDING'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use pasta de saida nova para impedir mistura com builds anteriores' }
$arguments = @('publish','GerenciadorAplicativos.csproj','-c','Release','-r','win-x64','--self-contained','true',
    '-p:PublishSingleFile=false','-p:DebugType=None','-p:DebugSymbols=false',"-p:GfSourceCommit=$source", "-p:GfBetaEndpoint=$Endpoint",'-o',$OutputDirectory)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Falha de compilacao; nenhuma candidata autorizada' }
& dotnet build TestesTelemetria/TestesTelemetria.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha no verificador do assembly' }
$dll = Join-Path $OutputDirectory 'GerenciadorFinal.dll'
$report = & dotnet run --project TestesTelemetria/TestesTelemetria.csproj -c Release --no-build -- --verify-connected-build $dll $source $Endpoint
if ($LASTEXITCODE -ne 0) { throw 'Configuracao conectada ausente/divergente no assembly publicado' }
$report | Set-Content (Join-Path $OutputDirectory 'BETA-BUILD.json') -Encoding utf8
& ./TestesManifesto/Testar-Ativacao.ps1 -ExecutablePath (Join-Path $OutputDirectory 'GerenciadorFinal.exe')
if ($LASTEXITCODE -ne 0) { throw 'Manifesto Win32 nao confirmado' }
@('Produto: Gerenciador Final', 'Versao: 5.13.11.66', "Commit Git: $source", "Commit fonte: $source", 'Base da PR (quando aplicavel): ',
    "Canal: BETA CONECTADA - $BuildPurpose - DISTRIBUICAO DEPENDE DE APROVACAO FINAL",
    'Assinatura digital: AUSENTE (conferir assinatura real e AVG antes da entrega)',
    'Origem: https://github.com/walissonminel-1991/gerenciador-final') |
    Set-Content (Join-Path $OutputDirectory 'BUILD-INFO.txt') -Encoding utf8
$root = (Resolve-Path -LiteralPath $OutputDirectory).Path
$lines = foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
    "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
}
$lines | Set-Content (Join-Path $root 'SHA256SUMS.txt') -Encoding utf8
& python scripts/verificar_pacote.py $root --version 5.13.11.66 --source $source --build $source --beta
if ($LASTEXITCODE -ne 0) { throw 'Pacote reprovado' }
Write-Host 'Build conectado verificado localmente. Nenhum upload, release ou distribuicao realizado.'
