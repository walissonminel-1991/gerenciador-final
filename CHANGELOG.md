# Histórico de versões — Gerenciador Final

Este arquivo resume as alterações mais recentes do projeto. As versões anteriores a 5.12 são etapas históricas do desenvolvimento e não representam versões publicadas no GitHub.

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
