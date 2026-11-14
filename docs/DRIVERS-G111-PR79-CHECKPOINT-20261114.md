# GERENCIADOR FINAL — Drivers G1.11: integridade visual, legibilidade e acessibilidade

**Data:** 14/11/2026. **PR:** #79 Draft · **branch:** `drivers-pnp-problemas-consulta-20261113` · base `candidata-portatil-pessoal-pc-forte-20261101`.

## 1. Gate inicial G1.10

SHA de partida: `fed91f1bbed4c84f76b7a430f3944e6a30acf0a3`. [Windows 43551817907](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43551817907) e [integridade 43551817935](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43551817935): ambas `completed/SUCCESS`, mesmo SHA, drivers 2022/2025 (24/24 G1.9 por runner), G1.10 WinForms, regressões gerais, testes visuais, política, consentimento, compilação x64 e integridade. PR #79 aberta/Draft e sem merge.

## 2. Contraexemplos no código real

- **F1 — botões incorretos na primeira pintura:** `DriverReadOnlyDialog.ShowDriverReadOnlyReport` criava os dois botões habilitados e só aplicava as condições `inventory.Partial`, `inventory.Items.Count` e `canVerify` no `finally` de `Run`. Com inventário vazio/parcial ou nenhuma seleção, a UI anunciava indevidamente ações disponíveis antes de qualquer consulta.
- **F2 — aviso incompleto para usuários de teclado/leitor de tela:** mensagens longas (cancelamento, timeout, reabertura, bloqueio da STA) eram atualizadas apenas por `status.Text` num `Label` de 27 px com `AutoEllipsis`, sem descrição acessível correspondente nem tooltip de texto completo.
- **F3 — fechamento e foco:** o formulário não configurava `CancelButton` para Esc e forçava o foco inicial na consulta oficial, inclusive quando ela deveria estar desabilitada.
- **F4 — sem seleção:** relatório dizia que a verificação INF "ainda não foi solicitada para este dispositivo", apesar de não existir dispositivo selecionado.

## 3. Correção mínima

No diálogo existente, estabelecer elegibilidade da **primeira pintura**: consulta WUA consultiva somente se inventário não parcial/não vazio; verificação INF somente se seleção tiver pacote Mapped, respeitando o guard G1.9. Sem seleção, a consulta WUA continua geral quando há itens elegíveis, mas o INF fica indisponível. O mesmo check é aplicado aos handlers reais, inclusive quando acionados sinteticamente.

Centralizar mensagens em `SetStatus`: atualiza `Text`, `AccessibleDescription` e tooltip de texto completo. Nomear campos e botões acessíveis, configurar Esc para Fechar por `CancelButton`, iniciar foco na primeira ação habilitada (ou Fechar), manter obsolescência G1.10 irreversível. No relatório, explicitar que a ausência de seleção impede verificar INF local. Não alterar API WUA, Store, snapshots, COM/STA ou políticas.

## 4. Ensaios e limites

`TestesLayout/DriverReadOnlyDialogAccessibilityTests.cs` exercita o formulário WinForms real: seleção válida, nenhuma seleção, inventário completo vazio, parcial com/sem seleção, obsolescência notificada e botões; mede viewport 500×340 e 790×510, limites geométricos, `DeviceDpi`, texto integral por `AccessibleDescription`, nomes acessíveis, tabulação e `CancelButton` (contrato de Esc). Estes testes não equivalem à navegação física por teclado, leitor de tela externo ou homologação em DPI real distinto daquele dos runners; requerem validação de experiência humana em hardware posteriormente. Não executam WUA/INF reais.

A fixture permanece sintética; `WaitAsync` não interrompe `Search` síncrono COM. Não inventar `DriverVer`, `TargetOSVersion`, ranking, pacote remoto verificado, assinatura remota ou compatibilidade. `CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`.

## 5. Gate final e decisão

**PENDENTE NO COMMIT DE DESENVOLVIMENTO:** CI Windows 2022/2025, testes G1.9/G1.10/G1.11, regressões, visuais, segurança, privacidade, consentimento, compilação Windows x64 e integridade no MESMO SHA. Somente `completed/SUCCESS` das duas CIs após este documento autoriza GO restrito a interface consultiva. NO-GO permanente para instalação/manutenção de drivers e qualquer pacote remoto não verificado.

PR #79 permanece Draft; roadmap 27/42 e G1–G5 abertos. Não tocar main, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. Nenhum download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, política ou reinício.

## 6. Continuidade G1.12 sugerida

Auditoria apenas de navegação e consistência do painel principal de drivers frente a filtro/seleção, estado parcial e mensagens de progresso. Não ampliar motores nem habilitar atualização.
