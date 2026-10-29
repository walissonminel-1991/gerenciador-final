# GERENCIADOR FINAL — CHAT ALTO — Lote 2 pós-WORK: N1 e N2
**29/10/2026 | Experimento isolado | G1–G8 BLOCKED, 0/8 homologados**

## 1. Estado, fontes, limites e evidências

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial verificado: `4002cedbf11dcac8bb68341f2b81b52d8de5f3d9`.
- Contrato de origem: `docs/CHAT-ALTO-LOTE1-CONTRATO-CHECKPOINT-G1-G2-G8-20261029.md`, parecer crítico WORK G1/G2/G8 recebido no chat.
- **Código aprovado pela CI:** `bebc12556125da338d0261d51e3c91411a3965a3`.
- **CI Windows 2022 [40914793447](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40914793447): SUCCESS, 5/5 PASS, 0 FAIL, 0 warnings, 0 errors**, SDK .NET 10.0.402; commit final de código acima.
- PR #65 Draft, branch `work-integracao-final-20261027`, referência preservada `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Confirmar novamente ao terminar.
- Nada foi executado no GF real nem nos PCs pessoais. Nenhum merge, release, deploy, Worker/D1/telemetria, serviço, conta, endpoint, chave operacional ou atualização do roadmap.
- Não se usou WORK. O teste não executa suítes históricas G1/G2/G8.

## 2. Hipóteses congeladas antes de codificar

O schema amplo `GF-U2-COMPOSITE-CHECKPOINT/PROPOSED-1` **ainda não possui codificação, raiz de testemunha, política de recibo nem extensão terminal autenticada suficientes para autorizar execução**.

Para obter uma nova prova negativa de vínculo entre diários, foi criada uma **projeção restrita de fixture**, `gf-composite-proof-inert/1`, que inclui:
`InstallationId`, `RepositoryIdentity` de teste, `TxId`, digest do candidato, `WitnessSequence` lógico, `PriorWitnessDigest`, `TrustView.Sequence/Digest/Epoch/Root/Timestamp/Snapshot/Targets`, `OperationId/Digest`, `Stage2IntentDigest`, `IntentGeneration`, `PreviousTerminalDigest`, `PreviousWitnessIntentDigest`.

O digest é `SHA256(StateCodec.Encode(checkpoint))`. **Isso NÃO é assinatura nem prova externa**. O witness `InertCompositeAnchor` é objeto **em memória** no mesmo harness e o resultado permitido é somente `CompositeDecision.BoundButNotAuthorized`. Contadores de witness não são igualados a gerações Stage2. O teste é conscientemente anterior ao perfil composto completo e não supõe a autenticidade de um terminal apenas porque a cadeia local tem hashes consistentes.

**O que é real no teste:** `DurableEpochCache` da Etapa 11 com fonte TUF fixada/patch **idênticos ao procedimento já validado** pela Etapa 11; `OperationJournal` da Etapa 9; `PersistentState`/transação/recuperação da Etapa 2. A raiz TUF de teste preexistente é carregada do repositório; nenhum novo signatário de release é criado. N1 exercita o diário Stage11 **bootstrap e begin/finish**, sem refresh completo de `targets` nem aceitação de release. N2 usa rollback de transação na fixture com seletor anterior preservado, **não** `selected/confirmed`.

## 3. Arquivos criados somente no ramo experimental

- `prototipos/autoatualizador-seguro/m3-composed/ComposedCheckpoint.cs.txt` — representação do checkpoint inerte; validação cruzada de `TrustView`, operação preparada, Stage2 intent e cadeia de extensão desde a intenção externa anterior.
- `prototipos/autoatualizador-seguro/m3-composed/ComposedTests.cs.txt` — 5 testes N1/N2 dirigidos, sem executar candidata.
- `prototipos/autoatualizador-seguro/m3-composed/InertComposedCase.cs.txt` — raiz temporária marcada, três bundles `.fixture.txt`, seletores/usuário somente dados inertes e sem fixture validator legado.
- `prototipos/autoatualizador-seguro/m3-composed/Composed.csproj` — SDK 10, *TreatWarningsAsErrors*, inclui somente fontes necessárias de Stage2, Stage8, Stage9, Stage11 e pin de TUF com fonte externa. Nenhum `Compile` experimental integrado ao projeto GF.
- `prototipos/autoatualizador-seguro/m3-composed/prepare_pinned_source.py` — chama `stage8/prepare_source.py` já existente; verifica commit/manifests/hash e aplica **mesmo patch Stage8** à fonte TUF descarregada fora do repositório. Não altera dependências do produto.
- `.github/workflows/autoatualizador-m3-composed-n1-n2-inerte.yml` — CI Windows 2022 dedicada, .NET 10.0.402, Python 3.13 para fonte fixada, restore locked, relatório com flags que bloqueiam falso aceite e isolamento do `GerenciadorAplicativos.csproj`.

## 4. Resultados dos 5 testes inéditos

| Caso | Modelo, contraprova e comportamento |
|---|---|
| **N1-01** | Cria `tuf11` real, preserva snapshot anterior do subdiretório, avança validamente a cadeia Stage11 com `begin/finish`, prepara operação/intent e grava ACK inerte. Restaura **somente** `tuf11` à versão anterior, com Stage2 e recibo **idênticos byte a byte** ao estado atual. Stage11 ainda aceita seu histórico interno restaurado, mas a ligação com o registro de operação/checkpoint recusa `COMPOSITE_OPERATION_OR_TUF_UNBOUND`. Seletor antigo intacto. |
| **N1-02** | Depois do compromisso lógico, avança `tuf11` sem atualizar os outros diários; vínculo com o `OperationJournal` anterior também recusa silenciosa reinterpretação. |
| **N2-01** | Primeira `intent` Stage2 na geração 2 e witness lógico na sequência 1. `FixtureSession.Recover` encerra com `rolled_back` na geração 4, **sem mudar o seletor**. Segunda operação legítima com candidato inerte 3 produz `intent` na geração 5, witness lógico na sequência 2, incluindo prova local de extensão `intent2 → rollback_intent3 → rolled_back4 → intent5`. A classificação é `BoundButNotAuthorized`, nunca seleção. |
| **N2-02** | Um predecessor lógico adulterado cujo digest da primeira intenção não corresponde ao disco faz a prova de extensão recusar `COMPOSITE_PRIOR_INTENT_RESTORED`. |
| **N2-03** | Alterar o digest vinculado do `OperationRecord` no checkpoint (mesmo recalculando o hash **local** do checkpoint) recusa `COMPOSITE_CROSS_JOURNAL_ROLLBACK_OR_FORK`. |

Relatório `GF_COMPOSED_FINAL_REPORT`: `passed=5`, `failed=0`, `actual_stage11_trust_only_rollback_blocked=true`, `stage2_and_ack_byte_preserved=true`, `two_sequential_intents_with_real_rollback_terminal=true`, `foreign_operation_digest_denied=true`, `selector_never_changed=true`.

Flags obrigatórias: `terminal_external_authenticity_proven=false`, `witness_is_memory_model=true`, `tuf_targets_validated_for_candidate=false`, `real_commit_receipt_authenticated=false`, `gates_closed=0`, `product_built=false`, `product_executed=false`, `activation_authorized=false`, `physical_power_loss_tested=false`. Essas flags foram verificadas pela CI e não são meros comentários.

## 5. Histórico de correções de CI — sem enfraquecer proteções

A sequência inicial encontrou erros de **composição do projeto isolado**, não reprovação dos contraexemplos:

1. Dependências auxiliares ausentes `TrustExitProof`/`Stage8Repository`; inclusão das fontes originais de fixture.
2. NuGet `Tuf 1.1.1` sem APIs experimentais `ValidationUtc`/`TufRefreshStatus` do patch da Etapa 8. Em vez de alterar o produto ou o upstream, o novo projeto reutiliza a mesma fonte TUF pinada e patch Stage8 sem modificá-los.
3. O validador legado Stage2 importado por `src/*.cs.txt` exigia a API TUF antiga. O projeto passou a compilar **somente** as fontes Stage2 necessárias, usando `InertComposedCase` minimalista, sem reescrever o validador.
4. Diagnósticos `CA1416` do código Stage11 herdado exigiam que o assembly de CI fosse marcado Windows-only. Foi registrada `[assembly: SupportedOSPlatform("windows")]`, **sem suprimir warnings**.

**Somente a execução [40914793447](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40914793447) é a evidência final de aprovação.** As execuções intermediárias falharam na compilação e não contam como testes aprovados. Nada disso alterou as suítes históricas ou sua CI.

## 6. O que CONTINUA sem prova

1. **N1 ainda não prova TUF composto operacional:** os eventos Stage11 testados são bootstrap/begin/finish, e não um refresh com root/timestamp/snapshot/targets e release realmente autenticado; `tuf_targets_validated_for_candidate=false`. O objetivo demonstrado é a checagem de ligação entre históricos persistentes independentes.
2. **N2 não é duas instalações confirmadas:** são duas operações de fixture, a primeira **revertida** e a segunda **preparada**. A extensão lógica entre elas está coberta, mas o fluxo `selected → confirmed` e a prova externa do resultado terminal permanecem em aberto por decisão de segurança.
3. **O checkpoint é um digest em memória, não uma assinatura** nem um recibo externo. Se invasor controla todos os arquivos locais, pode reescrever os três diários + hashes; o teste não protege contra isso sem witness independente.
4. **Assinatura P-256 anterior não autentica este digest composto:** são experimentos separados. Não alegar composição verificada de raiz, chave, revogação, epoch, receipt e TUF.
5. **OperationJournal e Stage2 são domínios de escrita distintos**: ainda não existe transação atômica entre seus registros e o checkpoint externo. Não há CAS/idempotência após reboot de witness.
6. **Rotas legadas permanecem abertas nos protótipos históricos:** `FixtureSession.Begin/Confirm/Recover`, `IntegratedPipeline.Start`, `TrustIntegratedPipeline.Start` e `M3CommitGate.Confirm` continuam sem barreira G1 composta. Nenhuma foi modificada.
7. **G8 prazo e isolamento físico não demonstrados:** deadline não preempta syscall, `File.Move` em M3 pausa antes da observação externa da expiração; nem `Flush(true)` nem este teste qualificam queda elétrica, segurança NTFS contra mesmo usuário ou estabilidade de identidade sob carga tardia.
8. **G2/interop e D6-02 pendentes:** dois vetores Python/.NET continuam divergentes na referência bruta, decisão de manutenção e revisão humana qualificada não realizadas.

**Todos os gates G1–G8 permanecem BLOCKED (0/8).**

## 7. Próxima missão com ganho concreto

**Não repetir N1/N2.** Próxima frente recomendada: especificação/fixture **somente negativa** do vínculo `TUF targets autenticado ↔ digest composto` e do terminal autenticado `selected/confirmed`, sem substituir `FixtureSession.Begin/Confirm` pelo protótipo ou disponibilizar mecanismo operacional. Estudar se a testemunha precisará de segunda transição externa para ancorar resultado terminal, preservando checksum versus assinatura e idempotência durável.

Somente depois de provas negativas dessas relações, decidir o Lote 3 de barreira única para seletores/confirmadores experimentais; nenhuma rota deve ter API `CanSelect` enquanto G1/G2/G8/G5/G6 estiverem sem garantias compostas. Reservar WORK para revisão específica futura, não tarefas mecânicas.

**Preservar PR #65 Draft, main, BETA .66, telemetria desativada, infraestrutura, códigos de produção e todos os gates bloqueados.**
