# GERENCIADOR FINAL — CHAT ALTO — Lote 15
## Integração INERTE: TUF Stage11/Stage3, identidade NTFS e recuperação L14 em processos Windows distintos
**29/10/2026 · checkpoint técnico pós-CI · G1–G8 BLOCKED (0/8)**

### 1. Baseline, limites e invariantes preservadas

- Repositório: walissonminel-1991/gerenciador-final.
- Branch de testes exclusiva: experimento-g5-ipc-chat-alto-20261029.
- HEAD de entrada conferido: **c3786cf9f13aa1d312179df55f9d0a166e044b35**; compare inicial foi IDENTICAL, ahead=0, behind=0.
- L14 checkpoint relido: docs/CHAT-ALTO-LOTE14-DUAS-VERSOES-INERTES-CRASH-RESTART-NTFS-CHECKPOINT-20261029.md.
- PR #65 permaneceu aberta, Draft, sem merge, head protegido work-integracao-final-20261027 com SHA **ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc** na entrada. Não houve operação nessa PR.
- Sem alterações a main, PR #65, BETA .66, Cloudflare Worker/D1, domínio, telemetria, consentimento, chaves operacionais, computadores pessoais ou dados reais do usuário. Sem WORK, release, deploy ou atualização operacional.
- Todo o novo código reside no protótipo de testes e fonte .cs.txt explicitamente compilada, fora do GF principal.

### 2. Escopo implementado — reutilização de componentes comprovados

- **Commit técnico inicial:** 0c857e3046577b57bd4e0f1e9241802a482792c4 — cinco novos arquivos e nenhum preexistente alterado.
- Projeto: prototipos/autoatualizador-seguro/m3-l15-bound/L15Bound.csproj, net10.0, EnableDefaultItems=false, TreatWarningsAsErrors=true, IsPublishable=false, TUF Stage8 source PINADO fora do repositório e package lock já existente no Stage11.
- L15Binding.cs.txt: reconstrução de evidência local de diagnóstico por cliente que reabre a cadeia DURÁVEL Stage11 TUF com raiz pública de laboratório, reconstrói CompositeCheckpoint e Stage3TufBinding e verifica o ZIP do target. Reutiliza Stage9 OperationJournal, Stage2 intent e WindowsFileGuard para pin de identidade/bytes de arquivo candidato e ZIP do sandbox Stage11/Stage3.
- A evidência local (L15LocalEvidence) associa SourceInstallationId, SourceRoot, LabTx, digest Stage3, digest do estado TUF, zip/entry SHA256, identidade NTFS do candidato, journal Stage9, intent Stage2, hash dos textos L14 old/new. **O recibo NÃO é assinado nem uma referência independente de confiança.**
- L15BoundTests.cs.txt: testes negativos com reabertura independente, processo filho Windows pausado e morto, segundo processo recuperador, replay, modificação de byte e substituição por outro NTFS file ID com os mesmos bytes.
- README.md: contrato de isolamento, separação de marcadores e limites.
- Workflow: .github/workflows/autoatualizador-m3-l15-vinculo-tuf-ntfs-recuperacao-inerte.yml. Windows 2022, checkout sem credenciais persistentes, SDK 10.0.402, Python 3.13 e dependências TUF de fixtures já pinadas. Verificação do relatório e do vazamento de Compile para o produto.

**Não houve implementação de autoatualizador**, nova raiz/assinatura operacional, serviço, conta, comando remoto, autorização READY, helper, privilégio elevado, processo GF, atualização ou seleção real.

### 3. Contrato de isolamento: limites novos identificados pela própria CI

O WindowsFileGuard original exige FixtureSandbox.Marker do Stage2. O protótipo L14 possui **outro marcador** e, por segurança, **NÃO** foi alterado para satisfazer aquele contrato. Em L15, WindowsFileGuard abre e fixa com handle os bytes do sandbox Stage11/Stage3 (candidato e ZIP), enquanto os arquivos do sandbox L14 passam pela política própria de L14 e por checks de escopo, atributos reparse e conteúdo usando APIs gerenciadas. **Não existe pin NTFS de handle para esses arquivos L14 na nova camada**. Checar antes de abrir não fecha sozinho janela TOCTOU, especialmente com atacante ativo.

Outra distinção essencial: o target efetivamente assinado Stage11/Stage3 contém um bundle de texto de laboratório identificado como GF-U9-INERT. Os dois textos da versão OLD/NEW do L14 são outros bytes. O L15 registra um vínculo observacional entre as provas e os dois textos, mas **não há assinatura TUF autenticando diretamente os bytes da nova versão L14**. A flag de CI signed_binary_covers_l14_new_bytes é deliberadamente FALSE.

A recuperação L14 só é chamada se a evidência local reabrir e concordar. Após a retomada, ocorre nova leitura para detectar mudanças. Porém uma alteração entre o preflight e File.Replace em L14 continua possível e NÃO foi excluída em todos os entrelaçamentos. A corrida controlada L15-14 apenas testa que uma segunda inspeção rejeita uma substituição já observável.

### 4. Evidências e histórico de CI — sem esconder falhas

A implementação passou por três falhas de integração, todas mantidas no histórico GitHub. Nenhuma foi contabilizada como aprovação:

| Run | Commit | Resultado | Causa |
|---|---|---|---|
| [40943064388](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40943064388) | 0c857e30 | FAILURE | Incompatibilidade de marcadores Stage2/WindowsFileGuard vs L14, FIXTURE_SANDBOX |
| [40943366496](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40943366496) | 69d40f6b | FAILURE | Handle FileShare.None do recibo ainda aberto ao reler, IOException |
| [40943594592](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40943594592) | 07d6bd03 | FAILURE | Codec JSON divergente (camelCase StateCodec versus defaults), L15_EVIDENCE_FIELDS |
| [40943845715](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40943845715) | 2962c25a | SUCCESS | 17/17, 0 warnings, 0 errors; codec unificado |
| [40944125021](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40944125021) | 76b81693 | SUCCESS | 17/17, 0 warnings, 0 errors, com caso de adulteração L15-05 corrigido para alterar SOMENTE o digest alvo |

A correção da causa primária foi feita por commits experimentais:
- 69d40f6bd0aa44f30d64f5369519d158ce3af446 — separa os dois sandboxes sem enfraquecer marcador original.
- 07d6bd03dcb1caa2f87478c91d63600ad834df6b — fecha o writer exclusivo antes da leitura independente.
- 2962c25a5e26827bf3f349f1b467fe5ab5633596 — reusa StateCodec para recibos camelCase fechados.
- 76b81693d4ae7141562cc3f2fa7acfcec1645a17 — reusa o mesmo decoder no negativo de digest, evitando falso positivo por nulls.
- 7b51850537b5881f0f6b9b38c08d448b72e55b27 — atualiza README com limite explícito do pin NTFS entre sandboxes.

Os relatórios das duas execuções SUCCESS apresentaram GF_L15_FINAL_REPORT passed=17 failed=0 e todas as flags exigidas pelo workflow. O resultado técnico foi baseado nos LOGS dos jobs, não apenas no status verde.

### 5. Os 17 casos executados

| Caso | Asserção verificável |
|---|---|
| L15-01 | Stage11 TUF assinado de TESTE, Stage3 e candidato NTFS reabertos, recuperação inicial continua bloqueada em OLD |
| L15-02 | Child morto após intent-flushed; outro processo reconcilia OLD |
| L15-03 | Child morto após File.Replace do seletor fictício; outro processo retorna OLD com geração 3 |
| L15-04 | Child morto após terminal-flushed; outro processo observa NEW inerte sem executar; repetição idempotente |
| L15-05 | Adulteração isolada do campo Stage3Digest no recibo; recusa |
| L15-06 | ZIP de target da fixture alterado; recusa |
| L15-07 | Registro da cadeia TUF de laboratório adulterado; recusa |
| L15-08 | Último registro Stage9 OperationJournal adulterado; recusa |
| L15-09 | Conteúdo do candidato Stage3 alterado; recusa |
| L15-10 | Substituição do candidato por arquivo NTFS novo com bytes iguais; identidade detectada e recusa |
| L15-11 | Symlink criado no arquivo NEW da fixture L14; recusa por política de reparse |
| L15-12 | Recibo L15 de outra instalação LAB; recusa |
| L15-13 | Tentativa de usar recibo LAB com instalação source diferente; recusa |
| L15-14 | Child pausa após check; parent troca inode mantendo bytes; segundo check recusa, sem promoção |
| L15-15 | Timeout de espera não mata child e recuperação posterior conserva OLD |
| L15-16 | Ledger Stage2 da fonte adulterado; recusa |
| L15-17 | Após terminal do L14, conteúdo da nova fixture alterado; UNKNOWN bloqueado |

**PASS** em recusa/UNKNOWN significa que a tentativa foi bloqueada, não que houve atualização. As versões OLD fictícias e marcadores de preferências/BETA fictícios foram preservados. A verificação de seleção/rollback refere-se apenas ao seletor de texto do L14; nunca ao seletor do GF operacional.

### 6. Matriz D/H/P e gates

**D — Demonstrado:** autenticação de fixtures TUF de teste no Stage11 herdado, reconstrução de Stage3 a partir de registros persistentes, verificação de id NTFS de candidato/ZIP em processos Windows descartáveis, mortes reais de child, recuperação em novo processo, restauração conservadora do seletor fictício pré-terminal, manutenção de OLD, recusa de corrupções, replay e uma corrida controlada. CI com 17/17 aprovações, sem erros ou warnings, e zero vazamento dos fontes para o Compile do produto.

**H — Premissas:** bootstrap público inicial confiável fornecido por fixture; autoridade/editor TUF honesto; estado local de Stage2/Stage9 não é prova independente antifork; diretórios CI descartáveis; adversário não controla kernel/admin; hashes e identidade NTFS após novas leituras são observações temporais, não ancoragem externa.

**P — Pendências impeditivas:**
- G1: antirollback após restore integral do disco e relógio, independent witness e bootstrap operacional continuam sem solução.
- G2: divergências de interoperabilidade TUF Python/.NET, validação normativa e D6-02 sem homologação.
- G3/G4: target real GF assinado, artefato completo, cadeia de build/release, autenticação da versão L14 NEW ainda não fechada.
- G5/G6: identidade real dos processos, contrato IPC e READY genuíno.
- G8: TOCTOU fechado na fronteira real de ativação, reparse/ACL em Program Files e privilégios mínimos, limite absoluto e conclusão efetiva de I/O, crash de kernel, power loss, transação de árvore de arquivos e retomada em máquina real.
- Segurança e mantenibilidade da solução gratuita, sem cadastro e sem obrigações de infraestrutura, ainda não homologadas.

**Todos os gates G1–G8 seguem BLOCKED (0/8), sem exceções.**

### 7. Próximo passo de engenharia e quando usar WORK

Não aumentar artificialmente o número de casos equivalentes. Um eventual **L16** deve conectar o alvo TUF de teste **aos exatos bytes staged do seletor de laboratório**, evitando a dissociação atualmente evidenciada, e inserir revalidação tão próxima quanto tecnicamente possível de File.Replace em uma interface inerte integrada, com testes de fork/TOCTOU e custódia de handles. Não declarar atomicidade multiarquivo, e não integrar ao produto sem parecer independente.

Após essa conexão estrita, um WORK de revisão crítica com escopo restrito (refutar a validade da cadeia de evidências e localizar corridas/contraexemplos, somente leitura) poderá ser custo-efetivo, se o mantenedor autorizar. WORK não foi usado neste lote.

**STATUS: L15 inerte concluído, CI Windows validada, BETA .66 / PR #65 / main / infraestrutura intocados, G1–G8 BLOCKED (0/8).**
