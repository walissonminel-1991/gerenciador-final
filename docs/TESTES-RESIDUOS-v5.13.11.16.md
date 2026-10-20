# Testes supervisionados — v5.13.11.16 TESTE

Objetivo desta candidata: comparar cobertura, coerência e interpretação da análise. Exclusão de resíduos segue bloqueada; não há teste autorizado de remoção. Atualizações/desinstalações oficiais são fluxos independentes, com sua própria confirmação.

## Antes de abrir

Baixar o artefato da revisão final indicada na PR #40 e conferir SHA-256. Extrair em pasta nova, mantendo os arquivos ao lado do EXE. Não executar arquivo em quarentena nem desativar AVG/Defender/SmartScreen/UAC. Não usar pacote de execução intermediária do CI como candidata final.

## PC com histórico longo

1. Abrir normalmente e observar início, eventuais alertas e resposta da interface. Registrar versão do EXE, resolução e duração aproximada; não enviar caminhos de usuário.
2. Executar **Resíduos de aplicativos**. Conferir resumo: entradas/grupos examinados, grupos encontrados, locais/grupos fora da exibição, motivos e fontes indisponíveis. Quando qualquer limite, cancelamento ou falha impedir cobertura, a análise deve aparecer **PARCIAL**, inclusive sem achados.
3. Limite de profundidade indica que a busca ficou nos locais comuns e até dois níveis. Não é falha de exclusão nem promessa de examinar o computador inteiro. Grupos fora da exibição não são lixo confirmado.
4. Conferir WinMerge, foobar2000, VLC e perfis conhecidos. Playlists, preferências, histórico, saves e dados de fabricantes precisam permanecer preservados. Driver Booster/Netease devem exigir conferência; uma instalação portátil pode existir sem registro de desinstalação.
5. Conferir se uma mesma localização reaparece em fontes distintas com motivos preservados. Visões distintas de Registro podem aparecer separadas. Ancestral de outro produto deve receber preservação compartilhada.
6. Na prévia pós-desinstalação de programa fictício/de teste já autorizado, usar **Conferir novamente**. Não executar nova desinstalação só para cumprir este roteiro. Desinstalador ainda aberto ou auxiliares sem término comprovado não autorizam exclusão.
7. Em uma pasta criada exclusivamente para teste, adicionar/modificar um arquivo entre prévia e conferência. A comparação deve invalidar a prévia. Não simular trocas de ACL/junctions em perfis ou pastas reais.
8. Fechar a revisão e cancelar/fechar o GF durante busca. Conferir ausência de travamento persistente e de mensagem enganosa de conclusão. Uma chamada nativa do Windows em andamento pode demorar a devolver o controle.

## PC FRACO

Repetir consulta somente leitura em 1440×900; medir tempo e responsividade. Conferir navegação, rolagem escura, leitura do aviso parcial, dados preservados e ausência de botão funcional para apagar resíduos. Comparar com versão .15 sem interpretar mudança de quantidade como ganho/perda comprovada até comparar limites e fontes.

## Evidências úteis para próxima missão

Registrar: versão, cenário, duração aproximada, mensagem de parcialidade e contagens, enquadramento esperado/observado e presença de alerta AVG. Capturas de revisão podem conter caminhos e nomes de programas; revisar/ocultar informações pessoais antes de compartilhar. Logs agregados devem continuar sem envio remoto e sem incluir nomes/caminhos de resíduos.

Não validar estabilidade, proteção antivírus ou futura exclusão segura apenas por abertura bem-sucedida. Próxima missão: corrigir divergências reais e desenhar paginação/inspeção sob demanda, com continuidade explícita dos limites. Executor destrutivo continua fora da candidata.
