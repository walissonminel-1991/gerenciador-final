# GERENCIADOR FINAL — MISSÃO ÚNICA DE CONCLUSÃO DO AUTOATUALIZADOR
## Fase 1 — Primeiro ensaio composto com os bytes reais do GF — checkpoint parcial

**2026-10-29 · experimental · Windows descartável GitHub Actions · FASE 1 AINDA ABERTA**

> **Estado mais recente — missão especial WORK:** CI
> [40991153584](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40991153584)
> `completed/success`; código validado `bcd7f7c27d0659ed8f310748c87c0ac1ecb26606`.
> **5 recuperações GF real pré-launch / 6 bloqueios seguros / 11 replays em
> novos processos. READY=false; Fase 1 aberta; G1–G8 BLOCKED (0/8).**
> Seções iniciais registram os ensaios históricos. O resultado e handoff da
> missão especial, com limites de aplicabilidade, estão ao final deste arquivo.


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

**Registro de desenvolvimento da missão; resultado Windows definitivo e
handoff na subseção final abaixo. Os registros de preparação/falha permanecem
históricos e não substituem a qualificação da execução definitiva.**

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

**Segunda CI:**
[40989081210](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40989081210),
commit [a2c2145](https://github.com/walissonminel-1991/gerenciador-final/commit/a2c2145105525da1c13e40277f4f62ed07f96ea8):
`failure`. ACL privada aceita, interrupção do owner executada; houve cinco
resultados de contenção, inclusive escritor descendente com ambas as escritas
recusadas. **Nenhuma recuperação aprovada nesta execução.** As retomadas
retornaram UNKNOWN antes dos cortes de recuperação, sem causa exportada pelo
processo sem console. O caso de NEW encerrada também expôs uso incorreto do
parser estrito TUF/state sobre relatório diagnóstico com tempos fracionários
(`JSON_INTEGER`). Ajustado apenas o parser desse diagnóstico limitado em
bytes, preservando o parser de segurança para manifesto/estado. A próxima CI
exige a causa exata de cada bloqueio e exporta erros delimitados do observador;
os cinco resultados anteriores não bastam para qualificar a causa pretendida.

**Terceira CI:**
[40990033271](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40990033271),
commit [85a19fa](https://github.com/walissonminel-1991/gerenciador-final/commit/85a19fa63c1d8dbe8e3ad8c6a70b33cd738e65d2):
`failure`, interrompida no primeiro caso inesperado para poupar runner. Causa
exportada: `REAL_CONTAINED_PROCESSES_STILL_ACTIVE` antes de iniciar NEW. Logo,
contagem do grupo do reconciliador precisa ser investigada, sem relaxar o
assert. Adicionado diagnóstico limitado de contagens/nomes dos membros do Job
Object, sem usar PIDs como identidade ou autorização. Nenhum PASS novo.

**Quarta CI e correção objetiva da barreira:**
[40990738787](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40990738787),
commit [5c83d70](https://github.com/walissonminel-1991/gerenciador-final/commit/5c83d7070a96b3ca413cfaa2925649fd8ea4e3bc):
`failure` no primeiro caso. Evidência: owner `active=0`; recovery `active=2`,
membros `GF.Updater.M3.L18Tests,conhost`. `CREATE_NO_WINDOW` criou console
headless, mantendo um auxiliar enquanto o reconciliador vivia. Corrigida a
criação para `DETACHED_PROCESS|EXTENDED_STARTUPINFO_PRESENT`, preservando
`JOB_LIST` e a exigência de somente o próprio reconciliador ativo. Nenhum
processo é subtraído por nome. Após saída do observador Python, espera limitada
exige desaparecimento real de qualquer membro auxiliar; se persistir, bloqueia.


### Resultado definitivo da missão especial — handoff ao CHAT ALTO

**HEAD inicial:** `7e5ac2f13ab5a0b2ce79170c9fc20f3c0f12cbd7`.
**HEAD final técnico validado:** `bcd7f7c27d0659ed8f310748c87c0ac1ecb26606`.
**HEAD documental da entrega:** o commit que contém esta atualização final;
seu SHA é fornecido no handoff do WORK. A revisão documental não altera os
bytes técnicos testados. Branch única: `experimento-g5-ipc-chat-alto-20261029`.

**Compilação e execução são provas distintas:** SDK local 10.0.402 compilou
Debug/Release sem erros/avisos; isso não executou código Windows no host Linux.
A CI final recompilou os dois GF reais e o coordenador em Windows 2022
**descartável**, passou no ciclo normal de controle e nos onze casos abaixo.
Job: `123853612220`. Resultado estruturado `GF_PHASE1_RECOVERY_REPORT`:
`failed=0`, `expected_cases=11`, `operational_ready=false`,
`phase1_complete=false`, `operational_gates_closed=0`.

#### Commits produzidos e CIs

| Commit | Ganho / finalidade | CI Windows |
|---|---|---|
| [ba20288](https://github.com/walissonminel-1991/gerenciador-final/commit/ba20288be3f95a895c170dd449f7a874739cce4f) | Contexto/pins reais, OLD no manifesto, reconciliação C# e injeções | [40988706524](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40988706524): failure de preparação ACL |
| [a2c2145](https://github.com/walissonminel-1991/gerenciador-final/commit/a2c2145105525da1c13e40277f4f62ed07f96ea8) | Temp privado com ACL estrita, cópia só de bytes, cleanup por handle, publicação das barreiras após flush | [40989081210](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40989081210): failure, sem recuperação aprovada |
| [85a19fa](https://github.com/walissonminel-1991/gerenciador-final/commit/85a19fa63c1d8dbe8e3ad8c6a70b33cd738e65d2) | Causa exata obrigatória, diagnóstico separado de metadados, fail-fast | [40990033271](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40990033271): failure de barreira, identificado grupo ocupado |
| [5c83d70](https://github.com/walissonminel-1991/gerenciador-final/commit/5c83d7070a96b3ca413cfaa2925649fd8ea4e3bc) | Identificar membros reais do Job sem usar PID como autorização; exigir raiz nova | [40990738787](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40990738787): failure; identificado conhost adicional |
| [bcd7f7c](https://github.com/walissonminel-1991/gerenciador-final/commit/bcd7f7c27d0659ed8f310748c87c0ac1ecb26606) | Eliminar console desnecessário com DETACHED_PROCESS e esperar término real de auxiliares | [40991153584](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40991153584): **completed/success** |

As quatro CIs anteriores continuam reprovadas. A CI final qualifica o conjunto
corrigido; não reclassifica os resultados antigos como sucesso.

#### Defeitos corrigidos e mecanismo efetivo

- Faltava retomar a seleção GF real após morte do coordenador. O novo caminho
  do **mesmo L18Coordinator** reabre gate, contexto, histórico L18, estado da
  instalação, metadados TUF existentes e objetos NTFS, sem estado em memória
  do coordenador morto. O supervisor de laboratório permanece vivo: seus
  handles dos Jobs e pins de pacote são testemunhas **limitadas ao ensaio**.
- Antes de qualquer possibilidade de iniciar NEW, persiste-se um fence por
  CreateNew/WriteThrough/Flush(true). Qualquer existência desse arquivo, mesmo
  incompleto, impede inferir ausência de efeitos. Não é recibo READY.
- O manifesto `/3` autentica `files` de NEW e `old_files` de OLD. Identidades
  físicas do contexto são reabertas/checadas; pins de todos os arquivos e
  canário negam WRITE/DELETE; namespace e bytes são conferidos novamente.
- `TargetSha` dos eventos L18 é o SHA do manifesto real. **StagedIdentity e
  BaselineDigest históricos L18 ainda descrevem fixtures inertes**; não são
  identidade/digest do GF. OLD real é vinculada por `old_files` e pelos pins
  do contexto, sem fingir que a fixture se tornou o pacote.
- Seleção real continua exclusivamente `l18-real-active.fixture.json`, escrita
  pelo coordenador. Python resolve o slot pelo seletor persistido, revalida e
  observa; não possui seletor nem permissão lógica de promover NEW/READY.
- Quando elegível, OLD é efetivamente iniciada em novo processo GF, com imagem
  de disco/caminho observados, filtro read-only exercitado externamente e saída
  graciosa obrigatória. Cleanup forçado não passa como shutdown.
- O replay de recuperação concluída relê os fatos, exige OLD e UNKNOWN final,
  retorna `OldReobservedNoReady`, não relança GF e não altera gerações/histórico.
  Isso é reobservação de consistência, não nova prova independente de saúde.
- A herança ACL ampla de RUNNER_TEMP e o conhost oculto foram causas concretas
  de bloqueio, corrigidas sem relaxar validação de ACL, política do Job ou
  contagem. Contagem não ignora processos por nome.

#### Matriz adversarial observada

Todos os casos verificaram instalação/transação/TargetSha persistidos, OLD e
canário intactos, bytes/identidades rechecados e ausência de promoção de NEW ou
READY. Cada reconciliador e cada replay foram executados em processos C# novos.

| Caso | Interrupção / contraexemplo | Observado / estado final do seletor |
|---|---|---|
| `intent` | Morte do owner após intenção durável, antes de selecionar NEW | **PASS_RECOVERY_LAB_ONLY**; GF OLD funcional/saída graciosa; old geração 1 |
| `selected` | Morte após NEW geração 2, antes do fence de início; outro coordenador com gate ocupado; tentativa WRITE e rename de DLL OLD | **PASS_RECOVERY_LAB_ONLY**; concorrente Busy, mutações recusadas; OLD funcional, old geração 3 |
| `recover-before-replace` | Owner morto; reconciliador também morto após intenção de recuperar, antes do replace | **PASS_RECOVERY_LAB_ONLY**; segundo reconciliador inicia OLD funcional; old geração 3 |
| `recover-after-replace` | Reconciliador morto após old geração 3, antes de iniciar OLD | **PASS_RECOVERY_LAB_ONLY**; novo processo mantém geração 3 e inicia OLD funcional |
| `launch-fence` | Fence durável, sem iniciar NEW: a janela já deve ser conservadora | **PASS_CONTAINMENT**; `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`; new geração 2 preservada, sem launch OLD |
| `new-live-descendant` | Morte só do coordenador; GF e escritor descendente sobrevivem; recibo READY falsificado | **PASS_CONTAINMENT**; primeiro `REAL_CONTAINED_PROCESSES_STILL_ACTIVE`; ambas as escritas tardias em OLD/canário recusadas; após cleanup do próprio Job, ainda bloqueia por efeitos NEW; new geração 2 |
| `new-exited` | NEW observada funcionalmente e encerrada graciosamente; morte do coordenador antes da recuperação | **PASS_CONTAINMENT**; mesmo com grupo vazio e saída graciosa, fence mantém efeitos desconhecidos; new geração 2 |
| `selector-fork` | ManifestSha do seletor contradiz contexto/TUF/diário | **PASS_CONTAINMENT**; `PHASE1_REAL_SELECTOR_WRONG_STATE`; seletor não reescrito |
| `journal-torn` | Truncamento sintético do último evento após matar owner | **PASS_CONTAINMENT**; `JSON_INVALID`, marcador separado de bloqueio durável, replay continua bloqueado; new geração 2 |
| `selector-locked` | Handle exclusivo impede acesso ao seletor | Primeiro UNKNOWN por sharing violation 32/33, sem replace cego; após fechar o handle e revalidar os fatos: **PASS_RECOVERY_LAB_ONLY**, old geração 3 |
| `witness-lost` | Destruição da testemunha Job vazia, sem substituir por um novo recibo | **PASS_CONTAINMENT**; `WIN32_2`; new geração 2, replay bloqueado |

Nos **cinco** resultados de recuperação, retorno `OldRecoveredNoReady` e replay
`OldReobservedNoReady`; diário termina em UNKNOWN após evento de OLD funcional.
Nos **seis** resultados de contenção, retorno/replay `UnknownBlocked`. O
seletor não é corrigido cegamente quando contraditório. UNKNOWN após trava foi
reconciliado com novas evidências suficientes, não com um ACK novo.

**Aplicabilidade:** o novo reconciliador aceita transações preparadas com o
novo contexto e fences. Não migra silenciosamente os estados antigos do ensaio
cooperativo. A ausência do contexto necessário bloqueia. O ciclo normal
histórico é controle de regressão, não prova de que um ACK NEW_DONE autoriza
recuperação operacional, nem substitui esta matriz de crash.

#### READY e limites não comprovados

| Requisito | Evidência limitada obtida | Continua ausente / status operacional |
|---|---|---|
| R1 | Manifesto TUF de teste autentica OLD/NEW e vincula instalação/transação/TargetSha | Autoridade de produção, confiança persistente/frescor operacional sob restauração: **não aprovado** |
| R2 | Pins NTFS, IDs, hashes completos, WRITE/rename recusados, execução resolvida do seletor | Medida dos bytes carregados; todo TOCTOU de namespace/arquivos extras/brokers; resistência a mesmo usuário: **não aprovado** |
| R3 | GF real responde ao round-trip do filtro e fecha graciosamente nos casos positivos | Saúde externa relevante de todos os motores/fluxos: **não aprovado** |
| R4 | Contagem kernel de processos contidos, descendente sobrevivente observado e escritas atrasadas recusadas | I/O kernel pendente e efeitos de brokers externos; ACK same-user não é testemunha independente: **não aprovado** |
| R5 | Processo do owner e dois pontos de recuperação interrompidos; retomada idempotente pré-launch | Perda do supervisor/pins/Jobs, reboot, chamada de criação interrompida internamente e falta de energia: **não aprovado** |
| R6 | Recibo falso, fork de seletor, truncamento, lock, concorrente e mutações recusados | Adversário same-user completo, restauração integral de estado e revisão independente: **não aprovado** |

Encerramento forçado de processo **não equivale a power-loss**. O truncamento
é um contraexemplo sintético, não medição de escrita rasgada após queda física.
Os cortes de recuperação cercam o replace, não interrompem sua execução interna
no kernel. Flush de arquivo não demonstra durabilidade física do diretório.
O teste tardio atrasa uma escrita em descendente de usuário; não esgota cenários
de I/O assíncrono do kernel. Nenhuma ausência desses efeitos é inferida de PID,
HWND, exit code, contador zero ou relatório local.

**Riscos residuais não aceitos:** severidade **alta para eventual operação**:
efeitos pós-NEW sem contrato independente; recuperação fria sem testemunha
retida; loaded-image/namespace TOCTOU e autoridade same-user; confiança TUF de
produção/frescor; durabilidade após queda física; saúde limitada à UI de leitura.
A barreira mantém todos fora de uso operacional. Não há aceite de risco novo.

#### Arquivos e fronteiras preservadas

Exatamente oito arquivos desde o HEAD inicial: este checkpoint; workflow
`.github/workflows/gf-autoatualizador-fase1-real-lab.yml`; e, em
`prototipos/autoatualizador-seguro/phase1-composed/`, `Phase1Bridge.cs.txt`,
`Phase1RealBridge.csproj`, `phase1_lab.py`, `LabProcessJob.cs.txt`,
`Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt`. Observador Win32 anterior
reutilizado sem modificações. Nenhum componente L16/L18–L22 compartilhado foi
alterado; a extensão do L18Coordinator fica somente no projeto experimental.

Main `410d65e23546976db456337c71e4705f10217d36` e PR #65 aberta/Draft em
`ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc` preservadas. BETA .66, fontes do
produto, infraestrutura, endpoints, credenciais, telemetria, consentimento,
VLC/WinGet/drivers e PCs pessoais inalterados. Sem merge, reset, force-push,
release, deploy, distribuição/upload de build ou integração operacional.
Somente processos/arquivos de laboratórios descartáveis foram modificados.

**Próxima ação recomendada, ainda na Fase 1:** fazer convergir a entrada do
ensaio normal para a mesma preparação com contexto/pins/fences antes de ampliar
qualquer retorno pós-NEW. O avanço pós-NEW depende de um contrato explícito e
externo para limitar efeitos, inclusive brokers/I/O tardio e perda da testemunha
supervisora. Até essa prova existir, preservar `UnknownBlocked`, sem trocar o
fence por recibo, PID ou sucesso do filtro. Não repetir esta matriz sem mudança
ou novo contraexemplo, nem iniciar Fase 2/homologação.

**Estado final: Fase 1 ABERTA; READY=false; UNKNOWN/BLOCKED para operação;
G1–G8 BLOCKED (0/8). Missão WORK encerrada, sem encerrar a Fase 1.**

---

### CHAT ALTO — convergência do fluxo normal com a recuperação L18 (30/10/2026)

**Base técnica revisada:** `71a40163c625683679484bd6ebba9cb4c2389077`;
CI `40991153584` concluída com sucesso para os 11 cenários anteriores.
**Branch única:** `experimento-g5-ipc-chat-alto-20261029`.

**Problema encontrado:** antes desta rodada, o laboratório ainda possuía
duas rotas de execução real: o velho `--run` com Python cooperativo e
OLD→NEW→OLD, e a rota L18 de recuperação com contexto, pins, Job Object
e fences. O velho retorno pós-NEW usava recibos/observação locais, sem
provar ausência de efeitos tardios. Não podia representar recuperação
transacional segura.

**Implementação nesta rodada:**
- `1b20f9ca781cf5fce0bc5fd60c8d852c82ef13ad`: adicionou cenário
  `normal` ao próprio `RealOwner/ReconcileReal`, compartilhando contexto
  real TUF/3, seletor durável, NTFS pins, Job Object e fences, verificando
  GF OLD e NEW no Windows descartável sem injetar morte do owner.
  A condição pós-NEW exige `UnknownBlocked`, com NEW histórica preservada.
- `b2084fdb28953c835a6bf0989bf751711ee6f929`: corrigiu ordem dos
  eventos no diário estrito L18; `Switching` precede a observação OLD,
  e `Exited` só pode vir após `Launched` para NEW.
- `ee21776df9a0263d239fe93a05f1a7f6d579579f`: retirou o
  cooperativo `--run` do C# e do workflow, eliminando uma segunda
  rota de decisão e trocando a CI por preparação/compilação + suíte unificada.
- `c8e07396ab3ab4f731ff642c846102c1d6ca8aa7`: rejeita
  explicitamente a antiga entrada cooperativa no observador Python.
  Python continua subordinado ao C# para preflight TUF e observação.

**CI e defeitos:** `41041347431` terminou em failure com
`L18_TRANSITION_DENIED` na primeira implementação; a causa foi
corrigida, sem relaxar a máquina de estados. As demais execuções
`41041420038` e `41041894820` devem ser consultadas novamente,
pois estavam em curso/fila durante a preparação deste registro.
**Nenhum SUCCESS novo é declarado neste checkpoint antes de verificar
o log e os asserts da CI final.**

**Critérios do cenário normal:** sem crash do coordenador; OLD e NEW
observados funcionalmente com saída graciosa; os dois fences presentes;
owner termina; reconciliador **em novo processo** retorna
`UnknownBlocked` por `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`,
sem rollback cego nem promoção de NEW. Contagens esperadas depois da
alteração: 12 cenários, sendo 5 recuperações pré-NEW e 7 contenções
(incluindo a execução normal pós-NEW). O teste não pode alegar READY.

**Limites não resolvidos:** ausência de testemunha independente de
quiescência pós-NEW e I/O/brokers externos; perda do supervisor e pins;
durabilidade sob reboot/power-loss; TOCTOU e bytes carregados; saúde
integral do GF; confiança TUF de produção. R1–R6 operacionais seguem
sem aprovação; G1–G8 continuam BLOCKED (0/8).
A Fase 1 permanece aberta. Não houve mudança em main, PR #65 Draft,
BETA .66, infraestrutura, credenciais, produto distribuído ou PCs pessoais.

**Fechamento definitivo da convergencia — CI 41041894820:**
[CI Windows 41041894820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41041894820)
`completed/success` no SHA técnico
`c8e07396ab3ab4f731ff642c846102c1d6ca8aa7`;
job `124019005630`. Após remover o caminho cooperativo da
execução e do código, o workflow revalidou apenas o coordenador unificado:
`GF_PHASE1_RECOVERY_REPORT` com `failed=0`, `expected_cases=12`,
`operational_ready=false`, `phase1_complete=false`,
`operational_gates_closed=0`,
`real_power_loss_tested=false` e
`supervisor_loss_recovery_proven=false`.

**Casos conferidos no log:** `normal` passou como `PASS_CONTAINMENT`,
com owner encerrado naturalmente e bloqueio
`REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`; cinco resultados
`PASS_RECOVERY_LAB_ONLY` para interrupções pré-NEW; seis outros
`PASS_CONTAINMENT` para fence pós-NEW, descendente, saída NEW,
fork do seletor, diário truncado e perda da testemunha.
A CI intermediária `41041420038` também foi SUCCESS para os 12
cenários com o fluxo cooperativo legado ainda presente; a final
`41041894820` prova que o conjunto não depende dele.
A CI inicial `41041347431` permanece failure histórica por
transição L18 inválida corrigida em `b2084fdb`.

**Decisão:** convergência **aprovada como ensaio experimental**.
Fase 1 ainda ABERTA; READY=false; G1–G8 BLOCKED (0/8).
A seleção NEW pós-efeitos permanece historicamente selecionada e
bloqueada, sem retorno cego a OLD. Recuperação fria, crash interno
de CreateProcess, brokers/I/O externo, durabilidade física e
atestação de saúde independente permanecem sem prova.
A alteração de documentação não dispara este workflow (path filter).


---

### CHAT ALTO — continuidade de identidade de Job e perda de supervisor (30/10/2026)

**Base antes deste experimento:** `570444d04943c3183fd2fcf113ccfd5c87297d11`.
**Commit técnico implementado:**
[`582bef32c123544d085cfdb7440fe2eee1142725`](https://github.com/walissonminel-1991/gerenciador-final/commit/582bef32c123544d085cfdb7440fe2eee1142725).
**CI Windows descartável:**
[41046050267](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41046050267),
`completed/success`, job `124030292406`. Log `GF_PHASE1_RECOVERY_REPORT`:
`failed=0`, `expected_cases=14`, `operational_ready=false`,
`phase1_complete=false`, `operational_gates_closed=0`,
`supervisor_loss_recovery_proven=false`,
`new_effects_quiescence_proven=false`, `real_power_loss_tested=false`.

**Contraexemplo investigado:** `OpenJobObjectW(name)` e
`ActiveProcessCount == 0` não comprovam a continuidade da identidade do
Job Object criado pelo supervisor. Após perda dos handles, outro objeto
pode ser criado com o **mesmo nome**. O nome é localizador, não atestado
de identidade. Não se deve recuperar OLD ao ver apenas um Job vazio.

**Correção experimental:**
- `LabProcessJob.StartWithWitness` duplica o handle original do owner,
  herdável exclusivamente pela criação do reconciliador C# com
  `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, junto de `JOB_LIST`.
- `ReconcileReal` exige um handle herdado não nulo, inspeciona a
  política do objeto e utiliza `CompareObjectHandles` entre o objeto
  herdado e o objeto aberto por nome. A decisão consulta o handle
  **original**, não substitui por um objeto recém-criado de mesmo nome.
- Sem o handle original, a reconciliação acrescenta UNKNOWN/registro
  conservador e retorna `UnknownBlocked`. Uma identidade divergente
  também bloqueia; nenhuma destas falhas autoriza selecionar OLD ou NEW.
- Os cinco cenários pré-lançamento de NEW continuam recuperáveis **somente
  quando o supervisor de laboratório preserva e transmite a testemunha
  original**. Não há mecanismo de recuperação fria autônoma.

**Matriz final: 14 cenários**, **5** `PASS_RECOVERY_LAB_ONLY` e
**9** `PASS_CONTAINMENT`. Resultados novos e reproduzidos nos logs:
- `witness-recreated`: owner interrompido antes do lançamento de NEW,
  objeto original fechado, novo Job criado com o mesmo nome e sem o
  handle original herdado. Resultado `UnknownBlocked`, razão
  `REAL_OWNER_WITNESS_NOT_INHERITED`, seletor OLD geração 1 intacto.
- `witness-wrong-object`: handle herdado aponta ao Job do reconciliador,
  diferente do Job owner. Resultado `UnknownBlocked`, razão
  `REAL_OWNER_WITNESS_OBJECT_MISMATCH`, OLD geração 1 intacta.
- `witness-lost`: permanece `UnknownBlocked` e agora exige
  `REAL_OWNER_WITNESS_NOT_INHERITED`.
- Caso `normal` pós-NEW permanece `UnknownBlocked` por
  `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, sem rollback cego.

**Interpretação rigorosa:** PASS do controle de **continuidade do objeto
kernel durante a vida de uma testemunha supervisora** e de perda
fail-closed. A nova prova não autentica a identidade do supervisor perante
outro processo malicioso do mesmo usuário que controle argumentos/handles;
não equivale a protocolo IPC autenticado, prova de I/O concluído,
recuperação fria, reboot ou power-loss. Handle e nomes de Jobs são
capabilities voláteis do laboratório, nunca confiança persistente
de produção. A perda completa do supervisor continua sendo bloqueio,
não sucesso de recuperação.

**Efeitos pós-NEW:** a documentação do Windows indica que filhos normais
seguem o Job por padrão, mas processos iniciados por intermediários
externos (como WMI) não são automaticamente abrangidos e o Job Object
não atesta término de todo I/O ou efeitos de brokers. Não é seguro
inferir quiescência de Job vazio, exit code 0, HWND ou recibo local.
O fence NEW permanece ativo e a versão NEW histórica não é substituída
às cegas. Nenhuma nova transição operacional é autorizada.

**Arquivos alterados nesta rodada:** exclusivamente
`phase1-composed/LabProcessJob.cs.txt`,
`phase1-composed/Phase1Recovery.cs.txt`,
`phase1-composed/Phase1RecoveryTests.cs.txt` e
`.github/workflows/gf-autoatualizador-fase1-real-lab.yml`,
além desta atualização documental. Nenhum arquivo fonte do GF
distribuído, main, PR #65 Draft, BETA .66, infraestrutura, credenciais
ou PC pessoal foi alterado. Sem merge, release ou deploy.

**Próximo experimento proposto para CHAT ALTO:** distinguir a execução
de um processo completamente contido no laboratório de effects/brokers
externos por um contrato de quiescência independente e verificável
que sobreviva aos limites de processo; avaliar se um supervisor confiável
e recuperável pode existir **sem serviço permanente, servidor externo,
comandos remotos nem custos**. Não construir um pseudo-protocolo de READY
com recibos do mesmo usuário. Testar contraexemplos e preservar bloqueio
se a prova não existir.

**Estado formal:** Fase 1 ABERTA, READY=false, UNKNOWN/BLOCKED
para operação, **G1–G8 BLOCKED (0/8)**.

---

### CHAT ALTO — refutação de quiescência por Job vazio (30/10/2026)

**Base:** `046229830cc979baa2cd3d3bb9a99c4ed578c07b`;
branch exclusiva `experimento-g5-ipc-chat-alto-20261029`.

**Componente técnico:**
[`8d9ee9eecee8731fe4ee79c51427e25fc4892129`](https://github.com/walissonminel-1991/gerenciador-final/commit/8d9ee9eecee8731fe4ee79c51427e25fc4892129)
acrescenta o teste adversarial `external-broker`, utilizando
`IsProcessInJob` sobre handles do processo e dos dois Jobs (owner e
recovery), sem usar PID como autoridade. O processo é criado pelo
**supervisor do laboratório**, e NÃO pelo GF nem WMI real. Ele apenas
escreve em uma fixture inerte extra, não pinada e contida no sandbox.
Nenhum programa, dado pessoal ou recurso externo é alterado.

**Defeito de compilação:** a CI inicial
[41047573260](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41047573260)
falhou por literal JSON mal escapado (CS1056/CS1003) no teste; não
qualificou evidência de recuperação. A correção foi salva em
[`6e648675b34a78e6dc76f44d9a325a82d6e1492a`](https://github.com/walissonminel-1991/gerenciador-final/commit/6e648675b34a78e6dc76f44d9a325a82d6e1492a).

**CI final verificada:**
[41047617910](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41047617910),
commit técnico `6e648675b34a78e6dc76f44d9a325a82d6e1492a`,
job `124035924506`, `completed/success`. Log
`GF_PHASE1_RECOVERY_REPORT`: `failed=0`, `expected_cases=15`,
`operational_ready=false`, `phase1_complete=false`,
`operational_gates_closed=0`,
`new_effects_quiescence_proven=false`,
`supervisor_loss_recovery_proven=false`.
Matriz: **5** `PASS_RECOVERY_LAB_ONLY` pré-NEW e
**10** `PASS_CONTAINMENT`, inclusive o novo caso:
`external-broker`, `external_broker_effect_observed=true`,
`outcome=UnknownBlocked`,
`block_reason=REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`.

**Falsificação do pressuposto:** depois que NEW real executou
uma função read-only e saiu graciosamente, os Jobs do owner/recovery
estavam vazios. O broker de teste foi comprovadamente **fora de ambos
os Jobs** e permaneceu apto a escrever posteriormente numa fixture
deliberadamente não protegida. Um recibo local `ready/all_effects_complete`
foi fabricado para tentar enganar a reconciliação. O reconciliador em
processo novo bloqueou antes e depois da escrita. OLD/canário permaneceram
intactos, e o seletor não foi modificado cegamente.
Isso refuta a promoção `Job vazio + exit code + recibo => quiescência`.

**Limites exatos:** esse teste NÃO prova que o GF real delegou
tarefas ao broker, NÃO demonstra todos os modos de I/O tardio e NÃO
constitui prova independente de quiescência. Também NÃO dá recuperação
fria após perda do supervisor, durabilidade após reboot/power-loss,
atestação dos bytes carregados, autoridade TUF de produção nem
saúde operacional integral. A existência de processos/serviços externos
não necessariamente pode ser delimitada por um Job do atualizador.
`REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED` continua obrigatório.

**Direção técnica seguinte:** definir antes de qualquer desbloqueio
um contrato de efeitos explicitamente enumerados/contidos ou atestados
por autoridade realmente independente, incluindo processos externos
e operações assíncronas; demonstrar condições de início/terminação
sob falhas e perda de supervisão. Sem esse contrato, não implementar
rollback pós-NEW automático, não remover fence e não encerrar a Fase 1.
O piloto deve continuar inacessível ao produto distribuído.

**Fronteiras preservadas:** alteração apenas dos arquivos
`phase1-composed/LabProcessJob.cs.txt`,
`phase1-composed/Phase1RecoveryTests.cs.txt`, do workflow da Fase 1
e deste registro/README. `main`, PR #65 Draft, BETA .66,
infraestrutura, credenciais, telemetria e PCs pessoais intocados.
Sem WORK, merge, release ou deploy.

**Resultado formal:** Fase 1 ABERTA; READY=false; operação UNKNOWN/BLOCKED;
G1–G8 BLOCKED (0/8).

---

### CHAT ALTO — Efeitos delegados apos encerramento do intermediario (16 cenarios) — 30/10/2026

**Base desta rodada:** `ce128c1fb182e44b64fc82a99c0a7453704980ac`.
**Branch unica autorizada:** `experimento-g5-ipc-chat-alto-20261029`.
**Commits tecnicos:** `e036100f889619a641390afe943f9cf972081928`
(criacao do contraexemplo `external-broker-handoff` e especificacao do
contrato de escopo de efeitos),
`ecd3f0f82f746464c8af671d3dde358577bf1360` (corrige CS0165,
identificador de processo nao inicializado) e
`72f7a8880ad0124cb4893a0c87faf33b27df9ef5` (corrige corrida
de FileShare.None ao ler o marcador do escritor delegado).

**Validacao:** a CI
[41051684979](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41051684979),
job `124049028303`, concluiu `SUCCESS` para o commit tecnico
`72f7a8880ad0124cb4893a0c87faf33b27df9ef5`. Log inspecionado:
`GF_PHASE1_RECOVERY_REPORT`, `failed=0`,
`expected_cases=16`, com **5** `PASS_RECOVERY_LAB_ONLY` (pre-NEW)
e **11** `PASS_CONTAINMENT`. `operational_ready=false`,
`phase1_complete=false`, `operational_gates_closed=0`,
`supervisor_loss_recovery_proven=false`,
`new_effects_quiescence_proven=false`,
`real_power_loss_tested=false` e
`production_tuf_authority=false`.

**Falhas intermediarias, nao aprovadas:**
- [41050246051](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41050246051)
  falhou no build C# (CS0165); corrigido em `ecd3f0f8`.
- [41050490211](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41050490211)
  compilou e aprovou os 15 casos anteriores, mas o 16o falhou por
  `IOException`/ERROR_SHARING_VIOLATION durante leitura prematura do
  marcador criado com FileShare.None. Corrigido em `72f7a888` com
  espera limitada e tratamento apenas do erro 32.
- Nenhum assert foi relaxado, e nenhuma falha foi reclassificada como READY.

**Contraexemplo comprovado, limitado ao runner Windows descartavel:**
apos NEW real ter sido observado encerrando graciosamente, um processo
intermediario SIMULADO **pelo proprio laboratorio** inicia outro
processo delegado tambem de laboratorio e encerra. O segundo processo
permanece vivo e fora dos Jobs owner/recovery, conforme `IsProcessInJob`.
Ambos os Jobs estao vazios. Um arquivo local falsificado anuncia
`all_effects_complete=true` e `broker_exited=true`. Mesmo assim o
reconciliador C# em processo novo registra `UnknownBlocked`, razao
`REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, e preserva a selecao
historica NEW geracao 2. Posteriormente, o delegado escreve na
fixture inerte `external-broker-effect.fixture.txt`, comprovando
efeito tardio apos a saida do intermediario e apos a decisao
conservadora. O campo de evidencia
`external_handoff_effect_observed=true` foi conferido nos logs.

**Escopo e termino — contrato experimental registrado no README:**
(i) efeitos locais do coordenador em namespace privado podem ter
alvos, identidades e historico delimitados; (ii) descendentes
associados aos Jobs podem ser observados, mas Job vazio nao atesta
conclusao de I/O, brokers e efeitos de terceiros; (iii) tarefas
delegadas a WMI/COM/servicos/agendadores/instaladores exigiriam um
contrato especifico e uma fonte de verificacao independente,
com inicio, recursos atingidos, autoridade, efeitos pendentes,
quiescencia e persistencia observaveis; (iv) sem prova para uma
classe de efeitos, o estado permanece `UNKNOWN/BLOCKED`.
A ausencia do intermediario nao prova a ausencia de tarefas por
ele iniciadas. Esse contrato nao implementa um READY operacional.

**Limites inegociaveis:** nao houve teste de broker real criado por
GF ou WMI nem de todas as classes de I/O externo; o contraexemplo
simula delegacao com processos/arquivos inertes. Nenhum handle
volatil de Job sobrevive a reboot como testemunha de continuidade.
Nao ha prova de recuperacao fria, power-loss, efeito delegado
duravel fora do Job, bytes carregados em memoria, autoridade TUF
de producao ou READY independente. Nao e seguro liberar rollback
automatico depois do fence NEW.

**Proximo passo de maior valor:** realizar inventario somente-leitura
dos efeitos REAIS do startup e dos subprocessos do GF em Windows,
identificando explicitamente quais chamadas sao locais/read-only,
quais podem acionar brokers/servicos e quais exigem tratamento
especifico. Somente entao propor um contrato estreito
e testavel de escopo para execucao de candidato; nao criar
servico permanente, protocolo ACK same-user ou falsa testemunha.
Estudar recuperacao fria apenas em estados com ausencia de
lancamento demonstravel por invariantes duraveis independentes;
na duvida permanecer BLOQUEADO.

**Limites de escrita cumpridos:** apenas
`prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt`,
`phase1-composed/README.md`, workflow de Fase 1 e este checkpoint.
`main`, PR #65 Draft, BETA .66, infraestrutura, credenciais,
telemetria, dados pessoais, PCs pessoais e GF distribuido
nao foram alterados. Sem WORK, merge, release ou deploy.

**Estado final:** Fase 1 ABERTA, READY=false, G1–G8 BLOCKED (0/8).

---

### CHAT ALTO — Auditoria estaticamente fundamentada dos efeitos de inicializacao do GF (30/10/2026)

**Base verificada:** `cdb524e4120bfec47cbd218c5035fe76eec87684`;
CI [41051684979](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41051684979)
`SUCCESS` no commit tecnico `72f7a8880ad0124cb4893a0c87faf33b27df9ef5`
(16/16: cinco recuperacoes pre-NEW, onze contencoes).
A CI de documentacao 41048987595 tambem foi confirmada como `SUCCESS`.
`main` continua em `410d65e23546976db456337c71e4705f10217d36`;
PR #65 permanece aberta em Draft e nao mesclada.

**Escopo desta inspecao:** somente leitura dos arquivos reais da branch, sem
executar o GF em computadores pessoais, sem alterar o produto ou o
coordenador e **sem nova CI Windows**. Os caminhos abaixo sao efeitos
possiveis por fluxo de controle e chamadas de API observadas no codigo;
nao constituem captura runtime completa nem prova de que um broker foi
ativado em uma execucao especifica. A CI anterior comprova interacao
funcional limitada, nao ausencia de I/O ou de efeitos no startup.

**Descoberta central:** o laboratorio em
`phase1-composed/phase1_lab.py:254-315` invoca
`GerenciadorFinal.exe --tray`. Em `Program.cs:18-76`, essa
argumentacao segue o `Main` normal e cria
`MainForm(startInTray:true)`, sem `startServices:false`;
`Program.cs:794-835` executa `LoadInstalledAsync`,
`BeginStartupChecks` e `RebootResidualReview.CheckAfterBootAsync`.
A bandeja suprime a janela/convite inicial, mas NAO constitui um
modo imutavel de inicializacao sem efeitos. O contrato de health em
`win32_process_observation.py:175-288` exercita um filtro local
sem clicar para instalar ou desinstalar; nao atesta todo o startup.

#### Matriz de efeitos e fronteiras de delegacao

| ID | Etapa e fonte verificavel | Efeito/alvo | Evidencia e autoridade | Classificacao |
|---|---|---|---|---|
| S01 | `Program.cs:18-34`, `DiagnosticTrace.cs:39-113` | Cria/grava diagnostico em LocalAppData/GerenciadorFinal/Diagnosticos, com fallback para TEMP; pode excluir candidato existente com mesmo nome antes de CreateNew, sob guardas de diretorio/arquivo | Escrita direta pelo GF, nao pelo coordenador L18; nao passiva | **CONFIRMADO POR CODIGO: write local** |
| S02 | `Program.cs:655-660`, `LocalReliabilityHistory.cs:25-45,153-187` | Cria log GUID em LocalAppData/GerenciadorFinal/Historico, com append/flush | Escrita direta do processo GF e escopo fora do namespace transacional de OLD/NEW | **CONFIRMADO POR CODIGO: write local** |
| S03 | `Program.cs:794-821,2227-2301,241-296` | Leitura de ARP HKLM/HKCU, MSIX atual via `Windows.Management.Deployment.PackageManager.FindPackagesForUser`, jogos Steam/Epic e gerenciadores Scoop/Chocolatey | Leitura via Win32/WinRT, arquivos e catalogos locais; nao demonstra que fornecedores/OS nao alterem estado interno | **CONFIRMADO: chamadas de consulta; efeito externo nao completamente atestado** |
| S04 | `LocalMsixInventory.cs:102-139` | Instancia PackageManager, consulta MSIX e nomes/identidades | Ativacao de componentes WinRT/OS; nao equivale a instalacao | **POTENCIAL: trabalho delegado a OS** |
| S05 | `Program.cs:821-835`, `RebootResidualReview.cs:114-160` | Lê pendencias pos-reinicio; se houver marcadores de outro boot, faz nova leitura de inventario e apaga/reescreve o proprio arquivo de pendencia, sem excluir dados residuais do usuario | Efeito condicional no armazenamento de estado do GF; nao passivo, apesar de nao ser nova limpeza automatica | **CONDICIONAL: write/delete metadados locais** |
| S06 | `Program.cs:39-44`, `AnonymousTelemetry.cs:25-55,318-342`, `BetaRuntimePolicy.cs:4-18` | Inicializa estado de privacidade/telemetria. No perfil `GF_BETA_LOCAL` nao autoriza diagnostico colaborativo, envio nem persistencia desse caminho | Guardado por propriedade de compilacao, nao por variavel de ambiente | **INATIVO PARA ENVIO NO BUILD LOCAL; outros perfis exigem inspecao separada** |
| S07 | `Program.cs:2508-2531`, `IntegratedSystemUi.cs:647-663`, `MemoryRamMonitor.cs:28-87` | Inicia timer periodico para observacao da RAM; consulta APIs de memoria | Consulta periodica no GF; possivel custo/concorrencia, nao atesta ausencia total de efeito | **CONFIRMADO: polling local** |
| S08 | `MemoryRamDashboard.cs:255-310` | Ajuste de memoria de processo e cache em funcionalidade explicita/autorizada | Nao observado como disparo automatico incondicional no startup; exige politica e autorizacao de sessao | **FORA DO CAMINHO INICIAL PADRAO; manter como efeito de manutencao** |
| S09 | `Program.cs:2508-2531`, `BetaRuntimePolicy.cs:17`, `GerenciadorAplicativos.csproj:14-36`, workflow Fase1 linhas ~47-56 | A CI compila OLD/NEW com `-p:GfBetaProfile=local`; nesse perfil `AllowsAutomaticOnlineChecks=false` | Teste do laboratorio nao exercita atualizacoes automaticas HTTP/COM por inicializacao | **COMPROVADO POR CONFIGURACAO: caminho online nao executado no modo testado** |
| S10 | `Program.cs:2520-2531,2308-2412`, `IntegratedSystemUi.cs:665-741`, `AdvancedFeatures.cs:240-270` | Em perfil nao local, inicio pode consultar fontes HTTPS e Windows Update Agent (`Microsoft.Update.Session`, `CreateUpdateSearcher`, `Online=true`, `Search`) | Consulta e ativacao WUA/COM; sem autorizacao de instalar aqui, mas side effects internos da OS e rede nao delimitados | **POTENCIAL NO PERFIL INTERNAL; FORA DA COBERTURA DA CI LOCAL** |
| S11 | `WinGetComDiscoveryProvider.cs:16-52`, `WinGetComReader.cs:8-51` | Processo leitor separado e COM WinGet consultivo quando descoberta de atualizacoes acionada | Nao integra `EcosystemInventory.ReadLocal` do startup: esse utiliza Scoop/Chocolatey (`EcosystemInventory.cs:75-97`) | **CAMINHO CONDICIONAL DE CONSULTA, NAO PROVADO NO STARTUP LOCAL** |
| S12 | `AdvancedFeatures.cs:25-69`, `IntegratedSystemUi.cs:45-55` | Consultas WMI de hardware quando paginas/funcoes correspondentes sao acessadas | Ativacao de WMI/COM externa a Job e ao escopo da transacao | **SOB DEMANDA; NAO DEMONSTRADO NA OBSERVACAO DA ABA APLICATIVOS** |
| S13 | `Program.cs:982-997,2582-2600`, `win32_process_observation.py:175-287` | Digita nonce nao correspondente e remove texto de pesquisa, modificando somente estado de interface/lista no fluxo conhecido | Funcao minima visivel externamente; nao percorre toda funcionalidade do GF | **VERIFICADO FUNCIONALMENTE NOS ENSAIOS, NAO E READY** |
| S14 | `Program.cs:837-925`, `AnonymousTelemetry.cs:318-342`, `LocalReliabilityHistory.cs:153-196` | Encerramento cancela tarefas, fecha historico e pode salvar telemetria local sob politica/consentimento; `GF_BETA_LOCAL` recusa envio | Efeitos tambem ocorrem apos health probe; precisam do contrato de escopo | **CONDICIONAL: persistencia no encerramento** |

**Nota de precisão:** `S01` e `S02` demonstram que o startup
NAO e estritamente read-only. `S03` demonstra operacoes de leitura
por APIs e nao implica que todas as chamadas nativas/COM tenham
efeito nulo. `S09` impede extrapolar o ensaio BETA LOCAL para
o perfil `internal`. `S13` verifica apenas uma interacao grafica,
nao um contrato universal de saude.

#### Contrato minimo viavel — proposta, NAO implementacao

- **Limite A, antes de NEW:** coordenador deve demonstrar integridade do
  contexto, journal, seletor e pacote, exclusividade de transacao e
  persistencia de fence **anterior ao primeiro ponto de lancamento**.
  Recuperacao fria desse subconjunto requer provar que o codigo nunca
  consegue executar NEW se o fence ainda nao foi persistido, mesmo
  diante de crash, power-loss e concorrencia. Os ensaios atuais
  recuperam OLD apenas com supervisor e handle original; sem essa
  testemunha, o caso frio segue BLOCKED.
- **Limite B, health minimo do GF:** uma consulta controlada ao
  inventario e filtro de pesquisa pode atestar funcionamento parcial
  da UI, desde que o observador seja independente do executavel
  e que writes locais conhecidos de inicializacao estejam previstos
  no escopo. A UI/nonce sozinha nao atesta quiescencia nem ausencia
  de brokers, nem autentica os bytes efetivamente carregados.
- **Limite C, efeitos externos:** para qualquer acao via WinRT/COM,
  WUA/WinGet, servicos e escritos fora do namespace transacional,
  o responsavel deve fornecer inventario fechado de efeitos,
  ponto de cancelamento/conclusao e testemunha confiavel que
  continue valida apos falha. Um Job vazio, recibo local, ETW/USN
  ou periodo sem novos eventos nao prova efeitos futuros ausentes.
  Nao liberar READY ou rollback pos-NEW nesse estado.

**Conclusao da rodada:** esta era uma **auditoria de efeitos baseada em
fontes do repositorio**, nao uma etapa de desenvolvimento funcional que
justificasse nova compilacao Windows. Nenhum codigo/CI experimental
foi alterado, porque ja existe contraexemplo suficiente de efeitos
externos e nao se encontrou contrato que autorizasse relaxar fences.
A proxima prova util seria uma auditoria dinamica limitada em runner
descartavel dos **writes locais S01/S02/S05** e de sua origem/escopo,
sem declarar ausencia total de writes pela mera falta de eventos.
Em separado, discutir estados frios comprovadamente PRE-LANCAMENTO,
nunca reutilizando prova para os estados posteriores ao fence NEW.

**Status preservado:** Fase 1 ABERTA, READY=false,
`G1–G8 BLOCKED (0/8)`; `main`, PR #65 Draft, BETA .66,
infraestrutura, credenciais e PCs pessoais intocados.

---

### CHAT ALTO — prova dinamica de efeitos locais no startup e limite de recuperacao fria (30/10/2026)

**Base:** `93419bf6bae79817dfa88956d62e7ff82c34cd50`.
**Commit tecnico inicial:** `d35f379f9847a9b73bed396dc936acd650f1df02`.
**Correcao:** `0c0b02e2acb087ec18ef81f1273636a6ea95f5c7`.
**CI final isolada:**
[41060137516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41060137516),
job `124077509441`, **SUCCESS** no commit corrigido. Nenhum dos 16
cenarios de recuperacao da CI anterior foi repetido. A ultima CI
transacional continua sendo `41051684979` (SUCCESS, 16/16).

**Instrumentacao implementada SOMENTE no laboratorio:**
- Script `prototipos/autoatualizador-seguro/phase1-startup-effects/startup_effects_probe.py`
  testa o executavel real `GerenciadorFinal.exe --tray` da
  `GfBetaProfile=local` em runner Windows descartavel, construindo
  somente uma copia do produto em `RUNNER_TEMP`.
- Workflow separado `.github/workflows/gf-fase1-startup-effects-windows.yml`
  possui filtros de paths independentes, `contents: read`, checkout
  sem credenciais persistentes, SDK/Python fixados, nao publica
  artefatos e nao executa os lotes de recuperacao.
- Precondicoes fail-closed: CI hospedada Windows,
  executavel publicado no `RUNNER_TEMP` e pasta
  `%LOCALAPPDATA%/GerenciadorFinal` inexistente antes do teste.
  Nenhum perfil com estado GF previo e modificado ou apagado.
- O script cria uma sentinela separada dentro do espaco GF
  descartavel e um `RevisaoReinicio.dat` propositalmente invalido,
  ambos com hashes conhecidos. Executa GF com a mesma interface
  e observador Win32 preexistente, valida busca real com nonce,
  encerramento gracioso, diagnostico e historico.
  Nao coleta nem imprime nomes de programas, caminhos do perfil,
  stdout de processos externos ou dados pessoais.

**Falha intermediaria identificada e corrigida:** a CI
[41059920231](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41059920231)
falhou em `ModuleNotFoundError` antes de iniciar o GF.
Foi acrescentado somente o path da pasta `phase1-composed` para
importar o observador existente. Nenhum assert de efeitos, isolamento,
seguranca ou READY foi relaxado.

**Evidencia do log `GF_PHASE1_STARTUP_EFFECTS_REPORT`:**
`runner_disposable=true`; `beta_profile=local`;
`diagnostic_local_write_observed=true`;
`reliability_history_local_write_observed=true`;
`unrelated_sentinel_preserved=true`;
`invalid_pending_postboot_sentinel_preserved=true`;
`functional_search_minimum_observed=true`;
`valid_postboot_pending_reconciliation_tested=false`;
`all_writes_enumerated=false`;
`external_brokers_quiescent_proven=false`;
`cold_recovery_proven=false`; `operational_ready=false`;
`phase1_complete=false`; `gates_closed=0`.

**Interpretacao S01/S02/S05:**
- S01 e S02 deixaram de ser apenas deducoes estaticas: a CI observou
  a criacao real de arquivos locais de diagnostico e historico GF,
  com assinaturas esperadas, sem declarar que esse e todo o I/O.
- Para S05, apenas uma pendencia **invalida** foi testada como
  sentinela negativa: GF a rejeitou e preservou sem alterar bytes.
  O fluxo verdadeiro de conclusao de revisao pos-reboot com arquivo
  valido **NAO FOI TESTADO**; nao e seguro declarar S05 fechado.
- A integridade da sentinela foi verificada por hash antes/depois.
  O probe nao monitora globalmente Registro, disco, servicos,
  WMI/COM, ETW, USN, I/O de kernel ou brokers externos.
- `--tray` nao e modo read-only integral; o filtro pesquisado
  e funcionalmente somente leitura, enquanto o startup grava
  estado local previsto.

**Analise de recuperacao fria PRE-LANCAMENTO, somente leitura:**
`Phase1Recovery.cs.txt:158-221` seleciona OLD, registra
Switching, seleciona NEW geracao 2, persiste `NewLaunchFence`
antes de tentar executar NEW. Uma interrupcao antes do fence
pode ocorrer apos a selecao NEW. A ausencia do fence, sozinha,
nao e autenticacao de que nenhum processo conseguiu executar
durante falha, concorrencia, interferencia same-user ou perda de
energia. Em `ReconcileReal`, a recuperacao exige
`LabProcessJob.FromInherited(c.OwnerJob, ownerWitness)`,
Jobs na contagem esperada, metadados TUF do teste revalidados,
pins NTFS, diario e ausencia dos fences de OLD/NEW.
Sem o supervisor original, o handle kernel herdado inexiste:
o fluxo **falha fechado**. Nao houve teste de power-loss,
reboot, supervisor morto mais reinicio do sistema nem prova
de exclusividade duravel para recuperar OLD autonomamente.

**Decisao de engenharia:** nao promover READY, nao introduzir
testemunha fake baseada em arquivo local, PID, Job vazio ou
retorno zero. Nao liberar rollback pos-NEW. O proximo incremento
de valor sera estudar o **protocolo duravel PRE-CreateProcess**
e estados apos crash sem testemunha viva, procurando um
invariante criptografico/persistente e um observador de
exclusividade independente, sob adversario same-user e falha
de energia. Se nao for possivel, continuar bloqueando.

**Fronteiras:** nenhuma alteracao no executavel/codigo fonte
distribuido, `main`, PR #65 Draft, BETA .66, Worker/D1,
credenciais, telemetria ou computadores pessoais. Sem WORK,
merge, release, deploy. Fase 1 ABERTA; READY=false;
G1–G8 BLOCKED (0/8).
