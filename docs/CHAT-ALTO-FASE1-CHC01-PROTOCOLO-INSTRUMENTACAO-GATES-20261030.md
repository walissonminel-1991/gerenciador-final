# GERENCIADOR FINAL — Fase 1 — C-HC01: protocolo verificável de instrumentação antes do teste GF LOW

**30/10/2026. ESTADO: `DESIGN_ONLY_NO_REAL_GF_LOW_EXECUTION`.** Este arquivo é um desenho de medição e critérios de refutação, **não é** implementação, resultado de experimento, requisito de produto nem autorização para CI. Não criar desktop, instalar serviço, alterar ACL/política Windows, executar GF real com LOW ou modificar o C# L18 nesta rodada.

## 1. Base auditada

- Repositório `walissonminel-1991/gerenciador-final`, única branch autorizada para escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial `4195ff9b8240cf73f642aa3a3de0b23bdcf7a2d3`. Último técnico L18 `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6`; Windows [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790) job `124252340866`: `SUCCESS`, 27/27 `PASS_CONTAINMENT`, zero recuperações.
- Protótipo inerte LOW `4499fef1d8e44085406d7e3ffecadaade5f545b0`; Windows [41114014768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41114014768) job `124263420669`: `SUCCESS`, filho LOW 4096 leu arquivo inerte médio e não o escreveu; controle gravou antes e depois. `H1=false`, `H2=false`, `H3_global=false`.
- Evidência real de startup **normal, não LOW**: Windows [41060137516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41060137516), job `124077509441`, `SUCCESS`: GF BETA LOCAL `--tray` executou pesquisa de UI e gravou arquivo de diagnóstico e de histórico; `all_writes_enumerated=false`.
- Fonte examinada no mesmo HEAD: `Program.cs`, `DiagnosticTrace.cs`, `LocalReliabilityHistory.cs`, `LocalMsixInventory.cs`, `RebootResidualReview.cs`, `phase1-composed/phase1_lab.py`, `phase1-composed/win32_process_observation.py`, `phase1-startup-effects/startup_effects_probe.py`, `h3-token-scope/low_integrity_effect_probe.py`.
- Checkpoint antecedente, corrigido quanto ao desktop: `docs/CHAT-ALTO-FASE1-H3-LOW-HEALTHCHECK-COMPATIBILIDADE-20261030.md`.

## 2. Pergunta falsificável e limite da hipótese

**`C-HC01/0`:** é possível observar uma execução funcional OLD/NEW do **GF real** com token de integridade LOW, atribuindo resultados de startup a classes de causa verificáveis e evitando afirmar `READY`, ausência global de efeitos ou recuperação segura?

Uma resposta **não** é obtida por apenas `CreateProcessAsUserW` ter retornado sucesso ou por `ExitCode=2`. O observador deverá distinguir processo realmente criado, nível de integridade efetivo, caminho da imagem, sucesso/erro de diagnóstico, capacidade de desktop, inventário e comportamento de UI. Até existirem meios seguros para demonstrar as causas, a resposta é **`INCONCLUSIVE`**.

**Restrição de segurança que impede o ensaio nesta missão:** o protótipo LOW é um teste sem UI; o GF é WinForms e um filho com token restrito no desktop padrão pode introduzir riscos de mensagens. A Microsoft recomenda desktop separado; configurar desktop e suas permissões **não está autorizado**. A possibilidade de usar o desktop herdado não equivale a aprovação de segurança.

## 3. Observações necessárias, autoridade de cada medição e falsos positivos

| Etapa | Instrumentação futura *proposta* | Campo de evidência | Falha que NÃO constitui prova |
| --- | --- | --- | --- |
| Preflight do runner | Confirmar Windows descartável, perfil BETA LOCAL, `RUNNER_TEMP`, ausência de qualquer estado GF preexistente, imagem teste esperada e endpoint remoto vazio; abortar em colisão | `environment_clean`, `real_gf_previously_present=false`, `artifact_pinned` | Redirecionar só `LOCALAPPDATA` por variável de ambiente e presumir que o Known Folder do .NET mudou |
| Preparação do token | `CreateRestrictedToken` a partir do próprio token do runner, `SetTokenInformation(TokenIntegrityLevel)`, `GetTokenInformation(TokenIntegrityLevel)` | `prepared_integrity_rid=4096` | Token LOW preparado mas processo real criado com outro token |
| Lançamento real (somente após autorização distinta) | `CreateProcessAsUserW` com caminho absoluto pinado, sem credenciais alternativas, `bInheritHandles=false`, reter handles de processo/thread, delimitar Job do laboratório | `creation_succeeded`, `owned_process_handle` | Erro de criação contado como MIC eficaz; retorno da API contado como GF inicializado |
| Verificação **pelo supervisor** | Consultar token do próprio processo criado usando `OpenProcessToken`/`GetTokenInformation`, sujeito a permissões. Consultar imagem com handle retido; conferir manifesto/pins separados; não confiar exclusivamente em relatório same-user | `observed_child_integrity_rid`, `observed_image_match` | Apenas PID, janela, recibo Python, token da fixture ou hash do disco apresentado como identidade confiável da imagem em memória |
| Duas rotas diagnósticas | Observar separadamente acesso e resultado da tentativa ao diretório de diagnósticos e ao `Path.GetTempPath()`; comparar objeto, token, DACL e política MIC quando obtidos por meios não destrutivos | `diagnostic_profile_write_outcome`, `diagnostic_temp_write_outcome` | Ausência de log, `LastFailure` com um tipo de exceção ou `ExitCode=2` apresentados como **prova de duas negativas MIC** |
| Desktop/UI | Recolher, quando direitos permitirem, estação/desktop do processo e thread via APIs de consulta; usar observador existente e registrar separadamente HWND, nonce, estado vazio, restauração e WM_CLOSE | `desktop_observable`, `ui_contract`, `graceful_close` | Não achar HWND e concluir que o processo nunca executou; não diferenciar desktop inacessível de falha anterior de startup |
| Inventário | Usar somente controles de UI conhecidos e estado de varredura disponível; mensagens de falha MSIX/Registro são diagnóstico, não autorizações. Nunca executar manutenção | `baseline_nonzero`, `inventory_complete_evidence` | Contagem não zero usada para afirmar completude de Registro, COM e fontes de jogos |
| Preservação | Medir hashes de canários próprios, arquivos OLD/NEW, seletor, fences e apenas superfícies GF definidas; reconhecer que a lista é **incompleta** | `protected_artifacts_unchanged`, `all_effects_enumerated=false` | Ausência de mudanças nos caminhos medidos usada como prova de quiescência de brokers ou do host |
| Encerramento | Observar `WM_CLOSE` só na janela inequivocamente pertencente ao processo; se falhar, encerrar somente por handle/job retido do laboratório, sem equivaler kill a shutdown gracioso | `graceful_exit`, `forced_cleanup_required` | Kill forçado contado como sucesso funcional ou cancelamento global dos efeitos delegados |

### A lacuna de atribuição é concreta, não cosmética

Em `DiagnosticTrace.cs:43–56,61–115`, o GF tenta o perfil local e, depois, a pasta temporária. Os erros são capturados e a propriedade `LastFailure` mantém **o último tipo de exceção**, não o objeto, a operação, a causa MIC ou o resultado de **cada** tentativa. Em `Program.cs:26–34` o código `2` demonstra apenas a **falha do diagnóstico exigido**. A instrumentação externa atual observa relatório de UI/arquivos, mas **não intercepta de maneira verificável as duas operações de abertura**. Logo, sem observador adicional de acesso que seja preciso e autorizado, `DIAGNOSTIC_WRITE_DENIED` **não pode ser atribuído** a MIC.

Mesmo uma comparação entre LOW negado e controle MEDIUM permitido seria uma **evidência diferencial**, não prova exclusiva de MIC se outros atributos de token, DACL, identidade do objeto, diretório-pai ou ambiente diferirem. Manter erro `ACCESS_DENIED` separado de origem `MIC_CONFIRMED`.

**Instrumentação invasiva desautorizada:** não inserir hooks/DLL no GF, interceptar syscalls, usar driver/ETW privilegiado, ProcMon em computador pessoal, debuggers que alterem processo, editar `DiagnosticTrace` ou suprimir logs para facilitar um verde. Um teste sem essas fontes pode registrar **resultados limitados** e terminar `INCONCLUSIVE` sem executar GF LOW.

## 4. Taxonomia conservadora, precedência e critérios de rejeição

**Regra:** tratar falhas de pré-condição como `ENVIRONMENT_UNSAFE_ABORTED` (**sem executar GF**). Para observação executada, escolher o resultado mais específico que tenha **evidência independente suficiente**; se houver duas causas plausíveis, classificar `INCONCLUSIVE`.

| Resultado possível | Evidência mínima necessária | Resultado se faltar prova |
| --- | --- | --- |
| `ENVIRONMENT_UNSAFE_ABORTED` | Perfil já existente, janela/desktop inseguro, endpoint não isolado, caminho não pinado ou token não autorizado | Não iniciar processo |
| `CHILD_NOT_CREATED` | Erro real em `CreateProcessAsUserW`, com Win32 error e zero filho | `INCONCLUSIVE`; não chamar contenção de efeitos |
| `TOKEN_NOT_VERIFIED` | Processo iniciou, mas supervisor não consegue confirmar seu token LOW | `INCONCLUSIVE` |
| `DIAGNOSTIC_WRITE_DENIED` | Filho LOW comprovado; erro de abertura atribuível e documentado **nas duas rotas reais** do diagnóstico; exit 2; GF não alcançou `Application.Run`; atribuição MIC só se comprovada em separado | `STARTUP_DIAGNOSTIC_FAILED_UNATTRIBUTED` ou `INCONCLUSIVE` |
| `DESKTOP_UNAVAILABLE` | Processo vivo e startup executado além do diagnóstico; identidade do desktop/thread e restrição de acesso identificadas; falha específica de GUI, não mera ausência de HWND | `INCONCLUSIVE` |
| `INVENTORY_PARTIAL` | Janela e pesquisa acessíveis; falha/parcialidade de fonte **observada explicitamente** (COM/MSIX/Registro/jogos), sem inferir ausência de apps | `INCONCLUSIVE` se não houver sinal específico |
| `FUNCTIONAL_OBSERVATION_PASSED` | Token LOW do GF confirmado pelo supervisor; imagem correta; desktop acessível; inventário base não vazio; nonce → vazio → restauração; `WM_CLOSE` e saída graciosa; sentinelas intactas | `INCONCLUSIVE`, jamais `READY=true` |

**Importante:** `FUNCTIONAL_OBSERVATION_PASSED` não prova inventário integral, ausência de efeitos externos, falta de novos processos, que o GF esteja saudável em uso real, nem disponibilidade de rollback. Se uma execução tiver saída graciosa, mas inventário incompleto, a classe deve refletir a parcialidade, não sucesso pleno.

## 5. Plano de controle e de não contaminação — exigência de autorização futura

**C0 (sem executar GF):** fazer apenas verificação estática do contrato, documentação e referências das APIs. **Este é o único estágio aprovado nesta rodada**.

**C1 (prova inerte já existente):** LOW x MEDIUM para arquivo próprio inerte, com sucesso [41114014768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41114014768). **Não repetir.**

**C2 (futuro, dependente de aprovação):** controle `GerenciadorFinal.exe --tray` com token original e perfil fresco da runner descartável, sem reutilizar arquivos de um teste LOW anterior. A execução normal já foi observada em [41060137516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41060137516); nova execução só se um controle pareado realmente resolver atribuição causal. Nunca remover uma instalação preexistente para forçar perfil limpo.

**C3 (futuro, NÃO AUTORIZADO):** GF real com token LOW **somente se** houver desenho aprovado de desktop seguro e permissões, instrumentação das duas rotas diagnósticas e método de atribuição independente. O próprio startup pode gravar no perfil e usar COM/WinRT. A execução real não tem caráter read-only universal, mesmo se a ação de usuário limitar-se a pesquisar apps.

**C4 (sempre):** reconciliar `UnknownBlocked`, `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`, `READY=false`; não chamar `BeginCore` nem autorizar OLD, rollback, limpeza de resíduos ou alteração do seletor a partir desses resultados. Nenhuma mudança na política H1/H2/H3.

**Critério para nova CI:** precisa existir (a) autoridade específica para GF LOW e, se necessário, desktop isolado, (b) medição da causa das duas falhas que não dependa só de um arquivo local same-user, (c) controle pareado protegido contra contaminação, (d) condição de rejeição que possa diferenciar `DIAGNOSTIC_WRITE_DENIED` de `DESKTOP_UNAVAILABLE` e `INCONCLUSIVE`, (e) teste num runner descartável sem dados pessoais, privilégios ou infraestrutura de BETA conectada. Se qualquer requisito falhar, **NO-GO de execução**.

## 6. Formato de relatório futuro — CONTRATO NÃO EMITIDO

`gf-phase1-chc01-observation/0` é uma **proposta de esquema**, não o nome de um relatório já gerado. Se implementado, deve emitir apenas enumerações/booleanos, jamais caminhos pessoais, textos de usuário, aplicativos instalados ou hashes de arquivos particulares:

- `status`: um dos estados acima, inclusive `INCONCLUSIVE`.
- `gf_real_launched`: bool, e `observed_child_integrity_rid`: 4096 somente se comprovado externamente.
- `diagnostic_profile_write_outcome`, `diagnostic_temp_write_outcome`: `ALLOWED|DENIED|OTHER_ERROR|NOT_OBSERVED`.
- `diagnostic_failure_attribution`: `MIC_CONFIRMED|ACCESS_DENIED_UNATTRIBUTED|OTHER_ERROR|NOT_PROVEN`.
- `desktop_observable`, `inventory_complete_evidence`, `functional_search_observed`, `graceful_exit_observed`, `sentinels_preserved`.
- `read_only_health_not_global_read_only=true`, `all_effects_enumerated=false`.
- `h1_proven=false`, `h2_proven=false`, `h3_global_proven=false`, `external_launch_blocked=false`, `broker_quiescence_proven=false`, `operational_ready=false`, `automatic_recovery_authorized=false`, `gates_closed=0`.

**Nunca gerar** `status=FUNCTIONAL_OBSERVATION_PASSED` a partir de PID/HWND isolados, recibo do processo, escrita de log ou comparação de hash do arquivo em disco; a classificação exige o exercício funcional completo e seus limites permanecem declarados.

## 7. Fontes primárias e transferência

- `CreateProcessAsUserW`: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw — `lpDesktop=NULL` herda desktop/window station do pai; acesso ao desktop e interatividade não são garantidos.
- `Restricted Tokens`: https://learn.microsoft.com/en-us/windows/win32/secauthz/restricted-tokens — recomendação expressa de desktop separado para aplicação com token restrito.
- `Mandatory Integrity Control`: https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control — acesso a objetos, `NO_WRITE_UP` versus DACL; não há contenção global de brokers.
- `GetTokenInformation`: https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation — consulta de atributos com direitos adequados, não autentica história global de execução.
- `GetUserObjectInformationW`: https://learn.microsoft.com/pt-br/windows/win32/api/winuser/nf-winuser-getuserobjectinformationw — metadados da estação/desktop sob acesso permitido.
- `Desktop Security and Access Rights`: https://learn.microsoft.com/en-us/windows/win32/winstation/desktop-security-and-access-rights — segurança/ACL do desktop.

**Decisão:** `C_HC01_INSTRUMENTATION_NOT_YET_SAFELY_AVAILABLE`. O único avanço aprovado é um modelo de observação falsificável, com critérios de recusa. No envelope de autorização atual, **não** produzir mais um teste que reportaria apenas `ExitCode=2` ou ausência de HWND e afirmaria MIC/health. Retomar investigação H1/H2/H3 somente com mecanismo independente e nova propriedade demonstrável.

**Fase 1 ABERTA. `READY=false`. G1–G8 `BLOCKED 0/8`. H1/H2/H3 global NÃO demonstradas. Zero recuperações automáticas autorizadas.** Preservar main, PR #65 Draft, BETA .66, TUF/B+A/NTFS/TOCTOU, credenciais, telemetria, infraestrutura e PCs pessoais. Sem WORK, merge, release, deploy, serviço/helper/tarefa privilegiada, ACL/política Windows, TPM, backend, nova CI ou integração operacional.
