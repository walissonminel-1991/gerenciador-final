# GERENCIADOR FINAL — roadmap funcional

**v5.13.11.31 TESTE · Progresso: 26/42 (61,9%).**
Os percentuais representam checkpoints; não medem segurança nem prontidão de release.

## Estado atual — MISSÃO WORK 4

Retenção MSIX em memória, identidade de snapshot na fusão de atualizações,
TTL monotônico, limites de resíduos por origem, enumeração protegida e correlação
consultiva INF ↔ Driver Store implementados. Diagnóstico opt-in permanece local,
com teto total de 160 tentativas. Código `de6ee62` aprovado nas três Actions;
[matriz de testes, pacote e hashes](OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido).

O último teste real do PC FORTE é a **.30** (174 apps, 27 MSIX, 177 associações/63
INFs, 44 grupos/161 locais de resíduos parciais). No PC FRACO, o último é a **.24**,
com MSIX 0x80040154 persistente e 55 entradas observadas; não significa remoção
dos 19 aplicativos não enumerados. Não houve nova bateria presencial nesta missão.

- [Matriz de achados, dependências, avanços e limites](OPERACAO-FAXINA-FINAL.md)
- [Roteiro objetivo PC FORTE/PC FRACO, somente leitura primeiro](TESTES.md)
- [Estado dos 42 checkpoints em JSON](PROGRESSO.json)
- [Histórico de versões](../CHANGELOG.md) e [organização](ORGANIZACAO-REPOSITORIO.md)

R6 e U6 receberam melhorias verificáveis, mas continuam sem evidência de campo
suficiente. D2 ganhou o elo Store; histórico Windows e pacotes sem associação
permanecem pendentes. **Nenhum dos 16 checkpoints abertos foi encerrado.**
R4 e G1–G5 continuam bloqueados. Consultas não concedem permissão de excluir.

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
- [ ] D2 — Correlacionar dispositivos, pacotes e histórico Windows. Evidência: WMI/INF/presença e vínculo publicado ↔ Store via SetupAPI implementados na .31. Histórico Windows e pacotes sem associação continuam pendentes.
- [x] D3 — Inventário funcional na aba DRIVERS. Evidência: Tabela WMI sob demanda; teste real de coleta no runner Windows e matriz de layout aprovados. Sem instalação/remoção.
- [ ] D4 — Validar interpretação nos computadores. Evidência: Etapa futura

### Gestão de drivers — 0/5 (0%)

- [ ] G1 — Consultar versões oficiais e compatibilidade. Evidência: Etapa futura
- [ ] G2 — Verificar pacote e assinatura. Evidência: Etapa futura
- [ ] G3 — Instalar e confirmar dispositivo/versão. Evidência: Etapa futura
- [ ] G4 — Reversão quando suportada e recuperação de falhas. Evidência: Etapa futura
- [ ] G5 — Validar fluxo controlado nos computadores. Evidência: Etapa futura

## Próxima etapa prioritária

Seguir [TESTES](TESTES.md): primeiro bateria A no PC FORTE, refinar casos
históricos, repetir A no PC FRACO; operações individuais de apps somente na
bateria B após backup/consentimento. Drivers continuam apenas leitura.

### Evidências para avançar sem maquiar o percentual

- **R6** exige resolver amostras históricas ambíguas concretas, não somente
  aprimorar textos ou aumentar a quantidade de achados.
- **U6** exige comparação equivalente com WinGet no mesmo equipamento e
  explicação de casos divergentes.
- **I7, R7, U8, P5, V5 e D4** dependem das validações presenciais previstas
  nos dois computadores; testes CI não substituem essa evidência.
- **R4** permanece suspenso até prova da exclusividade e segurança da
  exclusão real; nenhum programa de terceiro ou limpeza por heurística
  substitui essa comprovação.
- **D2** requer completar a correlação de dispositivos, pacotes do Driver
  Store e histórico confiável do Windows. A matriz WMI/INF/Store atual cobre apenas parte do objetivo;
  validar também a interpretação do histórico real.
- **G1–G5** continuam em zero: não há motor seguro de obtenção, validação,
  instalação, remoção ou reversão de drivers nesta versão.

### Critérios de abertura da gestão de drivers

Antes de qualquer escrita, obter identificação inequívoca de hardware e do
pacote **oficial** correspondente (G1); verificar compatibilidade de
SO/arquitetura, integridade, assinatura e origem do pacote (G2). Apenas depois
poderá existir uma operação por dispositivo, com confirmação clara, restauração
ou recuperação previamente validada e verificação pós-instalação (G3).
Reversão precisa respeitar suporte real do Windows e registrar falhas (G4).
G5 demanda testes supervisionados **inicialmente em ambiente descartável**,
e só depois em hardware de teste não crítico. Proibidos a remoção em massa,
limpeza heurística de Driver Store, substituição silenciosa de drivers do
sistema e promessa de rollback quando não disponível.

**Decisão de checkpoint nesta missão:** manter **26/42 (61,9%)**;
as melhorias foram implementadas e verificadas, mas os critérios de campo dos
checkpoints abertos não foram satisfeitos. A branch
de teste e o PR #40 permanecem Draft, sem merge/release.

## Desenvolvimento posterior

A gestão de drivers permanece não implementada: fontes oficiais, compatibilidade, verificação de pacotes, instalação e reversão. A consulta atual não comprova essas funções. Validar sua coleta nos equipamentos é o próximo marco escolhido; não significa que todo desenvolvimento possível sem esses equipamentos esteja esgotado.

## Limites da validação

- A CI cobre compilação, regressões de comparação, catálogos, instaladores, drivers, resíduos, MSIX, privacidade, encerramento, layout/bandeja/RAM e ativação do pacote. WMI/SetupAPI somente leitura são exercitados no runner Windows; fontes online e downloads mantêm jobs manuais opcionais. Consultar o resultado acima antes de considerar o lote aprovado.
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
