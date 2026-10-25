# WORK15 — cobertura de busca Microsoft sem promover permissões silenciosas

Data: 25/10/2026. Fonte: PR #50 (WORK14), sem alterações em `main`.

## Pesquisa externa (fontes oficiais)
- [WinGet: suporte EXE/ZIP/INNO/NULLSOFT/MSI/WIX/APPX/MSIX/BURN/PORTABLE](https://learn.microsoft.com/en-us/windows/package-manager/winget/). Cobertura do gerenciador de pacotes é maior que o subconjunto executável do GF. Nem todo formato tem instalação silenciosa segura para qualquer produto.
- [Comandos search, list e upgrade](https://learn.microsoft.com/en-us/windows/package-manager/winget/upgrade): ID e origem exatas removem ambiguidade; `--silent` existe, mas **não** deve ser executado genericamente pelo GF com base só no nome.
- [Get-WinGetPackage](https://github.com/microsoft/winget-cli/blob/master/src/PowerShell/Help/Microsoft.WinGet.Client/Get-WinGetPackage.md): API estruturada lista pacotes WinGet e ARP/MSIX; entradas ARP/MSIX sem correlato ficam identificadas com IDs artificiais; requer módulo próprio. O backend COM/CLI do GF segue desligado em produção até revisão específica de permissões, custo e impactos.
- [WinGet installer schema](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.7.0/installer.md): instaladores publicam switches silenciosos, mas pacotes e metadados **não** substituem validação de assinatura, hash, identidade, arquitetura, escopo, processos e pós-instalação.
- [Chocolatey upgrade](https://docs.chocolatey.org/docs/commandsupgrade/): `choco upgrade` pode instalar se o programa estiver ausente; portanto, proibido delegar genericamente um upgrade de inventário ao gerenciador externo sem prova de identidade. [Scoop manifests](https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate) são úteis como pista consultiva de aplicações portáteis, não como autorização de executar comandos remotos.

## Gargalo identificado na versão .60
`AutomaticUpdateDiscovery.QueryAsync` só oferecia um resultado quando uma instalação Win32 podia ser integralmente ligada à receita executável (manifesto, PE, mecanismo, arquitetura e escopo). O índice HTTPS assinado da Microsoft já podia confirmar **uma versão publicada e uma identidade catalogada** mesmo quando a instalação local não passava nesses pré-requisitos. Assim, a interface descartava versões informativas e marcava muitas entradas como sem correspondência.

Outra restrição anterior: `PackageCatalogIndex.Find` exige nome de produto compatível, preservando o bloqueio de falsos positivos. Para entradas MSI, o mesmo índice tem ProductCode de registro. Podemos descobrir uma correspondência **consultiva** baseada em ProductCode válido, fornecedor exato e ID de pacote único sem converter essa descoberta em autorização.

## Mudança implementada — PR WORK15
- `PackageCatalogIndex.FindForReviewByProductCode`: pesquisa local SQLite parametrizada somente para GUID MSI validado e id único + editor exato no MSIX Microsoft assinado. Não baixa instalador nem gera receita. Mantém `Find` estrito inalterado como porta de execução.
- `AutomaticUpdateDiscovery.QueryAsync`: percorre instalações aptas à *consulta*, publica versão mais nova do catálogo mesmo quando não há contrato de instalação silenciosa; se houver prova executável estrita, mantém o fluxo da .60. Evidência apenas consultiva usa `catalog-version-only`.
- `MultiSourceMatcher.Merge`: evidencia `catalog-version-only` com prioridade inferior às fontes mais fortes e `RequiresReview=true`; conflito entre fontes continua bloqueando execução. Sem permissão efêmera, nenhuma atualização automática é iniciada.
- UI e diagnóstico: distinguem instaladores comprovados de versões apenas consultivas, sem nomes privados em logs.
- Testes com índice SQLite descartável: identidade por nome continua estrita; ProductCode só permite observação com fornecedor e ID únicos; versão maior, versão desconhecida e falsos positivos são diferenciados; candidato consultivo mantém `RequiresReview`.

## Limites e próximos incrementos para ampliar o número executável
1. Medir no PC FORTE e no PC FRACO `antes/depois`: inventário, candidatos de atualização, candidatos só consultivos, razões de incompatibilidade e contratos de desinstalação. As 102 entradas sem correspondência da .59 não são uma estimativa de quantas a nova implementação recuperará.
2. Pesquisar e **pilotar isoladamente** o `Microsoft.WinGet.Client`/COM com consentimento explícito, no modo apenas leitura, preferência por saída tipada, sem scraping localizado da CLI, sem alteração de fontes nem aceitação automática de licenças; manter WinGet desligado por padrão até homologação.
3. Ampliar os contratos executáveis por motor instalador (MSI, NSIS, Inno; avaliar Burn/portátil como projetos separados) com amostras reais assinadas, modelo de identidade, assinatura do binário do fornecedor, consentimento, estado pós-execução e bloqueio de repetição.
4. Desinstalação: o GF já usa o comando registrado, MSI ProductCode, QuietUninstallString verificado e motores NSIS/Inno com evidências. Não inventar desinstalador para entradas MSIX/Store, drivers, jogos e componentes do Windows; quando a entrada é somente leitura, só oferecer remoção pelo gerenciador original ou fluxo interativo oficial conforme cabível.
5. Qualquer fonte nova de Scoop/Chocolatey/RuckZuck é **sinal de versão**, não regra dinâmica de execução, de scripts ou exclusão. Não ampliar poder destrutivo para inflar métricas.

## Gate de lançamento
Até CI Windows e os testes físicos serem aprovados, PR Draft e versão TESTE. U8/R7/R4 permanecem abertos; roadmap não deve ganhar checkpoints com simulações apenas. Sem assinatura GF/AVG validado, sem promoções a `main` e sem exclusões residuais reais.
