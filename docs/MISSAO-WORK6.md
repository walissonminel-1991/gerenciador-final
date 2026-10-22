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

## Fase A — atualização

Reprodução anterior à correção: quatro falhas locais independentes em `--work6-authorization`, com controle OBS legítimo positivo. Commit remoto `3f601a24223e9c474cbf8d07bbbed5114bdab27f`; [regressão Windows vermelha](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39703481779), job `119577926659`: `Falha: WORK6 somente inventário bloqueia autorização no endpoint`. Não é falha de rede/compilação; o teste alcançou o autorizador e encontrou o defeito. Etapas posteriores foram skipped, não aprovadas.

Correções: autorização EXE verifica proteção e fabricante exato; GameInput deixa de ser receita aprovada inclusive no ponto de download/lançamento; confirmação exige receita completa, família original e identidade única. Leitura de pacote recusa hardlinks. Prazo cobre corpo do download, além dos headers. Espera do instalador limitada a 15 minutos, sem matar processo nem repetir; arquivo protegido permanece retido. Gate usado pelo fluxo real revalida inventário completo imediatamente antes de lançar, mede estado terminal e não aceita exit zero sem confirmação.

Histórico consultivo em memória conserva último resultado completo por até 30 minutos monotônicos para o MESMO inventário. Falhas sucessivas não renovam prazo. Consulta parcial/cancelada não apaga candidatos conhecidos nem os apresenta como consulta recente; tooltip declara evidência anterior, requer revisão e bloqueia receita. Novo inventário ou consulta completa substitui o histórico. Nenhum novo acesso a WinGet/PowerShell/administrador.

Validação local: 5 controles de autorização após reproduzir 4 defeitos; 9 controles do gate com lançador falso (zero/uma chamada, concorrência, cancelamento determinístico via TCS, 0/3010/1603, UAC1223, timeout, ausência de versão); 7 cenários de histórico incluídos em 139/139 multifontes; 15/15 políticas. Compilação cruzada sem avisos. Hardlink exige Windows e está na regressão nativa. Nenhuma instalação foi realizada. Aprovação integral Windows será registrada separadamente.
