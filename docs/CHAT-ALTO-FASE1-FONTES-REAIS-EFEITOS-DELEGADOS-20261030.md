# GERENCIADOR FINAL — Fase 1 — Superfícies reais de delegação e limite de quiescência

**30/10/2026 · análise fundamentada em código e gate executável no Windows descartável; não homologação do GF operacional.**

## 1. Estado verificável anterior à alteração

- Repositório `walissonminel-1991/gerenciador-final`, única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial `c2d89d38857d63050f8a4266798c4601dc176295`.
- Último commit de testes aprovado previamente: `c38db307dae6973b46aa44e339c335549e54fdf3`. CI Windows [41078357355](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41078357355) **SUCCESS 21/21 PASS_CONTAINMENT**, job `124140520370`. Zero recuperações.
- `same-user-full-state-rollback-after-new` e `out-of-band-new-with-old-selected` continuam no contrato de recusa `UnknownBlocked`.
- PR #65 foi lida e continua `open/Draft`, sem merge.
- Nenhum código da aplicação, `Phase1Recovery.cs.txt`, TUF, NTFS, B+A, ou instalação do GF foi modificado nesta rodada.

## 2. Constatações no código real — não confundir com efeitos observados

**Estas são superfícies concretas do código-fonte, não casos de operação real de manutenção executados nesta CI**.

| Classe | Evidência concreta no código da branch | Consequência para H3 |
| --- | --- | --- |
| Subprocessos nativos | `Program.cs`: `AppServices.RunCommandAsync`, `Process.Start`, `WaitForExitAsync`, e `WorkerHost` chamando DISM/SFC/netsh/defrag/net.exe/ipconfig/powercfg | O processo chamado é acompanhado; saída não é prova global de que o SO concluiu mudanças posteriores ou de que não existem efeitos delegados |
| Auxiliar elevado do próprio GF | `Program.cs`: `RunElevatedAsync`, `UseShellExecute=true`, `Verb="runas"`, `--worker`, pipe nomeado e `WorkerHost` | A elevação pode mudar identidade/token e interromper a inferência de que todos os trabalhos pertencem ao Job do lançador; o canal de controle não prova efeitos de terceiros drenados |
| Desinstalador de fornecedor | `RegisteredUninstaller.cs`: `PreparedUninstaller.LaunchAsync`, `Process.Start`, `UseShellExecute=true` e acompanhamento limitado a `WaitForExitAsync` | O instalador externo/serviço/custom actions pode ter trabalho não representado pela saída do processo cliente |
| Instalador MSI/EXE oficial | `VerifiedUpdaters.cs`: `Process.Start` com `msiexec.exe` ou EXE verificado, `UseShellExecute=true` e `runas`; `WaitForInstallerExitAsync` com prazo | MSI pode transferir execução para Windows Installer Service, e ações customizadas podem ser assíncronas; `ExitCode` não é testemunha de quiescência universal |
| Implantação MSIX/Windows Terminal | `WindowsTerminalUpdater.cs`: `PackageManager.AddPackageAsync`, callback `Completed` e `PendingDeployment` | A API gerencia a operação de implantação do pacote; seu resultado não atesta que *todos os efeitos do sistema causados por NEW* foram delimitados para rollback do próprio GF |
| Windows Update Agent | `Program.cs`: `WorkerHost` chama `WindowsUpdateClient.InstallAsync` por identidade validada | A operação é entregue ao Windows Update; a quiescência exigiria contrato específico com WUA/serviços, pendência de reinicialização e efeitos de instalação |
| Restauração do Sistema | `RestorePointService.cs`: chamada a `srclient.dll / SRSetRestorePointW` | O próprio serviço/SO controla o resultado; um retorno não valida a delimitação dos demais efeitos em andamento |
| COM WinGet consultivo | `WinGetComReader.cs`: `Microsoft.Management.Deployment.PackageManager` apenas catálogo instalado/local | Classificar apenas como escopo consultivo neste código; **não** confundir a ativação COM com execução de updates |
| Telemetria HTTPS (fora do autoatualizador) | `BetaTelemetryUploader.cs`: `HttpClientHandler` e `HttpClient`; endpoint de BETA é desativado no runner `GF_BETA_ENDPOINT=''` | Efeito de comunicação externa potencial, mas **fora do contrato desta CI** e sem autorização para ligar rede/telemetria |

**Cuidado metodológico:** os sete caminhos marcados `UNBOUNDED` são riscos de delimitação de efeitos sob o contrato de recuperação; não se afirma que tenham produzido efeitos externos nesta CI. O relatório é um recorte auditado, **não** varredura completa de todo C#/PInvoke/COM/reflection, nem prova de que cada execução sempre escapa ao Job.

## 3. Barreira de regressão nova (sem funções privilegiadas)

Commit experimental técnico `ce9567ad40a30716398a26f8fc681db647cec7a5` acrescentou somente:

- `prototipos/autoatualizador-seguro/phase1-composed/delegated_effect_surface_gate.py`: programa de leitura dos arquivos-fonte reais, só executado com `GITHUB_ACTIONS=true` e `os.name==nt`. Confere âncoras de nove classes, sete como `UNBOUNDED`, duas outras com escopos explicitamente diferentes.
- Uma etapa adicional em `.github/workflows/gf-autoatualizador-fase1-real-lab.yml`, antes do build real, verifica o JSON `GF_PHASE1_DELEGATED_EFFECT_SURFACE_REPORT`. Exige `fail_closed_barrier_present=true` e `quiescence_proven=false`, `operational_ready=false`, `complete_call_graph_audited=false`, sem realizar operações nativas.
- A barreira `Require(false,"REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY")` permanece presente no mesmo `ReconcileReal`.
- **Finalidade do gate:** se algum caminho examinado ou a recusa forem removidos/mudados, o workflow para até que o contrato seja revisado. Isso NÃO impede que um caminho novo não contemplado exista; por isso, nenhum `PASS` do gate autoriza recuperação.

A CI disparada para validar esse gate é [41082816324](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41082816324), inicialmente **queued/in progress**. Atualizar o resultado neste checkpoint apenas depois de examinar job, relatório da nova etapa e a matriz 21/21 no log Windows.

## 4. O que o runner poderá realmente comprovar

- Que os *nove grupos de âncoras* ainda existem nos arquivos-fonte e que a barreira conservadora não foi removida.
- Que o fluxo de laboratório anterior foi reexecutado sem regressão, se houver `21/21 PASS_CONTAINMENT`.
- Não executa `msiexec`, `DISM`, `SFC`, `netsh`, instalação de updates, MSIX ou restauração; não mede seus efeitos. **Sem teste real de quiescência, nenhuma rota foi liberada.**
- Os cenários anteriores `external-broker` e `external-broker-handoff` seguem como contraexemplos controlados de processos fora do Job, não como reprodução de WUA/MSI reais.

## 5. Contrato de quiescência específico por classe — pesquisas posteriores

**Estado padrão: `UNKNOWN/UNBOUNDED`.** Uma prova futura precisa identificar com precisão os executores externos, fonte de autoridade, transação, resultado do provider, handles/capabilities duráveis, reinício pendente, I/O tardio e comportamento após perda do supervisor. Não usar PID, HWND, recibo local, contagem do Job ou código zero como substituto.

Para MSI, não chamar o retorno de `msiexec` de conclusão universal de custom actions. Para MSIX, `Completed` é conclusão da operação de deployment que a API descreve; não abrange arbitrariamente todo outro serviço/efeito possível. Para Windows Update, observar resultado de instalação e eventual reboot é uma questão específica do WUA, não da integridade do seletor L18.

**Ações negativas mais promissoras em laboratório, sem alteração de sistema:** (a) testar um imitador MSI com client terminando enquanto broker fora do Job continua escribvendo uma fixture inerte; (b) verificar controle de tratamento de timeout e estado `UnknownBlocked`; (c) executar revisão independente das classes de efeito por trás de cada API antes de ligar qualquer operação real. Os casos (a)/(b) ainda não foram adicionados — os testes existentes já fazem uma simulação genérica do handoff. Não duplicá-los sem nova evidência.

## 6. Decisão

**Preservar `UnknownBlocked` e zero recuperações automáticas.** O ganho executado é uma *detecção regressiva negativa* das rotas de delegação evidenciadas no código-fonte; não prova quiescência. Exigir autorização explícita antes de avaliar qualquer chamada que instale/desinstale/repare componentes reais do Windows, mesmo em CI descartável.

Nenhuma alteração do produto, main, PR #65 Draft, BETA .66, Cloudflare/D1, credenciais, telemetria, usuários ou PCs pessoais. Sem WORK, merge, release, deploy. Fase 1 **ABERTA**, READY=false, G1–G8 **BLOCKED 0/8**.

## Referências Windows oficiais

- [Job Objects e exceções de propagação para processos](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).
- [Windows Installer: Sequencing Custom Actions](https://learn.microsoft.com/en-us/windows/win32/msi/sequencing-custom-actions).
- [Windows Installer: Synchronous and Asynchronous Custom Actions](https://learn.microsoft.com/en-us/windows/win32/msi/synchronous-and-asynchronous-custom-actions).
- [PackageManager.AddPackageAsync](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.addpackageasync).
- [Windows Update Agent — Interfaces](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/interfaces).
