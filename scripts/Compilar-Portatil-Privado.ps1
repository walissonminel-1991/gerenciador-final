param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-f0-9]{40}$')][string]$ExpectedSourceCommit,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'Windows e PowerShell 7 exigidos' }
foreach ($command in @('git','dotnet','python')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Ferramenta ausente: $command" }
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$head = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $ExpectedSourceCommit) { throw 'HEAD divergente da fonte autorizada' }
$dirty = & git -C $repo status --porcelain --untracked-files=all
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Fonte nao limpa; nenhuma compilacao autorizada' }

[xml]$project = Get-Content -LiteralPath (Join-Path $repo 'GerenciadorAplicativos.csproj') -Raw
$versions = @($project.SelectNodes('//PropertyGroup/Version'))
if ($versions.Count -ne 1 -or $versions[0].InnerText -notmatch '^[0-9]+([.][0-9]+){3}$') {
    throw 'Versao ausente, invalida ou ambigua'
}
$version = $versions[0].InnerText
$name = "GerenciadorFinal-v$version-$($head.Substring(0,8))-TESTE-PRIVADO-win-x64"
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path $repo -Parent) 'GF-Testes-Privados' }
$root = [IO.Path]::GetFullPath($OutputDirectory)
if ($root.TrimEnd('\') -ieq $repo.TrimEnd('\') -or
    $root.StartsWith($repo.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Saida deve ficar fora da arvore fonte'
}
$null = [IO.Directory]::CreateDirectory($root)
$zip = Join-Path $root ($name+'.zip')
$hashFile = $zip+'.sha256'
if ((Test-Path -LiteralPath $zip) -or (Test-Path -LiteralPath $hashFile)) {
    throw 'Pacote ou hash anterior preservado; use outra pasta de saida'
}
$stage = Join-Path $root ('.build-'+$name+'-'+[Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $stage
$source = Join-Path $stage 'fonte'
$dist = Join-Path $stage 'Distribuicao'
$extracted = Join-Path $stage 'Conferencia-ZIP'
$tempZip = Join-Path $stage 'pacote.tmp.zip'
& git -C $repo worktree add --detach $source $ExpectedSourceCommit
if ($LASTEXITCODE -ne 0) { throw 'Checkout isolado falhou' }
Push-Location -LiteralPath $source
try {
    $actual = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actual -cne $ExpectedSourceCommit) { throw 'Checkout isolado divergente' }
    & python -c "import sys; assert sys.version_info >= (3,11)"
    if ($LASTEXITCODE -ne 0) { throw 'Python 3.11 ou posterior exigido' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $publishArgs = @('publish','GerenciadorAplicativos.csproj','-c','Release','-r','win-x64','--self-contained','true',
        '-p:GfBetaProfile=local','-p:GfBetaEndpoint=','-p:PublishSingleFile=false',
        '-p:DebugType=None','-p:DebugSymbols=false',"-p:GfSourceCommit=$actual",'-o',$dist)
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'Compilacao falhou' }
    & ./TestesManifesto/Testar-Ativacao.ps1 -ExecutablePath (Join-Path $dist 'GerenciadorFinal.exe')
    $defines = @($project.SelectNodes('//PropertyGroup/DefineConstants') | ForEach-Object { $_.InnerText -split ';' })
    $beta = $defines -contains 'GF_BETA'
    if ($beta) {
        & dotnet build TestesTelemetria/TestesTelemetria.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falha no verificador BETA existente' }
        $inspectArgs = @('run','--project','TestesTelemetria/TestesTelemetria.csproj','-c','Release','--no-build','--',
            '--inspect-local-beta-build',(Join-Path $dist 'GerenciadorFinal.dll'),$actual)
        $report = & dotnet @inspectArgs
        if ($LASTEXITCODE -ne 0) { throw 'Perfil local nao confirmado' }
        $meta = ($report -join [Environment]::NewLine) | ConvertFrom-Json
        if ($meta.transport_configured -ne $false -or $meta.endpoint -ne '' -or $meta.source_commit -cne $actual) {
            throw 'Transporte remoto ou origem inesperados'
        }
        $report | Set-Content -LiteralPath (Join-Path $dist 'BETA-BUILD.json') -Encoding utf8
    }
    @(
        'Produto: Gerenciador Final'
        'Perfil BETA: local; TESTE PRIVADO - HOMOLOGACAO FISICA PENDENTE'
        "Versao: $version"
        "Commit Git: $actual"
        "Commit fonte: $actual"
        'Base da PR (quando aplicavel): '
        'Assinatura digital: AUSENTE (pacote experimental)'
        'Status AVG: NAO VALIDADO; nao executar se estiver em quarentena'
        'Origem: https://github.com/walissonminel-1991/gerenciador-final'
    ) | Set-Content -LiteralPath (Join-Path $dist 'BUILD-INFO.txt') -Encoding utf8
    $files = @(Get-ChildItem -LiteralPath $dist -Recurse -Force -File | Sort-Object FullName)
    $lines = foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($dist,$file.FullName).Replace('\','/')
        "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
    }
    $lines | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding utf8
    $verifyArgs = @('scripts/verificar_pacote.py',$dist,'--version',$version,'--source',$actual,'--build',$actual)
    if ($beta) { $verifyArgs += '--beta' }
    $verification = & python @verifyArgs
    if ($LASTEXITCODE -ne 0) { throw 'Pacote local reprovado' }
    $verification | Set-Content -LiteralPath (Join-Path $stage 'Verificacao-fonte.json') -Encoding utf8
    [IO.Compression.ZipFile]::CreateFromDirectory($dist,$tempZip,[IO.Compression.CompressionLevel]::Optimal,$false)
    [IO.Compression.ZipFile]::ExtractToDirectory($tempZip,$extracted)
    $verifyArgs[1] = $extracted
    $zipVerification = & python @verifyArgs
    if ($LASTEXITCODE -ne 0) { throw 'Conteudo extraido do ZIP reprovado' }
    $zipVerification | Set-Content -LiteralPath (Join-Path $stage 'Verificacao-ZIP.json') -Encoding utf8
    if (($verification -join [Environment]::NewLine) -cne ($zipVerification -join [Environment]::NewLine)) { throw 'Resultados de verificacao divergentes' }
    $zipHash = (Get-FileHash -LiteralPath $tempZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $stagedHash = Join-Path $stage 'ZIP.sha256'
    "$zipHash  $($name+'.zip')" | Set-Content -LiteralPath $stagedHash -Encoding utf8
    # Nenhuma sobrescrita: File.Move sem overwrite falha se o destino existe.
    [IO.File]::Move($stagedHash,$hashFile)
    [IO.File]::Move($tempZip,$zip)
    if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -cne $zipHash) {
        throw 'Hash do ZIP publicado divergente'
    }
    Write-Host "ZIP privado verificado: $zip"
    Write-Host "SHA-256: $zipHash"
    Write-Host "Evidencias locais: $stage"
    Write-Host 'Nao assinado; manter AVG ativo. Somente homologacao fisica autorizada; nenhum upload.'
} finally {
    Pop-Location
}
