# MISSÃO WORK 6 — auditoria, correções e regressões

## Base e critérios

Base remota `1bd1ff89b5b7c47d3c41a60dd4c347a0d9da00dc`, PR #40 Draft, branch experimental; main `9ac3dc167b9e67ac50628749d5ad33c563c09376` preservada. Roadmap: **27/42 (64,3%)**, sem checkpoint novo. Ordem: atualização → desinstalação → resíduos. Nenhuma instalação, desinstalação ou exclusão de dados reais autorizada; fixtures exclusivamente temporárias. R4 permanece suspenso.

## Inventário de risco inicial

| Risco | Evidência no código base | Reprodução necessária |
|---|---|---|
| P1: proteção somente inventário contornada no autorizador | `Resolve` não verifica `InventoryOnly` nem nome protegido; receita GameInput ainda acessível | OBS somente inventário e GameInput sem flag devem retornar null |
| P2: fabricante parcial autoriza OBS | `Contains("OBS") / Contains("Project")` | fabricante adversarial contendo esses fragmentos |
| P2: confirmação falsa com receita adulterada | confirmação compara versão recebida sem validar receita inteira | reduzir alvo para versão já instalada; deve recusar |
| P2: consulta parcial apaga candidatos anteriores | substituição integral da lista e invalidação em timeout | 15 fontes → falhas parciais/totais; preservar evidência marcada como antiga, sem ação automática |
| P1: comando registrado não é autorização de shell | auditoria aceita cmd/rundll32 por nome | parser compartilhado, comandos hostis e launch count zero |
| P2: lote pode repetir seleção e executar comando alterado | array sem deduplicação nem revalidação de identidade antes de cada item | fake de inventário/lançador e contagem exata |
| P2: prévia requer vínculo de identidade e cobertura | capturas temporárias reais e comparação por handle/digest | alteração, substituição e cobertura parcial invalidam; nunca exclusão |

Severidade expressa risco no ponto de execução e defesa em profundidade; não afirma exploração externa demonstrada. Testes antigos de mensagens/constantes não comprovam ausência de lançamento. Simulações isoladas anteriores (7+6+13) não são fluxo integrado do sistema operacional. Suites Windows anteriores: atualizador 104, resíduos/desinstalação 379. Resultados novos e evidências serão registrados após execução.
