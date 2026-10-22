# MISSÃO WORK 7 — auditoria e candidata v5.13.11.42 TESTE

Data: 22/10/2026. **Código final validado: `bc9fd8a094ef934428ea7c66150278b9aa5f0ca7`.** Branch: `piloto-diagnostico-github-v5.13.11.2`. [PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40), mantido Draft. Roadmap: **27/42 (64,3%)**, sem novos checkpoints físicos concluídos.

## Estado inicial

O HEAD remoto era `908a333545831d74876dbb260a755ee2f7ac4f09`, igual ao informado na missão; a base da PR era `410d65e23546976db456337c71e4705f10217d36`. Não havia alterações posteriores no início da investigação. A .41 tinha compilação aprovada e regressão Windows **reprovada**: [regressão 39857308603](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39857308603), [build 39857308591](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39857308591), [política 39857308624](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39857308624). Foram recuperados os logs completos desses três jobs e examinados os commits da preparação da .41.

A falha estava no teste de Reparo e Rede: ele selecionava o viewport externo por conter descendentes com “Verificar Windows”. Contava, portanto, outros painéis junto dos cartões. Não demonstrava desaparecimento de cartões. O erro interrompia a suíte antes das regressões MSIX, privacidade e encerramento. Um pacote tinha sido gerado mesmo com essa regressão reprovada; não foi homologado nesta missão.

Foram lidos os diagnósticos privados .39 e .40, sem publicá-los no repositório. Na .40, o código 2 da desinstalação precede uma consulta com 172 registros e outra com 171. Isso **não identifica qual produto saiu** nem comprova conclusão da remoção. As consultas Windows Update com zero e uma oferta são compatíveis com mudança legítima no serviço; os logs antigos não contêm dados suficientes para atribuir uma causa individual à KB2267602.

## Defeitos, correções e riscos

| ID | Defeito real e risco anterior | Correção e evidência | Situação |
|---|---|---|---|
| A1 | A regra .41 de 7-Zip suprimia atualização de instalações em outra pasta, escopo ou canal; ignorava arquitetura explícita contraditória e versão incoerente no nome. | Correlação exige Registro/escopo conhecidos, mesma pasta local e metadados coerentes. 14 casos adversariais, dos quais dez falharam antes e todos passaram depois. Nenhuma linha é removida do inventário. | Corrigido no código; confirmar o caso físico. |
| A2 | Um resultado RuckZuck marcado como registro antigo impedia reconhecer uma versão oficial ainda mais recente. | Reavaliar a correlação usando a versão oficial escolhida. Regressão 26.04/26.05 instaladas, catálogo 26.05 e fonte oficial 26.06. | Corrigido. |
| A3 | A fusão apagava `RequiresReview` existente. | Preservar a restrição anterior; fonte sem arquitetura também não contorna conflito explícito do inventário. | Corrigido. |
| A4 | A fonte oficial ignorava “7-Zip 26.05 (x64 edition)”, embora o catálogo reconhecesse o produto. | Gramática oficial aceita somente a variante prevista, mantendo editor e identidade. | Corrigido. |
| A5 | WUA `SucceededWithErrors` poderia ser apresentado como busca concluída, inclusive vazia. | Exigir `ResultCode=2` em consulta e revalidação antes da instalação. Ofertas inválidas ou revisões contraditórias tornam a consulta inconclusiva. | Corrigido; não prova a causa das diferenças .39/.40. |
| A6 | A consulta manual reutilizava a primeira fotografia indefinidamente; uma falha podia conservar seleção acionável antiga. | Consulta explícita usa `force: true`; falha limpa seleção/ofertas, invalida o estado de sucesso e impede instalação. Consulta simultânea na mesma janela não inicia outra busca. | Corrigido; validação presencial pendente. |
| A7 | Se apenas parte da seleção continuasse elegível, o lote podia instalar esse subconjunto. Download parcial não impedia a etapa de instalação. | Exigir a seleção inteira, com revisão e licença, e download inteiramente bem-sucedido antes de instalar. | Corrigido; nenhum download/instalador real executado pelos testes desta correção. |
| B1 | Qualquer código de saída não zero virava falha sem observar estado posterior. | Examinar sete fotografias posteriores. Erro com ausência, fonte parcial ou estado alterado fica pendente; erro com presença persistente vira falha. Código original preservado. | Corrigido; conclusão real do foobar permanece não comprovada. |
| B2 | Duas ausências transitórias encerravam a conferência; um lote podia iniciar outro fornecedor com o anterior pendente. | Observar a janela inteira, exigir três ausências finais e interromper lote em pendência. Código 3010 exige reinicialização e não libera limpeza. | Corrigido; ausência de Registro não equivale a remoção integral. |
| B3 | O tratamento genérico de erros WinVerifyTrust permitia advertência para desconfiança explícita ou erro desconhecido. | Lista explícita de falhas conhecidas de cadeia/validade; adulteração, revogação, desconfiança explícita e estados desconhecidos bloqueiam o EXE. Ausência de assinatura e editor divergente validado continuam com aviso. | Corrigido. |
| C1 | Limpeza foobar/OBS/VLC comparava só pasta, quantidade e bytes; não fixava conteúdo, identidade de arquivos ou chave de Registro. Exclusão recursiva permitia mudança após prévia e relato incompleto em cancelamento. | Suspender todos os pontos de exclusão desses perfis. A UI explica a preservação, sem confirmação enganosa. Executor destrutivo sintético só é compilado no projeto de testes, fora da aplicação. | **Mitigado. R4 continua suspenso.** |
| D1 | A regressão de margens selecionava o painel externo e reprovava com contagem incorreta. | Nomear a grade `RepairActions` e selecioná-la de forma única. Continuam exigidos sete cartões, três colunas quando cabíveis e ocupação mínima de largura. | Corrigido e aprovado na CI Windows, com capturas dos dois tamanhos de referência. |
| E1 | O PID era reaberto depois de consultar início/caminho, sem conferir a identidade no handle usado pelo ajuste de memória. | Consultar PID, início e executável pelo mesmo handle entregue a `EmptyWorkingSet`; serializar ajustes. Testes não esvaziam working set de processos. | Corrigido estruturalmente; ganho físico e AVG pendentes. |
| F2 | O teste de fontes ao vivo retornava aprovação mesmo com fontes falhando, contrariando o contrato do job manual. | Consulta real anterior: 19/20 fontes OK, feed Windows Proton expirado, mas exit code zero. Três controles negativos reproduziram a aceitação incorreta. Exigir diagnóstico único e sucesso de todas as fontes esperadas. | Corrigido no teste; disponibilidade externa segue uma fotografia, não garantia. |
| F1 | Compilação/pacote eram independentes da regressão e podiam ser publicados para teste com CI vermelha. | Build depende dos dois workflows reutilizáveis: regressões Windows e política. Grupos de concorrência distintos evitam cancelamento do próprio chamador. Actions externas continuam fixadas por SHA, sem credenciais persistentes. | Validado: duas falhas de regressão bloquearam pacote; após aprovação, o build foi iniciado e passou. |

Quando `InstallLocation` estiver ausente, apenas a pasta de um desinstalador EXE absoluto e validado pode auxiliar a correlação consultiva. MSI, hosts, UNC e caminhos relativos não geram essa evidência. Isso não concede autorização para executar instaladores ou apagar pastas.

## Arquivos alterados e impactos

- **Inventário/atualizações:** `InventoryReconciler.cs`, `Matching.cs`, `RuckZuckModels.cs`, `OfficialReleaseSources.cs`, `Program.cs`. Metadados de instalação passam ao matcher; comparação não apaga registros. Fontes incertas continuam consultivas.
- **Windows Update:** `WindowsUpdateSearchPolicy.cs`, `AdvancedFeatures.cs`, `IntegratedSystemUi.cs`. Contrato de identidade/revisão compartilhado; nenhum teste novo instala atualizações.
- **Desinstalação:** `UninstallBatch.cs`, `RegisteredUninstaller.cs`. Espera limitada por estado e interrupção de lote. Não há segunda tentativa automática nem remoção forçada de arquivos do fornecedor.
- **Resíduos:** `FoobarProfileCleanup.cs`, `FoobarProfileCleanupFlow.cs`, `ScopedAppProfileCleanup.cs`, `ResidualExplorerDialog.cs`. Leitura/classificação preservadas; exclusão de perfis suspensa por falta de prova. O resumo agregado permanece um resumo, não uma autorização.
- **RAM:** `SelectiveMemoryTrim.cs`. Mesmo handle para conferir identidade e ajustar. A API continua seletiva; não há limpeza de standby ou componentes essenciais.
- **Testes:** novos `Work7IdentityTests`, `Work7UpdateTests`, `Work7UninstallTests`, `Work7SafetyTests`; entradas das suítes atualizadas. `Work6UninstallFlowTests` passa a alterar a fotografia quando o fornecedor simulado é lançado, em vez de fazer outro produto desaparecer por número de leituras. Mantidas as mesmas exigências de ordem, identidade e uma execução por produto.
- **Controles/CI:** `scripts/test_work6_mutations.py`, `scripts/test_politicas_seguranca.py` e os três workflows atuais. Mutação de ausência transitória adaptada à nova política, sem retirar seu controle negativo. Versão do projeto e manifesto alinhadas em .42; README, changelog e roadmap atualizados.

## Testes e evidência antes/depois

| Categoria | Execução local | Limite |
|---|---|---|
| Motor/identidade | Casos WORK 7: 4/14 antes, 14/14 depois; mais 20 casos existentes aprovados. | Fotografia sintética; não examina o Registro do PC FORTE. |
| Fusão/fontes | Três falhas reproduzidas antes: versão oficial superior suprimida, revisão apagada e nome “x64 edition” sem fonte. Suíte final local: 159/159. | HTTPS simulado; não prova atualizações instaláveis. |
| Desinstalação | Nove falhas em dez cenários novos antes; dez após correção, mais cinco controles de caminho. Fluxo WORK 6 relacionado também aprovado localmente. | Fornecedor é callback; nenhum desinstalador real é iniciado. |
| Políticas | Dois controles novos falharam antes (alcance destrutivo dos perfis e resultado WUA não verificado). Depois: 18/18 políticas locais. | Análise estática não equivale a segurança universal. |
| Cross-build | Aplicação e TestesLayout compilados para Windows no Linux com analisadores, sem avisos/erros na rodada local. | Compilar no Linux não executa WinForms, WUA, Authenticode ou SetupAPI. |
| Segurança nativa | Nova regressão exige preservação após troca de conteúdo com mesmo tamanho e ausência do executor sintético no assembly distribuído. Confere identidade do próprio processo por handle, sem ajuste de memória. | Regressão executada e aprovada no Windows; nenhum working set alterado. |
| Mutação | Controles existentes preservados; execução final registrada abaixo. | Teste só vale se o comportamento correto passa e a retirada da proteção reprova pela causa esperada. |
| Layout/desempenho | Mantida matriz de 132 combinações de resolução/aba, modelos DPI e ciclos de bandeja. Benchmark sintético de classificação/navegação adicionado à CI. | Runner não reproduz monitores, AVG, carga ou todos os DPI físicos dos dois PCs. |

Primeira CI WORK 7: [39861447294](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39861447294), commit `e982022`. Passaram 127/127 atualizador, 155/155 multifontes nessa revisão, 475/475 resíduos, 10/10 mutações e controles nativos R4/RAM. A suíte parou em uma assertiva de preservação: a nova frase tinha omitido a informação “outros itens preservados”. A mensagem foi corrigida, preservando a assertiva. A nova dependência de CI funcionou: build/pacote ficaram **skipped**, sem gerar candidata reprovada.

A segunda CI, [39862410092](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39862410092), commit `4ff82d2`, aprovou 159/159 multifontes, 475/475 resíduos, 10/10 mutações, controles nativos R4/RAM, **132 combinações de resolução/aba e 12 ciclos de bandeja**. A captura adicional de referências falhou porque redimensionava o Form, sujeito ao limite do monitor físico do runner, em vez de simular o viewport. A captura foi corrigida para usar a mesma superfície da matriz, sem reduzir a exigência de três colunas e 85% de ocupação. O ZIP de capturas dessa rodada foi baixado e seu SHA-256 conferido (`42c1ef38b6fccf869d57eb3019946f19d472056e12f2b2ad3fc1b2c645772b34`). A inspeção visual de 1440×900 e 1366×768 mostrou três colunas, botões e descrições legíveis. O pacote executável continuou bloqueado pela CI.

**Rodada final aprovada:** [39863103739](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39863103739), fonte `bc9fd8a094ef934428ea7c66150278b9aa5f0ca7`. Política, regressões Windows e compilação portátil: **success**. Foram aprovados 14/14 casos novos de identidade mais 20 existentes, 17/17 catálogo, 159/159 multifontes, 127/127 atualizador, 475/475 resíduos/desinstalação, 10/10 mutações, 47/47 MSIX e 30/30 encerramento. Também passaram WSC, drivers/histórico, prévias com identidade, privacidade, modelos de RAM, 132 combinações de resolução/aba, 12 ciclos de bandeja (608 ms no runner) e as duas capturas específicas de Reparo. A aplicação e a suíte de layout compilaram com analisadores e zero avisos/erros.

O pacote foi gerado somente depois dessas validações. Foram baixados o ZIP e as 56 capturas sintéticas, sem executar o binário Windows no host local. O ZIP de capturas tem SHA-256 `d08a0ca5ef4a1b24777d5569f04e3501fef00737e2ddd1c47afbad31c4d48f12`. A inspeção visual incluiu Reparo em 1440×900/2560×1440 e Limpeza/Sistema em 1440×900. A captura maior conserva três colunas, tipografia legível e largura ocupada; não comprova DPI/hardware físicos.

Jobs opcionais de fontes externas/downloads de instaladores na CI ficaram **skipped**, assim como a atestação restrita à main. Isso está separado das consultas HTTPS locais abaixo. Não houve testes reais de instalação de aplicativos nem validação AVG.

Rede somente leitura no host local: RuckZuck retornou **631 entradas**. A consulta HTTPS de fontes observou **19/20 OK**, com `proton-pass-windows=TaskCanceledException`; o teste antigo retornou sucesso incorretamente e motivou F2. Nenhuma instalação, autenticação de usuário, upload ou download de instalador fez parte dessas consultas. O timeout do feed não foi mascarado com redução dos critérios de segurança. Na consulta posterior, com a política reforçada, **20/20 fontes responderam** e o teste passou. A recuperação não apaga o primeiro timeout nem comprova disponibilidade contínua.

## Benchmark Windows sintético (primeira rodada)

Runtime 10.0.13, quatro CPUs do runner; três amostras por cenário. Não houve leitura de resíduos reais nem comparação de ganho com a versão anterior.

| Apps / evidências sintéticas | Classificação, mínimo–máximo | Grupos retornados | Contador `unexamined` |
|---|---|---|---|
| 500 / 5.000 | 45,4–116,3 ms | 2.500 | 0 |
| 1.000 / 10.000 | 90,3–139,3 ms | 5.000 | 0 |
| 1.000 / 20.000 | 90,6–249,8 ms | 10.000 | 0 |
| 1.000 / 50.000 | 109,8–114,6 ms | 15.000 | 20.000 |

O cenário de 50.000 esbarra no limite de orçamento: o tempo menor **não significa que tudo foi examinado**. Navegação do índice consumiu 6,4–74,5 ms nas amostras, com uma passagem de filtro por cenário. Essas medições não substituem P5/P6 nos computadores físicos.

## Segurança e privacidade

Foram revisados parser MSI/EXE, preparação e pinning do executável, política de assinatura, comparação de identidade, resultados parciais, saídas tardias, caminhos específicos, limites de resíduos, IPC por peer PID, carregamento de DLLs, logs e consentimento local. As proteções anteriores de downloads verificados, SHA-256, revisão de receita e DLLs System32 foram preservadas. A revisão é direcionada; não constitui auditoria exaustiva de todas as linhas nem certificação.

Sem alterações na `main`, sem release estável, sem WinGet COM/CLI, upload de diagnóstico, novos privilégios ou desativação de AVG/SmartScreen. Não foram instalados/desinstalados programas, apagados resíduos, reparado Windows ou modificados drivers dos computadores pessoais. Arquivos destrutivos de teste ficam restritos a fixtures descartáveis.

Pendências técnicas: executor R4 com identidade/conteúdo fixados até a ação, manifesto e revalidação do Registro, relato de cancelamento parcial; correlação duradoura de desinstaladores com processos filhos; observação além da janela curta; regressões com WUA real sem instalar; disponibilização/assinatura das fontes oficiais; validação AVG. A consulta WUA continua síncrona dentro da thread STA: não congela a UI, mas ainda não há aborto seguro da operação COM em curso.

## Roadmap e próximos testes físicos

Continuam abertos U8, R7, R4, I7, P5/P6, V5 e os demais checkpoints de campo/gestão de drivers. Os 27 concluídos não foram aumentados por simulação.

**PC FORTE, primeiro somente leitura:** usar apenas pacote cujo SHA/commit e CI estejam confirmados; manter antivírus ativo. Abrir .42, registrar inventário Win32/MSIX e repetir “Atualizar e verificar”. Conferir as duas linhas 7-Zip, pasta/escopo correlacionados e versão oferecida; não apagar entradas manualmente. Fazer duas consultas manuais Windows Update e registrar quantidade, horário e eventual inconclusão, sem instalar. Abrir Reparo e Rede, redimensionar e conferir três cartões por linha, textos e resultados. Abrir análise de resíduos: perfis devem explicar preservação e não oferecer confirmação destrutiva. Registrar tempo de abertura, RAM/CPU, minimizar/restaurar e encerramento normal. Não concluir ganho de RAM apenas pelo working set.

**PC FRACO:** repetir consultas e registrar fonte MSIX/parcialidade; falha não pode significar que apps foram removidos. Conferir 1440×900 e DPI real, botões acessíveis, rolagem, três cartões quando houver largura e redução de colunas quando necessária. Cancelar consulta de drivers e confirmar que lista/botão se recuperam. Fazer ciclos de bandeja; registrar RAM/CPU, tempo e alertas do AVG.

**Bateria posterior, somente após autorização específica:** uma atualização por vez, com versão antes/depois e editor/arquitetura; uma desinstalação inofensiva com registro de estado e preferências preservadas. Não repetir fornecedor automaticamente quando pendente. R4 e qualquer operação de driver continuam indisponíveis; não executar reparos reais ou exclusões como parte do roteiro de consulta.

## Entrega GitHub e integridade

- Branch: `piloto-diagnostico-github-v5.13.11.2`; [PR #40 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/40). `main` preservada.
- Commits: `0383ddef` (atualizações/WUA), `2c9c1073` (desinstalação/perfis/RAM), `e982022c` (gates e .42), `4ff82d2f` (fonte ao vivo e explicação de preservação), `bc9fd8a0` (captura por viewport). Consolidação documental posterior não altera C# nem executáveis da candidata conferida.
- [CI aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39863103739); [pacote portátil TESTE conferido](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39863103739/artifacts/12462353118).
- Artefato: `GerenciadorFinal-v5.13.11.42-f4d1d46c-TESTE-win-x64`; versão .42; 58.938.143 bytes de ZIP; **273 hashes internos conferidos**. ZIP comparado com o digest GitHub; manifesto exige que todo arquivo, salvo o próprio manifesto, esteja listado.
- `BUILD-INFO`: fonte `bc9fd8a094ef934428ea7c66150278b9aa5f0ca7`, merge sintético `f4d1d46c488e8df3f6efe077e6caa8c0a3c24d99`, base `410d65e23546976db456337c71e4705f10217d36`, run `39863103739`; todos conferidos. Manifesto Win32/SxS aprovado no runner.
- SHA-256 do ZIP: `dcd654318544d355d7d32922188cd5621ae64a3ac27a60bdff293698678413d1`.
- SHA-256 EXE: `7ab897a2e235ac377bc02a283c1c63a1168c517e8cb03345bf0cba8f2fda049f`.
- SHA-256 DLL principal: `2e6a3101c74f3779b3a3b749bdd02e32b753801bbf57ced90ee0db44e2e175cb`.
- Sem assinatura Authenticode e sem aprovação AVG. Não é release estável. O artefato de Actions tem retenção até 29/10/2026; os fontes e as evidências documentais permanecem no Git.

## Checkpoint técnico para continuidade

Atualizações, desinstalações e preservação foram auditadas/corrigidas e aprovadas nas suítes, sem fechar os portões físicos. Próxima etapa: bateria de consulta nos dois PCs, depois U8/R7 supervisionados. Não retomar R4 antes de projetar e testar um executor que fixe identidade/conteúdo por alvo, confira o Registro e relate cancelamento/parcialidade. Não inferir remoção por contagem global, não repetir desinstalador pendente e não liberar atualização por fonte incompleta.

A lista completa de arquivos alterados pode ser conferida em `git diff --name-only 908a333545831d74876dbb260a755ee2f7ac4f09..bc9fd8a094ef934428ea7c66150278b9aa5f0ca7`:

```text
.github/workflows/build-windows.yml
.github/workflows/triagem-diagnostico-github.yml
.github/workflows/validar-candidata-windows.yml
AdvancedFeatures.cs
CHANGELOG.md
FoobarProfileCleanup.cs
FoobarProfileCleanupFlow.cs
GerenciadorAplicativos.csproj
IntegratedSystemUi.cs
InventoryReconciler.cs
Matching.cs
OfficialReleaseSources.cs
Program.cs
README.md
RegisteredUninstaller.cs
ResidualExplorerDialog.cs
RuckZuckModels.cs
ScopedAppProfileCleanup.cs
SelectiveMemoryTrim.cs
TestesLayout/Program.cs
TestesLayout/Work7SafetyTests.cs
TestesMotor/Program.cs
TestesMotor/Work7IdentityTests.cs
TestesMultifontes/LiveSourceValidation.cs
TestesMultifontes/Program.cs
TestesMultifontes/TestesMultifontes.csproj
TestesMultifontes/Work7UpdateTests.cs
TestesResiduos/Program.cs
TestesResiduos/Work6UninstallFlowTests.cs
TestesResiduos/Work7UninstallTests.cs
UninstallBatch.cs
WindowsUpdateSearchPolicy.cs
app.manifest
docs/MISSAO-WORK7-RESULTADO.md
docs/PROGRESSO.json
docs/ROADMAP.md
scripts/test_politicas_seguranca.py
scripts/test_work6_mutations.py
```

Referências das APIs: [OperationResultCode](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/ne-wuapi-operationresultcode), [ISearchResult.ResultCode](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-isearchresult-get_resultcode), [EmptyWorkingSet](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset) e [GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes).
