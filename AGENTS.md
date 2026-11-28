# AGENTS.md — Protocolo obrigatório de desenvolvimento do GF

**Aplicação transversal, decisão do proprietário de 25/11/2026.** Válido para toda frente do Gerenciador Final: ChatGPT (qualquer chat ou nível de raciocínio), WORK, agentes de código, desenvolvimento local, revisão, integração, testes e documentação. Não é uma autorização para alterar áreas protegidas ou executar operações reais. Durante a recuperação, suspender novas funcionalidades e mudanças especulativas.

## Antes de trabalhar
- Ler este arquivo, [autoridade do produto](docs/GF-AUTORIDADE-PRODUTO-V269.md), [matriz vigente](docs/GF-MATRIZ-RASTREABILIDADE-RECUPERACAO.md) e os contratos relevantes; consultar o HEAD real, candidatas em HOLD, CI e frentes concorrentes. Se a branch de origem ainda não contém este protocolo, consultar o HEAD da Draft #113 em vez de adotar uma metodologia antiga.
- Não reiniciar auditorias já comprovadas no mesmo SHA, nem recuperar decisões descartadas. Última decisão explícita do proprietário prevalece; resolver divergências com rastreabilidade, não por preferência do agente.
- Delimitar um objetivo funcional completo e seus impactos. Uma única integração #113; frentes especializadas não produzem versões concorrentes nem sobrescrevem trabalho alheio.

## Avaliação de ferramentas antes de soluções próprias

Antes de criar uma solução própria complexa, pesquisar ferramentas, bibliotecas, SDKs, analisadores, depuradores, plugins ou servidores MCP maduros que possam resolver o problema com maior eficiência, segurança e custo zero. Avaliar somente opções compatíveis com o produto, a licença, a manutenção, a privacidade, a reversibilidade e as áreas protegidas.

Ferramentas de desenvolvimento, teste e diagnóstico permanecem fora do EXE/ZIP e não alteram o produto por si só. A adoção exige escopo explícito e prova de valor; não instalar silenciosamente, não enviar código/logs privados a serviços externos e não criar nova infraestrutura.

Quando uma ferramenta ou melhoria metodológica for comprovadamente benéfica, consolidar a regra ou decisão nesta fonte de autoridade e na seção vigente correspondente, para que nenhuma evolução fique restrita à memória de uma IA, a uma conversa ou a comentários. Não criar documento, relatório, gate ou protocolo paralelo quando a regra puder ser incorporada ao ciclo já existente.


A aplicação corrente desta avaliação é:
- analisadores Roslyn do SDK, inicialmente como sugestões não bloqueantes, para descobrir riscos sem converter diagnóstico em gate prematuro;
- `dotnet-trace` e `dotnet-counters` versionados no manifesto local, anexados somente a um PID explicitamente autorizado e com saídas locais;
- PerfView, Windows Performance Recorder/Analyzer e ferramentas equivalentes somente para investigação externa de desempenho, sem incorporá-las ao produto;
- Git Bisect para localizar regressões reproduzíveis, preservando a branch, o SHA e as áreas protegidas;
- Python com OpenCV headless para comparação objetiva de capturas, exata por padrão e com tolerância/região somente quando declaradas;
- uma aplicação WinForms sintética isolada para validar a cadeia de ferramentas antes de qualquer uso no GF; a prova sintética não substitui homologação física;
- automação nativa de acessibilidade/UI Automation como primeira opção para interação real; FlaUI permanece apenas uma alternativa a avaliar se a UI Automation nativa se mostrar insuficiente.

Plugins, extensões de IDE e servidores MCP podem acelerar leitura, diagnóstico e validação, mas são auxiliares de desenvolvimento: não entram no EXE/ZIP, não recebem código ou logs privados sem autorização, não criam infraestrutura e não substituem os contratos, o HEAD, os testes nem a homologação do GF.

## Ciclo de trabalho extenso e consolidado
1. Investigar causa, contrato e caminho de produção; conferir a implementação ativa e as substituídas.
2. Preparar alteração causal e testes positivos/negativos, preservando segurança, identidade e autorização. Reutilizar infraestrutura compartilhada. Não apagar código por nome, nem reimplementar o que já existe.
3. Conferir compatibilidade, HEAD, ancestralidade e condições de publicação. CAS/fast-forward protegido; base falha admite apenas recuperação causal isolada. Candidatas em HOLD permanecem preservadas.
4. Executar validações, consultar CI, examinar logs e artefatos. Enquanto houver CI em curso, realizar tarefas independentes úteis (comparações por método, rastreabilidade, investigação de desempenho), sem alterações concorrentes especulativas. Voltar a consultar resultados dentro da mesma execução. Se houver falha, investigar, corrigir causalmente, revalidar e continuar enquanto houver capacidade e autorização.
5. Homologar o fluxo como parte do produto único, não como componente isolado: comportamento, estado, integração, segurança, acessibilidade, desempenho, temas e viewport quando aplicáveis. Diferenciar sintético de físico. WORK é responsável pela homologação física nos PCs autorizados.
6. Atualizar a matriz com evidências no SHA exato. Relatar resultados consolidados, bloqueios reais e próximo objetivo, não apenas tarefas executadas.

## Incrementos pequenos dentro de ciclos longos
- Cada ciclo pode abranger investigação, implementação, CI, correção e homologação sem pedir continuidade a cada etapa. Dividir a publicação em incrementos causais pequenos, cada um associado a um requisito, caminho ativo, testes e evidência próprios; evitar um lote grande de mudanças não relacionadas que dificulte localizar regressões.
- Antes de começar um incremento, registrar objetivo funcional, escopo, dependências, critérios positivos e negativos, efeito esperado na interface e no motor, e condição para encerrá-lo. Se a base falhar, manter mudanças independentes em HOLD e tratar apenas a causa comprovada da falha.
- Reavaliar somente as validações afetadas por mudanças no código, contratos, dependências ou ambiente. Preservar evidências anteriores com seu SHA e suas condições, sem promovê-las automaticamente para o HEAD novo.

## Evidências, bloqueios e retomada
- Cada linha relevante da matriz deve apontar requisito e decisão vigente, origem/versão do contrato, caminho de produção, estado da função, SHA do código testado, execução ou artefato, ambiente, resultado positivo/negativo e pendência física. Data de edição do documento não substitui SHA de execução.
- Indicar explicitamente quando uma evidência vale só para um SHA antigo ou quando mudanças em dependências exigem nova verificação. Atualizar o registro de estado corrente sem reescrever resultados históricos nem tratar documentação como homologação.
- Para cada bloqueio, registrar ID, falha reproduzível, impacto, causa demonstrada ou hipótese identificada como tal, responsável/área, candidata preservada, próximo teste seguro e condição objetiva de desbloqueio. Se a causa não for conhecida, a próxima ação é investigá-la; não inventar correção ou prazo.
- Acompanhar tempo até uma função comprovada, recorrência de falhas e tempo de recuperação apenas como indicadores para ajustar o processo. Eles não substituem a métrica oficial de funções reais funcionando nem justificam baixar os critérios de aceite.

## Prova de interação real
- Para fluxos WinForms, quando houver infraestrutura apropriada, testar controles e eventos na janela real (inclusive automação de interface/acessibilidade), sequências de navegação e teclado, confirmação/cancelamento, troca de tema e retenção de estado. Registrar o que foi apenas sintético e o que foi observado fisicamente; capturas estáticas e chamadas diretas de método não provam interação.
- Proteger dados e máquinas: automação de interface não autoriza instalação, remoção, reparo, elevação ou manutenção real. A homologação física permanece exclusiva do WORK nos computadores autorizados.

## Contenção de falhas graves
- Se uma mudança puder causar ação não autorizada, perda de dados, violação de identidade/consentimento, perda da recuperação ou travamento de fluxo essencial, interromper sua publicação e registrar o SHA, a reprodução, o alcance e o último estado conhecido como íntegro.
- Investigar em ambiente descartável ou somente leitura; preservar candidatas e evidências. Corrigir a causa sem ocultar o teste, restaurar os controles negativos e verificar o fluxo afetado de ponta a ponta. Reverter uma mudança somente por caminho protegido que preserve o histórico e o trabalho concorrente.
- Não declarar o produto infalível nem simular uma recuperação real. Provas de restauração e manutenção física nos PCs autorizados dependem de autorização operacional e registro pelo WORK.

## Regra de continuidade
- Não encerrar porque um commit foi criado, uma etapa de CI passou, uma consulta terminou ou uma candidata foi preparada. Não exigir novo clique ou comando para cada microetapa.
- Dar atualizações breves quando úteis, sem transformar cada atualização em pausa. Botões de continuidade deixam de ser padrão: só quando necessários para retomar um bloqueio real, uma decisão indispensável ou um limite efetivo de execução; nunca substituir trabalho que ainda pode ser realizado na interação atual.
- Pausar apenas por conclusão do ciclo, dependência externa ainda não resolvida após trabalho útil disponível, limite efetivo de ferramenta/execução, conflito de autoridade, risco de sobrescrever outra frente ou necessidade de autorização. Registrar o motivo concreto e estado exato para retomada.
- Não prometer espera ou trabalho em segundo plano quando a ferramenta não permite. Sem automações agendadas. Consultas de CI em execução são permitidas na interação ativa, sem prometer duração.
- Ao retomar em outro chat, verificar HEAD/CI e ler esta autoridade antes de agir. Não pressupor que a memória de conversa é suficiente.

## Aceite e limites permanentes
A métrica oficial é funções reais funcionando. Código presente, conectado, validado sinteticamente, validado fisicamente e concluído são estados distintos. CI verde, imagens estáticas, commits e contagem de testes não encerram uma função. A evidência deve mostrar que a versão aprovada é a efetivamente usada e que a substituída não segue ativa indevidamente.
Preservar HTML V2.6.9, motores funcionais, Monitor, segurança, acessibilidade, privacidade, orçamento zero, versão portátil e código próprio. Não tocar main, G5, GF_BETA, Secrets, workflows ou PRs de origem; sem merge, release pública, force push destrutivo, serviços/infraestrutura novos ou manutenção real sem autorização. Respeitar decisões posteriores explícitas do proprietário.

## Entrega privada de versões de teste (decisão do proprietário de 26/11/2026)
- O padrão obrigatório é compilar por GitHub Actions no repositório privado `walissonminel-1991/gf-beta-entregas`, no SHA de código com gates integrais aprovados. Substitui a instrução anterior de compilação pessoal local. PC FORTE e PC FRACO destinam-se à homologação física, não à compilação ordinária.
- Reutilizar o controlador privado comprovado, vincular fonte, árvore, pai, base e CIs exatas; verificar Windows x64, perfil BETA local sem transporte, manifesto e integridade, ZIP e SHA-256, e verificar independentemente os bytes armazenados no Linux. Só disponibilizar link direto após sucesso integral; retenção privada de sete dias; mesmo ZIP/hash nos dois PCs, AVG ativo. Sem release pública, credenciais novas, automação agendada ou mudança nos workflows do repositório público. A atualização estritamente necessária dos identificadores do controlador privado para uma entrega autorizada não altera o produto.

## Prioridade transversal REC-BASE-01 (decisão do proprietário de 26/11/2026)
- Antes de retomar as funções do GF, finalizar a base de interface e fluidez, seguida de otimização. A decisão explícita está registrada na [PR #113](https://github.com/walissonminel-1991/gerenciador-final/pull/113#issuecomment-7193812714). Consultar também decisões posteriores registradas na PR enquanto aguardam consolidação documental protegida.
- Fase 1: uma única interface WinForms coerente com a autoridade visual; recuperar todos os menus, componentes compartilhados, navegação, rolagem, dimensionamento, pintura, temas, estados e interação contínua. Testar em janela real, com dados representativos, três temas, resoluções autorizadas, DPI e janela reduzida. Não substituir o comportamento por screenshots.
- Fase 2: após aprovação física da base visual/comportamental, otimizar com medições de latência, pintura/layout, CPU/RAM e sessões prolongadas nos dois PCs, sem reduzir segurança, cobertura ou cadência por conveniência. Não inventar limites finais.
- Fase 3: somente após aceite da base e otimização pelo proprietário, revisar e corrigir funções existentes. Preservar motores e testes já conectados; intervenção funcional antes disso apenas quando indispensável à segurança, integridade ou desbloqueio essencial da base, com causa e prova documentadas. Nenhuma nova função autorizada.
- Entregar candidatas pelos gates existentes e canal privado. A métrica oficial permanece funções reais funcionando; aprovação da base é um pré-requisito, não contagem de funções concluídas. Preservar a cadeia visual até 1aedf393 e a candidata funcional f8951941 em HOLD independente, sem publicação automática desta última.

- Estado corrente e evidências executadas: consultar a seção REC-BASE-01 da matriz, incluindo as reconciliações após REC-FIS-OPAQUE e as referências de otimização. Não promover resultados sintéticos a aceite físico; manter o SHA de código e o SHA documental distintos e exigir gate próprio de cada publicação.
