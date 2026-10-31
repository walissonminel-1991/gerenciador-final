# GERENCIADOR FINAL — CHAT ALTO — Checkpoint D2.4.2: sessão obrigatória C# → Python-TUF de TESTE → Stage .NET

**Referência local:** 30/10/2026 (America/Fortaleza); CIs executadas em 31/10/2026 UTC. **Status:** correção lógica e integração **de laboratório**, sem homologação operacional.

## 1. Identidade e provas congeladas

- Repositório `walissonminel-1991/gerenciador-final`.
- Branch única de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `89aafd0eb09ca589070b812d6440d68c970f2cc7`.
- **SHA técnico aprovado D2.4.2: `a2e3ec50d5c66a45aeed0f509ba68dbbcc6cfe49`**; [CI 41158584096](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41158584096), job `124410778695`, tentativa 1, `SUCCESS`.
- **HEAD de código+README retestado: `f415e2758975d33811337f8a1932963adad0845f`**; [CI 41158785503](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41158785503), job `124411406457`, tentativa 1, `SUCCESS`. Comparação dos dois SHAs: mudança **exclusivamente em `github-d24-integrated/README.md`**, nenhuma alteração do código funcional ou workflow.
- Regressão D2.3 (fonte compartilhada): [CI 41158219800](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41158219800), `SUCCESS`, SHA `b6703214d2fa20ab9af7e4bb101bdde8b7363423`; mantém as correções D2.4.1 de canários e deadline.
- Regressão do módulo D2 após mudança opcional do parâmetro `bootstrap_bytes`: [CI 41158132840](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41158132840), `SUCCESS`, SHA `5d55f210ab0d2743d7b7602d4b19afda98ea105d`.
- Tentativas intermediárias falharam ou foram canceladas durante a atualização do contrato, incluindo uma execução com workflow antigo que esperava o formato D2.4 anterior. **Somente as CIs verdes identificadas acima fundamentam o resultado final.**

## 2. Mudança arquitetural

**Uma única sessão funcional de laboratório sob controle do coordenador C#:**

1. O antigo `--d24 <manifesto> <hash> <tag> <instalado>` **não é mais uma entrada válida de preparação**. Uma chamada direta antiga retorna erro antes de abrir conexão.
2. A entrada atual `--d242 <diretório-de-fixtures>` inicia uma sessão criada pelo coordenador C#, com identificador aleatório de correlação. O coordenador não recebe hash nem recibo de autenticação do chamador como prova de TUF.
3. O código `d242_tuf_child.py` é incluído como **`EmbeddedResource`** no mesmo projeto C# D2.4; o coordenador o carrega da própria assembly e inicia um processo Python filho por `python -c`, sem caminho de verificador passado em CLI. O interpretador Python e suas bibliotecas, contudo, ainda não têm identidade segura contra substituição pelo mesmo usuário.
4. O filho reutiliza **a mesma** função `verify_d0_tuf` de D2, o parser C# D0 e python-tuf. Faz uma leitura limitada do bootstrap de laboratório, compara SHA-256 com o pin de TESTE e entrega **exatamente o mesmo snapshot de bytes** à biblioteca TUF. Não reabre o bootstrap para o verificador após conferir o hash.
5. Somente após o filho concluir com sucesso, o coordenador exige **uma única saída estruturada**, status do processo, schema exato, sessão correspondente, manifesto de mesma SHA-256 e tags/flags fixas. Reabre o manifesto para obter um snapshot próprio e valida esses bytes em memória antes de passar tamanho/hash inertes ao `Stage`.
6. A preparação usa a **mesma implementação .NET D2.3** com `HttpClient` somente em loopback, preservando a D2.4.1 (não apagar arquivo preexistente por nome e deadline cooperativo). Nenhum segundo downloader ou cliente TUF foi implementado.
7. `--d242-selftest` é um ensaio isolado de parsing de recibo, não chama `Stage`.

## 3. Arquivos da mudança

- `prototipos/autoatualizador-seguro/github-d24-integrated/D24Handoff.cs.txt`: controlador de sessão, verificação de recibos e manifesto, processo Python filho, modo self-test.
- `prototipos/autoatualizador-seguro/github-d24-integrated/d242_tuf_child.py`: validador que reutiliza python-tuf, D0 e D2; bootstrap fixado por snapshot único.
- `prototipos/autoatualizador-seguro/github-d24-integrated/GF.D24.InertChainLab.csproj`: embute o validador Python como recurso; conserva a fonte única de `Stage` D2.3.
- `prototipos/autoatualizador-seguro/github-d24-integrated/d24_chain_lab.py`: matriz de sessões e negativos substitui o ensaio antigo que esperava bypass autorizado.
- `prototipos/autoatualizador-seguro/github-d2-inert/d2_inert_lab.py`: `verify_d0_tuf` aceita `bootstrap_bytes` opcional, mantendo compatibilidade com ensaios antigos; o filho D2.4.2 usa o snapshot conferido.
- `prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/HttpClientLoopback.cs.txt`: condiciona a nova entrada ao build experimental `D24_INTEGRATION` e desativa `--d24`.
- `.github/workflows/gf-d24-inert-integrated-lab.yml`: gate com relatório da sessão C# e 15 negativas, 7 falsificações sintéticas; inclui a dependência D2 alterada.
- `prototipos/autoatualizador-seguro/github-d24-integrated/README.md`: documentação revisada, sem promoção de segurança.

Nenhuma mudança no código distribuído do GF, main, PR #65 Draft, BETA .66, Cloudflare, Releases, credenciais ou PCs físicos.

## 4. Resultados Windows

Ambas as CIs finais `41158584096` e `41158785503` apresentam `GF_D242_RESULT`:

```json
{
  "schema":"gf-d242-csharp-owned-tuf-session/1",
  "status":"passed",
  "positive_inert_sessions":1,
  "negative_sessions_rejected":15,
  "synthetic_receipts_rejected":7,
  "embedded_tuf_validator_source":true,
  "csharp_owns_child_and_stage":true,
  "old_direct_manifest_sha_handoff_rejected":true,
  "bootstrap_single_pinned_snapshot_used":true,
  "shared_dotnet_d23_transport":true,
  "session_replay_rejected":true,
  "secure_against_same_user_process_replacement":false,
  "persistent_tuf_history_rollback_proven":false,
  "same_user_ntfs_toctou_proven":false,
  "test_signer_public_only":true,
  "external_requests":0,
  "downloaded_executables":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

**15 negativos de sessão:** replay da mesma fixture com diretório de trust já iniciado, chamada direta antiga `--d24`, manifesto com whitespace e SHA-256 recalculada sem novo TUF, tag de Release errada, draft, nomes/tamanhos/URL de assets hostis, Targets adulterados, Timestamp expirado ou ausente, arquitetura/repositório incorretos mesmo quando assinados com as sementes públicas de teste, chave JSON duplicada, e interpretador Python indisponível.

**7 recibos sintéticos recusados pelo C#:** sessão diferente, hash diferente, campo extra, declaração `tuf_verified_test_only=false`, sessão ausente, objeto inválido e chave duplicada. Essas verificações do parser **não provam** que um interpretador malicioso com controle de arquivos/processo de mesmo usuário não possa produzir uma saída forjada consistente.

## 5. Restrições de segurança e pendências

- O **bypass lógico da antiga entrada com manifesto+hash** está corrigido no ensaio. Isso não significa que o coordenador seja uma autoridade de segurança resistente ao mesmo usuário: o executável C#, o interpretador Python, seus módulos e as fixtures podem ser substituídos pela própria conta. A sessão aleatória é apenas correlação, não segredo ou assinatura. Não há credencial privada de produção.
- O bootstrap assinado de **TESTE** é autenticado com um snapshot único no verificador filho. A prova não inclui isolamento de memória contra o mesmo usuário, attestation, canal IPC protegido ou garantia de que o próprio filho não foi substituído por outro executável.
- O armazenamento TUF deste ensaio ainda nasce de um bootstrap para cada fixture. D1.2 demonstrou rollback integral do trust store. **H1 segue não comprovado**; H2 e H3 também. Persistência entre processos e NTFS/TOCTOU ficam para D2.4.3, sem realizar recuperação funcional do GF.
- Os testes não estabelecem confiabilidade de downloads HTTPS de GitHub Releases, redirects/CDN, código assinado em produção, durabilidade a crash, instalação ou vínculo dos bytes efetivamente executados. O objeto supostamente nomeado ZIP é **texto inerte**, sem possibilidade de instalação nesta etapa.
- O deadline D2.4.1 é cooperativo nas operações assíncronas; não prova interrupção preemptiva de syscalls NTFS ou recuperação segura de execução inconclusiva.

## 6. Próxima etapa permitida

**Somente D2.4.3**, em rodada posterior, para estado persistente entre processos, replay com trust intacto, contraexemplo de rollback integral e caminhos NTFS conservadores. Preservar a autoridade de sessão C# criada aqui, sem duplicar TUF ou downloader e sem prometer isolamento same-user. Após D2.4.3, considerar D2.4.4 com CI consolidada e WORK independente; **D3 continua bloqueada**.

## 7. Invariantes

**Opção B, Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, zero recuperações automáticas, HOLD somente contenção.** H1/H2/H3 e TOCTOU same-user não comprovados. Sem tocar em GF distribuído, main, PR #65 Draft, BETA .66, Cloudflare, credenciais, dados pessoais, merge, Release ou deploy.
