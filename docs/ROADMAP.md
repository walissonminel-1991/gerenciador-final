# GERENCIADOR FINAL — roadmap funcional

**Candidata atual: v5.13.11.25 TESTE · Progresso: 26/42 (61,9%).**
Os percentuais indicam checkpoints e não segurança ou prontidão para release.

## Revisão da faxina — v5.13.11.25 TESTE

Correções de preservação MSIX parcial, propagação de cobertura ao diagnóstico,
nomes de dispositivos, identidade de arquivos/instaladores, ABI de restauração
e scripts de distribuição; contrato GUID/revisão do Windows Update preservado
entre UI e worker. Resíduos continuam somente leitura; nenhuma nova
etapa funcional foi declarada concluída. [Relatório, evidências e roteiro](OPERACAO-FAXINA-FINAL.md).

## Última bateria real — PC fraco (21/10/2026, v5.13.11.24)

A v5.13.11.24 manteve a interface responsiva e a leitura de 108 associações
de drivers, 50 INF (três associações sem INF válido); 2 possíveis atualizações
e uma atualização Windows consultada, sem execução de manutenção.

**Regressão MSIX real:** erro de ativação
`COMException(0x80040154)` persistiu após a única tentativa de retry.
Inventário observado: **55** entradas (19 aplicativos MSIX não enumerados)
em vez das 74 entradas observadas na .23. A lista deve ser classificada
como **PARCIAL**; não há prova de que os aplicativos foram removidos.
O diagnóstico completo da outra etapa não apaga a falha MSIX do inventário.

Descoberta geral de resíduos: 12 origens consultadas, 2.000 entradas
observadas, 11 grupos candidatos; limites `ReadFailure+InventoryPartial+
DepthLimit+LinkSkipped`. Há 2 fontes com falhas pontuais de leitura
e 0 fontes globalmente indisponíveis identificadas nessa consulta.
A janela combinada preserva 12 grupos/41 locais; resultados não autorizam
exclusão. Este diagnóstico valida a separação entre falhas pontuais e
origens não consultadas, não o diagnóstico completo de todos os caminhos.

**Próxima decisão técnica:** investigar ativação WinRT/MSIX de modo
não invasivo, revisar nomes/versões incorretos de dispositivos WMI e
testar se o inventário volta na mesma sessão. Não executar
reinstalação, alteração de permissões, PowerShell auxiliar ou limpeza
de dados privados para mascarar falha de leitura.

## Organização e evidências

- [Índice de arquivos atuais e históricos](ORGANIZACAO-REPOSITORIO.md)
- [Roteiro atual e revisão da faxina](OPERACAO-FAXINA-FINAL.md)
- [Resultados anteriores .23](TESTE-PC-FRACO-2026-10-21-v5.13.11.23.md)
- [Missão técnica Work 3](MISSAO-WORK3-v5.13.11.17.md)
- [Histórico resumido de versões](../CHANGELOG.md)
- [Workflows ativos e removidos](ACTIONS-ATUAIS-E-HISTORICAS.md)

O histórico detalhado das alterações de v5.13.11.0–.22 permanece no
CHANGELOG e nos commits Git; ele não é repetido neste roadmap para
evitar a acumulação de um diário inteiro antes do checklist vivo.

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

### Desinstalação e resíduos — 4/7 (R4 suspenso na v5.13.11.15)

- [x] R1 — Proteção de jogos e instalações ativas. Evidência: GameLibraryAudit.cs; BroadResidualDiscovery.cs
- [x] R2 — Classificação conservadora de fabricantes compartilhados. Evidência: BroadResidualDiscovery.cs; ResidualAuditDialog.cs
- [x] R3 — Auditoria histórica somente leitura. Evidência: HistoricalResidualAudit.cs; HistoricalDeviceAudit.cs
- [ ] R4 — Limpeza real específica após confirmação e revalidação. Suspensa na v5.13.11.15: somente prévia e simulação até comprovar exclusividade, integridade do consentimento e segurança de execução. Evidência: ResidualPreview.cs; UninstallResiduals.cs; AdvancedUninstallCleaner.cs
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

- [x] P1 — Cadência de monitoramento conforme visibilidade. Evidência atual: IntegratedSystemUi.UpdateProcessSamplingCadence e ProcessSamplingPolicy.cs; amostragem global de RAM.
- [x] P2 — Consultas de caminhos fora da interface. Evidência: Program.cs v5.13.10.17
- [x] P3 — Atualização incremental e registro em lotes. Evidência atual: MemoryRamDashboard.cs; DarkControls.cs. A antiga tabela de processos foi substituída pelo acesso ao Gerenciador de Tarefas do Windows.
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

- A CI cobre compilação, regressões de comparação, catálogos, instaladores, drivers, resíduos, MSIX, privacidade, encerramento, layout/bandeja/RAM e ativação do pacote. Consultas WMI ao vivo e downloads têm caminhos manuais opcionais; só são validados quando executados. Consultar o resultado acima antes de considerar o lote aprovado.
- O runner Windows não reproduz o AVG, os dispositivos, a resolução nem a carga dos computadores do usuário.
- A CI não executa instaladores reais de aplicativos, não valida fontes online ao vivo e não comprova compatibilidade universal.
- A tabela de drivers não enumera todo o Driver Store e não permite instalar, remover ou reverter pacotes.
- Identidade alterada por um instalador permanece indeterminada, mesmo quando a atualização pode ter funcionado.
- A `main` contém a candidata v5.13.11.0. As correções posteriores estão na branch do PR #40 até sua integração.
- A lista tem escopo explícito e pode crescer com novos achados. Seus percentuais não medem a proporção de todo o produto pronta.

## Manutenção de repositório — 21/10/2026

A branch de teste removeu workflows e guias obsoletos, preservando
três Actions atuais e todas as regressões necessárias. Veja
[ACTIONS-ATUAIS-E-HISTORICAS.md](ACTIONS-ATUAIS-E-HISTORICAS.md)
para o escopo e limitações. Esta limpeza não corrige MSIX nem
habilita manutenção destrutiva; não altera a contagem de checkpoints.
