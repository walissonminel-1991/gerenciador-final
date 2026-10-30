# GERENCIADOR FINAL — Fase 1 — Modelo inerte de fornecedor assíncrono e escrita tardia

**30/10/2026 — checkpoint experimental, NÃO implantação MSI, NÃO prova de quiescência do Windows Installer**

## Âncora e restrições

- Repositório `walissonminel-1991/gerenciador-final`; única branch autorizada `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial confirmado `51d60346db37b9a4df930c8275450ab68f7cd19a`.
- CI anterior aprovada [41082816324](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41082816324), job `124156095885`, 21/21 PASS_CONTAINMENT, zero recuperações, auditoria de nove rotas reais aprovada, sem provas de quiescência.
- PR #65 continua Draft, aberta e sem merge. `main`, BETA .66, código distribuído, TUF, B+A, NTFS, C# `Phase1Recovery.cs.txt`, infraestrutura, PCs e telemetria não modificados.
- Último commit técnico novo de teste `bf314f17b5aceb98e9f3801fce4fc3a2212c467a`: somente `Phase1RecoveryTests.cs.txt` e workflow experimental.
- Nova execução Windows [41087305170](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41087305170), aguardando confirmação de resultados deste checkpoint. **Não declarar sucesso/22/22 antes de ler o log do job.**

## Fonte oficial e hipótese precisa

Microsoft Learn [Custom Action Return Processing Options](https://learn.microsoft.com/en-us/windows/win32/msi/custom-action-return-processing-options) diferencia os flags da coluna `Type`:
- `msidbCustomActionTypeAsync` (`0x80`) é assíncrono e aguarda o resultado no final da sequência.
- `msidbCustomActionTypeAsync | msidbCustomActionTypeContinue` (`0xC0`) é a combinação **EXE assíncrona sem espera**, cuja execução pode continuar **após o Windows Installer terminar**.
- Não generalizar o `0xC0` para todas as ações, rollback, DLL, ou qualquer MSI; a documentação limita as aplicações desses flags.

O GF realmente inicia MSI/EXE em `VerifiedUpdaters.cs` com `Process.Start` e acompanha `WaitForInstallerExitAsync`; `RegisteredUninstaller.cs` acompanha o cliente do fornecedor. Isso **não** demonstra como um MSI específico foi autorado. O ponto verificável é que encerrar um processo observado não fornece um critério geral para declarar ausentes efeitos assíncronos de fornecedores.

**Lacuna em relação aos testes anteriores:** `external-broker` tinha um broker vivo na reconciliação e `external-broker-handoff` já possuía escritor vivo antes da reconciliação. Faltava observar um intermediário externo vivo *mas nenhum escritor criado*, e só depois da decisão do L18 o broker criar o escritor e produzir um efeito.

## Novo teste do harness: `provider-late-spawn-after-reconcile`

Exclusivamente no Windows Actions descartável e em pasta de fixture privada:

1. Executar o GF real NEW através da observação já existente `after-new-exit` (sem READY, sem quiescência, sem instalação).
2. Confirmar owner/recovery Jobs vazios. Criar `provider-effect.fixture.txt` com `before\n`.
3. Um **cliente simulado do fornecedor**, lançado pelo supervisor de testes fora dos Jobs C#, cria um **broker simulado externo** e termina com `ExitCode=0`. Ele não é `msiexec` nem o serviço Windows Installer.
4. Reabrir um handle para o broker somente após validar PID **e caminho da própria imagem de laboratório**; garantir broker vivo, Jobs vazios, ausência de `provider-writer-ready.fixture.txt` e efeito ainda `before\n`.
5. Inserir um recibo de quiescência fictício; executar `ReconcileReal` em processo separado **ANTES de qualquer escritor externo ser criado**, exigir `UnknownBlocked` e razão `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, seletor NEW/2 intacto e nenhum escrito tardio.
6. Somente após o bloqueio autorizar a criação do escritor pelo broker externo; exigir marcador de criação e que o escritor **não** pertence a nenhum Job C#. Permitir escrever exclusivamente na fixture `provider-effect.fixture.txt`, de `before\n` para `after\n`.
7. Aguardar broker/escritor terminarem, exigir ExitCode=0, Jobs vazios e bytes alterados. Reexecutar reconciliação: `UnknownBlocked`, replay bloqueado. Nenhuma OLD reexecutada.
8. A matriz passa a exigir **22 casos** `PASS_CONTAINMENT`, zero recuperações. Campos novos do caso: `provider_client_exited_before_worker_spawn` e `provider_worker_spawned_after_reconcile` ambos verdadeiros.

**Falha de segurança que este teste pode refutar:** qualquer tentativa de promover `OLD` porque o fornecedor terminou, o Job está vazio e nenhuma instância de escritor externo é visível naquele instante. O teste constrói efeito futuro depois da consulta de estado, sem invocar APIs reais de instalação.

## Limites inegociáveis

- **NÃO** executa `msiexec.exe`, `MsiInstallProduct`, comandos de instalação/desinstalação, serviços SCM, UAC, Windows Update, MSIX, reparo, criador de ponto de restauração nem alterações em diretórios do usuário.
- É uma analogia **sintética** de um comportamento permitido pela documentação MSI. Não comprova que o Windows Installer real tenha delegado trabalho neste runner, nem mede seu estado.
- Identificação de processos por PID serve **somente para adquirir um handle e verificar a imagem de laboratório** e limpar os processos descartáveis; não autoriza rollback ou READY.
- `UNKNOWN` é a interpretação segura mesmo após a última escrita. O sucesso desta CI comprovará **contenção do contraexemplo**, não drenagem universal de efeitos, cold recovery, reboot, perda física de energia, estabilidade ou H3.
- Se a CI falhar, diagnosticar a razão real e não contar testes que não rodaram.

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8.** Sem WORK, merge, release, deploy, promoção de branch, infraestrutura ou operação no PC do usuário.

---

## Resultado final verificado no runner Windows

**[CI 41087305170](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41087305170) — `completed/success`**, job `124171724810`, commit técnico `bf314f17b5aceb98e9f3801fce4fc3a2212c467a`.

O log bruto `GF_PHASE1_RECOVERY_CASE` incluiu **22 resultados `passed`, todos `PASS_CONTAINMENT`**, nenhum `PASS_RECOVERY_LAB_ONLY`. O `GF_PHASE1_RECOVERY_REPORT` confirmou `expected_cases=22`, `failed=0`, `operational_ready=false`, `phase1_complete=false`, `operational_gates_closed=0`, `new_effects_quiescence_proven=false`, `real_power_loss_tested=false`, `supervisor_loss_recovery_proven=false`.

**Novo caso `provider-late-spawn-after-reconcile`**, leitura literal do relatório:
- `status=passed`, `classification=PASS_CONTAINMENT`;
- `provider_client_exited_before_worker_spawn=true`;
- `provider_worker_spawned_after_reconcile=true`;
- `outcome=UnknownBlocked` e `replay=UnknownBlocked`;
- `block_reason=REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`;
- `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`, `operational_ready=false`.

Os quatro testes regressivos `external-broker`, `external-broker-handoff`, `same-user-full-state-rollback-after-new` e `out-of-band-new-with-old-selected` também passaram em `UnknownBlocked` com replay bloqueado.

**Ganho incremental demonstrado:** o cliente sintético terminou, mas o trabalho futuro só foi criado **depois** da primeira reconciliação. Logo, nem a saída do processo fornecedor nem a ausência de escritor existente naquela fotografia bastam como autoridade de quiescência. A distinção é mais forte do que os testes anteriores em que um escritor já estava vivo antes da reconciliação.

**Limite:** não executou o Windows Installer real ou uma ação personalizada MSI de verdade. A ligação com `msidbCustomActionTypeAsync | msidbCustomActionTypeContinue = 0xC0` continua fundamentação documental da classe de risco, não prova empírica do serviço Windows Installer. `UNKNOWN` continua a escolha segura mesmo depois de o efeito inerte ser observado.

**Estado preservado:** nenhuma mudança em `Phase1Recovery.cs.txt`, GF distribuído, `main`, PR #65 Draft, BETA .66, políticas de Windows, serviço ou infraestrutura. Sem WORK, merge, release ou deploy. Fase 1 **ABERTA**, READY=false, G1–G8 BLOCKED 0/8.
