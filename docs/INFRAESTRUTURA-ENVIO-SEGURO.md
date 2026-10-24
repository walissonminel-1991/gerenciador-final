# Infraestrutura de diagnóstico — homologação protegida

**Estado em 20/10/2026:** código de homologação e testes no repositório, sem implantação, domínio público, segredo configurado ou envio no aplicativo. A candidata v5.13.11.8 continua coletando dados somente no computador após consentimento local. Nenhuma automação de IA é ativada por este protótipo.

## Experiência do participante

O participante final **não precisa de GitHub, senha de desenvolvedor nem arquivos .txt**. Na interface atual, `Simular sem enviar` usa dados inventados sem consentimento, arquivo ou rede; `Ativar coleta local` guarda apenas contagens técnicas após aceite; `Não participar / Desativar` revoga a coleta. O futuro botão para envio precisa de **consentimento separado**, destino explicado e confirmação de rede recebida; **não está disponível**.

A transmissão futura deverá ser feita apenas pelo código do GF (JSON gerado internamente), **sem seleção de arquivos, screenshots ou logs**, e nunca por URL com dados sensíveis em query string. Não adicionar segredos, tokens, senha compartilhada ou chave de API no EXE.

## Componentes preparados e isolados

- `worker.mjs`: receptor de produção **desativado**; responde 503 sem receber dados reais.
- `staging-receiver.mjs`: código opcional **somente de homologação**, desligado por padrão; só aceita POST no caminho fixo, `application/json`, até 4 KiB, em forma JSON canônica. Rejeita anexos, campos extras, categorias desconhecidas, chaves duplicadas, conteúdo compactado e requisições adulteradas.
- Homologação exige `STAGING_ENABLED=true`, um `STAGING_TOKEN` **secreto no servidor** com pelo menos 32 caracteres, binding de rate limiting e banco D1 isolado. Sem qualquer um desses elementos, não recebe dados. Esse token **NUNCA** pode ir para versões distribuídas nem ser disponibilizado a usuários.
- A base armazena apenas `day`, `version`, `event`, `total`, `expires_at`. Não guarda corpo original, token, nome de usuário, IP, inventário nem identificador de máquina nas tabelas da aplicação.
- Expiração definida em 7 dias, com limpeza agendada. A retenção efetiva em produção requer configurar e auditar cron, políticas de backups e logs do provedor.
- Os testes via `Request` do Node simulam o envio e a resposta em memória; **não abrem sockets para a internet** nem precisam de conta Cloudflare.

## Procedimento de homologação — sem colocar usuários em risco

1. Executar `node --test prototipos/telemetria-worker/test.mjs prototipos/telemetria-worker/staging-receiver.test.mjs` no CI. Os dados e tokens de teste são fictícios.
2. Executar regressões .NET/Windows no GitHub Actions e conferir a interface (simulação, recusa, cancelamento, DPI, AVG) sem ligar o envio.
3. Se for realmente necessário um teste com servidor privado depois, escolher e provisionar um provedor, domínio de homologação, D1 privado, rate limiter e segredo de servidor. O arquivo `wrangler.staging.toml.example` é **exemplo de configuração**, não é implantação nem contrato com a Cloudflare. Não usar `wrangler.staging.toml.example` diretamente nem publicar `.dev.vars`.
4. Revisar WAF/firewall, limites de rede, logs de acesso, alertas de erro, manutenção e política de exclusão. Cloudflare pode registrar metadados de transporte como IP apesar de não entrarem no JSON nem no D1; não prometer anonimato absoluto.
5. Apenas dados sintéticos podem ser enviados na homologação com token. A credencial exige controle do mantenedor, nunca integração ao GF do usuário.
6. Não liberar funcionalidade real antes de revisão humana, controles de branch, teste AVG e dos dois PCs, avaliação de privacidade/LGPD, consentimento de transmissão e possibilidade de revogar.

## Problema ainda não resolvido: autenticar contribuições anônimas

Um EXE gratuito distribuído publicamente **não consegue manter em segredo uma chave de API embutida**. Portanto, o token do staging não resolve o envio do público e não deve ser reaproveitado. Sem conta GitHub, será necessário um serviço intermediário antiabuso, emissão curta de credenciais/provas e rate limiting por mecanismos adequados; antes da produção, decidir com avaliação de ameaças e acessibilidade qual prova exige menos passos do usuário. Validação de JSON **não autentica a origem** de relatos e não impede envios falsos. A API de rate limiting de Workers permite uma barreira, mas a proteção pública também precisará de limites no edge para evitar sobrecarga. <https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/>

**Cloudflare não é obrigatória**: a arquitetura de recepção pode ser reproduzida em outro provedor/servidor seguro. O que é obrigatório é existir um destino controlado e autenticado, com proteção de abuso, segregação de segredos e política de privacidade.

## Gate de liberação

- [x] Contrato de dados técnicos estrito e testes negativos.
- [x] Simulador local sem consentimento, disco ou rede.
- [x] Receiver de homologação com chave de servidor, fail-closed, limites e base agregada.
- [x] Testes de requisições sintéticas sem rede externa.
- [ ] Ambiente externo provisionado e testado (staging; somente relatos fictícios).
- [ ] Autenticação/antiabuso seguro para participantes sem conta.
- [ ] Consentimento específico para **transmissão**, aviso de privacidade e revogação.
- [ ] Logs, acessos, retenção e limpeza efetivamente auditados no provedor escolhido.
- [ ] Branch `main` protegida; pipelines revisados; assinatura confiável; testes AVG e PCs.
- [ ] Envio real habilitado após aceite humano, com opção de desativar e sem dados de arquivo.

**A presença do código no GitHub não significa que o recebimento está disponível. Não marcar envio/IA como concluídos no roadmap.**
