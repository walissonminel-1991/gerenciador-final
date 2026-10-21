# PC fraco — v5.13.11.23 TESTE: diagnóstico de parcialidade (plano de validação)

## O que foi corrigido no código
- Resumo da investigação de resíduos passa a exibir **motivos específicos**
  de cobertura parcial (quantidade, tempo, permissão/leitura, profundidade,
  link ignorado, fonte indisponível etc.) em texto fixo, sem caminhos privados.
- A linha local `RESIDUOS_GERAIS.CONSULTA_FIM` ganhou `motivos=` com flags
  agregadas (separador `+`) para distinguir o motivo efetivo do teste.
- `ResidualScanBudget.Unavailable` não atribui a uma fonte anterior
  a indisponibilidade da fonte explicitamente indicada.
- Três testes adicionais de regressão cobrem o erro de atribuição,
  a distinção entre falha de leitura e indisponibilidade e os rótulos.
- Nenhuma rotina de desinstalação, exclusão, atualização, driver ou rede
  foi alterada por esta revisão.

## Interpretação rigorosa do teste anterior (.21)
O log anterior registrou **2.000 entradas examinadas** e **2 fontes
inacessíveis** em uma consulta parcial; **2.000 é a quantidade observada,
não necessariamente um limite global configurado**. Há limites específicos
por origem, quantidade por subpasta, tempo e profundidade. Sem os motivos
agregados não é correto concluir qual limite encerrou a investigação.

## Bateria segura no PC fraco (1440×900)
1. Extraia a nova candidata em pasta própria, sem substituir a versão anterior.
   Mantenha o AVG ativo e não crie exceções para contornar alertas.
2. Em Aplicativos > Inventário, pesquise `MSIX`. Conte as linhas e observe
   se os itens continuam rotulados como `somente leitura`; sem ações de
   remoção. Volte ao filtro Todos.
3. Abra Investigação de resíduos em **somente leitura**. Observe a primeira
   linha do resumo, especialmente `ANÁLISE PARCIAL — ...`; alterne fontes,
   classificações, buscas e paginação sem apagar nada.
4. Abra Resumo. Compare motivos e fontes indisponíveis com os resultados
   da tela; anote os motivos, não caminhos pessoais.
5. Feche normalmente; no log local sanitizado, localize
   `RESIDUOS_GERAIS.CONSULTA_FIM` e `motivos=`.
6. Registre tempo de abertura, RAM/CPU, integridade visual e qualquer
   intervenção do AVG. Não desative a proteção.

**Expectativa, não promessa:** o inventário pode repetir 74 apps / 19 MSIX
e drivers 108 se nada mudou no sistema. Limites e permissões podem variar.
O teste do retry MSIX sob erro 0x80040154 real ainda não foi reproduzido.

## Estado de segurança
**Nenhuma exclusão de resíduo ou instalação/desinstalação autorizada nesta
bateria**. PR #40 continua Draft; nenhuma publicação ou merge na main.
Roadmap 26/42 até validação adicional. A implementação não valida o
comportamento no Windows antes de a candidata ser executada no PC fraco.
