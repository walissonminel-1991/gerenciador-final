# CHAT ALTO — G1 / E5: contrato transacional e contraexemplos — 29/10/2026

> Documento e protótipo experimentais. Nenhum gate operacional aprovado. Sem conexão com executável GF, telemetria ou infraestrutura remota.

## 1. Baseline e evidências verificadas

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial confirmado: `f5fbc0ac3f27a7402cab312d7578d5a8e4b1634f`.
- Matriz de entrada: `docs/CHAT-ALTO-G1-MATRIZ-ARQUITETURAL-20261029.md`.
- Código da subprova (commit da CI): **`2594eb6f02b541706e6ebceb35976688f0556667`**.
- CI Windows 2022: [40898382162](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40898382162), **SUCCESS**, **16/16 PASS, 0 FAIL, 0 warnings, 0 errors**.
- PR #65 Draft mantida no HEAD **`ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`**; main, BETA .66, Worker/D1, domínio, telemetria, canal e release não alterados.
- WORK não utilizado. Sem merge, deploy, serviço, endpoints, chaves, contas ou segredos. Nenhum PC pessoal foi tocado.

## 2. Problema específico e ganho em relação às 11 subprovas anteriores

O modelo anterior `G1TrustedMemoryWitness` comparou head local e head externo; **não modelou publicação nem resposta de commit perdida**. A matriz E5 exigia distinguir entre o servidor que **não chegou a gravar** uma proposta e o que **gravou, mas perdeu a resposta**, sob publicação concorrente.

A nova subprova isolada em `prototipos/autoatualizador-seguro/m3-g1-transaction/` implementa:

- `G1TransactionModel.cs.txt`: objeto testemunha em memória com `Publish` idempotente, **CAS atômico sob um lock**, índice de `(InstallationId,TxId)`, monotonicidade de geração/piso, recibo por transação, falha simulada **depois** do commit e consulta do estado;
- `G1LocalJournalModel`: journal **somente em memória** com `Stable`, `Prepared` e `AckRecorded`, além de `CrashAndRestart()` que clona o estado para simular um restart;
- `G1TransactionReconciler`: consulta **sem mutação**. Jamais converte uma igualdade de evidências em autorização de seleção, confirmação ou execução;
- `G1TransactionTests.cs.txt`: 16 testes **novos** sobre fronteiras de crash, concorrência, idempotência, registro de recibo, ausência de witness e bootstrap;
- `G1Transaction.csproj` e `.github/workflows/autoatualizador-m3-g1-tx-inerte.yml`: projeto .NET 10.0 independente, dependências travadas via `../packages.lock.json`, Windows 2022 e verificação de que o GF não compila os fontes experimentais.

**Limite fundamental:** o uso de `StateCase` / `PersistentState` / `FixtureSession` originais é **somente para obter e revalidar o ponto de partida de uma instalação inerte**. O novo journal de preparo/ack **não grava em NTFS** e não modifica a máquina transacional da Etapa 2. Portanto a suíte **não prova commit distribuído durável**, corte real de energia nem recuperação de um processo executável após crash do sistema.

## 3. Contrato modelado (não aprovado para implementação)

### 3.1 Objetos

- `TxHead(InstallationId,Generation,ApprovedSequence,Digest)`: referência lógica de checkpoint do **modelo**. O seletor, recibo TUF, prazo e autenticidade ainda não integram este formato; isso impede usá-lo como formato operacional.
- `TxProposal(InstallationId,TxId,Expected,Next)`: operação condicional vinculada à instalação e a uma referência de base.
- `TxReceipt(InstallationId,TxId,Expected,Committed)`: comprovação **apenas lógica** do CAS em memória. Não é assinatura, não tem epoch externo independente nem certificado.
- `TxLocalPhase`: `Stable` → `Prepared` → `AckRecorded`; não há `Activate`, `Select`, `Confirm` ou função que aprove atualização.

### 3.2 Invariantes efetivamente exercitados

1. **C1 — CAS**: um avanço novo só é aceito se `Expected` for exatamente o head atual da testemunha; geração deve subir uma unidade, piso não recuar e digest novo deve mudar.
2. **C2 — idempotência**: o par instalação + `txId` com payload idêntico retorna o recibo original; reutilizar o mesmo identificador com outro payload recusa `G1TX_IDEMPOTENCY_CONFLICT`. Isso não é persistência após reboot da witness.
3. **C3 — serialização**: duas chamadas concorrentes sobre o mesmo pai não podem gerar dois heads; exatamente uma vence no modelo com lock.
4. **C4 — ambiguidade**: o modelo pode gravar o head e **então** lançar `TimeoutException`. O cliente não recebe confirmação e não assume que houve falha no servidor; consulta posterior constata `RemoteCommittedAckMissingBlocked`.
5. **C5 — preparo local**: `Prepared` por si só não concede recibo nem altera `Stable`; diante de `Unseen`, `RemoteAhead` ou `Conflict`, reconciliação só **classifica e bloqueia**.
6. **C6 — ACK local**: `RecordAck` exige recibo com `TxId`, instalação, pai e destino esperados; marca `AckRecorded` mas não altera `Stable`. O teste não prova autenticidade de um recibo forjado com todos os campos coincidentes; tal ataque precisa ser tratado pelo lote de assinatura.
7. **C7 — indisponibilidade**: witness offline impede `Publish`; `Inspect` produz `UnavailableBlocked`. Não existe fallback para estado local ou inscrição automática.
8. **C8 — escopo restrito**: a fixture real da Etapa 2 permanece na geração, piso, digest e seletor anteriores. Nenhuma atualização de produto, instalação, DLL ou EXE é executada.

### 3.3 Matriz de cortes simulados e resultado

| Ponto de corte / falha injetada | Estado local modelado | Estado remoto modelado | Resultado de reconciliação |
|---|---|---|---|
| Antes de preparar | `Stable` | Pai original | `StableMatchBlocked` |
| Após preparar; antes de `Publish` | `Prepared`, sem ack | Pai original, tx não vista | `PreparedUnseenBlocked` |
| `Publish` com resposta perdida **após CAS** | `Prepared`, sem ack | Destino comprometido + recibo | `RemoteCommittedAckMissingBlocked` |
| Crash após publish; antes de ack | `Prepared`, sem ack | Destino + recibo | `RemoteCommittedAckMissingBlocked` |
| Crash após ack local | `AckRecorded` | Destino + recibo | `AckMatchesButNotAuthorized` |
| Outro publicador vence a disputa | `Prepared` para tx perdedora | Head de transação distinta | `ConflictBlocked` |
| Falha de witness na publicação/consulta | `Prepared` | Não observável | `UnavailableBlocked` |
| Reinício com testemunha não inscrita | `Prepared` | Sem registro | `ConflictBlocked` (fail closed) |

**Nenhum dos nomes de estado contém `Approved`, `Active` ou `Success`**. `AckMatchesButNotAuthorized` expressa igualdade lógica, não capacidade de ativar a versão candidata.

## 4. Evidências da CI (somente testes novos)

1. `G1TX-01`: igualdade estável retorna bloqueio, nunca aprovação.
2. `G1TX-02`: restart antes do preparo.
3. `G1TX-03`: restart depois do preparo, antes da publicação.
4. `G1TX-04`: resposta perdida **após CAS efetivo**, sem recibo local inventado.
5. `G1TX-05`: repetição idempotente depois de timeout retorna o mesmo head, sem segundo avanço.
6. `G1TX-06`: restart entre publicação e ack preserva ambiguidade.
7. `G1TX-07`: restart após ack continua sem executar/selecionar.
8. `G1TX-08`: `txId` igual e payload alterado é recusado.
9. `G1TX-09`: outra transação com pai desatualizado é recusada por CAS.
10. `G1TX-10`: **dois Task.Run concorrentes** tentam avançar do mesmo pai; resultado exatamente um `committed` e um `conflict`.
11. `G1TX-11`: transação preparada localmente, mas witness avançada por outra, bloqueada.
12. `G1TX-12`: recibo com destino divergente não vira ack.
13. `G1TX-13`: indisponibilidade real do objeto testemunha no modelo não permite publicação ou retorno otimista.
14. `G1TX-14`: recibo remoto visível e ack local ausente **não gera ack automático**.
15. `G1TX-15`: testemunha sem inscrição não autoriza bootstrap por `Publish`.
16. `G1TX-16`: repetição do bootstrap não reseta a testemunha.

O relatório `G1_TRANSACTION_FINAL_REPORT` confirma `post_commit_lost_reply_reconciled_blocked=true`,
`concurrent_cas_single_winner=true`, `ack_did_not_activate=true`,
`offline_did_not_fallback=true`, `competing_commit_refused=true`,
`idempotent_retry_no_double_commit=true`, `simulated_crash_boundaries_exercised=true` e
`stage2_original_fixture_untouched=true`.

Também declara explicitamente: `local_journal_durable=false`,
`remote_witness_durable=false`, `authenticated_witness=false`,
`physical_power_loss_tested=false`, `update_execution_implemented=false`,
`product_built=false`, `product_executed=false` e `operational_gates_closed=0`.

## 5. Lacunas e decisões para a próxima fronteira

- **Incompatibilidade de ordem com a Etapa 2:** `FixtureSession.Begin` da implementação experimental existente **troca o selector pelo candidato e grava selected antes de qualquer protocolo externo G1**. A máquina de estados deste lote **não foi integrada** ao caminho real. Uma futura integração segura exigirá reorganizar a ordem e provar que nenhum código executa/seleciona binário antes das condições necessárias. Não interpretar a presente suíte como correção desse risco.
- **Ack e witness ainda em memória:** `CrashAndRestart` copia objetos e não simula persistência de journal, reboot do runner nem queda elétrica. Ainda não existe transação atômica entre NTFS e witness.
- **Autenticidade inexistente:** uma resposta `TxReceipt` com campos consistentes **não é assinatura**. O modelo assume witness honesta e independente; witness comprometida ou replay com nonce de outro desafio não são resolvidos nesta frente. As 11 provas anteriores delimitam apenas parte desse risco.
- **Modelo de checkpoint incompleto:** `TxHead` não carrega selector, TUF, fase de transação, assinatura, prova de extensão, época por instalação nem tempo de resposta. Não adotar como formato real.
- **Atômico só em memória:** `lock` + `Dictionary` não provam exclusividade contra múltiplos servidores, threads não cooperativas externas, queda da witness, falhas de disco, troca de domínio ou rollback do estado remoto.
- **Incerteza de status**: se a witness não responde, não é seguro classificar `unseen` por presunção; a consulta autenticada futura deverá ser idempotente, durável e não dependente do retorno inicial de `Publish`.
- **Sem caminho automático de recuperação:** divergência, `pending`, remote-ahead, local-ahead e fork permanecem bloqueados e exigem política específica, com revisão independente e evidência de continuidade.

**Decisão:** G1–G8 permanecem **BLOCKED, 0 homologados**. Nenhuma funcionalidade do GF foi ampliada por este laboratório.

## 6. Próximo passo de alto valor e critério de uso do WORK

Um novo experimento deve **acrescentar garantia comprovável**, não repetir outros cenários `CAS` em memória. Prioridade possível: especificar e testar **integração de estado não executável antes do compromisso G1**, com um diário de intent durável inerte e um `checkpoint` estável que não se invalide ao gravar o ack local. Isso exigirá revisão dos *cutpoints* da Etapa 2 e ameaça do mesmo usuário; conservar G8 e G2 separados.

**WORK somente com contrato e evidências suficientes**, para uma revisão independente focada em protocolo, autenticação e crash consistency. Caso contrário CHAT ALTO e Windows CI continuam adequados.

**Restrições mantidas:** nenhuma PR #65, main, BETA .66, Worker, D1, flags, serviço, segredo, endpoint, telemetria, release, merge ou deploy alterados.
