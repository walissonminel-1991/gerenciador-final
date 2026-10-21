# PC fraco 21/10/2026 — v5.13.11.21 TESTE (leitura MSIX restaurada)

Relatório técnico **agregado** de imagens e de registro de diagnóstico cedidos
voluntariamente, sem publicar o arquivo bruto, perfis, caminhos pessoais,
nomes privados de aplicativos nem identificadores de dispositivos.

## Teste realizado

- Primeira consulta: `MSIX.LEITURA status=ok enumerados=123 aplicativos=19 adicionados=19 filtrados=104 falhas=0 repeticao=False`.
- Repetição manual por **Atualizar lista**: exatamente os mesmos indicadores.
- Inventário agregado: **74** registros, dos quais **19 MSIX** e
  **54 desinstaláveis**. A contagem representa categorias diferentes e
  não deve ser somada como total novo.
- A falha anterior `0x80040154` **não se reproduziu** na sessão. Não há
  prova de que a nova tentativa automática foi usada ou de que o erro não
  voltará; ambas as leituras concluíram na primeira tentativa.
- Consultas de driver: **108** associações, **50** INFs, **3 sem INF válido**,
  alterações no histórico: 0.
- Atualizações: **2** candidatos, nenhuma instalação realizada.
- Investigações de resíduos: **11** candidatos de descoberta geral,
  **12** origens consultadas, **2.000** entradas examinadas,
  **2** fontes inacessíveis, **parcial=True**. Nenhum resíduo removido.
- Diagnóstico completo final: 74 instalados, 0 desinstaladores ausentes,
  106 dispositivos consultados, parcial=False para essa etapa independente.
- Encerramento normal.

## Tempos de execução registrados

- Inicialização: **719,94 ms**.
- Inventário inicial: **1.389,13 ms**; pós-atualização manual: **941,11 ms**.
- Atualizações iniciais: **3.204,70 ms**; após atualização: **1.519,60 ms**.
- Descoberta de resíduos: **886,48 ms**, depois **222,85 ms**.
- Filtro de resíduos: **3,2–6,4 ms** na maioria das interações.
- Pico de RAM do processo durante sessão: **144.580.608 bytes (~137,9 MiB)**.
- Estes números são de uma única sessão e não um benchmark controlado.

## Problema de transparência identificado

A imagem mostra o usuário pesquisando por `MSIX` na aba Aplicativos, com
filtro **Possíveis** ativo, sem qualquer linha. Os 19 itens MSIX estavam
corretamente no inventário, mas eram `InventoryOnly` e não tinham ação
permitida, portanto **não apareciam em nenhum filtro padrão de ações**.
O campo de busca só pesquisa os itens já visíveis: o retorno vazio não
significa falha da leitura MSIX.

O mesmo termo na aba Drivers não corresponde a hardware ou INF; e o
explorador de resíduos não é um inventário de pacotes instalados.

## Melhoria v5.13.11.22

Adicionar o filtro explícito **Inventário** na aba Aplicativos. Essa visão
consulta todas as entradas já armazenadas no inventário local, inclusive
Store/MSIX, e permite buscar por `MSIX` usando o campo da origem do
inventário. Entradas sem ação autorizada exibem **Somente leitura** em
vez de botão clicável; a tela **Todos** permanece focada em ações.

Não alterar as políticas de autorização: sem permissão para atualizar,
desinstalar ou executar comandos em itens inventariados. Testes de
regressão verificam o filtro, as ações proibidas e encaixe de quatro
filtros na janela em várias resoluções. Continuar trabalho separado
para distinguir resíduos genuínos de dados ativos, sem exclusão.
