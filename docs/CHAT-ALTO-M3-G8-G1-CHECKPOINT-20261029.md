# CHAT ALTO — M3 / G8–G1: bloqueio de I/O tardio — 29/10/2026

## Estado verificado e linha de separação

- Repositório: `walissonminel-1991/gerenciador-final`.
- PR #65 Draft, branch `work-integracao-final-20261027`, HEAD preservado:
  `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- Branch exclusiva de ensaio: `experimento-g5-ipc-chat-alto-20261029`.
- Baseline M1/M2 recebido: `b957d92ebeff5edf5429b6e7409af2fee067b2fb`.
- SHA **M3 de código/CI para a segunda frente**: `acca88c42fcbf3fbce8d58311ecdf0fc99e2b1c8`.
- CI isolada Windows 2022 (segunda frente): [40891129725](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40891129725), **success**. Rodada anterior: [40886591578](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40886591578).
- Nenhuma compilação ou execução do Gerenciador Final, publicação, release, merge, deploy, alteração do Worker/D1, flag, telemetria ou canal privado.

## Lacuna investigada

A Etapa 5 já continha `StageDeadline` com prazo monotônico compartilhado,
cancelamento cooperativo e o contraexemplo de uma leitura de pipe sem token,
cuja conclusão tardia pode ocorrer após a espera cancelada. Isso não significava
que um **resultado tardio verificado** não pudesse ser usado para confirmar o
diário persistente da Etapa 2.

A nova subprova M3 reutiliza `../stage5/src/Deadline.cs.txt` e
`../src/*.cs.txt` (diário, seleção e recuperação da Etapa 2), mais
`../stage3/src/PipeFrames.cs.txt`. Todos são compilados somente pelo projeto
`prototipos/autoatualizador-seguro/m3/M3.csproj`, usando o lockfile original
de dependências `net10.0` em modo travado. Saídas de build permanecem em
`RUNNER_TEMP` e a CI verifica a exclusão de M3 dos Compile items do GF.

`M3CommitGate` é uma autorização experimental consumível uma vez. Antes de
`FixtureSession.Confirm`, exige o mesmo prazo compartilhado ainda válido,
frame READY com transação/nonce esperados, e transação `selected` ainda
correspondente. Uma falha consome a tentativa e não libera retry automático.

## Evidência observada

**10/10 testes aprovados; 0 falhas; 0 warnings; 0 errors** no Windows, sendo sete já validados na primeira frente e três novos cenários de escrita síncrona:

1. `M3-REAL-PIPE-LATE-RESULT-NO-COMMIT`: um `PipeFrames.Read` real
   sem cancellation token continua pendente após `ObserveRead` vencer;
   depois recebe READY válido e termina, mas `M3CommitGate` recusa
   confirmação, e a fixture recupera o antecessor sem abaixar o piso.
2. `M3-REAL-PIPE-TIMELY-READY-CONFIRMS`: READY dentro do prazo
   confirma exatamente a transação selecionada; o gate não pode ser reutilizado.
3. `M3-REAL-PIPE-USER-CANCEL-NO-LATE-COMMIT`: cancelamento do usuário
   não transforma conclusão posterior em autorização.
4. `M3-ON-TIME-READ-LATE-VALIDATION-NO-COMMIT`: uma leitura pontual
   não basta quando a validação posterior excede o prazo.
5. `M3-INVALID-NONCE-CANNOT-COMMIT`: nonce alterado e replay recusados.
6. `M3-MALFORMED-FRAME-RECOVERS-SELECTED`: prefixo de frame inválido
   provoca recusa e recuperação.
7. `M3-STALE-TRANSACTION-CANNOT-RECONFIRM`: uma transação previamente
   recuperada não pode ser confirmada por um recibo antigo.

Relatório da CI: `real_named_pipe_late_io_observed=true`,
`late_completion_blocked_from_journal=true`,
`prior_selection_recovered=true`, `operational_gates_closed=0`,
`product_built=false`, `product_executed=false`,
`physical_power_loss_tested=false`,
`synchronous_journal_write_preemptible=false` e
`independent_freshness_anchor_verified=false`.

## Segunda frente — atraso dentro da gravação síncrona e reconciliação

O M3 ganhou `m3/M3WriteRace.cs.txt`, compilado somente na suite inerte.
Um processo filho da própria suíte executa a **mesma transição de estado
selected→confirmed** que `FixtureSession.Confirm`, pela **mesma
PersistentState.Append** da Etapa 2, com os ganchos de falha já existentes
(`record-flushed` e `record-promoted`). A instrumentação de testes
replica a validação de selector e pinagem dos dois bundles; **não** altera
a implementação de produção ou a Etapa 2.

A injeção de atraso é deliberada (`Thread.Sleep(Timeout.Infinite)` dentro
de um gancho), não é uma simulação honesta da duração de um `FlushFileBuffers`
real. Um controlador no processo de testes observa a pausa, deixa expirar
um prazo monotônico, mata **somente seu próprio filho inerte**, aguarda a
saída confirmada pelo kernel e reabre o diário sob lock exclusivo.

Casos novos aprovados na CI **40891129725**, SHA
`acca88c42fcbf3fbce8d58311ecdf0fc99e2b1c8`:

- `M3-SYNC-WRITE-PRE-APPEND-TIMEOUT-RECOVERS`: interrupção antes da
  persistência; estado `selected` íntegro, `Recover` restaura o
  antecessor e preserva o piso de confiança.
- `M3-SYNC-FLUSHED-PENDING-BLOCKS`: `record-flushed` após
  `Flush(flushToDisk:true)`, antes de `File.Move`; sobra um
  `pending-*.fixture`. `Store.Load` recusa `STATE_INCOMPLETE`;
  não excluímos nem promovemos automaticamente o arquivo ambíguo.
- `M3-SYNC-PROMOTED-LATE-COMMIT-REQUIRES-REVIEW`: `record-promoted`
  após `File.Move`; o diário já contém `confirmed`, apesar do
  timeout externo. A reconciliação identifica o **commit tardio**,
  mantém os dados e sinaliza revisão obrigatória; não declara rollback,
  sucesso operacional ou autorização retroativa.

A primeira CI do novo lote [40890630970](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40890630970)
teve **9 passes / 1 falha** causada por compartilhamento transitório de
`lease.fixture` durante limpeza da sandbox após kill. Foi acrescentada
sondagem limitada de liberação do lock de instalação, antes de remoção da
fixture: se ele continuar ocupado, o teste falha. Reexecução
[40891129725](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40891129725)
aprovou **10/10**, zero warnings e zero errors. Sem supressão de
warnings ou afrouxamento dos asserts de segurança.

Propriedades **não provadas**: atraso real dentro de syscall bloqueante,
interrupção segura de commit síncrono, confiabilidade de `Flush(true)`
contra desligamento elétrico, proteção contra snapshot/rollback integral
do disco, âncora de frescor independente, equivalência entre fixture e
executável GF. Um `pending` mantém a instalação **bloqueada para revisão**;
não é recuperação automática concluída. Um `confirmed` tardio não pode
ser revertido pela máquina de estados existente, exigindo reconciliação
política separada.

Relatório: `real_sync_append_interrupted=true`,
`timeout_pre_append_recovered=true`,
`flushed_pending_refused=true`,
`promoted_after_timeout_requires_review=true`,
`operational_gates_closed=0`, `physical_power_loss_tested=false`,
`independent_freshness_anchor_verified=false`.

## Limites de segurança — gates permanecem BLOCKED

- **G8:** impedir a **inicialização** de uma confirmação depois do prazo não
  prova prazo absoluto se a própria `FixtureSession.Confirm` síncrona bloquear
  durante persistência; o ponto da checagem imediatamente anterior à escrita
  ainda tem uma janela de corrida. O teste do pipe e a recuperação manual da
  fixture não constituem preempção de I/O arbitrário do kernel.
- **G1:** nenhuma âncora externa de frescor, prova de power loss, flush físico
  ou proteção contra restauração integral de journal/selector foi obtida.
  O contraexemplo da Etapa 3 permanece válido. **Não considerar o gate G1
  resolvido** por hashes encadeados, File.Replace ou shutdown de processo.
- **G5/G6:** um Named Pipe no mesmo processo/usuário de teste não equivale
  a autenticação de cliente entre contas ou integração WinForms/worker GF.
- O runtime/instalador GF, memória/tray, transporte e BETA .66 nunca entram
  nesta subprova. O roadmap funcional continua 27/42; G1–G8, 0 homologados.

## Próximo passo do CHAT ALTO

A janela entre autorização e persistência foi demonstrada com cortes
determinísticos. Próxima investigação dirigida: examinar como inspecionar
e classificar com segurança um `pending-*.fixture` após interrupção,
sem autopromover nem excluir evidências desconhecidas, e delimitar a
necessidade de armazenamento/âncora G1 externos. Não declarar G8 fechado. Não afirmar cancelamento de
escrita kernel ou perda elétrica sem evidência física. Manter as etapas M1/M2
aprovadas, não repetir suites históricas sem regressão.

WORK apenas mediante bloqueio concreto, com SHA, falha reproduzível e ambiente
exigido. Nenhum WORK foi usado neste M3.
