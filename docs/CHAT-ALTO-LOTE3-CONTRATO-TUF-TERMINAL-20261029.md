# GERENCIADOR FINAL — Lote 3 — contrato de cadeia TUF e terminal G1/G2/G8 (pré-código)
**29/10/2026 | somente protótipo inerte | G1–G8 BLOCKED (0/8)**

## Contexto auditado

Base `4eed23cbd01210ec7e3fa06d18f019e99ec70498`; `docs/CHAT-ALTO-LOTE2-N1-N2-CHECKPOINT-20261029.md`; parecer WORK G1/G2/G8 recebido no chat. O Lote 2 demonstrou vínculos por `TrustView.Digest` e extensão de `intent → rolled_back → intent`, **não** uma atualização integralmente autenticada nem a autenticidade de terminal depois do compromisso de intenção. PR #65 Draft, main, BETA .66, infraestrutura e telemetria não entram.

## Invariantes **propostas**, ainda não implementadas operacionalmente

- **L3-I1 (TUF-chain):** checkpoint só incorpora uma `TrustView` completa (`root/timestamp/snapshot/targets` presentes, tentativa encerrada), produzida por refresh TUF de fonte fixada e raiz de teste, com versões/digests de bytes por papel vinculados. Um `TrustView.Digest` isolado ou quatro versões numéricas **não bastam**; a proveniência da verificação importa. `targets` deve autenticar exatamente o alvo e seu hash.
- **L3-I2 (target-binding):** candidato inerte da operação deve ser ligado ao conteúdo TUF autenticado e às referências de `targets`, não apenas possuir o mesmo número de versão ou `permit.CacheSha256`. A identidade de arquivo guardada no Windows e o digest do bundle continuam separadamente necessários para TOCTOU. **Sem prova de ligação, bloqueio.**
- **L3-I3 (duas fases externas):** `IntentCommit(txId, intentDigest, checkpointDigest)` e `TerminalCommit(txId, intentCommitDigest, terminalDigest, outcome)` são **duas transições lógicas distintas**; o primeiro nunca significa que `selected` ou `confirmed` ocorreram nem autentica o resultado local.
- **L3-I4 (admissão seguinte):** qualquer segundo `intent` só poderá depender de um `TerminalCommit` explícito correspondente à transação anterior; estado local terminal coerente mas sem compromisso externo permanece **bloqueado**. O terminal pode ser `rolled_back` inerte na fixture; não acionar `Begin`, `Confirm`, `selected`, `READY` ou troca de seletor.
- **L3-I5 (idempotência/conflito):** repetir o mesmo `txId` e o mesmo `terminalDigest/outcome` retorna o mesmo compromisso **no modelo em memória**; txId igual com outro digest/outcome ou predecessor incorreto é conflito; qualquer resultado remoto desconhecido é bloqueado.
- **L3-I6 (rollback/fork):** depois de comprometido o terminal, uma cópia local de terminal com bytes alterados, mesmo que semanticamente plausível e validável por checksums locais, deve divergir da âncora de terminal **não restaurada**. Se a âncora também for restaurada/for maliciosa, o modelo deixa de proteger.
- **L3-I7 (não autorização):** toda saída é `BoundButNotAuthorized` ou `Blocked`. Nenhum teste novo fornece `CanSelect/CanConfirm`, recibo criptográfico operacional, witness independente, atualização real, energia/armazenamento homologados ou integração ao GF.

## Máquina de estados proposta

`NotEnrolled → TrustChainVerifiedInert → IntentPrepared → IntentCommittedModelOnly → TerminalObservedInert → TerminalCommittedModelOnly → NextIntentEligibleForInspectionOnly`.

Em cada transição, falha conhecida, `pending`, falta de trust chain, desafio/recibo incorreto, perda de resposta após CAS ou `remote-ahead/local-ahead` dão **BLOCKED/UNKNOWN**, nunca permissão. Depois de `IntentCommitted`, não inferir terminal. Depois de `TerminalCommitted`, não inferir disponibilidade ou prontidão do pacote. Um timeout não cancela efeito síncrono anterior e não substitui reconciliação.

## Contraexemplos novos direcionados

| Caso | Condição de ataque | Falha objetivamente detectável | Evidência que NÃO é fornecida |
|---|---|---|---|
| L3-TUF-01 | View sem `snapshot` ou `targets` após eventos Stage11 válidos | Recusa antes de criar vínculo completo | Não prova release operacional |
| L3-TUF-02 | Refresh TUF Stage11 de fixtures assinadas, recomputar vínculo e substituir um papel/cache por bytes de outra cadeia | Digests por papel, referências e estado vinculado divergem | Sem autorização de candidato |
| L3-TERM-01 | `intent` ancorado e terminal local `rolled_back` com histórico válido, mas terminal **não** testemunhado | Rejeição do uso do terminal na operação seguinte | Não prova durabilidade externa |
| L3-TERM-02 | Terminal `rolled_back` ancorado em memória, editar bytes terminais mantendo estrutura JSON válida, reabrir diário Stage2 | A comparação com compromisso terminal imutável recusa o fork | Teste não prova comprometimento de witness |
| L3-TERM-03 | Reutilizar `txId` com mesmo/diferente digest, ou restaurar âncora em memória | Idempotência exata/recusa de conflito **apenas no modelo**; quando memória se perde, ausência de autoridade bloqueia | Não prova CAS persistente em serviço |

**Condição para qualquer CI:** usar Stage11 TUF fixado/patch de laboratório, raiz/test vectors já existentes, runner Windows descartável; construir a suite nova sem rodar N1/N2. Não modificar `FixtureSession.Begin/Confirm`, o seletor ou o GF principal. A CI não pode promover G1/G2/G8 de BLOCKED.

## Decisão de escopo

Uma demo de validação de `targets` sobre ZIP inerte e uma prova negativa de terminal sem compromisso acrescentam garantias diferentes de N1/N2. Se for inviável no escopo isolado, registrar a lacuna em vez de simular hashes como autenticação. O requisito definitivo de terminal externo poderia ser segunda testemunha/commit ou outra prova de continuidade com garantias equivalentes, mas ainda requer especificação e revisão de adversário, privacidade, custos e duração. O modelo não seleciona uma solução operacional.
