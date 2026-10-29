# GF — CHAT ALTO — Lote 4: contrato inerte de IntentCommit / TerminalCommit
**29/10/2026 | pré-código | após WORK G1/G2/G8 | nenhuma autorização operacional**

## Base e escopo

- Base congelada: `18820c62993223c818f3a1036dc1022b1f8a7112`, branch `experimento-g5-ipc-chat-alto-20261029`; leia `docs/CHAT-ALTO-LOTE3-TUF-TERMINAL-CHECKPOINT-20261029.md` e parecer WORK completo compartilhado.
- PR #65 Draft em `work-integracao-final-20261027`, HEAD de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`, `main`, BETA .66, infraestrutura, telemetria inalteradas.
- Problema preciso: `Stage3TufBinding.Bind` comprova em **fixture** root/timestamp/snapshot/targets→ZIP→entry, mas seu `CompositeDigest` é projeção local. `InertCompositeAnchor.CommitModelOnly` ancorou intent separadamente, e `Stage3TerminalMemoryAnchor` ancorou terminal em outro objeto em memória. Nada garante que os três registros se refiram **ao mesmo compromisso autenticado** nem preserva os dois recibos num histórico CAS único.
- Meta L4: especificar duas fases no **mesmo** head lógico em memória, com invariantes de vínculo por bytes canônicos do `Stage3TufTarget`, checkpoint `CompositeCheckpoint`/intent e terminal Stage2. **Não** criar serviço de witness, chave, recibo real, protocolo operacional, seleção, confirmado nem integração GF.

## Autoridades e formatos — modelo proposto, não certificado

**Pré-requisito externo não implementado**: `SignedTargetsEvidence` só seria elegível depois da verificação real do Stage11 e da ligação de `Stage3TufBinding.Bind`. O modelo de CAS **não consegue** autenticar a origem de bytes arbitrários; usá-los diretamente como garantia de TUF seria falha grave. No laboratório, a entrada é um token de teste e a saída é *apenas diagnóstico bloqueado*.

O `IntentClaim` deve conter:
- `schema/purpose = gf-g1-l4-intent-fixture/1`, instalação/repositório/época do pin **assumidos**, `txId` não reutilizável, `CandidateSha256`, `Stage2IntentDigest`, `CompositeCheckpointDigest`, `TufBindingBytesDigest` e **digest dos mesmos bytes TUF** do `Stage3TufTarget` da Etapa 3; `expectedWitnessSequence + expectedWitnessDigest` e `releaseFloor`.
- `TufBindingBytesDigest` e `CompositeCheckpointDigest` são ligações **impostas** pelo payload; não se presume que os bytes foram autenticados só porque SHA-256 confere. Transformar ambos em um digest único assinado é requisito futuro.
- `TerminalClaim` contém `schema/purpose=gf-g1-l4-terminal-fixture/1`, instalação/repo/txId, **digest do IntentCommit exato**, `Stage2TerminalDigest`, `outcome=rolled_back` (apenas fixture), e predecessor do estado CAS testemunhado. Não aceita `confirmed`, `selected`, READY sintético nem permissão de execução.

Cada commit incrementa **uma única sequência** witness. `IntentCommit` e `TerminalCommit` usam domínios diferentes; seus respectivos recibos possuem `phase, txId, commitDigest, priorHeadDigest, seq, bodyDigest`. O head passa por `STABLE → INTENT_COMMITTED → TERMINAL_COMMITTED`. A transação seguinte tem predecessor `TERMINAL_COMMITTED` obrigatório. O campo `Stage2Generation` **nunca** é comparado a `WitnessSequence`.

### Contrato de concorrência e idempotência

1. CAS linearizável **no modelo** exige o head esperado idêntico em sequência, digest, fase e txId, sob uma única seção crítica. Não basta `seq` ou `txId` isolado.
2. O mesmo `txId/phase/bodyDigest` retorna exatamente o recibo previamente registrado, mesmo que o head já tenha avançado. `txId` igual com payload divergente recusa, **inclusive entre fases**; dois `txId` diferentes competindo pelo mesmo head deixam exatamente um vencedor.
3. `TerminalCommit` depende do `IntentCommit` do **mesmo** txId e do digest íntegro da reivindicação terminal. O primeiro commit não habilita inferência de terminal e o segundo não habilita ativação.
4. Timeout/perda de resposta **depois de CAS** é resultado `UNKNOWN`, não evidência de aborto; a consulta por `installation,txId,phase` deve recuperar **o mesmo recibo**. Ausência de recibo e head anterior implica `unseen`; head divergente ou recibo incompatível implica `conflict`; witness indisponível implica `blocked`. Nada autoriza republicação cega, seleção nem exclusão de `pending`.
5. Reinício da testemunha **não é provado**: serialização/cópia de objeto em RAM é somente simulação. Um novo processo com memória vazia é `not-enrolled`, jamais TOFU. Nenhuma CI desta missão pode chamar isso de CAS durável ou rollback-protected.

## Cutpoints e hipóteses adversariais

| Corte | Witness observado depois | Leitura conservadora | Proibição |
|---|---|---|---|
| Antes de intent CAS | head anterior, sem recibo | `UNSEEN`; pode planejar nova tentativa **somente após** políticas futuras | Nunca selecionar |
| Intent CAS gravado, resposta perdida | head `INTENT`, recibo recuperável | `REMOTE_AHEAD`; recuperar mesmo txId | Não interpretar como erro/rollback |
| Intent conhecido, terminal local ainda ausente | head `INTENT` | `TERMINAL_MISSING` | Não publicar terminal inventado |
| Antes de terminal CAS, resposta/rede perdida | head `INTENT`, sem recibo terminal | `UNSEEN/UNKNOWN` conforme observabilidade; reconsultar | Não afirmar que terminal foi gravado |
| Terminal CAS gravado, resposta perdida | head `TERMINAL`, recibo recuperável | `REMOTE_AHEAD`, reconsultar txId | Não avançar automaticamente |
| Após terminal, transação seguinte com head de intent antigo | head `TERMINAL` | `STALE_PREDECESSOR` | Não aceitar seq/geração ignorando fase |
| Duas transações com o mesmo predecessor | só um CAS vence | `CONFLICT` para perdedora | Não trocar de candidato/txId |
| Dois terminais de mesmo txId com resultados distintos | primeiro recibo imutável | `IDEMPOTENCY_CONFLICT` | Não sobrescrever/reaproveitar recibo |
| Witness offline, ausente, forjada ou reiniciada sem estado | desconhecido | `UNAVAILABLE/NOT_ENROLLED` | Sem TOFU nem aceitação offline |
| Após deadline G8, gravação tardia de qualquer fase | efeito possivelmente já ocorreu | `LATE_EFFECT_REVIEW`; conciliar sem assumir abort | Não admitir/confirmar como sucesso |
| Queda elétrica/snapshot integral incluindo witness | sem prova disponível | **PENDENTE** de ambiente independente | Não reivindicar durabilidade/antirollback |

## Provas novas autorizáveis

**L4-N1:** um único CAS intercalado entre `IntentClaim` e `TerminalClaim` deve retornar recibos exatos para respostas perdidas *em ambas as fases* e jamais produzir `Authorized`.

**L4-N2:** `TerminalClaim` de `txId`/digest TUF/intent diferente ou com predecessor de outra fase não pode comprometer.

**L4-N3:** duas publicações concorrentes que partem do mesmo head devem deixar uma única vencedora, sem sobrescrever payload diferente para txId já visto.

**L4-N4:** **duas operações seguidas** só evoluem `terminal k → intent k+1`; não existe bypass `intent k → intent k+1`.

**L4-N5:** alterar **os bytes** do vínculo `Stage3TufTarget` entre preparo e CAS deve invalidar a proposta (sem simular ou reexecutar assinaturas TUF).

Se forem implementados, usar apenas dados inertes e CI Windows; os testes anteriores L3-TUF e L3-TERM permanecem intocados. Não derivar `CanSelect` de qualquer resultado.

## Decisão do Lote 4

O ganho concreto esperado é provar **segregação de duas fases no mesmo estado monotônico** e detectar compromisso contraditório/replay entre fases. Isso é diferente de testar somente checksum, assinatura individual, rollback terminal ou idempotência unitária da memória.

É **bloqueio conhecido**: o estado remoto não é durável, sua autenticidade não está implementada, o token de cadeia TUF depende do Stage11 em outro protótipo, a inscrição é modelada por premissa, não existem testes físicos G8 nem políticas G5/G6. G1–G8 seguem `BLOCKED` (0/8).

**Nenhuma rota `FixtureSession.Begin/Confirm/Recover` pode ser modificada neste lote**; a próxima decisão de integração exigirá autoridade composta, revisão independente e provas reais.
