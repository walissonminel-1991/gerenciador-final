$ErrorActionPreference = 'Stop'
# Controle negativo: apenas uma cópia descartável recebe a implementação antiga.
# Compilação com erro ou modo não executado não é evidência de regressão detectada.
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('GF-Lote4-' + [guid]::NewGuid().ToString('N'))
try {
    git worktree add --detach $temporary HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar checkout descartável' }
    $legacy = git show '8f8d1c8fb109ba4956876b398f02dee6ce70f2e3:Program.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Fonte anterior indisponível' }
    # Trocar somente o método antigo, não o Program.cs inteiro: WORK14-17
    # acrescenta propriedades de ProgramEntry exigidas pelo restante do projeto.
    $target = Join-Path $temporary 'Program.cs'
    $current = [IO.File]::ReadAllText($target)
    $legacyText = ($legacy | Out-String)
    $marker = '    private void ShowLocalReliabilityReport()'
    $boundary = '    private static Button CreateSecondaryButton('
    $s1 = $current.IndexOf($marker, [StringComparison]::Ordinal)
    $e1 = $current.IndexOf($boundary, $s1, [StringComparison]::Ordinal)
    $s2 = $legacyText.IndexOf($marker, [StringComparison]::Ordinal)
    $e2 = $legacyText.IndexOf($boundary, $s2, [StringComparison]::Ordinal)
    if ($s1 -lt 0 -or $e1 -lt 0 -or $s2 -lt 0 -or $e2 -lt 0 -or
        $current.IndexOf($marker, $s1 + 1, [StringComparison]::Ordinal) -ge 0) {
        throw 'Âncoras exclusivas do método antigo ausentes: não prosseguir'
    }
    $regressed = $current.Substring(0, $s1) +
                 $legacyText.Substring($s2, $e2 - $s2) +
                 $current.Substring($e1)
    [IO.File]::WriteAllText($target, $regressed, [System.Text.UTF8Encoding]::new($false))
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
