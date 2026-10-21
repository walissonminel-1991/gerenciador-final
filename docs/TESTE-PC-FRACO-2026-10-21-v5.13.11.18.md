# PC fraco — observações reais da v5.13.11.18 (21/10/2026)

Relatório **derivado de capturas e diagnóstico local fornecidos voluntariamente**.
Somente dados agregados: não republicar o log bruto, nomes de conta, identificadores
de dispositivos, caminhos pessoais nem `drivers-v1.json` neste repositório.

## Ambiente e observação
- Windows x64; resolução física mostrada: **1440×900**, escala 100%.
- Aplicativo abriu e encerrou normalmente; nenhuma remoção de resíduos nem mudança de driver.
- Painel de privacidade abriu e a coleta local foi ativada pelo usuário; envio remoto desativado.
- Pesquisa de programas, filtros de drivers e filtros de resíduos funcionaram nas capturas.
- Bandeja: ocultação e restauração registradas; encerramento normal confirmado.

## Métricas registradas pelo GF (uma sessão; não são benchmarks comparativos)

| Etapa | Tempo informado | Observação |
| --- | ---: | --- |
| Inicialização até janela | 1320,69 ms | Medida local, antes do consentimento |
| Primeiro inventário | 2013,67 ms | 74 itens no inventário |
| Inventário posterior | 1155,57 ms | Condições/cache podem variar |
| Primeira busca de atualizações | 6311,78 ms | RuckZuck 631 entradas |
| Busca posterior de atualizações | 1344,03 ms | Condições/cache diferentes |
| Descoberta geral de resíduos | 420,93 ms | 2000 entradas examinadas |
| Classificação de resíduos | 29,29 ms | Parte da descoberta, não somar tempos |
| Primeiro filtro de resíduos | 7,00 ms | 17 grupos na revisão exibida |
| Primeiro exame sob demanda | 31,95 ms | Apenas metadados |
| Pico de RAM do processo na sessão | 138,56 MiB | Não equivale à RAM total do Windows |

## Inventário e atualizações
- 74 entradas de inventário, 54 desinstaláveis, 20 ocultas sem ação.
- Três **possíveis** atualizações em catálogo; nenhuma instalação validada por este teste.
- Uma atualização de Windows detectada; nenhuma atualização instalada.
- 108 associações de drivers, 50 arquivos INF, três associações sem INF; histórico
  com uma mudança adicionada relativa ao estado local prévio, não uma contagem
  de drivers novos ou removíveis. O JSON contém 108 chaves opacas, sem nomes/IDs brutos.

## Resíduos: problemas observados
- Consulta geral terminou **parcial**: 12 origens, 2000 entradas examinadas, 16
  candidatos de descoberta, sete instalações a conferir e duas fontes inacessíveis.
  A revisão combinada exibiu 17 grupos e 57 locais; essas grandezas têm escopos
  diferentes e não devem ser somadas.
- **Falha de identidade:** subpastas genéricas com nome `Config` de dois
  aplicativos diferentes eram agrupadas como um suposto produto `Config`.
  Também apareceram grupos `Logs`, `Updates`, `Scripts` e `Providers`.
- **Falha de mensagem:** inspeção vazia exibia "Nenhum grupo nesta seleção",
  embora a seleção de produto continuasse presente.
- Permaneceram corretamente preservados dados de fabricantes/jogos, instalações
  não confirmadas, registros compartilhados e perfis pessoais.
- A parcialidade não deve desaparecer artificialmente: limites de profundidade,
  fontes não acessíveis e resultados não examinados continuam informados.

## Correções preparadas na v5.13.11.19 TESTE
1. Nomes de subpastas genéricas não se tornam identidades independentes;
   evidências passam ao grupo do pai. Sem afirmar desinstalação ou exclusividade.
2. Testes sintéticos e de enumeração de diretórios/Registro com subpastas
   `Config` homônimas de fabricantes diferentes.
3. Texto distinto para lista de grupos vazia e inspeção de nível sem itens.
4. Exclusão real de resíduos continua indisponível.

## Próximo teste controlado
Comparar na v5.13.11.19 o número de grupos/locais, ausência dos falsos
grupos genéricos, razões de parcialidade, tempos/RAM, suspensão/restauração
da bandeja e ausência de alerta do antivírus. Executar apenas operações de
leitura; não reinstalar, remover, reparar nem alterar drivers durante a bateria.

**Limite da evidência:** não há dados de eficiência de limpeza/desinstalação,
nem confirmação de cobertura integral do computador. A fonte é uma única
sessão de um PC real; screenshots não medem fluidez nem períodos de CPU.
