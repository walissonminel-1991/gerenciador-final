# GERENCIADOR FINAL — DRIVERS G1.20: validade temporal da persistência do histórico v1

**Data:** 14/11/2026 · PR #79 Draft · branch `drivers-pnp-problemas-consulta-20261113` · base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap 27/42; G1–G5 abertos.

## Entrada G1.19 validada independentemente
SHA `a322bbf94bf97c805d9c33f8c69b2c7fe202c279` · CIs Windows [43643900594](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43643900594) e integridade [43643900581](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43643900581): `completed/SUCCESS` no mesmo SHA, tentativa 1. Windows 2022/2025, 24/24 G1.9 por runner, WinForms G1.10–G1.19, 36/36 mutações negativas, visuais, regressões gerais, segurança/privacidade, consentimento, compilação Windows x64 e integridade aprovados. PR aberta/Draft/sem merge.

## Falhas reproduzíveis do caminho G1.19
- **F1 — consulta antiga pode vencer por hora tardia do gravador:** `RunRefresh` usava `DateTimeOffset.UtcNow` dentro de `Task.Run` da persistência. Uma consulta A iniciada antes de B, mas cujo gravador rodou depois, poderia ganhar timestamp artificialmente posterior e substituir B no histórico v1. Menor correção: capturar horário na **abertura da consulta**, antes de WMI, e persistir essa marca, conservando proteção `at <= previous.At` do v1.
- **F2 — descarte antes da gravação:** a UI verificava `IsDisposed` somente depois de `Record`; ao descartar página, `driverQuery.RequestCancel` não era passado ao gravador. Agora `RunRefresh` exige token/generation válidos antes de agendar a gravação, e `Record` recusa tokens invalidados no início, depois da fotografia e **antes de File.Move**.
- **F3 — descarte durante escrita de temporário:** o gravador antigo não revia cancelamento após flush; o novo contrato de `Record` recusa publicar o temporário e o remove na limpeza. A fixture possui interleaving determinístico exclusivamente em teste, não cron/timer/serviço.
- **F4 — snapshot mutável após publicação:** os dados simples de histórico são congelados por cópias `with` antes de qualquer `await` na fase de gravação, sem alterar os campos de autorização/identidade do inventário.
- **F5 — coerência acessível:** o rótulo comparativo somente é aplicado quando a geração e token continuam válidos; os tooltips e a `AccessibleDescription` acompanham o resultado desta fotografia. O fluxo anterior de não gravar fixtures sem caminho isolado continua intacto.

## Fixtures adversariais
`TestesLayout/DriverG120HistoryLifetimeTests.cs`: WinForms real com provedores **exclusivamente sintéticos**, duas instâncias de `MainForm(startServices: false)`, duas páginas, inversão da ordem de conclusão, descarte entre publicação/escrita, descarte durante temporário, cancelamento, timeout, retorno tardio, rótulo/tooltip/descrição acessível, filtro, teclado e viewports 500×340, 790×510, 1200×720 e 1450×820. Um caminho de teste injetável só é aceito junto de `readForTest`, e a fixture usa exclusivamente pasta temporária aleatória descartável. **Nunca** chama `DriverHistory.DefaultPath` nos testes, WMI/WUA/SetupAPI nativos, INF, download ou manutenção. Regressões G1.10–G1.19 seguem em execução pelo mesmo gate. Não alegar DPI físico ou leitor de tela externo.

## Invariantes e limites
Preservados schema histórico v1, hash SHA-256, lock interinstâncias, escrita `File.Move` atômica, 2 MB, retenção de 20, monotonicidade de timestamp, DeviceId global inequívoco, snapshots e guards. `CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`. `WaitAsync` cancela a espera mas não termina COM `Search` síncrono. Ainda não é uma transação entre leitura WMI e sistema de arquivos; validade de gravação é avaliada até a troca final, não equivale a homologação física de interrupções abruptas do SO.

**Proibido:** alterações no `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO; nenhuma instalação, atualização, remoção, rollback, elevação, mudança de política, reinício, URL de conteúdo, staging, serviço, timer ou polling.

## Falha registrada da primeira CI
- Candidato `2b30bc560b3d83b2120d702e2aa381cd05cce1fc`: CI Windows [43645622662](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43645622662) falhou no teste G1.20 `consulta obsoleta exibiu comparacao como atual`; verificação do arquivo v1 e mensagem visual já tinham passado. O teste acessível observou a descrição antes de validar explicitamente sua sincronização final. Corrigida a fixture para aguardar ambas as propriedades da MESMA fotografia e falhar se continuarem divergentes; não alterar lógica de produção por uma hipótese de agendamento. Falhas posteriores de artefatos WORK14/16/17/18 vieram da interrupção antecipada.
- O gate da primeira tentativa não é GO, ainda que uma CI individual tenha passado. Revalidar ambos os workflows no novo SHA, sem omitir falhas ou cancelamentos.

## Gate G1.20
**PENDENTE:** exigir duas CIs `completed/SUCCESS` no mesmo SHA final: Windows 2022/2025 24/24 G1.9 por runner, WinForms G1.10–G1.20, regressões gerais, testes visuais, controles negativos de mutação, segurança/privacidade, consentimento, compilação Windows x64 e integridade externa. Documentar falhas e reexecuções; nenhuma alegação de GO antes das duas conclusões.

**Próxima G1.21 proposta:** auditar integridade e limites da fotografia congelada sob mutações in-place e reentrâncias durante os eventos WinForms sem alterar o contrato de manutenção.
