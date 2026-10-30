# GERENCIADOR FINAL — MISSÃO ÚNICA DE CONCLUSÃO DO AUTOATUALIZADOR
## Fase 1 — Primeiro ensaio composto com os bytes reais do GF — checkpoint parcial

**2026-10-29 · experimental · Windows descartável GitHub Actions · FASE 1 AINDA ABERTA**

### 1. Regras e contexto
- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusivamente experimental: `experimento-g5-ipc-chat-alto-20261029`.
- Baseline obrigatória: `352293ab4431f4643feee406699c5d1cb308d78b`. HEAD verificado idêntico no início, ahead/behind 0.
- Commit inicial de fontes de ensaio: `5b12a59189a00ef32acd68a7936c84b6e590f40b`.
- **Commit técnico aprovado:** `1292e76bbd69b06a1a1b727bc35132ad7eb17f6e`.
- Main `410d65e23546976db456337c71e4705f10217d36` preservada.
- PR #65 permanece aberta/Draft no HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`, sem merge.
- BETA .66 distribuída, telemetria conectada, Cloudflare, D1, domínio, credenciais, produção, PC FORTE, PC FRACO e distribuição: inalterados.
- Sem WORK, merge, release, deploy, ativação de telemetria ou integração operacional. **G1–G8 BLOCKED (0/8), riscos residuais não aceitos.**

### 2. Arquivos experimentais adicionados
- `prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py`.
- `prototipos/autoatualizador-seguro/phase1-composed/README.md`.
- `.github/workflows/gf-autoatualizador-fase1-real-lab.yml`.
- Este checkpoint. Nenhum arquivo do código do produto `GerenciadorAplicativos.csproj`, `Program.cs` ou das PRs de integração foi modificado.
- O ensaio **não é um novo motor de autoatualização operacional**: é um adaptador temporário de CI para demonstrar composição com bytes reais de GF enquanto L1–L22 permanecem como base de contratos/proteções.

### 3. Ciclo executado no runner Windows
1. Compilou **duas variantes reais de GF** a partir do mesmo código `GerenciadorAplicativos.csproj` e SDK .NET 10.0.402, com `GfBetaProfile=local` e alterações **efêmeras** do metadado `Version` para 5.13.11.65 e 5.13.11.66. Não são versões publicadas; fonte idêntica e byte-diff de `GerenciadorFinal.dll` demonstram builds distintos para simular o ciclo.
2. Cada diretório de distribuição tinha **284 arquivos**, com `GerenciadorFinal.exe`, DLLs e runtime self-contained. Ambos foram copiados para `RUNNER_TEMP` em estrutura descartável de duas versões, sem atualizar instalação de usuário.
3. Gerou manifesto estrito de caminhos, tamanho e SHA256 de **todos os arquivos** de NEW. Autenticou seu digest e comprimento por metadata TUF `targets`, `snapshot`, `timestamp`, com a raiz de laboratório fixa `bootstrap.test-only.json`, verificada em SHA256 `a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e`. Chaves Ed25519 públicas e determinísticas de teste do Stage11 **não servem para produção**. O perfil não é operacional nem migrável diretamente para `gf-self-update/1`.
4. Alterou `GerenciadorFinal.dll` de NEW **in-place**; o manifesto autenticado recusou a candidata. Restaurou o DLL exato do build descartável e revalidou o conjunto completo antes de selecionar NEW.
5. Executou o **GerenciadorFinal.exe real** OLD com `--tray`: PID criado, ainda ativo após ~3 s, encerrado pelo ensaio. O mesmo ocorreu com NEW, depois da seleção de fixture. A existência de PID ou sua duração **não demonstra WinForms READY**, saúde de motores, fechamento gracioso de todas as instâncias nem ausência de I/O pendente.
6. Por ausência de READY verificado, **sempre reverteu a seleção local para OLD**, conferindo SHA256 de todos seus 284 arquivos, seletor final `old` e canário fictício `PERSONAL-KEEP-UNCHANGED.txt` inalterado.
7. Relatório declara `phase1_complete=false`, `ready_verified=false`, `automatic_update_authorized=false`, `operational_gates_closed=0`. Não há upload de EXE/DLL nem instrução de uso público.

### 4. Evidência Windows e histórico completo
- **Execução definitiva bem-sucedida:** https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40973919264
  - Commit técnico `1292e76bbd69b06a1a1b727bc35132ad7eb17f6e`.
  - Job `completed/success`. No log `GF_PHASE1_LAB` e JSON `GF_PHASE1_REPORT`.
  - `old_files=284`, `new_files=284`, `authenticated_real_package=true`, `tampered_dll_rejected_before_selection=true`, `candidate_selected_in_disposable_sandbox=true`, `old_and_canary_preserved=true`.
  - OLD criado PID 1536, permaneceu ativo ao limite; NEW criado PID 1212, permaneceu ativo ao limite; ambos encerrados ao final do ensaio. Esses PIDs são exclusivos do runner efêmero e não são identidades operacionais persistentes.
  - `rolled_back_due_to_no_ready=true`, `ready_verified=false`. A execução é **PASS de evidências parciais, não PASS da Fase 1**.
  - Test-only target manifest digest: `08fe0c68625e86628e0fbd8791c9d10e0cde901f0d072c8419ce1bde618974f8`.
- **Tentativa inicial falha, preservada:** https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40973674172
  - Dois builds reais de 284 arquivos já haviam passado; Python-TUF 7.0.1 expõe `timestamp.signed.snapshot_meta`, não `timestamp.signed.meta["snapshot.json"]`; `AttributeError` bloqueou antes da seleção de NEW. A contingência manteve OLD selecionada.
  - Corrigido **somente o acesso à API** no commit técnico definitivo; critérios TUF, assinatura e hashes não foram enfraquecidos.

### 5. Estado dos quatro marcos — sem promoção prematura
- **Fase 1 — ciclo GF real:** **PARCIAL / AINDA ABERTA**. Autenticação de pacote real em laboratório, adulteração rejeitada, criação e observação limitada de OLD/NEW e restauração de OLD **demonstradas**. Faltam contrato comprovável de encerramento gracioso e **READY específico da aplicação**, além de vínculo operacional dos bytes à imagem de processo. O runner não comprova automaticamente desktop WinForms saudável.
- **Fase 2 — qualificação de segurança / WORK independente:** não iniciada; **G1–G8 BLOCKED (0/8)**, revisão/aceite de manutenção TUF, frescor após restore integral e trust anchor operacional ainda ausentes.
- **Fase 3 — integração operacional:** não iniciada; não modificar PR #65 sem aprovação explícita.
- **Fase 4 — homologação real:** não iniciada; sem testes pessoais, sem distribuição.

**Progresso formal: 0/4 FASES ENCERRADAS.** Ganho verificável na Fase 1: primeiro ensaio com os executáveis GF reais e retorno conservador OLD em CI descartável.

### 6. Próxima ação única dentro da mesma Fase 1
Projetar e demonstrar **um contrato mínimo de saúde e encerramento que dependa do processo GF real**, com identidade de processo e pacote vinculados e observação em ambiente Windows descartável. Nenhuma confirmação por mero `Process.Start`, PID, terminal same-user, login UI inexistente ou log sintético deve ser usada como READY. Distinguir limites de sessão não interativa Windows CI de falha do GF. Manter a troca reversível e fail-closed. Corrigir **dentro da Fase 1**, sem criar novo Lote 24 ou outras tarefas paralelas.

**Próximo WORK só após candidato composto com health/shutdown/recuperação demonstrados; não consumir quota para repetir TUF/NTFS já testados.**


---

### Continuidade na mesma Fase 1 — seleção executável e recuperação (29/10/2026)

**Referência recebida:** `7846514595bdc422ced7be4214427c51a672f1ff`;
CI `40983654046` concluída com sucesso, mas **sem atestação READY**.

**Commits observados na branch:**
- `fd74fced9f6f2ea00e8a7d3bb42d697d995bb6ae`: seletor GF real
  escrito pelo coordenador L18, comunicação OLD_DONE/NEW_DONE; CI
  `40985184200` **failure** por interrupção prematura antes de OLD_DONE.
- `79da466ba9b707527a1c7a698d5081e7947c8145`: diagnóstico
  de EOF e stderr na barreira; CI `40985491980` **failure**:
  Python rejeitou `COORDINATOR_SELECTOR_OUTSIDE_TEMP`.
- `d47ac5942237f8f83d337fcb435b0ed56c32af93`: raiz do cenário
  .NET validada sob marcador de fixture e diretórios temporários
  corretos; CI `40985805701` **failure**: timeout após última
  barreira, durante espera de encerramento do filho.
- `6266aff1e6ad92d0ed23551447b724b3bce64ca1`: resolutor
  de slot efetivo OLD/NEW a partir do seletor durável L18, sem
  seletor próprio Python; retorno para OLD agora reinicia o GF real
  e exige prova funcional read-only e encerramento. CI `40986009708`
  **failure** (timeout após a última barreira, sem drenagem da saída padrão).
- `b9ecaa3ceac929d7aad894a14a500efe1d5fc3a6`: drenagem
  da saída padrão do filho enquanto aguarda o término; evita possível
  deadlock quando o JSON final excede o buffer do pipe. CI
  `40986098577` **completed/success** em 30/10/2026 UTC (29/10 em Fortaleza).

**O que é demonstrável pelo código, não ainda pela nova CI:**
o seletor fica no mesmo source root do diário L18; o Python
lê `selected` e resolve `sandbox/versions/{old|new}`, em vez de
escolher o executável por um segundo seletor. O pós-retorno procura
reiniciar OLD, observar pesquisa funcional e saída graciosa, mantendo
NEW bloqueada por ausência de READY. Qualquer CI com falha permanece
falha: **não transformar UNKNOWN em sucesso por um relatório local**.

**Bloqueios objetivos mantidos:** observação do código realmente
carregado e TOCTOU; atestação externa independente de função/saúde;
garantia de fim de I/O tardio; durabilidade de diretório e recuperação
sob queda de energia/crash/restart; autoridade TUF de produção;
homologação independente. PID, HWND, recibo same-user, pesquisa read-only
e retorno 0 isolados **não constituem READY**. Nenhum G1–G8 liberado.

**Proteções de escopo:** alteração restrita à branch experimental,
sem merge, release, deploy, Work, mudança em PC pessoal, alteração da
main, PR #65 Draft ou BETA .66. A Fase 1 segue aberta e o status de
transação permanece **UNKNOWN/BLOCKED** até prova auditável.

**Fechamento da verificação desta rodada:**
CI [40986098577](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40986098577)
`completed/success`, commit técnico
`b9ecaa3ceac929d7aad894a14a500efe1d5fc3a6`.
Log do job `123837915813`:
`GF_PHASE1_CHILD_COMPLETED_VERIFIED_EXIT_ZERO`;
`GF_PHASE1_CSHARP_BRIDGE` com `coordinator_decision=UnknownBlocked`,
`one_real_selector_coordinator_owned=true`,
`real_selector_new_then_old_observed=true`,
`real_manifest_sha_in_l18_target=true`,
`minimum_readonly_functional_search_verified=true`,
`verified_operational_ready=false`, `phase1_complete=false` e
`operational_gates_closed=0`. O workflow aprovado exige também
`executable_path_resolved_from_l18_selector=true`,
`restored_old_booted_after_recovery=true`, `restored_old_process.terminated=true`
e `restored_old_process.graceful_shutdown_verified_for_owned_process=true`.

**Interpretação rigorosa:** PASS do ensaio GF real OLD→NEW→OLD com
reexecução funcional de OLD restaurada em Windows descartável.
**Não é PASS sob crash/restart/power-loss**, nem prova independente
de READY, nem aprovação transacional da Fase 1 ou liberação de gates.
Este commit apenas documental não altera os bytes técnicos aprovados.

---

### Missão especial WORK — recuperação após interrupção de processo (29/10/2026, São Paulo)

**Estado de preparação deste registro: código escrito e compilação local aprovada;
execução Windows nova ainda pendente. Esta seção será atualizada com o resultado real.**

- HEAD inicial confirmado: `7e5ac2f13ab5a0b2ce79170c9fc20f3c0f12cbd7`;
  checkout sem alterações; desde `b9ecaa3` somente os dois commits documentais.
- CI anterior `40986098577`: `completed/success`, vinculada a `b9ecaa3`;
  prova exclusivamente o ciclo normal, sem interrupção.
- Hipótese: após intenção/seleção, mas **antes do fence durável que precede
  qualquer início possível de NEW**, um coordenador C# novo pode revalidar
  manifesto, objetos NTFS, seleção e histórico e iniciar OLD real com segurança
  limitada ao laboratório. Depois do fence, ausência de processos não demonstra
  ausência de efeitos: bloquear, inclusive quando NEW saiu graciosamente.
- Mesmos `L18Coordinator`, gate L20, diário L18 e seletor real. Nenhum seletor
  Python nem uso do reconciliador de fixture L16 para determinar o GF real.
- Manifesto TUF de laboratório `/3` passa a autenticar também a lista completa
  de OLD. Reutilizada a preparação TUF existente, sem novas chaves/dependências.
- Contexto de laboratório vincula instalação, transação, manifesto e IDs NTFS.
  Handles sem compartilhamento WRITE/DELETE retidos durante a interrupção e
  recuperação; revalidação completa antes/depois da execução de OLD.
- Dois Job Objects descartáveis, sem breakaway, com handles mantidos pelo
  supervisor do ensaio. Processo C# entra no grupo por `JOB_LIST` na criação;
  filhos/descendentes permanecem contidos. O reconciliador consulta o kernel,
  em vez de usar PID/HWND/ACK como prova de encerramento do grupo.
- Fence NEW significa **efeitos desconhecidos**, mesmo se crash ocorreu antes
  de `CreateProcess`. Perda do supervisor/job também bloqueia. Não há garantia
  de efeitos de brokers externos, I/O pendente, reboot ou falta de energia.
- Recuperação válida escolhe OLD pelo seletor persistido, reautentica TUF,
  relê todos os arquivos, inicia GF real, exerce filtro read-only externo e
  exige saída graciosa sem cleanup forçado. Resultado termina UNKNOWN para
  READY; replay não relança nem altera o histórico de recuperação concluída.
- Cenários planejados: intenção; seleção NEW sem launch; interrupção da
  recuperação antes/depois do replace; fence pré-launch; NEW viva com escritor
  descendente; NEW encerrada; fork do seletor; journal truncado; seletor travado;
  testemunha Job perdida. Recibo READY forjado não autoriza recuperação.
- Validações locais: .NET SDK `10.0.402`, builds Debug/Release com zero avisos
  e erros; AST Python e YAML do workflow válidos. Não são testes Windows.
- Arquivos técnicos: `phase1-composed/Phase1Bridge.cs.txt`,
  `Phase1RealBridge.csproj`, `phase1_lab.py`, `Phase1Recovery.cs.txt`,
  `Phase1RecoveryTests.cs.txt`, `LabProcessJob.cs.txt` e workflow
  `gf-autoatualizador-fase1-real-lab.yml`; mais este checkpoint existente.
- Limites: Job Object não é barreira de segurança contra mesmo usuário nem
  testemunha de I/O concluído; identidade/hash no disco não medem bytes em
  memória; pins não impedem toda adição de arquivos/brokers; flush não prova
  persistência física de diretório. R1–R6 continuam sem qualificação operacional.
- Restrições preservadas: somente branch autorizada; sem alteração no produto,
  main, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PCs;
  sem merge, release, deploy, upload de builds ou integração operacional.
  **Fase 1 aberta; READY=false; G1–G8 BLOCKED (0/8).**

**Primeira CI da missão (evidência negativa preservada):**
[40988706524](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40988706524),
commit [ba20288](https://github.com/walissonminel-1991/gerenciador-final/commit/ba20288be3f95a895c170dd449f7a874739cce4f):
`failure`. Compilação Windows e ciclo normal passaram; a preparação das onze
fixtures foi recusada com `NTFS_BROAD_WRITE`, antes de qualquer injeção. Não é
PASS de contenção nem de recuperação. Causa: redirecionar TEMP para RUNNER_TEMP
herdou ACL ampla. Correção: manter o temp privado original das fixtures e
aceitar cópias **somente** nos slots canônicos de uma raiz marcada, conferida
pelo guard NTFS existente; build original continua sob RUNNER_TEMP. Sem alterar
ACL, whitelist de proprietários ou asserts. Revisão adicional removeu
`taskkill /PID`: fallback agora usa o handle retido do próprio processo; Job
Object do ensaio faz a limpeza de descendentes. Barreiras são publicadas após
flush/close para evitar leitura antes do fim da gravação.
