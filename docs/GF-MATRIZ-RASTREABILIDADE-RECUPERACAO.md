# GF — Matriz vigente de rastreabilidade e recuperação

Snapshot auditado: PR #113 `2407d42dbdba711932bf2bad8d63d292a5aa1695`. As evidências abaixo pertencem a esse SHA ou, quando indicado, a um SHA anterior. O presente documento não declara o produto aprovado. Fonte de autoridade: [GF-AUTORIDADE-PRODUTO-V269.md](GF-AUTORIDADE-PRODUTO-V269.md).

## 1. Proveniência dos componentes (comparação por blob Git, não só nome)
| PR | Origem congelada | Relação com #113 | Arquivos examinados, iguais / diferentes |
|---|---|---|---|
| #103 | 5d5a3ac9dfbef8534bbaf5ad6b079181c01dbe01 | diverged 299 à frente / 26 atrás | 1 / 1 |
| #105 | 912b8b3996b3250a2139803b19014c00a65ccfd3 | diverged 271 / 34 | 4 / 1 |
| #106 | 75eb27f431706133a714baeadcd4f823ad332511 | diverged 271 / 107 | 6 / 7 |
| #107 | 3aa6bb466e3901faeb8ae1ea9ceeb01e6f627310 | diverged 271 / 28 | 2 / 2 |
| #108 | 6055baff1021b4de5363d09ad1bbcbcf0da1219f | diverged 299 / 63 | 2 / 0 |
| #109 | 825f24a53808204642ed11971d66f2c9e30c3259 | ahead 268 / 0 | HTML, contrato, matriz originais idênticos |
| #111 | d96f3f93c16b0b9af8c2b90704eb0181c57b63ba | ahead 255 / 0 | ancestralidade, não aprovação de UI |
| #114 | 0f10023975cb966fb5d7761828fa6b320eccfd30 | diverged 238 / 35 | 6 / 7 |

39 arquivos de motores/Monitor examinados (21 iguais, 18 diferentes, nenhum ausente nesse conjunto); mais três referências #109 iguais. Arquivos compartilhados Program.cs, DarkControls.cs, DarkConfirmDialog.cs, DriverUpdatePilotDialog.cs e testes não foram incluídos nesse total. Divergência de blob não prova perda nem regressão: revisão por método/contrato pendente. Não reaplicar PR inteira.

| Origem | Iguais | Diferentes (revisar contratos/métodos) |
|---|---|---|
| #103 | MsixUninstall.cs | LocalMsixInventory.cs |
| #105 | DriverG32RecoveryGate.cs; DriverSilentUninstall.cs; DriverUpdatePilot.cs; DriverUpdatePilotFileJournal.cs | DriverRecoveryVault.cs |
| #106 | LiveHardwareMonitoring.cs; WindowsWddmGpuProvider.cs; HardwareMonitoringDiagnosticReport.cs; AcpiThermalZoneProvider.cs; StorageAssociationLinker.cs; StorageReliabilityCounterMapper.cs | HardwareSensorPortfolio.cs; D3dKmtGpuSensorProvider.cs; DxgiGpuIdentityProvider.cs; NvidiaNvmlSensorProvider.cs; WindowsPlatformHardwareProvider.cs; NativeHardwareDiscovery.cs; WindowsStorageSensorProvider.cs |
| #107 | NetworkDiagnostics.cs; NetworkRepairOutcome.cs | WindowsIntegrityAssessment.cs; SafetyDiagnosticsUi.cs |
| #108 | BundlewareReview.cs; UninstallBatch.cs | — |
| #114 | UiV269MonitorNativePresentation.cs; UiV269MonitorSettingsPanel.cs; UiV269MonitorSnapshotRelay.cs; UiV269MonitorWindowBinding.cs; UiV269ResourceMonitorPreferences.cs; UiV269ThermalPresentation.cs | UiV269MonitorHost.cs; UiV269MonitorMetricTimes.cs; UiV269ResourceMonitor.cs; UiV269ResourceMonitorAdapter.cs; UiV269ThermalEvidence.cs; UiV269DynamicMeter.cs; UiV269UniversalMeter.cs |

## 2. Caminho de produção e aceite

Legenda: C = conexão constatada no código ativo; S = evidência sintética em SHA identificado; P = homologação final pendente; B = divergência comprovada ou gate falho. Não atribuir S a código não executado nessa revisão.

| ID / superfície | Caminho ativo verificado | Evidência e pendência |
|---|---|---|
| AUT-SHELL-01 | Program.Main → MainForm → _nav (V269Shell), SetPageContent (Program.cs 859–864 e 1648–1650) → oito views; NavigationChanged preserva detalhes e operações | C; S anterior fd37b7f1, Windows 45694478766: 57 cliques/3 temas, seleção única, 16 passos de sessão; P captura em movimento, PC FORTE/FRACO |
| AUT-SHELL-02 | V269RightPane.ConfirmAsync/PresentPanel; Program.cs 865–871 registra históricos de app, limpeza, reparo, módulos | C; B três rotas modais reais descritas abaixo; P substituir sem descartar resultados, testar retorno |
| AUT-APP-01 / inventário | BuildProgramsPanel → Verificar/CheckUpdatesFromButtonAsync, pesquisa/RenderPrograms, V269AppSort, DarkProgramList.ApplyV269Table, ItemActionClicked (Program.cs 1216–1336) | C; S fd37 pesquisa/scroll; P paridade catálogo/ícones, estado binário e fluxo real com oferta |
| AUT-APP-01 / MSIX | ItemActionClicked(msix-uninstall) → UninstallMsixAsync → MsixUninstall.CanStart/ExecuteAsync/DescribeForUi (Program.cs 2876–2911) | C; MsixUninstall idêntico #103; LocalMsixInventory diferente; P revisão sem operação física, elegibilidade/aceite/resultado |
| AUT-APP-01 / bundleware | ScanAllInstalledAppsForSafety, BundlewareReview.CanLaunchIndividual, UninstallEntriesAsync → Select/RelationsStillSafe → RegisteredUninstaller.Prepare → ObserveAfterUninstall → SafeResidualCleanupCandidates (Program.cs 3434–3553) | C; dois motores idênticos; P teste end-to-end de seleção + revisão posterior, sem remover reais |
| AUT-DRV-01 | BuildDriversPlaceholder → UseV269Table → Verificar PnP/WUA, PresentDetails, ConfirmV269Async, DriverReview, cofre OEM/DriverUpdatePilotDialog | C; S CI 45698586891 Drivers 2022/2025 passou; P ofertas elegíveis, cofre/restore físico autorizado; Display NO-GO |
| AUT-HOME-01 | BuildV269Home → RefreshV269HardwareAsync → HardwareSensorPortfolioReader.CaptureOnDemand; StartV269LiveSampling, V269HomeModels, V269GpuModel | C; S fd37 hardware + sessão; 7 arquivos #106 divergentes; P identificar sensores por hardware, sem valores fictícios e sem scroll ordinário |
| AUT-CLEAN-01 | AddToolsPages → ScrollablePage, CreateV269CleanupTools e BuildDiskPanel → SafetyDiagnosticsUi.ShowCleanupPreviewAsync/CleanTemporaryCategoryAsync/EmptyRecycleBinAsync | C; S anterior resíduos e limpeza; P prévia x execução, barras, categoria, confirmação, cancelamento e resultado |
| AUT-REPAIR-01 | CreateV269RepairPage → SafetyDiagnosticsUi.RunNetworkDiagnosticAsync/RepairNetworkAfterDiagnosisAsync e diagnósticos por alvo; Program.cs WINREPAIR / NetworkRepairOutcome.FromWire | C; NetworkDiagnostics e Outcome idênticos; P revisar diferenças WindowsIntegrityAssessment/SafetyDiagnosticsUi, comprovar pós-verificação; não executar reparos reais |
| AUT-SYS-01 | BuildIntegratedSystemPanel → Verificar Windows Update, módulos energia e RAM; MemoryRamDashboard.RefreshV269RamMeter | C; barra RAM universal presente; P verificar consulta manual, planos, RAM sem liberar operação não autorizada; layout e estados reais |
| AUT-SYS-01 / Inicialização | BuildV269StartupPage → NavigationChanged.RefreshStartupEntriesAsync; IntegratedSystemUi.SelectV269StartupCard/PresentV269StartupDetails e geração de view | C; S fd37 consulta assíncrona sintética + troca de aba; P itens protegidos, persistência autorizada |
| AUT-CONF-01 | BuildV269Settings → BindV269Monitor(view) quando produção, General.History/Diagnostic e preferências; V269SettingsPage | C; P aparência, opt-in, exportação/privacidade, configuração persistente, retorno ao Geral |
| AUT-MON-01/02 | Program.Main retorna GfMonitorStandalone.Run em argumento próprio antes do trace; UiV269MonitorIntegration.Bind/Attach, PublishV269Monitor → BeginGeneration/Publish; UiV269MonitorSession lease e produtor | C; S 45697735231 logs host/relay/desktop, três temas e pixels; P diferenças dos 7 arquivos, quatro combinações startup/fixar/sobreposição, Win+D/Explorer e fullscreen físico, um produtor |
| AUT-COLOR-01/METER-01 | V269VisualTokens, GfMeterScale, GfDynamicProgressBar, UiV269ResourceMonitor, V269AppTable/DetailStyle | B token Dark anterior + texto/preenchimento compartilhados; candidata e4352db1 em HOLD, não homologada; conflito contraste de seleção HTML explícito; P pixels/contraste temas |
| AUT-OP-01 | GfUniversalMeter.Update/Uninstall/Operation; V269AppFlowMeter.SetProgress; V269RightPane.UpdateLegacyMeter; Program.RefreshLegacyOperationMeter e DrawOperationMeter | C; B CI 45698586891 etapa 19: teste espera azul fixo aos 37%, contrário à escala inversa. Recuperação corrige oráculo e acrescenta três temas, 10/35/75/100%, troca de aba. P executar novo SHA |
| AUT-SCROLL-01 | UiV269ScrollHosts.V269TextScrollHost/V269ReviewScrollHost e DarkVScrollBar, DarkScrollableFlow, ScrollablePage | C; S Lote4 negativo compila e detecta sobreposição no 2407d42d; P suite completa, scroll apenas por overflow, teclado/foco; modal ainda é divergência AUT-SHELL-02 |
| AUT-UX-01 | V269Shell.SelectPage/RefreshVisiblePage, ModernButton, DarkProgramList, MainForm.RefreshV269HardwareAsync | S fd37: despacho p50 19,38ms / P95 251,08 / max 575,86; STA p50 19,30 / P95 28,68 / max 29,30; sessão p50 73,92 / max 879,94 (viewport compacto); P isolar por transição, medir renderização e PCs |
| AUT-SAFE-01 / diagnóstico | Program.Main trace/recovery; LocalDiagnosticReport, DiagnosticRecoveryJournal, exportação em Configurações; bloqueios _busy/_v269Confirming/_betaClose; worker separado | C; política/consentimento passaram 45698586891; P comprovar exportação multibloco, recuperação, privacidade e desempenho no SHA final; não alterar motores |

## 3. Inventário de caminhos anteriores — não apagar por nome
| Achado | Classificação e ação |
|---|---|
| TestesLayout/V269ShellTests.cs 167 exige Blue aos 37% | Oráculo obsoleto comprovado; corrigido por AUT-OP-01, cobertura ampliada. Não desabilitar teste. |
| TestesLayout/V269ComponentTests.cs protege Dark #1c252d/#202a33 | Oráculo obsoleto comprovado por cascata final #109; correção na candidata cromática em HOLD, teste deve proteger tokens finais. |
| ScanHiddenWindowsProgramsForReview no Program.cs; BuildProgramsPanel não contém botão Ocultos | Motor consultivo presente; botão antigo não está no caminho aprovado. Preservar scanner de segurança; não reintroduzir UI extra. Se consulta pública voltar a ser requerida, resolver conflito explicitamente. |
| UiV269MainForm.cs 44–67 detalhes de hardware → CreateV269HardwareDetailsDialog/ShowDialog se resultado/histórico ocupado | Rota de produção alcançável, não simples arquivo morto. Substituir por navegação/retorno no painel preservando resultado; hoje não remover porque faria perder acesso aos detalhes ou histórico. |
| UiV269MainForm.cs 937–950 General.History → ShowLocalReliabilityReport se resultado/histórico ocupado | Modal legado ainda ativo; teste Lote4 protege integridade da visualização, não significa que modal seja desejado. Migrar primeiro contrato de preservação do resultado e leitura/cópia do relatório; manter controles negativos que detectam o defeito real. |
| UiV269MainForm.cs 1079–1098 PresentV269SettingsInformation → Form/ShowDialog se resultado/histórico ocupado | Modal legado alcançável; consolidar com mesma retenção, não apagar fallback antes da substituição segura. |
| Program.BuildOperationsPanel + PresentLegacyOperationMeter / V269RightPane.UpdateLegacyMeter | Apesar do nome, ainda pertencem ao fluxo real e usam a escala universal. Não excluir sem comprovar exclusividade/retirar consumidores. Testar uma única barra por estado e ausência de timer oculto. |
| DarkTabHost | Não é _nav (produção usa V269Shell); ocorrência confirmada em VerifyIndependentFeedback. Outras referências ainda precisam de inventário completo; não declarar código morto. |
| ToolTipIcon.Info no aviso do Windows | Não confundir ícone de notificação do Windows com componente ToolTip hover. Auditar árvore de controles e eventos reais, não só grep. |

## 4. Gates e execução
1. Recuperar CI falha sem mascaramento: Lote4 e Drivers passaram; oráculo azul fixo comprovadamente obsoleto; publicar exclusivamente a correção desse teste e este registro sob CAS, preservando a candidata cromática.
2. Após SUCCESS integral do SHA recuperado, reconciliar candidata e4352db1 (18 arquivos antes desta recuperação) sem sobrescrever novas correções. Homologar RGB, contraste, rolagem, navegação. Não importar novamente PRs originais.
3. Revisar os 18 arquivos divergentes desta amostra por função e continuar inventário de todos os caminhos de produção; criar evidência antes de remoção. Resolver rotas modais substituídas em incrementos únicos e verificáveis.
4. Executar sequência única do EXE nativo: Início → Aplicativos (consulta/pesquisa/scroll/detalhes) → Drivers (consulta, revisão sem instalar) → Limpeza (prévia/cancelar) → Reparo (consulta) → Sistema (busca manual) → Inicialização (somente leitura) → Configurações (temas, escala e Monitor). Durante operações sintéticas, alternar abas, cancelar, tema, foco/teclado, fechar/reabrir e validar resultados. Não executar manutenção real sem autorização específica.
5. Verificar aparência em três temas, pixels reais de cores e estados, rolagem e DPI; medir navegação, latência, uso CPU/RAM, GDI/USER e ausência de reconstrução indevida. Comparar com referência congelada, não com uma captura anterior reprovada.
6. WORK homologar fisicamente no PC FORTE 2560×1440 e PC FRACO 1440×900. Não contar estado consultivo/sintético como operação real comprovada. Nenhum percentual de conclusão global nesta amostra.
