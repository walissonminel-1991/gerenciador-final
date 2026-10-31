# GERENCIADOR FINAL — CHAT ALTO — D2.3: .NET HttpClient em loopback

**Data de referência:** 30/10/2026 (Fortaleza); CI Windows concluída em 31/10/2026 UTC. **Natureza:** laboratório de HTTP real com bytes INERTES, sem integração ao GF.

## 1. Referências verificáveis

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch de escrita autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `904541eccfcac5a4ec509bd728ada5bc40ad1c49`.
- **SHA técnico final validado D2.3: `94307c43fda04f0ebc0e380494aa785189eb2e40`**.
- [CI Windows 41155201208](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155201208), job `124400352759`, tentativa 1, `completed/success` em SHA idêntico.
- [CI anterior 41155100296](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155100296) `FAILURE` na compilação C#: `SocketsHttpHandler` não possui `UseDefaultCredentials`. Correção: remover essa propriedade e usar `Credentials=null` e `PreAuthenticate=false`, mantendo `UseProxy=false`. A CI subsequente compilou e validou os mesmos critérios de teste.
- D2.2 aprovado em [CI 41153039809](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41153039809), SHA `ec25fac108250cfb7803d36fa61745d0cd225136`, três transferências HTTP/1.1 em loopback Python e 31 recusas.
- D2.1 aprovado em [CI 41152528724](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41152528724), SHA `2f12aaef08de6216331cd7df2fab524bf62dfce2`.

## 2. Trabalho executado

Diretório novo `prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/`:

- `HttpClientLoopback.cs.txt`: código C# que inicia `TcpListener` exclusivamente em `IPAddress.Loopback` e responde HTTP/1.1 por socket TCP real com bytes de texto inertes. O cliente é `System.Net.Http.HttpClient` com `SocketsHttpHandler`, `AllowAutoRedirect=false`, `UseProxy=false`, `UseCookies=false`, `AutomaticDecompression=None`, `Credentials=null`, `PreAuthenticate=false` e `ConnectCallback` com destino estritamente `127.0.0.1` e porta efêmera do próprio servidor.
- A URI da Release GitHub é apenas um **identificador fictício de objeto**, nunca um destino HTTP. A chamada TCP real usa URI construída somente do literal `127.0.0.1` e da porta local controlada.
- O cliente exige HTTP/1.1, status 200, Content-Length exato igual ao esperado, Content-Type octet-stream, ausência de Location, encoding inesperado, Transfer-Encoding chunked ou cabeçalho de resposta não permitido. Processa corpo por `ResponseHeadersRead`, limite de tamanho, timeout e `CancellationToken`.
- O arquivo é escrito em `RUNNER_TEMP` com `FileMode.CreateNew` e `FileOptions.WriteThrough`, SHA-256 calculado durante a transferência e novamente após escrita e renomeação. O arquivo de teste é `inert.fixture.bin`; **não é ZIP, executável, instalador ou pacote de GF**.
- `GF.D23.HttpClientLab.csproj`: projeto .NET 10 independente, `EnableDefaultItems=false`, compilando só `.cs.txt` do laboratório e proibindo publish como produto.
- `README.md`: contrato e limites de confiança.
- `.github/workflows/gf-d23-dotnet-httpclient-lab.yml`: runner descartável Windows 2022, .NET 10 pinado, guarda de isolamento do `GerenciadorAplicativos.csproj`, sem publicação de artefatos, sem credenciais persistentes e permissões GitHub `contents:read`.

Nenhum arquivo do GF distribuído, seletor OLD/NEW, PR #65 Draft, BETA .66, main ou infraestrutura Cloudflare foi alterado.

## 3. Resultado CI

O log contém `GF_D23_RESULT`:

```json
{
  "schema":"gf-d23-dotnet-httpclient-loopback/1",
  "status":"passed",
  "positive_inert_streams":3,
  "negative_cases_rejected":25,
  "loopback_connections_observed":27,
  "http_client":"dotnet10-System.Net.Http.HttpClient",
  "only_numeric_loopback_connection":true,
  "redirects_disabled":true,
  "cancellation_tests_included":true,
  "inert_sha256_readback_verified":true,
  "outside_staging_sentinel_preserved":true,
  "d0_d1_tuf_chain_verified_in_dotnet_lab":false,
  "production_trust_root":false,
  "github_asset_redirect_supported":false,
  "tls_or_real_github_tested":false,
  "same_user_toctou_resistance_proven":false,
  "full_history_rollback_resistance_proven":false,
  "real_executable_downloads":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

Os três positivos variam a fragmentação de envio em 1, 7 e 4096 bytes. As 25 negativas abrangem oito status não permitidos (301, 302, 307, 308, 206, 404, 429, 500), tipo HTML, ausência de comprimento, comprimentos inconsistentes, zero, negativo, textual e acima do limite, respostas codificadas ou chunked, Location inesperada, cabeçalho extra, truncamento, modificação de mesma extensão, cancelamento antes dos cabeçalhos, cancelamento antes do corpo e identidade de Release falsa. Esta última é recusada **antes de estabelecer conexão**, por isso houve 27 aceitações de loopback, não 28. As verificações de status e relatório são exigidas pela CI.

## 4. Limitações que permanecem BLOQUEADAS

1. O SHA-256 esperado é derivado de **bytes INERTES constantes no próprio laboratório**. A D2.3 **não consome manifesto autenticado pelo TUF nem demonstra uma ligação D0/D1→HttpClient**; isso é uma etapa futura. O sucesso da CI não oferece autoridade criptográfica para um pacote real.
2. Não houve comunicação HTTPS/TLS, GitHub, DNS externo, CDN ou download de asset. A política atual **recusa todos os redirects** e não representa a política necessária para GitHub Releases reais.
3. O código não valida de modo universal cabeçalhos duplicados no nível de bytes da resposta: `HttpClient` pode normalizá-los. O teste se concentra em propriedades observáveis e em falhas determinísticas, não anuncia uma garantia inexistente.
4. O arquivo de destino é um laboratório descartável do próprio runner. A proteção contra reparse, junction, concorrência de outro processo same-user e TOCTOU entre verificação e uso permanece sem prova. A revalidação de hash não demonstra autenticidade dos bytes efetivamente executados, nem durabilidade após falha de energia.
5. **H1 continua refutado em laboratório** pelo contraexemplo D1.2 de reversão integral do histórico TUF. H2 e H3 também permanecem sem prova. Nenhum rollback ou recovery foi acionado.
6. O projeto C# não é um downloader operacional, não interage com o GF distribuído, não instala nem inicia binários e não cria ponto de promoção de segurança.

## 5. Próxima etapa recomendada — integração sem duplicar arquiteturas

**D2.4: construir um único contrato experimental entre a decisão de versão D0, os metadados TUF autenticados D1/D1.1 e o transportador .NET D2.3, sempre com manifesto de teste e bytes inertes.** Evitar duas implementações concorrentes de downloader ou mecanismos de recuperação. O WORK pode revisar esse contrato quando existir um fluxo de integração verificável e um SHA congelado. Não usar WORK apenas para repetir estes ensaios independentes.

Qualquer conexão operacional ao GitHub deve exigir raiz de confiança de produção gerada e protegida fora do repositório, política segura de redirecionamento e CDN, contenção de caminhos NTFS/TOCTOU, avaliação de histórico antirrollback e testes de interrupção. A publicação e distribuição permanecem desautorizadas até homologação apropriada.

## 6. Estado congelado

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD é contenção, não recuperação.** Preservar Opção B, B+A portátil, dados pessoais, main, PR #65 Draft, BETA .66 e Cloudflare; sem merge, Release, deploy, instalação ou testes nos PCs FORTE/FRACO.
