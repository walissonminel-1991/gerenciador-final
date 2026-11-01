# GERENCIADOR FINAL — P5 — checkpoint: tempo da primeira carga de inventário

**Data:** 31/10/2026. Base imutável PR #65 `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Status: melhora de observabilidade em CI, **P5 não homologado**. Roadmap **27/42**.

## Lacuna comprovada na base

`Program.Main` chama `LocalPerformanceMetrics.BeginStartup()`, mas `MainForm.Shown` executa `CompleteStartup()` **antes** de carregar, tratar e desenhar a lista inicial de programas. Portanto `DESEMPENHO.LOCAL etapa=Startup` mede a apresentação da janela, não o tempo até a lista ficar pronta. É incorreto compará-lo como latência completa da primeira consulta. `LoadInstalledAsync` já usa `Task.Run` para Registro, MSIX, jogos, ecossistemas e verificação de desinstaladores; nenhuma alteração na arquitetura de threads é necessária por essa lacuna.

## Correção isolada

Novo estágio `InitialInventoryLoad` no mecanismo existente de métricas, com início após a decisão de consentimento e antes da primeira chamada a `ExecuteAsync("Carregando programas instalados...", LoadInstalledAsync)`, finalizando na saída da tentativa. A emissão é agregada e mantém a métrica histórica `Startup` inalterada. `ExecuteAsync` captura falhas, portanto a nova duração mede **tentativa**, não equivale a sucesso nem a completude do inventário; conferir junto a `REGISTRO_PROGRAMAS.LIDO`, `RENDER_FIM` e `parcial`. Inclui CPU e memória do próprio processo já previstas por `LocalPerformanceMetrics`; sem nomes, arquivos ou dados pessoais. Nenhuma leitura nova de disco/Registro/MSIX/rede.

Teste Windows em `TestesLayout/Program.cs` exige exatamente um `PerformanceSample`, estágio correto, métricas não negativas e texto agregado. Não presume redução de consumo e não transforma a simulação em medição física.

## Para concluir P5 futuramente

Amostragem comparável nos PCs FORTE/FRACO de **mesma build portátil identificada por SHA-256**, registrando tempo de janela visível, primeira lista, CPU/RAM após 30/60/120 s de repouso, resolução/DPI, estado de inventário completo e condições da sessão. Registrar consentimento/modal em separado. Evidência E1/E2 anterior: `Startup` interno 376,16/667,98 ms, CPU 359,38/593,75 ms, **sem** comprovação de tempo até a lista inicial. Não inferir vazamento de amostras isoladas. Não coletar caminhos ou identificadores pessoais.

Preservar PRs #65–#69, autoatualizador experimental, `main`, Releases e PCs pessoais. Não contornar o gate de política de workflows preexistente.
