# Operação e recuperação BETA .66 — proposta, NÃO EXECUTAR EM PRODUÇÃO

Esta missão altera somente fonte, testes, documentos e a PR #62. Não autoriza deploy, SQL remoto, mudanças de flags, Secrets, cron, WAF, endpoint, Issues reais ou distribuição. `0002_github_retention.sql` permanece histórico e imutável; **não reexecutá-lo** em tabelas existentes. O receptor ainda implantado é a versão anterior; nada deste documento comprova validação remota.

## Migração futura do D1

O `schema.sql` atualizado descreve uma instalação nova v3. Não aplicá-lo por cima de v1 como mecanismo de upgrade. `CREATE TABLE IF NOT EXISTS` não acrescenta colunas.

O planejador `schema-migration.mjs` inspeciona tipos, chave primária, índices e contagem de registros inválidos. Produz somente DDL e backfills necessários, suporta v1, v2 e prefixos parciais conhecidos de `0002`, e bloqueia estruturas incompatíveis. Preserva número/ID de Issues, ator, nonce e prazos existentes; não possui DELETE de obrigações. Intenções antigas sem número viram `review_required`. Não muda o prazo de registros que já possuem expiração. As tabelas novas são `ingest_budget` e `github_delete_refs`; o singleton recebe heartbeat e versão de schema.

Os testes executam todo o plano numa transação SQLite real, injetam falha em cada instrução e verificam rollback de DDL e preservação das referências. Um segundo planejamento após sucesso é seguro. Um índice incompatível, nonce duplicado ou dados fora dos limites bloqueiam a operação; nenhuma linha é truncada para contornar o erro. O suporte a SQL transacional em D1 precisa ser comprovado na homologação do ambiente autorizado, além dos testes SQLite.

Ensaio exclusivamente local, sobre **cópia sintética SQLite**, sem credenciais:

```sh
node plan-migration-local.mjs fixture.sqlite
node plan-migration-local.mjs fixture.sqlite --apply-local-fixture
```

O comando não contém transporte Cloudflare, opção `--remote` nem acesso a Secret. O resultado contém fingerprint do schema e instruções SQL, sem conteúdo de linhas. O modo padrão é somente leitura. O modo de aplicação usa `BEGIN IMMEDIATE`/`COMMIT`, com rollback em falha.

Depois de autorização futura específica do proprietário:

1. Confirmar conta/banco e destino originais por identificadores, sem copiar dados de participantes para repositórios, artefatos ou a estação de desenvolvimento. Manter coleta/exportação desligadas e suspender a manutenção antiga sob mudança operacional aprovada.
2. Registrar referência do backup/Time Travel e contagens de linhas/obrigações. Backup com dados tem acesso e retenção próprios: não é arquivo para anexar à PR.
3. Inspecionar o schema real, produzir o plano e conferir fingerprint imediatamente antes da aplicação. Um schema desconhecido exige revisão administrativa; não “corrigir” referências apagando registros.
4. Executar o plano inteiro como transação/batch D1, respeitando os limites do plano contratado. Não executar `ALTER` isolados por tentativa. Validar schema, índices, singleton, contagens e mapeamento de obrigações antes/depois.
5. Se houver erro, verificar rollback. Se uma ferramenta anterior deixou alterações parciais, inspecionar novamente e gerar um plano novo; nunca reaplicar cegamente `0002` ou supor que todo erro implica rollback remoto.
6. Só depois revisar a publicação conjunta de Worker e módulos. Se publicação falhar, manter flags desligadas. Não voltar ao Worker v1 que descartava referências sem excluir Issues.

Uma restauração pode reintroduzir dados vencidos ou perder intenções posteriores ao ponto restaurado. Antes de retomar, registrar o ponto de recuperação, remover agregados vencidos sob a política aprovada e reconciliar todas as Issues do intervalo. Uma Issue com marcador mas sem registro de origem é bloqueada para triagem, não adotada automaticamente. Pausa/restauração/retomada exigem autorização; nenhuma é executada aqui.

## Estados e triagem de obrigações

| Estado | Significado | Automação permitida |
|---|---|---|
| `prepared` | Intenção persistida antes da primeira tentativa | Conservar e buscar prova; não repetir criação nem descartar por tempo, inclusive após restore |
| `uncertain` | A requisição pode ter sido aplicada | Reconciliar por nonce da operação e ator previamente gravados |
| `published` | Número e ID associados à obrigação | Conferir ID/destino e tentar `deleteIssue` quando devido |
| `review_required` | Falta prova suficiente, inclusive legado | Bloquear novas exportações e manter referência para decisão administrativa |
| `deleted` | Confirmação positiva de `deleteIssue` persistida | Conservar prova até terminar a varredura e tratar duplicatas; então retirar somente referência resolvida |

O nonce identifica **uma exportação do servidor**, nunca participante, instalação ou computador. O ID do ator é do executor GitHub, não dos voluntários. Um título ou formato parecido não prova propriedade. Mudança de título com marcador/ator intactos ainda permite recuperar a associação. Remoção do marcador, ator diferente ou ausência de ledger requer triagem.

Duplicatas comprovadas recebem referência por ID em `github_delete_refs` **antes** da exclusão. Resposta perdida ou falha D1 não obriga a reencontrar texto: a próxima execução consulta o alvo por número, confere ID e revalida. Essas referências sobrevivem a falha/alteração do corpo. `issue: null` no repositório de origem não prova exclusão global: transferência ou perda de visibilidade podem produzi-lo. O código retorna `absence_unproven`, mantém a referência e alerta para triagem, inclusive se o DELETE pode ter sido aplicado mas a resposta/tombstone se perdeu. Somente confirmação positiva persistida autoriza aposentar automaticamente a obrigação. Se o alvo voltar a ser verificável com o mesmo ID/destino, a limpeza pode retomá-lo. Não se usa fechamento de Issue como substituto de exclusão definitiva.

Não há endpoint público administrativo ou botão para “ignorar erro”. O proprietário deve investigar de forma privada e autorizada: cruzar o registro da operação, audit trail/atividade disponível no GitHub e metadados imutáveis do alvo; confirmar repositório, ID, número e autoria. Somente depois de prova positiva pode vincular manualmente o alvo à obrigação ou registrar resolução. **Ausência de resultado de busca textual não é prova de que a Issue nunca existiu.** Se não houver prova, a pendência permanece, o piloto não avança e a situação é tratada como incidente de retenção. Não inserir justificativas livres/dados pessoais nas colunas ou alertas.

Antes de qualquer correção manual, preparar alteração SQL transacional vinculada ao snapshot revisado, com verificação de contagens e revisão do proprietário. Nenhum script desta PR aplica resolução administrativa automaticamente ou gera prova de inexistência. Mudanças manuais devem preservar evidência operacional mínima em área privada, com retenção definida pelo controlador.

O limite de criação é 32 registros no ledger, e há teto de 64 referências de duplicatas. Qualquer ambiguidade bloqueia novas criações antes de atingir o limite. Um legado já maior que o teto é preservado e saneado por exclusões confirmadas/triagem; não se descarta o excedente. O Worker não promete exactly-once entre D1/GitHub nem resolve retrospectivamente ausência de registro auditável.

## Monitoramento proposto

`observability.mjs` retorna apenas códigos fixos, quantidades e idades. Não retorna IP, repositório, token, ID de operação, autor, número de Issue, corpo de relatório ou identificação individual. `OBSERVABILITY_LOG_ENABLED` está em `no` no exemplo. O observador independente é somente uma proposta de código/configuração; não foi criado, agendado ou conectado a serviço de alerta.

| Condição | Alerta / ação proposta |
|---|---|
| Cron não observado por mais de 2 horas | Crítico: verificar scheduler/credenciais do ambiente; não inferir execução por haver tráfego HTTP |
| Nenhuma manutenção completa em 4 horas | Warning: checar backlog, scan, quotas e erros |
| 3 falhas consecutivas | Crítico: preservar obrigações, bloquear liberação e investigar código sanitizado |
| Qualquer obrigação vencida | Crítico: acompanhar volume e idade; não esconder atraso eliminando metadata |
| `absence_unproven` (inclusive DELETE com resposta perdida) | Crítico imediato: preservar referência; investigar transferência/visibilidade e confirmação, sem concluir exclusão por `null` |
| Intenção sem prova / candidato não verificável | Crítico: triagem administrativa; não criar outra Issue |
| Lease vencido com token de execução | Investigar Worker interrompido; nova execução retoma após expiração |
| 401/403 de autenticação/permissão | Aviso imediato de acesso; não ampliar escopos automaticamente |
| 14 dias ou menos até expiração prevista | Planejar rotação sob autorização; manter capacidade de exclusão |
| 8 dias ou menos até expiração | Código bloqueia novas exportações; limpeza continua sendo tentada |
| Metadata de expiração ausente/inválida | Bloquear criação e alertar; não tentar ler o conteúdo do token |
| Credencial expirada | Crítico; 401 real mantém referência e backoff, nunca confirma exclusão |

`last_started` acompanha o cron inclusive durante cooldown. `last_success` só avança após manutenção completa; `backlog`, `scanning` e revisão não simulam sucesso. `last_error`, instante e falhas são separados do último resultado. `readHealth` inclui obrigações de duplicatas. O cron do receptor não pode detectar sua própria ausência: exige observador independente ou monitoramento de plataforma, configurados futuramente pelo proprietário.

`health-observer.mjs` realiza apenas SELECT por código. O binding D1 não concede tecnicamente uma conta SQL somente leitura; acesso administrativo, versão do código e implantação desse observador precisam de revisão. Não expor health público. Uma integração externa deve receber apenas o snapshot sanitizado e ter autenticação/retencão próprias, ainda não aprovadas.

## Rotação e recuperação de credencial — somente procedimento

A data **25/11/2026** foi informada pelo proprietário. O código não leu nem verificou o token atual. `GITHUB_TOKEN_EXPIRES_AT` é metadata não secreta e pode estar desatualizada; não é prova criptográfica de validade. A partir de aproximadamente 11/11, o aviso de 14 dias pede planejamento; o gate de oito dias impede criar nova dívida perto do vencimento. Usar margem operacional, sem esperar o último dia.

Após autorização: criar credencial restrita ao mesmo repositório privado e permissões necessárias de Issues/metadados; conferir separadamente papel administrativo e compatibilidade real de `deleteIssue`. Não adicionar escopos amplos ou tornar o repo público para “resolver” uma falha. Se usar GitHub App, validar vida útil/renovação de token de instalação; metadata estática não renova token.

Validar a credencial substituta em ambiente descartável com dados sintéticos, guardar somente como Secret servidor e atualizar metadata de expiração sob mudança aprovada. Conferir o destino, executar manutenção/reconciliação autorizada, comprovar ausência de backlog e só então aposentar a credencial anterior. Não inserir valores em comandos, documentos, PR, histórico do terminal, artefatos ou logs. Nem o observador nem o EXE precisam do Secret GitHub.

Se a credencial expirar/revogar antes da exclusão, manter todas as obrigações, interromper criação, acionar proprietário e restaurar acesso restrito autorizado. Depois retomar exclusões pelos IDs conhecidos e triagem das intenções incertas. Comprometimento exige revogação imediata e resposta a incidente; a interrupção não autoriza apagar o ledger. Exclusão no GitHub não comprova apagamento de notificações, backups ou cópias feitas por administradores.

## WAF, orçamento e estatísticas

O mesmo rate limiter usa chave IP e uma chave comum por PoP, com os limites existentes do binding. Isso reduz carga, mas não é limite global estrito nem autenticação de voluntário. Configurações remotas não foram examinadas/alteradas. Uma regra WAF futura deve cobrir somente host/caminho/método aprovados, limite real do corpo, orçamento global e observação de custo, com todas as rotas alternativas fechadas.

O D1 permite **até 1000 relatórios aceitos por dia UTC**, em transação com as contagens. O ticket aleatório é apenas controle interno da transação e volta a NULL antes do commit; não fica como identificador de participante. Uma falha reverte orçamento e contagens juntos. Depois do teto, a resposta é 429 e não há novas escritas de agregados. Pedidos maliciosos válidos podem consumir o orçamento e leituras; WAF e limites financeiros/da conta continuam necessários. Não afirmar que spam autenticado ou poisoning estatístico foram resolvidos.

- **Eventos:** soma dos valores declarados nas categorias, sujeitos a limites/saturação e dados não autenticados.
- **Relatórios:** requisições aceitas/gravadas; incluem possíveis reenvios após resposta perdida.
- **Participantes:** não estimados; não há base técnica para deduzi-los dessas duas métricas sem outras informações.

Não deduplicar PCs criando IDs permanentes. Supressão abaixo de cinco e arredondamento não equivalem a cinco pessoas nem garantem anonimato. Comparações públicas/roadmap não devem usar essas métricas como prevalência ou número de máquinas.

## Capacidade, prazos e teste real futuro

Lotes conhecidos de até quatro referências, três páginas de 25 Issues, resposta GitHub até 256 KiB, até 30 chamadas GitHub e 38 operações combinadas do trabalho, com reserva para heartbeat/estado/health (teste verifica teto conservador de 50). Timeout de rede 8s, deadline de trabalho 120s e lease de 600s, revalidado com margem antes de mutações. Backoff persistido, `Retry-After` e reset são respeitados. Progresso parcial não muda prazo de retenção.

No regime saudável, a capacidade de limpeza é maior que uma nova exportação por dia, mas indisponibilidade, quotas, backlog legado ou falta de prova podem impedir a exclusão no prazo. Não existe garantia de cumprimento de sete dias durante uma interrupção; alertar, manter a referência e escalar. O cron exemplo é horário; sua instalação real não foi alterada.

Cloudflare Free documenta 10ms de CPU e 50 consultas D1 por invocação; limites de espera/CPU são distintos. A prova de limite de operações e um microbenchmark Node **não homologam CPU no isolate Cloudflare**. Medir CPU p95/p99, memória, custo de leituras/escritas, duração e interrupção no plano exato durante homologação autorizada. Se exceder, reduzir lote ou rever arquitetura/plano sem desabilitar validações. Esta medição é bloqueador de implantação.

## Checklist de homologação — pendente, sem voluntários nesta missão

- [ ] Aprovar controlador, contato, aviso versionado, destinatários, direitos, transferências e retenções externas.
- [ ] Revisar endpoint exato e hash do aviso em `BetaTelemetryPolicy.props`; atualmente lista/hash vazios.
- [ ] Windows descartável, AVG ativo: 720p/1440×900/2560×1440, DPI 100/150/200%, aviso rolável e botões acessíveis, recusa sem perda de funções, bandeja e revogação.
- [ ] Simular offline, 202/403/429/5xx, timeout e revogação durante envio; conferir pendência e fechamento sem duplicar tentativa.
- [ ] Migrar somente clone sintético primeiro; comprovar rollback, repetição e prefixo parcial; depois pedir autorização para D1 remoto.
- [ ] Conferir Worker/DB/bindings/cron/URLs/WAF e plano/custos, mantendo flags `no` e URLs alternativas desligadas.
- [ ] Ensaiar GitHub descartável: criar/excluir agregado sintético, resposta perdida, título/corpo alterado, duplicatas, token expirado e restauração; nunca usar o destino privado real sem autorização.
- [ ] Configurar observador independente e canal sanitizado somente depois da aprovação correspondente.
- [ ] Revisar pacote e proveniência com CI aprovada. AVG/SmartScreen e homologação física não são substituídos por CI.
- [ ] Decidir separadamente sobre implantação, ativação e distribuição; nenhum convite/EXE é autorizado por este checklist.
