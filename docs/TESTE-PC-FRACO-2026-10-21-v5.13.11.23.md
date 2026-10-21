# Resultado agregado — PC fraco v5.13.11.23 TESTE (21/10/2026)

Este relatório resume capturas e diagnóstico cedidos voluntariamente. Não
publica logs originais, caminhos pessoais, nomes de perfis, IDs de dispositivos
ou listas de aplicativos.

## Inventário e atualização
- Inicialização 696,54 ms; inventário 1.601,07 ms.
- 74 entradas, incluindo 19 MSIX; 123 pacotes MSIX enumerados,
  0 falhas, nenhuma segunda tentativa necessária.
- 54 desinstaláveis e 20 sem ação. Filtro Inventário mostra MSIX
  em somente leitura.
- 2 possíveis atualizações do catálogo, sem instalar nenhuma;
  1 atualização do Windows encontrada, sem instalar.
- 108 associações de drivers, 50 INFs, 3 sem INF;
  sem mudanças históricas identificadas.

## Descoberta histórica
- 12 origens, 2.000 entradas, 11 grupos candidatos gerais,
  3 instalações a conferir; análise parcial.
- Motivos EXATOS: `ReadFailure+DepthLimit+LinkSkipped`.
  Não houve flag `EntryLimit` nesta consulta. 2.000 foi quantidade
  observada, não prova de limite global atingido.
- Após reunir fontes, explorador mostra 12 grupos e 41 locais;
  o resumo combinado contabiliza 2.110 entradas entre análises.
  As duas contagens têm escopos diferentes.
- A .23 nomeava fonte inteira indisponível por falha de leitura pontual;
  revisão .24 diferencia fontes lidas parcialmente de indisponíveis.
- Nenhuma exclusão, alteração de drivers ou instalação executada.
- Descoberta 579,03 ms; classificação 25,01 ms; encerramento normal.

## Limites
Uma sessão real não é benchmark controlado. AVG não foi
certificado por esta consulta. Os resultados não provam que
qualquer vestígio está seguro para exclusão.
