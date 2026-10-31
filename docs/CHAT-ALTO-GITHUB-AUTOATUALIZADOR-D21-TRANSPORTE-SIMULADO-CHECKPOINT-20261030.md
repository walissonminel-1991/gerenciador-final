# CHAT ALTO — GERENCIADOR FINAL — D2.1: streaming HTTP simulado de bytes inertes

**Referência local:** noite de 30/10/2026 (America/Fortaleza); CI concluída em 31/10/2026 UTC. **Escopo:** experimento de transferência simulada, não autoatualizador operacional.

## 1. Estado obrigatório

- Repositório `walissonminel-1991/gerenciador-final`; única branch autorizada para escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial: `ad3d7140ce771e3268231edc2c3f0a53dbc70932`.
- **SHA técnico D2.1 aprovado:** `2f12aaef08de6216331cd7df2fab524bf62dfce2`.
- [CI Windows 41152528724](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41152528724), job `124392060816`, tentativa 1: `completed/success`; SHA do runner corresponde ao técnico.
- D2 previamente validado: [CI 41151421931](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41151421931), `completed/success`, SHA `4a912555bb73b9e7d65984a2dd4eb41c9815b08b`.
- D1.2: [CI 41150770479](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150770479), sucesso e contraexemplo de rollback integral.

## 2. Código experimental novo

- `prototipos/autoatualizador-seguro/github-d21-simulated-http/d21_simulated_http_lab.py`: reaproveita `verify_d0_tuf` (parser C# D0 + manifesto TUF assinado apenas com **sementes públicas de teste**) e `acquire_and_stage`/releitura de digest do D2.
- `SimulatedResponse`: resposta feita de bytes `BytesIO`, campos HTTP controlados, fragmentação de leitura, interrupção antecipada e falha simulada; nenhuma conexão de rede é realizada.
- `StrictSimulatedHttpSource`: aceita somente a URL canônica da Release **fictícia**, status HTTP 200, sem redirects, cabeçalhos `Content-Type: application/octet-stream` e `Content-Length` único e decimal exato, dentro do limite de teste. Recusa cabeçalhos duplicados sem diferenciar caixa, extras, codificação/transporte inesperado, bytes a menos ou a mais. Não segue URLs e não possui método de request real.
- O writer D2 prepara **somente texto inerte** em `RUNNER_TEMP`, com nome fixo, gravação exclusiva de temporário e SHA-256 revalidado após escrita/renomeação; fora da pasta, um sentinela permaneceu inalterado.
- `.github/workflows/gf-github-d21-simulated-http-lab.yml`: CI Windows 2022 com dependências pinadas, permissões `contents:read`, checkout sem credenciais persistentes e verificação de que as fontes experimentais não entraram no projeto do GF.
- `github-d21-simulated-http/README.md`: escopo e limitações operacionais.

## 3. Resultados

O log do job `124392060816` contém `GF_D21_RESULT`:

```json
{
  "schema":"gf-github-d21-inert-http-simulation/1",
  "status":"passed",
  "positive_inert_streams":3,
  "negative_streams_rejected":37,
  "d0_csharp_reused":true,
  "d1_tuf_test_manifest_verified":true,
  "d2_stage_writer_reused":true,
  "memory_only_http_simulation":true,
  "streaming_length_sha_checked":true,
  "outside_stage_sentinel_preserved":true,
  "actual_http_network_tested":false,
  "github_redirect_flow_operational":false,
  "production_trust_root":false,
  "same_user_toctou_resistance_proven":false,
  "full_history_rollback_resistance_proven":false,
  "real_network_requests":0,
  "real_executable_downloads":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

Os três positivos diferem na fragmentação dos bytes (1, 7 e 4096 bytes por leitura). As 37 negativas simulam redirects 301/302/307/308, status 206/404/429/500, domínio HTTP/HTTPS ou tag divergentes, histórico de redirect, `Content-Length` ou `Content-Type` ausentes, duplicados e conflitantes, comprimentos não numéricos/zero/negativo/sobredimensionados/truncados, corpo HTML ou comprimido, `Transfer-Encoding` inesperado, injeção e excesso de cabeçalhos, conexão cortada, erro de leitura, payload maior/menor/adulterado, fragmentação inválida e adulteração do arquivo após escrita.

A guarda final exige `can_install=false` (via writer D2), `operational_ready=false`, `automatic_recovery_authorized=false` e zero download real.

## 4. Fronteiras de validade da prova

- Esta é a validação de uma **interface fake de HTTP**. Não testa TLS, `HttpClient`, `urllib3`, sockets, HTTP/2, certificados, conexões legítimas a `api.github.com`, API rate limits ou a infraestrutura real de downloads do GitHub.
- O teste rejeita todos os redirecionamentos de propósito. **Essa política ainda não implementa downloads reais de assets GitHub**, que frequentemente passam por destinos temporários ou CDN. Uma política operacional futura deverá controlar destinos/redirecionamentos, tempo e tamanho sem transformar URLs em autoridade de execução, validando bytes contra o manifesto autenticado.
- Chaves de teste derivadas de seeds públicas: não existe autoridade privada de assinatura para produto ou distribuição. A observação de hash da fixture não autentica um pacote executável real.
- O código não prova proteção contra ataques de mesmo usuário ao diretório, ao arquivo ou aos handles entre verificações. Não há prova universal de NTFS/TOCTOU, crash/power loss, durabilidade, execução dos bytes verificados, startup ou READY.
- O contraexemplo D1.2 permanece válido: substituir todo o histórico TUF confiável por cópia antiga permite reaceitação. **H1 continua não comprovado**, juntamente com H2/H3. D2.1 não promove a confiança global.

## 5. Próxima etapa recomendada

**D2.2 — HTTP em loopback Windows com resposta inerte, usando uma biblioteca HTTP real**, com servidor local controlado somente pelo runner e sem qualquer request à internet; testar Content-Length, cortes, erros, limits e recusas de redirect. Isso esclarece a diferença entre um fake de resposta e comportamento real de `HttpClient`/sockets, sem publicar Release ou recuperar arquivos executáveis. Continuar preservando o H1 bloqueado e o isolamento do GF.

Uma auditoria WORK independente só se justifica ao integrar controles de transporte, autenticidade e filesystem de forma efetiva, com SHA e CI fixos; não gastar WORK para repetir fixtures isoladas.

## 6. Invariantes congelados

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas. HOLD é contenção, não recuperação.** Sem mudança em main, PR #65 Draft, BETA .66, Cloudflare, backend, dados pessoais, binário do GF distribuído, chaves privadas, release, merge, deploy ou PCs FORTE/FRACO.
