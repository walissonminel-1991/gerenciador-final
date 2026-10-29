# GERENCIADOR FINAL — Lote 1 pós-auditoria WORK: contrato de checkpoint composto G1/G2/G8
**CHAT ALTO · 29/10/2026 · ADR de projeto, estritamente NÃO operacional · decisão de desenho proposta**

## 0. Baseline, proibição de integração e epistemologia

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch de documentação: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial e código experimental congelado confirmados: `7da46d4943a4c5b9a306bb3a14a1e77bc5b6b6ef` e `9e616663d8574d48b122d22fc493799c75aa4c36`, respectivamente.
- PR #65 Draft em `work-integracao-final-20261027`, HEAD observado: `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. `main`, BETA .66, telemetria e infraestrutura não entram no escopo.
- Fontes principais: `docs/CHAT-ALTO-DOSSIE-REVISAO-INDEPENDENTE-G1-G2-G8-20261029.md` e parecer WORK G1/G2/G8 fornecido ao CHAT ALTO. A segunda leitura automatizada não é parecer humano qualificado externo.
- **Natureza do lote:** contrato, invariantes, tabela de falhas e inventário de rotas; **nenhuma modificação de C#, testes, vetores, pipeline, CI ou produto**. Sem autorização de criar witness, segredo, endpoint, deploy, release, merge ou operar GF.
- **G1–G8 BLOCKED, 0/8 gates homologados.** D = demonstrado em fixtures/código; H = hipótese de modelo; P = pendência para integração/homologação. O texto abaixo distingue contratos *propostos* das propriedades implementadas.

## 1. Achados do WORK aceitos e delimitações

**D — falhas/limites existentes:**
- Etapa 2, `src/TransactionPrototype.cs.txt:81–111`: `FixtureSession.Begin` grava `intent`, em seguida `Files.ReplaceSelector` antes de qualquer compromisso testemunhado G1; `Confirm` verifica `FixtureReadiness` (txId/nonce/candidateSha256), sem witness/guard G8. `Recover` também pode escrever seletor. Estão isolados do executável do GF.
- Etapas 9/11 chamam `Begin`/`Confirm` diretamente; o M3 `M3CommitGate` verifica deadline antes, mas a syscall síncrona de confirmação não é preemptível pela checagem.
- A Etapa 11 tem diário TUF `DurableEpochCache` **separado** do `PersistentState`/seletor e do `OperationJournal`; `TrustIntegratedPipeline` exige metadados legados vazios, não migra fontes automaticamente.
- `m3-g1-transaction` modela CAS e idempotência **em memória**; `m3-g1-ack` escreve sidecar de **checksum**, enquanto `m3-g1-crypto` autentica uma **mensagem distinta** com chaves efêmeras. A existência de ambos não autentica o sidecar.
- `m3-g1-anchor` demonstra que restaurar a chave pública e a época no mesmo disco permite assinatura nova pela chave antiga; um anchor de memória fora do snapshot recusa **somente sob hipótese de independência**.
- No M3, o registro `confirmed` pode estar presente quando o supervisor constata timeout; `record-promoted` pausa **depois** do `File.Move` e NÃO demonstra que o rename ocorreu após a expiração.
- Na Etapa 12, duas diferenças entre .NET/Python fixado persistem na referência bruta, apesar da interpretação favorável, limitada aos dois vetores, da TUF 1.0.36 §5.3.11.

**P — ainda NÃO provado:** chave/identidade de witness provisionada independentemente, monotonicidade pós-reboot, idempotência durável/contra operador comprometido, ligação autenticada a G2, prova de extensão composta, autorização única, prontidão G5/G6 real, prazo absoluto para efeitos em syscall, recuperação pós-falha elétrica, aceite D6-02 e revisão humana.

## 2. Duas raízes e uma regra anti-ciclo

**Contrato proposto:** separar e provisionar previamente **dois vínculos de confiança**, sem que um se autorize exclusivamente pelo outro:

1. **Raiz editorial TUF (G2)**: autoridade para autenticar metadados `root/timestamp/snapshot/targets`, referências e conteúdo do pacote. Persistir e evoluir sua política/época em diário próprio. **Não** permite, por si só, afirmar frescor de uma instalação cujo disco foi integralmente restaurado.
2. **Raiz/identidade de testemunho G1**: autoridade *independente* para atestar checkpoint monotônico de uma **instalação + repositório**; não escolhe, não assina editorialmente nem substitui metadados de release TUF.
3. **Ligação local G8**: emissor/coordenador autorizado, prazo de admissão monotônico e estado real do processo (G5/G6) são condições separadas **necessárias**. Não derivar prazo sobrevivente a reboot de um timestamp de recibo.

Uma raiz G1 entregue **somente** pelos próprios metadados TUF que dependem de G1 para detectar rollback cria **ciclo de bootstrap**; reciprocamente, uma witness não deve substituir a assinatura editorial TUF. Se binário, TUF root e G1 key estiverem todos restauráveis na mesma imagem, continua faltando âncora externa. **Nenhum mecanismo externo é escolhido ou criado neste lote.** Enrolamento automático de `InstallationId` desconhecido é proibido para adoção futura; recuperação após perda/revogação exige autoridade independentemente validada e decisão do mantenedor.

Separar os contadores: `TufEpoch` (rotação de chaves timestamp/snapshot), `WitnessKeyEpoch` (rotação da chave de recibo), `WitnessSequence` (checkpoint monotônico) e `Stage2Generation` (eventos locais). Esses números **não são intercambiáveis**. Também separar piso dos metadados TUF, sujeito a resets normativos por época, de `releaseFloor` funcional, que não pode recuar por esse reset.

## 3. Proposta de checkpoint lógico imutável (formato NÃO implementado)

Uma revisão posterior pode alterar os nomes, mas **não pode omitir as relações abaixo sem justificar ameaça e prova alternativa**.

`GF-U2-COMPOSITE-CHECKPOINT/PROPOSED-1` (separação de domínio no hash; codificação canônica e política de algoritmos ainda pendentes):

| Grupo | Campos obrigatórios propostos | Por quê / regra |
|---|---|---|
| **Domínio** | `schema`, `purpose=commit-not-execution-permit`, `protocolVersion`, `repositoryIdentity`, `tufAnchorIdentity`, `witnessAuthorityIdentity` | Evitar cross-protocol/cross-repository; âncoras não inferidas de resposta |
| **Instalação** | `installationId` vinculada à inscrição autenticada; `enrollmentEpoch` irreutilizável sem recuperação | Impede reset por reinstalação com identificador gerado do disco revertido |
| **Relação witness** | `expectedWitnessSequence`, `expectedWitnessCheckpointDigest`, `nextWitnessSequence`, `txId` | CAS atômico e idempotente por (instalação, txId, payload); sequência externa não é geração Stage2 |
| **TUF estável verificado** | `TrustView.Digest/Sequence`, `RootVersion`, `TufEpoch`, `timestamp/snapshot/targets versions`, digests da cadeia/metadados canônicos, digest da política de chaves, referência assinada de `targets`, expiração relevante | Vincular **o diário TUF de Stage11** ao ato; nunca confiar só em `StateRecord.Trust` legado nem em cache mutável não verificado |
| **Operação** | `OperationJournal.OperationId`, digest do `OperationRecord` autorizado/preparado, `InertPermit.CacheSha256` e hashes do payload/manifest/entry; mapeamento obrigatório `operationId ↔ txId` | Evitar pacote/identidade trocados entre três diários |
| **Predecessor local** | `previousSettledStage2Digest`, `previousConfirmedBundle`, **prova de extensão desde o intent ancorado anterior** e fase terminal anterior | Conectar múltiplas operações sem usar igualdade de gerações |
| **Intent atual** | `intentStage2Digest`, `intentGeneration`, `transactionId`, `stage2Nonce`, `previous/candidate BundleRef`, `candidateContentDigest`, `candidateIdentity` no momento da pinagem | Recibo deve ligar os **mesmos bytes e identidade** da proposta e deixar o seletor anterior intacto |
| **Pisos** | `releaseFloorBefore`, `releaseFloorAfter`, `trustPolicyDigest`, `observedUtc` (não substitui relógio monotônico) | Sem rebaixamento silencioso do releaseFloor em reset de metadados |
| **Política de prazo** | `attemptId`, `deadlinePolicyId`, limite de admissão e política de expiração de prova | Apenas contexto; autorização depende do **deadline vivo do processo**, não de relógio em disco |

`CheckpointDigest = SHA256(domainSeparatedCanonicalBytes(checkpoint))`. Proibido confiar em assinatura sobre uma estrutura *diferente* de `CheckpointDigest`. A resposta externa hipotética é uma mensagem distinta `SignedCommitReceipt` que **assina** pelo menos domínio, testemunha/época/chave, instalação, repositório, `txId`, nonce do desafio, digest exato do checkpoint, seq anterior/próxima e status `committed`; sua verificação exige chave previamente autorizada, estado externo monotônico e desafio atual. Recibo, pin público local ou checksum **não são a fonte do piso de frescor**.

**Pendente antes de código:** definir parser e codificação exata, terminologia das provas de TUF (bytes signed vs wrapper), modelo de igualdade/canonicalização da identidade do pacote após reboot, política de múltiplas roots, limitação de tamanho, período de validade e semântica de confidencialidade; nada foi implantado nem criptografado neste lote.

## 4. Invariante central: head lógico ≠ último número do journal

Para cada transação `k`:

- `H_(k-1)`: **último checkpoint externo comprometido** e autenticado, vinculado ao `intent` de operação anterior ou ao bootstrap inicial.
- `L_(k-1)`: head Stage2 **local terminal** (`confirmed` ou `rolled_back`) que deve ter **caminho contínuo válido** desde o `intent` que `H_(k-1)` autenticou. Essas gerações locais posteriores são **extensões**, não outro compromisso externo.
- `I_k`: novo `intent` Stage2, criado a partir de `L_(k-1)`, com seletor antigo. `T_k`: head do diário de confiança TUF validado e vinculado ao checkpoint (pode ter avançado desde a operação anterior, mas a extensão precisa de validação TUF independente).
- `C_k`: proposta imutável que inclui `H_(k-1)`, `L_(k-1)`, prova de extensão, `T_k`, `I_k`, operação, candidato e releaseFloor. **O witness confirma `C_k`, não `L_k` nem o estado local mais recente.**
- `H_k`: recibo externo durável autenticado sobre `digest(C_k)` com `witnessSequence(k-1)+1`.
- `L_k`: evolução local após confirmação do checkpoint: `I_k → selected → confirmed/rollback...`, cujo resultado também deve ser preservado e conciliado, não inferido de `H_k`.

**Admissão da operação `k+1` exige provar `H_k → I_k → L_k → I_(k+1)` como encadeamento local completo**, comparando *os digests dos marcos* (e não números de geração iguais), confrontando a última confiança TUF com `T_k`, seus avanços normativos e os pisos externos/locais. Se `L_k` é `pending`, inconsistente ou perdeu proveniência, **bloquear**. Se a witness tem `H_k` mas Stage2 antigo foi restaurado, não criar `C_(k+1)` usando `H_k` por mera equivalência do candidato.

**Questão difícil, ainda P:** um `H_k` ancorado só em `intent` não atesta o resultado final `confirmed`. Uma proposta seguinte precisa de prova da transição local e de definição de como/onde a witness passa a conhecer **o terminal**; caso um adversário possa reescrever um terminal inteiro coerente, uma cadeia local apenas não resolve rollback de terminal. Antes de alegar proteção contra rollback **entre duas instalações**, estudar uma segunda transição externa autenticada ou um checkpoint que inclua o resultado terminal, sem transformar o sucesso em suposição. O contrato deliberadamente **não** declara resolvido esse problema.

## 5. Invariantes de segurança para futura implementação

| ID | Regra obrigatória; estado desta rodada |
|---|---|
| C1 | **Autoridades separadas**: G2 autentica release; G1 testemunha história; G8 limita admissão/efeitos; G5/G6 autenticam prontidão. Proposta, **P** operacional. |
| C2 | **Inscrição sem TOFU** e pin da witness fora do conjunto restaurável junto ao verificador; identidade e revogação não podem ser resetadas pela instalação. **P**. |
| C3 | **Digest composto canônico** liga repo/TUF metadata-root-epoch, operação, Stage2 predecessor+intent, txId, candidato, piso e testemunha. Modelo proposto, **P**; contratos anteriores demonstraram campos isolados. |
| C4 | **Uma única barreira de seleção/confirm**: nenhuma rota direta `Begin/Confirm/ReplaceSelector` fora do coordenador de evidências; preselect inerte não concede seleção. Falha arquitetural D, correção **P**. |
| C5 | **Nenhum bypass com G1 indisponível, inválido, expirado, sem inscrição ou com cabeça divergente**. Bloquear só autoatualização dependente. Modelo D/H, produto **P**. |
| C6 | **Pending é evidência**: não apagar/promover por plausibilidade nem por checksum; crash remotahead/local-ahead e fork preservam histórico e bloqueiam até reconciliação. Fixtures D; integração **P**. |
| C7 | **CAS/idempotência pós-reinício**, incluindo body diferente no mesmo txId; remoto assina **digest(C_k)** e recibos persistentes. Memória D; durável **P**. |
| C8 | **Separar `witnessSequence` de `Stage2Generation`** e exigir prova de extensão local/TUF; duas transações válidas não devem conflitar por simples diferença de geração. Contrato **P**. |
| C9 | **Tempo único de tentativa**: não reiniciar orçamento em refresh, preparo, extração, seleção; timeout fecha admissão, mas efeitos iniciados precisam de reconciliação explícita. M3 D, prazo absoluto físico **P**. |
| C10 | **No fail-open after signed equality**: recibo válido/assinatura P-256 são evidência necessária, nunca `CanRun` enquanto os demais gates não estiverem cumpridos. Fixtures D; produção **P**. |
| C11 | **Pisos funcionais preservados** em resets TUF legítimos (§5.3.11); rotação de metadados não constitui downgrade do release GF. G2 casos D; adoção **P**. |
| C12 | **Até o último uso**: identidade do pacote e dos processos verificada sob handles/namespace protegido; lease cooperativo não cobre mesmo-usuário malicioso. TOCTOU D; proteção integrada **P**. |

## 6. Sequência de estados e matriz de resultados desconhecidos

Os estados abaixo são **requisitos propostos**, não `enum` de produto:

| Fase | Condições para entrar / sair | Em crash/timeout: resultado obrigatório |
|---|---|---|
| `NotEnrolled` | Raízes/instalação não provisionadas; G2 e G1 necessitam autoridades distintas | **BLOCKED**, não criar novo enrollment por TOFU |
| `StableReconciled` | Head externo autenticado + extensão local terminal validada + TUF verificado + sem `pending` | Sem consulta válida ou passado duvidoso, **BLOCKED**, não admitir |
| `IntentPrepared` | Consentimento/política; pacote TUF validado; operação+intent Stage2 persistidos; seletor velho | Pending local: `UnknownLocalWrite`, preservar arquivos; não selecionar |
| `PublishInFlight` | CAS remoto sobre `H_(k-1)`, txId/payload idempotente autenticados | Timeout **UNKNOWN_REMOTE**, consultar mesmo txId; não deduzir falha |
| `WitnessCommitted` | Recibo autenticado durável sobre `C_k`; witness external head `H_k` | `remote-ahead`: bloquear e reconciliar; não recuar witness, não autopromover sidecar |
| `AckLocallyBound` | ACK da mesma mensagem verificado no journal, extensão confiável, root/epoch e candidato rechecados | Ack parcial/sem assinatura/antigo: **BLOCKED**, sem seleção |
| `Selectable` | **Todas** as provas + gate G8 ainda vivo; G5/G6 reais e demais gates prontos; autoridade única admite ação | Deadline expirado: não iniciar novo efeito; syscall já iniciada pode produzir resultado tardio **UNKNOWN** |
| `SelectedPendingReadiness` | Seletor mudou sob guarda única após admissão; prontidão vinculada a processo/candidato/txId | Timeout ou dúvida sobre processo: impedir confirmação; observar participantes e registrar evidência; não deduzir rollback automático |
| `ConfirmedOrRollbackTerminal` | Confirmação real autorizada ou rollback previsto + provas de extensão sem perda de piso | Se confirmado depois do timeout externo: `LateEffectReviewRequired`, jamais sucesso presumido |
| `NextOperationEligible` | Provar continuidade de `H_k`, `I_k`, `L_k`, extensão TUF e condições de nova operação | Falta de prova terminal/âncora restaurada: **BLOCKED**, não utilizar `Stage2Generation==WitnessSequence` |

**Pontos distintos de não retorno:** (1) CAS externo gravado, que não se desfaz por cancelamento; (2) troca efetiva de seletor local, com efeitos próprios; (3) `confirmed` persistido. **Nenhum** desses pontos pode ser confundido com resposta recebida, prazo vencido ou sucesso funcional do candidato. Não declarar que a operação `File.Move` no M3 ocorreu depois do deadline: ela foi observada **antes da pausa de teste**, enquanto o supervisor viu o timeout depois.

## 7. Inventário verificado de rotas de seleção/confirmação e recuperação

Verificação estática no SHA técnico `9e616663d8574d48b122d22fc493799c75aa4c36`. Cobertura: fontes de execução experimental `src`, `stage9`, `stage11`, M3 e G1; **não equivale a varredura de todo o produto ou de cada harness de testes**.

| Rota | Chamadas / linhas de referência | Efeito e lacuna | Exigência do Lote 2 |
|---|---|---|---|
| **Primitiva `FixtureFiles.ReplaceSelector`** | `src/TransactionPrototype.cs.txt:41` | Escreve seletor; não consome prova G1/G8 | Capacidade restrita ao único coordenador, sem API livre de seleção |
| **`FixtureSession.Begin`** | `src/TransactionPrototype.cs.txt:81–100`, em particular `ReplaceSelector:97` | Após intent, seleciona candidato sem witness; checa tempo/expiry mas não G1 | Dividir prepare não selecionável de etapa autorizada; remover bypass |
| **`FixtureSession.Confirm`** | `src/TransactionPrototype.cs.txt:102–111` | `FixtureReadiness` sintética + seletor bastam; sem G1/deadline | Exigir prova composta e READY real/autenticado |
| **`FixtureSession.Recover`** | `src/TransactionPrototype.cs.txt:112–135`, `ReplaceSelector:129` | Pode reverter seletor e anexar gerações; exige proteção de recovery e política sobre remoto comprometido | Recuperação sem rebaixar witness/releaseFloor, com estados desconhecidos bloqueados |
| **`IntegratedPipeline.Start` (Etapa 9)** | `stage9/src/IntegratedPipeline.cs.txt:140,142` | Invoca diretamente `Begin/Confirm` após preparo | Retirar caminho alternativo ou torná-lo fixture read-only até gate composto |
| **`IntegratedPipeline.Recover`** | `stage9/src/IntegratedPipeline.cs.txt:150–168` | Após `ExitProof`, chama `Recover` ou apenas lê seleção; classifica `confirmed` | Sem recovery implícito diante de `remote-ahead/pending`; não legitimar confirmed tardio |
| **`TrustIntegratedPipeline.Start` (Etapa 11)** | `stage11/src/TrustIntegratedPipeline.cs.txt:71,75` | Apesar de `DurableEpochCache` e `OperationJournal`, chama mesmas primitivas legadas; G1 e G8 não compostos | Vínculo do `TrustView.Digest`/operation digest + seleção somente por coordenador |
| **`TrustIntegratedPipeline` catch** | `stage11/src/TrustIntegratedPipeline.cs.txt:78–82` | Em exceção síncrona conhecida usa `Recover`; **não** generalizar para crash arbitrário | Não alargar rollback automático para falha ambígua |
| **`M3CommitGate.Confirm`** | `m3/M3CommitGate.cs.txt:13–29` | Gate consome tentativa e deadline antes de chamada síncrona a `Confirm`; não a preempta, não tem G1 | Fazer autorização atomicamente vinculada à fase real + estado desconhecido |
| **`M3TimeoutReconciler.InspectAfterExit`** | `m3/M3WriteRace.cs.txt:16–43` | Após processo sair, pode executar `session.Recover` ou sinalizar `LateConfirmedReviewRequired` | Reconciliar com testemunha remota e política antes de qualquer mutação |
| **`G1Preselect.Prepare`** | `m3-g1-preselect/G1Preselect.cs.txt:13–50` | Alternativa **inerte** que só grava intent, **não** seleciona | Preservar como prova de ordem, não promover a API de permissão |
| **`G1AckFixture.WriteFixtureReceipt/Inspect`** | `m3-g1-ack/G1AckFixture.cs.txt:91–169` | Sidecar sem assinatura, status sempre bloqueado | Não adotar como autorização; vincular recibo assinado exato em futuro contrato |
| **`G1PinnedFixtureVerifier.Verify`** | `m3-g1-crypto/G1CryptoFixture.cs.txt:80–109` | Aceita assinatura válida para diagnóstico `SignatureMatchesButBlocked` | Não transformar retorno em `CanSelect` sem G1 independente |
| **`G1TransactionReconciler.Inspect`** | `m3-g1-transaction/G1TransactionModel.cs.txt:111–157` | CAS+reconciliação simulados; sem durabilidade/autenticação | Usar só modelo de estados; não confundir com prova persistente |
| **`G1EpochReconciliation.Inspect`** | `m3-g1-anchor/G1EpochAnchor.cs.txt:121–143` | Comparação com anchor externo **apenas em memória** | Não aceitar pin local como fonte independente do próprio rollback |

**Nota de escopo:** os arquivos de teste podem construir selectors/fixtures e chamar primitivas diretamente para simular ataques. No Lote 2, além das rotas de runtime listadas, será preciso auditar os **call sites que compilam no produto futuro**, resolver visibilidade/interface e impedir bypass por APIs legadas. Uma verificação de `Compile` da CI mostra isolamento anterior, não elimina futuras rotas por inclusão indevida.

## 8. Matriz de testes negativos futuros (NÃO executados nesta missão)

| ID | Nova propriedade/ataque, não simples repetição | Falha objetiva | Ambiente |
|---|---|---|---|
| **N1** | Restaurar **apenas diário TUF** mantendo Stage2 intent, sidecar e witness checkpoint; metadados restaurados ainda autenticam individualmente | Qualquer admissão/seleção aceita `TrustView.Digest`/epoch não vinculados ao checkpoint | CI Windows descartável; metadados/chaves de teste |
| **N2** | Executar **duas transações sucessivas** (intent→selected→confirmed→intent), com gerações Stage2 extras e witness contador contínuo | Segunda transação legítima falha por `gen != witness seq` ou aceita sem extensão ao terminal | CI Windows, fixture inerte; sem binário |
| **N3** | Produzir `C_k` com candidato/pacote/manifest correto mas `OperationRecord` de outra transação ou `TUF root` de outro repositório | Digest composto considera válido o checkpoint por colisão de autoridades/identidades | CI Windows, fixtures sintéticas |
| **N4** | Resposta remota perdida **após** CAS, seguidas de novo coordenador e dois candidatos concorrentes sobre mesmo predecessor | Um txId contraditório ganha permissão, ou o local avança sem recibo autenticado | Modelo CI de protocolo, **não** idempotência real de witness pós-reboot |
| **N5** | Expirar o prazo antes de liberar `record-promoted`, observar ordem temporal instrumentada em vez de usar timeout posterior a rename | Resultado tardio é promovido a permissão ou evidência é apagada; distinguir tempo de rename versus observação | CI com barreiras; não comprova preempção kernel |
| **N6 (externo)** | Commit + perda de resposta + reinício **real** da testemunha externa; recuperação de `txId`/epoch com storage independente | Replay, dupla publicação, perda de recibo, esquecimento de revogação | Ambiente externo descartável ainda **não autorizado** |
| **N7 (físico)** | Desligamento elétrico durante flush/rename de diário/selector | Aceitação de estado parcial ou rollback do piso | Laboratório físico descartável ainda **não autorizado** |

**Prioridade imediata autorizável em uma próxima missão: N1 e N2, após congelar schema/política do checkpoint.** N3–N5 somente se agregarem prova; N6/N7 exigem decisão e ambiente independentes. Não reexecutar as suítes históricas G1 ou G2 sem motivo.

## 9. Condições de saída do Lote 1 e bloqueios do Lote 2

**Este lote será concluído documentalmente** quando contrato, lista de rotas e testes negativos estiverem salvos/inspecionados. Isso não implementa C1–C12.

**Pré-requisitos para iniciar código do Lote 2 em ramo isolado:**
1. Decisão entre `trustRootAnchor` e `witnessRootAnchor` sem ciclo de bootstrap, e classificação explícita do que é simulação.
2. Escolha de `checkpoint` lógico capaz de ligar `DurableTrust.TrustView.Digest`, `OperationRecord` e `Stage2 intent` sem equivalência artificial de gerações.
3. Prova negativa N1 e N2 antes de permitir qualquer `selected`; se esses testes não forem possíveis sem chave externa, **manter seleção bloqueada**, não forçar skip.
4. Inventário e bloqueio de todas as chamadas legadas de `Begin/Confirm/ReplaceSelector`, inclusive recuperação. Não usar hacks de versão ou flags para contornar prova.
5. Prazo G8 exige admissão ainda vigente, mas mutação síncrona pode ter resultado ambíguo; contrato de reabertura/quarentena deve preservar evidências e não deduzir rollback.

**Decisões pendentes do mantenedor antes de qualquer adoção:** adversário real (mesmo usuário/admin/disco), modalidade de witness (online/manual/hardware), privacidade/consentimento, recuperação de inscrição e perda de chaves, custos, revisor humano qualificado, D6-02 e homologação de armazenamento e IPC.

**Status final:** G1–G8 BLOCKED (0/8). Este ADR não altera código, workflow, executável GF ou infraestruturas.
