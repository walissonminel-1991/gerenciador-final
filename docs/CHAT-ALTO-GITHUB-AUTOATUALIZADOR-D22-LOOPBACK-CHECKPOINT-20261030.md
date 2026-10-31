# GERENCIADOR FINAL — CHAT ALTO — D2.2: HTTP real restrito a loopback

**Data local de referência:** 30/10/2026 (Fortaleza); CI concluída em 31/10/2026 UTC. **Natureza:** laboratório Windows descartável; não integra nem autoriza autoatualização do GF.

## 1. Estado e provas verificadas

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `fde67ba346a5fa006f00aabde140419223fb8815`.
- **SHA técnico D2.2 validado: `ec25fac108250cfb7803d36fa61745d0cd225136`**.
- [CI Windows 41153039809](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41153039809), job `124393674898`, tentativa 1, `completed/success`; `head_sha` igual ao SHA técnico.
- D2.1: [CI 41152528724](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41152528724), SUCCESS, SHA `2f12aaef08de6216331cd7df2fab524bf62dfce2`, 3 positivos e 37 negativos de HTTP simulado.
- D2: [CI 41151421931](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41151421931), SUCCESS, SHA `4a912555bb73b9e7d65984a2dd4eb41c9815b08b`, 2 preparações de bytes inertes e 21 contraprovas.
- D1.2: [CI 41150770479](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150770479), SUCCESS com **contraexemplo de rollback integral de histórico**; H1 permanece não comprovado.

## 2. Incremento realizado

Arquivos novos, sem tocar o GF distribuído:

- `prototipos/autoatualizador-seguro/github-d22-loopback/d22_loopback_http_lab.py`: servidor `ThreadingHTTPServer` efetivamente ligado somente a `127.0.0.1` e cliente `http.client.HTTPConnection` com destino literal `127.0.0.1` e timeout de 0,6 s. Não utiliza proxies, resolução de URL remota ou fluxo de redirect. Há apenas um GET por cenário para `/inert-only`.
- `prototipos/autoatualizador-seguro/github-d22-loopback/README.md`: contrato, natureza do transporte e limitações.
- `.github/workflows/gf-github-d22-loopback-lab.yml`: CI Windows 2022 com SDK .NET 10/Python 3.13 e dependências TUF fixadas, `contents:read`, checkout sem credenciais persistentes; guarda de fontes do protótipo fora da compilação do projeto principal.

A candidata de Release é um JSON fictício interpretado pelo **mesmo parser C# D0**; manifesto D1 é verificado por `verify_d0_tuf` e python-tuf com bootstrap e signatários **públicos de TESTE** do laboratório histórico; o componente D2 `acquire_and_stage` recebe os bytes inertes vindos do HTTP local, confere o tamanho e SHA-256, grava em pasta temporária sob `RUNNER_TEMP` e relê o arquivo antes e depois da renomeação. O sentinela fora da pasta de preparo permaneceu intacto.

A URL de GitHub Releases é somente **identidade lógica de asset** na fixture. **Não há request ao domínio GitHub**: a origem física é o servidor TCP loopback, explicitamente diferente da URL consultiva. A existência de uma URL não concede autoridade para executar, instalar ou confiar em bytes de um pacote real.

## 3. Resultado da CI

Log `GF_D22_RESULT`:

```json
{
  "schema":"gf-github-d22-loopback-http/1",
  "status":"passed",
  "positive_inert_http_transfers":3,
  "negative_http_cases_rejected":31,
  "loopback_requests":34,
  "transport":"tcp-ipv4-loopback-only",
  "http_library":"python-stdlib-http.client",
  "http_real_loopback_exercised":true,
  "d0_d1_test_manifest_verified":true,
  "d2_writer_reused":true,
  "outside_stage_sentinel_preserved":true,
  "external_http_requests":0,
  "github_redirect_support_proven":false,
  "tls_or_github_real_tested":false,
  "production_trust_root":false,
  "same_user_toctou_resistance_proven":false,
  "full_history_rollback_resistance_proven":false,
  "real_executable_downloads":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

**Três positivos:** respostas HTTP/1.1 locais com corpo inerte transferido pelo servidor em fragmentos de 1, 7 e 4096 bytes, com gravação e digest conferidos.

**31 recusas:** redirects 301/302/307/308, status 206/404/429/500, Content-Length ausente/duplicado/com capitalização alternativa/conflitante/curto/longo/zero/negativo/textual/com zero à esquerda/acima do orçamento; Content-Type ausente/duplicado/incompatível; Content-Encoding inesperado, Transfer-Encoding inesperado, Location em 200, cabeçalho extra, corpo truncado ou adulterado, encerramento antecipado do socket, timeout e alteração no arquivo após a escrita.

Todas as respostas tinham origem no servidor de loopback controlado pela CI. O resultado `external_http_requests=0` descreve o escopo do laboratório, cujo cliente tem destino literal fixo; não constitui monitoramento independente de todo tráfego da máquina.

## 4. Limitações que não podem ser promovidas

1. O socket HTTP de loopback é real, mas a comunicação é **HTTP sem TLS**, sem conexão com GitHub, CDN, DNS, proxy ou assets oficiais. A política atual recusa todos os redirects; **não é ainda um downloader operacional de GitHub Releases**, que usam redirecionamentos legítimos em muitos fluxos.
2. O manifest é assinado apenas por chaves de teste cujas sementes privadas estão publicadas. **Não existe raiz de confiança de produção nem sigilo dessas chaves**.
3. O retorno de `Content-Length` e a releitura do SHA-256 são observações de laboratório. O código **não prova proteção universal contra concorrência de mesmo usuário, reparse points, handles NTFS e TOCTOU**, nem durabilidade por queda de energia ou execução dos bytes efetivamente validados.
4. **H1 continua com contraexemplo válido:** rollback de todo o histórico local TUF após v2 reabilita aceitação de v1 no laboratório. A D2.2 não resolve H1, H2 ou H3.
5. Nenhum executável foi obtido ou executado, nenhuma instalação ocorreu, nenhuma Release foi criada e nenhuma alteração no GF distribuído, main, PR #65 Draft, BETA .66, Cloudflare ou infraestrutura privada foi autorizada ou realizada.

## 5. Próximo incremento recomendado

**D2.3 opcional, ainda apenas laboratório:** exercitar uma implementação .NET `HttpClient` sobre loopback usando bytes inertes, com `AllowAutoRedirect=false`, limites, cancelamento e URI de origem fixa; distinguir explicitamente política real de download de assets do GitHub (redirecionamento/CDN) do fake local. Antes de qualquer downloader operacional, definir trust root de produção protegida fora do repositório, proteção antirrollback conforme modelo de ameaça e segurança de caminho/handles no Windows. Não utilizar WORK para repetição de fixtures; reservar auditoria independente para integração de transporte, autenticidade e transação.

## 6. Invariantes

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD é contenção.** Preservar Opção B, B+A portátil, histórico TUF/NTFS/TOCTOU e dados pessoais. Sem merge, release, deploy, instalação, publicação, ativação remota ou testes em PCs FORTE/FRACO.
