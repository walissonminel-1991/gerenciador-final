# Matriz de validação — computadores reais

Esta bateria será realizada mais adiante. Sua preparação não interrompe as tarefas verificáveis por código, cenários simulados ou Windows remoto. O terceiro computador é opcional e ainda não está confirmado.

Identificar os ambientes como PC A, PC B e PC C; não registrar nome de usuário, número de série ou identificadores de hardware. Usar a mesma versão do pacote para comparar os resultados. Não presumir qual máquina é a mais fraca: preencher a configuração resumida quando estiver disponível.

| Ambiente | Windows / arquitetura | RAM / CPU resumidos | Resolução / escala | Antivírus | Disponibilidade |
|---|---|---|---|---|---|
| PC A | A preencher | A preencher | A preencher | A preencher | A combinar |
| PC B | A preencher | A preencher | A preencher | A preencher | A combinar |
| PC C (opcional) | A preencher | A preencher | A preencher | A preencher | Não confirmado |

| Caso | Critério de aprovação | PC A | PC B | PC C |
|---|---|---|---|---|
| Abrir e restaurar da bandeja | Sem fechamento inesperado; controles acessíveis | Não executado | Não executado | Opcional |
| Inventário | Conferir instalações conhecidas, escopos e arquiteturas | Não executado | Não executado | Opcional |
| Cobertura de atualizações | Comparação com WinGet na mesma máquina/horário, diferenças explicadas | Não executado | Não executado | Opcional |
| Atualizar aplicativo de teste | Confirmar instalação escolhida e versão final | Não executado | Não executado | Opcional |
| Desinstalar aplicativo dispensável | Remoção confirmada; arquivos pessoais e compartilhados preservados | Não executado | Não executado | Opcional |
| Auditoria histórica | Conferir vestígios conhecidos sem exclusão de itens ambíguos | Não executado | Não executado | Opcional |
| Consulta de drivers | Conferir exemplos de fornecedor, versão, INF e presença | Não executado | Não executado | Opcional |
| Histórico próprio | Duas consultas estáveis não inventam mudanças; reinício preserva a base | Não executado | Não executado | Opcional |
| Mudança de dispositivo | Opcional: periférico USB dispensável; ausência não vira autorização de remoção | Não executado | Não executado | Opcional |
| Desempenho | Registrar abertura, CPU/RAM estabilizadas e durante consulta | Não executado | Não executado | Opcional |
| AVG / outro antivírus | Registrar eventual detecção, horário e versão sem desativar proteção | Não executado | Não executado | Opcional |

Resultados possíveis: aprovado, falhou, inconclusivo, não aplicável. Para falha: anotar o caso, os passos, o esperado, o observado e a versão. Revisar capturas antes de compartilhar dados pessoais. Não enviar o histórico inteiro como substituto dos logs agregados.

## Histórico local de drivers

O arquivo `%LOCALAPPDATA%\GerenciadorFinal\Historico\drivers-v1.json` mantém a última consulta completa e até vinte resumos de mudanças. A coleta começa quando o usuário consulta DRIVERS; não reconstrói trocas anteriores à primeira consulta. Os hashes permitem comparação local e não são uma garantia de anonimização absoluta. Nomes e IDs brutos não são gravados.

Consulta parcial/vazia, arquivo corrompido, esquema desconhecido, relógio regressivo ou disputa de gravação preservam a base anterior. A interface deve continuar mostrando a consulta mesmo se o histórico estiver indisponível. Os detalhes retidos aparecem na dica do resumo e em sua descrição acessível.

O histórico pertence ao perfil local de cada computador. Não copiar o arquivo entre máquinas nem compartilhar o mesmo perfil de teste entre ambientes. Para reiniciar o histórico, fechar o programa e arquivar o arquivo antes de removê-lo; a próxima consulta completa criará uma nova base.
