# MISSÃO WORK 6 — fechamento técnico após retomada

**Estado final analisado:** código `1c4c460c7380edfa95318af7c7dec3fa15b84c07`, candidata **v5.13.11.35 TESTE** no PR [#40](https://github.com/walissonminel-1991/gerenciador-final/pull/40), que permanece **Draft**. Esta conclusão técnica revisou a implantação do Work e consolidou a documentação; **não executou instalações nem exclusões reais**. A `main` continua preservada.

## O que efetivamente foi feito

O Work entregou **13 commits** depois do baseline `1bd1ff89`, na ordem solicitada. Alguns testes foram criados primeiro para reproduzir defeitos antes das correções; outros verificam as proteções de forma positiva e adversarial.

| Fase | Falha/riscos que motivaram a alteração | Correção implementada | Evidência no repositório |
| --- | --- | --- | --- |
| A — Atualizações | Receita direta aceita fora das condições de inventário; cancelamento e tentativa dupla; fonte HTTPS parcial podia apagar visualmente candidatos | `VerifiedUpdaters` mais restrito, `UpdateLaunchGate`, `UpdateResultHistory`, `PendingDeployment` com retenção até o término do MSIX, rechecagem de identidade antes de lançar | `Work6AuthorizationTests`, `Work6FlowTests`, `PendingDeploymentTests`, `Work6HistoryTests` |
| B — Desinstalações | Comandos potencialmente perigosos/ambíguos, autorização baseada em estado antigo e conclusão inferida pela ausência transitória do Registro | `UninstallCommand` (gramática segura para MSI e EXE), `UninstallBatch` (snapshot e tentativa única), `RegisteredUninstaller` (imagem PE/assinatura e caminho), confirmação com duas leituras completas, UI de confirmação ampliada | `Work6UninstallTests`, `Work6UninstallFlowTests`, `ConfirmReviewTests` |
| C — Resíduos | Prévia não vinculava toda a cadeia de diretórios; arquivos duplicados/fora de escopo, hardlinks e substituição de diretório | `ResidualPreview`/ `InstallerFileGuard` amarram identidades de arquivos e diretórios, hash do conteúdo, alvos estritos, limite e revalidação; remoção real permanece desligada | `Work6PreviewTests`, suíte existente de resíduos |

A revisão final também protegeu evidência de fontes com consulta parcial, evitou liberação antecipada do arquivo durante operações MSIX e corrigiu o tratamento de informações de versão do código da candidata.

## Verificações CI realmente concluídas

Último código de produção/engenharia: `1c4c460`. Workflows de PR:
- [Validação Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39708331526): **success**.
- [Compilação portátil x64 sem assinatura](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39708331528): **success**.
- [Política de diagnóstico, segurança e privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39708331607): **success**.

Contagens confirmadas nos logs das Actions: **127/127 atualizações**, **139/139 multifontes**, **434/434 resíduos/desinstalação**, **41/41 MSIX**, **30/30 encerramento**, e **22 cenários de interface**. O controle de mutação matou **10/10 defeitos deliberados** em cópias descartáveis: cancelamento pré-lançamento, concorrência, atualização antiga erroneamente autorizada, inventário parcial de desinstalação, ausência transitória de Registro, liberação antecipada de pacote, GUID MSI entre aspas, hardlink e duas falhas de prévia.

**Limites:** runners temporários não representam os dois computadores nem o AVG. Jobs opcionais de fontes externas/downloads foram **skipped**, assim como atestação destinada à `main`. Mutação em fontes descartáveis não é teste de malware, assinatura de binário ou de operações reais. Não há evidência de instalação/desinstalação de aplicativo real nesta missão.

**Artefato CI:** [GerenciadorFinal-v5.13.11.35-bd37d331-TESTE-win-x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39708331528/artifacts/12395853049), publicação temporária pelo GitHub Actions com expiração informada em **29/10/2026**. O pacote não é release estável, não foi validado com AVG no PC e não foi assinado. Verificar `BUILD-INFO.txt` e `SHA256SUMS.txt` depois do download; não misturar pastas de versões.

## Avaliação adicional após o fim do Work

- Conferidos os pontos reais de entrada de atualização e desinstalação em `Program.cs`, bem como `UninstallBatch`, `UninstallCommand`, `RegisteredUninstaller`, `ResidualPreview`, `InstallerFileGuard` e a automação de testes. Proteções novas estão conectadas ao programa, não só a mocks.
- Uma execução única sem travamento no runner **não comprova estabilidade e compatibilidade em ambiente doméstico**, principalmente na atualização/desinstalação de terceiros com UAC, elevadores do fabricante, processos filhos e AVG.
- A validação de um EXE de desinstalação por Authenticode é **conservadora**: aplicativos legítimos com desinstalador não assinado podem perder a ação dentro do GF. Isso é um risco funcional de compatibilidade a medir, **não motivo para reduzir a segurança** sem estudo e testes.
- A prévia de resíduos **nunca habilita exclusão**: `ResidualDecision.CanExecute=false` permanece intencional. Os testes de arquivos sintéticos confirmam revalidação, não a segurança de limpar vestígios reais.
- Não criar botão/opção de modo administrador. O usuário pode iniciar o programa elevado por escolha própria. Manter WinGet COM/CLI real desativado e sem PowerShell/SDK instalados automaticamente.

## Roadmap e próximas decisões

**Permanece 27/42 (64,3%)**. Sem novos checkpoints fechados, pois:
- **U8:** requer instalação individual autorizada, instalador oficial íntegro e prova de versão/identidade final num ambiente adequado.
- **R7:** requer desinstalação individual real autorizada, preservação de dados e verificação de que processos do fornecedor encerraram ou ficaram pendentes.
- **R4:** permanece suspenso; somente evidência específica de exclusividade, consentimento, operação controlada em ambiente descartável e recuperação podem justificar futura reabertura.

Próximos ensaios de maior valor (em ordem): **1.** leitura/execução mínima da .35 no PC FORTE com AVG ativo, verificando interface, catálogo e estabilidade, **sem instalar ou desinstalar**; **2.** escolher pacote descartável ou VM dedicada para U8; **3.** confirmar R7 na mesma VM/ambiente controlado; **4.** revisar R4 somente com fixture sintética e autorização específica, sem uso em dados reais. PC FRACO permanece pendente até estar disponível; não repetir inventário/resíduos nele sem motivo técnico.

Nenhum merge, release, assinatura, elevação automática, instalação de drivers, alteração de redes ou limpeza destrutiva foi executado para concluir esta revisão.
