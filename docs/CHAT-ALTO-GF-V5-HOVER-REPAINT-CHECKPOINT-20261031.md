# GERENCIADOR FINAL — V5 — hover da lista: invalidação local de pintura

**Base técnica imutável:** PR #65 `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. **Data:** 31/10/2026. **Estado:** melhoria restrita de UI; V5 continua aberto até homologação física vinculada a artefato, DPI e resolução. Roadmap **27/42**.

## Contraexemplo reproduzível

`DarkProgramList.OnMouseMove` usava `Invalidate()` sem retângulo cada vez que o cursor entrava, saía ou alternava ações de uma entrada. `OnMouseLeave` repetia a invalidação total. Em uma lista de dois aplicativos de teste, alternar entre os botões de desinstalação sinalizava para redesenhar **toda a viewport**, apesar de apenas a linha anterior e a atual mudarem os estilos de hover. Esse custo repete o trabalho de renderização de nomes, ícones, estados e botões das demais linhas visíveis. Não se deduz ganho percentual de CPU sem medição.

## Menor correção

`InvalidateActionRow(ProgramEntry?)` encontra a entrada por referência, calcula sua posição com o offset de rolagem e chama `Invalidate(Rectangle)` somente quando visível. Ao trocar hover, invalidar a linha anterior e a nova (uma vez só se forem a mesma); ao sair da lista, apenas a anterior. **Sem alteração** em `HitAction`, verificação de permissões, botões, clique, temporizador do feedback de 160 ms, progresso, seleção, scroll, descarte ou acesso aos dados. `OnPaint` permanece virtualizado.

## Fixture descartável Windows

`TestesLayout/Program.cs`: dois aplicativos fictícios `CanUninstall=true` em viewports de 600 e 1000 px de largura e 260 px de altura, linhas de 66 px. Chamadas ao método original de movimento e saída via reflexão apenas no teste. O evento `Invalidated` deve ocorrer para hover, mudança entre linhas e saída, e **nenhum** retângulo pode superar a altura de uma linha; a troca entre duas linhas exige ao menos duas invalidações localizadas. A implementação anterior falharia, pois invalida todo o controle com altura 260. Nenhum aplicativo real ou ação de desinstalação é executado.

## Matriz V5 pendente

Os testes existentes verificam **22 cenários de layout** e **132 combinações** resolução/aba sintéticas, além de bandeja, foco, rolagem e mensagens. Essa cobertura não substitui confirmar legibilidade, scroll, DPI real e fluidez nos PCs FORTE 2560×1440 (DPI histórico 120) e FRACO 1440×900 em pacote de hash identificado; T11 iniciar com Windows ainda não foi executado. Não fabricar capturas físicas nem solicitar bateria repetitiva sem alteração funcional concreta.

**Preservação:** PRs #65–#70, autoatualizador experimental, `main`, Releases e PCs pessoais. Gate de inventário de workflows anterior à branch não deve ser contornado. V5 continua **aberto**, roadmap 27/42.
