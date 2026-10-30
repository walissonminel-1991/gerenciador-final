# GERENCIADOR FINAL — Fase 1 — Auditoria estática: compatibilidade do health-check OLD/NEW com token LOW

**Data:** 2026-10-30. **Classificação:** `NO_GO_DROP_IN_LOW_HEALTHCHECK_CURRENT_CONTRACT`. Decisão restrita: **não substituir diretamente o lançamento normal do GF pelo launcher LOW inerte**, sem provar compatibilidade das escritas do startup, desktop herdado e observação funcional. **Não** é prova de que um GF adaptado não possa executar sob LOW.

**RETIFICAÇÃO TÉCNICA (30/10/2026):** a versão anterior afirmava erroneamente que `CreateProcessAsUserW` com `STARTUPINFO.lpDesktop=NULL` cria por padrão num desktop não interativo. A documentação da Microsoft confirma que **NULL herda a window station e o desktop do processo pai**. Uma string vazia tem semântica diferente. O desktop efetivo e os direitos do token LOW **não foram medidos**. Esta retificação remove a incompatibilidade de desktop presumida, mas **não altera o NO-GO restrito** por escritas do startup e compatibilidade funcional não demonstrada. Fonte: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw (parâmetro `lpStartupInfo`).

## 1. Identidade, integridade e método

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial confirmado `7cd1869349a4d7c4a6f566ec6a1b5ea28c98970a`. Último commit técnico C# L18 validado `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6`, CI Windows [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790), job `124252340866`, `SUCCESS`, **27/27 PASS_CONTAINMENT**, zero recuperações automáticas.
- Protótipo LOW inerte validado no commit `4499fef1d8e44085406d7e3ffecadaade5f545b0`; CI Windows [41114014768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41114014768), job `124263420669`, `SUCCESS`: LOW lê, escrita em arquivo de integridade superior negada; controle de integridade original escreve. **Não executou GF real.**
- Evidência pré-existente independente e relevante: [CI startup 41060137516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41060137516), job `124077509441`, `SUCCESS`, observou `GerenciadorFinal.exe --tray` em BETA LOCAL com **criação de diagnósticos e histórico**, pesquisa funcional mínima, sentinelas preservadas. `all_writes_enumerated=false`, `external_brokers_quiescent_proven=false`, `valid_postboot_pending_reconciliation_tested=false`, `operational_ready=false`.
- Para evitar atribuir prova histórica ao código errado, foram **comparados blob SHAs** entre o commit da CI startup `0c0b02e2acb087ec18ef81f1273636a6ea95f5c7` e o HEAD auditado. Permanecem **idênticos**: `Program.cs` (589a515c302b055a55bbac52c2792f24472c526b), `DiagnosticTrace.cs` (05544869a8e6e031a6a1afbff0616a6959a07a63), `LocalReliabilityHistory.cs` (2ded3c0717726d100d4c2affabbc0050223979c1), `LocalMsixInventory.cs` (f4359406e3c6a9b2500e22e1145dece3b7be1b38), `phase1-startup-effects/startup_effects_probe.py` e respectivo workflow.
- Método: **leitura de fonte** + CI pré-existente (execução normal, não LOW) + documentação oficial Windows. Sem alteração de código, nova execução do GF, CI, serviço, ACL, política Windows, desktop, token ou produto nesta rodada.

## 2. O contrato real de observação, passo a passo

1. **Preparação:** `phase1-composed/phase1_lab.py:423–449` revalida manifesto TUF, bytes e pacotes OLD/NEW em `--recovery-verify`. Trata arquivos como dados; não chama o GF. A validação de dados **não é** uma prova de health nem de `READY`.
2. **Lançamento:** `phase1_lab.py:281–343, 450–490` resolve o slot via seletor C# e inicia `GerenciadorFinal.exe --tray` pelo Python `subprocess.Popen`, com `GfBetaEndpoint=""`, sem instalar ou atualizar pacotes. A observação exigida é um processo **real** de GF OLD ou NEW, não mock.
3. **Janela/identidade:** `phase1-composed/win32_process_observation.py:79–127,297–358` consulta caminho da imagem, hash do arquivo em disco, sessão/desktop, encontra HWND principal pertencente ao PID, aguarda message loop e lê controles da UI. Um HWND e o hash do arquivo em disco **não provam imagem carregada, saúde integral ou READY**.
4. **Pesquisa de UI:** `win32_process_observation.py:130–286` identifica o campo EDIT associado a `Pesquisar:`, envia `WM_SETTEXT` com nonce gerado no observador, procura o estado vazio esperado `Nenhum aplicativo corresponde à pesquisa.`, recupera o campo para vazio e exige inventário inicialmente não vazio. Esse é um **efeito local sobre estado de UI**, não uma consulta passiva. Não executa manutenção.
5. **Encerramento:** `win32_process_observation.py:358–384` exige `WM_CLOSE` no HWND observado e saída graciosa, em tentativas limitadas. `phase1_lab.py:323–343` trata kill/timeout só como limpeza de fixture, nunca health comprovado.
6. **Decisão:** `phase1_lab.py:475–489` exige `created`, `terminated`, identidade, shutdown gracioso e `minimum_readonly_health_contract_observed=true`. O próprio relatório mantém `ready_verified=false` e `application_functional_health_verified=false`, e o coordenador L18 continua `UnknownBlocked`.

## 3. Ações do próprio GF **antes** e durante o health-check

| Caminho observado na fonte | Efeito/depêndencia real | Por que LOW é problemático | Estado da evidência |
| --- | --- | --- | --- |
| `Program.cs:26–34` → `DiagnosticTrace.Start` | **Obrigatoriamente** cria/escreve e confirma log local antes de `Application.Run`; falha resulta em mensagem de erro e exit 2 | Processo LOW normalmente não pode gravar em objetos médios. `DiagnosticTrace.cs:43–115` tenta `%LOCALAPPDATA%/GerenciadorFinal/Diagnosticos`, depois pasta temporária; **não foi verificada** a integridade e DACL efetivas dos dois diretórios no runner LOW | Exigência comprovada por código; criação observada em startup normal. **Falha LOW provável, mas não testada no GF real** |
| `MainForm` (campo `_reliability` em `Program.cs:593`) | `LocalReliabilityHistory.cs:25–49` cria pasta/arquivo de histórico e escreve cabeçalho | LOW pode negar escrita no perfil normal, mas esse componente trata a falha e segue em memória; não é por si só um bloqueio fatal de startup | Código verificado; gravação observada em startup normal, não LOW |
| `Program.cs:655–710,794–818` | Constrói formulário WinForms, janela/bandeja, `Shown`; inicia carga local mesmo em `--tray` | Exige desktop e message loop acessíveis ao observador, diferentes da prova de acesso a arquivo inerte | Fonte verificada, desktop LOW não ensaiado |
| `Program.cs:2235–2274,240–293` | Inventário Win32 do registro, MSIX do usuário, arquivos de bibliotecas de jogos e fontes de ecossistema; sem executar desinstalações neste caminho | Leitura pode ser parcial sob token restrito. Status parcial ou contagem nula não pode ser promovido a observação funcional equivalente | Fonte verificada; comportamento COM/registro com LOW não testado |
| `LocalMsixInventory.cs:102–132` | `PackageManager()` e `FindPackagesForUser(string.Empty)` (WinRT/COM) | Permissões, ativação e dependências do broker sob LOW não foram medidas; não inferir que somente leitura seja livre de efeitos ou equivalente a quiescência | Código verificado, teste LOW inexistente |
| `Program.cs:2508–2531`, `BetaRuntimePolicy.cs:17–18` | `BeginBackgroundMemoryMonitoring` antecede o gate; em BETA LOCAL, checagens automáticas online são desabilitadas. Em perfil não local podem ocorrer HTTPS e WUA/COM | Não generalizar o caso de BETA LOCAL para outros perfis; o caminho funcional inclui atividade assíncrona não observada exaustivamente | Código verificado; testes restringem perfil local |
| `Program.cs:819–835` → `RebootResidualReview.cs:114–159` | Confere pendências após boot. Se existirem pendências de outro boot, pode **deletar/regravar** arquivo de controle após leitura | A fixture de startup anterior comprovou apenas preservação de arquivo pendente **inválido**, não processamento real de pendência válida | Código verificado; caso real condicionado não testado |
| `Program.cs:35–66` e `AnonymousTelemetry.cs:31–47` | Cria contexto de telemetria; BETA LOCAL recusa coleta colaborativa, mas o modo `--tray` não define isso sozinho | Não afirmar ausência geral de I/O/COM/rede; ambiente de CI define `GfBetaEndpoint=""` | Código verificado; nenhum envio remoto autorizado |

**Resultado central:** o contrato se chama `readonly_inventory_search_health` porque **não aciona comandos de instalação/desinstalação**, não porque todo o processo GF seja read-only. Há escritas locais condicionais e obrigatórias antes da pesquisa. A evidência de startup normal provou dois tipos de escrita, com `all_writes_enumerated=false`.

## 4. Fronteira UI e isolamento: incompatibilidade específica do protótipo LOW atual

- `h3-token-scope/low_integrity_effect_probe.py` usa `CreateRestrictedToken`, `SetTokenInformation(TokenIntegrityLevel)` para `S-1-16-4096`, e `CreateProcessAsUserW` com `STARTUPINFOW.lpDesktop` **não configurado**. Prova concreta: seu filho Python lê e falha ao gravar fixture, sem qualquer UI WinForms.
- **Correção do contrato da API:** em `CreateProcessAsUserW`, `STARTUPINFO.lpDesktop=NULL` **herda window station e desktop do processo pai**, em vez de forçar desktop não interativo. Um runner com desktop não interativo transmitirá esse contexto; com desktop interativo o resultado depende das permissões do filho. O observador atual depende de HWND acessível, mensagens Win32, pesquisa e WM_CLOSE; **o ensaio inerte LOW não comprovou essa compatibilidade nem a excluiu**. Não inferir uma falha obrigatória de desktop apenas a partir de `lpDesktop=NULL`.
- UIPI impede mensagens de processos de integridade inferior para superior. Em geral mensagens de um observador MEDIUM para um destino LOW são permitidas conforme a API, **mas isso não resolve** o isolamento de desktop, acesso ao processo, os direitos de janela ou a segurança de mensagens.
- A Microsoft alerta que aplicações com token restrito devem usar desktop diferente do desktop padrão para reduzir ataques por mensagens contra aplicações irrestritas. A viabilidade e proteção de uma UI LOW interativa **não** foram demonstradas e não autorizam alterações em DACL/desktop/política nesta missão.

Fontes oficiais:
- MIC e `NO_WRITE_UP`: https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control
- Tokens restritos, riscos de desktop/mensagens: https://learn.microsoft.com/en-us/windows/win32/secauthz/restricted-tokens
- Desktop padrão do `CreateProcessAsUser`: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw
- UIPI e `SendMessage`: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessage

## 5. Decisão, contraprovas futuras e limites

**Decisão atual: `NO_GO_DROP_IN_LOW_HEALTHCHECK_CURRENT_CONTRACT`.** É incorreto tratar a CI LOW inerte como indicação de que OLD/NEW GF executado com LOW passaria no health-check atual, ou de que H3 foi resolvida. O contrato de health-check exige escritas no próprio aplicativo e interface/interação que o protótipo LOW não demonstrou.

**Contraexemplos futuros úteis, ainda NÃO executados:**
- **C-HC01:** em ambiente Windows descartável e com perfil de arquivo já existente, um GF real lançado LOW não consegue completar `DiagnosticTrace.Start` e retorna antes de criar HWND, mesmo que o processo tenha sido criado. Confirma que falta uma arquitetura de observação sem escrita, não justifica suprimir diagnósticos sem outro mecanismo.
- **C-HC02 (corrigido):** medir o desktop e a window station efetivamente herdados pelo filho LOW e a acessibilidade ao observador. A ausência de HWND pode decorrer de falha anterior de diagnóstico, acesso a desktop, WinForms ou inventário; não prova automaticamente que `lpDesktop=NULL` escolheu um desktop diferente, nem ausência de efeitos.
- **C-HC03:** interação UI funciona, mas `PackageManager`, registro ou fontes de inventário falham/retornam parcial; não aceitar `apps=0` como health e não executar maintenance para compensar.
- **C-HC04:** uma execução GF LOW consegue observar a UI mas delega trabalho a broker/serviço externo, que mantém capacidade de modificar objetos MEDIUM após seu encerramento. Restrição LOW **não** garante que o broker herde o token nem H3 global.
- **C-HC05:** um segundo processo NEW iniciado por U fora de LOW continua possível; H2 permanece sem despacho obrigatório.

**Próximo avanço racional:** projetar primeiro um contrato distinto e falsificável de `DATA_ONLY_VERIFY` (já existente) e, **se vier autorização**, especificar um *health oracle* somente leitura e sem UI/efeitos obrigatórios **antes de qualquer inicialização gravadora**. Qualquer modo novo do GF seria alteração do produto e está **fora** da autorização atual. Não simular READY nem usar um recibo do processo LOW como autoridade. Não criar desktop, ACL, política, helper, serviço ou executar GF sob token LOW nesta rodada.

**Estado inalterado:** Fase 1 **ABERTA**, `READY=false`, G1–G8 `BLOCKED 0/8`; H1, H2 e H3 global **não demonstradas**; `UnknownBlocked` e `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` continuam obrigatórios. Sem nova CI, WORK, merge, release, deploy, promoção, modificação do GF operacional, TPM, backend, telemetria ou PCs pessoais.

## 6. Critérios falsificáveis de C-HC01 — desenho, NÃO execução

**Status: `DESIGN_ONLY_NEEDS_SEPARATE_EXPLICIT_RUN_AUTHORIZATION`.** Sem execução de GF real sob LOW, criação de desktop/ACL/política Windows, nova CI ou alteração do produto.

**P1 — Runner e perfil isolados:** um futuro teste deverá usar Windows descartável, GF BETA LOCAL sem endpoint remoto e diretório de perfil inicialmente sem arquivos do GF; abortar se existir estado prévio, sem apagar dados. Não presumir que alterar a variável `LOCALAPPDATA` redirecione `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` no .NET.

**P2 — Confirmar execução, não apenas falha:** exigir prova de que o processo real iniciou e seu token tem SID LOW `S-1-16-4096`. Erro ao criar processo, token não verificável ou falta de capacidade de observar o filho = `INCONCLUSIVE`, nunca contenção aprovada.

**P3 — Especificar as DUAS rotas diagnósticas:** `DiagnosticTrace.Start()` tenta `%LOCALAPPDATA%/GerenciadorFinal/Diagnosticos` e, se falhar, `Path.GetTempPath()`. Um C-HC01 positivo precisaria associar saída `2` e falha de ambas as gravações à restrição; o caminho `%TEMP%` e suas permissões reais não foram testados. Um único erro de escrita não demonstra que o GF parou por MIC.

**P4 — Discriminar as causas:** medir saída de `Program.Main` antes de HWND, capacidade de criar janela no desktop herdado, leitura de inventário COM/WinRT e fluxo nonce → resultado vazio → limpar → `WM_CLOSE`. Resultados distintos: `DIAGNOSTIC_WRITE_DENIED`, `DESKTOP_UNAVAILABLE`, `INVENTORY_PARTIAL`, `FUNCTIONAL_OBSERVATION_PASSED`, ou `INCONCLUSIVE`. O último resultado funcional não significa READY.

**P5 — Controle independente e sentinelas:** comparar com execução normal sob o mesmo contrato em perfil descartável sem contaminação entre casos; preservar sentinelas, nunca executar manutenção. O run normal de startup `41060137516` é referência relevante, mas não substitui controle com condições equivalentes. Não relaxar critérios de health para obter sucesso.

**P6 — Segurança de UI:** o Windows recomenda desktop separado para aplicações com token restrito por riscos de mensagens; portanto um futuro experimento de GUI LOW exige análise de desktop/ACL/acesso e autorização nova. A API com `lpDesktop=NULL` herda contexto, mas **herdar não significa que o isolamento de UI seja seguro**.

**Regra de gasto:** não repetir a CI de arquivo LOW; não iniciar GF LOW real apenas para contar um novo caso. Autorizar execução exclusivamente depois de provar que a instrumentação discrimina os cinco resultados acima e que o ensaio não altera o produto, o usuário ou as políticas Windows.

**Limite preservado:** mesmo que C-HC01 consiga um health-check funcional, H1/H2/H3 globais continuam não demonstrados, `UnknownBlocked` obrigatório, Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8.
