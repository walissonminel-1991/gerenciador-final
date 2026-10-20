# Missão Work 2 — candidata v5.13.11.16

Base confirmada via GitHub: head `7f70344759ad21ffb071f4b1dd9cc84413c41983`, PR #40 aberta/Draft, main `9ac3dc167b9e67ac50628749d5ad33c563c09376`. Três workflows finais da v5.13.11.15 aprovados; 26/42 checkpoints funcionais, sem atribuir testes sintéticos aos PCs.

## A — integridade dos resultados

Falhas confirmadas: `ResidualReportMerger.Take(500)` descartava fontes/relatórios sem indicação; classificação truncava 30.000 evidências, 100 grupos e sete locais sem propagar todos os limites; exatamente 100 grupos era marcado como parcial mesmo sem corte; auditorias históricas/pós-desinstalação confundiam erros e diretórios grandes com ausência; interface descartava estados parciais vazios.

Correções: retirada do corte da união (entrada já limitada na descoberta); ordenação ordinal dos produtos e dos locais exibidos; cobertura tipada com motivos, fontes fixas consultadas/indisponíveis, entradas examinadas, grupos encontrados, grupos/locais fora da exibição e quantidade não avaliada quando conhecida. Quantidade impossível de determinar é explicitamente desconhecida. União preserva cobertura mesmo vazia; interface mostra análise parcial, inclusive quando não há achados. Leituras históricas e pós-desinstalação propagam erros/limites. Sem caminhos/nomes de aplicativos nos metadados agregados; detalhes de evidência permanecem locais.

Limites de descoberta permanecem cooperativos, não interrompem chamadas nativas bloqueadas. Profundidade de cobertura deliberadamente limitada a dois níveis; não há promessa de varredura completa do disco. Exibição de 100 grupos/sete locais não é paginação: omissões são contadas, tornam o resultado desconhecido e exigem nova investigação.

Validação local A: 26 verificações novas de integridade/saturação (500, 501, 1.000, 5.000 e 10.000 candidatos; 30.001 evidências; fronteira exata; cancelamento; fonte indisponível; união duplicada), além das 23 verificações locais anteriores. Aplicação compilada para Windows, sem erros; aviso SYSLIB0057 preexistente. Validação Windows real via CI ainda pendente neste marco.

## B, C e D

Em andamento. Exclusão real continua obrigatoriamente bloqueada. Sem alteração administrativa, merge na main, release estável, upload de diagnóstico ou manutenção autônoma.
