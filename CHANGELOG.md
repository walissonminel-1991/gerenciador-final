# Histórico de versões — Gerenciador Final

Este arquivo resume as alterações mais recentes do projeto. As versões anteriores a 5.12 são etapas históricas do desenvolvimento e não representam versões publicadas no GitHub.

## 5.13.10.8 TESTE — pintura leve e Registro estavel para computadores antigos
- Remove WS_EX_COMPOSITED aplicado indiscriminadamente a toda a janela; o estilo pode gerar custo alto de repintura em GPUs integradas e maquinas sem placa dedicada.
- Usa buffering somente na janela principal, mantendo a estrutura nativa e a transicao oculta/visivel pela bandeja.
- Prepara o layout antes de Show() ao restaurar a bandeja, evitando exibir uma arvore suspensa que precisaria ser remontada.
- Na abertura, executa o inventario sem animar o progresso ou alternar estados de dezenas de controles; operacoes solicitadas pelo usuario continuam com progresso e bloqueios.
- Agrupa mensagens consecutivas do Registro em lotes de 85 ms, sem descartar linhas; a fila existe apenas em memoria.
- Recalcula barra do Registro so quando ha mudanca de metricas, limitando invalidacoes em cascata.
- Teste no Windows verifica composicao global desativada, 70 linhas preservadas em um unico lote, 21 resolucoes e 6 transicoes para a bandeja.
- Nao altera a classificacao conservadora de residuos, os limites de exclusao nem a deteccao de drivers. Os registros historicos permanecem somente leitura.
- A aprovacao depende de observacao no PC de laboratorio, alem de verificacao posterior no PC FRACO.

## 5.13.10.7 TESTE — pintura unica, botoes proporcionais e sinais de programa ativo
- A janela utiliza WS_EX_COMPOSITED para compor todos os controles antes de apresenta-los; a restauracao evita reorganizacoes intermediarias enquanto esta oculta.
- Nomes das cinco abas passam a ser desenhados em linha unica com tipografia ajustada somente quando necessario, sem reticencias.
- Reduz alturas de abas e dos cartoes de ferramentas, libera largura na pesquisa, evita esticar botoes de acoes ate preencher todo o espaco.
- Remove a quebra manual no nome Limpeza de Disco automatica; tipografia dos botoes se ajusta ao espaco, sem truncar o titulo.
- Os contadores da aba Aplicativos usam os nomes completos quando couberem, eliminando as abreviacoes 'Apps' e 'Atual.' na abertura normal.
- A revisao historica cruza tambem nomes de processos em execucao, inclusive programas portateis como MPC-HC, sem concluir remocao pela ausencia de processo.
- O reconhecimento adicional nao apaga dados e nao confunde executaveis inativos, saves ou pastas remanescentes com prova de desinstalacao.
- Testes verificam estilo nativo de composicao, seis restauracoes com HWND estavel, botoes sem quebras, matriz de resolucoes e verificacoes de residuos somente leitura.
- GOG.com, McAfee e Microvirt foram informados como desinstalados no computador de teste; MPC-HC permanece ativo segundo o usuario. A ferramenta continua exigindo evidencias locais e nao transfere esse estado individual para outros computadores.
- A avaliacao de fluidez real no computador permanece necessaria.

## 5.13.10.6 TESTE — controle de rolagem e informacoes sem cortes
- Corrige rolagem fantasma provocada pela margem tecnica de 3 pixels no componente de pagina.
- Os cartoes de limpeza, reparo e discos deixam de ter barras internas ao serem incorporados numa pagina; se o conteudo for maior, apenas a pagina rola.
- A altura do painel de discos e calculada conforme a quantidade de unidades, atualizada quando a leitura termina, sem comprimir cartoes.
- Reparo e energia ocupam menos altura e evitam a barra externa quando a pagina cabe na area visivel.
- Botoes de disco e margens do painel de limpeza foram reduzidos sem remover funcionalidades; os limites de seguranca permanecem os mesmos.
- Os quatro atalhos da aba Inicializacao e sistema usam legendas curtas que cabem; as explicacoes completas ficam em dicas de acessibilidade.
- Resumo de aplicativos mostra sempre quantidade e atualizacoes, sem cortar a legenda apos o primeiro numero.
- Testes automatizados em 21 resolucoes e na referencia 1440x900 checam se existe rolagem duplicada ou fantasma.
- Bandeja, ausencia de maximizar, limite de 60 por cento e diagnostico local permanecem inalterados.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem classificados de modo conservador e so leitura, sem exclusao automatica.
- Aprovacao final depende da verificacao visual real, especialmente ao alternar de aba e revisar varias unidades de disco.

## 5.13.10.5 TESTE — bandeja fluida, mais informacao por tela e diagnostico
- Nao alternar ShowInTaskbar ao minimizar/restaurar: Hide e Show preservam o mesmo HWND e evitam repintura por recriacao.
- Suspende reorganizacoes de paginas e barras de rolagem durante transicoes de bandeja; amostragem de processos e consultas continuam ativas.
- Registra eventos de visibilidade e criacao/destruicao de janela no diagnostico ja existente, para diferenciar repaint de novo processo.
- Interface mais compacta: margens menores, cabecalho e comandos baixos, abas estreitas em duas linhas e log preservado.
- Linhas de aplicativos com 67 px, texto com quebra em vez de reticencias, dicas com identidade/versao integrais.
- Ferramentas com cartoes de 244x124 px e ajuste para 3 colunas quando houver largura; botoes aceitam multiplas linhas.
- Processos, inicializacao, atualizacoes do Windows e planos de energia usam cartoes menos altos e espacos internos menores.
- Reduz alturas minimas de paginas, conservando rolagem quando necessaria para nao esconder botoes.
- Testes de seis ciclos de minimizacao/restauracao sem mudar o HWND, alem da matriz de resolucoes e testes de seguranca anteriores.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem historicos e somente leitura; nenhuma exclusao automaticamente autorizada.
- A ausencia de duplicacao de APP.INICIALIZACAO no log anterior nao confirma a causa do desaparecimento, motivo pelo qual foram adicionados marcadores de ciclo de janela.
- Validacao visual no PC real e testes de longos nomes/monitores continuam necessarios.

## 5.13.10.4 TESTE — janela discreta e bandeja do Windows
- Em monitores a partir de 1366x768 (area util correspondente), a janela fica abaixo de 60% da area util; resolucoes menores mantem excecao para usabilidade.
- Tamanho inicial compacto de 875x745 ou menor quando necessario, sem aumentar fontes ou botoes conforme a resolucao.
- Removida a funcao e o botao de maximizar. Barra superior escura personalizada, com somente minimizar e fechar, incluindo dicas e realce ao passar o mouse.
- Minimizar oculta o programa da area de trabalho/barra de tarefas e mostra o icone de bandeja sem interromper tarefas, consultas nem a coleta periodica de CPU/RAM.
- Menu de bandeja Abrir e Sair; duplo clique restaura. Fechar encerra o aplicativo, diferentemente de Minimizar.
- Quando Sair e solicitado durante operacao protegida, solicita cancelamento seguro quando disponivel e espera a operacao terminar antes de encerrar.
- Nenhum arquivo de configuracao ou residuo novo e persistido por esses controles.
- Testes de layout e bandeja automatizados no Windows; validacao visual real continua necessaria.

## 5.13.10.3 TESTE — janela inicial compacta como a referencia 1440x900
- Substitui o tamanho inicial de 1250x980 pela dimensao de referencia 875x745: em monitores grandes, a janela deixa espaco livre ao redor.
- Abre centralizada dentro da area util do monitor; em telas pequenas limita ao espaco disponivel, sem perder navegacao, rolamento ou o registro.
- Mantem fontes, botoes e proporcoes visuais do layout atual sem ampliar a interface com a resolucao fisica (4K, 5K, 8K).
- Mantem suporte a redimensionamento manual, troca de monitor e escalonamento DPI existente.
- Acrescenta validacao explicita da referencia 1440x900, alem das 21 resolucoes anteriores.
- Ajusta os testes de regressao que ainda exigiam indevidamente a abertura em dimensoes grandes.
- Esta e uma compilacao de teste; a avaliacao visual no Windows real continua necessaria.

## 5.13.10.2 TESTE — consistencia de interface e registro
- Corrige o registro excessivamente baixo e reserva espaco real para diagnosticos em qualquer altura util.
- Usa largura e altura preferidas limitadas em telas 4K/8K; nao dimensiona fontes ou botoes de acordo com a resolucao nominal.
- Quebra os nomes das abas em duas linhas quando necessario, em vez de truncar o nome das funcionalidades.
- Ajusta o resumo da aba Aplicativos ao espaco, preservando a informacao completa em uma dica acessivel.
- Botoes de acoes de aplicativos permitem duas linhas para nao ocultar seus titulos.
- Testes com 21 resolucoes e simulacoes geometricas para 100%, 125%, 150% e 200% de DPI.
- A auditoria historica permanece informativa e somente leitura. GOG.com, McAfee, Microvirt e MPC-HC nao sao candidatos a exclusao automatica.
- Continuam necessarios testes visuais no equipamento real, principalmente 800x600 e DPI alto.

## 5.13.10.1 TESTE — resolucoes, DPI e multiplos monitores
- Matriz de verificacao automatizada com as 21 resolucoes fornecidas: 16:9, ultrawide, 4:3 e 16:10, incluindo 800x600 e 8K.
- A janela inicial permanece inteiramente dentro da area util de cada monitor; o tamanho minimo deixa de impor 970x690 em monitores maiores.
- Reposicionamento e reducao da janela ao trocar para monitores menores, alterar resolucao ou mudar DPI, sem armazenar preferencias no disco.
- Cabecalho e registro cedem espaco para a area de trabalho em alturas reduzidas.
- Abas com largura adaptavel, altura escalada por DPI e dicas mostrando o titulo completo quando o texto precisa ser encurtado.
- Cartoes de ferramentas ajustam sua largura ao espaco disponivel, sem criar barra horizontal nativa.
- Testes de geometria, navegacao e foco nao executam manutencao do sistema; as resolucoes altas nao geram capturas gigantes.
- A aprovacao final depende dos testes de CI no Windows e de validacao visual em equipamentos reais, incluindo 125%, 150%, 200% e mais de um monitor.

## 5.13.3 TESTE — precisão no inventário de resíduos
- Filtra namespaces compartilhados do Registro e componentes do Windows que estavam aparecendo como resíduos.
- A auditoria geral só apresenta candidatos com evidências de dados de aplicativo cruzadas com outra fonte independente; um único caminho, atalho ou vista do Registro não basta.
- Mantém candidatos como não confirmados, sem qualquer exclusão automática, e preserva a varredura histórica específica.
- Testes com casos extraídos do diagnóstico v5.13.2, incluindo Netease, dotnet, Classes, Windows Defender e K-Lite.
- Esta versão ainda exige validação da CI e testes no Windows real antes de aprovação.

## 5.12.3 — Instalador e distribuição
- Preparação de instalador Inno Setup para Windows x64 com consentimento UAC.
- Aplicativo .NET autocontido: não exige SDK no computador do usuário final.
- Atalhos no Menu Iniciar e opção de atalho na área de trabalho.
- Integração do ícone GF e preparação para futura assinatura digital.
- Sem criação automática de exceções no antivírus ou regras de firewall.

## 5.12.2 — Reparo de rede e interface
- O botão Reparar rede depende dos resultados do diagnóstico e solicita confirmação antes de agir.
- A redefinição completa da rede permanece separada do reparo específico.
- Ajuste da largura do botão Atualizar discos.

## 5.12.1 — Diagnóstico de rede
- Diagnóstico consultivo de adaptador, gateway, DNS e conectividade TCP/443.
- Resultados mostrados na aba Reparo e rede e registrados no log.
- Cartão Criar ponto de restauração com confirmação.
- Remoção de atalhos redundantes na interface.

## 5.12 — Reparação do Windows
- Fluxo de reparo com DISM RestoreHealth e depois SFC Scannow, com confirmação e UAC.
- Registro dos resultados e códigos de saída das verificações.

## Histórico anterior
As versões 5.4 a 5.11 introduziram progressivamente a interface escura, listas de aplicativos, ferramentas de limpeza, diagnóstico e gerenciamento do sistema. A documentação atual e o código-fonte representam o estado funcional da versão 5.12.3.

> **Observação:** estas são notas de desenvolvimento; compilações e testes de compatibilidade devem ser feitos no Windows antes de publicar binários para usuários finais.
