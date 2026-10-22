# GERENCIADOR FINAL — roadmap funcional

**v5.13.11.42 TESTE — MISSÃO WORK 7 em validação. A .41 não foi homologada: falha de regressão Windows. Última bateria física fornecida: .40 PC FORTE. Progresso mantido: 27/42 (64,3%). [Auditoria atual](MISSAO-WORK7-RESULTADO.md).**
Os percentuais representam checkpoints; não medem segurança nem prontidão de release.

## MISSÃO WORK 6 — atualização, desinstalação e resíduos

O Work chegou ao código `1c4c460` com **13 commits** de alterações e 3 Actions de CI aprovadas. Contratos de autorização, revalidação e prévia foram reforçados. Os 10/10 controles negativos por mutação falharam corretamente quando uma proteção foi removida artificialmente. **U8, R7 e R4 continuam pendentes**: não houve operações reais autorizadas e R4 permanece suspenso. Nenhum botão de administrador foi criado. [Fechamento técnico, matrizes, links e próximo lote](MISSAO-WORK6-RESULTADO.md). Roadmap segue 27/42.

## Avanço validado — U6 encerrado, 27/42 (64,3%)

A comparação real do PC FORTE mostrou nove entradas em `winget upgrade`
e oito no Gerenciador Final .34, das quais duas exigiam revisão.
**Reconciliação completa dos nove produtos**, sem afirmar que sejam
instaláveis: seis candidatos consultivos sem conflito documentado
(7-Zip, EA app, FanControl, Jellyfin Server, K-Lite Mega e Outlook),
dois casos com fontes/versionamento em revisão (Plex Media Server e
Proton Pass) e um item propositalmente somente inventário
(Microsoft GameInput, componente protegido do Windows).

Fontes oficiais e catálogos HTTP(S), sem executar WinGet COM/CLI;
identidades do servidor Jellyfin e Plex protegidas contra confusão
com players. Fixture automatizada de nove produtos e cenário de
GameInput protegido na suíte multifontes, CI Windows aprovada:
[regressões](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959079),
[build](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959083),
[política](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959189).
Código de teste aprovado: `5067a419`. Essa aprovação encerra
**U6 (explicar divergências de cobertura)**, e não U8 (instalação
real), G1–G5 ou validação antivírus. Sem nova build/versão,
sem novas dependências ou botões.

**Diretriz permanente de produto:** o usuário decide se inicia
o executável como administrador. Não criar botão administrativo,
não forçar elevação nem alterar o manifesto padrão. Uma possível
recomendação de uso elevado só será avaliada no final do projeto,
se um benefício material for comprovado. A comparação A/B feita
não aumentou a cobertura de aplicativos nem de atualizações.

## Estado histórico — MISSÃO WORK 5

[Auditoria e matriz dos nove produtos](MISSAO-WORK5.md): contrato WinGet desligado,
sem executor COM/CLI. Cinco produtos com novas consultas HTTPS; evidências e
conflitos multifontes preservados, instalação direta bloqueada quando houver revisão.
Progresso real por fontes e diagnóstico de contexto/etapas com perfil de teste temporário.
Na conclusão original da missão, U6/I7 e testes físicos/AVG continuavam abertos; U6 foi encerrado depois pela comparação .34 e CI, conforme seção de avanço.

## Resultado do teste A/B administrativo (.34)

PC FORTE: mesma build sob sessões normais/elevadas. **174 aplicativos,
27 MSIX e 8 candidatos (2 em revisão)** nos dois contextos, com
15/15 fontes OK na comparação válida e 178 drivers/63 INFs.
Os resíduos gerais tiveram 2 falhas de leitura no modo normal
contra 0 elevado; 3.925 versus 3.929 entradas e 867 versus
871 grupos examinados. Os 44 candidatos gerais e os 45 grupos/
164 locais finais **não mudaram**. Análise continua parcial por
limites de entradas/profundidade e links redirecionados.
Uma execução A/B é evidência de ganho observado na leitura,
não atribuição causal definitiva a permissões. Não há motivo
demonstrado para exigir administrador na consulta de atualizações,
e o WinGet real permanece desativado.
[Detalhes](TESTES.md#resultado-da-comparacao-normal--administrador-no-pc-forte-34).
Roadmap permanece 26/42 e a política de menor privilégio se mantém.

## Validação presencial .34 e comparação de privilégios

No PC FORTE, versão .34 observou 174 aplicativos/27 MSIX,
8 candidatos (2 em revisão), 15/15 fontes disponíveis no
primeiro ciclo. Jellyfin e Plex Server versionados reconhecidos
sem confundir os players. Consultas posteriores sofreram
indisponibilidade de fontes (8/15 e 4/15), derrubando candidatos
para 6 e 2, **sem redução comprovada da cobertura instalada**.
Houve encerramento normal; WinGet executável não foi invocado.

Próxima bateria proposta: mesmo executável .34 em sessões
isoladas normal e elevada por UAC manual, somente leitura,
monitorando `elevado`, inventário, MSIX, falhas e fontes OK.
Sem comparar consultas parciais com completas. Elevação não
equivale a inventariar todos os perfis de usuário. Nenhuma
elevação permanente da aplicação. PC FRACO permanece adiado.
[Critérios e observações](TESTES.md#validacao-fisica-34-no-pc-forte-e-teste-ab-de-privilegios-21102026).

## Validação presencial — .33 PC FORTE

Bateria privada de 21/10/2026: 174 aplicativos/27 MSIX completos;
**6** candidatas multifontes versus **3** na .32, todas as 13 fontes
consultadas OK e WinGet COM/CLI **não executado**. Contexto local:
App Installer e alias observados, pasta de alias fora de PATH;
consulta executada sem elevação. Cobertura agregada esclareceu:
fontes novas 1/2/4 geraram candidatas, fonte 3 sem identidade
canônica, fonte 5 inelegível (somente inventário), fonte 6
identidade/canal não aceito; três fontes de produto 7/8/9
divergem e bloqueiam instalação direta. A UI deve diferenciar
candidatas totais das que exigem revisão.

Drivers 178/63 sem alterações e resíduos 45 grupos/164 locais
parciais, sem exclusão. Nenhum alerta AVG nas evidências desta
sessão, mas apenas teste local limitado; PC FRACO ainda pendente.
U6 permanece aberto até explicar individualmente as três
barreiras, sem relativizar critérios de segurança.
[Detalhes e códigos](TESTES.md#primeira-bateria-real-33--pc-forte-21102026).
Roadmap preservado 26/42. Nenhuma build nova.

## Baseline — MISSÃO WORK 4

Retenção MSIX em memória, identidade de snapshot na fusão de atualizações,
TTL monotônico, limites de resíduos por origem, enumeração protegida e correlação
consultiva INF ↔ Driver Store implementados. Diagnóstico opt-in permanece local,
com teto total de 160 tentativas. Código `de6ee62` aprovado nas três Actions;
[matriz de testes, pacote e hashes](OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido).

O teste real mais recente do PC FORTE é a **.32** (174 apps, 27 MSIX,
178 associações/63 INFs, 45 grupos/164 locais de resíduos parciais).
A auditoria estruturada de limpezas foi comprovada: temporários de usuário
com 0 removidos/30 preservados; temporários Windows e cleanmgr com retorno 0.
A operação de Lixeira retornou 0x8000FFFF (E_UNEXPECTED) na interface
e ainda exige diagnóstico sem nova exclusão. No PC FRACO, a última
bateria registrada é a **.24**, com MSIX 0x80040154 persistente e
55 entradas observadas; não equivale à remoção dos aplicativos não enumerados.
A validação da .32 no PC FRACO ainda está pendente.

- [Matriz de achados, dependências, avanços e limites](OPERACAO-FAXINA-FINAL.md)
- [Roteiro objetivo PC FORTE/PC FRACO, somente leitura primeiro](TESTES.md)
- [Estado dos 42 checkpoints em JSON](PROGRESSO.json)
- [Histórico de versões](../CHANGELOG.md) e [organização](ORGANIZACAO-REPOSITORIO.md)

R6 continua exigindo evidências concretas de classificação. U6 foi concluído
por comparação real e regressão dos nove produtos. D2 ganhou o elo Store;
histórico Windows e pacotes sem associação permanecem pendentes.
**Restam 15 checkpoints em aberto.**
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

### Atualizações — 7/8 (87,5%)

- [x] U1 — Consulta e fusão de múltiplas fontes. Evidência: OfficialReleaseSources.cs; RuckZuckCatalog.cs
- [x] U2 — Comparação de versões e proteção de identidades. Evidência: Matching.cs; TestesMotor; TestesMultifontes
- [x] U3 — Receitas verificadas e alternativas oficiais. Evidência: VerifiedUpdaters.cs; WindowsTerminalUpdater.cs
- [x] U4 — Confirmação posterior rejeita editor divergente e entrada somente leitura. Evidência: VerifiedUpdaters.cs; TestesAtualizador v5.13.10.17
- [x] U5 — Vincular confirmação à instalação original e conferir arquitetura/escopo Sumatra. Evidência: VerifiedUpdaters.ConfirmInstallation; TestesAtualizador aprovados. Identidade alterada permanece indeterminada; não equivale à validação real de todos os instaladores.
- [x] U6 — Explicar divergências de cobertura com WinGet. Evidência: 9 produtos na consulta WinGet do PC FORTE reconciliados com 8 do GF .34 (6 candidatos consultivos, 2 em revisão, 1 GameInput protegido); teste de 9 identidades aprovado na CI Windows 39698959079. Não comprova instalação compatível.
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
- **U6 encerrado:** o resultado original era WinGet 9 versus GF .32 3; na .34, o GF reconciliou os nove como 6 candidatos consultivos, 2 em revisão e 1 GameInput protegido. CI comprova regras de identidade e proteção, não compatibilidade de instaladores.
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

**Decisão atual após a validação U6:** **27/42 (64,3%)**;
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

