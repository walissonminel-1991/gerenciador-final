# GF BETA .66 — Rodada 2: segurança, retenção e recuperação

Data: 26/10/2026. Escopo: fonte, simulações, CI e proposta operacional. **Nenhuma implantação, migração remota, ativação ou transmissão de diagnóstico real está autorizada ou foi realizada.**

## 1. Resumo executivo e continuidade

O HEAD da PR #62 foi conferido na API GitHub antes de alterar arquivos: `864e4c73bb975b19a7713ce1befd4121495bcedf`, branch `work-beta-retencao-issues-20261026`, aberta/Draft, base BETA. A CI [40363104639](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40363104639) estava **completed/success no mesmo HEAD**. Portanto, a falha antiga do espelho VLC não foi tratada como estado atual da base.

PR #61 conferida aberta/Draft, HEAD `f64600748b45b0ddc7e39c530ac56809c26f7ea8`, base `work18-ampliacao-atualizacoes-seguras` preservada. `main` conferida em `410d65e23546976db456337c71e4705f10217d36`; nenhum commit desta missão é dirigido a ela. O trabalho segue na [PR #62](https://github.com/walissonminel-1991/gerenciador-final/pull/62), sem PR duplicada, merge, fechamento ou reescrita de histórico.

A falha prioritária foi confirmada e corrigida: uma intenção sem número **não é mais apagada por expiração nem por varredura textual sem correspondência**. Criações incertas não são repetidas automaticamente. Há estados explícitos, preservação de IDs de duplicatas antes da exclusão, prova de associação por operação/ator e bloqueio administrativo para o que não pode ser comprovado. O cliente passou a exigir endpoint/aviso revisados e a cancelar a transmissão ao retirar o consentimento. O receptor ganhou parsing JSON sem chaves repetidas, orçamento diário transacional e observabilidade sem dados individuais.

## 2. Achados, severidade e evidências

| Severidade | Problema confirmado na base | Correção / bloqueio |
|---|---|---|
| Crítica | `DELETE ... issue_number=0 AND expires_at<=?` descartava intenção após busca textual, mesmo com Issue criada/alterada | Removido. `uncertain`/`review_required` preservados; nenhum replay de POST ambíguo; testes com corpo/título alterados e nove dias de atraso |
| Alta | Dez minutos de espera seguidos de novo POST não comprovavam ausência da criação anterior | Replay automático eliminado. Pendência bloqueia criação global e exige prova positiva/triagem |
| Alta | Formato/título bastavam para excluir Issue sem vínculo registrado | Marcador por operação e ator previamente gravados; legado sem prova não é adotado/excluído automaticamente |
| Alta | Falha ao excluir duplicata podia perder a associação se o texto mudasse | `github_delete_refs` guarda ID antes de DELETE; retry usa ID/destino, não texto |
| Alta | Credencial poderia vencer antes das exclusões e gerar novas obrigações | Metadata de expiração, aviso de rotação, gate de oito dias para novas exportações; falha de autenticação preserva obrigações |
| Alta | Receptor aceitava JSON válido repetidamente sem orçamento de escritas global | Teto transacional de 1000 relatórios/dia UTC, com rollback conjunto de orçamento e contagens; rate limit adicional por chave comum/PoP |
| Alta | Endpoint injetado no build podia apontar para qualquer domínio HTTPS público | Lista exata e hash do aviso revisados em fonte; ambos vazios nesta missão; build não aprovado falha e cliente não habilita aceite |
| Alta | Revogação não cancelava POST e uma resposta antiga podia apagar uma nova pendência | Lease local de envio, cancelamento, revalidacao da escolha persistida e identidade de arquivo comparada/removida no mesmo handle |
| Média | `JSON.parse` perdia evidência de chaves repetidas | Parser limitado por bytes, profundidade/nós, com rejeição de duplicatas inclusive escapadas |
| Média | Execução antiga poderia voltar após perder o lease | Verificação de lease com margem antes de mutações e rechecagem de deadline depois de espera |
| Média | Sucesso parcial podia renovar `last_success`; monitoramento era só procedimento | Sucesso completo separado de progresso, heartbeat, erro sanitizado e snapshot operacional; observador independente preparado, desligado |
| Média | `0002` não era seguro para repetição ou falha parcial | `0002` preservado; planejador inspeciona e gera upgrade adaptativo/atômico, com falha injetada em cada instrução |
| Média | Aceite remoto não identificava aviso aceito | Schema local 2 com versão/hash; schema 1 LocalOnly continua local, RemoteBeta antigo volta a pedir escolha |
| Média | Aviso fixo podia ultrapassar viewport em escala alta | Diálogo limitado à área disponível, texto rolável e matriz de layout simulada 720p/900p/1440p, 100/150/200% |

## 3. Implementado, simulado e ainda não validado realmente

| Área | Implementado | Testado em simulação/CI | Validação real pendente |
|---|---|---|---|
| Retenção | Estados, prova por nonce/ator, IDs de duplicatas, bloqueio de ambiguidade/capacidade | SQLite real + API fictícia: resposta perdida, adulteração, backoff, lease, paginação, duplicatas e falhas D1 | Token real, permissões efetivas, exclusão em GitHub descartável autorizado |
| Receptor | JSON estrito, limite de corpo/tempo, chave comum por PoP e orçamento D1 | Payload repetido, quota, rollback, parsing e cancelamento de stream | WAF real, volumetria distribuída e custos |
| Cliente | Endpoint exato/aviso revisado, cancelamento, exclusão condicional e aceite versionado | Handlers HTTP sintéticos e fixtures locais; matriz WinForms na CI aplicável | Windows físico/AVG, DPI real, rede lenta e ciclo de bandeja/fechamento |
| Migração | Inspeção, plano e CLI exclusivamente local | v1/v2/parcial, repetição, rollback em cada instrução, índices e conflitos | Clone D1 autorizado, backup e aplicação real posterior |
| Monitoramento | Snapshot/alertas sem conteúdo individual; observer separado sem endpoint | Cron ausente, erro, atraso, credencial e sanitização | Scheduler independente, canal de alertas e governança de acesso |

O nonce é de uma exportação do **servidor**, não um ID de PC. O ator GitHub pertence ao executor da operação. Esses metadados não entram nos alertas. A identidade do arquivo é usada somente em memória, sem ser gravada/enviada; uma outra instância com escolha persistida diferente interrompe o envio. O comprovante local do aviso é igual em estrutura entre participantes e não contém hora/ID individual. Nenhum teste lê Secret Cloudflare ou cria Issue no destino privado real.

## 4. Arquivos e justificativas

| Arquivo(s) | Motivo |
|---|---|
| `prototipos/beta-telemetria/github-retention.mjs` | Preservar obrigações, limitar replay, identificar propriedade, recuperar duplicatas, cercar lease e classificar autenticação |
| `policy.mjs` | Marcador de operação e prova com ator, preservando categorias/versão/supressão |
| `schema.sql` | Estrutura nova v3, estados, referências de duplicatas, heartbeat e orçamento |
| `schema-migration.mjs`, `plan-migration-local.mjs` | Preflight/adaptação e ensaio transacional só em SQLite local; nenhuma rotina remota |
| `worker.mjs`, `strict-json.mjs` | Rejeitar JSON ambíguo, aplicar rate limit e quota de aceitação com agregação atômica |
| `observability.mjs`, `health-observer.mjs` | Medir manutenção/retardo e preparar monitor independente sem exposição de dados |
| `test-support.mjs`, `test.mjs`, `retention.test.mjs`, `round2.test.mjs` | Simulador sem rede e regressões adversariais. Expectativas antigas de apagar/repetir sem prova foram substituídas por bloqueio explícito, não removidas |
| `AnonymousTelemetry.cs`, `TelemetryConsentPolicy.cs` | Aviso versionado, lease/generation local, cancelamento e confirmação da pendência correta |
| `BetaEndpointPolicy.cs`, `BetaTelemetryPolicy.props`, `BetaTelemetryUploader.cs`, `BetaTelemetryTransport.cs` | Gate de build/runtime, lista exata vazia, aviso ainda não aprovado e transporte testável sem cookies/redirecionamento/credenciais padrão |
| `TelemetryPrivacyDialog.cs`, `PrivacyConsentPresentation.cs`, `Program.cs` | Aviso compartilhado, ajuste à área, estado remoto legível e proteção contra fechamento/reenvio concorrente |
| `GerenciadorAplicativos.csproj`, `TestesTelemetria/*`, `TestesLayout/Program.cs` | Gate negativo de endpoint e execução das verificações de cliente/UI |
| `.github/workflows/validar-candidata-windows.yml`, `build-windows.yml` | Incluir nova suíte Node/build negativo; compilar e verificar BETA sem disponibilizar o executável como artefato/atestado, conservando evidências técnicas |
| `PRIVACY.md`, exemplos Wrangler, `OPERACAO-RODADA2.md`, relatório | Transparência, recuperação, limites, rotação e ações de autorização futura |

## 5. Testes e CI

Os 70 testes Node da base foram executados e aprovados antes das mudanças. A suíte atual possui **113 testes Node**, aprovados localmente, e a descoberta Python usada pela CI executou **41 testes**, aprovados. Isso inclui os 18 testes anteriores de políticas. Sintaxe Node e diff foram verificados. O host local não possui SDK .NET; execução .NET/WinForms depende da CI Windows, sem simular aprovação local.

```sh
node --test prototipos/beta-telemetria/test.mjs prototipos/beta-telemetria/ingest.test.mjs prototipos/beta-telemetria/retention.test.mjs prototipos/beta-telemetria/round2.test.mjs
python3 -B -m unittest discover -s scripts -p 'test_p*.py' -v
dotnet run --project TestesTelemetria/TestesTelemetria.csproj -c Release
dotnet run --project TestesLayout/TestesLayout.csproj -c Release
```

As últimas duas rotinas devem rodar no Windows descartável da CI. O transporte usa somente `HttpMessageHandler` fictício: recusa, revogação durante envio, falha ao persistir retirada do envio remoto, timeout, erro/redirect, envio concorrente, relatório adulterado e confirmação antiga sobre nova pendência. A suíte WinForms também verifica o aceite bloqueado sem receptor e o acesso pela bandeja. A matriz de escala não equivale a mudança de DPI físico no PC de um voluntário.

O microbenchmark local de 10.000 parses de payload sintético mediu p95 aproximado de **0,0041 ms**, máximo de **0,604 ms**, somente para o parser Node. Não mede toda a requisição, o cron ou CPU no isolate Cloudflare. Não foi usado como critério de homologação do plano Free.

**Evidência de CI da rodada:** consultar [verificações da PR #62](https://github.com/walissonminel-1991/gerenciador-final/pull/62/checks) e o resultado por HEAD registrado na descrição da PR. A CI base aprovada não se transfere a alterações novas. Caso uma fonte externa falhe, registrar etapa/causa e manter HTTPS, identidade, assinatura e hash; uma reexecução explicada não substitui correção de código quando necessária. Nenhum pacote BETA é distribuído por esta missão.

## 6. Comparação de segurança e integridade

| Antes | Depois |
|---|---|
| Busca sem correspondência podia aposentar intenção | Ausência textual não encerra obrigação |
| Criação incerta podia ser repetida | Criação incerta conserva estado e bloqueia novos POSTs |
| Síntese com formato parecido podia ser adotada | Vínculo persistido e ator/nonce ou ID previamente comprovado |
| Duplicata precisava manter texto reconhecível | ID de duplicata guardado antes de excluir |
| Qualquer versão HTTPS pública de endpoint podia habilitar envio | Build/runtime exigem lista exata e aviso revisado; ambos fechados |
| Preferência remota sem versão do aviso | Nova escolha quando versão/hash diverge |
| Revogar só alterava preferência/arquivo | Também cancela tentativa e invalida confirmação antiga |
| Eventos recebidos sem orçamento diário global D1 | 1000 relatórios/dia, orçamento e contagens no mesmo commit |
| Metadados com sucesso parcial | Heartbeat/progresso separados de manutenção completa |
| Migração única sem adaptação de prefixos parciais | Preflight, plano repetível e teste de rollback por instrução |

Relatórios aceitos contam requisições gravadas; **eventos** somam valores declarados e podem saturar. Reenvios podem contar mais de uma vez. **Participantes não são estimados**: não há identificação para deduplicar máquinas. As contagens são não autenticadas; supressão/arredondamento não comprovam anonimato ou prevalência de falhas.

## 7. LGPD e riscos ainda não resolvidos

A revisão usa LGPD e orientações oficiais da ANPD como requisitos de engenharia, sem declarar conformidade jurídica. Necessidade/transparência, prova e revogação, identificação/contato do controlador e direitos precisam de definição antes do piloto. O registro local de versão/hash é editável e não prova identidade ou manifestação humana independentemente. Sem identificadores de voluntário, não é possível localizar uma contribuição individual dentro de um agregado; o procedimento de direitos deve explicar isso. Repositório privado e contagens pequenas não tornam o dado automaticamente anônimo. A avaliação de operadores/transferências deve considerar Cloudflare, GitHub e retenções externas.

Riscos e bloqueios restantes:

1. **Permissão real:** `viewerPermission=ADMIN` é uma barreira conservadora, não prova do escopo do token para `deleteIssue`. Autenticação GraphQL e permissões por recurso precisam de ensaio autorizado com credencial de teste. Não se amplia escopo aqui.
2. **Ambiguidade irrecuperável:** marcador removido, legado sem prova, restore anterior ao registro ou alterações administrativas fora do protocolo podem exigir triagem permanente. Não se fabrica prova de inexistência nem se descarta a obrigação para liberar a fila.
3. **Prazos:** sete dias é o vencimento, não uma garantia de apagamento físico durante indisponibilidade, quotas ou backlog. O gate bloqueia novas exportações em ambiguidade/capacidade; exclusões e alertas devem continuar.
4. **Abuso:** quota evita escritas de contagens ilimitadas, mas um atacante pode consumir admissões, leituras e orçamento de borda. Não há autenticidade estatística dos relatos nem deduplicação de pessoas. WAF e alertas financeiros/globais continuam pendentes.
5. **Plano Cloudflare:** Free documenta CPU de 10ms e até 50 consultas D1 por invocação. Limitar operações/corpo/páginas não comprova CPU p95/p99 no plano real; homologação de CPU/memória/custo é bloqueador. Paid e outros limites precisam ser conferidos para a conta usada, sem inferir o plano atual.
6. **Credencial:** expiração prevista 25/11/2026 é informação do proprietário, sem verificação do valor do token. Metadata pode divergir. Rotação e exclusão após 401 exigem intervenção autorizada.
7. **Governança:** faltam controlador/contato/aviso final, atendimento de direitos, contratos/transferências, acesso de colaboradores e retenção de notificações/backups. Exclusão de Issue não elimina cópias externas.
8. **Cliente:** cancelamento não retira bytes já recebidos pelo servidor; falha de disco pode impedir persistência da recusa para próxima abertura, embora a sessão corrente pare de transmitir e informe a falha. Retirada não persistida não pode ser observada por outro processo; fechar outras instâncias antes de nova tentativa é parte da homologação. Malware/usuário com acesso ao perfil pode editar o registro local. AVG, hardware e DPI reais não foram homologados aqui.

## 8. Migração, monitoramento e rotação

O procedimento integral, estados, thresholds e checklist estão em [OPERACAO-RODADA2.md](../prototipos/beta-telemetria/OPERACAO-RODADA2.md). Ele cobre: backup e preflight; v1/v2/parcial; transação e rollback; restore sem retomar imediatamente; triagem sem descartar intenções; cron ausente; erro/atraso/lease; rotação prevista; GitHub indisponível e credencial revogada.

Os exemplos preservam `PILOT_ENABLED=no`, `GITHUB_EXPORT_ENABLED=no`, `workers_dev=false` e `preview_urls=false`. Observabilidade também vem desligada. Nenhuma configuração remota foi lida/alterada e nenhum Secret foi acessado. O observador não recebe token GitHub. Nenhuma ação de deploy/migração/rotação é embutida nos testes ou na CI desta PR.

## 9. Ações que exigem autorização do proprietário

- Aprovar controlador/contato, aviso e política final; aprovar domínio/caminho/hash em fonte.
- Criar qualquer ambiente/integração/cron de observação ou canal de alertas.
- Ler/alterar configurações remotas, aplicar migração D1, restaurar backup ou corrigir pendência administrativa.
- Criar, verificar, substituir ou revogar credenciais; testar capacidade de exclusão com token de teste.
- Implantar Worker/módulos, alterar WAF/flags/URLs e realizar ensaio de rede em ambiente descartável aprovado.
- Preparar/distribuir executável com receptor ativo, convidar voluntários ou iniciar piloto.

Nenhuma foi realizada. As mudanças de fonte e PR foram autorizadas pela missão; não há pedido de confirmação para decisões técnicas locais. Main, bases das PRs e checkpoints do roadmap não foram alterados.

## 10. Recomendação

**Não apto a avançar para homologação conectada/voluntários neste estado.** A implementação está preparada para revisão e ensaios locais/CI; o avanço depende de CI aplicável registrada, revisão das permissões/exclusão real, plano/CPU/WAF/monitoramento, migração ensaiada e aprovação do aviso/controlador/contato. Os problemas críticos foram corrigidos em código ou bloqueados explicitamente para triagem, sem antecipar a ativação. A conclusão técnica desta missão não equivale a liberação do piloto.

## Referências oficiais consultadas

- [GitHub: autenticação GraphQL e permissões por recurso](https://docs.github.com/en/graphql/guides/forming-calls-with-graphql).
- [GitHub: deleteIssue](https://docs.github.com/en/graphql/reference/issues#deleteissue) e [papel administrativo para exclusão](https://docs.github.com/en/issues/tracking-your-work-with-issues/administering-issues).
- [Cloudflare: limites Workers/CPU/cron](https://developers.cloudflare.com/workers/platform/limits/), [limites D1](https://developers.cloudflare.com/d1/platform/limits/) e [batch transacional](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch).
- [LGPD, texto compilado](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm).
- [ANPD: perguntas frequentes](https://www.gov.br/anpd/pt-br/acesso-a-informacao/perguntas-frequentes) e [regulamento de transferência internacional](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-19-de-23-de-agosto-de-2024).
