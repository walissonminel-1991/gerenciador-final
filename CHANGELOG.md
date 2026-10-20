# Histórico de versões — Gerenciador Final

## v5.13.11.5 — protecao de instaladores, pipe e arquivos locais (TESTE)

- Receita integral aprovada e comprovante em memoria vinculam o instalador ao download desta sessao.
- SHA-256 e assinatura sao revalidados antes da execucao; arquivo permanece sem compartilhamento de escrita/exclusao enquanto o processo iniciado e aguardado. Terminal recebe trava equivalente durante deployment.
- P/Invokes do GF usam somente a pasta de sistema para carregar DLLs nativas.
- Pipe administrativo verifica PID informado pelo Windows e limita espera da autenticacao a cinco segundos.
- Privacidade usa temporarios exclusivos com CreateNew; recusa pastas redirecionadas e limita leitura pelo arquivo aberto.
- Testes de adulteracao, receitas forjadas, PID incorreto e temporarios preexistentes. Limites documentados em docs/SEGURANCA-v5.13.11.5.md.


## v5.13.11.4 — auditoria de segurança, diagnóstico e consistência (TESTE)

- Comandos internos Windows e Gerenciador de Tarefas usam caminhos absolutos da pasta de sistema, com lista fechada; evita procurar executáveis homônimos no diretório portátil/PATH.
- Limpeza avançada só pode excluir pastas/chaves simultaneamente válidas na reconsulta e presentes na confirmação original.
- Diagnóstico expirado não pode ser compartilhado quando sua exclusão é bloqueada pelo Windows; sessão vazia preserva pendência anterior.
- Reaceitar coleta já ativa preserva contagens; consentimento inválido/ambíguo e versão com quebra de linha são recusados.
- Roadmap reconciliado: a checklist publicada contém 27/42 checkpoints (64,3%), diferente do resumo histórico 30/45. Evidências de monitoramento atualizadas para o painel RAM.
- Regressões para comandos, escopo autorizado, consentimento, retomada e expiração; relatório em docs/AUDITORIA-v5.13.11.4.md.


## v5.13.11.3 — retomada e revogação do diagnóstico (TESTE)

- Revisão no GitHub usa o resumo da sessão atual ou, sem eventos novos, o relatório local anterior ainda válido, preservando sua versão original.
- Pendências são revalidadas por tamanho, conteúdo permitido e prazo de sete dias também ao abrir a revisão.
- Revogar interrompe a coleta imediatamente nesta sessão mesmo quando o disco impede gravar a preferência; falhas de gravação/exclusão são informadas sem afirmar sucesso.
- Aviso antes de abrir o navegador explica que a URL já transmite o resumo ao GitHub; criar a Issue pública ainda exige confirmação no site.
- Regressões para retomada, prioridade da sessão atual, adulteração, expiração durante a execução e falhas de disco.
- Publicação continua manual no GitHub; testes reais/AVG e automação por IA continuam pendentes.


## v5.13.11.2 TESTE — piloto colaborativo GitHub-only
- Menu da bandeja permite revisar dados técnicos agregados e abrir um formulário de Issue no repositório público, com autenticação pelo próprio navegador do usuário.
- Dupla confirmação humana: aviso de publicidade no GF e envio final no GitHub. Nenhum token, conta vinculada automaticamente, upload ou transmissão invisível é adicionado.
- Filtro estrito de schema, versão, categorias e contagens antes de montar URL; recusa de dados arbitrários/relatórios vazios; testes offline dedicados.
- Mantém a coleta local/consentimento anterior. GitHub App e envio automático ao encerrar ainda não implementados.


## v5.13.11.1 TESTE — privacidade e diagnostico colaborativo local
- Consentimento explícito na primeira abertura, recusável e revogável pelo menu da bandeja; sem caixa pré-selecionada.
- Resumo estrito com versão do GF, encerramento e contagens de 14 categorias técnicas, sem logs brutos, nomes, caminhos, inventário ou IDs persistentes.
- Somente um relatório pendente local, substituído ao encerrar, com expiração de 7 dias e limpeza ao revogar; não utiliza a rede.
- Código de referência para recepção HTTPS/Cloudflare D1 com TTL, ainda **não implantado**. Transmissão automática futura dependerá de novo consentimento e revisão de privacidade.
- Testes próprios de opt-in, recusas, limites, sanitização, rotação e retenção.


## v5.13.11.0 CANDIDATA — novas fontes e consolidação para GitHub
- Quatro novas consultas oficiais somente leitura: ShareX, HandBrake, Rufus e qBittorrent. URLs fixas GitHub Releases; nomes e editores conferidos em catálogo interno antes da chamada. Nenhuma instalação automática habilitada pelas novas fontes.
- Regressões com fontes simuladas: candidatos, versões oficiais, falso editor, nomes parecidos, cache, pré-lançamento, falha de provedor e fusão sem duplicar linhas.
- Bateria abrangente Windows para comparar motores de versões, instaladores, drivers, resíduos, RAM, inventário MSIX e compatibilidade de interface.
- Projeto e manifesto atualizados para v5.13.11.0; README e identificação do pacote atualizados; nome do ZIP com versão e commit, BUILD-INFO e SHA256SUMS verificado em CI.
- Consolidado o desenvolvimento das séries v5.13.10.19 a v5.13.10.24: histórico de drivers, resíduos específicos, tema escuro, painel de RAM, otimização de caches próprios, instrumentação de bandeja/erros MSIX e validação automatizada.
- Estado: CANDIDATA. Validação em PC FRACO e segundo PC, assinaturas digitais e avaliação de alerta AVG ainda pendentes. Sem afirmação de segurança final, estabilidade pública ou cobertura universal.


Este arquivo resume as alterações mais recentes do projeto. As versões anteriores a 5.12 são etapas históricas do desenvolvimento e não representam versões publicadas no GitHub.

## 5.13.10.18 TESTE — consolidacao e inventario consultivo de drivers
- Diagnostico de desinstalacao considera ambas as alternativas registradas; caminho absoluto ausente nao herda confianca do nome msiexec.exe.
- Leitura incompleta do Registro bloqueia os fluxos que precisam confirmar ausencia antes da limpeza; auditoria contextual informa limitacoes do inventario.
- Confirmacao posterior exige a identidade original da instalacao; Sumatra confere tambem arquitetura PE e escopo. Mudanca de identidade permanece indeterminada, sem afirmar sucesso.
- Diagnostico de cobertura registra apenas contagens por motivo, sem nomes de aplicativos; nao presume equivalencia com WinGet.
- DRIVERS recebe tabela consultiva de associacoes dispositivo/INF/versao/fornecedor, presenca e assinatura informada pelo WMI, sob demanda e em segundo plano. Nao enumera todo Driver Store, nao instala, remove ou reverte drivers.
- Consulta de drivers tem limite de espera na interface e impede consultas sobrepostas enquanto WMI termina. Identificadores completos de dispositivos nao sao gravados no log.
- Consulta WMI real validada no runner; enumeracao COM corrigida apos falha reproduzida.
- Aba DRIVERS preserva altura da tabela e permite rolagem em janelas pequenas; captura 800x600 revisada.
- Nove grupos de regressao, 22 cenarios de layout e ativacao do pacote aprovados. Validacao nos computadores do usuario continua pendente.

## 5.13.10.17 TESTE — consolidacao antes dos testes nos computadores
- Verificacao de caminhos de desinstalacao movida para o mesmo trabalho em segundo plano do inventario, antes de entregar o snapshot a interface.
- Resumo acessivel e dica do contador distinguem inventario completo, itens com acao, sem acao, desinstalaveis e atualizacoes, sem duplicar itens com duas acoes.
- Confirmacao posterior de instalacao exige editor reconhecido e recusa entradas somente leitura; nome e versao isolados nao confirmam sucesso.
- Novos casos de regressao para contadores, fabricante divergente, arquitetura Visual C++, receita desconhecida e versao antiga.
- Roadmap de consolidacao com checkpoints verificaveis; validacao nos dois computadores permanece separada.
- Nao comprova resolucao do AVG, ganho de desempenho real ou cobertura universal. Correlacao de arquitetura/escopo na confirmacao posterior de todos os instaladores permanece pendente.

## 5.13.10.16 TESTE — PC FRACO e procedencia verificavel
- A amostragem do monitor usa 2 s somente na aba Processos, 12 s nas demais abas e 20 s ao minimizar para a bandeja; a coleta continua na bandeja.
- A primeira coleta de processos aguarda o intervalo do timer, sem competir imediatamente com o inventario inicial; ao abrir Processos ou restaurar a janela, atualiza sob demanda.
- Consulta de atualizacoes de aplicativos precede a consulta do Windows Update, com pausa curta; ambas continuam somente leitura e sem instalacao automatica.
- O artefato inclui hashes SHA-256 para o executavel e seus arquivos principais; facilita confirmar a integridade, mas nao substitui assinatura digital ou avaliacao do AVG.
- Nenhuma protecao e desativada, nenhuma exclusao antivirus criada e nenhuma deteccao e contornada. Ganhos de desempenho devem ser verificados no PC FRACO.

## 5.13.10.15 TESTE — monitor de processos semelhante ao Gerenciador de Tarefas
- Substitui cartoes de CPU por tabela de processos com CPU instantanea, RAM, PID e entrada/saida em KB/s; colunas ordenaveis e filtro por nome/PID.
- CPU de processos usa diferenca de TotalProcessorTime normalizada pela quantidade de processadores logicos; CPU total usa GetSystemTimes; RAM total usa GlobalMemoryStatusEx; trafego total de rede usa contadores de adaptadores ativos.
- GetProcessIoCounters informa bytes de leitura/escrita acumulados e suas taxas. Esse numero nao e disco fisico: nao inferir consumo de disco nem rede por processo.
- Primeira amostra e acesso negado exibem valor indisponivel em vez de inventar zero. Processos que terminam nao interrompem o monitor.
- Atualiza a cada 2 segundos somente quando a aba Processos esta visivel; em segundo plano e na bandeja passa para 5 segundos; nenhuma operacao pode encerrar processos.
- Tabela WinForms com pintura dupla e atualizacao incremental de celulas (sem apagar todas as linhas quando a ordem nao muda), com filtro preservado e scroll restaurado.
- Botao Tarefas do Windows abre Task Manager nativo para informacoes que nao sao medidas pelo Gerenciador Final.
- Ampliados testes de layout e testes de amostras CPU/E/S, memoria, PID, ordenacao, filtro, refresh sem remontagem e dados sinteticos. Preservadas regras de seguranca e residuos.
- Nenhum codigo ou elemento grafico proprietario do Task Manager foi copiado.

## 5.13.10.12 TESTE — descricoes da ultima aba e Atualizar lista na area correta
- A aba Inicializacao e sistema volta a exibir as quatro descricoes completas, em texto legivel abaixo dos botoes (Processos, Inicializacao, Windows Update e Este computador), com aviso acessivel e tooltip.
- Amplia a altura reservada aos comandos para evitar que suas descricoes fiquem ocultas por falta de espaco.
- Corrige seis verificacoes com indice de aba antigo no painel integrado: depois de incluir DRIVERS, o Sistema passou a ser a sexta aba (indice 5), evitando deixar de exibir resultados atualizados de Processos e Windows Update.
- A opcao Atualizar lista sai da faixa de pesquisa e passa a integrar os cinco comandos de Aplicativos, antes de Verificar atualizacoes.
- Em janelas estreitas, os cinco comandos sao distribuidos em duas linhas proporcionais (tres em cima e dois embaixo), sem abreviar nomes.
- O campo de pesquisa mantem apenas Pesquisar, filtro textual e Limpar; Atualizar lista permanece uma leitura local, enquanto Verificar atualizacoes realiza consulta online.
- Testes automatizados cobrem as descricoes de cada comando nas 22 resolucoes da matriz, visibilidade das legendas, posicao de Atualizar lista, distribuicao responsiva dos cinco comandos e ausencia de indices antigos da ultima aba.
- Nao modifica acoes de drivers, politicas de limpeza, inventario ou o tratamento conservador de residuos de GOG.com, McAfee e Microvirt (somente leitura).
- Versao ainda sujeita a avaliacao presencial no PC FRACO.

## 5.13.10.11 TESTE — sexta aba DRIVERS e navegacao uniforme
- Insere a aba DRIVERS imediatamente depois de Aplicativos; e uma area apenas visual com Inventario, Atualizacao/instalacao e Historico/reversao como funcionalidades futuras.
- Nenhum driver e consultado, instalado, removido ou alterado pela aba provisoria. Nenhuma funcao de manutencao existente foi modificada.
- As seis abas distribuem-se uniformemente em toda a largura disponivel, com margens laterais simetricas de ate 12 pixels e pequenas separacoes iguais entre botoes.
- Titulos completos sao desenhados em uma linha sempre que possivel; em janelas estreitas, usam quebra de linha natural para preservar o texto.
- Ajusta os indices internos de Limpeza, Reparo, Planos de energia e Sistema para evitar carregamento da pagina errada apos inserir DRIVERS.
- A pagina provisoria possui tres cartoes informativos, reorganizados em uma, duas ou tres colunas conforme a largura disponivel. Sem botoes que prometam funcionalidades inexistentes.
- Aumenta a bateria de testes de layout para 22 cenarios x 6 abas = 132 combinacoes, verificando margens simetricas, largura uniforme, hit-test de todas as abas e ausencia de acao na pagina DRIVERS.
- Testa 12 ciclos de bandeja sem recriar janela, 150 linhas de Registro em um lote e sete escalas simuladas de DPI alem dos testes funcionais e de seguranca ja existentes.
- Mantem o limite de ate 60% da area util como teto, podendo ocupar menos quando adequado. A aparencia final ainda requer conferencias no Windows real e no PC FRACO.

## 5.13.10.10 TESTE — densidade visual consistente em 1920x1080 e 2560x1440
- Preserva a janela proporcional e o teto de 60% da area util nas 21 resolucoes.
- Alinha a configuracao DPI do executavel e dos testes no modo PerMonitorV2; trocar de monitor ou escala 100/125/150% deve manter textos com escala nativa do Windows.
- Centraliza as cinco abas com o mesmo espacamento dos dois lados, incluindo a logica de acerto de clique e navegacao por teclado.
- Limpeza distribui oito ferramentas em no maximo quatro colunas, e Reparo distribui cinco ferramentas em no maximo tres colunas; os cartoes aproveitam melhor a largura sem esticar botoes.
- Discos locais podem usar tres colunas quando ha espaco; altura do painel de limpeza acompanha os discos detectados em vez de preencher o resto da janela com uma caixa vazia.
- Diagnostico de rede passa a ter altura baseada no conteudo, deixando a area sem informacoes no fundo neutro.
- Alteracoes de resolucao do Windows recalculam o tamanho proporcional da janela, com 170 ms de consolidacao de eventos para evitar redesenhos repetidos; o redimensionamento manual continua sujeito ao limite.
- Acrescenta testes de distribuicao de cartoes em Full HD e 1440p, preservando rolagem, bandeja e protecao de dados.
- Nenhum mecanismo de busca, desinstalacao, limpeza ou driver foi alterado nesta revisao visual.

## 5.13.10.9 TESTE — proporcao universal de ate 60% da area da tela
- Substitui a janela inicial fixa de 875x745 por dimensoes proporcionais a area util real do monitor, independentemente da resolucao (inclusive 800x600 e 8K).
- A abertura utiliza cerca de 57,4% da area util (70% da largura e 82% da altura); o limite de redimensionamento usa no maximo 59,76% (72% da largura e 83% da altura).
- O conteudo interno agora acompanha todo o espaco da janela, sem o teto de 875x745 escalonado por DPI que mantinha a interface pequena em 2560x1440.
- Mantem os tamanhos de fonte controlados pelo DPI do Windows: a janela cresce sem multiplicar artificialmente textos e botoes.
- Ao trocar de monitor ou DPI, limita novamente o tamanho e reposiciona a janela dentro da area util, sem salvar configuracoes no disco.
- Nao permite maximizar. Continua com minimizar para bandeja, menu Abrir/Sair e operacoes de leitura em andamento.
- Testes de 21 resolucoes + 1440x900 e de monitores 125% verificam limite de area, minimo de aproveitamento de 55% e centragem.
- A interface visual final continua sujeita a capturas e validacao nos computadores de laboratorio e PC FRACO.
- Nenhuma alteracao em limpeza, atualizacao, catalogo, instaladores, drivers ou registros historicos.

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
