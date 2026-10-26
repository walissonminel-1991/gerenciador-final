# BETA .66 — roteiro de homologação conectada

Preparação de fonte na PR #62; **nenhuma operação abaixo foi autorizada por este documento**. Não há candidata offline substituindo o piloto. A compilação comum da CI é interna e mantém o envio bloqueado; somente a compilação conectada com configuração/aviso aprovados poderá ser candidata. Main, PR #61, Secrets e flags reais permanecem preservados.

## 1. Aprovações e sequência operacional

| Ordem | Ação para autorização específica | Alvo e condição |
|---|---|---|
| A | Confirmar responsável, canal privado, aviso r3, fornecedores/transferências e domínio | Alteração revisada em fonte, sem Secret; endpoint exato e hash do texto final |
| B | Provisionar ensaio descartável e credencial própria | Worker/D1/repo `OWNER/gf-beta-rehearsal-NOME`, privados; sem credencial da produção |
| C | Executar homologação sintética conectada | WAF ativo, criação/leitura/`deleteIssue`, migração em cópia, falhas/recuperação, Windows/AVG |
| D | Migrar D1 existente e implantar Worker/módulos revisados | `gf-beta-private` / `gerenciador-final-beta-private`, flags ainda `no`; mudança/rollback revisados |
| E | Ativar flags/cron/observador e testar canário fictício no destino aprovado | Homologação anterior aprovada, monitoramento independente em funcionamento |
| F | Compilar candidata conectada e aprovar entrega a primeiro grupo | SHA do build, endpoint/aviso conferidos no assembly, AVG ativo; aprovação final distinta da compilação |

Não é necessário o avaliador fazer login no GitHub ou entrar em página para a coleta. O proprietário faz a preparação do servidor; o avaliador somente decide no GF. Convites/mensagens e distribuição continuam manuais e dependem de aprovação.

## 2. Endpoint HTTPS e WAF propostos

Proposta verificável em [endpoint-waf.proposed.json](../prototipos/beta-telemetria/endpoint-waf.proposed.json). Usar um hostname dedicado da zona do proprietário, por exemplo `diagnostico-beta.<dominio-do-proprietario>`, com `https://HOST/ingest` como única URL do aplicativo. Esse exemplo não afirma propriedade de um domínio. DNS/certificado/route ou Custom Domain e WAF precisam ser conferidos juntos na conta correta.

- Preservar IDs atuais do Worker, D1, namespace do rate limiter, rotas e recursos. Não implantar o exemplo `.toml` diretamente sobre a configuração real nem trocar namespace por `1001`.
- `workers_dev=false` e `preview_urls=false` explícitos; verificar também configuração no painel e ausência de versões/aliases acessíveis. O Worker confere adicionalmente `INGEST_ORIGIN`, origem HTTPS exata sem path/query/porta, e recusa origens alternativas mesmo que uma rota seja reaberta.
- WAF: bloquear HTTP, caminhos/métodos/query fora do contrato no host dedicado; regra de rate limit somente no POST `/ingest`. Não usar desafio interativo/JavaScript no cliente desktop. Não criar regra de Skip ampla nem desativar proteções para passar no teste. Confirmar quais campos/parâmetros existem no plano da zona; a proposta 6/60s depende disso.
- Limite real de 4096 bytes e timeout de leitura são do Worker; `Content-Length` é conferido contra bytes reais. Não prometer que WAF Free inspeciona todo corpo. Nenhum GET de D1/health/admin, CORS de consulta ou endpoint de SQL é exposto.
- Manter coleta/exportação em `no` até a autorização correspondente. Testar primeiro em hostname e bindings descartáveis com payload fictício; flags de ensaio são distintas das de produção.
- Revisar logs automáticos, WAF e retenções de plataforma. Não habilitar gravação do corpo, headers de autenticação ou IP em logs da aplicação. Health sanitizado não exige log de requisições.

Critério: 202 somente no host/payload/schema aprovados; origem alternativa 403 no Worker, caminho desconhecido 404, método 405, sem flags/bindings/schema v3 503, payload extra 422, JSON ambíguo 400, tamanho excessivo 413 e limite atingido 429. WAF pode bloquear antes e devolver 403/429/erro de provedor: registrar separadamente de resposta do Worker. Não seguir redirecionamento algum no ensaio de telemetria.

Fontes: [Custom Domains](https://developers.cloudflare.com/workers/configuration/routing/custom-domains/), [WAF custom rules](https://developers.cloudflare.com/waf/custom-rules/), [limites Workers](https://developers.cloudflare.com/workers/platform/limits/), [D1 preços](https://developers.cloudflare.com/d1/platform/pricing/). Consultadas em 26/10/2026; confirmar o plano efetivo.

## 3. D1: ensaio, migração existente e rollback

Continuam válidos o [planejador](../prototipos/beta-telemetria/schema-migration.mjs) e [procedimento de recuperação](../prototipos/beta-telemetria/OPERACAO-RODADA2.md). `schema.sql` é para instalação nova; `0002` não deve ser reaplicada sobre tabelas já alteradas. Versão futura desconhecida agora bloqueia o plano, sem rebaixar o banco.

Ensaio local permitido, sem rede:

```sh
node --test prototipos/beta-telemetria/test.mjs prototipos/beta-telemetria/ingest.test.mjs prototipos/beta-telemetria/retention.test.mjs prototipos/beta-telemetria/round2.test.mjs prototipos/beta-telemetria/finalization.test.mjs
node prototipos/beta-telemetria/plan-migration-local.mjs COPIA_SINTETICA.sqlite
node prototipos/beta-telemetria/plan-migration-local.mjs COPIA_SINTETICA.sqlite --apply-local-fixture
```

O teste de migração injeta falha em cada instrução, confere rollback real SQLite, preservação de referências/prazos e repetição do plano. Isso não comprova comportamento do D1 remoto. Para a cópia remota descartável, após autorização:

1. Conferir conta, ID do banco e schema; registrar fingerprint, versões, índices e contagens, sem publicar linhas. Cópia autorizada de banco real tem as mesmas restrições de acesso; preferir schema equivalente e dados fictícios.
2. Parar escritores e cron antigo somente sob mudança aprovada. Manter flags `no`; registrar bookmark/backup privado e referência do código que poderá ser restaurado. O exemplo não cria backup nem inicia restore.
3. Inspecionar com `inspectSchema(DB)`, revisar `migrationPlan(snapshot)` e comparar fingerprint imediatamente antes. Aplicar as instruções como **um único `DB.batch(plan.map(sql => DB.prepare(sql)))`**, não como sequência independente de `wrangler d1 execute`. Não enviar `BEGIN/COMMIT` do SQLite local como se fossem o mecanismo remoto. Conferir previamente limites de operações e tamanho do plano.
4. Reinspecionar em chamada independente: schema 3, tabelas/índices, singleton, contagens, mapeamento ID/número/nonce/ator e prazos intactos. Injetar falha apenas na cópia e comprovar rollback do DDL/batch. Se a ferramenta deixar prefixo parcial, novo snapshot/plano; nunca apagar referências para destravar.
5. Só depois repetir a mudança no D1 existente com autorização D específica. Implantar conjuntamente todos os módulos revisados e configurar origem exata; D1 antigo é recusado pelo receptor mesmo se a flag estiver incorreta.

A [API D1 batch](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch) documenta rollback da sequência em falha; a homologação deve comprová-lo no plano contratado. Não há endpoint público de migração nesta PR.

Rollback: deixar ingresso/exportação desligados; preferir corrigir para frente com Worker compatível com v3. Não retornar ao Worker v1 que esquecia obrigações. Se restore for necessário, confirmar bookmark e preservar ledger/dívidas criadas depois dele em área privada; restauração pode ressuscitar dados vencidos ou perder intenções. Reconciliar todas as Issues do intervalo, limpar agregados vencidos conforme política aprovada e resolver ambiguidades antes de reativar. Não fazer downgrade SQL destrutivo nem apagar o D1/repositório.

## 4. GitHub: ensaio real preparado, ainda não executado

O servidor sempre valida repositório privado/ID, papel ADMIN e, para conta pessoal, que o ator seja o proprietário. Revalida identidade/privacidade antes de criar. Mudança administrativa de visibilidade é proibida durante o piloto; pausar o serviço antes de qualquer alteração no destino. Não existe transação atômica entre checagem de visibilidade e criação remota.

[GitHub — excluir Issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/administering-issues/deleting-an-issue): em repositório pessoal só a conta proprietária pode excluir. Em organização, conferir ADMIN e política permitindo exclusão. Issues:write/Metadata:read são permissões propostas, **não prova de que o token suporta `deleteIssue`**. Não ampliar automaticamente a permissão nem trocar para repo público se falhar.

Preparado [github-rehearsal.mjs](../prototipos/beta-telemetria/github-rehearsal.mjs), usando o mesmo motor de reconciliação. Sem argumentos ele imprime somente um plano e não lê credenciais nem abre rede. Rejeita os dois repositórios reais antes da rede. Modo real exige repo privado vazio com prefixo `gf-beta-rehearsal-`, aprovação explícita na linha de comando e credencial **própria** via `GF_REHEARSAL_GITHUB_TOKEN`, com data em `GF_REHEARSAL_TOKEN_EXPIRES_AT`. Nunca usar o Secret servidor atual. Provisionar esses valores por canal seguro do operador, sem colar valores em comando, chat, PR ou logs.

Após autorização B/C, substituir somente o repo e o caminho local:

```sh
node prototipos/beta-telemetria/github-rehearsal.mjs --execute --approved-disposable-repository OWNER/gf-beta-rehearsal-20261026 --ledger ENSAIO.beta-rehearsal.sqlite --phase create
node prototipos/beta-telemetria/github-rehearsal.mjs --execute --approved-disposable-repository OWNER/gf-beta-rehearsal-20261026 --ledger ENSAIO.beta-rehearsal.sqlite --phase inspect
node prototipos/beta-telemetria/github-rehearsal.mjs --execute --approved-disposable-repository OWNER/gf-beta-rehearsal-20261026 --ledger ENSAIO.beta-rehearsal.sqlite --phase expire
node prototipos/beta-telemetria/github-rehearsal.mjs --execute --approved-disposable-repository OWNER/gf-beta-rehearsal-20261026 --ledger ENSAIO.beta-rehearsal.sqlite --phase inspect
```

`create` usa somente `MSIX.LEITURA:17` fictício e dia anterior; GitHub recebe 15. `inspect` lê metadados/contagem. `expire` avança **apenas o relógio do processo descartável** para além do vencimento; não altera relógio Windows, D1 real ou calendário do servidor. `recover` reconcilia sem novas criações. Conservar o mesmo ledger entre fases: perder o arquivo pode perder prova de obrigação. Depois de exclusão confirmada e aprovação do operador, remover o banco sintético local; ele é ignorado pelo Git.

Critérios: uma criação, leitura privada por ID, resposta positiva de `deleteIssue` com repo/ID de operação esperados persistida, zero obrigações e zero Issues no destino descartável ao final. Fechar Issue/404/`issue:null` não substitui prova de exclusão. Repetição de `create` com destino não vazio bloqueia. Testar credencial sem direito, 401/403/429, resposta perdida e restauração primeiro com simulador. Ensaios reais de falha destrutiva adicionais precisam de escopo aprovado próprio. Se surgir ambiguidade, `review_required`/`absence_unproven`: preservar ledger, bloquear novas exportações e acionar responsável.

Rotação: expiração **informada** em 25/11/2026, sem ler/verificar o Secret. Preparar substituição antes de 11/11; aviso de 14 dias e bloqueio de novas exportações com menos de 8 dias continuam ativos. Validar nova credencial própria em ensaio; somente após autorização substituí-la no servidor e atualizar metadata de expiração, provar retenção/backlog zero e revogar a anterior. Não colocar token no EXE ou nas configurações versionadas.

## 5. Monitoramento, orçamento e resposta

Cron receptor proposto horário; o observador deve ser independente e privado. Configuração de cron/log/alerta não é feita por esta PR. O exemplo `health-observer.mjs` só emite códigos, quantidades/idades, sem IDs de Issue, repo, IP, nonce ou payload. Não expor health público nem enviar alertas a pessoas nesta missão.

| Sinal | Critério de interrupção/resposta |
|---|---|
| Qualquer obrigação vencida ou ausência sem prova | Crítico imediato; pausar novas exportações por decisão operacional, preservar provas e tentar limpeza/reconciliação |
| Cron sem início por >2h / sem sucesso por >4h | Acionar responsável; não inferir saúde a partir de POSTs aceitos |
| ≥3 falhas consecutivas / 401 ou 403 de manutenção | Investigar credencial, schema, quotas e destino; sem ampliar escopos ou eliminar ledger |
| Erros de CPU 1102, timeout/5xx recorrentes | Pausar ingresso sob autorização, medir isolate/plano e reduzir lote se necessário |
| Origem alternativa respondendo, WAF ausente ou conteúdo público | Parar piloto, preservar evidência privada e executar resposta a incidente aprovada |
| Quota/custo próximo ao limite | Não ativar voluntários; ajustar volume/plano somente com autorização financeira |

O D1 limita a 1000 relatórios aceitos/dia UTC; até 14 categorias/relatório, 17 consultas no caminho de admissão completo incluindo versão, orçamento e limpeza do ticket. Não há quota financeira global de leituras por isso: pedidos rejeitados/spam ainda podem consumir recursos. WAF e limites da conta são necessários. Para o Free, documentação atual indica 100 mil requisições Workers/dia, 10ms CPU/invocação, 50 consultas D1/invocação, 5 milhões de linhas lidas e 100 mil escritas D1/dia; índices também contam em escritas. Não afirmar custo zero ou que 1000 × 14 é o total faturado.

Homologar pelo menos 100 admissões sintéticas e 10 ciclos de manutenção com dados/backlog do ensaio, respeitando limites para não fazer teste de carga indiscriminado. Registrar CPU p95/p99, máximo, duração, leituras/escritas e erros no isolate real. Critério proposto no Free: nenhuma interrupção por limite, CPU p99 ≤8ms e consultas abaixo de 50 com margem; teto diário operacional do piloto ≤50% das quotas compartilhadas. Se não cumprir, bloquear piloto e propor ajuste, sem desligar validações. Node/SQLite/cron simulado não comprovam CPU Cloudflare. Métricas devem ter acesso/retenção aprovados e não conter payload/identidade.

## 6. Compilação, integridade e configuração conectada

CI determinística: Python/políticas, Node/receptor/migração, .NET consentimento/transporte, layout e regressões Windows. Integridade externa dos instaladores tem workflow separado, continua falhando quando TLS/hash/assinatura/identidade falham, sem `continue-on-error`. A candidata depende também de triagem explícita desse resultado; CI determinística verde sozinha não significa que uma receita externa quebrada foi homologada. Não liberar a receita afetada com evidência vermelha sem correção comprovada. Não houve mudança nas barreiras de segurança dos motores.

Build comum: x64 self-contained por commit, identificado como **BETA-INTERNA-BLOQUEADA**; confere metadata no assembly e SHA-256, conserva somente evidência/manifestos, sem EXE/ZIP BETA publicado como artefato. Essa compilação não satisfaz a candidata conectada.

Para testar o cliente conectado antes da homologação física, há o modo `-BuildPurpose Homologacao`: exige aprovação específica `synthetic_homologation_build_approved`, status `approved_for_synthetic_homologation_build`, endpoint descartável em `homologation_endpoint` e aviso/contato aprovados. Não exige evidência AVG anterior, pois produz justamente o espécime para esse teste; continua proibida a distribuição a avaliadores. Essa preparação não altera endpoint/flags de produção.

Após A–E e Windows/AVG aprovados, registrar evidências sanitizadas em `docs/BETA-APROVACOES.json`, status `approved_for_connected_candidate_build`, endpoint/hash idênticos a `BetaTelemetryPolicy.props`, contato e aprovações verdadeiros. Trocar também o contato pendente no texto do aplicativo e aviso completo, recalcular hash e commitar; CI deve passar nesse novo SHA. Nunca preencher os campos apenas para vencer o gate.

```powershell
./scripts/Compilar-Beta-Conectada.ps1 -BuildPurpose Candidata -ExpectedSourceCommit SHA40_APROVADO -Endpoint https://HOST_APROVADO/ingest -OutputDirectory PASTA_NOVA
```

O procedimento exige HEAD exato e fonte rastreada limpa; valida aprovações, publica com runtime incluído, confere versão/canal/commit/endpoint/hash/aviso no assembly efetivo, manifesto Win32, composição e SHA-256 de cada arquivo. Não faz upload/release/distribuição. `BETA-BUILD.json`, `BUILD-INFO.txt` e `SHA256SUMS.txt` formam a evidência. Depois de qualquer assinatura, alteração ou nova compactação, refazer as verificações e registrar hash final do ZIP; aprovação do EXE antigo não cobre bytes novos. Assinatura de procedência/CI não substitui Authenticode ou avaliação AVG.

## 7. Matriz obrigatória de homologação

Registrar para **cada linha**: SHA fonte, ambiente descartável, data, executor, resultado esperado/observado e referência de evidência sanitizada. “Preparado” não é “executado”. Não anexar logs brutos, tokens, banco real ou prints com dados pessoais à PR pública.

| Etapa | Prova objetiva | Situação antes de operação humana |
|---|---|---|
| 1. Simulação isolada | Suítes sem sockets, injeção de falhas, esquema fechado e pipeline sintético completo | Executável local/CI; resultados no relatório final |
| 2. Migração em cópia | Tipos/índices/contagens/referências intactos; falha atômica e reexecução; v4 desconhecida bloqueada | SQLite testado; D1 real pendente |
| 3. GitHub descartável | Criar/ler/excluir, prova positiva, ledger final zero; credencial própria | Script e testes preparados; API real pendente |
| 4. HTTPS/WAF | Host único, TLS válido, sem redirect, regras bloqueando rotas, limites e payload fictício | Configuração proposta; pendente |
| 5. D1 agregado | Somente colunas autorizadas, contagens corretas, `ticket=NULL`, teto 1000 e rollback | SQLite testado; D1 real pendente |
| 6. Exportação/retenção | Síntese privada, supressão/arredondamento, exclusão com flags no, clock de ensaio | Simulado; remoto pendente |
| 7. Windows/AVG ativo | Executável exato e SHA, 720p/900p/1440p e DPI, bandeja, inicialização e aviso acessível | Runner não comprova AVG/PC físico; pendente |
| 8. Consentimento/recuperação | Matriz abaixo, sem envio proibido e sem travar fechamento/desligamento | Testes automatizados + validação real pendente |
| 9. Primeiro grupo | Todos os gates anteriores verdes, aviso/domínio/contato aprovados e autorização final | Bloqueado até aprovação |

Casos Windows/consentimento, inicialmente só fixtures/VM e dados fictícios:

- Primeiro uso/fechar aviso sem escolher/recusar: zero POST e todas as funções locais utilizáveis; repetir após reiniciar e iniciar com Windows na bandeja, sem abrir diálogos inesperados.
- Aceitar aviso final: guardar somente escolha/versão/hash, registrar eventos fictícios permitidos; fechar e conferir um POST/202, pendência removida e contagens no ensaio. Aviso diferente exige nova escolha.
- Minimizar/restaurar repetidamente: zero POST, pares de contagem de bandeja quando consentido; “Sair” tenta uma vez e libera ícone/handle. Não aparecer perfis PC FORTE/PC FRACO.
- 403/429/5xx, DNS/TLS sem confiança, offline e perda de conexão: não seguir redirect, não repetir em laço, manter no máximo uma pendência válida; nova sessão autorizada recupera. Não instalar certificado inseguro ou desativar AVG/SmartScreen.
- Revogar antes/durante envio, inclusive em outra instância: interromper/cancelar; recusa em memória mesmo com falha de disco; status explica persistência falha. Uma resposta já recebida pode constar do agregado.
- Duas instâncias: só uma tentativa simultânea por pasta; lock não bloqueia mudança de consentimento. Pendência nova, inclusive bytes idênticos de outra instância, sobrevive a 202 antigo; instância antiga não recria relatório após revogação.
- Fechar durante trabalho: proteger operação existente; enquanto aguarda telemetria não iniciar instalação/desinstalação/reparo novo. Segundo clique em fechar não inicia outro POST. Resposta atrasada não apaga pendência posterior.
- Windows Shutdown/reiniciar: incluir cancelamento do desligamento (`WM_ENDSESSION` FALSE), retomada do uso e conclusão tardia sem fechar a janela; cancelar envio, não iniciar rede, não abrir modal que impeça desligamento; persistência local de melhor esforço, recuperação só em próxima sessão autorizada. Testar com AVG ativo em VM; não desligar PCs pessoais por automação.
- Pendência >7 dias, timestamp futuro, JSON adulterado, link/junction, arquivo bloqueado: não enviar; nenhuma exclusão fora do local autorizado. Queda de energia/kill pode perder último resumo e não prova ausência de falha.

## 8. Primeiro grupo de avaliadores, após aprovação final

Começar com **3 a 5 adultos de confiança**, por até 48 horas antes de ampliar. Esse número pertence à lista privada de convites do proprietário, não é inferido das contagens técnicas nem inserido no payload. Confirmar versão/Windows por conversa privada somente se necessário, com política própria. Não pedir ao avaliador para instalar SDK, fazer login ou desligar controles de segurança.

1. Entregar pacote exato aprovado, hash, aviso completo e canal privado. Explicar liberdade de recusa/revogação; não condicionar suporte à participação.
2. Primeiro contato orienta somente abrir/usar tarefas normais/fechar, conferir legibilidade/bandeja. Atualização/desinstalação somente por escolha consciente e roteiro aprovado, sem automatizar modificações em PCs de terceiros.
3. Proprietário acompanha saúde, backlog e custos; interromper ampliação diante de atraso de retenção, falhas 5xx repetidas, bloqueio AVG, perda de consentimento ou dado fora do esquema.
4. Sugestões livres vão ao canal privado aprovado e separado, sem upload de arquivos/diagnóstico bruto. Não adicionar texto livre ao receptor.
5. Ao final, conferir retenção, resolver incidentes e decidir ampliação por evidência. Nenhum item do roadmap é homologado fisicamente apenas por contagens ou runner verde.

**Recomendação:** concluir as correções/CI e submeter o pacote operacional A–F à decisão do proprietário. A liberação conectada permanece bloqueada até as provas remotas/AVG e os dados de domínio/contato. Não substituir esses bloqueios por distribuição offline.
