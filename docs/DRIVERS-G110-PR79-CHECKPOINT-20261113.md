# GERENCIADOR FINAL — Drivers G1.10 · coerência visual sem provas obsoletas

**Data:** 13/11/2026 (execuções de CI atravessaram para 14/11 UTC).
**Repositório:** `walissonminel-1991/gerenciador-final`. **PR #79:** Draft, branch `drivers-pnp-problemas-consulta-20261113`; base `candidata-portatil-pessoal-pc-forte-20261101`.
**SHA congelado G1.9:** `425ecca555ff0120b7ad5a12f783125189d6e4ac`.
**G1–G5:** permanecem abertos. **Roadmap:** 27/42. Nenhuma escrita em drivers.

## 1. Gate inicial: G1.9 integralmente aprovado ANTES de editar

- [CI Windows 43526995292](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43526995292): `completed/SUCCESS` em `425ecca5...`, incluindo drivers Windows 2022/2025 (24/24 G1.9 cada), regressões Windows gerais, teste de layout, segurança, consentimento e compilação x64. Job exclusivo da main `skipped` por condição.
- [Integridade 43526995223](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43526995223): `completed/SUCCESS` **no mesmo SHA**, sem instalação.
- PR #79 aberta, `draft=true`, base indicada, sem merge.

## 2. Contraexemplo no código real

Antes de G1.10, `DriverReadOnlyDialog.cs` chamava `Render()` na abertura e após o encerramento de `Run()`. Uma alteração de inventário ou seleção **enquanto o diálogo permanecesse aberto** podia tornar a fotografia obsoleta sem nenhum término de tarefa. Assim, o `DarkLogView` ainda exibia dados antigos — incluindo eventual relatório local de assinatura ou vínculo WUA — e os botões ainda podiam permanecer ativos. A validação G1.9 no instante de publicação não bastava para retirar conteúdo **já publicado**.

Um diálogo modal limita a interação humana com a página pai, mas não demonstra que callbacks assíncronos, refiltros programáticos, descarte do controle, alteração de geração ou retomada de foco preservem a fotografia.

## 3. Menor correção no caminho WinForms de produção

- `DriverReadOnlyDialog.cs`: acrescenta `CheckCurrent()` com transição **irreversível para obsoleto durante a vida do diálogo**. Ao detectar `!isCurrent()`: remove `official` e `verification`, substitui imediatamente o conteúdo do relatório por aviso sem DeviceId/INF, altera status para reabertura, desabilita os dois botões e solicita `current.Cancel()`. **Não aborta thread**, não interrompe `Search` COM síncrono.
- `dialog.Activated` e `dialog.Enter` revalidam a fotografia quando volta o foco. Não há polling, timer, thread ou serviço adicional na produção. `Render`, entrada de comando, retorno de consulta, cancelamento, exceção e `finally` respeitam o estado obsoleto; o `finally` não reativa botões antigos.
- `dialog.FormClosed`: marca encerramento, cancela somente a espera ativa, descarta as referências de resultado e impede publicação/status após fechamento. O callback de invalidação é registrado antes de `ShowDialog` e removido no `finally`.
- `DriversPlaceholder.cs`: entrega callback apenas para invalidar a tela; notifica no aumento da geração PnP, refiltro `Render`, alteração da seleção e descarte da página. O vínculo com `DriverReportPublicationGuard` G1.9, incluindo cópia das evidências PnP/Store, permanece.
- `DarkDriverList.cs`: evento WinForms simples `SelectionChanged`, emitido por `SelectIndex` ou quando `SetItems` troca efetivamente a seleção. Não altera a geometria dos cards, rolagem nem a lógica de correspondência.
- A rotina de cada botão agora se chama `RunOfficial` / `RunInf`; o evento `Click` chama o mesmo método. Três parâmetros **opcionais e somente de fixture** permitem substituir apenas tarefas reais por `Task` sintética, prazo de teste e acionador dos mesmos handlers; produção sempre usa os valores padrões, sem nova fonte ou privilégio.
- `DriverOfficialLookup.cs`, guard COM de G1.7, regra de uma pesquisa STA, verificação nativa do INF, pacote e fonte WUA: **não foram modificados**.

## 4. Testes adversariais reais de interface

`TestesLayout/DriverReadOnlyDialogTemporalTests.cs` foi incluído na **mesma rota** `--drivers-readonly` após o teste visual preexistente. Testa janela modal WinForms real e motores WUA/INF substituídos por objetos artificiais em memória:
1. Verifica que `DarkDriverList` notifica seleção e refiltro sem polling.
2. Inicia consulta fictícia, incrementa geração durante a espera, notifica a página e comprova desaparecimento do texto e desabilitação dos botões em **500×340** e **790×510**. O retorno WUA atrasado não é renderizado; Fechar permanece acessível.
3. Faz `WaitAsync` da UI expirar em prazo artificial curto enquanto `Task` permanece pendente. Impede reentrar numa segunda consulta, preserva o relatório anterior, e não publica resultado tardio.
4. Inicia verificação INF fictícia, muda a geração **sem notificação**, invoca o evento real `OnActivated`; a revalidação deve ocultar prova antiga. Fecha a janela com tarefa pendente e confirma que retorno posterior não publica nada.
5. Reabre novo diálogo/generation e verifica que dados legítimos/botões voltam a aparecer, sem herdar estado obsoleto.
6. Mantém a fixture visual de 500×340 e 790×510, rolagem escura, botões acessíveis, sem áreas fora da tela ou consultas reais.

**Regressão da fixture durante o desenvolvimento:** a CI [43530507571](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43530507571) falhou porque `PerformClick()` programático não acionava o handler no runner. Um segundo ensaio [43531082398](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43531082398) demonstrou precisamente `launched=0`, status inicial e botão `CanSelect=true`. A menor correção: a fixture passou a invocar **os mesmos métodos locais dos eventos Click**, em vez de exigir foco programático do runner. Isso não representa homologação de clique físico ou do PC pessoal.

Na validação técnica pré-checkpoint [43531612762](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43531612762), o passo `Conferir leitura e rolagem do diálogo de drivers em viewport pequeno` foi observado como **completed/SUCCESS**. Não confundir essa execução intermediária com aprovação global do commit documental final.

## 5. Evidência, limites e gate final

O teste é de **layout WinForms real com respostas WUA/INF sintéticas**. Ele não comprova oferta WUA positiva real, assinatura de driver remoto, `DriverVer`, `TargetOSVersion`, ranking, instalação, atualização ou compatibilidade do pacote. Também não demonstra que a UI se atualiza espontaneamente quando um processo externo modifica o SO sem qualquer evento do GF: **não há monitoramento contínuo**. A próxima revalidação ocorre no evento de aplicação/foco. Em nenhum cenário `WaitAsync` encerra a chamada COM bloqueada.

`CanInstall=false`, `CanMaintain=false`, `CompletePackageVerified=false`, `IsNewerProven=false`. **GO** apenas para diagnóstico e retenção da proteção visual, se CI final passar; **NO-GO** para toda manutenção de drivers.

O commit que inclui **este documento** precisa ter **CI Windows 2022/2025**, regressões gerais, teste visual temporal, segurança/consentimento, compilação Windows x64 e **integridade externa** `completed/SUCCESS` no **mesmo SHA**. Uma CI intermediária cancelada, falha de fixture ou jobs de drivers isolados verdes NÃO satisfazem esse gate.

## 6. Continuidade G1.11 proposta, limitada

Antes de ampliar motores, verificar a **integridade da interface consultiva real**: se avisos de obsolescência e de bloqueio aparecem corretamente com dispositivo selecionado, nenhum dispositivo e enumeração parcial; testar redução de DPI/resize, navegação via teclado e acessibilidade com foco, sem modificar motor WUA nem habilitar atualização. Se faltar evidência de WUA positiva real, declarar bloqueio de homologação funcional, não inventar oferta.

**Fora de escopo e intactos:** `main`, G5 autoatualizador, D1, Worker, Cron, Secrets, BETA, releases, Driver Store, políticas, UAC, instalação/staging/rollback/remover drivers, downloads, PC FORTE, PC FRACO.

## 7. Revisão adversarial complementar — descarte da página

**Contraexemplo no HEAD anterior `394c9d49...`:** `DriversPlaceholder.root.Disposed` invocava `invalidateOpenReport` sem alterar `inventoryGeneration`. Caso a última lista e seleção continuassem referenciadas, `DriverReportPublicationGuard.IsCurrent` ainda podia retornar `true` durante a notificação. Portanto, descartar a página não eliminava necessariamente o texto de INF/WUA já renderizado. É uma falha distinta do cancelamento e retorno tardio cobertos nos ensaios anteriores.

**Correção localizada:** avançar `inventoryGeneration` **antes** do callback de descarte, sem tocar WUA, STA, Store, formato de pacotes, instalação ou o diálogo. A transição irreversível de `CheckCurrent` continua responsável por limpar texto, descartar resultados e desarmar botões.

**Regressão WinForms sintética adicional:** o ensaio temporal inclui o evento real `Panel.Disposed`, geração aposentada antes da notificação, e `AssertObsolete` nos viewports 500×340 e 790×510; a ligação específica com o `root.Disposed` de produção foi inspecionada no código-fonte, sem pretender que o painel sintético substitua a página real. Nenhuma leitura ou escrita de drivers.

**Gate:** revisão complementar sujeita a novo par de CIs Windows + integridade, ambas `completed/SUCCESS` no mesmo SHA final; até a conclusão, esta revisão **não** substitui o gate verde anterior do SHA `394c9d49...`. PR #79 preservada como Draft, G1–G5 abertos, roadmap 27/42.
