# GERENCIADOR FINAL — F3: interface WUA experimental e fail-closed

Data: 14/11/2026, PR #80 Draft, branch experimento-atualizador-drivers-wua-20261114, base drivers-pnp-problemas-consulta-20261113. G1.23 aprovado: SHA 5335d3693021e690526e3d03b43d50881da05fd8. F2 SHA anterior: 5723f15b7d9cf89dbf2841a89a44b734c43253ee, Windows 43658567996 e integridade 43658567995 SUCCESS no mesmo SHA.

## Escopo e desenvolvimento
- DriverUpdatePilotDialog.cs implementa uma janela real WinForms com título explícito experimental, lista pesquisável de ofertas WUA vinculadas por UpdateID/revisão/DeviceId/HardwareID/fornecedor, confirmação individual e de Windows descartável, status/tooltip/acessibilidade, cancelamento e estados finais. Na versão normal, SEM backend/journal injetados, o botão de instalação é sempre **desabilitado** e a tela é apenas revisão consultiva.
- O diálogo de investigação existente ganha **LinkLabel** para revisão piloto após consulta oficial completa e seleção vinculada; os três botões e seus guards/semântica consultiva permanecem. O link invalida com mudanças de geração e não autoriza instalação.
- DriverUpdatePilot.Execute classifica exceções de revalidação inicial e posterior, ausência de journal e falha na gravação do desfecho, com estado fail-closed. Valida invalidação do diálogo após download e antes de iniciar Install. Reporta estágios sem transformar callbacks em autoridade. Depois do início de instalação não simula cancelamento do COM síncrono.
- DriverWuaNativeBackend.StartSta libera a exclusividade antes de concluir o Task, corrigindo corrida da F2. Liberação COM malsucedida agora impede tratar conclusão nativa como segura. Atualização de classes não permitidas permanece bloqueada. Testes nativos NÃO executados.
- Journal continua isolado do histórico de consultas v1; novo teste WinForms usa somente caminho temporário exclusivo em sandbox descartável, e não DriverHistory.DefaultPath.
- TestesDrivers expandido para exceções nas duas revalidações, erro journal.TryBegin/Mark, revogação depois do download; TestesLayout inclui testes WinForms F3 nas resoluções 500×340, 790×510, 1200×720, 1450×820, pesquisa/seleção/consentimento, atraso de download, cancelamento, descarte e retorno tardio, sem WUA/COM/SetupAPI/INF nativos.

## Limites obrigatórios
A UI real **NÃO está armada para instalar em PC pessoal**. O backend nativo permanece experimental, ainda sem homologação física em VM Windows descartável. Nem compilação nem simulação provam elegibilidade real do pacote, aumento de versão, estabilidade da instalação, recuperação, permissões ou comportamento COM real. O protocolo WUA sincronico pode continuar mesmo quando um WaitAsync/UI é cancelado. O arquivo journal com SHA-256 detecta inconsistência acidental, não constitui assinatura autenticada da origem.

Para homologação VM: imagem Windows descartável isolada, snapshot externo restaurável, dispositivo não crítico e atualização realmente disponível pelo Windows Update, consentimento específico de execução na VM, journal durável com ACL adequada, ensaios de download/Install/reinício/erro sem afetar PC FORTE/FRACO; validação do mesmo DeviceId e INF/versão depois. Até isso, NO-GO para atualização de drivers em computador pessoal e para distribuição como instalador pronto.

Roadmap 27/42 e G1–G5 abertos; PR #79 e main intocadas; G5 autoatualizador, D1, Worker, Cron, Secrets, BETA, releases e PCs pessoais preservados. Nenhuma alteração de política, elevação, download, staging, instalação, rollback, limpeza ou reinício na F3.

## Gate
CI Windows 2022/2025 com 24/24 G1.9 por runner, WinForms G1.10–G1.23 + F3, 36/36 mutações negativas, regressões gerais/visuais, segurança/privacidade, consentimento, compilação x64, integridade externa no mesmo SHA. Atestação exclusiva main skipped. Checkpoint versionado ANTES da CI; comentar resultado final na PR após verificar ambos completed/SUCCESS, sem novo commit documental pós-gate.
