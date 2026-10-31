# CHAT ALTO — GERENCIADOR FINAL — D2.4: D0 → TUF → HttpClient .NET, laboratório inerte

**Data de referência:** noite de 30/10/2026 em Fortaleza, CI encerrada em 31/10/2026 UTC. **Escopo:** integração experimental com chaves públicas de teste, NÃO um atualizador operacional.

## 1. Referências verificadas

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `535754d18546ac30dbfa0d2b38adbd7103b69a74`.
- **SHA técnico validado: `a7e2a15a571a95ca0ed53124c8c3c2d89a446ee3`**.
- [CI D2.4 41155677811](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155677811), job `124401833809`, tentativa 1, `completed/success`; SHA executado corresponde ao SHA técnico.
- [CI D2.3 regressão 41155563065](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155563065) `completed/success` após refatoração da fonte compartilhada, SHA `ad2a19a14898d58326bc9e5bbd6b2d9dae4d2f34`, 3/3 streamings inertes, 25/25 negativas, 27 conexões locais.
- Primeira tentativa de refatoração [41155550212](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155550212) falhou por erro de nome na assinatura do parâmetro de digest, corrigido antes da regressão válida. Não houve relaxamento dos controles.
- Evidências anteriores: D0 [41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551); D1 [41148601868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148601868); D1.2 [41150770479](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150770479) com contraexemplo H1; D2 [41151421931](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41151421931); D2.2 [41153039809](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41153039809).

## 2. Implementação e ausência de duplicação do transportador

- Fonte existente `prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/HttpClientLoopback.cs.txt`: refatorada para `partial class Program` e um overload de `Stage` que aceita tamanho e digest experimentais, mantendo o modo D2.3 original e todos os testes anteriores. `#if D24_INTEGRATION` ativa a entrada de laboratório somente no novo projeto, sem efeito no build D2.3 comum ou no produto.
- `prototipos/autoatualizador-seguro/github-d24-integrated/GF.D24.InertChainLab.csproj` compila **a mesma fonte D2.3**, mais `D24Handoff.cs.txt` na configuração independente `D24_INTEGRATION`. Não foi criado um segundo transportador HttpClient.
- `D24Handoff.cs.txt`: lê manifesto de laboratório sob `RUNNER_TEMP`, confere hash recebido, campos exatos e não duplicados, schema, repositório, arquitetura, tag, canal, asset, bytes inertes, flags explícitas de teste e versão instalada. Só então inicia servidor local em `127.0.0.1`, executa o transportador D2.3 e verifica conteúdo após a gravação. As negativas de handoff retornam `rejected`, sem conexão local ou artefato final.
- `d24_chain_lab.py`: orquestra a mesma fonte C# do parser D0 e `verify_d0_tuf` de D2, com python-tuf e bootstrap pinado **exclusivamente de teste**. Reutiliza raiz/assinaturas `Stage11` derivadas de seeds públicas; não cria segredo de produção. Somente após a validação positiva chama o modo C# `--d24` com manifesto e SHA-256. Todas as Releases e payloads são **ficções locais**, não objetos baixados do GitHub.
- `.github/workflows/gf-d24-inert-integrated-lab.yml`: CI Windows descartável, dependências fixadas, `contents:read`, sem publicar executáveis; build D0 isolado, build D2.4 incluindo fonte D2.3, testes Python e validação estrita do relatório. Guardas verificam fontes experimentais fora do glob do GF distribuído.
- `README.md`: contrato, fronteira de confiança e contraexemplo.

## 3. Resultados

O log `GF_D24_RESULT` do job aprovado registra:
```json
{
  "schema":"gf-github-d24-inert-integrated/1",
  "status":"passed",
  "orchestrated_d0_tuf_dotnet_inert_staging":true,
  "authenticated_inert_preflight_positive":1,
  "tuf_d0_negative_cases_rejected":12,
  "dotnet_handoff_negative_cases_rejected":14,
  "shared_dotnet_d23_transport_source":true,
  "standalone_handoff_without_tuf_accepted":true,
  "standalone_bypass_is_security_counterexample":true,
  "secure_cross_process_authority_proven":false,
  "same_user_rollback_protection_proven":false,
  "same_user_toctou_resistance_proven":false,
  "public_test_signer_only":true,
  "real_network_requests":0,
  "real_executable_downloads":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

- Fluxo positivo: um manifesto de TESTE autenticamente ligado por Python-TUF à versão do parser C# D0 chega ao transportador C# após o preflight; o arquivo de texto inerte é adquirido por loopback real e revalidado por hash.
- 12 rejeições antes de chamar .NET: tag Release errada, draft, asset com nome/tamanho/URL hostil, manifesto adulterado, Targets alterado, Timestamp expirado/ausente, manifesto assinado com campos reprovados (arquitetura ou repo incorretos), ausência de upgrade.
- 14 rejeições no C#: hash de handoff divergente, manifesto alterado após preflight, versão da candidata ou instalada divergente, arquitetura/repo/asset/hash/tamanho incorretos, flags de teste e de instalação perigosas, campo extra, campo ausente e chave JSON duplicada.

## 4. Contraexemplo independente que mantém a segurança BLOQUEADA

**Um adversário do mesmo usuário pode invocar diretamente o executável de laboratório D2.4 com o manifesto e seu SHA-256 públicos, pulando a etapa Python-TUF, e o componente aceita o payload inerte.** O laboratório exige essa demonstração de forma deliberada: `standalone_handoff_without_tuf_accepted=true`; `secure_cross_process_authority_proven=false`.

Portanto, **não há prova de autorização criptográfica efetiva na fronteira Python→C#**, nem de que uma decisão TUF legítima seja necessária para todo processo de preparação. O hash recebido via argumento protege contra certas adulterações enquanto o argumento é fixado, mas não contra o mesmo usuário que controla ambos os arquivos e argumentos. HOLD continua contenção; esse fluxo não pode ser usado operacionalmente.

D1.2 já provou outra limitação: reversão integral do armazenamento TUF same-user permite aceitar metadados antigos; H1 continua não comprovado. H2/H3, autenticidade dos bytes executados, NTFS/TOCTOU robusto, recuperação transacional, durabilidade e READY também não foram comprovados.

## 5. Próxima decisão e momento adequado para WORK

Recomendo **WORK somente leitura agora**, com SHA congelado `a7e2a15a571a95ca0ed53124c8c3c2d89a446ee3`: tentar refutar garantias da integração e classificar riscos de autorização interprocesso, replay/rollback integral, falsificação de resultado D0/TUF, troca de manifesto após preflight, filesystem e lançamento. Exigir propostas que **evitem duplicar a arquitetura** e priorizem baixo custo/portabilidade, sem permitir novas chaves privadas em GitHub. Esse uso é mais eficiente que repetir auditorias independentes de cada fixture.

Para a implementação seguinte, antes de D3, definir uma arquitetura em que o componente que decide a confiança controle também a concessão efetiva de acesso aos bytes, ou em que o executor realize verificação independente de confiança **sem criar um segundo sistema concorrente**. Alternativas devem ser avaliadas por modelo de ameaça e CIs adversariais. A produção também exigirá raiz privada gerada e protegida fora do repositório, política para redirects GitHub/CDN reais, estado persistente com antirrollback e contenção Windows.

## 6. Estado congelado

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD é contenção, não recuperação.** Nenhum merge, Release, deploy, executável externo, instalação ou alteração em main, PR #65 Draft, BETA .66, Cloudflare ou PCs do usuário.
