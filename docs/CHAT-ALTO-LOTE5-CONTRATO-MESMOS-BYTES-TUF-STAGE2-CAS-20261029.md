# GF — CHAT ALTO — Lote 5: contrato de composição real em fixtures (ANTES DO CÓDIGO)
**2026-10-29 · G1–G8 BLOCKED 0/8 · exclusivamente branch experimental**

Base congelada `5768ec3164b18778de8d4be9ca5528ef1fe33dc3`. Referências: `docs/CHAT-ALTO-LOTE4-CAS-DUAS-FASES-CHECKPOINT-20261029.md`, `docs/CHAT-ALTO-LOTE3-TUF-TERMINAL-CHECKPOINT-20261029.md`, parecer WORK G1/G2/G8 no chat. PR #65 Draft `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; main, BETA .66, telemetria e infraestrutura não se tocam.

## Problema exato novo

O Stage3 usa `DurableTufAttempt.Get` (fonte TUF fixada) e `Stage3TufBinding.Bind` para produzir `Stage3TufTarget` em memória; Stage2 grava `intent` e `rolled_back`. L4 usa um único head CAS em RAM, mas seu `L4TufProjection` **não contém digest de todos os bytes de `Stage3TufTarget`** e `L4CompositeProjection` não inclui digest da operação preparada. Portanto, verificar cada modelo separadamente não demonstra o mesmo payload entre o verificador TUF, o diário de operação, o Stage2 e a publicação CAS.

## Invariantes de prova (não garantias operacionais)

- **L5-I1:** no mesmo processo/fixture, somente após refresh Stage11 TUF assinado e `Stage3TufBinding.Bind(live, live, zip, bundle, checkpoint)`, congelar os bytes da estrutura `Stage3TufTarget` com `StateCodec.Encode`; SHA-256 desses bytes integra explicitamente o `L4IntentClaim` através de campo obrigatório na projeção L4. Os digest/versões de todos os quatro papéis, ZIP, manifest e entrada devem ser copiados da estrutura *validada*, não recomputados de strings inventadas.
- **L5-I2:** no mesmo `L4CompositeProjection` devem ser vinculados os bytes da `CompositeCheckpoint` real e os bytes do `OperationJournal` preparado. A identidade (instalação, repo, txId, `CandidateSha256`, `TrustView.Digest`) deve continuar igual em Stage3/Stage2/L4. Não vincular somente `TxId` e sequência.
- **L5-I3:** antes do CAS, reabrir `DurableEpochCache`/Stage2/`OperationJournal` e reproduzir `Stage3TufBinding.Bind` contra a mesma `TrustView`. Qualquer alteração de bytes TUF, operação, entrada ou `intent` entre bind e CAS bloqueia. Se a validação anterior ficou obsoleta, **não** presumir que o hash é autenticidade renovada.
- **L5-I4:** um `IntentCommit` já publicado pode ter resposta perdida; reconsultar mesmo `txId` e mesma fase em modelo CAS não autoriza seleção. Após `FixtureSession.Recover` inerte, ler de novo o terminal `rolled_back` Stage2 e sua cadeia desde a intenção comprometida, conferindo seletor antigo, instalação, txId, digest e resultado.
- **L5-I5:** `TerminalCommit` deve usar o exato digest terminal lido dos bytes reais do diário Stage2, predecessor = recibo de `IntentCommit` da mesma witness; sem terminal local, sem testemunho de terminal ou após alteração de bytes, **BLOCKED**. O mesmo commit terminal é idempotente **somente dentro da RAM**.
- **L5-I6:** composição inerte não possui `CanSelect`, `CanConfirm`, serviço externo, root de witness provisionada, chave de recibo, atomicidade real de NTFS+witness, resistência a rollback de ambos domínios ou prazo G8 operacional. Mesmo o caminho feliz retorna `CommittedButNotAuthorized`.

## Cutpoints e negativos inéditos propostos

| Caso | Interrupção/ataque | Falha exigida |
|---|---|---|
| L5-01 | Complete Stage11→Stage3→Stage2→Stage9→L4 intent; perda de resposta **após** CAS; `rolled_back` real → L4 terminal | Duas fases avançam uma vez cada, recibos idênticos após retry, seletor antigo, nenhuma autorização |
| L5-02 | Alterar **somente bytes Stage3TufTarget** do vínculo, preservando os campos limitados legados `RootSha/EntrySha`, entre validação e CAS | `L4IntentClaim` recusa por digest completo dos bytes, head permanece estável |
| L5-03 | Alterar **somente OperationJournal** após bind, preservando candidato/intent e TrustView | Nova leitura/digest incompatível → CAS bloqueado |
| L5-04 | Alterar ou reverter `Stage2 intent`/identidade após bind, preservando os demais arquivos | Prepublish/checkpoint recusa sem avançar CAS |
| L5-05 | Commit de intent feito; sem terminal ou com terminal local de txId estranho/bytes alterados | Terminal CAS não pode ser publicado legitimamente; recibo externo inicial não autoriza resultado |
| L5-06 | Depois do terminal, alterar bytes Stage2 válidos e tentar registrar novo resultado no mesmo txId | Recusa por vínculo com digest terminal ancorado, mesmo quando conteúdo local parece plausível |

**Metodologia:** usar arquivos `*.fixture` em `RUNNER_TEMP`, `stage11/make_fixtures.py` e TUF Stage8 pinado já existentes. Só código em `m3-l5-composed`, além de adição mínima de campos opcionais ao tipo de projeção L4 (necessária para transportar todo o digest; nenhuma mudança de gate). Pode haver regressão automática de CI L4 por alteração de fonte; NÃO reexecutar manualmente suítes L3/L4 nem adulterar testes antigos.

**Não testar** `FixtureSession.Begin` ou `Confirm`; utilizar `G1Preselect.Prepare` e `Recover` somente no estado inerte. Compromisso terminal não substitui prontidão de processo G5/G6; `rolled_back` não é sucesso funcional.

**Limite adicional:** `StateCodec.Encode` não é esquema de assinatura normativa; os bytes são vinculados no laboratório, mas autenticidade da raiz de witness e durabilidade pós-crash não são comprovadas. Nenhuma regressão autoriza gate.

**Saída:** suíte Windows dedicada com casos negativos dirigidos, checkpoint com CI/SHAs/limitações, PR #65 Draft, main, BETA .66 e infraestrutura intactos. G1–G8 `BLOCKED`, 0/8.
