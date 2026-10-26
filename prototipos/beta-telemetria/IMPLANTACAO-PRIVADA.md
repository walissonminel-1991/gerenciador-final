# BETA .66 — implantação controlada do receptor (NÃO ATIVAR AINDA)

Status: documento operacional, não evidência de implantação. A PR #61 continua Draft e o fluxo está bloqueado até revisão de LGPD, WAF, ensaio end-to-end e CI completa.

## Modelo de confiança

- O aplicativo só conhece uma URL HTTPS pública, validada no build; **não inclui tokens ou chave do GitHub**.
- A URL não autentica o emissor: qualquer cliente pode simular o mesmo JSON. Logo, as contagens recebidas **não são prova da origem nem número de participantes**. Limite por IP/PoP e WAF reduzem abuso, mas não substituem autenticação verificável. No piloto, conferir plausibilidade antes de qualquer conclusão ou exportação.
- O Worker deve rodar sob zona Cloudflare com WAF no domínio personalizado; **não expor via workers.dev, preview URLs, ou rota alternativa que contorne WAF**.
- D1 guarda só dia, versão, código permitido, total e expiração; nenhuma tabela guarda o IP. A Cloudflare pode processar metadados de conexão, inclusive em logs de rede fora do D1. Revisar Logpush/Workers Logs, retenção e permissões na conta.

## Preparação sem credenciais em arquivos

1. Criar na conta GitHub do controlador **um novo repositório PRIVATE**, separado do `gerenciador-final` público. Verificar que a API GitHub reporta `private: true`; não instalar Apps ou ações externas sem necessidade.
2. Para o piloto, atribuir **somente ao repositório privado** uma credencial com permissão mínima para listar/criar Issues e ler metadados da própria repository. O protótipo atual lê o segredo de `GITHUB_TOKEN` no **servidor**. Se escolher GitHub App (preferível), adaptar/validar a renovação do token de instalação antes de habilitar exportação: esses tokens não devem ser tratados como credenciais permanentes. Nunca registrar token nos logs, TOML, GitHub Actions, URL ou executável.
3. Na pasta `prototipos/beta-telemetria`, copiar `wrangler.toml.example` para `wrangler.toml` **local e não versionado**. Preencher ID do D1, repo privado e namespace de rate limit, mantendo `PILOT_ENABLED = "no"` e `GITHUB_EXPORT_ENABLED = "no"`. Configurar domínio/rota sob WAF. `workers_dev = false` e `preview_urls = false` são intencionais. Revisar se a rota/preview mudou após cada deploy.
4. Inicializar e validar em isolamento:

```sh
node --test test.mjs
npx wrangler d1 create gf-beta-private
npx wrangler d1 execute gf-beta-private --local --file=./schema.sql
```

5. Após auditar identidade e destino do D1 e criar WAF na zona, inicializar D1 remoto e publicar Worker **ainda desligado**, sempre no contexto da conta correta:

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
10. Desligamento de emergência: mudar `PILOT_ENABLED` e `GITHUB_EXPORT_ENABLED` para `no`, verificar deploy e bloqueio 503, e revogar a credencial GitHub no servidor. A expiração D1 ocorre pelo cron diário independentemente do piloto estar ativo.

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
