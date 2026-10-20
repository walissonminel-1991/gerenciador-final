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
