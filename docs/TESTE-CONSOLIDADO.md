# Validação do lote v5.13.10.19

Quando iniciarmos a bateria, executar a mesma sequência nos computadores disponíveis. Usar [MATRIZ-COMPUTADORES.md](MATRIZ-COMPUTADORES.md), que admite um terceiro computador opcional. Registrar qual máquina foi usada, a versão exata e o resultado. Não instalar SDK. Manter todos os arquivos do pacote portátil juntos.

## 1. Abertura e proteção

- Abrir o programa com o antivírus ativo.
- Se houver bloqueio: registrar captura do alerta, nome da detecção, horário e a versão do Gerenciador Final. Não insistir em instalação/limpeza durante o bloqueio.
- Conferir se o arquivo de diagnóstico identifica v5.13.10.19, incluindo a última revisão.
- Abrir, minimizar para bandeja e restaurar três vezes. Registrar travamento, fechamento ou piscada observada.

## 2. Inventário e atualização

- Atualizar a lista local e comparar os aplicativos conhecidos com a lista de ações. O resumo completo está na descrição/dica do contador.
- Consultar atualizações. Os logs gravam contagens por motivo, sem nomes de aplicativos.
- Para investigar diferença com WinGet, comparar capturas feitas na mesma máquina e aproximadamente no mesmo horário. Separar canais Preview/ESR, arquitetura e instalações por usuário/computador. Revisar dados pessoais antes de compartilhar capturas.
- Escolher apenas um aplicativo de teste para atualização. Conferir versão e instalação realmente atualizadas. Uma identidade alterada pelo instalador pode produzir resultado indeterminado, que não deve aparecer como sucesso confirmado.

## 3. Resíduos

- Começar pela análise somente leitura. Conferir se aplicativos ativos e perfis de fabricantes/jogos continuam preservados.
- Registrar os casos ambíguos e seus motivos na interface. Não tratar falta de correspondência como autorização para apagar.
- Para testar remoção, usar aplicativo dispensável instalado para este fim. Confirmar que configurações pessoais e dados compartilhados permanecem preservados.
- Se ocorrer falha de leitura do Registro, a confirmação de ausência/limpeza deve ser interrompida.

## 4. Drivers

- Abrir DRIVERS e clicar em Consultar drivers; a abertura do programa não inicia esta consulta.
- Comparar alguns dispositivos com o Gerenciador de Dispositivos: fornecedor, versão e INF quando disponível.
- Verificar se valores desconhecidos aparecem como indeterminados. Dispositivo não presente não significa pacote inútil.
- A tabela mostra associações WMI de dispositivos e drivers, não o inventário completo do Driver Store nem histórico de trocas.
- A assinatura apresentada é a informação fornecida pelo Windows, não validação nova de um pacote baixado.
- Não há instalação, remoção ou reversão de drivers nesta versão.

## 5. Desempenho e interface

- Registrar tempo aproximado até a lista responder, CPU e RAM pelo Gerenciador de Tarefas após estabilizar, e comportamento na bandeja.
- Conferir textos, botão Consultar drivers, tabela/rolagem e navegação na resolução e escala DPI reais.

## Evidências que esses testes fornecerão

- Cobertura real do inventário e divergências de atualização (I7, U6, U8).
- Classificação dos vestígios específicos e preservação após desinstalação (R6, R7).
- Desempenho, AVG e acabamento nos equipamentos reais (P5, P6, V5).
- Confiança na coleta de dispositivos/INF antes de ampliar histórico e selecionar fontes de drivers (D2, D4).

A gestão completa de drivers ainda exige desenvolvimento e testes próprios. Este roteiro não a considera implementada, nem transforma a aprovação deste lote em conclusão de todo o roadmap.
