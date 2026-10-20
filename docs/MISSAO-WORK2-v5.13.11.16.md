# Missão Work 2 — candidata v5.13.11.16

Base confirmada via GitHub: head `7f70344759ad21ffb071f4b1dd9cc84413c41983`, PR #40 aberta/Draft, main `9ac3dc167b9e67ac50628749d5ad33c563c09376`. Três workflows finais da v5.13.11.15 aprovados; 26/42 checkpoints funcionais, sem atribuir testes sintéticos aos PCs.

## A — integridade dos resultados

Falhas confirmadas: `ResidualReportMerger.Take(500)` descartava fontes/relatórios sem indicação; classificação truncava 30.000 evidências, 100 grupos e sete locais sem propagar todos os limites; exatamente 100 grupos era marcado como parcial mesmo sem corte; auditorias históricas/pós-desinstalação confundiam erros e diretórios grandes com ausência; interface descartava estados parciais vazios.

Correções: retirada do corte da união (entrada já limitada na descoberta); ordenação ordinal dos produtos e dos locais exibidos; cobertura tipada com motivos, fontes fixas consultadas/indisponíveis, entradas examinadas, grupos encontrados, grupos/locais fora da exibição e quantidade não avaliada quando conhecida. Quantidade impossível de determinar é explicitamente desconhecida. União preserva cobertura mesmo vazia; interface mostra análise parcial, inclusive quando não há achados. Leituras históricas e pós-desinstalação propagam erros/limites. Sem caminhos/nomes de aplicativos nos metadados agregados; detalhes de evidência permanecem locais.

Limites de descoberta permanecem cooperativos, não interrompem chamadas nativas bloqueadas. Profundidade de cobertura deliberadamente limitada a dois níveis; não há promessa de varredura completa do disco. Exibição de 100 grupos/sete locais não é paginação: omissões são contadas, tornam o resultado desconhecido e exigem nova investigação.

Validação local A: 26 verificações novas de integridade/saturação (500, 501, 1.000, 5.000 e 10.000 candidatos; 30.001 evidências; fronteira exata; cancelamento; fonte indisponível; união duplicada), além das 23 verificações locais anteriores. Aplicação compilada para Windows, sem erros; aviso SYSLIB0057 preexistente. Validação Windows real via CI ainda pendente neste marco.

## B, C e D

Em andamento. Exclusão real continua obrigatoriamente bloqueada. Sem alteração administrativa, merge na main, release estável, upload de diagnóstico ou manutenção autônoma.

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
