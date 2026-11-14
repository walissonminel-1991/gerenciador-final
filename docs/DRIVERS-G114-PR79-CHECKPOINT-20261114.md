# GERENCIADOR FINAL — Drivers G1.14: abertura do diálogo com identidade ambígua

**Data:** 14/11/2026 · **PR #79 Draft**, branch `drivers-pnp-problemas-consulta-20261113`, base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap 27/42; G1–G5 abertos.

## 1. Gate inicial G1.13
SHA `de0c27619c30f1a90defa1d9ee47019fbaa1cb7f`. [CI Windows 43606075609](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43606075609) e [integridade 43606075600](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43606075600) ambas `completed/SUCCESS`, mesmo SHA; drivers Windows 2022/2025 com 24/24 G1.9 por runner, WinForms G1.10–G1.13, regressões gerais, visuais, política, consentimento, compilação x64 e integridade. PR aberta/Draft, sem merge.

## 2. Contraexemplos reais do código
- **F1 — duplicidade inicial identificada como mudança temporal:** uma seleção de `DeviceId` duplicado faz `DriverReportPublicationGuard.IsCurrent()` retornar `false` já na abertura; o antigo `CheckCurrent` dizia "Inventário ou seleção alterados" e ocultava o relatório, sugerindo mudança posterior inexistente.
- **F2 — consulta WUA disponível sem unicidade global:** com `selected = null` e `DeviceId` repetido na coleção completa, `SelectedUnique()` retorna `true` (não há seleção). O diálogo antes habilitava "Versões oficiais" para uma busca cuja vinculação por instância não é inequívoca. Mesmo com o bloqueio final de `CanPublish`, era um acionamento e uma sinalização inadequados.
- **F3 — duplicata escondida por filtro:** a lista G1.13 pode apresentar uma única associação após refiltro, mas a outra entrada continua no inventário. O diálogo nunca deve usar a cardinalidade filtrada como autoridade para publicar INF/WUA.

## 3. Correção estrita e segura
- **`DriverReadOnlyDialog.cs`:** antes de oferecer botões, computar duplicatas e IDs ausentes sobre `inventory.Items` completo e congelar a validade do guard de abertura. Se a seleção é inicialmente ambígua ou incoerente, mensagens específicas **sem atribuir causa temporal**, com ações WUA/INF desabilitadas. Se não há seleção, ou ela é única mas outro DeviceId é repetido, WUA permanece bloqueado; INF local de seleção única coerente continua consultivo. Mantém `CheckCurrent` irreversível e o texto G1.10 para *invalidação verdadeiramente posterior*.
- **`DriverReadOnlyReport.cs`:** informa textualmente quando o inventário completo possui IDs duplicados ou ausentes, sem promover associação de INF/WUA.
- **`TestesLayout/DriverReadOnlyDialogIdentityTests.cs`:** formulário WinForms modal real, objetos e provedores totalmente sintéticos, guard G1.9 autêntico, testes em 500×340 e 790×510. Casos: duplicata selecionada conflitante, repetição idêntica, seleção filtrada que oculta a outra linha, sem seleção e global duplicado, seleção única com duplicata alheia, abertura legítima e reabertura sem seleção. Os callbacks de produção são invocados para verificar bloqueio, sem WUA ou INF reais.

## 4. Limites, invariantes e gate
**PENDENTE NO SHA DESSE COMMIT:** CI Windows 2022/2025, 24/24 G1.9 por runner, WinForms G1.10–G1.14, regressões gerais, testes visuais, segurança/privacidade, consentimento, compilação x64 e integridade **em duas execuções completed/SUCCESS no mesmo SHA**. Nenhum GO global até a confirmação.

Preservar `CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`, frozen snapshots, guard de unicidade `DeviceId`, coerência Store/INF, cancelamento e retorno tardio, STA única, segurança e privacidade. `WaitAsync` não encerra `Search` COM síncrono. Testes sintéticos não provam pacote remoto compatível, assinatura remota, `TargetOSVersion`, ranking ou versão nova.

NÃO realizar downloads, URLs de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, política, reinício, monitoramento ou timer de produção. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. PR #79 Draft, roadmap 27/42, G1–G5 abertos.

**GO proposto somente para consistência do diálogo consultivo**, condicionado a todas as CIs. **NO-GO** para manutenção/instalação.

## 5. Próxima etapa G1.15 proposta
Auditar a coerência entre inventário parcial, inconsistência de vínculo INF/Store e permissões consultivas exibidas no diálogo, com testes WinForms sintéticos, sem modificar mecanismos de instalação ou permitir URLs de conteúdo.

### Complemento F4 — discrepância entre botão WUA e relatório

Na abertura sem seleção e com `DeviceId` duplicado, a consulta WUA está desabilitada mas a seção **OFERTAS OFICIAIS** ainda dizia apenas "consulta ... ainda não solicitada; exige conexão". A renderização agora explicita que a consulta está **indisponível** enquanto a identidade completa é ambígua e suprime qualquer bloco de vínculos WUA que porventura tenha sido passado ao formatador. O teste WinForms G1.14 exige a mensagem de indisponibilidade, não a anterior. O guard G1.9 não foi alterado, nem foi feita consulta WUA real.
