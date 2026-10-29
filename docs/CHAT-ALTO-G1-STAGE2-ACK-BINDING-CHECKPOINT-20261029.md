# GERENCIADOR FINAL — CHAT ALTO — G1: vínculo Stage2 intent / TxProposal / recibo inerte

**29/10/2026 | Subprova isolada | G1–G8: 0/8 gates homologados**

## Estado comprovado e continuidade

- Repositório: `walissonminel-1991/gerenciador-final`
- Branch experimental: `experimento-g5-ipc-chat-alto-20261029`
- HEAD inicial validado: `77659f61d6ef2c061675372740ad6b423dddbff3`
- Checkpoint antecedente lido: `docs/CHAT-ALTO-G1-PRESELECT-STAGE2-CHECKPOINT-20261029.md`
- Código validado pela CI, **commit `5dc7fcdbcb4b01f8ba8c43947e7820ba7cfda82c`**
- **GitHub Actions Windows 2022: [40901344959](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40901344959), SUCCESS, 13/13 PASS, 0 failures, 0 warnings, 0 errors.**
- PR #65 Draft originalmente no HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; confirmar novamente após este checkpoint. Nada nesta rodada alterou a PR, `main`, BETA .66, telemetria ou infraestrutura.
- WORK usado: zero; nenhum merge, release, deploy, serviço, segredo, endpoint ou configuração remota.

## Problema que a rodada resolveu no laboratório

O experimento anterior provou `intent` persistida sem seleção de candidato, mas não ligou essa geração a uma proposta G1 nem a um recibo sobrevivente a encerramento do processo.

A nova implementação experimental em `prototipos/autoatualizador-seguro/m3-g1-ack/` acrescenta um **arquivo lateral de recibo sem semântica operacional**. O código original Stage2 (`PersistentState`, `FixtureSession`, `FixtureFiles`) e `G1Preselect.Prepare` foram **reutilizados sem alterações**. Em particular, `FixtureSession.Begin` anterior, que ainda troca o selector antes de compromisso G1 externo, **continua sem correção e fora do caminho de teste**.

### Construção da proposta vinculada

`G1AckFixture.Binding(root)` abre uma `FixtureSession` com lease exclusivo e valida o ledger Stage2 completo por `Store.Load()`. Exige:

1. A última geração estar em fase `intent` (não `selected` ou `confirmed`) e possuir transação.
2. Geração anterior obtida por caminho de nome canônico diretamente no ledger, decodificada por `StateCodec.Unwrap`, com digest igual ao `PreviousDigest` da `intent` atual.
3. Identidade da instalação, continuidade de geração, `Confirmed`, referência anterior e fase terminal/sem transação anterior consistentes.
4. Seletor ativo ainda no bundle anterior e verificação/pinagem de ambos os bundles da fixture.
5. `TxProposal.Expected` = instalação, geração, piso de sequência e digest do **predecessor estável**. `TxProposal.Next` = instalação, geração, piso e digest da `intent` Stage2. O identificador `TxId` vem da própria transação Stage2.

A geração `intent` não se torna um estado selecionável por estar referenciada no witness em memória. **É apenas uma proposta lógica de checkpoint, não uma aprovação TUF ou G1.**

### Recibo lateral com escrita interrompível no laboratório

`G1AckFixture.WriteFixtureReceipt(root,receipt,cut)`, **somente para fixtures inertes**, valida:
- campos do recibo correspondentes à `TxProposal` reconstruída;
- `TxNonce` e digest integral da intenção;
- inexistência de recibo ou `g1-ack-*` desconhecidos.

Escreve `g1-ack-pending.fixture` com `FileOptions.WriteThrough`, `Flush(flushToDisk:true)`, fecha o handle e somente então chama `File.Move` para `g1-ack.fixture.json`. Há três hooks reais: `ack-created`, `ack-flushed`, `ack-promoted`.

O envelope `G1AckBody` contém esquema, recibo, nonce Stage2, digest da `intent` e checksum local SHA256. **O checksum não autentica a testemunha**: um escritor malicioso do mesmo usuário pode construir outro envelope consistente. Essa API não deve migrar para produto; não exige assinatura nem oferece autorização de execução.

### Reconciliação após reinício

`G1AckFixture.Inspect(root,witness)` reabre a sessão/ledger, exige a mesma `intent` e consulta o estado do witness em memória. A classificação nunca altera arquivos nem retorna token de permissão:

| Estado da fixture / witness | Diagnóstico **sempre bloqueado** |
|---|---|
| Sem recibo; witness ainda na referência estável | `UnseenBlocked` |
| Sem recibo; witness comprometido para a mesma tx | `RemoteCommittedAckMissingBlocked` |
| Recibo local íntegro + mesmo registro no witness | `AckMatchesStillBlocked` |
| Arquivo `g1-ack-pending.fixture` após interrupção | `IncompleteBlocked`, **sem autopromoção/exclusão** |
| Recibo malformado, checksum inválido ou binding divergente | `MalformedBlocked` |
| Publicação conflitante, estado local incompatível, inscrição ausente ou artefato inesperado | `ConflictBlocked` |
| Witness indisponível | `UnavailableBlocked` |

Nenhum método chama `ReplaceSelector`, `Confirm`, `Recover`, `Begin` ou seleciona/lança binários.

## Testes Windows — apenas 13 casos inéditos

| Teste | Garantia demonstrada no laboratório |
|---|---|
| ACK-01 | Vínculo exato do digest da `intent` ao predecessor estável e ao `TxId` Stage2 |
| ACK-02 | Witness avançada sem ACK local: `RemoteCommittedAckMissingBlocked` |
| ACK-03 | Recibo íntegro persistido, sem possibilidade de ativação; tentativa duplicada de escrita recusada |
| ACK-04 | Timeout após compromisso witness: nenhuma gravação de ACK é inventada automaticamente |
| ACK-05 | `TxId` adulterado recusado antes de gravar sidecar |
| ACK-06 | Processo filho encerrado em `ack-created`: pending vazio preservado e bloqueado |
| ACK-07 | Processo filho encerrado após `Flush(true)`: pending completo preservado e bloqueado |
| ACK-08 | Processo filho encerrado após `File.Move`: recibo visível, mas seleção anterior mantida e classificação bloqueada |
| ACK-09 | Sidecar truncado/adulterado não é aceito |
| ACK-10 | Arquivo de ACK extra/desconhecido gera conflito, preservando evidência |
| ACK-11 | Witness offline bloqueia mesmo com ACK válido em disco |
| ACK-12 | Outra transação publica head remoto incompatível: conflito |
| ACK-13 | Sem `intent`, inspeção não inicia inscrição nem preparo |

O processo filho é **o executável do próprio teste**, iniciado somente em sandbox Stage2 marcada sob `%TEMP%/gf-u2-*`. O teste pai o encerra, espera sua saída e readquire o lease antes de examinar os arquivos. O recibo usado pelo processo filho é **deliberadamente sintético e não autenticado**; a testemunha do processo pai permanece separada em memória apenas durante o teste.

O relatório `G1_ACK_FINAL_REPORT` confirmou:
- `intent_and_stable_predecessor_bound=true`;
- `remote_ahead_without_ack_blocked=true`;
- `receipt_never_selected_candidate=true`;
- `interrupted_created_receipt_preserved=true`;
- `interrupted_flushed_receipt_preserved=true`;
- `promoted_receipt_after_child_kill_still_blocked=true`;
- `malformed_receipt_rejected=true`;
- `remote_offline_blocked=true`;
- `receipt_externally_authenticated=false`;
- `witness_in_memory_only=true`;
- `operational_gates_closed=0`, `product_built=false`, `product_executed=false`, `physical_power_loss_tested=false`, `selector_activation_supported=false`.

CI dedicada `.github/workflows/autoatualizador-m3-g1-ack-isolado.yml` valida esses campos, usa .NET SDK `10.0.402`, `windows-2022`, `dotnet restore --locked-mode`, permissões read-only e verifica que nenhum arquivo `m3-g1-ack` integra `Compile` do projeto GF. Não repetiu as suítes antigas de G1, M3 ou G8.

## Fronteiras de segurança não resolvidas

1. **Sem autenticação externa:** `TxReceipt` recebido pelo protótipo é dado de fixture; o código aceita dados estruturais sem assinatura, raiz de confiança ou epoch externo. Um invasor que controla a testemunha pode responder com uma história falsa; mesmo com ACK local correspondente, o resultado permanece bloqueado.
2. **Sem durabilidade da witness:** `G1AtomicMemoryWitness` não persiste após reboot. A fonte não é rede nem testemunha independente real e não há demonstração de resistência a rollback do witness.
3. **Sem commit físico atômico:** `Flush(true)` é chamado em arquivos inertes Windows; não há teste de queda elétrica, flush em hardware, interrupção dentro do kernel, nem contrato de transação distribuída.
4. **Sidecar fora do ledger Stage2:** o ACK não é uma geração Stage2, não tem prova de extensão integrada ao ledger nem validação por `PersistentState.Load`. Falta projetar uma cadeia autenticada entre witness e checkpoint local estável. O leitor novo inspeciona apenas seus nomes reservados; isto não demonstra resistência geral a arquivos arbitrários ou links/reparse fora do protocolo.
5. **Sem ativação:** não há fase final `selected` e a divergência estrutural de `FixtureSession.Begin` anterior continua. É obrigatório eliminar seu uso operacional antes de uma eventual integração segura.
6. **TOCTOU/mesmo usuário:** um lock cooperativo não protege contra todas as mutações fora do protocolo e o checksum do sidecar não impede reescrita coerente.
7. **G2/G5/G6/G8 separados:** assinatura TUF real, autenticação IPC, prazo absoluto e I/O síncrono continuam bloqueados.

**Status formal: G1–G8 BLOCKED (0/8 gates).** O roadmap funcional do GF não mudou.

## Próximo passo com ganho verificável

Não repetir testes de ACK/Flush em outra pasta. O próximo ponto é uma **especificação de recibo autenticado verificável e de recuperação local/externa com prova de extensão**, prevendo rotação de chave, não-equivocação e enrollment sem TOFU. Se houver implementação de fixture assinada, exigir testes negativos concretos e revisão independente do contrato antes de qualquer serviço real. Considerar uso pontual do WORK **apenas** para essa revisão especializada já bem delimitada, preservando a carga. Nenhum serviço, credencial, endpoint ou telemetria pode ser ligado sem autorização do mantenedor.
