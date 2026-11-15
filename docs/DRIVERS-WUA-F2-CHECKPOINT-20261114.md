# GERENCIADOR FINAL — Atualizador de Drivers WUA F2 (backend nativo experimental)

Data: 14/11/2026, PR #80 Draft, branch experimento-atualizador-drivers-wua-20261114, baseada em PR #79 G1.23 SHA 5335d3693021e690526e3d03b43d50881da05fd8.

## Implementacao F2

- \`DriverWuaNativeBackend.cs\`: adaptador WUA com STA unica por processo (nenhuma concorrencia), pesquisa online dirigida ao Windows Update e somente tipo Driver, seleção **por UpdateID/revisão exatos**, comparação de HardwareID, título, fornecedor, classe, estado instalado, assinatura/presença/INF/versão WMI atuais, EULA já aceita, beta/hidden=false, zero atualizações adicionais em bundle e modo sem interação. Nenhuma aceitação automática de EULA, serviço novo, Force, conteúdo por URL ou staging próprio.
- \`Download\` usa \`Microsoft.Update.Session.CreateUpdateDownloader\`, apenas 1 oferta, verifica \`ResultCode\` geral e por update. Após download, \`DriverUpdatePilot.Execute\` revalida antes de chamar \`Install\`. \`Install\` refaz o preflight na STA, usa \`CreateUpdateInstaller\`, \`IsForced=false\`, \`AllowSourcePrompts=false\`, só considera sucesso quando WUA e o resultado da atualização retornam sucesso. Nunca invoca reboot, rollback ou desinstalação. Falha COM após começo de Install é inconclusiva e não autoriza retry.
- \`Observe\` faz novo WMI PnP na mesma DeviceId; se não houver alteração de INF/versão e estado aceitável, não confirma atualização.
- \`DriverUpdatePilotFileJournal.cs\` grava intenção **antes** de instalar com lock interinstância mantido, escrita em arquivo temporário com flush e File.Move atômico, limite 4 KB e hash SHA-256 para detectar alteração acidental. Pendência, estado incerto, reinício e JSON adulterado impedem repetir. O journal é distinto do histórico v1. Os testes usam SOMENTE caminho temporário aleatório exclusivo.
- Testes \`DriverUpdatePilotJournalTests\`: bloqueio concorrente, resultado incerto, reinício pendente, integridade de bytes, arquivo malformado e liberação após verificação, além dos testes F1 de fluxo inteiramente sintético.

## Limites de prova e liberação

O adaptador compila em Windows se aprovado na CI, porém **nenhuma chamada nativa de download ou instalação foi executada nesta fase**. Ainda não há conexão com o botão da interface e não existe homologação em VM descartável. A classe pode ser invocada somente por chamada interna futura. O estado \`IsNewerProven=false\` continua válido para oferta consultiva — sucesso WUA não é prova independente da ordem de versões, e o produto não deve prometer isso.

Antes da liberação: auditoria técnica independente do backend COM (inclusive lifecycle de RCW, failure e ranking), seleção manual/consentimento real na UI, testes de pacote não crítico em Windows descartável, caso pendente/reboot e recuperação supervisionada, checagem de fontes oficiais e integridade WUA em ambiente real. A extensão de classes críticas deve ter contrato separado. Nenhum PC pessoal, main, PR #79, G5, D1, Worker, Cron, Secrets, BETA ou release modificado. Roadmap 27/42; G1–G5 abertos.

## Gates F2

Windows 2022/2025: 24/24 G1.9 por runner, TestesDrivers F1/F2, WinForms G1.10–G1.23, 36/36 mutações, regressões gerais/visuais, privacidade/consentimento, x64. Integridade externa na mesma revisão. CI não executa COM WUA novo, download ou Install.
