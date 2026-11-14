# GERENCIADOR FINAL — Drivers G1.21: auditoria adversarial de fotografia publicada

Data: 14/11/2026 · PR #79 aberta/Draft/sem merge; branch drivers-pnp-problemas-consulta-20261113; base candidata-portatil-pessoal-pc-forte-20261101. Roadmap 27/42; G1–G5 abertos.

## Gate de entrada G1.20 confirmado
SHA e97b62a3dc419914d16c38f98a9cdd85bf74a3ab. Windows 43645877108 e integridade 43645877119 completed/SUCCESS, attempt 1, mesmo SHA. Win 2022/2025, 24/24 G1.9 por runner, G1.10–G1.20 WinForms, 36/36 mutações negativas, segurança/privacidade, consentimento, x64, regressões gerais/visuais e integridade aprovados. Comentário final PR #6884984190.

## Contraexemplo G1.21 — congelamento só para o gravador
DriversPlaceholder.RunRefresh em G1.20 produzia historyInventory por cópias shallow de cada registro, mas atribuía snapshot = result.Items e lastResult = result. Uma fonte sintética mutável podia alterar a coleção, HardwareIds, WindowsEvents, Store, publishedPackages e WindowsHistory após a publicação e antes da persistência, de modo que reentrância dos filtros e posterior investigação lessem evidência diferente do histórico antigo congelado. A captura WMI real tipicamente devolve arrays criados pelo GF; isto é um limite de integridade da fronteira de publicação, não prova de exploração em um PC pessoal.

## Reprodução isolada (ainda sem correção de produção)
Novo TestesLayout/DriverG121PublicationFreezeTests.cs usa WinForms real, duas linhas de dispositivo originais com evidência Store/INF e histórico Windows, coleções sintéticas mutáveis e tarefa de gravação bloqueada por TaskCompletionSource. Após publicação, substitui/limpa a coleção, altera identidade, HardwareIds, WindowsEvents e evidências Store/Published/History, reexecuta filtros, pesquisa, seleção e modal somente leitura; depois libera gravação em diretório temporário exclusivo. Deverá detectar se lista, relatório ou histórico divergirem. Não usa WMI/WUA/SetupAPI/INF nativos nem DriverHistory.DefaultPath. Não habilita ações de manutenção.

## Reprodução real anterior à correção
- SHA apenas de teste `0f6c204b43cadd72b4385384c4775d06967eef3b`, [CI Windows 43647662456](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43647662456): regressão WinForms **FAILURE** em `G1.21: mutacao da colecao fonte alterou a lista publicada`, depois de G1.10–G1.20 SUCCESS. A fixture esvaziou e substituiu o `List<DriverInventoryEntry>` original durante uma gravação bloqueada; ao acionar o filtro "Todos" o painel reconstruiu a lista a partir da origem alterada. Esta é uma reprodução verificável; não é erro de timeout/infra. Os erros seguintes de upload WORK14/16/17/18 decorrem do término antecipado da regressão, não comprovam falha própria dessas matrizes.
- **Correção mínima** em `DriversPlaceholder.cs`: criar um `DriverInventoryResult` congelado **antes da publicação**, copiando `Items`, `HardwareIds`, `WindowsEvents`, `Store.Packages`, `PublishedPackages.Packages` e `WindowsHistory.Events`. Publicar essa única fotografia como `snapshot`, `lastResult` e fonte de `historyInventory`; todos os filtros, seleção, guard, relatório e histórico passam a usar os mesmos valores. Os registros de evidência são `record` imutáveis. O provider original não é alterado.
- O congelamento não é uma transação interthread atômica durante a cópia de origem; o capturador WMI real retorna coleções materializadas antes de publicar. Os testes reproduzem **mutação após publicação**, não declaram proteção absoluta para todos os escritores concorrentes ou homologação física DPI/leitor de tela.

## Gate G1.21
PENDENTE: executar primeiro a fixture adversarial sem correção, registrar falha literal e somente então aplicar a menor modificação. CI final deverá comprovar Windows 2022/2025, 24/24 G1.9 por runner, G1.10–G1.21 WinForms, 36/36 mutações negativas, testes visuais/gerais, segurança/privacidade, consentimento, compilação Windows x64 e integridade externa completed/SUCCESS no MESMO SHA.

Invariantes: CanInstall=false, CanMaintain=false, IsNewerProven=false, CompletePackageVerified=false. Schema v1, SHA-256, lock, 2 MB, atomicidade, retenção 20, monotonicidade, G1.9–G1.20 e invalidação de geração intactos. Sem instalação/download/remoção/elevação/reinício, main/G5/D1/Worker/Cron/Secrets/BETA/releases/PCs pessoais intocados. Não alegar DPI físico/leitor de tela real. GO só após duas CIs concluídas.
