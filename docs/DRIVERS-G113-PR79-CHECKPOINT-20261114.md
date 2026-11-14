# GERENCIADOR FINAL — G1.13: identidade da seleção na lista virtualizada

**Data:** 14/11/2026; **PR:** #79 Draft, branch `drivers-pnp-problemas-consulta-20261113`; base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap **27/42**; G1–G5 abertos.

## 1. Base congelada e comprovada
G1.12 SHA `7ba1d923961577d047595909d446fdc3d4c26e75`: [CI Windows 43604691677](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43604691677) e [integridade 43604691690](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43604691690), ambas `completed/SUCCESS`; Windows 2022/2025 com 24/24 G1.9 por runner, WinForms G1.10–G1.12, regressões gerais, visuais, segurança, consentimento, compilação x64 e integridade. PR aberta/Draft, sem merge.

## 2. Contraexemplos adversariais do código
- **F1 — primeira linha igual não é necessariamente a selecionada:** `DarkDriverList.SetItems` restaurava o foco com `_items.FindIndex(item => item == selected)`, isto é, `record.Equals`. Duas entradas estruturalmente iguais (inclusive duas referências à mesma linha) podiam fazer a seleção da segunda migrar silenciosamente para a primeira depois de reordenação/refiltro.
- **F2 — substituição de registro:** um novo `record` equivalente, sem a identidade do objeto originalmente selecionado, podia herdar a seleção. A UI não deve apresentar isso como uma escolha do usuário nem usar nome/INF como chave.
- **F3 — duplicidade adicionada no refiltro:** se o objeto selecionado continuasse visível mas surgisse outra linha com o mesmo `DeviceId`, a seleção podia ser mantida automaticamente, como se a instância tivesse associação unívoca.
- **F4 — acessibilidade insuficiente para nomes idênticos:** a descrição da seleção informava posição, nome e PnP, mas não identificava o `DeviceId` e o INF nem alertava quando linhas visíveis compartilhavam o mesmo identificador.

## 3. Correção mínima no caminho real
`DarkDriverList.SetItems` preserva a seleção **somente** quando o objeto original existe na nova lista por `ReferenceEquals` e o `DeviceId` tem ocorrência única **antes e depois** do refiltro. Em duplicatas, substituição do objeto ou remoção por filtro, limpa a seleção e dispara `SelectionChanged` já existente, permitindo ao painel G1.12 sincronizar `Investigar`. Não cria coordenador/monitoramento, não altera filtros, WMI, WUA, Store ou guard.

A descrição acessível mantém o dispositivo explicitamente identificado por `DeviceId` e INF relatado, sem inferir autenticidade; se houver `DeviceId` duplicado **na lista visível**, avisa que a associação INF/WUA não é inequívoca. Filtro pode ocultar outra linha: isto **não constitui** prova de unicidade global. A barreira independente `DriverReportPublicationGuard.SelectedUnique` permanece no inventário completo, rejeitando seleção ambígua para publicação INF/WUA.

## 4. Testes WinForms
`TestesLayout/DriverListIdentityTests.cs` usa controle e host WinForms reais, `OnKeyDown`, `OnMouseDown` e `SetItems` de produção, somente objetos sintéticos. Cenários: nomes iguais com DeviceIds distintos; inversão de ordem sem trocar referência; clone `record` igual; duplicatas conflitantes/iguais; seleção por mouse e teclado; descarte por refiltro; status acessível e barreira G1.9 sem publicação de INF. Viewports 540×320, 500×210, 1200×720 e 1450×820, sem alegar DPI físico distinto nem testes em hardware pessoal. G1.12 e G1.10/G1.11 preservados.

## 5. Critério GO/NO-GO
**Gate pendente na versão de desenvolvimento**: o SHA com este checkpoint precisa receber **duas** CIs `completed/SUCCESS` no mesmo commit: Windows 2022/2025 (24/24 G1.9 por runner), WinForms G1.10–G1.13, regressões gerais, layout, segurança/privacidade, consentimento, compilação Windows x64 e integridade. Uma CI anterior não prova o novo SHA.

`CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`. `WaitAsync` não encerra `Search` COM síncrono. Testes sintéticos não certificam `DriverVer`, `TargetOSVersion`, ranking, assinatura remota ou pacote compatível. **GO exclusivamente consultivo e condicionado ao gate**; **NO-GO** para manutenção/instalação de drivers.

Preservar PR #79 Draft, roadmap 27/42, G1–G5 abertos. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. Nenhum download, staging, instalação, atualização, desinstalação, rollback, elevação, política ou reinício.

## 6. Próxima etapa G1.14 proposta
Auditar somente vínculo entre a seleção visual duplicada e a abertura do diálogo consultivo: mensagem específica para seleção ambígua, inexistência de prova de INF/WUA, interações de filtro/teclado e cancelamento, sem mudar o guard nem criar ações de manutenção.
