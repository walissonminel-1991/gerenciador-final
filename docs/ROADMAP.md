# GERENCIADOR FINAL — progresso verificável

## Continuidade — diagnóstico v5.13.11.3 TESTE (20/10/2026)

Estado confirmado: `main` na v5.13.11.0 candidata; PR #38 com coleta local e PR #40 com revisão pública no GitHub. O CI Windows da v5.13.11.2 passou (run 39444626580). Nenhuma dessas evidências comprova testes nos dois PCs nem resolve o AVG.

- [x] Consentimento local e formulário público manual implementados na branch do PR #40.
- [x] Corrigidas retomada de pendência e interrupção da coleta ao revogar com falha de disco; testes adicionados na v5.13.11.3.
- [ ] Aprovar compilação e regressões Windows da v5.13.11.3.
- [ ] Simular novo participante: cadastro/login GitHub no navegador, recusa, aceite, coleta, revisão, cancelamento/publicação manual e revogação. Conferir também reinício e relatório anterior.
- [ ] Validar AVG e experiência nos dois PCs antes de publicação estável.
- [ ] Implementar triagem/agrupamento e PRs por IA com testes e revisão. Ainda não há manutenção autônoma, envio automático ou exclusão garantida de Issues públicas.

O marcador original permanece **30/45 checkpoints (67%)**. Este complemento não altera checkpoints dependentes de hardware real.


## Complemento — v5.13.11.2 TESTE: piloto GitHub-only

Com a conta GitHub do colaborador, o aplicativo oferece formulário pré-preenchido e revisável para Issues públicas, exclusivamente com categorias técnicas aprovadas. Publicação exige confirmação no navegador, não há credenciais embutidas. A próxima fase exige uma GitHub App registrada com permissões limitadas, consentimento separado para envio e revisão de privacidade. Veja [PILOTO-DIAGNOSTICO-GITHUB.md](PILOTO-DIAGNOSTICO-GITHUB.md).

## Complemento de desenvolvimento — v5.13.11.1 TESTE: diagnóstico local voluntário

Uma nova frente de privacidade foi iniciada com opção de consentimento, eventos agregados em allowlist, um relatório local rotativo e revogação. **Envio remoto não está implantado**; sua ativação exigirá consentimento separado e avaliação de segurança. Consulte [DIAGNOSTICO-COLABORATIVO.md](DIAGNOSTICO-COLABORATIVO.md). Isso não altera os 30/45 checkpoints originais.

## Complemento de desenvolvimento — v5.13.11.0 CANDIDATA (20/10/2026)

O marcador original de 30/45 checkpoints (67%) permanece inalterado: testes de código e aprovação em hardware real não são equivalentes. A partir de v5.13.10.19, novas entregas em branches de desenvolvimento incluem revisão de resíduos do Driver Booster, diagnóstico de AVG/MSIX, painel de RAM e otimização conservadora de cache do próprio aplicativo, SHA-256 e identificação versionada de pacotes.

A candidata v5.13.11.0 acrescenta **quatro fontes oficiais verificadas** (ShareX, HandBrake, Rufus e qBittorrent), totalizando 18 fontes oficiais de versões com identidade vinculada, além do catálogo RuckZuck. Inclui bateria automatizada consolidada de verificações de fontes, identidade, atualização, segurança e UI. As fontes novas são apenas consultivas; não constituem receitas de instalação nem conferem assinatura dos pacotes.

**Pendências críticas antes de publicação estável:** avaliação de `IDP.Generic` pelo AVG; revisão e execução no PC FRACO e segundo computador; variação de leitura MSIX; divergências reais de WinGet; cobertura, qualidade e reversão de drivers; assinatura Authenticode.


Base: 5.13.10.18 TESTE · 2026-10-20

Percentual = checkpoints concluídos / checkpoints listados. Pesos iguais; não mede horas, cobertura universal ou aprovação final. Implementação e testes reais são checkpoints distintos.

Código desta rodada: `5171c895725e779df3ccba8d54a8faf56735950d`

CI: success — https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045

## Entregas deste lote

- Confirmação de atualização vinculada à instalação escolhida; validação adicional de arquitetura e escopo do SumatraPDF.
- Diagnóstico agregado dos motivos de cobertura, sem nomes de aplicativos nos novos registros de diagnóstico.
- Recusa de confirmação de ausência quando a leitura do Registro falha; evidência explícita de inventário parcial na auditoria histórica.
- Correção da avaliação de desinstalador ausente e consideração da alternativa silenciosa.
- Aba DRIVERS com consulta local sob demanda, fornecedor, versão, INF, presença e assinatura informada pelo Windows.
- Arquivo de diagnóstico identifica também a revisão da versão.

## Checklist

### Inventário — 6/7 (86%)

- [x] I1 — Leitura Win32 e MSIX independente das receitas. Evidência: Program.cs; LocalMsixInventory.cs
- [x] I2 — Identidades e deduplicação preservando instalações distintas. Evidência: InventoryReconciler.cs; TestesResiduos
- [x] I3 — Bibliotecas de jogos e entradas somente leitura. Evidência: GameLibraryAudit.cs; TestesResiduos
- [x] I4 — Verificação dos desinstaladores em segundo plano. Evidência: Program.cs v5.13.10.17
- [x] I5 — Contadores de inventário completo e ações disponíveis. Evidência: ActionableInventory.cs; Program.cs v5.13.10.17
- [x] I6 — Regressões da consolidação aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045
- [ ] I7 — Cobertura conferida nos dois computadores. Evidência: Validação presencial adiada

### Desinstalação e resíduos — 5/7 (71%)

- [x] R1 — Proteção de jogos e instalações ativas. Evidência: GameLibraryAudit.cs; BroadResidualDiscovery.cs
- [x] R2 — Classificação conservadora de fabricantes compartilhados. Evidência: BroadResidualDiscovery.cs; ResidualAuditDialog.cs
- [x] R3 — Auditoria histórica somente leitura. Evidência: HistoricalResidualAudit.cs; HistoricalDeviceAudit.cs
- [x] R4 — Limpeza específica após confirmação e revalidação da remoção. Evidência: Program.cs; UninstallResiduals.cs
- [x] R5 — Regressões de proteção aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045
- [ ] R6 — Resolver casos históricos ambíguos com evidências adicionais. Evidência: Adicionada proteção para inventário incompleto; a atribuição dos vestígios específicos ainda depende de evidências dos computadores.
- [ ] R7 — Desinstalação e preservação de dados validadas nos computadores. Evidência: Validação presencial adiada

### Atualizações — 6/8 (75%)

- [x] U1 — Consulta e fusão de múltiplas fontes. Evidência: OfficialReleaseSources.cs; RuckZuckCatalog.cs
- [x] U2 — Comparação de versões e proteção de identidades. Evidência: Matching.cs; TestesMotor; TestesMultifontes
- [x] U3 — Receitas verificadas e alternativas oficiais. Evidência: VerifiedUpdaters.cs; WindowsTerminalUpdater.cs
- [x] U4 — Confirmação posterior rejeita editor divergente e entrada somente leitura. Evidência: VerifiedUpdaters.cs; TestesAtualizador v5.13.10.17
- [x] U5 — Vincular confirmação à instalação original e conferir arquitetura/escopo Sumatra. Evidência: VerifiedUpdaters.ConfirmInstallation; TestesAtualizador aprovados. Identidade alterada permanece indeterminada; não equivale à validação real de todos os instaladores.
- [ ] U6 — Explicar divergências de cobertura com WinGet. Evidência: Contagens por motivo implementadas sem nomes nos logs; comparação com WinGet depende de capturas equivalentes da mesma máquina.
- [x] U7 — Regressões da consolidação aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045
- [ ] U8 — Instalação e versão final validadas nos computadores. Evidência: Validação presencial adiada

### Desempenho e estabilidade — 4/6 (67%)

- [x] P1 — Cadência de monitoramento conforme visibilidade. Evidência: ProcessSamplingPolicy.cs v5.13.10.16
- [x] P2 — Consultas de caminhos fora da interface. Evidência: Program.cs v5.13.10.17
- [x] P3 — Atualização incremental e registro em lotes. Evidência: ProcessMonitorUi.cs; DarkControls.cs
- [x] P4 — Regressões de monitoramento e bandeja aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045
- [ ] P5 — Medir tempo de abertura, CPU e RAM nos computadores. Evidência: Validação presencial adiada
- [ ] P6 — Diagnosticar bloqueio AVG e verificar estabilidade real. Evidência: Causa e resolução ainda não comprovadas

### Interface — 4/5 (80%)

- [x] V1 — Layout adaptável, DPI e navegação. Evidência: ResponsiveLayout.cs; StartupWindowSizing.cs
- [x] V2 — Cartões compactos, textos e rolagem. Evidência: Program.cs; IntegratedSystemUi.cs
- [x] V3 — Descrição acessível do resumo completo de aplicativos. Evidência: Program.cs v5.13.10.17
- [x] V4 — Matriz automatizada de layout aprovada nesta consolidação. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39348340045
- [ ] V5 — Legibilidade e fluidez aprovadas nos dois computadores. Evidência: Validação presencial adiada

### Investigação de drivers — 2/4 (50%)

- [x] D1 — Comparação histórica PnP somente leitura. Evidência: HistoricalDeviceAudit.cs
- [ ] D2 — Correlacionar dispositivos, pacotes e histórico Windows. Evidência: Correlação dispositivo/INF/presença implementada em parte. Driver Store completo e histórico Windows continuam pendentes.
- [x] D3 — Inventário funcional na aba DRIVERS. Evidência: Tabela WMI sob demanda; teste real de coleta no runner Windows e matriz de layout aprovados. Sem instalação/remoção.
- [ ] D4 — Validar interpretação nos computadores. Evidência: Etapa futura

### Gestão de drivers — 0/5 (0%)

- [ ] G1 — Consultar versões oficiais e compatibilidade. Evidência: Etapa futura
- [ ] G2 — Verificar pacote e assinatura. Evidência: Etapa futura
- [ ] G3 — Instalar e confirmar dispositivo/versão. Evidência: Etapa futura
- [ ] G4 — Reversão quando suportada e recuperação de falhas. Evidência: Etapa futura
- [ ] G5 — Validar fluxo controlado nos computadores. Evidência: Etapa futura

## Próxima etapa: validar o lote nos dois computadores

1. Seguir [TESTE-CONSOLIDADO.md](TESTE-CONSOLIDADO.md) primeiro no PC FRACO e depois no principal.
2. Comparar inventário e atualizações na mesma máquina e horário; coletar os casos históricos ambíguos.
3. Conferir a consulta de drivers com o Gerenciador de Dispositivos; registrar dados ausentes e divergências.
4. Medir abertura, CPU/RAM, bandeja e legibilidade; registrar eventual alerta AVG sem desativá-lo.
5. Corrigir divergências observadas antes de ampliar Driver Store/histórico e iniciar gestão de pacotes.

## Desenvolvimento posterior

A gestão de drivers permanece não implementada: fontes oficiais, compatibilidade, verificação de pacotes, instalação e reversão. A consulta atual não comprova essas funções. Validar sua coleta nos equipamentos é o próximo marco escolhido; não significa que todo desenvolvimento possível sem esses equipamentos esteja esgotado.

## Limites da validação

- A CI cobre compilação, nove grupos de regressão, consulta WMI real no runner, layout/bandeja/monitoramento e ativação do pacote. Consultar o resultado acima antes de considerar o lote aprovado.
- O runner Windows não reproduz o AVG, os dispositivos, a resolução nem a carga dos computadores do usuário.
- A CI não executa instaladores reais de aplicativos, não valida fontes online ao vivo e não comprova compatibilidade universal.
- A tabela de drivers não enumera todo o Driver Store e não permite instalar, remover ou reverter pacotes.
- Identidade alterada por um instalador permanece indeterminada, mesmo quando a atualização pode ter funcionado.
- As alterações estão numa branch de teste; a branch principal não foi substituída.
- A lista tem escopo explícito e pode crescer com novos achados. Seus percentuais não medem a proporção de todo o produto pronta.
