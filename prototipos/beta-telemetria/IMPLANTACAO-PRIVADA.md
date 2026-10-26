> **Atualização Rodada 2:** os procedimentos v2 abaixo são históricos. Para migração, recuperação, monitoramento e rotação usar [OPERACAO-RODADA2.md](OPERACAO-RODADA2.md). Não reexecutar `0002` em tabelas existentes. Nenhuma ação remota deste documento está autorizada nesta missão.

# BETA .66 — implantação controlada do receptor (NÃO ATIVAR AINDA)

Status: documento operacional. Segundo informação do proprietário em 26/10/2026, Worker/D1/rate limiter/secret já foram preparados, com ambas as flags em `no`, URLs Production/Preview desligadas e tabelas vazias. Essa configuração remota não foi acessada nem alterada nesta auditoria. Não repetir o provisionamento abaixo em recursos existentes. A PR #61 continua Draft e o fluxo está bloqueado até revisão de LGPD, WAF, ensaio end-to-end e CI completa.

## Modelo de confiança

- O aplicativo só conhece uma URL HTTPS pública, validada no build; **não inclui tokens ou chave do GitHub**.
- A URL não autentica o emissor: qualquer cliente pode simular o mesmo JSON. Logo, as contagens recebidas **não são prova da origem nem número de participantes**. Limite por IP/PoP e WAF reduzem abuso, mas não substituem autenticação verificável. No piloto, conferir plausibilidade antes de qualquer conclusão ou exportação.
- O Worker deve rodar sob zona Cloudflare com WAF no domínio personalizado; **não expor via workers.dev, preview URLs, ou rota alternativa que contorne WAF**.
- D1 guarda só dia, versão, código permitido, total e expiração; nenhuma tabela guarda o IP. A Cloudflare pode processar metadados de conexão, inclusive em logs de rede fora do D1. Revisar Logpush/Workers Logs, retenção e permissões na conta.

## Preparação sem credenciais em arquivos

1. Criar na conta GitHub do controlador **um novo repositório PRIVATE**, separado do `gerenciador-final` público. Verificar que a API GitHub reporta `private: true`; não instalar Apps ou ações externas sem necessidade.
2. Para o piloto, atribuir **somente ao repositório privado** uma credencial com permissão mínima para ler/criar/excluir Issues e ler metadados. A exclusão definitiva usa GraphQL `deleteIssue` e exige também papel administrativo efetivo no destino. O código verifica `isPrivate`, `viewerPermission=ADMIN` e ID estável. Não ampliar permissões para contornar uma falha; comprovar a compatibilidade em ambiente descartável antes de autorizar exportação. O protótipo atual lê o segredo de `GITHUB_TOKEN` no **servidor**. Se escolher GitHub App (preferível), adaptar/validar a renovação do token de instalação antes de habilitar exportação: esses tokens não devem ser tratados como credenciais permanentes. Nunca registrar token nos logs, TOML, GitHub Actions, URL ou executável.
3. Na pasta `prototipos/beta-telemetria`, copiar `wrangler.toml.example` para `wrangler.toml` **local e não versionado**. Preencher ID do D1, repo privado e namespace de rate limit, mantendo `PILOT_ENABLED = "no"` e `GITHUB_EXPORT_ENABLED = "no"`. Configurar domínio/rota sob WAF. `workers_dev = false` e `preview_urls = false` são intencionais. Revisar se a rota/preview mudou após cada deploy.
4. Inicializar e validar em isolamento:

```sh
node --test test.mjs ingest.test.mjs retention.test.mjs
npx wrangler d1 create gf-beta-private
npx wrangler d1 execute gf-beta-private --local --file=./schema.sql
```

5. **Instalação nova apenas:** após auditar identidade e destino do D1 e criar WAF na zona, inicializar D1 remoto e publicar Worker **ainda desligado**, sempre no contexto da conta correta:

```sh
npx wrangler d1 execute gf-beta-private --remote --file=./schema.sql
npx wrangler deploy
```

6. Informar `GITHUB_TOKEN` somente via **secret do Worker**, depois de criar/validar o destino privado (evitar salvar token em histórico de comandos):

```sh
npx wrangler secret put GITHUB_TOKEN
```

7. Verificar WAF e regras de volume ANTES de mudar `PILOT_ENABLED` para `yes`: as chamadas da API de rate limit são aproximadas por localidade, com exemplo de seis tentativas por IP/minuto; implementar limite WAF de borda e controlar origens de tráfego conforme a política. O Worker falha fechado se faltam D1, binding ou flag. Bloquear tráfego aos endpoints de preview. Nunca usar IP como chave persistente de participação.
8. Teste de rede em conta e Windows **descartáveis**, primeiro com exportação GitHub desligada: POST válido = 202, sem IP encaminhado = 403, limite = 429, conteúdo inválido = 415/422, corpo excessivo = 413, D1 indisponível = 503. Simular offline, reset e timeout do cliente Windows; verificar retenção de pendência e ausência de upload após recusa/revogação. Depois testar GitHub privado apenas com `GITHUB_EXPORT_ENABLED = "yes"` e verificar que o Worker se recusa a publicar em repo público.
9. **Só** compilar um EXE BETA apontando para o receptor auditado via `-p:GfBetaEndpoint=https://dominio-validado.example/ingest` depois de concluir os controles e documento LGPD. A URL é um exemplo, não deve ser usada literalmente. Verificar hash SHA-256/manifesto do pacote e ensaiar a versão instalada.
10. Desligamento: interromper coleta e criação de sínteses pelas flags, mantendo a rotina de exclusão. A retenção não depende das flags: preservar o acesso restrito necessário até confirmar que não restam Issues/obrigações. Se houver comprometimento, revogar imediatamente a credencial e tratar a exclusão pendente com outro acesso autorizado; não apagar o ledger para esconder o atraso. Nenhuma dessas ações remotas está autorizada nesta missão.

## Privacidade e aceite — bloqueios jurídicos/operacionais

Antes de habilitar os flags, registrar controlador, contato para titulares, destinatários/suboperadores, base legal, transferência internacional, aviso versionado, prova de consentimento específica, mecanismo de revogação e prazo/método de remoção das Issues privadas. **Repositório privado não é sinônimo de dado anônimo**: em grupo pequeno, combinação de data/versão/categoria pode identificar indiretamente participantes. O protótipo não permite identificar um remetente para apagar um resumo individual já agregado; validar como cumprir direitos dos titulares e explicar limitação com transparência. Não afirmar conformidade LGPD automática.

## Confirmações exigidas na PR

- [ ] CI completa verde no HEAD, inclusive layout e testes BETA
- [ ] Receptor, D1, cron e WAF reais comprovados e URLs alternativas bloqueadas
- [ ] GitHub privado confirmado e credencial servidor-only com expiração/rotação
- [ ] Política de retenção e exclusão privada implementada, controlador/contato aprovados
- [ ] Testes reais de consentimento/recusa/revogação/timeout e envio 202/4xx/5xx
- [ ] Pacote BETA auditado em Windows descartável/AVG ativo
- [ ] Aprovação humana explícita antes de convidar voluntários

Referências técnicas: https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/ · https://developers.cloudflare.com/d1/wrangler-commands/ · https://developers.cloudflare.com/workers/configuration/routing/workers-dev/ · https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados-1/direito-dos-titulares


## Retenção v2 — proposta em código, sem implantação nesta auditoria

- D1 e Issues têm vencimento em `(dia_UTC_da_coleta + 7) * 86400`. Exportar tarde ou repetir uma tentativa não renova esse prazo. A exclusão física ocorre no próximo cron bem-sucedido; falhas, quotas ou indisponibilidade podem atrasá-la.
- O exemplo Wrangler propõe cron horário. Não é evidência de mudança do cron ativo. Se o ativo ainda for diário, o atraso normal pode chegar a 24 horas, além de falhas. Revisar a cadência antes de qualquer ativação futura.
- `github_exports` conserva a referência da Issue até confirmação de `deleteIssue` ou ausência autenticada sem erros. Não há mais descarte cego de metadados. Pendências de exclusão podem exceder o prazo; seus metadados mínimos são mantidos para recuperação, sem contagens ou relatórios individuais.
- O Worker pagina Issues abertas/fechadas, recupera o formato v1/v2 exato, conserva cursor em varreduras longas e não cria sínteses antes de concluir a reconciliação. ID do destino fica fixado no estado D1; mudança de destino bloqueia mutações.
- Token restrito sem papel administrativo suficiente bloqueia exportação e exclusão. Não foi examinado o secret atual. Não confundir Issues `write` com comprovação de que `deleteIssue` funciona nesse contexto.
- Lotes de exclusão têm prioridade, até 8 referências conhecidas por execução. Limites: 30 chamadas GitHub, 44 operações combinadas SQL/API mais reserva de controle (máximo conservador 50), 3 páginas de 100, 120 segundos de trabalho, timeout de rede de 8 segundos e intervalo mínimo de um segundo entre mutações. Lease D1 de 10 minutos impede concorrência; crons seguintes retomam o restante.
- `Retry-After` (segundos/data), `x-ratelimit-reset` e erros GraphQL HTTP 200 suspendem tentativas. Recuo exponencial persistido de 1 minuto até 6 horas; um cron horário só retoma na primeira execução após o prazo. Alertas contêm código sanitizado, sem mensagem remota, corpo ou credencial.
- Intento D1 é gravado antes de criar a Issue. Resposta perdida/falha D1 posterior é reconciliada por marcação e paginação antes de repetir; aguarda-se pelo menos 10 minutos para repetir criação incerta. Não há transação distribuída/exactly-once entre GitHub e D1. Duplicatas reconhecidas são removidas.
- Totais menores que 5 são omitidos e os demais arredondados para baixo em múltiplos de 5. Isso atenua detalhes, mas **não mede participantes nem garante anonimato**.

### Migração de um D1 existente (procedimento futuro)

Nesta missão executar apenas a simulação SQLite dos testes. Não usar `schema.sql` v2 como migração de tabelas v1: `CREATE TABLE IF NOT EXISTS` não adiciona colunas. Não houve migração remota.

Após revisão e autorização específica futura: confirmar conta/banco/destino originais, registrar contagens antes/depois sem exportar dados, aplicar **uma vez** `migrations/0002_github_retention.sql`, verificar novas colunas e singleton de manutenção e só então publicar o conjunto Worker + módulos. A migração preserva todas as referências existentes e calcula expiração a partir de `day`. Se o ledger antigo já foi perdido, a varredura recupera apenas Issues com conteúdo reconhecível; material manualmente adulterado exige triagem privada autorizada. Não restaurar o Worker v1 que elimina referências sem excluir Issues.

### Monitoramento proposto antes do piloto

Sem ler payloads, acompanhar `last_code`, `last_success`, `failures`, `retry_at`, lease e número de obrigações vencidas. Alertar se o cron não executar por duas cadências, se houver falhas/permissões inválidas ou pendências vencidas que não diminuem. Isso é um requisito operacional pendente: esta PR não cria alarmes remotos. Um `cooldown` não prova que o backlog foi eliminado.

Após eventual restauração de backup/Time Travel, manter a coleta/exportação desligada, apagar contagens já vencidas e reconciliar Issues antes de retomar. Uma exclusão de Issue não comprova apagamento de backups do provedor, notificações por e-mail ou cópias feitas por colaboradores. Restringir colaboradores/Apps e notificações no destino, e aprovar também esses prazos com o controlador.
