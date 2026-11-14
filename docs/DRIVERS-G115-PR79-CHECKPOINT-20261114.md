# GERENCIADOR FINAL — Drivers G1.15: elegibilidade consultiva e estados do INF

**Data:** 14/11/2026. **PR:** #79 Draft; branch `drivers-pnp-problemas-consulta-20261113`; base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap 27/42; G1–G5 abertos.

## 1. Gate G1.14 revalidado
SHA `5d5394feeb1b1e8ff6a329030a9bddab38a48eef`; [Windows 43608791465](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43608791465) e [integridade 43608791499](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43608791499) **completed/SUCCESS no mesmo SHA**. Drivers 2022/2025 24/24 verificações G1.9 por runner; WinForms G1.10–G1.14, testes visuais, regressões, mutações, segurança/privacidade, consentimento e compilação x64 aprovados. PR #79 aberta/Draft, sem merge.

## 2. Contraexemplos encontrados no caminho de produção
- **F1 — ação INF indisponível anunciada como não solicitada:** `DriverReadOnlyReport.Format` mostrava `"Ainda não solicitada"` para `NotConsulted`, `Unavailable`, `Inconsistent` ou `PackageEvidence=null`, apesar de `Verificar INF` desabilitado. Sem dados não é prova de ausência.
- **F2 — acessibilidade do botão INF desabilitado:** `DriverReadOnlyDialog` mantinha a descrição genérica `"Verifica somente o INF local"`, mesmo quando a consulta era impossível, sem explicar a razão.
- **F3 — contradição sintética de associação:** `WithoutSignedDriverAssociation=true` com `PackageEvidence.State=Mapped` habilitava `Verificar INF` no diálogo porque só `Mapped` era checado; a própria linha informa inexistência de associação observada com driver assinado. A UI falha segura e não inicia verificação nessas condições, sem alterar o guard G1.9.
- **F4 — WUA indisponível em inventário parcial ou vazio:** o botão estava desabilitado, mas o relatório dizia `"consulta ainda não solicitada; exige conexão"`, sugerindo possibilidade inexistente nesta fotografia.

## 3. Correções mínimas
`DriverReadOnlyReport.InfAvailability` retorna uma justificativa explícita e conservadora para seleção ausente, `Mapped`, `NotConsulted`, `Unavailable`, `Inconsistent`, evidência nula ou associação de driver assinado não observada; não inventa INF ausente/obsoleto. `Format` evita exibir resultado de verificação injetado para associação não elegível e descreve consulta WUA indisponível em fotografias parciais/vazias, sem divulgar vínculos WUA passados nesses estados.

`DriverReadOnlyDialog` alinha a disponibilidade inicial de `Verificar INF` às evidências `Mapped` sem contradição e sincroniza `AccessibleDescription` com a razão do bloqueio; `validAtOpening`, unicidade DeviceId, `CheckCurrent` irreversível e `CanPublishInf` do guard G1.9 permanecem inalterados.

`TestesLayout/DriverReadOnlyDialogEvidenceTests.cs` exercita modal WinForms real com dados Store/publicados sintéticos e provedores WUA/INF inertes: estados de pacote, parcial/vazio, seleção ausente, inconsistências Store/publicados, inventário publicado parcial, status/acessibilidade, callbacks desabilitados, 500×340/790×510. G1.10–G1.14 continuam rodando, incluindo timeout, cancelamento e retornos tardios, sem claims de hardware real.

## 4. Gate, limites e continuidade
**Gate pendente nesta revisão:** Windows 2022/2025 (24/24 G1.9 por runner), WinForms G1.10–G1.15, regressões gerais, visuais, mutações, política, consentimento, compilação Windows x64 e integridade externa **completed/SUCCESS em ambas as CIs no mesmo SHA**. Não declarar GO antes de todas as etapas.

`CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`; sem download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, mudança de política ou reinício. `WaitAsync` não interrompe `Search` síncrono COM. Testes sintéticos não comprovam pacote compatível, versão superior, assinatura remota, leitor de tela físico nem DPI real distinto.

Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. Preservar PR #79 Draft, roadmap 27/42, G1–G5 abertos.

**GO proposto somente para interface consultiva, condicionado ao gate; NO-GO para manutenção.**

**Próxima G1.16:** auditar apenas semântica de histórico da investigação, evidências de publicação parcial e relatórios após cancelamento/retorno tardio, sem ampliar motores nem autorizar ações de drivers.
