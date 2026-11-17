# GERENCIADOR FINAL — Drivers UX2: diagnóstico PnP e motivos WUA
Data: 17/11/2026. PR #80 Draft, base `156b780d64d3e6a02941178b60a9204bec17b722`.
- Orientações consultivas para PnP 10, 22, 28, 31, 43 e 52; demais estados tratados sem inventar causa ou solução.
- Tooltip, acessibilidade da lista virtualizada e relatório exibem orientação não prescritiva, sem alterar PnP/WMI.
- Relatório WUA lista totais e explica o primeiro bloqueio observável do piloto: consulta parcial, origem não comprovada, classe não permitida, ausência de metadados/identidade inequívoca ou gates conservadores incompletos.
- `DriverUpdatePilot.TryPrepare` permanece **única autoridade do candidato pré-download**, inalterada. Não há promoção para instalação real, comparação de versões ofertadas, download, reinício ou EULA.
- Testes sintéticos de PnP e WUA integrados à suíte TestesDrivers. Exigir CIs Windows + integridade no mesmo SHA, gates G1.9–G1.23, F1–F4.2, 36/36 mutações e x64.
- PC FRACO continuará reservado para testes manuais de leitura e simulação, após versão portátil candidata aprovada; nenhum teste instala driver.
