# GERENCIADOR FINAL — Drivers G1.21: auditoria adversarial de fotografia publicada

Data: 14/11/2026 · PR #79 aberta/Draft/sem merge; branch drivers-pnp-problemas-consulta-20261113; base candidata-portatil-pessoal-pc-forte-20261101. Roadmap 27/42; G1–G5 abertos.

## Gate de entrada G1.20 confirmado
SHA e97b62a3dc419914d16c38f98a9cdd85bf74a3ab. Windows 43645877108 e integridade 43645877119 completed/SUCCESS, attempt 1, mesmo SHA. Win 2022/2025, 24/24 G1.9 por runner, G1.10–G1.20 WinForms, 36/36 mutações negativas, segurança/privacidade, consentimento, x64, regressões gerais/visuais e integridade aprovados. Comentário final PR #6884984190.

## Contraexemplo G1.21 — congelamento só para o gravador
DriversPlaceholder.RunRefresh em G1.20 produzia historyInventory por cópias shallow de cada registro, mas atribuía snapshot = result.Items e lastResult = result. Uma fonte sintética mutável podia alterar a coleção, HardwareIds, WindowsEvents, Store, publishedPackages e WindowsHistory após a publicação e antes da persistência, de modo que reentrância dos filtros e posterior investigação lessem evidência diferente do histórico antigo congelado. A captura WMI real tipicamente devolve arrays criados pelo GF; isto é um limite de integridade da fronteira de publicação, não prova de exploração em um PC pessoal.

## Reprodução isolada (ainda sem correção de produção)
Novo TestesLayout/DriverG121PublicationFreezeTests.cs usa WinForms real, duas linhas de dispositivo originais com evidência Store/INF e histórico Windows, coleções sintéticas mutáveis e tarefa de gravação bloqueada por TaskCompletionSource. Após publicação, substitui/limpa a coleção, altera identidade, HardwareIds, WindowsEvents e evidências Store/Published/History, reexecuta filtros, pesquisa, seleção e modal somente leitura; depois libera gravação em diretório temporário exclusivo. Deverá detectar se lista, relatório ou histórico divergirem. Não usa WMI/WUA/SetupAPI/INF nativos nem DriverHistory.DefaultPath. Não habilita ações de manutenção.

## Gate G1.21
PENDENTE: executar primeiro a fixture adversarial sem correção, registrar falha literal e somente então aplicar a menor modificação. CI final deverá comprovar Windows 2022/2025, 24/24 G1.9 por runner, G1.10–G1.21 WinForms, 36/36 mutações negativas, testes visuais/gerais, segurança/privacidade, consentimento, compilação Windows x64 e integridade externa completed/SUCCESS no MESMO SHA.

Invariantes: CanInstall=false, CanMaintain=false, IsNewerProven=false, CompletePackageVerified=false. Schema v1, SHA-256, lock, 2 MB, atomicidade, retenção 20, monotonicidade, G1.9–G1.20 e invalidação de geração intactos. Sem instalação/download/remoção/elevação/reinício, main/G5/D1/Worker/Cron/Secrets/BETA/releases/PCs pessoais intocados. Não alegar DPI físico/leitor de tela real. GO só após duas CIs concluídas.
