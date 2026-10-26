# Auditoria BETA .66 — retenção e recuperação de agregados

Data: 26/10/2026. Escopo: código e testes sintéticos; **não autoriza implantação ou distribuição**.

## Estado de partida e preservação

- PR [#61](https://github.com/walissonminel-1991/gerenciador-final/pull/61): aberta, Draft, sem merge, HEAD `f64600748b45b0ddc7e39c530ac56809c26f7ea8` verificado pela API e pelo clone local.
- Branch de origem: `beta-piloto-consentimento-envio-20261026`, base `work18-ampliacao-atualizacoes-seguras` em `2bc9f4228fd448445fbf181652e5af93005def20`.
- CI desse HEAD: execução [40324802334](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40324802334), **completed/success**. Os seis testes Node originais foram reexecutados e passaram antes das alterações.
- Trabalho isolado na branch `work-beta-retencao-issues-20261026`, com PR destinada à branch BETA. `main`, branch BETA e PR #61 não recebem commits desta auditoria automaticamente.
- Infraestrutura informada pelo proprietário: Worker, D1, rate limiter e destino privado preparados; flags `no`, URLs alternativas desligadas e tabelas vazias. Esta informação foi preservada como declaração operacional, sem acessar Cloudflare, ler credenciais ou repetir provisionamento.

## Achados e correções

| Gravidade | Achado no HEAD inicial | Resultado proposto |
|---|---|---|
| Alta | Cron excluía `github_exports` sem excluir a Issue remota | Referência preservada até sucesso de `deleteIssue` ou ausência autenticada sem erros |
| Alta | GitHub não tinha prazo de retenção | Vencimento compartilhado D1/Issue no dia UTC da coleta + 7; cron tenta exclusão física e conserva pendência em falhas |
| Alta | Deduplicação só nas primeiras 100 Issues e baseada em título | Paginação de abertas/fechadas, formato exato v1/v2, intenção anterior à criação e reconciliação de órfãs |
| Alta | Falhas/limites da API sem estado de recuperação | Cooldown persistido, backoff, budget, timeout, controle de erros GraphQL e prioridade à exclusão |
| Média | Cron concorrente podia duplicar criação | Lease D1 atômico de 10 minutos, com recuperação após expiração |
| Média | Versão arbitrária multiplicava chaves D1 | Entrada aceita apenas `v5.13.11.66`, 14 categorias, até 100 por categoria e total agregado saturado |
| Média | Grupo pequeno podia expor contagens muito específicas | Supressão abaixo de 5 e arredondamento; aviso explícito de que isso não mede pessoas nem garante anonimato |
| Média | Falhas do limitador ou preparação SQL escapavam do receptor | Resposta genérica 503, sem corpo recebido, mensagem remota ou credencial no log |

## Implementação e limites operacionais

`policy.mjs` centraliza categoria, versão, vencimento e formato reconhecível da síntese. `github-retention.mjs` mantém a máquina de recuperação, com SQL parametrizado. `worker.mjs` continua responsável pela ingestão de até 4096 bytes, agora também limitada por prazo total de leitura. A limpeza local e remota roda com o piloto desligado; desligar coleta não apaga as obrigações anteriores.

A exclusão usa GraphQL `deleteIssue`, e não apenas fechamento ou edição do corpo. Antes das operações, o destino deve ser privado, ter ID estável e papel efetivo `ADMIN`. Isso é uma barreira conservadora: não comprova sozinho que a credencial pode executar a mutação. A permissão real ainda requer ensaio em ambiente descartável, sem examinar o secret atual. Não foi solicitado aumento de escopos ou alterações de Secrets.

O prazo é calculado por `(day + 7) * 86400`, sem renovação no retry/exportação. Como o vencimento é por dia UTC, a retenção de um evento pode ser menor que sete dias. A remoção física depende do próximo cron bem-sucedido; não há garantia de apagamento exato no segundo do vencimento. O exemplo propõe manutenção horária, mas **nenhum cron remoto foi alterado**. Se a instalação mantiver cron diário, a espera normal pode chegar a 24 horas, além de indisponibilidade, quotas e backlog.

O ledger guarda apenas dia, número/ID da Issue, ID do repositório, instante da tentativa e prazo. Mantê-lo após vencimento enquanto houver falha de exclusão é intencional: descartá-lo perderia a obrigação sem apagar a síntese. A linha de controle tem tamanho fixo e registra cursor, lease, último código de resultado e retry; não registra relatórios ou IP.

Por execução: até 30 chamadas GitHub sequenciais, no máximo 44 operações combinadas SQL/API durante o trabalho e reserva de controle para manter o total conservador em 50. São processadas até oito referências conhecidas e três páginas de 100 Issues por lote; o restante é retomado. Há deadline de trabalho de 120 segundos, timeout de rede de oito segundos e intervalo mínimo de um segundo entre mutações. `Retry-After`, reset e GraphQL HTTP 200 com erro suspendem novas chamadas; recuo exponencial vai de um minuto a seis horas, respeitando a cadência do cron.

Uma criação incerta conserva intenção com número zero. A varredura procura o formato reconhecível antes de repetir e aguarda ao menos dez minutos. Resposta perdida no delete mantém a referência e a próxima execução confirma ausência. Falhas D1 posteriores não induzem descarte de obrigação. Não existe transação distribuída GitHub/D1: trata-se de reconciliação com remoção de duplicatas reconhecidas, sem promessa de exactly-once.

## Privacidade, consentimento e LGPD

Esta é uma avaliação técnica, **não uma declaração de conformidade jurídica**. O cenário deve ser revisado antes de qualquer distribuição, considerando também o IP e outros metadados processados pelos provedores.

| Área | Evidência disponível | Pendência para liberar o piloto |
|---|---|---|
| Escolha livre | Diálogo sem aceite padrão, recusa sem perda de função e revogação pela bandeja; testes existentes preservados | Homologar o fluxo no Windows, inclusive encerramento durante rede lenta |
| Consentimento separado | `LocalOnly` não autoriza `RemoteBeta`; cliente sem endpoint mantém aceite bloqueado | Registrar versão/hash do aviso e estratégia de prova do consentimento. Preferência local `schema/state` não é uma trilha histórica verificável |
| Transparência | Aviso atualizado com retenção, falhas e risco de grupos pequenos | Identificar/aprovar controlador e canal de contato no aviso acessível ao participante; detalhar destinatários e direitos |
| Minimização | Payload de contagens allowlisted; sem nomes, inventário, caminho, identificador de PC ou IP persistido no D1 | Revisar logs de borda/observabilidade e contratos/retencão dos provedores |
| Exclusão e revogação | Revogar bloqueia novos envios; cron apaga sínteses e conserva obrigação se falhar | Definir atendimento a pedidos relativos a agregados não individualizáveis, sem prometer remoção individual tecnicamente impossível |
| Acesso e transferências | Destino privado, identidade fixada e verificação administrativa; segredo só no backend | Auditar colaboradores/Apps, retenção de notificações/backups, mecanismo aplicável às transferências e resposta a incidentes |
| Abuso | Rate limiter obrigatório, limite de corpo/prazo/versão/categorias e saturação | Comprovar WAF e orçamento global de escritas; não há autenticação de participantes nem contagem confiável de pessoas |

Os arts. 6, 8, 9, 12, 16 e 18 da LGPD fundamentam a revisão de necessidade, transparência, prova/revogação e direitos. A existência de contagens não demonstra anonimização. Transferências internacionais demandam avaliação própria, inclusive dos mecanismos previstos pela regulamentação da ANPD. Não se presumiu que um projeto pessoal gratuito está automaticamente fora da LGPD.

## Testes e reprodução

Os testes usam SQLite em memória (`node:sqlite`) com as consultas SQL reais e simulador local da API. Não carregam Secrets, não resolvem endpoint do piloto e substituem a rede por funções locais; qualquer `fetch` não previsto falha. Fixtures contêm somente datas, contagens e IDs fictícios. O adaptador local não simula limites de CPU, consistência de rede ou todos os erros do D1 gerenciado.

```sh
node --test prototipos/beta-telemetria/test.mjs prototipos/beta-telemetria/ingest.test.mjs prototipos/beta-telemetria/retention.test.mjs
python scripts/test_politicas_seguranca.py
```

A suíte foi ampliada dos seis casos iniciais para **70 testes Node**, todos aprovados localmente. As **18 verificações Python** de políticas também passaram. Node local: `v24.22.1`; SQLite local: `3.53.4`. A CI Windows passa a preparar Node e executar as três suítes, além dos testes preexistentes de consentimento e layout. O resultado da CI da nova PR deve ser consultado no commit correspondente; a aprovação da CI inicial #61 não se transfere às alterações.

Cobertura significativa: migração v1 sem perda de referência; vencimento na fronteira; flags desligadas; Issue fechada/órfã/duplicada; mais de 100 e 300 Issues; interrupção entre páginas; 403/404/429/5xx/redirect; GraphQL parcial e HTTP 200 com erros; `Retry-After` numérico/data; falhas D1 antes/depois das mutações; resposta perdida; lease e reinício; budgets e espaçamento; supressão; versão arbitrária; rollback da ingestão; UTF-8 inválido; stream lento e erros sanitizados. O teto combinado de SQL/API também possui teste executando o handler do cron.

Não foram executados testes remotos com o receptor, Issues de dados, credenciais reais, dispositivos pessoais, AVG ou hardware físico. O host local não possui .NET; os testes WinForms/.NET dependem da CI Windows. Não foram alterados checkpoints do roadmap.

## Riscos residuais e próximos passos

1. Revisar a PR filha, a migração e os resultados da CI antes de integrar à branch BETA. Não fazer merge nesta missão.
2. Em autorização futura, aplicar uma única migração no destino original verificado e publicar Worker **com seus módulos**, mantendo as flags desligadas. `schema.sql` v2 é para instalação nova; não modifica tabela v1 existente.
3. Comprovar no ambiente descartável a permissão para `deleteIssue`, cron, recuperação, alarmes e reconciliação após restauração de backup. A auditoria não configura monitoramento remoto. Falha de exclusão pode reter dados além do prazo até intervenção/recuperação.
4. Aprovar controlador/contato, aviso versionado, evidência de consentimento, direitos, transferências, contratos e tratamento de agregados pequenos. Supressão/arredondamento não autorizam chamar os dados de anônimos.
5. Validar WAF, custo/limites globais e origem dos resumos. Contagens são relatos não autenticados; não comprovam participantes, prevalência de falhas ou conclusão de checkpoints.
6. Uma Issue órfã manualmente alterada fora do formato reconhecível pode exigir triagem privada autorizada. Cópias, e-mails, backups e retenções internas do provedor não são eliminados pela mutação de Issue. Mudança concorrente de visibilidade do repositório exige proteção administrativa: a verificação da API não é uma transação com a configuração externa.
7. Só após os gates anteriores: homologação Windows/AVG e decisão separada de distribuição. Nenhum novo EXE BETA com receptor ativo foi preparado nesta missão.

## Referências oficiais consultadas

- [GitHub: deleteIssue e DeleteIssueInput](https://docs.github.com/en/graphql/reference/issues#deleteissue).
- [GitHub: exclusão exige administração](https://docs.github.com/en/issues/tracking-your-work-with-issues/administering-issues#deleting-an-issue).
- [GitHub: limites GraphQL, HTTP 200 com erros e intervalo entre mutações](https://docs.github.com/en/graphql/overview/rate-limits-and-query-limits-for-the-graphql-api).
- [Cloudflare: D1 batch e rollback](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch).
- [Cloudflare: Cron Triggers](https://developers.cloudflare.com/workers/configuration/cron-triggers/), [limites](https://developers.cloudflare.com/workers/platform/limits/) e [rate limiting aproximado por localidade](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/).
- [LGPD, texto compilado](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm).
- [ANPD: Regulamento de Transferência Internacional de Dados](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-19-de-23-de-agosto-de-2024).
