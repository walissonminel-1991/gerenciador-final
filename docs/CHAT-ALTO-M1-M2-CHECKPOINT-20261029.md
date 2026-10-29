# CHAT ALTO — checkpoint verificável M1/M2 — 29/10/2026

## Referências e separação

- PR #65 Draft: `work-integracao-final-20261027`, baseline `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. **Não modificada** por estes experimentos.
- Branch experimental: `experimento-g5-ipc-chat-alto-20261029`.
- SHA **de código com CI aprovada**: `5d87a178ca2f962a5e5c53494c9aef5b43422132`.
- M1/G5 e M2/G6 foram executados pelo Chat Alto; zero utilização do Work.
- Nenhum commit no produto, `main`, beta .66, canal de entrega, infraestrutura conectada ou flags de telemetria.

## M1 — IPC, somente subprovas

Reutiliza `stage3/src/PeerIdentity.cs.txt`, `PipeFrames.cs.txt` e código de sonda anterior. Acrescentou `m1/G5Admission.cs.txt` e testes do contexto esperado: PID do kernel, SID, sessão, elevação, imagem, digest de executável **de fixture**, transação, nonce, prazo e READY de uso único.

CI Windows [40879372539](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40879372539): 12 passes, zero failures, 2 blocked. Foram exercitados pipes reais e recusas de mensagens sob **elevação herdada da CI**. A admissão G5 em token realmente não elevado e os pares entre usuários/sessões continuam bloqueados. Modelos não são handshakes entre contas.

## M2 — ciclo e diário persistente de fixture

Sondas e testes em `prototipos/autoatualizador-seguro/m2/`:

- `M2ExitBarrier`: permite avançar só depois da saída real e bem-sucedida de todos os processos inertes registrados; recusa busy, erro, duplicação, prazo e reutilização.
- `M2WinFormsProbe`: executável inerte com HWND WinForms e envio de READY após `Application.Idle` no loop de mensagens; o servidor cruza a janela com PID do processo criado e a identidade do canal.
- `M2StateBridge`: compila o **mesmo diário transacional da Etapa 2**, sem novo formato de persistência e com dependências fixadas pelos locks originais.
- `M2UnifiedLifecycle`: conecta saída real do antecessor de teste, transação `intent → selected`, prontidão com transação/nonce derivado e janela real, confirmação no diário enquanto o filho está vivo, ou recuperação do antecessor com piso de confiança retido. Falha de transporte antes da confirmação encerra primeiro a sonda inerte e recupera a transação; falha depois da confirmação não provoca rollback artificial.
- Os testes de recuperação também matam um **filho inerte criado pelo próprio harness** após persistir `selected` e reabrem o estado em novo processo. Não simulam perda elétrica.

CI Windows **[40885330176](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40885330176)** no SHA `5d87a178ca2f962a5e5c53494c9aef5b43422132`:
- M2 ciclo/Named Pipes/WinForms: **11/11 passed**.
- M2 diário/recuperação + fluxo unificado: **8/8 passed**, incluindo normal, nonce incorreto, transação incorreta, sem READY e desconexão antecipada do pipe.
- Três compilações: **zero warnings e zero errors**.
- `unified_winforms_journal_confirmed=true`; `unified_negative_ready_rolled_back=true`; `unified_no_ready_rolled_back=true`; `unified_disconnect_rolled_back=true`; `unified_non_elevated_admission_verified=false`.
- `product_built=false`, `product_executed=false`, `simulated_power_loss=false`, `hardware_failure_tested=false`, `operational_gates_closed=0`.

O runner elevado **não** equivale a aprovação da política não elevada; a sonda de teste não é o GF; HWND não prova saúde funcional do produto. Digest do apphost da fixture não é identidade de pacote GF autenticado/pinado. A recuperação é do diário de fixture, não do instalador Windows operacional.

## Gates e próximos passos

Os oito gates G1–G8 continuam **blocked**, nenhum homologado. O roadmap funcional do GF continua **27/42**, independente dos gates. G5 ainda precisa de handshakes reais em ambiente descartável não elevado, entre usuários e sessões distintos; G6 requer descoberta/encerramento reais de instâncias, tray, workers/manutenção e pacote/autorização GF. G2 continua pendente de revisor TUF independente.

**Próxima ação do CHAT ALTO: M3 dirigido (G8/G1).** Inspecionar `stage5/src/Deadline.cs.txt`, os caminhos síncronos de I/O, cancelamento e recuperação; selecionar um único caso de efeito tardio/armazenamento com teste negativo reproduzível antes de escrever mais código. Não reexecutar as suítes históricas nem confundir parada de processo com corte de energia. Não presumir acesso a disco persistente de laboratório nem testemunha de frescor externa.

**WORK**: não convocar por hábito. Só preparar prompt com SHA e critérios precisos se for disponibilizado um ambiente Windows descartável realmente capaz de autenticar outros usuários/sessões, ou se existir falha reproduzível impossível de analisar com Chat Alto + GitHub Actions. Não criar contas, alterar ACL, token, UAC, trust store ou infraestrutura por iniciativa própria.

Nenhum merge, release, deploy, publicação, ativação de telemetria ou integração de autoatualizador no GF está autorizado por este checkpoint.

## Observação sobre a desconexão (M2/G8)

O teste `M2-UNIFIED-DISCONNECT` usa EOF real antes de READY. Foi aprovado no
Windows, demonstrando recuperação da seleção pendente da fixture após saída
do filho. Isso **não** garante cancelamento de I/O síncrono bloqueado nem um
prazo global sobre TUF→ZIP→PE→confirmação; esses permanecem no escopo M3/G8.
A simulação de encerramento de processo não equivale a queda física de energia
ou garantia de frescor independente (G1).
