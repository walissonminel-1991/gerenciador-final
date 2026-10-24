# Missão Work 2 — candidata v5.13.11.16

Base confirmada via GitHub: head `7f70344759ad21ffb071f4b1dd9cc84413c41983`, PR #40 aberta/Draft, main `9ac3dc167b9e67ac50628749d5ad33c563c09376`. Três workflows finais da v5.13.11.15 aprovados; 26/42 checkpoints funcionais, sem atribuir testes sintéticos aos PCs.

## A — integridade dos resultados

Falhas confirmadas: `ResidualReportMerger.Take(500)` descartava fontes/relatórios sem indicação; classificação truncava 30.000 evidências, 100 grupos e sete locais sem propagar todos os limites; exatamente 100 grupos era marcado como parcial mesmo sem corte; auditorias históricas/pós-desinstalação confundiam erros e diretórios grandes com ausência; interface descartava estados parciais vazios.

Correções: retirada do corte da união (entrada já limitada na descoberta); ordenação ordinal dos produtos e dos locais exibidos; cobertura tipada com motivos, fontes fixas consultadas/indisponíveis, entradas examinadas, grupos encontrados, grupos/locais fora da exibição e quantidade não avaliada quando conhecida. Quantidade impossível de determinar é explicitamente desconhecida. União preserva cobertura mesmo vazia; interface mostra análise parcial, inclusive quando não há achados. Leituras históricas e pós-desinstalação propagam erros/limites. Sem caminhos/nomes de aplicativos nos metadados agregados; detalhes de evidência permanecem locais.

Limites de descoberta permanecem cooperativos, não interrompem chamadas nativas bloqueadas. Profundidade de cobertura deliberadamente limitada a dois níveis; não há promessa de varredura completa do disco. Exibição de 100 grupos/sete locais não é paginação: omissões são contadas, tornam o resultado desconhecido e exigem nova investigação.

Validação local A: 26 verificações novas de integridade/saturação (500, 501, 1.000, 5.000 e 10.000 candidatos; 30.001 evidências; fronteira exata; cancelamento; fonte indisponível; união duplicada), além das 23 verificações locais anteriores. Aplicação compilada para Windows, sem erros; aviso SYSLIB0057 preexistente. Validação Windows real via CI ainda pendente neste marco.

## Restrições permanentes da missão

Exclusão real continua obrigatoriamente bloqueada. Sem alteração administrativa, merge na main, release estável, upload de diagnóstico ou manutenção autônoma.

## B — bloqueios e prévia

Auditoria por referências/sinks: cinco pontos internos de exclusão (`Clean`, `CleanCandidateFiles`, `DeleteApprovedTree`, `DeleteApprovedRegistryKey`, `AdvancedUninstallCleaner.Clean`) permanecem sem chamadas destrutivas, inclusive com allowlist exata. Botões da revisão não apagam; somente desinstaladores oficiais possuem execução própria com confirmação. Limpeza de temporários (`AppServices.CleanDirectorySafely`), descarte de pacotes de atualização, histórico próprio, telemetria local e alteração consentida de inicialização são operações distintas; nenhuma é alcançada pelo motor de resíduos. Rotinas genéricas de temporários ainda usam exclusão textual, sem identidade contínua: não reutilizar para resíduos.

Falha de decisão confirmada: contar itens preservados não detecta troca de um arquivo desconhecido por outro. Prévia agora recusa integralmente pastas com dados misturados, copia os alvos do chamador, rejeita duplicações e caminhos ambíguos, reenumera somente para invalidar (nunca ampliar seleção) antes/depois da captura e aplica timeout cooperativo de oito segundos também à conferência. Capturas incompletas/alteradas são reportadas como parciais, com estado desconhecido.

Testes: chamadas diretas dos cinco bloqueios; Registro HKCU sintético; ACL de leitura negada em arquivo sintético; junction NTFS sintética; lista mutável, alvos duplicados, conteúdo novo antes da captura. Fixtures isolados; teardown só remove recursos próprios. Testes Windows de ACL/junction aguardam CI; bloqueio direto de arquivos e oito testes Python passaram localmente. Guardrail estático impede que os componentes de resíduos passem a invocar exclusão, shell, rede ou limpador genérico; não substitui análise semântica.

Limitação mantida: prévia não retém handles até uma futura execução, não comprova propriedade exclusiva nem término dos auxiliares/MSI/serviços. Hash não é autorização. Nenhuma remoção foi reativada.

## C — precisão e desempenho

B aprovado nos três workflows: política 39508255073, build 39508255116 e regressões Windows 39508255014 (incluindo ACL de leitura negada, junction NTFS, hard links e todos os cinco bloqueios). O CI de A foi substituído pelo de B por concorrência, não por falha de teste.

Correções C: ProgramData e preferências do Registro também preservam dados; diretório em Program Files ou Netease implica estado desconhecido, e Driver Booster exige conferência. Proteção geral de perfis desconhecidos não depende da lista fixa. União reconcilia aliases de Registro sem misturar 32/64 bits, preserva tipos/motivos e marca pastas ancestrais/descendentes de produtos diferentes como compartilhadas. Evidências incompatíveis no mesmo local ficam parciais/desconhecidas.

Inventário e processos são normalizados/indexados uma vez, com comparação de prefixos por fronteira de palavra. Classificação tem limite cooperativo de cinco segundos; cancelamento durante classificação mantém estado parcial. Corrigida regressão durante desenvolvimento: flag de limite por raiz não pode interromper o orçamento global; teste cobre continuação das demais fontes. Profundidade limitada, indisponibilidade e falhas de fontes não percorridas são informadas. Não se afirma cobertura completa do computador.

Novos casos sintéticos: onze fabricantes conhecidos, perfis desconhecidos/foobar/VLC, nomes próximos, Netease, instalação portátil, pai/filho compartilhados, aliases de Registro, contradição e continuação após limite local. Dez mil evidências/cinco mil instalações/mil processos avaliados com todos os 5.000 grupos contabilizados; medição local apenas orientativa, não equivale ao PC FRACO. Teste de interface Windows adiciona relatório parcial vazio com exclusão indisponível. Nenhuma alteração em atualização oficial do Notepad++/receitas.

Contagens: entradas são itens visitados na descoberta (ou evidências recebidas na classificação isolada); grupos examinados são nomes elegíveis correlacionados; candidatos encontrados são grupos não protegidos com indícios independentes. Evidências não avaliadas são conhecidas somente quando a fonte oferece tamanho; caso contrário, quantidade desconhecida. Fontes sem acesso não são tratadas como inexistentes. Limite de exibição não descarta resultados silenciosamente.

## D — segurança GitHub

C aprovado nos três workflows (política 39508752193, Windows 39508751898, build 39508752010), incluindo interface parcial vazia e novas regressões de classificação. Nove verificações Python e dez testes Node locais aprovados após reforço dos workflows.

43 workflows auditados; 38 históricos corrigidos com Actions fixadas em SHA oficial e checkout sem persistência de credenciais. YAMLs analisados com parser, todas as configurações confirmadas. Três checks atuais recebem nomes únicos para futura exigência em proteção de branch. Dependabot e auditoria NuGet bloqueante mantidos; sem pacotes de terceiros declarados nos projetos. Main segue sem proteção/rulesets e auto merge desativado; integração não lê endpoint administrativo (403). Configuração exigida do proprietário detalhada em [Segurança GitHub](SEGURANCA-GITHUB-WORK2.md). Nenhuma configuração administrativa alterada.


## Consolidação da candidata

Versão incremental correta: v5.13.11.16 TESTE. Nenhuma versão intermediária foi entregue como candidata final. Correção adicional de integração: resumos compostos e coberturas por fonte podiam somar as mesmas contagens duas vezes; a união agora mantém as observações originais e deduplica a composição. Teste de resumos aninhados acrescentado. Motivos de parcialidade traduzidos para linguagem acessível, sem nomes internos de enums.

Validação anterior à versão: C com 260/260 verificações Windows, UI, demais regressões e três workflows aprovados. Local final: 75 verificações específicas; nove testes Python de segurança/privacidade e dez testes Node. Compilação local Windows sem erros; avisos preexistentes SYSLIB0057 e CS8602 documentados. O novo teste de cobertura aninhada ainda precisa da aprovação final Windows junto à versão .16. Nenhum teste enfraquecido ou desativado para obter aprovação.

Arquivos centrais alterados: ResidualReportMerger.cs, ResidualScanBudget.cs, BroadResidualDiscovery.cs, ResidualAudit.cs, HistoricalResidualAudit.cs, ResidualPreview.cs, ResidualAuditDialog.cs, UninstallResiduals.cs, AdvancedUninstallCleaner.cs e Program.cs. Novas suítes ResidualIntegrityTests.cs, ResidualBlockingTests.cs, ResidualPrecisionTests.cs; ampliações em ResidualMissionTests.cs, TestesResiduos/Program.cs, TestesLayout/Program.cs e scripts/test_politicas_seguranca.py; 41 YAMLs alterados (38 históricos e três checks atuais), versão/manifesto/README/changelog/roadmap e documentos desta missão.

Pacote final só deve ser indicado após três workflows da mesma revisão aprovados. A PR #40 registra commit do head, commit de integração usado no CI, igualdade de árvores, IDs de runs/artefato, SHA-256 do ZIP e conferência de cada entrada do manifesto. O commit de integração da PR é criado pelo GitHub para testes, sem merge na main. O fechamento da PR deve conter esses resultados para não alterar o commit do pacote após validá-lo.

Continuidade: seguir o roteiro .16 no PC de histórico longo e no PC FRACO; medir cobertura/limites, erros e responsividade, preservar perfis e conferir AVG sem desativar proteções. Limitações remanescentes: chamadas nativas não preemptíveis, profundidade e exibição limitadas (com aviso/contagem, sem paginação), propriedade exclusiva não comprovada, handles liberados após leitura, auxiliares do fabricante não rastreados integralmente e limpeza de temporários separada com exclusão textual antiga. Não reutilizar essa rotina como executor de resíduos. Não há autorização de remoção real nesta missão.

Próxima missão Work: começar pela PR/commit final e pelos resultados reais deste roteiro; corrigir apenas divergências novas; avaliar inspeção sob demanda/paginação sem perder cobertura/consentimento. Não reauditar tudo nem habilitar executor, telemetria remota ou manutenção autônoma. Franquia Work não consultável por ferramenta; nenhum saldo estimado.

D aprovado nos três workflows antes da candidata: política 39509088643, Windows 39509088640 e build 39509088564, com os novos nomes de jobs. A revisão de versão/contagem aninhada é validada na rodada final, cujos resultados ficam na PR.


Conferência final de fonte: `App Paths` usava `File.Exists`, que transforma erros de leitura em `false`. Substituído por leitura de atributos que distingue ausência confirmada de erro/tipo inesperado/caminho redirecionado. Incerteza dessa proteção agora torna a análise parcial. Dois testes locais de existência/ausência e pista não verificável adicionados; revisão de segurança feita na mesma v5.13.11.16, sem entregar candidata intermediária.
