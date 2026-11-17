# GERENCIADOR FINAL — Piloto funcional Drivers WUA F1
Data: 14/11/2026. Branch experimental `experimento-atualizador-drivers-wua-20261114`, proveniente do SHA aprovado G1.23 `5335d3693021e690526e3d03b43d50881da05fd8`. A PR #79 e main permanecem intocadas.

**Objetivo:** construir o caminho real de atualização de drivers, em vez de repetir a série G1 de diagnósticos.

**Entrega F1:** o novo `DriverUpdatePilot.cs` define seleção de oferta WUA por UpdateID/revisão/HardwareID/DeviceID globalmente único, bloqueio de dados parciais, autorização explícita e específica da oferta nos cinco minutos anteriores, restrição temporária a classes Image/Printer/Ports em ambiente descartável, revalidação antes e depois do download, exigência de journal durável antes de instalar, não repetição automática em falhas, reinício pendente sem reinício automático e verificação PnP pós-instalação. `TestesDrivers/DriverUpdatePilotTests.cs` usa apenas backend/journal sintéticos e exercita falhas, cancelamento, consulta modificada, falta de intenção durável, reinício e retorno positivo sem mudança PnP.

**Status funcional:** NÃO é ainda um instalador utilizável. Nesta F1 não existe backend WUA nativo de download/instalação, journal persistente em disco nem ligação com os botões da interface. Todos os testes são simulados; as antigas flags consultivas CanInstall/CanMaintain/IsNewerProven/CompletePackageVerified permanecem false.

**Próximas entregas focalizadas, não auditorias infinitas:** F2 implementar WUA nativo com busca de UpdateID/revisão e revalidação da instância, journal durável e testes negativos COM; F3 conectar fluxo com confirmação na UI e testar download/instalação de driver NÃO CRÍTICO em Windows descartável, com autorização separada, validação pós-instalação, erros, reinício opcional. Só depois cogitar PC FORTE mediante autorização explícita e evidência física.

**Gate F1:** CIs Windows e integridade no mesmo SHA, Windows Server 2022/2025 24/24 G1.9 por runner, G1.10–G1.23 e novo TestesDrivers, 36/36 mutações, segurança/consentimento, x64. Nenhum novo teste deve instanciar COM ou baixar/instalar drivers.

Proibições mantidas nesta fase: main, PR #79, G5 autoatualizador, D1, Worker, Cron, Secrets, BETA, releases, PCs pessoais. Roadmap formal 27/42, G1–G5 abertos até homologação real.
