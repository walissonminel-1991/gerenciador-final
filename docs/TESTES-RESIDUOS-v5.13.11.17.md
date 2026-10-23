# Roteiro de testes reais — v5.13.11.17 TESTE

A candidata é experimental e permanece sem assinatura. Usar o ZIP final associado ao commit aprovado da PR #40, conferir SHA-256 e extrair em pasta nova com todos os arquivos adjacentes. Não executar se estiver em quarentena; manter proteções do Windows e antivírus ativos.

## Primeiro: PC FRACO, 1440×900

Preencher [checklist de medições](TESTES-HISTORICOS-PC-FRACO.md#registro-7). A bateria usa consultas de inventário/atualizações/resíduos, busca, filtros, paginação e inspeção somente leitura. Não instalar/desinstalar programas, limpar temporários, editar Registro ou operar drivers.

1. Registrar tempo até primeira janela funcional e inventário. Manter estado dos demais programas anotado.
2. Consultar atualizações sem executar instaladores; observar tempo, CPU, RAM e troca de abas.
3. Em **Limpeza e desempenho → Resíduos de aplicativos**, observar progresso e botão Cancelar. Quando a revisão abrir, conferir contagens de resultados disponíveis e de outras páginas.
4. Navegar páginas, buscar um programa/fabricante presente em outra página, combinar classificação/origem e voltar à página anterior. Conferir seleção, rolagem e texto completo nos detalhes.
5. Selecionar um local e usar **Inspecionar local**. Conteúdo dos arquivos e valores do Registro não é lido; filhos são metadados. Limites/indisponibilidade mantêm análise parcial. A inspeção não conclui que os dados são lixo nem autorização de remoção.
6. Cancelar busca principal, filtro/inspeção e fechar revisão com leitura pendente. Esperar a chamada atual do Windows retornar. Não deve aparecer resposta antiga sobre outra seleção nem conclusão enganosa.
7. Minimizar para bandeja/restaurar durante consulta, trocar de aba e repetir após fechar/reabrir. Registrar alertas AVG, sem desativá-lo ou forçar exclusão.
8. Comparar com .16 em condições equivalentes: .16 limitava exibição no motor a 100 grupos/sete locais; .17 preserva todos os classificados dentro do teto da descoberta. Maior contagem exibida não comprova instalação/desinstalação ou cobertura integral.

## Depois: computador com histórico extenso

Repetir consultas somente leitura, dando prioridade à classificação e cobertura. Conferir fabricantes de jogos, variantes antigas/novas, portáteis e caminhos de reinstalação. Revisar WinMerge e `Thingamahoochie\WinMerge`, foobar2000 e VLC quando presentes. Não reinstalar/desinstalar aplicativos só para este teste e não concluir que um registro ausente confirma o término do desinstalador.

Comparar listas por grupo/local e motivo, não apenas totais. Resultados reconciliados têm motivos/tipos/origens unidos; visões 32/64 distintas podem permanecer separadas. Perfis ou pasta ancestral de outro aplicativo precisam continuar preservados. Grupos protegidos pelo inventário e indícios isolados são separados de duplicatas/locais não examinados.

## Limites que precisam ser compreendidos

- Descoberta geral: 30.000 entradas, 2.500 por raiz, 128 filhos por contêiner, dois níveis e 14 segundos cooperativos; classificação com cinco segundos.
- Páginas: até 100 grupos por chamada (UI usa 50) e 30 locais por página. Limitar a página não descarta grupos ou locais classificados.
- Inspeção: 512 entradas por padrão, máximo 2.048, cinco segundos, dois níveis adicionais a partir do local inicial e 4.096 novos contêineres por sessão. Chaves enormes podem ser recusadas antes de alocar listas. Não há retomada automática de fonte que excedeu o orçamento.
- Revisão avançada: orçamento global de 30.000 entradas/10 segundos; qualquer falha/limite exige preservar e indicar parcialidade.
- Chamadas nativas não podem ser interrompidas arbitrariamente. Fotografia/cache não é monitoramento transacional em tempo real. Nova inspeção não torna completa uma descoberta anterior parcial.
- Reversão, propriedade exclusiva e término de todos os auxiliares/serviços/tarefas não são comprovados. Cinco bloqueios destrutivos continuam ativos.

## Retorno para desenvolvimento

Enviar somente observações necessárias: versão, cenário, duração/contagens agregadas, resolução/escala e classificação esperada/observada. Revisar dados pessoais em capturas/logs. Não há envio remoto automático. A próxima missão deve começar pelas divergências reais e pelo relatório Work 3, sem repetir auditorias completas nem habilitar exclusão.
