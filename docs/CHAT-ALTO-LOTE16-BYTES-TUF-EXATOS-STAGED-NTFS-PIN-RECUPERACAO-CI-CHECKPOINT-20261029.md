# GERENCIADOR FINAL — CHAT ALTO — LOTE 16
## Entry TUF de laboratório: bytes exatos no staged NTFS e continuidade de pin durante File.Replace fictício
**29/10/2026 · checkpoint técnico pós-CI Windows · experimental e exclusivamente INERTE · G1–G8 BLOCKED (0/8)**

### 1. Controle inicial e escopo
- Repositório walissonminel-1991/gerenciador-final, branch experimento-g5-ipc-chat-alto-20261029.
- HEAD inicial confirmado idêntico ao checkpoint L15 d2516428bf72b1ceb34cfa2c7c270159b00a9587. O checkpoint L15 foi relido antes da implementação.
- PR #65 aberta, Draft, sem merge, head da branch work-integracao-final-20261027 fixado em ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc.
- Sem WORK; nenhuma ação em main, PR #65, BETA .66, Cloudflare Worker/D1, domínio, consentimento, telemetria, chaves de produção, PCs pessoais ou dados reais. Sem merge, release, deploy, instalador, rede operacional, atualização ou execução do GF.
- Commit técnico do L16: 1db6f8786d6fb8daed2fe038732ffb2ecdfe91c4, exclusivamente CINCO arquivos novos; nenhuma fonte preexistente do GF foi alterada.

### 2. Implementação e reaproveitamento
Arquivos novos:
1. prototipos/autoatualizador-seguro/m3-l16-signed-stage/L16SignedStage.csproj: console de TESTES .NET 10, EnableDefaultItems=false, fontes explícitas .cs.txt, IsPublishable=false, package lock Stage11; sem compilação no GF principal.
2. L16Boundary.cs.txt: boundary inerte sobre sandbox Stage2 marcado, reutiliza L15Binding.Guard, DurableEpochCache, Stage3TufBinding, WindowsFileGuard e L14Model para marcador e isolamento. Cópia do byte array completo de bundle.fixture.txt extraído do ZIP TUF verificado, sem transformação, para l16-authenticated-entry.fixture.txt no NTFS; abertura com handle NOFOLLOW e verificação de igualdade de bytes exatos, SHA e identidade NTFS. O pin permanece aberto durante File.Replace de UM ÚNICO seletor novo l16-active.fixture.json, completamente separado do seletor Stage2 e do produto GF.
3. L16Tests.cs.txt: 18 testes negativos e de continuidade em Windows, processos filhos reais com cortes/terminação, recuperação em outro processo e concorrência controlada de escrita, rename e fonte assinada.
4. README.md: premissas, alcance dos handles e limites de garantia.
5. .github/workflows/autoatualizador-m3-l16-exact-tuf-ntfs-inerte.yml: GitHub Actions windows-2022, .NET SDK 10.0.402, Python 3.13 e dependências TUF só de teste pinadas, preparação Stage8 source fixada; relatório fail-closed, só leitura do GitHub e checagem de Compile sem fontes L16 no GF.

O Stage11/Stage3 é o fornecedor da prova assinada exclusivamente de LABORATÓRIO. O L16 não implementa verificador TUF alternativo, não instala nem executa conteúdo. O seletor l16-active.fixture.json não é o seletor Stage2 original, nem o seletor de instalação GF. O receipt L16Intent registra ID da instalação, transação de laboratório, digest de entry TUF e file ID staged; é estado LOCAL, sem assinatura ou witness independente.

### 3. Fronteira de verificação e recuperação
O processo de laboratório retém handle WindowsFileGuard do arquivo staged e revalida L15Binding.Guard e bytes/identidade antes e depois de File.Replace do seletor FICTÍCIO. O alvo staged é o MESMO texto que a entry do ZIP TUF assinado para o ensaio, não o texto NEW desconexo do L14 anterior. Um lease de arquivo impede processos cooperativos de iniciar recuperação simultaneamente.

A recuperação ocorre com novo processo sob novo lease. Se intenção existe mas o registro terminal não, um seletor fictício que aponta NEW volta a OLD e a decisão é OldBlocked; reabrir não repete a transição. Registro terminal íntegro + staged/assinatura/identidade revalidados retorna apenas NewInertObservedBlocked. Falha em recibos, evidência TUF, identidade, conteúdo, entrada externa ou dados persistentes resulta UnknownBlocked. Nenhum estado autoriza READY, instalação, confirmação, execução, fallback inseguro ou elevação.

### 4. Validação CI Windows — evidência primária
- Run: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40946851963
- HEAD testado: 1db6f8786d6fb8daed2fe038732ffb2ecdfe91c4.
- Status: completed / success.
- Logs: Build succeeded, 0 Warning(s), 0 Error(s); 18 linhas PASS, 0 FAIL; relatório GF_L16_FINAL_REPORT passed=18 failed=0.
- Flags reportadas verdadeiras: signed_tuf_entry_exact_bytes_on_disk, pinned_ntfs_identity_across_one_selector_replace, real_child_process_killed, fresh_process_recovery, controlled_concurrent_staged_write_blocked, controlled_fork_and_lease_blocked, old_app_and_markers_preserved.
- Flags obrigatoriamente falsas: actual_multifile_transaction_proven, arbitrary_selector_namespace_toctou_proven_closed, independent_freshness_or_trusted_clock_proven, malicious_same_user_full_rewrite_resisted, power_loss_tested, product_updated_or_executed. operational_gates_closed = 0.

| Caso | Expectativa exercitada |
|---|---|
| L16-01 | Igualdade dos bytes exatos TUF/staged com handle NTFS e seletor antigo |
| L16-02 | Bytes staged adulterados → UNKNOWN |
| L16-03 | ZIP de fonte TUF adulterado → UNKNOWN |
| L16-04 | Entry de origem substituída por outro NTFS ID → UNKNOWN |
| L16-05 | Child morto depois de intent-flushed → recuperação em processo novo retorna OLD |
| L16-06 | Child morto após selector temp flushed → OLD |
| L16-07 | Child morto após File.Replace, antes do terminal → rollback de seletor fictício para OLD, idempotente |
| L16-08 | Child morto após terminal íntegro → apenas NEW inerte observado, idempotente |
| L16-09 | Após intenção, arquivo staged substituído por novo file ID com mesmos bytes → UNKNOWN |
| L16-10 | Symlink staged para bytes iguais é recusado |
| L16-11 | Fork do txId no intent é recusado |
| L16-12 | Fork do digest do terminal é recusado |
| L16-13 | Fork do selector é recusado |
| L16-14 | Processo concorrente tenta recuperar sob lease ocupado → UNKNOWN |
| L16-15 | Durante pin NTFS, segundo escritor tenta alterar/renomear staged: negado; child prossegue e apenas NEW inerte observado |
| L16-16 | Fonte assinada alterada após prova inicial e antes do commit: revalidação do child recusa; recuperador UNKNOWN |
| L16-17 | Comprovante de outra instalação de laboratório não é aceito |
| L16-18 | Timeout da espera não termina child; kill real e recuperação mantêm OLD |

PASS em testes negativos significa recusa adequada dentro das premissas, não ativação segura de produto.

### 5. Demonstrado / premissas / pendências
**D — demonstrado:** a entry do ZIP TUF de fixture validada foi copiada sem alteração e verificada byte a byte sob WindowsFileGuard. A identidade do staged permaneceu pinada durante a única troca fictícia File.Replace. Concorrência controlada não pôde escrever ou renomear o staged pinado. Recuperação de processos Windows distintos, rollback e observação conservadora passaram na CI com a versão antiga preservada. Nenhuma mudança no executável GF.

**H — premissas:** bootstrap TUF de laboratório confiável, verificação assinada Stage11 pré-existente, diretórios temporários controlados, ausência de adversário que controle kernel/admin ou regrave toda a base local de confiança, timestamps/caches da fixture. As garantias específicas de WindowsFileGuard não substituem uma auditoria formal de segurança.

**P — pendências não resolvidas:** pin do staged não protege TODA a namespace/ACL do seletor contra corrida arbitrária; File.Replace de um arquivo não equivale à atomicidade de uma árvore de instalação. Falha de energia física, kernel I/O tardio, storage, retentativa pós-boot, privilégios em Program Files, reparse/junction adversário privilegiado, autenticidade de binário GF, integridade completa de runtime/DLLs e provação de build/release não foram avaliados. G1 restore integral + relógio e G2 TUF interoperabilidade/D6-02 ainda bloqueados, assim como G3–G6 e G8. Nenhum READY real.

### 6. Próxima decisão
Não produzir novo lote repetindo apenas negativos semelhantes. Recomenda-se consolidação do dossiê técnico L11–L16 e revisão crítica independente do contrato de end-to-end trust, janelas TOCTOU, recuperação real e custo de manutenção. WORK deve ser chamado apenas por autorização explícita do mantenedor; não foi utilizado nesta rodada.

**ESTADO FINAL L16: implementação inerte, CI Windows aprovada 18/18, G1–G8 BLOCKED (0/8), sem alterações operacionais.**
