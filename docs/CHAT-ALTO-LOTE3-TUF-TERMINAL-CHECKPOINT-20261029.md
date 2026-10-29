# GERENCIADOR FINAL — CHAT ALTO — Lote 3: TUF assinado + checkpoint terminal inerte
**29/10/2026 · checkpoint pós-auditoria WORK · G1–G8 BLOCKED (0/8 operacionais)**

## 1. Estado e evidência

- Repositório `walissonminel-1991/gerenciador-final`, branch `experimento-g5-ipc-chat-alto-20261029`.
- **HEAD inicial confirmado** `4eed23cbd01210ec7e3fa06d18f019e99ec70498`.
- **Contrato anterior ao código** `docs/CHAT-ALTO-LOTE3-CONTRATO-TUF-TERMINAL-20261029.md`, commit `7a3becfac417d17b57eb23963e90b59839f0d4c3`.
- **HEAD de código aprovado** `b60028bc05eab113c22bf659a63f5540400ce4e7`.
- **CI Windows 2022**: [40918988836](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40918988836), `SUCCESS`; **9/9 PASS**, 0 FAIL, 0 warnings, 0 errors, SDK .NET `10.0.402`.
- Comparar diff desde o HEAD inicial e conferir branch/proteções após este checkpoint. Nenhum WORK adicional utilizado.
- PR #65 `Draft`, branch `work-integracao-final-20261027`, HEAD esperado `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; `main`, BETA .66, Worker/D1, telemetria e domínio não podem ser modificados.

## 2. Novidade demonstrada versus Lote 2

**Lote 2 (N1/N2)** ligava `TrustView.Digest` aos diários Stage9/Stage2, com `begin/finish` de confiança Stage11, mas não exigia cadeia TUF completa nem alvo autenticado. Um terminal local coerente era suficiente no modelo de extensão, sem fonte externa que autenticasse seus bytes.

O **Lote 3** adicionou duas provas distintas:

### 2.1 Metadados TUF autenticados e alvo inerte exato

- Usa `DurableTufAttempt.Get`/ `DurableEpochCache` **originais da Etapa 11**, sob a mesma fonte TUF .NET fixada do Stage8, patch do próprio projeto e raiz pública de teste já existente.
- `stage11/make_fixtures.py` gera apenas metadados assinados com seeds de teste preexistentes em diretório descartável do runner. Não gera chaves de produção.
- O `TufClient` verifica assinaturas e referências durante `GetTargetAsync`; `TrustModel.Apply` persiste `root/timestamp/snapshot/targets` e verifica cadeia/assinaturas. A nova `Stage3TufBinding.Bind` requer a `TrustView` completa, idêntica à view atual, valida versões e os digests por papel, referências `timestamp→snapshot` e `snapshot→targets`, `targets→fixture-package.zip`, ZIP e conteúdo `bundle.fixture.txt` + manifesto.
- O candidato é um **arquivo inerte GF-U9 test-only** criado do ZIP após validação; a identidade NTFS do bundle e seu SHA são fixados no `BundleRef`, ligado ao `OperationJournal` e ao `intent` Stage2 sem selecionar o candidato.
- `Stage3TufTarget` inclui todos os digests e `CompositeDigest` como **projeção diagnóstica**. **Essa projeção não foi incorporada em um recibo assinado e atomicamente comprometido por G1**. Uma `TrustView` autenticada localmente não é por si só âncora antirollback.

### 2.2 Terminal G1 distinto de intent

- `Stage3TerminalMemoryAnchor` mantém, somente em memória, `IntentDigest`/ `TxId`, `TerminalDigest`/ `Outcome`, `PreviousCommitDigest`, `WitnessSequence` e digest de reivindicação terminal.
- Para terminal inerte, lê a cadeia Stage2 existente e verifica `intent → rollback_intent → rolled_back`, os mesmos `TxId`, instalação e seletor antigo. **Não usa seleção/confirmed**.
- Um compromisso de intent isolado não autoriza inferir terminal. `Inspect` exige que terminal exato tenha compromisso separado **no mesmo objeto em memória**. A mesma reivindicação é idempotente no modelo; corpo/outcome diferente na mesma transação falha.
- Modificação de bytes terminais que mantém JSON local interpretável altera o digest. A memória testemunha o digest anterior e recusa. A perda total da memória é tratada como **ausência de confiança**, não TOFU.
- **Não há autenticação criptográfica de recibos, relógio/armazenamento persistente de testemunha, enrolamento real nem resistência a snapshot conjunto de disco+witness**. Reinstanciar um modelo vazio não é equivalente a recuperar compromisso externo; não demonstramos persistência pós-reboot. Um operador malicioso pode controlar a testemunha em memória. Este teste não escolhe serviço ou tecnologia operacional.

## 3. Matriz de 9 casos inéditos da CI

| ID | Resultado esperado e observado |
|---|---|
| `L3-TUF-01` | Refresh Stage11 assinado `root/timestamp/snapshot/targets`, vinculado a ZIP, manifesto e bundle inerte: vínculo diagnóstico íntegro |
| `L3-TUF-02` | Remover `targets` da view: `L3_TUF_VIEW_FORK_OR_INCOMPLETE` |
| `L3-TUF-03` | Substituir papel `targets` por bytes assinados da fixture `release3` sem refresh legítimo: `L3_TUF_VIEW_FORK_OR_INCOMPLETE` |
| `L3-TUF-04` | Alterar bytes do ZIP mantendo metadados anteriores: `L3_TUF_ZIP_REF_MISMATCH` |
| `L3-TUF-05` | Apresentar bundle diferente da referência de `targets`: `L3_TUF_CANDIDATE_NOT_IN_SIGNED_TARGETS` |
| `L3-TERM-01` | Intent já comprometido em modelo; terminal local `rolled_back` sem segundo compromisso: `L3_TERMINAL_NOT_EXTERNALLY_ANCHORED` |
| `L3-TERM-02` | Compromisso terminal inerte igual publicado duas vezes: mesmo recibo em memória; resultado `BoundButNotAuthorized` |
| `L3-TERM-03` | Adicionar whitespace JSON válido ao último registro Stage2, sem alterar sua semântica; o digest local muda, `Inspect` recusa e republish contraditório falha no modelo |
| `L3-TERM-04` | Perder objeto âncora de memória após terminal: nova instância sem registros recusa adotar recibo local como prova de confiança |

`GF_L3_FINAL_REPORT` foi **inspecionado no log da CI**: `passed=9`, `failed=0`, flags para vínculo TUF e negativos `true`; `selector_never_selected=true`. Permanecem `false`: `terminal_external_authenticity_proven`, `durable_remote_witness_exists`, `receipt_cryptographically_authenticated`, `authorized_to_select`, `authorized_to_confirm`, `physical_power_loss_tested`, `product_built`, `product_executed`; `operational_gates_closed=0`. A CI exige essas condições.

## 4. Arquivos criados

- `prototipos/autoatualizador-seguro/m3-composed-3/Stage3Binding.cs.txt` — binding TUF de quatro papéis para ZIP/manifest/candidato e segundo compromisso terminal em memória
- `prototipos/autoatualizador-seguro/m3-composed-3/Stage3Tests.cs.txt` — nove testes dirigidos
- `prototipos/autoatualizador-seguro/m3-composed-3/Stage3.csproj` — somente fixture .NET/NTFS com fonte TUF pinada
- `prototipos/autoatualizador-seguro/m3-composed-3/generate_signed_fixtures.py` — geração de vetores públicos de teste preexistentes em `RUNNER_TEMP`
- `.github/workflows/autoatualizador-m3-l3-tuf-terminal-isolado.yml` — CI Windows específica, sem execução do produto
- `docs/CHAT-ALTO-LOTE3-CONTRATO-TUF-TERMINAL-20261029.md` e este checkpoint

Nenhuma alteração foi autorizada a `FixtureSession.Begin/Confirm`, `TrustIntegratedPipeline`, `IntegratedPipeline`, arquivos históricos do updater, `GerenciadorAplicativos.csproj` ou `main`.

## 5. Bloqueios remanescentes e nova prioridade

1. **A projeção `Stage3TufTarget` precisa participar dos mesmos bytes autenticados/comprometidos de um protocolo G1 real** (sem duplo significado de `TUF digest` e `candidate`). Hoje só há binding diagnóstico e modelo de intent independente.
2. **Dois compromissos externos exigem especificação transacional**, CAS monotônico/idempotência durável, rotação/revogação, identidade de instalação provisionada sem TOFU e recuperação crash/timeout entre o compromisso de intent e o terminal.
3. **A autenticidade do terminal depende de identidade de processo e READY G5/G6 reais**; apenas `rolled_back` foi analisado. `selected/confirmed` permanecem barrados.
4. **G8**: prazo único e expiração não cancelam syscall síncrona; resultado tardio/energia/armazenamento persistem pendentes.
5. **G2**: ainda faltam qualificação externa do perfil/patch, decisão sobre divergência Python e aceite D6-02. Testes da Etapa 11 no laboratório não homologam o TUF operacional.
6. **Domínio adversarial**: mesmo usuário administrador, substituição do verificador e snapshot simultâneo de todas as autoridades não foram mitigados.

**Próximo passo recomendado:** Lote 4 **somente de contrato**, criando contrato inerte de `IntentCommit` e `TerminalCommit` no mesmo digest testemunhado, estudando crash após intent, perda de resposta de terminal, CAS entre operações, recuperação ambígua e permissão temporária. Não codificar `CanSelect/Confirm` nem integrar executável enquanto G1/G2/G8/G5/G6 não tiverem barreiras compostas verificáveis. Reservar WORK para revisão independente pontual após contrato amadurecer.

**G1–G8 permanecem BLOCKED, 0/8 gates operacionais homologados.**
