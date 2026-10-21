# GERENCIADOR FINAL — progresso verificável

## Nova bateria PC fraco — v5.13.11.20 TESTE

A v5.13.11.19 no PC fraco 1440×900 confirmou a correção dos nomes genéricos: revisão com 12 grupos e 41 locais, contra 17/57 na anterior, sem apagar dados. Tempo de inicialização 709 ms, primeiro inventário 1,29 s, descoberta de resíduos 295 ms; sessão única, **não** benchmark controlado. A busca ainda é **parcial** (2.000 entradas, 2 fontes inacessíveis), sem conclusão de segurança para exclusão. Capturas mostraram que a seleção de origem filtrava grupos, mas continuava exibindo locais de origens diferentes; v5.13.11.20 passa a filtrar também os locais e suas contagens, com testes sintéticos e de interface. Também diferencia categorias sem candidatos revisáveis dos resultados não consultados. [Relatório agregado](TESTE-PC-FRACO-2026-10-21-v5.13.11.19.md).

**Próxima etapa:** confirmar a correção em testes reais somente leitura, com filtros de origem e categoria, busca, desempenho, AVG e parcialidade. Nenhuma exclusão ou alteração em drivers autorizada. Roadmap: **26/42**, inalterado.


## PC fraco real 21/10/2026 — v5.13.11.19 TESTE

Recebidas capturas e diagnóstico de um computador 1440×900 com v5.13.11.18. Inicialização 1,32 s; primeiro inventário 2,01 s; 74 programas; 108 associações de drivers; 3 possíveis atualizações; consulta de resíduos parcial com 2000 entradas examinadas, 17 grupos e 57 locais na revisão. O usuário testou interface, filtros, inspeção, bandeja e monitoramento de RAM sem executar exclusões. Detectado falso agrupamento de subpastas genéricas com nomes homônimos pertencentes a aplicações distintas. v5.13.11.19 corrige a identidade de `Config/Logs/Updates/Scripts/Providers` pelo contêiner pai, adiciona regressões de diretório/Registro e melhora a mensagem da lista vazia. Relatório agregado e sem identificadores pessoais: [PC fraco 21/10](TESTE-PC-FRACO-2026-10-21-v5.13.11.18.md). A cobertura continua parcial (limites e fontes inacessíveis); não inferir lixo seguro. Etapas funcionais: **26/42**. Próxima ação após passar CI: repetir análise somente leitura na nova candidata no mesmo PC, conferir grupos genéricos e parcialidade, sem instalar/desinstalar.


## Revisão pós-Work 3 — v5.13.11.18 TESTE

Auditoria independente da v5.13.11.17 comprovou três workflows finais aprovados e 310/310 verificações de resíduos. Uma inconsistência identificada na montagem da fotografia podia perder o estado parcial reportado por origens legadas/linhas individuais. Corrigida a propagação de falhas sem duplicar contagens e congeladas as listas de fontes da cobertura. Adicionados testes sintéticos e WinForms para preservar aviso de **ANÁLISE PARCIAL**, mesmo com zero achados. Nenhuma exclusão foi habilitada; progresso funcional permanece **26/42**. Próximo marco: testes reais no PC fraco 1440×900 conforme [checklist Work 3](CHECKLIST-PC-FRACO-v5.13.11.17.md); verificar aviso de parcialidade, desempenho, filtros, cancelamento e AVG sem apagar nada.


## Missão Work 3 — v5.13.11.17 TESTE

| Marco | Progresso comprovado |
|---|---|
| M1 — investigação | Cortes de grupos/locais removidos no classificador; índice global, páginas e inspeção por metadados, com fontes não examinadas/inacessíveis separadas de dados em outras páginas. Saturação até 50.000 evidências mantém teto e parcialidade. |
| M2 — precisão | Caminhos ainda usados, aliases e homônimos de fabricantes protegidos; origens tipadas e instalação a conferir. Testes negativos de conflitos e preservação. |
| M3 — desempenho | Cache por consulta, cancelamento cooperativo, métricas locais, orçamento e concorrência limitados; comparação reproduzível com .16 publicada, incluindo custo maior de alocações. Sem comprovação no PC fraco. |
| M4 — interface | Cartões virtualizados, busca/filtros globais, detalhe e inspeção integrados; oito resoluções, cancelamento, seleção e bandeja testados em WinForms real no runner. |
| M5 — segurança | Revisão avançada conserva falhas e limites; conferência segura por handles, integrações/reinstalação falham fechado. Cinco entradas destrutivas bloqueadas; privacidade testada. |
| M6 — consolidação | Revisão d0f8673 aprovada nos três workflows, 310/310 resíduos, 132 combinações gerais de resolução/aba. Versão/documentos consolidados; aprovação final do mesmo commit e integridade do pacote vinculadas na PR #40. |

**Checklist funcional: 26/42 (61,9%), sem novos checkpoints declarados concluídos.** Os marcos acima aprofundam funções existentes, não comprovam R4, hardware real, reversão ou manutenção completa. [Relatório e retomada](MISSAO-WORK3-v5.13.11.17.md), [medições reproduzíveis](BENCHMARKS-WORK3.md), [checklist PC fraco](CHECKLIST-PC-FRACO-v5.13.11.17.md) e [histórico extenso](TESTES-RESIDUOS-v5.13.11.17.md).

Main novamente observada sem proteção administrativa/rulesets; instruções objetivas em [Segurança GitHub](SEGURANCA-GITHUB-WORK2.md). Nenhuma alteração administrativa/merge. Próxima missão: partir de divergências dos testes reais e dos limites explicitados; não repetir auditorias completas nem habilitar exclusão.

## Missão Work 2 — v5.13.11.16 TESTE (20/10/2026)

| Marco | Implementação e evidência |
|---|---|
| A — integridade | Removido corte silencioso de 500 relatórios; saturação 500–10.000, omissões explícitas e estado parcial vazio. Integrado e validado com B/C. |
| B — decisão | Cinco entradas destrutivas bloqueadas; prévia copia alvos, recusa dados misturados e limita conferência. ACL/junction/hard links em fixtures passaram no Windows 39508255014. |
| C — precisão/desempenho | Preservação geral de perfis, locais compartilhados, aliases 32/64 e comparação indexada; 260/260 verificações e UI aprovadas no Windows 39508751898. |
| D — GitHub/consolidação | 43 workflows auditados, 38 históricos reforçados, checks com nomes únicos; proteção administrativa documentada. Revisão final da candidata, ZIP e SHA-256 registrados na PR #40 após aprovação. |

**Checklist funcional: 26/42 (61,9%).** A melhoria de confiabilidade não conclui checkpoints dependentes dos PCs nem reativa R4. [Relatório incremental](MISSAO-WORK2-v5.13.11.16.md), [testes supervisionados](TESTES-RESIDUOS-v5.13.11.16.md) e [pendências administrativas](SEGURANCA-GITHUB-WORK2.md). Main e envio remoto preservados; próxima missão deve partir das divergências observadas nos PCs, sem repetir a auditoria já documentada.

## Missão de resíduos — v5.13.11.15 TESTE (20/10/2026)

| Marco | Progresso comprovado |
|---|---|
| M1 | Auditoria concluída; exclusões inseguras bloqueadas no backend e UI; três workflows aprovados no commit 0f3701e. |
| M2 | Descoberta em dois níveis, Registro 32/64, Run/serviços e perfis de mídia; limites e cancelamento testados. Regressão do nome curto VLC encontrada no CI e corrigida no M3. |
| M3 | Classificação por item, união de evidências, inventário unificado e proteção MSI/caminhos; três workflows aprovados no commit afca0ae. |
| M4 | Prévia com identidade/hash e conferência sem apagar; decisão em simulação. 202/202 verificações de resíduos e UI passaram no Windows, commit 9583d55. |
| M5 | v5.13.11.15 preparada e compilada localmente sem erros; CI final, origem do ZIP e SHA-256 vinculados no fechamento da PR #40. |

**Checklist funcional atual: 26/42.** R4 foi suspenso deliberadamente: revalidar apenas nomes de arquivos/pastas não garante consentimento sobre conteúdo nem exclusividade. Isso corrige a contabilidade anterior de 27/42; as referências históricas abaixo permanecem como registro das respectivas versões. Nenhum checkpoint dependente dos PCs foi marcado como concluído por testes sintéticos. [Auditoria, decisões, testes e continuidade](MISSAO-RESIDUOS-v5.13.11.15.md).

Próximo teste supervisionado: comparar locais/enquadramento entre v5.13.11.14 e .15 no PC de histórico longo; conferir VLC/foobar2000, perfis dos fabricantes e WinMerge antes/depois do término completo do desinstalador; anotar tempo e responsividade. Nesta candidata não existe exclusão real de resíduos. Futuro executor exige propriedade exclusiva comprovada, validação/consentimento de conteúdo, proteção contínua por handles, rastreamento de auxiliares e bateria supervisionada com backup.


## Primeiro teste efetivo de manutenção — v5.13.11.14 TESTE (20/10/2026)

Teste real comprovou instalação oficial verificada do **Notepad++ v8.9.8.1** e registro do WinMerge x64 removido após execução do desinstalador, com um item restante na pasta de instalação para revisão. Identificada lacuna: `HKCU\Software\Thingamahoochie\WinMerge` não aparecia na auditoria de nome canônico, embora documentada pelo fabricante. A candidata acrescenta inspeção exata **somente leitura** dessa chave, das variantes HKLM correspondentes e de pastas WinMerge numa revisão histórica, sem habilitar exclusões. Lista é reconsultada antes de exibir a auditoria; mensagem não considera o fechamento de todas as janelas do desinstalador confirmado apenas pela ausência no Registro. Progresso funcional **27/42**, sem contabilizar manutenção completa com base nesses primeiros cenários. [Evidências, limitações e próximos testes](SIMULACAO-MANUTENCAO-WINMERGE-2026-10-20.md).


## Capturas reais da v5.13.11.12 — pequenos ajustes v5.13.11.13 (20/10/2026)

A nova revisão exibiu corretamente **três cartões por linha em Inicialização** e **Coleta local ativa** permanentemente no cabeçalho. Consultas concluíram: inventário de 171 programas e 183 associações de drivers, sem ações destrutivas e sem erro final. A ausência pontual de uma associação relativamente às 184 anteriores não prova desinstalação nem é fundamento para apagar driver. Correções pontuais de rótulos: botão de acesso ao Gerenciador de Tarefas e filtro Outros em DRIVERS, que mantêm explicação acessível e não alteram a consulta. Próxima bateria relevante: verificar **atualizações individuais, desinstalação de software de teste e reparos condicionados a diagnóstico**, somente após backup e consentimento explícito. Progresso funcional permanece 27/42.


## Teste v5.13.11.11 e correções visuais v5.13.11.12 (20/10/2026)

O testador abriu a nova lista DRIVERS com 184 associações e confirmou sua disposição no padrão visual de Aplicativos. Captura da aba Inicialização mostrou cartões com apenas **duas colunas** e espaço subutilizado; adaptada para três por linha quando houver largura, preservando duas/uma nas telas menores e mantendo detalhes acessíveis. O convite de privacidade não apareceu novamente porque o consentimento **LocalOnly** já estava persistido; foi acrescentado indicador permanente no cabeçalho, clicável para revisão/revogação, sem reativar envio remoto nem incomodar com novo diálogo a cada versão. Inventário e diagnósticos seguem somente leitura; manutenção real não validada e roadmap funcional permanece **27/42**.


## Padrão visual de Aplicativos aplicado a DRIVERS — v5.13.11.11 TESTE (20/10/2026)

Solicitado após observar tabela pequena e truncada no PC real. Aba DRIVERS agora usa cartões de lista virtualizada como Aplicativos, com busca por dispositivo/fabricante/versão/INF, filtros de presença e consulta sob demanda. Não há botões de atualizar, desinstalar, remover ou limpar drivers. A pesquisa/seleção não altera registros ou arquivos. A matriz de layout e 184 associações sintéticas foram adicionadas aos testes. **Gestão real de drivers continua pendente**; progresso funcional ainda 27/42.


## Testes reais e ajuste responsivo — v5.13.11.10 TESTE (20/10/2026)

As capturas/log de teste em PC real foram examinados. Houve abertura sem alerta AVG reportado, inventário, consultas, análise de hardware e visualização de abas. A tabela de drivers deixava muita largura ociosa e truncava valores; passou a usar colunas Fill ponderadas e mínimos, com teste na matriz de resoluções. Nenhuma operação destrutiva foi executada ou aprovada; verificar persistência/revogação da coleta, eficácia de atualização, desinstalação e reparo em testes posteriores. [Relatório detalhado](TESTE-REAL-v5.13.11.8-2026-10-20.md). Roadmap funcional continua **27/42**.


## Ajuste de legibilidade — v5.13.11.9 TESTE (20/10/2026)

Captura de tela em PC real confirmou o botão `Simular sem enviar` funcional, mas a prévia `DarkLogView` unia parágrafos porque TextBox nativo exigia CRLF. A revisão normaliza linhas, reduz o texto de consentimento, usa fonte mais legível e acrescenta regressões. O AVG não apresentou alerta durante a abertura observada; isso **não** é aprovação geral do antivírus nem das operações administrativas. Coleta permanece apenas local, sem envio, e o progresso funcional permanece **27/42**.


## Experiencia acessivel — v5.13.11.8 TESTE (20/10/2026)

Incluida simulacao local sem rede, arquivo ou consentimento e previa amigavel do diagnostico. Receptor de homologacao preparado, desativado por padrao e testado com requisicoes ficticias; nao e envio remoto real. A futura contribuicao devera ser simples para nao especialistas e nao depender de conta GitHub, arquivos .txt ou publicacao de Issues. [Criterios de seguranca, UI e ativacao](EXPERIENCIA-CONTRIBUICAO-SIMPLES.md). O progresso funcional anterior permanece **27/42**; a simulacao e uma frente de privacidade extra e **nao** significa envio implementado.


## Segurança v5.13.11.7 TESTE — nenhuma entrada manual (20/10/2026)

A PR #40 **substituiu** o piloto de Issues públicas por coleta local estritamente voluntária e esquema fechado; removeu a geração de formulários e o gatilho `issues` do CI. Bloqueou instaladores sem SHA-256 previamente autorizado, introduziu correspondência da revisão no Windows Update e desativou o receptor remoto experimental. Esta revisão supersede o trecho histórico abaixo que descreve a antiga triagem GitHub como ativa. Detalhes: [segurança e limites](SEGURANCA-v5.13.11.7.md). **O progresso funcional continua 27/42; a manutenção por IA e o envio remoto não estão implementados.**


## Continuidade — triagem de diagnosticos publicos (20/10/2026)

A PR #40 recebeu um workflow separado para triagem deterministica de Issues voluntarias do GitHub: parser com esquema fechado, allowlist de 14 eventos, limites de 1 a 100 ocorrencias por categoria, rejeicao de campos inesperados e testes de entradas maliciosas. O workflow nao executa texto fornecido por participantes, nao usa permissao de escrita e nao implementa manutencao IA ou envio remoto. Evidencias: [documentacao](TRIAGEM-DIAGNOSTICO-GITHUB.md) e checks da PR #40. **Sem mudanca de versao do aplicativo nem do progresso funcional: 27/42 (64,3%).**

## Reforco adicional — v5.13.11.6 TESTE (20/10/2026)

Protecao de ancestrais por handles, retencao de instaladores pela sessao, recusa de caminhos especiais e arquivos vizinhos inesperados. Auditoria NuGet bloqueante e configuracao Dependabot. Evidencias e limites: [relatorio](SEGURANCA-v5.13.11.6.md). Progresso funcional permanece 27/42; sem declarar envio automatico ou manutencao por IA implementados.

## Seguranca — v5.13.11.5 TESTE (20/10/2026)

Reforcados comprovantes de instaladores, revalidacao antes de executar, protecao de arquivos abertos, identidade do processo no pipe e escrita atomica de privacidade. Regressões adicionadas; o resultado final do Windows fica no PR #40. O progresso da checklist permanece **27/42 (64,3%)**; isto nao substitui testes reais. [Achados e limites](SEGURANCA-v5.13.11.5.md).


## Auditoria — v5.13.11.4 TESTE (20/10/2026)

Progresso recalculado a partir das sete áreas abaixo: **27 de 42 checkpoints (64,3%)**. Os complementos de diagnóstico/CI são frentes adicionais e não entram implicitamente nesse denominador. O número antigo 30/45 era inconsistente com os itens publicados.

Correções desta revisão: comandos internos vinculados à pasta de sistema, limpeza avançada restrita aos alvos aceitos, rejeição de diagnóstico expirado mesmo se sua exclusão falhar, preservação de pendência após sessão vazia e consentimento ativo idempotente. Acrescentadas regressões. O resultado final da validação Windows e do pacote é registrado no PR #40. Relatório: [AUDITORIA-v5.13.11.4.md](AUDITORIA-v5.13.11.4.md).


## Continuidade — diagnóstico v5.13.11.3 TESTE (20/10/2026)

Estado confirmado: `main` na v5.13.11.0 candidata; PR #38 com coleta local e PR #40 com revisão pública no GitHub. O CI Windows da v5.13.11.2 passou (run 39444626580). Nenhuma dessas evidências comprova testes nos dois PCs nem resolve o AVG.

- [x] Consentimento local e formulário público manual implementados na branch do PR #40.
- [x] Corrigidas retomada de pendência e interrupção da coleta ao revogar com falha de disco; testes adicionados na v5.13.11.3.
- [x] Aprovar compilação e regressões Windows da v5.13.11.3. Evidência: runs 39450808920 e 39450809136; revisão ec8c1ce.
- [ ] Simular novo participante: cadastro/login GitHub no navegador, recusa, aceite, coleta, revisão, cancelamento/publicação manual e revogação. Conferir também reinício e relatório anterior.
- [ ] Validar AVG e experiência nos dois PCs antes de publicação estável.
- [ ] Implementar triagem/agrupamento e PRs por IA com testes e revisão. Ainda não há manutenção autônoma, envio automático ou exclusão garantida de Issues públicas.

A checklist atualmente publicada soma **27/42 checkpoints (64,3%)**. O número 30/45 (67%) foi mantido de um resumo anterior e não corresponde à lista atual; fica registrado apenas como referência histórica. Esta correção é contábil e não comprova avanços nos computadores.


## Complemento — v5.13.11.2 TESTE: piloto GitHub-only

Com a conta GitHub do colaborador, o aplicativo oferece formulário pré-preenchido e revisável para Issues públicas, exclusivamente com categorias técnicas aprovadas. Publicação exige confirmação no navegador, não há credenciais embutidas. A próxima fase exige uma GitHub App registrada com permissões limitadas, consentimento separado para envio e revisão de privacidade. Veja [PILOTO-DIAGNOSTICO-GITHUB.md](PILOTO-DIAGNOSTICO-GITHUB.md).

## Complemento de desenvolvimento — v5.13.11.1 TESTE: diagnóstico local voluntário

Uma nova frente de privacidade foi iniciada com opção de consentimento, eventos agregados em allowlist, um relatório local rotativo e revogação. **Envio remoto não está implantado**; sua ativação exigirá consentimento separado e avaliação de segurança. Consulte [DIAGNOSTICO-COLABORATIVO.md](DIAGNOSTICO-COLABORATIVO.md). O resumo histórico de 30/45 foi reconciliado com a checklist atual: 27/42.

## Complemento de desenvolvimento — v5.13.11.0 CANDIDATA (20/10/2026)

O resumo anterior informava 30/45 checkpoints (67%); a auditoria de 20/10 contou 27/42 na checklist abaixo. Testes de código e aprovação em hardware real não são equivalentes. A partir de v5.13.10.19, novas entregas em branches de desenvolvimento incluem revisão de resíduos do Driver Booster, diagnóstico de AVG/MSIX, painel de RAM e otimização conservadora de cache do próprio aplicativo, SHA-256 e identificação versionada de pacotes.

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

- A CI cobre compilação, nove grupos de regressão, consulta WMI real no runner, layout/bandeja/monitoramento e ativação do pacote. Consultar o resultado acima antes de considerar o lote aprovado.
- O runner Windows não reproduz o AVG, os dispositivos, a resolução nem a carga dos computadores do usuário.
- A CI não executa instaladores reais de aplicativos, não valida fontes online ao vivo e não comprova compatibilidade universal.
- A tabela de drivers não enumera todo o Driver Store e não permite instalar, remover ou reverter pacotes.
- Identidade alterada por um instalador permanece indeterminada, mesmo quando a atualização pode ter funcionado.
- A `main` contém a candidata v5.13.11.0. As correções posteriores estão na branch do PR #40 até sua integração.
- A lista tem escopo explícito e pode crescer com novos achados. Seus percentuais não medem a proporção de todo o produto pronta.

## Manutenção da CI — 20/10/2026

Retirado o destino `main` de 24 workflows históricos que executavam verificações de versões anteriores. Mantidos os testes atuais em `Validar candidata Windows` e a validação do pacote em `Compilar Windows (sem assinatura)`, com cancelamento de revisões anteriores da mesma PR. A bateria consolidada também passa a executar em pushes na `main`. Detalhes e limitações: [Actions atuais e históricas](ACTIONS-ATUAIS-E-HISTORICAS.md). A mudança está na branch do PR #40 até integração e não altera os checkpoints dependentes dos computadores.
