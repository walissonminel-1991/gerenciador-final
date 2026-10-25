$ErrorActionPreference = 'Stop'
# Controle negativo: apenas uma cópia descartável recebe a implementação antiga.
# Compilação com erro ou modo não executado não é evidência de regressão detectada.
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('GF-Lote4-' + [guid]::NewGuid().ToString('N'))
try {
    git worktree add --detach $temporary HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar checkout descartável' }
    $legacy = git show '8f8d1c8fb109ba4956876b398f02dee6ce70f2e3:Program.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Fonte anterior indisponível' }
    $legacy | Set-Content (Join-Path $temporary 'Program.cs') -Encoding utf8
    Push-Location $temporary
    try {
        $output = dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --lote4 2>&1
        $result = $LASTEXITCODE
        $output | Write-Output
        if ($result -eq 0 -or -not ($output -match 'System.Exception: L4 relatório local usa rolagem nativa')) {
            throw 'Controle negativo não detectou o defeito visual esperado'
        }
        Write-Output 'PASS L4 controle negativo: relatório anterior recusado após compilação e abertura do diálogo real'
        git restore --source=HEAD -- Program.cs
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar diálogo atual na cópia descartável' }
        $legacyDock = git show '8f8d1c8fb109ba4956876b398f02dee6ce70f2e3:DarkControls.cs'
        if ($LASTEXITCODE -ne 0) { throw 'Fonte de layout anterior indisponível' }
        $legacyDock | Set-Content 'DarkControls.cs' -Encoding utf8
        $output = dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --lote4 2>&1
        $result = $LASTEXITCODE
        $output | Write-Output
        if ($result -eq 0 -or -not ($output -match 'System.Exception: L4 editor cobre a barra de rolagem escura')) {
            throw 'Controle negativo não detectou a sobreposição esperada'
        }
        Write-Output 'PASS L4 controle negativo: sobreposição do editor e barra anterior recusada'
    } finally { Pop-Location }
} finally {
    if (Test-Path $temporary) {
        git worktree remove --force $temporary
        if ($LASTEXITCODE -ne 0) { throw 'Limpeza do checkout descartável falhou' }
    }
}
