# GERENCIADOR FINAL — CHAT ALTO — Lote 4: CAS de duas fases em head único
**29/10/2026 | laboratório inerte pós-WORK | G1–G8 BLOCKED (0/8)**

## 1. Identificação e resultado verificado

- Repositório: `walissonminel-1991/gerenciador-final`, branch `experimento-g5-ipc-chat-alto-20261029`.
- **HEAD inicial confirmado** `18820c62993223c818f3a1036dc1022b1f8a7112`.
- Contrato pré-código: `docs/CHAT-ALTO-LOTE4-CONTRATO-INTENT-TERMINAL-CAS-20261029.md` no commit `dc98dee2245678d3af366a037aba8d085fe27423`.
- **HEAD de código validado** `8ef17e8b4509a6d04b9b9727b8127e0cafac7b73`.
- **CI Windows**: [40920731741](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40920731741), `SUCCESS`, **12/12 PASS**, 0 FAIL, 0 warnings, 0 errors; SDK .NET `10.0.402`, `windows-2022`, `TreatWarningsAsErrors=true`.
- PR #65 Draft referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; reconfirmar após este documento. `main`, BETA .66, backend, Worker/D1, consentimento, telemetria e demais gates não são objeto de alteração.
- WORK adicional: **nenhum**. Nenhum novo keypair, segredo, conta, endpoint, serviço, deploy, release, merge, alteração no GF executável ou nos PCs.

## 2. Propriedade adicional demonstrada

Lote 3 verificou em fixture a cadeia TUF assinada `root/timestamp/snapshot/targets→ZIP→bundle`, além de um `intent` e um `rolled_back` testemunhados **em objetos independentes**. Isso não estabelecia ordem conjunta, CAS inter-fases, um único predecessor monotônico nem recuperação de respostas perdidas de ambas as fases.

Este lote implementa **somente um modelo com uma única cabeça testemunhal em memória** para as fases separadas, sob lock por instância:

`stable(0) → intent(txA,1) → terminal_rolled_back(txA,2) → intent(txB,3)`.

- `L4Evidence` congela **bytes sintéticos** do vínculo Stage3-like TUF e do checkpoint composto; `L4Claim.Prepare` valida os campos cruzados `repo/install/txId/Stage2 intent/candidate/trust-view` e registra **SHA256 dos bytes efetivos**.
- `PublishIntent` exige o mesmo par de evidências recebidas no preparo (não basta o identificador da transação). Mudança de bytes antes do CAS é rejeitada. O payload tem domínios de hash separados `gf-g1-intent-commit-model/1`.
- `PublishTerminal` exige `txId`, instalação, `IntentCommit.Digest`, predecessor inteiro e resultado **exclusivamente `rolled_back`**. O payload usa `gf-g1-terminal-commit-model/1`; não há `selected`/`confirmed`/READY e nenhuma API de seleção.
- Um único `L4Head` `{Sequence,Digest,Phase,TxId}` governa ambas as operações. O próximo intent só pode suceder `terminal` ou `stable`, não outro intent. CAS testa o head **inteiro**, inclusive fase e txId, sob lock.
- O mapa de recibos `(phase,txId)→(claimDigest,receipt)` permanece no mesmo objeto em memória e não é eliminado ao avançar para nova transação. Retries de payload idêntico recuperam recibo antigo exato, inclusive quando o head já avançou; payload diferente é conflito.
- Quando a resposta é perdida **após o CAS**, o publicador pode não saber se houve compromisso; `ReadForReconciliation` retorna recibo existente ou ausência. Essa consulta **não está autenticada**; falha offline/no enrollment é bloqueada, não TOFU. A falha antes do commit e a ausência de recibo nunca conferem autorização.
- Todos os retornos de reconciliação são diagnósticos `CommittedButNotAuthorized`, `UnseenBlocked`, `UnavailableBlocked`, `NotEnrolledBlocked` e `ConflictBlocked`. Não existe `Authorized` ou `CanSelect`.

**Fronteira intencional de prova:** a `L4TufProjection` contém hashes e nomes similares aos do Stage3, mas **não chama** o verificador TUF nem `Stage3TufBinding.Bind`. Sua proveniência como cadeia assinada é hipótese externa. O teste assegura que os *mesmos bytes fornecidos* são reutilizados; **não demonstra** que são autênticos. Não inferir idempotência durável, atomicidade entre diários NTFS e witness, confiabilidade de rede, operação G8 após deadline, assinaturas de recibos ou segurança contra compromisso do witness.

## 3. Matriz de contraexemplos da CI — 12/12

| ID | Invariante exercitada |
|---|---|
| `L4-01` | Commit intent seq1 e terminal seq2 compartilham head único e digest anterior exato; classificação permanece bloqueada |
| `L4-02` | Resposta do intent perdida **depois** do CAS: consulta por `txId/phase` e retry recuperam recibo idêntico sem segundo avanço |
| `L4-03` | Resposta terminal perdida **depois** do CAS: consulta/retry preservam seq2 e recibo, sem duplicar |
| `L4-04` | Outra intenção não pode saltar o terminal de uma transação ainda aberta |
| `L4-05` | Duas operações concorrentes com txIds diferentes e mesmo predecessor: exatamente uma vencedora por CAS |
| `L4-06` | Mutação dos bytes TUF entre preparo e CAS impede gravação; outro corpo coerente com mesmo txId já comprometido é recusado |
| `L4-07` | Terminal de outra transação ou com digest de intenção diferente não avança o head |
| `L4-08` | Predecessor com seq/digest correto, mas fase forjada, não passa no CAS |
| `L4-09` | Nova transação só parte do head `terminal` exato; predecessor `intent` antigo recusa |
| `L4-10` | Witness offline ou testemunha nova vazia: bloqueio, sem inscrição TOFU automática |
| `L4-11` | Mesmo txId, terminal alternativo após compromisso: conflito de idempotência, sem reescrita |
| `L4-12` | Recibo terminal da transação anterior continua recuperável mesmo após novo intent |

No log da CI, `L4_TWO_PHASE_FINAL_REPORT` registrou `passed=12,failed=0`. A CI também exige `unified_two_phase_single_monotonic_head`, `intent_lost_reply_reconciled_without_double_CAS`, `terminal_lost_reply_reconciled_without_double_CAS`, `subsequent_tx_requires_terminal`, `competing_tx_has_exactly_one_winner`, `tuf_binding_bytes_tamper_rejected`, `cross_tx_terminal_rejected`, `full_head_phase_enforced`, `next_tx_continues_at_terminal_head`, `unavailable_or_missing_witness_blocked` e `only_diagnostic_blocked_results` como `true`. Exige `false` para `inputs_cryptographically_authenticated`, `real_tuf_validation_performed`, `durable_witness_exists`, `receipts_signed`, `real_cross_journal_atomicity`, `tested_kernel_io_or_power_loss`, `product_built/executed`, `permitted_selection/confirmation`; `operational_gates_closed=0`.

## 4. Arquivos experimentais novos

- `prototipos/autoatualizador-seguro/m3-l4-cas/L4TwoPhaseModel.cs.txt`: modelo isolado, CAS lock, fases, recibos, hashes de bytes e reconciliador, **sem dependência de TUF/.NET Windows operacional**.
- `prototipos/autoatualizador-seguro/m3-l4-cas/L4TwoPhaseTests.cs.txt`: 12 novos testes de cutpoints e concorrência.
- `prototipos/autoatualizador-seguro/m3-l4-cas/L4TwoPhase.csproj`: .NET 10 sem NuGet externo, sem chamadas ao produto.
- `.github/workflows/autoatualizador-m3-l4-cas-duas-fases-inerte.yml`: CI Windows read-only, relatório esperado e verificação contra inclusão do protótipo em `GerenciadorAplicativos.csproj`.
- `docs/CHAT-ALTO-LOTE4-CONTRATO-INTENT-TERMINAL-CAS-20261029.md` e este checkpoint.

## 5. Bloqueios e riscos importantes após o avanço

1. **Trust bootstrap/external:** modelo de witness em RAM, sem identidade autenticada, monotonicidade pós-restart, armazenamento independente, custódia, revogação, fallback/recuperação ou proteção contra operador malicioso. Um invasor que restaure witness junto com disco derrota a âncora.
2. **Autenticidade do insumo:** as projeções de TUF/Stage2 são **bytes sintéticos do harness**; a verificação completa do Lote 3 **não participa desta publicação CAS**. Não confundir hash consistente com TUF autenticado. Para segurança composta, obter o **mesmo vínculo Stage3 gerado por validação legítima**, selado pelo recibo real.
3. **Persistência e G8:** um lock em memória não dá CAS remoto durável, nem recupera resposta perdida se a própria witness reiniciar. Timeouts de I/O ou `File.Move` não garantem ausência de efeitos tardios. O runner não prova `Flush(true)` em energia física.
4. **Stage2 e operação:** `L4TerminalClaim` usa um digest terminal fornecido ao modelo, não valida Stage2 `rolled_back` em seu próprio processo; isso permanece garantido apenas na prova separada Lote 3. Não há commit atômico entre journal Stage2, diário TUF, operação, receipt e testemunha.
5. **Lotes de segurança ainda separados:** prova ECDSA de G1 anterior assinou outra mensagem; **não** autentica estes novos recibos. G5/G6 READY real, identidade de processo, proteção até o uso e G2 D6-02 permanecem pendentes.
6. **Atualizador histórico intacto:** `FixtureSession.Begin/Confirm/Recover`, `IntegratedPipeline`, `TrustIntegratedPipeline`, `M3CommitGate` seguem em protótipos separados; não há coordenador comum operacional e **não se deve criar uma API de seleção** a partir de resultados bloqueados.

## 6. Próximo passo recomendado e limiar do WORK

**Lote 5 proposto:** prova de composição *somente negativa* que passe **os mesmos bytes derivados de Stage3TufBinding/Stage2** para o `L4IntentClaim`, valide terminal Stage2 e duas fases sob um único head, sem distribuir autorização. Incluir falhas de alteração de conteúdo entre pré-validação e commit e recuperação de compromisso ambíguo. Essa integração de fixtures diminuirá a lacuna entre provas que hoje não compartilham o mesmo payload, mas ainda não provará testemunha durável nem G8 físico.

**WORK:** não invocar agora; usar auditoria pontual só após a composição L3+L4 ficar suficientemente madura para contraditório técnico real. Qualquer tentativa de dar `CanSelect` ou integrar `Begin/Confirm` antes disso seria prematura.

**Estado formal:** G1–G8 `BLOCKED` (0/8), sem merge, release, deploy, infra ou telemetria.
