# GERENCIADOR FINAL — Pacote de revisão crítica G1 / G2 / G8
**CHAT ALTO | 29/10/2026 | Dossiê congelado para uma única missão WORK de leitura e relatório**

## 0. Escopo imutável e regra de evidências

- Repositório: `walissonminel-1991/gerenciador-final`.
- **Branch analisada:** `experimento-g5-ipc-chat-alto-20261029`.
- **Commit-base verificado desta consolidação:** `9e616663d8574d48b122d22fc493799c75aa4c36`. Este documento e o prompt de auditoria serão commits **somente documentais posteriores**; o WORK deve conferir o HEAD efetivo e o diff antes de examinar.
- **Linha protegida:** PR [#65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65), branch `work-integracao-final-20261027`, HEAD verificado `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; usar como **referência**, sem qualquer gravação.
- Também protegidos: `main`, candidata BETA v5.13.11.66, infraestrutura Cloudflare/Worker/D1, chaves, domínio, diagnósticos, consentimento e telemetria (não ativar).
- **Natureza do mandato:** revisão técnica de arquitetura e evidências, somente leitura. Não é teste de máquina física, certificação, auditoria criptográfica formal contratada nem aceite operacional por especialista externo.
- **Status não negociável:** G1–G8 **BLOCKED (0/8 fechados)**. CI bem-sucedida não converte fixture em gate homologado, nem altera roadmap funcional. WORK **não deve** fazer merge, release, deploy, commit, comentários em PR, revisão/aprovação formal na interface, criação de Issue, contato externo ou modificação de secrets.
- **Regra de leitura:** citações com caminho, SHA e cenário. Separar (D) demonstrado por testes, (H) premissa da simulação, (P) pendente de comprovação. Nunca chamar um resultado `passed` de conclusão operacional.

## 1. Leitura mínima e ordem de trabalho

**Primeiro — contrato e gates existentes (prioritário):**

1. `docs/WORK-AUTOATUALIZADOR-ETAPA13.md` — dossiê G2 já existente; explicitamente revisão independente **não executada**. Não reescrever integralmente.
2. `docs/WORK-AUTOATUALIZADOR-ETAPA12.md` + `docs/evidencias/autoatualizador-etapa12-windows.json` + `docs/evidencias/autoatualizador-etapa12-vetores.json` — duas divergências, traces, ablação e interpretação normativa do próprio implementador.
3. `docs/evidencias/autoatualizador-etapa13-gates.json` — critérios G1–G8 abertos; mantenha cada bloqueio.
4. `docs/CHAT-ALTO-G1-MATRIZ-ARQUITETURAL-20261029.md` — hipóteses e opções G1; **propostas**, não implementação ou aprovação.
5. `docs/CHAT-ALTO-G1-ROLLBACK-EPOCA-CHAVE-CHECKPOINT-20261029.md` — última contraprova de trust epoch (CI 40903933090).
6. `docs/CHAT-ALTO-M3-G8-G1-CHECKPOINT-20261029.md` + `docs/CHAT-ALTO-M3-TOCTOU-CHECKPOINT-20261029.md` — timeout, escrita tardia, read-only revalidação e limite do lease.

**Somente sob dúvida demonstrável em código**, consultar caminhos:

- G2: `prototipos/autoatualizador-seguro/stage11/src/DurableTrust.cs.txt`, `TrustIntegratedPipeline.cs.txt`, `TrustFixtureProof.cs.txt`; `stage12/src/Tests12.cs.txt`, `stage12/reference_probe.py`, `stage12/fixtures` se existentes no commit, `stage8/tuf-delta.patch`, `stage11/fixtures/oracle-source-pin.json`.
- G1: `src/PersistentState.cs.txt`, `src/TransactionPrototype.cs.txt`, `m3-g1-preselect/G1Preselect.cs.txt`, `m3-g1-transaction/G1TransactionModel.cs.txt`, `m3-g1-ack/G1AckFixture.cs.txt`, `m3-g1-crypto/G1CryptoFixture.cs.txt`, `m3-g1-anchor/G1EpochAnchor.cs.txt` (todos sob `prototipos/autoatualizador-seguro/`).
- G8: `prototipos/autoatualizador-seguro/m3/M3WriteRace.cs.txt`, `m3/M3CommitGate.cs.txt`; `stage5/src/Deadline.cs.txt`, `docs/WORK-AUTOATUALIZADOR-ETAPA7-PRAZO-IO.md`.

**Princípio econômico:** não executar nenhuma suíte, reinstalar dependência, regenerar vetor, refazer 16/13/10 casos nem percorrer o repositório inteiro. Começar pelo dossiê e aprofundar somente onde houver pergunta sem resposta.

## 2. Evidência rastreável — o que de fato se sabe

| Frente | Prova e CI Windows verificada | Observação que importa | O que a prova NÃO cobre |
|---|---|---|---|
| **G2 — confiança persistente TUF** | Etapa 11, SHA `8dc843ff69b66f55f6e87fab93bd9c71c1242ec5`, [40859356385](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40859356385), **45/45** | Diário de raízes/papéis, pisos por época e recovery em profile fechado; **13/15** comparações brutas Python concordaram | Full conformance, auditoria de terceiros, armazenamento externo, energia |
| **G2 — divergência de rotação** | Etapa 12, SHA `eafec1070bd7c35420741bad4560dc9be4a37d6e`, [40864493929](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40864493929), **14/14 casos .NET** e 16 observações Python previstas | Python fixado mantém metadado antigo na rotação unilateral de chave de timestamp ou snapshot (root1→2, papéis100→1). Ablação controlada de invalidação conjunta permite aceitar os vetores. **Divergências da referência intacta persistem** | Conclusão independente sobre §5.3.11, perfil TUF GF amplo e adoção. `passed` aqui é reprodução da divergência, não concordância TUF |
| **G8 — escrita após prazo** | M3, SHA `acca88c42fcbf3fbce8d58311ecdf0fc99e2b1c8`, [40891129725](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40891129725), **10/10** | `pending-*.fixture` bloqueia após flush; após `File.Move` a geração `confirmed` pode já estar presente **depois do timeout** | Cancelamento de syscall síncrona em andamento, garantia de deadline absoluto ou power loss |
| **G8/G1 — TOCTOU** | SHA `f1d4480199a81ba9c1aebca2f0b37dd726f31438`, [40894656649](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40894656649), **8/8** | Outro processo não cooperativo altera arquivos mesmo com lease; altera/restaura os mesmos bytes e escapa a hash local | Proteção contra escritor malicioso mesmo usuário, uso atômico após revalidação |
| **G1 — transação** | SHA `2594eb6f02b541706e6ebceb35976688f0556667`, [40898382162](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40898382162), **16/16** | CAS em memória é idempotente para `txId`, mesmo sob resposta perdida após commit; exatamente um vencedor local concorrente | Persistência distribuída, idempotência após reboot de witness, rede |
| **G1 — preparo sem seleção** | SHA `ff5aef148c4514b136739233a77d30e2f92e5db3`, [40899933825](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40899933825), **10/10** | Nova rota experimental Stage2 grava `intent` e deixa seletor anterior, inclusive nos cortes `created/flushed/promoted` | O `FixtureSession.Begin` anterior **continua** trocando seletor antes de qualquer G1 externo; sem integração |
| **G1 — sidecar de recibo** | SHA `5dc7fcdbcb4b01f8ba8c43947e7820ba7cfda82c`, [40901344959](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40901344959), **13/13** | `TxProposal` ligado a `intent`/predecessor; recibo lateral durável ao processo, sempre bloqueado | O sidecar tem **checksum, não autenticação** nem prova de extensão do ledger |
| **G1 — assinatura e rotação** | SHA `beb565fa1e478fb7e1343667138cbecae15f3650`, [40902657571](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40902657571), **16/16** | Respostas ECDSA P-256 de chaves efêmeras, vínculo do Stage2, desafio novo e rotação assinada da época confiada **em RAM** | Não autentica o sidecar antigo; não cria witness ou raiz operacional durável |
| **G1 — rollback de chave/época** | SHA `1a203e8779601bd2d6b56998436432c9da83e24f`, [40903933090](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40903933090), **10/10** | Snapshot integral local restaura chave pública e época antigas; uma **nova assinatura** da chave antiga é válida localmente, mas anchor de memória intacto recusa | Nenhum testemunho externo real: memória é isolada **somente no harness**; um anchor comprometido pode mentir |

**G2, especificidade indispensável:** a Etapa 12 conclui que a causa nos dois vetores é a ausência de invalidação conjunta de `timestamp` e `snapshot` no caminho do Python TUF fixado, à luz da TUF 1.0.36 §5.3.11. Essa é **hipótese/avaliação do implementador respaldada por fonte e ablação**, não validação normativa independente. Não deduzir que o .NET está correto em todos os cenários nem que o Python está genericamente incorreto.

## 3. Quadro de risco que o WORK deve atacar primeiro

| ID | Prioridade proposta | Falha/hipótese | Pergunta de revisão e critério de saída |
|---|---|---|---|
| R1 | **Crítica** | **G1** — estado, raiz/época e pin público restauráveis no mesmo disco | Há algum caminho que trate assinatura correta como frescor sem witness independente? Definir trust bootstrap não-TOFU, domínio antirollback, revogação e recovery de perda da âncora. **Sem âncora real: bloqueado** |
| R2 | **Crítica** | **G1 × G8** — ordem `intent → selected` no Stage2 legado sem compromisso G1 e janela após deadline | Identificar chamadas capazes de selecionar/confirmar antes de todas as evidências. Sugerir ordem de integração segura e ponto de não retorno. **Não integrar nem liberar** |
| R3 | **Alta** | **G8** — `confirmed` persistido após timeout, kernel I/O não preemptível | Distinguir impedir **início** do commit de impedir **efeito tardio**. Especificar API de reconciliação sem mentir sobre rollback, prazo e seleção. |
| R4 | **Alta** | **G2** — 2/15 divergências brutas e interpretação §5.3.11 | Revisar raízes/épocas, piso anterior e invalidação conjunta; julgar por escopo dos vetores, não por autoridade do implementador. Propor decisão de referência e conformance pertinente, sem corrigir upstream aqui |
| R5 | **Alta** | **G1** — CAS/nonce/ACK em memória ou sidecar sem assinatura | Delimitar a passagem de observação para compromisso autenticado, prova de extensão e idempotência **durável**; identificar crash points ambíguos e risco de resposta remota falsa |
| R6 | **Alta** | **G1** — rotação da chave e epoch somente em RAM/disco | Ataques por pin antigo, mesma época com chave divergente, chave comprometida, revoked→re-enroll, concorrência e rotações cruzadas. Separar assinatura válida de identidade confiável |
| R7 | **Alta** | **G2** — patch TUF fechado/derivado sem aceite de manutenção | Verificar contrato do perfil, limites de TUF1.1.1/BC2.7.0, fixação da fonte, critérios externos, revisor qualificado e D6-02 (responsável aceita manutenção) |
| R8 | **Média-alta** | **G1/G8** — TOCTOU e `pending` desconhecido | Limites de lock cooperativo, NTFS handle pinning, flush/rename, reparse e alteração por mesmo usuário; preservar evidências, não autopromover/deletar |
| R9 | **Média-alta** | **G2/G1/G8** — efeito sistêmico e separação de gates | Definir se G2 TUF com root interno pode consumir recibo G1 externo sem ciclo circular; impedir que G1 ou G8 aprovado **por hipótese** seja usado para falsamente fechar outro gate |

Prioridades são **propostas do CHAT ALTO**, não classificação independente de exploração real. O WORK deve revisá-las criticamente, podendo reordenar com justificativa.

## 4. Formato exigido do parecer WORK (sem desenvolvimento)

Entregar **apenas relatório no chat**, com referências verificáveis. Produzir:
1. **Sumário executivo** de até 10 linhas: riscos impeditivos e se há erro demonstrável grave.
2. **Tabela de achados** com ID, severidade, caminho/símbolo/linha ou caso, SHA, cenário de ameaça, impacto, reprodutibilidade (`observado` / `inferido` / `não verificável`) e correção mínima sugerida **sem implementá-la**.
3. **Decisão G2** explicitamente separada em: conformidade dos dois vetores; divergência contra a referência Python intacta; cobertura residual do perfil; decisão de manutenção/adoção pendente. Não reivindicar sign-off.
4. **Contrato mínimo G1/G8**: ordem recomendada `bootstrap → intent → testemunha autenticada → ack/prova de extensão → selecionabilidade → confirmação/recuperação`, com as barreiras antes de cada transição, tratamento de timeout e estado `pending`.
5. **Cenários faltantes direcionados**, no máximo 5 casos novos, cada um com propriedade de segurança que as suites anteriores não provam. Distinguir teste em CI descartável de prova que exige armazenamento, energia ou domínio externo.
6. **Plano priorizado** de no máximo 5 lotes de correção futura para CHAT ALTO, com critérios de aceite verificáveis e gates dependentes; não alterar roadmap.
7. **Impossibilidades/limitações** e dependências de revisor humano qualificado, mantenedor, privacidade e aprovação real.
8. **Status final:** todos os gates permanecem BLOCKED. O parecer do WORK não constitui auditoria formal contratada nem aprovação automática de G2.

Não publicar vulnerabilidades fora desta conversa nem incluir dados de usuário/infraestrutura. Não solicitar mais permissões ou conexão por rotina. Se acesso a caminho exato falhar, registrar lacuna sem presumir código não visto.

## 5. Decisão de uso e salvamento

Este documento é um **índice incremental** que remete ao dossiê oficial da Etapa13 e às subprovas do CHAT ALTO. Evita nova rodada de desenvolvimento/CI. O prompt executável humano fica em `docs/PROMPT-WORK-REVISAO-CRITICA-G1-G2-G8-20261029.md`.

**WORK não foi acionado**, nenhum resultado independente foi recebido. O mantenedor decidirá quando consumir uma única execução. Depois, o CHAT ALTO deve inspecionar o relatório e só então decidir alterações isoladas em novas rodadas.
