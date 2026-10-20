# Pipeline de manutenção com IA, centrado no GitHub

**Estado: plano de integração, não operacional.** Este documento não autoriza coleta remota, execução autônoma de código recebido nem publicação automática. Base de código v5.13.11.1: opt-in local na PR #38 e compilação/regressões Windows aprovadas.

## Estado da triagem GitHub-only (20/10/2026)

- **Implementado na PR #40, ainda nao integrado na main:** workflow `.github/workflows/triagem-diagnostico-github.yml`, parser estrito em `scripts/triagem_diagnostico.py` e testes de regressao independentes. Issues publicas novas com titulo do piloto sao verificadas automaticamente, sem permissao de escrita, e geram resumo apenas de categorias e contagens aceitas. Consulte [triagem GitHub](TRIAGEM-DIAGNOSTICO-GITHUB.md).
- **Nao implementados:** envio pelo aplicativo em segundo plano, recepcao privada, comprovacao de origem dos relatos, triagem por agente IA, PRs gerados por IA, exclusao retroativa de Issues e atualizacoes automaticas. Uma Issue publica pode continuar publicamente visivel mesmo se o parser a rejeitar.
- O progresso funcional do roadmap permanece 27/42; CI e triagem integram um trabalho complementar.

## Objetivo

Com consentimento **separado para envio**, converter diagnósticos agregados (não logs) em tarefas verificáveis de manutenção; permitir que uma IA proponha correções em branches, preparar builds de teste e changelog, preservar revisão humana e eliminar dados temporários após processamento ou até sete dias.

```text
GF com consentimento -> [recepção/autenticação] -> validar esquema + rate limit
  -> agregados temporários privados
  -> GitHub Actions (triagem agrupada por versão/categoria)
  -> Issue técnica anonimizada e deduplicada, sem dados de usuários
  -> agente de código (GitHub Agentic Workflows ou Copilot cloud agent, configurado)
  -> PR em branch + testes Windows + análise estática + hashes do build
  -> revisão humana -> candidata de testes -> bateria de PCs/AVG -> Release aprovada
  -> excluir entrada temporária, preservando somente issue/PR/resultado de testes
```

## GitHub não é receptor público anônimo de diagnósticos

O tamanho do arquivo (.txt de poucos KB) não é o obstáculo. Gravar diretamente no repositório, abrir Issues e disparar `repository_dispatch` pela API exigem autenticação. **Jamais embutir token de escrita, credencial pessoal, chave privada de GitHub App ou segredo de API no EXE.**

Duas opções realistas, sem administrar servidor próprio:

1. **GitHub somente, grupo de testadores GitHub:** cada testador tem conta GitHub, faz autenticação individual e autoriza envio limitado. Precisamos projetar cuidadosamente permissões/visibilidade; colaboradores externos não devem ter acesso de escrita ao repositório principal nem a dados privados. Em um repositório público, Issues com diagnósticos são públicas e não servem para guardar logs.
2. **Todos os usuários comuns, sem conta GitHub:** é necessário um receptor HTTPS gerenciado, mínimo, que valide/resuma e encaminhe à automação do GitHub. Pode ser qualquer serviço apropriado; **Cloudflare não é obrigatória**. Isso não exige servidor físico dedicado. Receptor e GitHub precisam de autenticação entre si, guardada somente no serviço confiável, e regras contra spam.

O código de referência Worker + D1 na PR #38 é apenas uma das alternativas; não foi implantado.

## Fluxo de IA no GitHub

- `GitHub Actions`: valida esquema de relatórios, agrupa contagens, roda testes .NET/WinForms Windows, gera pacote de teste e relatórios de build. Ações determinísticas não precisam de IA.
- `GitHub Agentic Workflows` (prévia pública): interpreta Issues agregadas, sugere causa/testes e pode propor PR sob permissões e saídas controladas. Compatível com agentes como GitHub Copilot e OpenAI Codex; **requer configuração, autenticação e pode ter custo**. Precisa compilar o `.md` agentic para `.lock.yml` via `gh aw compile`; não basta copiar um prompt.
- `Copilot cloud agent`: pode receber Issues e preparar PRs para revisão. A função específica **Copilot Automations** de execução contínua é limitada a repositórios privados/internos, logo não presumir que funcione automaticamente em nosso repositório público.
- Os agentes devem trabalhar sobre uma Issue **derivada e saneada**, jamais receber texto arbitrário do usuário como instruções. Tratar até arquivos `.txt` como entrada não confiável; evitar prompt injection, execução de comandos, leitura de segredos e `pull_request_target` sobre código não confiável.
- Gerar PR candidata e **pedir aprovação humana para merge e publicação**. Um agente jamais altera a `main`, instala drivers, desliga antivírus, distribui binário ou publica versão estável sem revisão.

## Esquema de dados e limpeza

- Não recolher lista instalada, usuário, IP no JSON, paths, segredos, arquivo de log, serial, PID, IDs de dispositivo ou identificador do usuário. A infraestrutura HTTPS pode tratar metadados de rede independentemente do JSON e isso deve ser divulgado.
- Payload `{schema,version,exit,counts}` estritamente validado, com máximo 4 KB e apenas categorias fixas. Contagens por categoria, não nomes de programas e não um arquivo por máquina.
- Relatório temporário fica **fora do histórico de commits e Issues públicas**. Após agregação validada, apagar entrada temporária; aplicar TTL de sete dias mesmo quando a IA não agir.
- GitHub permite excluir artifacts do Actions via API; `retention-days` define prazo de retenção, mas apagar artifacts não garante apagar metadados, logs, cópias de segurança ou o histórico Git. Usar runners efêmeros e **não escrever o relatório nos logs**.
- Issues, PRs e changelog armazenam apenas conclusões técnicas deduplicadas, contagens gerais e testes, sem conteúdo individual.
- Se o usuário revogar, parar futuros envios e eliminar fila local. A ausência de identificador individual limita a exclusão retroativa de contagens já agregadas; descrever claramente isso antes do consentimento remoto.

## Critérios de ativação

- [x] Consentimento e retenção **local** na v5.13.11.1, com build e regressões Windows aprovadas.
- [ ] Definir se o público-alvo do primeiro piloto terá conta GitHub (ou se precisamos de receptor gerenciado sem login).
- [ ] Confirmar mecanismo de IA, plano/custos, permissões de agente e segredos somente no GitHub.
- [ ] Configurar repositório/área privada para agregados e política de acesso/retirada.
- [ ] Validar interface de consentimento **para envio**, destinatário, privacidade e retenção.
- [ ] Testar limites, entradas maliciosas, requisições repetidas, offline, falhas do destino e duplicações, sem interromper fechamento.
- [ ] Integrar workflow de análise de Issue saneada e abertura de PR para aprovação.
- [ ] Validar em PC FRACO/segundo computador com antivírus ativo, além da avaliação AVG.
- [ ] Aprovar regras de branch, CI, revisão de licença e assinatura para builds distribuídas.

## Referências oficiais

- GitHub Agentic Workflows: https://docs.github.com/en/copilot/concepts/agent-capabilities/agent-powered-workflows-and-operations/about-github-agentic-workflows
- Copilot Automations (restritas a repositórios privados/internos): https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/create-automations
- API de envio de eventos (autenticação): https://docs.github.com/en/rest/repos/repos#create-a-repository-dispatch-event
- Exclusão de artefatos do Actions: https://docs.github.com/en/rest/actions/artifacts#delete-an-artifact
- Proteção contra conteúdo de terceiros: https://docs.github.com/en/actions/concepts/security/script-injections
