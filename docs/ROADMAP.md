# GERENCIADOR FINAL — progresso verificável

Base: 5.13.10.17 TESTE · 2026-10-19

Percentual = checkpoints concluídos / checkpoints listados. Pesos iguais; não mede horas, cobertura universal ou aprovação final. Implementação e testes reais são checkpoints distintos.

Prioridade: inventário, resíduos e atualizações. Estabilidade e interface acompanham as correções. Os testes nos dois computadores ficam para o pacote consolidado. Atualizar esta lista após cada rodada de trabalho; não há execução automática fora da conversa.

Código validado: `859ba45a705c5461c2c4b3978dd280eb0edb96ca`

CI aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327

Compilação, oito grupos de regressão, layout/bandeja/monitoramento e ativação do manifesto aprovados. Pacote de teste gerado. Nenhuma instalação ou desinstalação de aplicativo foi validada nos computadores do usuário.

## Checklist

### Inventário — 6/7 (86%)

- [x] I1 — Leitura Win32 e MSIX independente das receitas. Evidência: Program.cs; LocalMsixInventory.cs.
- [x] I2 — Identidades e deduplicação preservando instalações distintas. Evidência: InventoryReconciler.cs; TestesResiduos.
- [x] I3 — Bibliotecas de jogos e entradas somente leitura. Evidência: GameLibraryAudit.cs; TestesResiduos.
- [x] I4 — Verificação dos desinstaladores em segundo plano. Evidência: Program.cs v5.13.10.17.
- [x] I5 — Contadores de inventário completo e ações disponíveis. Evidência: ActionableInventory.cs; Program.cs v5.13.10.17.
- [x] I6 — Regressões da consolidação aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327.
- [ ] I7 — Cobertura conferida nos dois computadores. Evidência: Validação presencial adiada.

### Desinstalação e resíduos — 5/7 (71%)

- [x] R1 — Proteção de jogos e instalações ativas. Evidência: GameLibraryAudit.cs; BroadResidualDiscovery.cs.
- [x] R2 — Classificação conservadora de fabricantes compartilhados. Evidência: BroadResidualDiscovery.cs; ResidualAuditDialog.cs.
- [x] R3 — Auditoria histórica somente leitura. Evidência: HistoricalResidualAudit.cs; HistoricalDeviceAudit.cs.
- [x] R4 — Limpeza específica após confirmação e revalidação da remoção. Evidência: Program.cs; UninstallResiduals.cs.
- [x] R5 — Regressões de proteção aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327.
- [ ] R6 — Resolver casos históricos ambíguos com evidências adicionais. Evidência: Preservar enquanto a atribuição não for confiável.
- [ ] R7 — Desinstalação e preservação de dados validadas nos computadores. Evidência: Validação presencial adiada.

### Atualizações — 5/8 (63%)

- [x] U1 — Consulta e fusão de múltiplas fontes. Evidência: OfficialReleaseSources.cs; RuckZuckCatalog.cs.
- [x] U2 — Comparação de versões e proteção de identidades. Evidência: Matching.cs; TestesMotor; TestesMultifontes.
- [x] U3 — Receitas verificadas e alternativas oficiais. Evidência: VerifiedUpdaters.cs; WindowsTerminalUpdater.cs.
- [x] U4 — Confirmação posterior rejeita editor divergente e entrada somente leitura. Evidência: VerifiedUpdaters.cs; TestesAtualizador v5.13.10.17.
- [ ] U5 — Correlacionar arquitetura e escopo em todas as confirmações posteriores. Evidência: Ainda incompleto, inclusive Sumatra x86/x64.
- [ ] U6 — Explicar divergências de cobertura com WinGet. Evidência: Precisa comparação dos resultados equivalentes.
- [x] U7 — Regressões da consolidação aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327.
- [ ] U8 — Instalação e versão final validadas nos computadores. Evidência: Validação presencial adiada.

### Desempenho e estabilidade — 4/6 (67%)

- [x] P1 — Cadência de monitoramento conforme visibilidade. Evidência: ProcessSamplingPolicy.cs v5.13.10.16.
- [x] P2 — Consultas de caminhos fora da interface. Evidência: Program.cs v5.13.10.17.
- [x] P3 — Atualização incremental e registro em lotes. Evidência: ProcessMonitorUi.cs; DarkControls.cs.
- [x] P4 — Regressões de monitoramento e bandeja aprovadas no Windows remoto. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327.
- [ ] P5 — Medir tempo de abertura, CPU e RAM nos computadores. Evidência: Validação presencial adiada.
- [ ] P6 — Diagnosticar bloqueio AVG e verificar estabilidade real. Evidência: Causa e resolução ainda não comprovadas.

### Interface — 4/5 (80%)

- [x] V1 — Layout adaptável, DPI e navegação. Evidência: ResponsiveLayout.cs; StartupWindowSizing.cs.
- [x] V2 — Cartões compactos, textos e rolagem. Evidência: Program.cs; IntegratedSystemUi.cs.
- [x] V3 — Descrição acessível do resumo completo de aplicativos. Evidência: Program.cs v5.13.10.17.
- [x] V4 — Matriz automatizada de layout aprovada nesta consolidação. Evidência: CI Windows aprovada: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39311936327.
- [ ] V5 — Legibilidade e fluidez aprovadas nos dois computadores. Evidência: Validação presencial adiada.

### Investigação de drivers — 1/4 (25%)

- [x] D1 — Comparação histórica PnP somente leitura. Evidência: HistoricalDeviceAudit.cs.
- [ ] D2 — Correlacionar dispositivos, pacotes e histórico Windows. Evidência: Etapa futura.
- [ ] D3 — Inventário funcional na aba DRIVERS. Evidência: DriversPlaceholder.cs ainda provisório.
- [ ] D4 — Validar interpretação nos computadores. Evidência: Etapa futura.

### Gestão de drivers — 0/5 (0%)

- [ ] G1 — Consultar versões oficiais e compatibilidade. Evidência: Etapa futura.
- [ ] G2 — Verificar pacote e assinatura. Evidência: Etapa futura.
- [ ] G3 — Instalar e confirmar dispositivo/versão. Evidência: Etapa futura.
- [ ] G4 — Reversão quando suportada e recuperação de falhas. Evidência: Etapa futura.
- [ ] G5 — Validar fluxo controlado nos computadores. Evidência: Etapa futura.

## Próxima rodada

1. Refinar evidências para resíduos históricos sem liberar exclusões ambíguas.
2. Correlacionar arquitetura e escopo na confirmação posterior de atualizações.
3. Investigar divergências de cobertura com entradas e versões equivalentes.
4. Preparar um roteiro único para os dois computadores após fechar as pendências verificáveis remotamente.

## Limitações conhecidas

- O runner Windows não reproduz o AVG nem a carga dos computadores do usuário.
- Metadado de fabricante no Registro não é assinatura digital; o novo filtro não substitui a verificação do pacote.
- A CI desta rodada não executa instaladores, não valida fontes online ao vivo e não comprova compatibilidade com todas as versões do Windows.
- A branch principal ainda permanece na v5.12.3. Esta consolidação está numa branch de teste.
- A lista tem escopo explícito e pode crescer com achados novos. Seu percentual não mede a proporção de todo o produto já pronta.
