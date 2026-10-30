# GERENCIADOR FINAL — Fase 1 — Identidade e resultado dos provedores MSI, MSIX e Windows Update

**30/10/2026 | Parecer de arquitetura baseado no código real e na documentação oficial Microsoft.**
**Sem nova execução Windows, instalação, desinstalação, serviço ou implementação de autoridade.**

## 1. Âncora auditável e fronteira de escrita

- Repositório: `walissonminel-1991/gerenciador-final`. Única branch autorizada para escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD antes da escrita: `44f2fefcd331de5837ae51729de6d55ef9b651f4`, **confirmado diretamente na branch**.
- Último commit técnico validado: `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`.
- CI Windows existente [41089673054](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41089673054), job `124179955658`: **SUCCESS**, 22/22 `PASS_CONTAINMENT`, 0 recuperações, 0 falhas. O ensaio NTFS de atribuição causal é **separado** dos 22 casos e também passou.
- PR #65 consultada: aberta, Draft e sem merge.
- Código lido: `VerifiedUpdaters.cs` (lançamento `msiexec.exe`/`UseShellExecute=true` e acompanhamento de processo), `MsiPackageEvidence.cs` (`ProductCode`, `UpgradeCode`, `MsiGetProductInfo[Ex]W`); `WindowsTerminalUpdater.cs` (`PackageManager.AddPackageAsync`, callback `Completed`, reconsulta de versão); `PendingDeployment.cs`; `WindowsUpdateSearchPolicy.cs` (GUID@revisão, distinção `ResultCode=2/3`); `Program.cs` (despacho para `WindowsUpdateClient.InstallAsync` e `--worker`). Esta seleção NÃO é uma auditoria completa dos componentes Windows Update.
- **Não modificar** `Phase1Recovery.cs.txt`, `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`, B+A, TUF, NTFS, C# L18, `main`, PR #65 Draft, BETA .66, infraestrutura, PCs, credenciais ou telemetria.

## 2. Garantias que precisam permanecer separadas

**I — Identidade de produto/pacote:** permite descobrir a entidade solicitada e comparar versão/assinatura. Não é identidade da invocação nem autoriza retrocesso da transação L18.

**O — Identidade de operação:** permite correlacionar um conjunto delimitado de eventos de determinado provedor. Não vincula, por si só, a operação a um `transactionId` e decisão de lançamento do L18.

**R — Resultado do provedor:** permite saber se sua API terminou ou declarou erro/pendência. Não equivale a quiescência universal dos efeitos externos.

**Q — Quiescência com autoridade:** encerramento independente e durável de todas as classes de efeitos relevantes, inclusive delegações e reinícios. Nenhuma das APIs analisadas prova **Q** sem um contrato adicional específico e cobertura comprovada.

Atributos de I/O observáveis, File ID, SHA-256, TUF, GUID emitido no processo do GF, código de saída de um cliente, Job vazio ou ausência de evento não substituem **O+R+Q**.

## 3. Inventário de APIs e limites

| Provedor | Sinal/identificador real | Propriedade limitada que pode sustentar | O que **não** autoriza |
| --- | --- | --- | --- |
| MSI comum | `ProductCode`, `PackageCode`, `UpgradeCode`, `MsiGetProductInfo[Ex]W` | Identidade de produto, pacote, família e informações da instância instalada | Não é ID exclusivo de uma execução de `msiexec`; não cobre ação customizada assíncrona, execução de terceiros ou anti-rollback L18 |
| MSI transação multipacote | `MsiBeginTransaction` => `MSIHANDLE`; `MsiJoinTransaction`; `MsiEndTransaction` | O Windows Installer oferece **transação de instalação multipacote**, controlada pelo proprietário do handle, em condições específicas | **Não** é transação automática de todo `msiexec`, nem ID persistente pós-crash, nem atomicidade com GF/TUF/NTFS ou efeitos fora do escopo MSI |
| MSIX | `PackageManager.AddPackageAsync` => operação assíncrona; `DeploymentResult.ActivityId`, `IsRegistered`, `ExtendedErrorCode` | `ActivityId` correlaciona eventos **daquela operação de implantação**; `IsRegistered` informa registro do pacote | Não prova que todos os efeitos de NEW cessaram, não é atestado assinado do vínculo ao L18, e não elimina perda da evidência após crash do chamador |
| Windows Update Agent | `IUpdateIdentity.UpdateID` + `RevisionNumber`; `IInstallationResult.ResultCode`, `GetUpdateResult(i)`, `RebootRequired` | Identifica atualização/revisão; apresenta resultado agregado, resultados individuais e exigência de reinicialização | `UpdateID@Revision` não identifica uma chamada única de instalação; sucesso do método não implica instalação **finalizada** quando `RebootRequired=true` |

**Nuance relevante:** é incorreto declarar que **nenhuma API MSI possui identificador de transação**. `MsiBeginTransaction` realmente retorna um handle para *multiple-package installations*, disponível desde Windows Installer 4.5. O caminho atual em `VerifiedUpdaters.cs` usa `Process.Start(msiexec.exe)`; não chama nem demonstra participar desse protocolo. O handle multipacote não se transforma automaticamente em autoridade persistente de lançamento do GF e não cobre todos os fornecedores ou ações externas.

**Nuance MSIX:** o trecho atual de `WindowsTerminalUpdater.BeginNativeDeploymentAsync` marca o `TaskCompletionSource` concluído ao receber `AsyncStatus.Completed`, mas só consulta `o.GetResults()` no ramo de erro. Logo, esse trecho **não coleta `ActivityId` nem `IsRegistered` no sucesso**. Há validação de identidade do pacote e reconsulta da versão em outros trechos do fluxo; **não concluir que há uma falha de atualização operacional apenas por essa omissão**, nem alterar produto durante a Fase 1. `ActivityId` seria útil para diagnóstico de implantação, não para habilitar rollback L18.

**Nuance WUA:** `OperationResultCode` distingue `orcSucceeded=2` de `orcSucceededWithErrors=3` (resultado potencialmente incompleto), `orcFailed=4` e `orcAborted=5`. Mesmo com `orcSucceeded=2`, `RebootRequired=true` atesta a necessidade de reiniciar para completar a operação. `WindowsUpdateSearchPolicy.RequireSuccessfulSearch` já recusa resultados de *busca* diferentes de 2; não inferir daí que **toda instalação** WUA foi auditada pelo mesmo contrato.

## 4. Quatro contraprovas concretas de inferência (fundamentadas em API, não rodadas como ensaio)

**P1 — WUA sucesso com reinicialização:** dado `ResultCode=orcSucceeded` e `RebootRequired=true`, é incorreto concluir `ready_to_roll_back_GF=true` ou `all_effects_quiescent=true`. O próprio provedor informa que a operação exige reinício para completar. *Não se está afirmando que esse retorno tenha ocorrido em um teste da branch.*

**P2 — WUA resultado agregado vs item:** dado um retorno agregado e uma coleção de atualizações, `GetUpdateResult(i)` fornece resultados específicos por índice. Um recibo agregado não prova a mesma conclusão para todos os itens por identidade de atualização. Ausência ou troca do mapeamento índice/identidade não autoriza atribuição à transação GF.

**P3 — MSI produto igual, invocações diferentes:** duas operações distintas de reparo/manutenção podem referenciar o mesmo `ProductCode`. Logo, observar o produto e versão após uma operação não identifica qual invocação os causou. `MsiBeginTransaction` é uma exceção explícita, mas depende da criação da **transação multipacote**; não extrapolar para `Process.Start(msiexec)`.

**P4 — MSIX ActivityId válido sem vínculo GF:** um `ActivityId` real é correlação **do provedor**. Se o vínculo `ActivityId <-> transactionId_GF` for escrito somente em arquivo reversível do usuário, esse vínculo pode ser trocado ou retrocedido por adversário same-user sem falsificar a atividade interna do provedor. A leitura de eventos também não prova ausência de efeitos externos não abrangidos.

**Em comum:** são contraexemplos lógicos a **inferências** inseguras e vêm dos contratos documentados; nenhum desses quatro é um caso de CI novo. Não chamá-los de 26/26, nem alegar ensaio de MSI, MSIX ou Windows Update.

## 5. Atribuição confiável e quiescência: requisitos que faltam

Uma futura prova de recuperação automática de OLD após potencial execução de NEW teria de atender cumulativamente:

1. **Fronteira de confiança:** autorização de lançamento resistente a rollback same-user e dispatcher não contornável no escopo afirmado, ou recusar recuperação.
2. **Vínculo transacional:** registrar de forma **não revertível pelo mesmo usuário** o par `(tx L18, identidade de pacote TUF, identidade de operação do provedor, escopo)`, com política sobre replay/reutilização.
3. **Confirmação pós-crash:** especificar como recuperar o identificador/resultado do provedor **quando o processo chamador morreu antes de persistir a resposta**; ausência de recibo é `UNKNOWN`.
4. **Finalidade de R:** verificar conclusão da própria API e sinalizar separadamente `SucceededWithErrors`, `IsRegistered` e `RebootRequired`; sucesso parcial ou reinício necessário **não** promove READY.
5. **Cobertura Q:** impedir ou demonstrar esgotados escritores futuros, brokers, serviços, ações customizadas e efeitos indiretos de todos os provedores envolvidos; não inferir de Job/PID/ExitCode/ActivityId.
6. **Escopo real:** como o autoatualizador portátil GF é distinto da instalação de apps de terceiros, **resultado de uma instalação MSI/MSIX/WUA feita pelo GF como utilitário** não se transforma automaticamente em autoridade para o mecanismo que atualiza o próprio GF.

## 6. Decisão desta rodada e economia de CI

**Nenhum novo teste Windows foi adicionado.** A existência desses campos já está definida nas especificações oficiais, mas gerar valores sintéticos de `RebootRequired`, `ActivityId`, `ProductCode` ou `MSIHANDLE` apenas confirmaria regras escritas pelo próprio teste, **sem comprovar nenhum comportamento real dos provedores**. Chamar `MsiBeginTransaction` ou iniciar deployment/update no runner pode acessar serviço Windows Installer/alterar o Windows, e **não foi autorizado**. Manter o último teste real e as 22 contenções da CI 41089673054; não gastar execução repetindo o mesmo gate de auditoria de âncoras.

**Próxima hipótese elegível a protótipo:** identificar uma operação **real e inteiramente somente leitura**, cuja autoridade consiga atestar vínculo persistente a uma transação L18 antes/após perda do processo; se não for possível sem privilegiar o host ou executar instalação/atualização, encerrar como `NotProven`. O próprio objeto `ActivityId` MSIX é pista diagnóstica, não resolução automática da lacuna.

**Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8. Zero recuperações e `UnknownBlocked` inalterado.** Sem alterações técnicas, sem WORK, merge, release, deploy, promoção, alteração do GF distribuído, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PCs pessoais.

## 7. Referências oficiais consultadas

- MSI ProductCode: https://learn.microsoft.com/en-us/windows/win32/msi/productcode
- MSI Product Codes / Package Codes: https://learn.microsoft.com/en-us/windows/win32/msi/product-codes
- MSI MsiGetProductInfo: https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msigetproductinfoa
- MSI MsiBeginTransaction: https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msibegintransactiona
- MSI MsiEndTransaction: https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msiendtransaction
- MSI async EXE actions: https://learn.microsoft.com/en-us/windows/win32/msi/custom-action-return-processing-options
- MSIX PackageManager.AddPackageAsync: https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.addpackageasync
- MSIX DeploymentResult: https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.deploymentresult
- WUA IUpdateIdentity: https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iupdateidentity
- WUA IInstallationResult: https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iinstallationresult
- WUA OperationResultCode: https://learn.microsoft.com/en-us/windows/win32/api/wuapi/ne-wuapi-operationresultcode
