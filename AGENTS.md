# AGENTS.md — Protocolo obrigatório de desenvolvimento do GF

**Aplicação transversal, decisão do proprietário de 25/11/2026.** Válido para toda frente do Gerenciador Final: ChatGPT (qualquer chat ou nível de raciocínio), WORK, agentes de código, desenvolvimento local, revisão, integração, testes e documentação. Não é uma autorização para alterar áreas protegidas ou executar operações reais. Durante a recuperação, suspender novas funcionalidades e mudanças especulativas.

## Antes de trabalhar
- Ler este arquivo, [autoridade do produto](docs/GF-AUTORIDADE-PRODUTO-V269.md), [matriz vigente](docs/GF-MATRIZ-RASTREABILIDADE-RECUPERACAO.md) e os contratos relevantes; consultar o HEAD real, candidatas em HOLD, CI e frentes concorrentes. Se a branch de origem ainda não contém este protocolo, consultar o HEAD da Draft #113 em vez de adotar uma metodologia antiga.
- Não reiniciar auditorias já comprovadas no mesmo SHA, nem recuperar decisões descartadas. Última decisão explícita do proprietário prevalece; resolver divergências com rastreabilidade, não por preferência do agente.
- Delimitar um objetivo funcional completo e seus impactos. Uma única integração #113; frentes especializadas não produzem versões concorrentes nem sobrescrevem trabalho alheio.

## Ciclo de trabalho extenso e consolidado
1. Investigar causa, contrato e caminho de produção; conferir a implementação ativa e as substituídas.
2. Preparar alteração causal e testes positivos/negativos, preservando segurança, identidade e autorização. Reutilizar infraestrutura compartilhada. Não apagar código por nome, nem reimplementar o que já existe.
3. Conferir compatibilidade, HEAD, ancestralidade e condições de publicação. CAS/fast-forward protegido; base falha admite apenas recuperação causal isolada. Candidatas em HOLD permanecem preservadas.
4. Executar validações, consultar CI, examinar logs e artefatos. Enquanto houver CI em curso, realizar tarefas independentes úteis (comparações por método, rastreabilidade, investigação de desempenho), sem alterações concorrentes especulativas. Voltar a consultar resultados dentro da mesma execução. Se houver falha, investigar, corrigir causalmente, revalidar e continuar enquanto houver capacidade e autorização.
5. Homologar o fluxo como parte do produto único, não como componente isolado: comportamento, estado, integração, segurança, acessibilidade, desempenho, temas e viewport quando aplicáveis. Diferenciar sintético de físico. WORK é responsável pela homologação física nos PCs autorizados.
6. Atualizar a matriz com evidências no SHA exato. Relatar resultados consolidados, bloqueios reais e próximo objetivo, não apenas tarefas executadas.

## Regra de continuidade
- Não encerrar porque um commit foi criado, uma etapa de CI passou, uma consulta terminou ou uma candidata foi preparada. Não exigir novo clique ou comando para cada microetapa.
- Dar atualizações breves quando úteis, sem transformar cada atualização em pausa. Botões de continuidade deixam de ser padrão: só quando necessários para retomar um bloqueio real, uma decisão indispensável ou um limite efetivo de execução; nunca substituir trabalho que ainda pode ser realizado na interação atual.
- Pausar apenas por conclusão do ciclo, dependência externa ainda não resolvida após trabalho útil disponível, limite efetivo de ferramenta/execução, conflito de autoridade, risco de sobrescrever outra frente ou necessidade de autorização. Registrar o motivo concreto e estado exato para retomada.
- Não prometer espera ou trabalho em segundo plano quando a ferramenta não permite. Sem automações agendadas. Consultas de CI em execução são permitidas na interação ativa, sem prometer duração.
- Ao retomar em outro chat, verificar HEAD/CI e ler esta autoridade antes de agir. Não pressupor que a memória de conversa é suficiente.

## Aceite e limites permanentes
A métrica oficial é funções reais funcionando. Código presente, conectado, validado sinteticamente, validado fisicamente e concluído são estados distintos. CI verde, imagens estáticas, commits e contagem de testes não encerram uma função. A evidência deve mostrar que a versão aprovada é a efetivamente usada e que a substituída não segue ativa indevidamente.
Preservar HTML V2.6.9, motores funcionais, Monitor, segurança, acessibilidade, privacidade, orçamento zero, versão portátil e código próprio. Não tocar main, G5, GF_BETA, Secrets, workflows ou PRs de origem; sem merge, release pública, force push destrutivo, serviços/infraestrutura novos ou manutenção real sem autorização. Respeitar decisões posteriores explícitas do proprietário.
