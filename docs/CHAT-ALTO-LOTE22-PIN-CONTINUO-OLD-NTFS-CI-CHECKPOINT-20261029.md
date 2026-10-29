# GERENCIADOR FINAL — CHAT ALTO — LOTE 22 — CHECKPOINT TÉCNICO

**Data:** 2026-10-29  
**Estado:** **concluído somente como experimento inerte**, sem integração ou execução do Gerenciador Final real.  
**G1–G8: BLOCKED (0/8)**. O mantenedor **não aceitou formalmente riscos residuais**.

## 1. Baseline e proteção

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch experimental: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD pré-modificação verificado e sem divergência: `c4866507be7bd1395298367c88fccc42e5e87ce7` (ahead=0, behind=0).
- **Commit técnico validado:** `ec816dd84be6a606206eff7497e0401f04edc534`.
- `main` preservada em `410d65e23546976db456337c71e4705f10217d36`.
- PR #65 continua aberta em Draft, sem merge, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- BETA .66, Cloudflare, Worker/D1, consentimento, domínio, telemetria, credenciais, PCs pessoais, releases e deploys: nenhuma ação. Nenhum WORK acionado.

## 2. Arquivos e alterações

**Modificados (somente fontes de protótipo experimental):**
- `prototipos/autoatualizador-seguro/src/WindowsFileGuard.cs.txt`;
- `prototipos/autoatualizador-seguro/m3-l18-reconciler/L18Coordinator.cs.txt`.

**Novos:**
- `prototipos/autoatualizador-seguro/m3-l22-old-pin/L22Tests.cs.txt`;
- `prototipos/autoatualizador-seguro/m3-l22-old-pin/L22OldPin.csproj`;
- `prototipos/autoatualizador-seguro/m3-l22-old-pin/README.md`;
- `.github/workflows/autoatualizador-m3-l22-old-pin-inerte.yml`;
- este checkpoint.

Nenhum arquivo de TUF, código operacional GF, configuração de infraestrutura ou documentação de gates foi modificado. O código de seleção do L16, os artefatos L17 e o tratamento L18–L21 foram reaproveitados.

## 3. L22-A — proteção NTFS contínua de OLD

O caminho OLD pertence a um sandbox **diferente** do sandbox da fixture de atualização: `L14Model.Old(lab)` = `versions/old.fixture.txt`.

- O `WindowsFileGuard` passa a oferecer `OpenLabOld(lab)`, entrada com escopo fixo ao OLD de um diretório que passa por `L14Model.RequireRoot`, sem aceitar outro caminho arbitrário.
- Essa entrada reutiliza integralmente o mecanismo existente `OpenScoped`: ancoragem do caminho NTFS, handles da cadeia de diretórios, `FILE_FLAG_OPEN_REPARSE_POINT`/no-follow, verificação de tipo, volume, File ID, single-link e ACL. A rota padrão `Open` continua exigindo `FixtureSandbox.RequireRoot`.
- `L18Coordinator.Probe` abre OLD **antes** de `L15Binding.Guard`. Se a leitura inicial de OLD já divergir dos bytes esperados, recusa a observação.
- `PinnedFacts` passa a conservar **três handles** (OLD, staged, seletor), com descarte seguro mesmo sob exceções. `Recheck` confirma File ID e bytes exatos de OLD e preserva as verificações anteriores de staged e seletor até o reconhecimento de `OldVerified` ou `LocalConsistentObserved`.
- Esta alteração não prova imunidade a writers que controlem o domínio completo do mesmo usuário nem fornece witness independente. O algoritmo de NTFS é aplicado a um arquivo inerte em runner Windows descartável.

## 4. Evidências novas específicas

1. **L22-01 — OLD decision:** tentativa de alterar `versions/old.fixture.txt` por escrita **in-place** no callback `selectorObserved`, após a verificação inicial. O teste não mantém um handle seu durante o ataque. A escrita é negada pelo pin do reconciliador; retorno `OldVerified` somente com OLD íntegra, File ID conferido e canário intocado.
2. **L22-02 — NEW decision:** atualização inerte concluída, tentativa equivalente de alterar OLD durante nova decisão NEW; tentativa recusada, `LocalConsistentObserved` e OLD/canário preservados.
3. **L22-03 — pré-corrupção mantendo o mesmo File ID NTFS:** alterar bytes de OLD antes de `Reconcile` não produz `OldVerified`. Retorna UNKNOWN. O teste restaura **somente** o texto OLD da fixture para que o teardown faça suas verificações e demonstra que o bloqueio persistente continua valendo na reentrada.

Os resultados não demonstram prova contra interrupção elétrica, alteração anterior ao pin, escritor malicioso com mesmas permissões fora das condições do ensaio ou dados reais de usuário.

## 5. Cinco CIs Windows verificadas no MESMO commit técnico

| Suíte | GitHub Actions | Resultado |
| --- | --- | --- |
| **L22 novo** | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40969908173 | **3/3 PASS**, 0 FAIL, 0 warnings, 0 errors |
| L21 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40969908001 | **3/3 PASS**, 0 FAIL, 0 warnings, 0 errors |
| L20 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40969908024 | **4/4 PASS**, 0 FAIL, 0 warnings, 0 errors |
| L19 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40969908088 | **5/5 PASS**, 0 FAIL, 0 warnings, 0 errors |
| L18 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40969908097 | **7/7 PASS**, 0 FAIL, 0 warnings, 0 errors |

- Cada execução: `completed / success`, HEAD `ec816dd84be6a606206eff7497e0401f04edc534`.
- Verificados os logs de cada job: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, registros `PASS` e relatórios JSON `GF_Lxx_FINAL_REPORT` com zero falhas.
- **Total: 3 testes novos + 19 regressões = 22/22 aprovados nas condições ensaiadas.**
- `L22_FINAL_REPORT`: `no_real_gf_execution=true`, `same_user_tamperproof=false`, `independent_witness=false`, `power_loss_proven=false`, `whole_disk_restore_resisted=false`, `operational_gates_closed=0`.

## 6. Limitações e continuidade

**Demonstrado apenas em laboratório:** proteção contínua dos bytes e identidade do OLD até decisão OLD/NEW, inclusive tentativas in-place que preservariam o File ID; OLD corrompida antes da leitura não vira sucesso; canários preservados e regressões L18–L21 intactas.

**Não demonstrado:** utilização de pacote GF verdadeiro; execução de aplicativo real; identificação/autenticidade real de PE, IPC, processo e READY; proteção histórica contra restauração integral de disco/relógio; isolamento criptográfico contra mesmo usuário; ausência universal de TOCTOU, reparse/rename, I/O kernel pendente e power loss. Nenhum teste de canário fictício substitui homologação física dos dados pessoais.

**Arquitetura B+A preservada:** TUF público para autenticação de conteúdo e versões lado a lado; coordenador temporário sem elevação; recibos locais como evidências auxiliares, nunca autoridade independente.

Após este L22 experimental, avaliar planejamento de **um candidato composto em Windows descartável**, **sem integração operacional**, e reservar WORK para auditoria independente desse candidato antes de qualquer proposta de homologação real.

**Conclusão: LOTE 22 concluído como experimento inerte, CI Windows 22/22, G1–G8 BLOCKED (0/8).**
