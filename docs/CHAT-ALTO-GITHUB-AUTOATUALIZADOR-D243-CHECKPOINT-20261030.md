# GERENCIADOR FINAL — CHAT ALTO — D2.4.3: confiança persistente e contenção NTFS

**Referência local:** 30/10/2026 em Fortaleza; ações do runner registradas em 31/10/2026 UTC. **Escopo:** exclusivamente laboratório Windows descartável. Não é atualizador operacional, não autoriza preparo do GF distribuído, instalação, recuperação ou promoção.

## 1. Base verificável e CIs

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `d61332f0f588179914d7d65a564eb53fb3369bdc`.
- **SHA técnico final D2.4.3: `cbcfa630dd79e160807023eccbc6b5a6fe571462`.**
- [CI específica D2.4.3 41161161598](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41161161598), job `124418802396`, tentativa 1, `completed/success`, `head_sha` igual ao SHA técnico.
- [Regressão integrada D2.4.2 41161161924](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41161161924), `completed/success`, no **mesmo** SHA técnico.
- [Regressão prévia D2.3 41159663798](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41159663798), `completed/success`, SHA `3177d63d54a4849aa2c7283427f03a27174b1e6b`, após a adição dos roteamentos D2.4.3; não substitui um teste de integração de produção.
- Documento de decisão: `docs/CHAT-ALTO-D24-WORK-DECISAO-CORRETIVA-20261030.md`; checkpoint predecessor `docs/CHAT-ALTO-GITHUB-AUTOATUALIZADOR-D242-CHECKPOINT-20261030.md`.

## 2. Componentes e reuse

Foram modificados/criados apenas **sete arquivos experimentais**:

1. `prototipos/autoatualizador-seguro/github-d24-integrated/d243_persistent_child.py`: filho Python embutido como fonte de laboratório, que reutiliza `d11_persistent_lab.observe`, o cliente python-tuf e o parser C# D0 real; não cria outro cliente TUF. Reutiliza bootstrap público de TESTE com hash pinado, sem segredos privados. Lê papéis e manifesto inertes, observa trust persistido e produz somente recibo de observação, nunca capacidade de instalação.
2. `prototipos/autoatualizador-seguro/github-d24-integrated/D243Coordinator.cs.txt`: modos C# `--d243` e `--d243-probe` de **observação sem Stage**; serialização cooperativa via mutex local; comparação de digest/recibo da sessão e limites de pasta; leitura NTFS com handle, file ID e SHA-256 antes/depois. HOLD diante de estado ou identidade inconclusivos.
3. `prototipos/autoatualizador-seguro/github-d24-integrated/d243_persistent_lab.py`: suíte Windows de duas gerações, replay com histórico íntegro, rewind integral, concorrência, cache parcial e seis observações NTFS reais.
4. `prototipos/autoatualizador-seguro/github-d24-integrated/GF.D24.InertChainLab.csproj`: embute filho D2.4.3 como recurso da **mesma** assembly e compila observador C# adicional; continua usando única fonte D2.3 para transporte nos modos anteriores.
5. `prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/HttpClientLoopback.cs.txt`: apenas roteamento `--d243`/`--d243-probe` no build experimental `D24_INTEGRATION`. Não alterou o estágio/gravador já validado na D2.4.1.
6. `prototipos/autoatualizador-seguro/github-d24-integrated/D243-README.md`: limites do novo observador.
7. `.github/workflows/gf-d243-persistent-ntfs-lab.yml`: GitHub Actions Windows 2022 descartável, .NET 10, Python 3.13, dependências TUF pinadas, `contents:read`, sem credenciais persistentes, sem upload de executáveis. Verifica resultado fechado com rollback H1 ainda vulnerável.

Nada foi alterado no projeto principal distribuído, main, PR #65 Draft, BETA .66, Cloudflare, credenciais, usuários reais, seleção OLD/NEW, Release, deploy ou merge.

## 3. Provas da CI D2.4.3

Relatório do job `124418802396`:

```json
{
  "schema": "gf-d243-persistent-ntfs-lab/1",
  "status": "passed",
  "csharp_controlled_test_tuf_generations": 2,
  "independent_windows_coordinator_processes": true,
  "observed_trust_role_generations": [1, 2],
  "intact_history_replay_blocked": true,
  "cooperative_concurrency_denied": true,
  "partial_history_held_without_cleanup": true,
  "full_history_rewind_reaccepted_old_version": true,
  "full_history_rewind_is_security_counterexample": true,
  "ntfs_observation_cases": 6,
  "reparse_junction_hardlink_and_race_tested": true,
  "external_sentinel_preserved": true,
  "same_user_rollback_resistance_proven": false,
  "same_user_ntfs_toctou_resistance_proven": false,
  "crash_durability_proven": false,
  "production_signing_authority": false,
  "operational_ready": false,
  "automatic_recovery_authorized": false,
  "real_external_network_requests": 0,
  "real_executable_downloads": 0,
  "installations": 0,
  "product_modified": false,
  "staging_or_recovery_performed": false
}
```

**Histórico TUF:** subprocessos Windows independentes, iniciados pelo coordenador C#, validaram v1 (papéis `[1,1,1,1]`) e v2 (papéis `[1,2,2,2]`) com o mesmo armazenamento. Reapresentação de v1 contra v2 íntegro foi **recusada**. Cache parcial foi preservado e colocado em HOLD, sem bootstrap novo implícito. Concorrência entre coordenadores foi recusada com mutex cooperativo, nunca tratado como exclusão de adversário do mesmo usuário.

**Contraexemplo H1 obrigatório:** uma cópia integral do histórico v1, salva anteriormente, substituiu o cache v2; o coordenador voltou a aceitar v1. Assim, **rollback integral do trust store continua viável no modelo same-user**. A CI exige esse resultado como evidência da lacuna, não como propriedade de segurança. Não existe fonte externa imutável ou persistência antirrollback com durabilidade demonstrada.

**Seis observações NTFS:** handle estável do arquivo inerte; hardlink com link count elevado; junction ancestral real; symlink de arquivo; substituição do nome enquanto handle antigo permanece aberto; modificação de conteúdo sob o mesmo ID. Nas observações suspeitas, `unknown_hold`, sem exclusão automática. Sentinela externa de teste preservada; nenhum arquivo pessoal foi tocado.

## 4. Falhas e correções instrumentadas

- **CI 41159814395 / 41159896377 (FAILURE):** código C# não compilava inicialmente por variável não utilizada. Removida.
- **CI 41159896377 (FAILURE):** liberação de `Mutex` em thread diferente da adquirente após `await`. Corrigida a seção protegida para manter a mesma thread durante aquisição/espera/liberação. Um mutex Windows é thread-affine; ele não garante proteção entre processos maliciosos do mesmo usuário.
- **CI 41160111067 e ensaios posteriores (FAILURE):** a transição TUF v1→v2 era conservadoramente bloqueada por `root.json` ser um link simbólico. Inspeção no runner confirmou que **python-tuf efetivamente cria `root.json` → `root_history\\1.root.json`**, com histórico regular interno e tamanho observado de 2306 bytes.
- **Correção delimitada:** o filho admite somente esse formato de link **interno, relativo e de destino pinado** `root_history/1.root.json`; o diretório de histórico e seu destino devem ser regulares/não links, a resolução deve permanecer sob trust. Qualquer outro link continua HOLD. Não há permissão genérica para seguir symlinks, junctions ou links externos.
- **CI final:** após essas correções, D2.4.3 e regressão D2.4.2 foram ambas SUCCESS no SHA exato `cbcfa630...`.

## 5. Afirmações que permanecem proibidas

- A verificação de ancestrais, tipo, symlink e file IDs tem janelas entre `check` e `use`. O link interno de root é confiado **apenas para formato observado de fixture**, não prova segurança contra substituição concorrente same-user. **TOCTOU NTFS same-user permanece não comprovado.**
- O mutex de laboratório e diretório TUF de usuário não oferecem proteção antirrollback nem bloqueio seguro contra execução de código com a mesma conta. O lock abandonado é contido em HOLD, não tratado como autorização de retomada.
- Nenhuma prova de persistência durável contra crash/power loss, autenticidade de processos externos, proteção de memória, chaves de produção, TLS GitHub/CDN, assinatura operacional, instalação, entrega de bytes executados ou recuperação transacional.
- Os modos D2.4.3 são de observação e **nunca chamam `Stage`**. Mesmo observações válidas permanecem BLOQUEADAS para preparo/instalação; `operational_ready=false`, `G1–G8 BLOCKED 0/8`.

## 6. Continuação permitida, sem antecipação

A etapa futura **D2.4.4** pode consolidar gates de CI, dependências que disparam regressão, evidências adversariais e uma auditoria WORK independente após autorização do mantenedor. **Não foi iniciada nesta rodada.** D3 continua bloqueada. Não substituir esta etapa por implementação operacional ou recuperação.

**Estado congelado:** Opção B; **Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD é somente contenção.** H1/H2/H3 e resistência TOCTOU same-user não comprovados. Sem alterações em GF distribuído, main, PR #65 Draft, BETA .66, Cloudflare, releases, merge, deploy ou instalações.
