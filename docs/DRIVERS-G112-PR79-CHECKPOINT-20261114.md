# GERENCIADOR FINAL — Drivers G1.12 · painel principal consultivo

**Data:** 14/11/2026. **PR:** #79 Draft, branch `drivers-pnp-problemas-consulta-20261113`, base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap 27/42; G1–G5 abertos.

## 1. G1.11 gate inicial (revalidado)
SHA `b30e8d62599e1441b2a6511728d6d5df248bedc8`; [Windows 43597151307](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43597151307) e [integridade 43597151303](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43597151303) ambas `completed/SUCCESS`. Drivers windows-2022/2025 com 24/24 G1.9 por runner, WinForms G1.10/G1.11, regressões gerais, visuais, segurança/privacidade, consentimento, compilação x64 e integridade. PR aberta, Draft, sem merge.

## 2. Contraexemplos identificados no painel real
- **F1:** `DriverInventoryStatus.Text` mudava durante espera, cancelamento, timeout, erro e retorno tardio, mas `AccessibleDescription` e tooltip só eram renovados em resize ou publicação completa. Com rodapé `AutoEllipsis` de uma linha, texto crítico ficava truncado e inacessível.
- **F2:** o botão `Consultar drivers` mudava o texto para `Cancelar` / `Cancelando...`, sem `AccessibleName` / `AccessibleDescription` coerentes. Após publicar o inventário, enquanto `DriverHistory.Record` seguia, continuava oferecendo `Cancelar`, mas esse clique apenas dizia "Finalizando histórico" e não cancelava mais leitura.
- **F3:** a lista WinForms virtualizada apresentava seleção desenhada por índice, mas a descrição acessível não acompanhava `SelectIndex`, filtragem, refiltro, dispositivos ausentes ou ausência de seleção. As cores de filtros ativos também não tinham indicação acessível de estado; o tooltip de uma linha podia permanecer associado a item anterior após rolagem.
- **F4:** os testes antigos não exercitavam o fluxo real do painel principal com inventários sintéticos e retorno tardio; existia cobertura do diálogo de investigação, não da página que abre o diálogo.

## 3. Correção mínima
- `DriversPlaceholder.cs`: centraliza mensagens em `SetDriverStatus`, com texto integral no `AccessibleDescription` e tooltip em cada transição; estados dinâmicos do botão de consulta em `SetRefreshCaption`. Após publicação, o botão desabilita e passa a "Finalizando..." enquanto o histórico termina; apenas na conclusão volta para "Consultar drivers".
- `DarkDriverList.cs`: atualiza `AccessibleDescription` para seleção por teclado/mouse e re-filtros, preservando `SelectionChanged`; limpa tooltip de item ao rolar. Filtros marcados comunicam seu estado.
- Teste em `TestesLayout/DriverMainPanelTemporalTests.cs`: formulário e painel WinForms reais, dados `DriverInventoryResult` artificiais, leitura injetada apenas nos testes, prazo curto injetado e handler de produção acionado pela mesma rotina. **Não consulta WMI, WUA, SetupAPI nem grava o histórico do usuário durante fixture.** Verifica filtros, teclas, viewport 540×320, 500×210, 1200×720 e 1450×820, estados vazio/parcial, cancelamento, bloqueio de duplicata, timeout, fechamento e retorno tardio.
- A produção continua usando `DriverStoreAudit.CaptureWithPackages` e `DriverHistory.Record`; não foi criado coordenador, timer de produção ou motor paralelo.

## 4. Limites e gate
Dados de teste não comprovam pacote remoto compatível, versão/ranking, INF assinado remotamente ou homologação real de DPI/leitor de tela. `WaitAsync` cancela a espera, **não interrompe** leitura WMI nem `Search` COM síncrono.

**Gate pendente:** novo SHA incluindo este documento deve passar CI Windows 2022/2025, 24/24 verificações G1.9 por runner, WinForms G1.10/G1.11/G1.12, regressões gerais, layout, segurança/privacidade, consentimento, compilação x64 e integridade, ambas execuções `completed/SUCCESS` no MESMO SHA. Correções de regressão comprovada somente, sem afrouxar gates.

`CanInstall=false`; `CanMaintain=false`; `IsNewerProven=false`; `CompletePackageVerified=false`. **GO restrito ao diagnóstico read-only somente após gate; NO-GO para toda manutenção.** PR #79 Draft, roadmap 27/42, G1–G5 abertos. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. Nenhum download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, mudança de política ou reinício.

## 5. Próxima etapa G1.13
Auditar somente semântica de acessibilidade e correspondência da lista virtualizada no painel em cenários de seleção ambígua, dispositivos repetidos e leitores de tela sem depender de heurísticas de versão. Validar por testes reais; nenhuma autorização de driver.

### Regressão intermediária de fixture e correção

A [CI 43599548422](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43599548422) falhou na asserção do filtro PnP, sem falha de compilação. A [CI diagnóstica 43599704300](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43599704300) confirmou que o acionamento via reflexão de `Control.OnClick` não disparou o evento real de `Button` no runner: itens=3, seleção=PCI\\G112\\A preservada, descrição ainda "Ativar filtro: Problemas". Assim, não havia prova de erro no mecanismo de filtragem. Foi substituído o acionamento programático por um callback exclusivamente de fixture para a MESMA rotina local `ApplyFilter` utilizada pelos handlers reais de botão e ComboBox, como já feito com o botão de consulta G1.10. Nenhuma mudança no predicado `DriverListFilters.Apply`; exigir novo gate global no SHA final.
