# MISSÃO WORK 7 — auditoria e candidata v5.13.11.42 TESTE

Data: 22/10/2026. Branch: `piloto-diagnostico-github-v5.13.11.2`. [PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40), mantido Draft. Roadmap: **27/42 (64,3%)**, sem novos checkpoints físicos concluídos.

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
| D1 | A regressão de margens selecionava o painel externo e reprovava com contagem incorreta. | Nomear a grade `RepairActions` e selecioná-la de forma única. Continuam exigidos sete cartões, três colunas quando cabíveis e ocupação mínima de largura. | Correção do teste; layout nativo aguarda CI. |
| E1 | O PID era reaberto depois de consultar início/caminho, sem conferir a identidade no handle usado pelo ajuste de memória. | Consultar PID, início e executável pelo mesmo handle entregue a `EmptyWorkingSet`; serializar ajustes. Testes não esvaziam working set de processos. | Corrigido estruturalmente; ganho físico e AVG pendentes. |
| F1 | Compilação/pacote eram independentes da regressão e podiam ser publicados para teste com CI vermelha. | Build depende dos dois workflows reutilizáveis: regressões Windows e política. Grupos de concorrência distintos evitam cancelamento do próprio chamador. Actions externas continuam fixadas por SHA, sem credenciais persistentes. | Corrigido no workflow; validar o encadeamento na CI. |

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
| Fusão/fontes | Três falhas reproduzidas antes: versão oficial superior suprimida, revisão apagada e nome “x64 edition” sem fonte. Suíte final local: 155/155. | HTTPS simulado; não prova atualizações instaláveis. |
| Desinstalação | Nove falhas em dez cenários novos antes; dez após correção, mais cinco controles de caminho. Fluxo WORK 6 relacionado também aprovado localmente. | Fornecedor é callback; nenhum desinstalador real é iniciado. |
| Políticas | Dois controles novos falharam antes (alcance destrutivo dos perfis e resultado WUA não verificado). Depois: 18/18 políticas locais. | Análise estática não equivale a segurança universal. |
| Cross-build | Aplicação e TestesLayout compilados para Windows no Linux com analisadores, sem avisos/erros na rodada local. | Compilar no Linux não executa WinForms, WUA, Authenticode ou SetupAPI. |
| Segurança nativa | Nova regressão exige preservação após troca de conteúdo com mesmo tamanho e ausência do executor sintético no assembly distribuído. Confere identidade do próprio processo por handle, sem ajuste de memória. | Execução nativa depende da CI Windows. |
| Mutação | Controles existentes preservados; execução final registrada abaixo. | Teste só vale se o comportamento correto passa e a retirada da proteção reprova pela causa esperada. |
| Layout/desempenho | Mantida matriz de 132 combinações de resolução/aba, modelos DPI e ciclos de bandeja. Benchmark sintético de classificação/navegação adicionado à CI. | Runner não reproduz monitores, AVG, carga ou todos os DPI físicos dos dois PCs. |

Os testes nativos e a CI final ainda estão em andamento nesta revisão do relatório. Os resultados finais, commits e links serão consolidados após sua conclusão. Não há build homologada da .42 neste ponto.

## Segurança e privacidade

Foram revisados parser MSI/EXE, preparação e pinning do executável, política de assinatura, comparação de identidade, resultados parciais, saídas tardias, caminhos específicos, limites de resíduos, IPC por peer PID, carregamento de DLLs, logs e consentimento local. As proteções anteriores de downloads verificados, SHA-256, revisão de receita e DLLs System32 foram preservadas. A revisão é direcionada; não constitui auditoria exaustiva de todas as linhas nem certificação.

Sem alterações na `main`, sem release estável, sem WinGet COM/CLI, upload de diagnóstico, novos privilégios ou desativação de AVG/SmartScreen. Não foram instalados/desinstalados programas, apagados resíduos, reparado Windows ou modificados drivers dos computadores pessoais. Arquivos destrutivos de teste ficam restritos a fixtures descartáveis.

Pendências técnicas: executor R4 com identidade/conteúdo fixados até a ação, manifesto e revalidação do Registro, relato de cancelamento parcial; correlação duradoura de desinstaladores com processos filhos; observação além da janela curta; regressões com WUA real sem instalar; disponibilização/assinatura das fontes oficiais; validação AVG. A consulta WUA continua síncrona dentro da thread STA: não congela a UI, mas ainda não há aborto seguro da operação COM em curso.

## Roadmap e próximos testes físicos

Continuam abertos U8, R7, R4, I7, P5/P6, V5 e os demais checkpoints de campo/gestão de drivers. Os 27 concluídos não foram aumentados por simulação.

**PC FORTE, primeiro somente leitura:** usar apenas pacote cujo SHA/commit e CI estejam confirmados; manter antivírus ativo. Abrir .42, registrar inventário Win32/MSIX e repetir “Atualizar e verificar”. Conferir as duas linhas 7-Zip, pasta/escopo correlacionados e versão oferecida; não apagar entradas manualmente. Fazer duas consultas manuais Windows Update e registrar quantidade, horário e eventual inconclusão, sem instalar. Abrir Reparo e Rede, redimensionar e conferir três cartões por linha, textos e resultados. Abrir análise de resíduos: perfis devem explicar preservação e não oferecer confirmação destrutiva. Registrar tempo de abertura, RAM/CPU, minimizar/restaurar e encerramento normal. Não concluir ganho de RAM apenas pelo working set.

**PC FRACO:** repetir consultas e registrar fonte MSIX/parcialidade; falha não pode significar que apps foram removidos. Conferir 1440×900 e DPI real, botões acessíveis, rolagem, três cartões quando houver largura e redução de colunas quando necessária. Cancelar consulta de drivers e confirmar que lista/botão se recuperam. Fazer ciclos de bandeja; registrar RAM/CPU, tempo e alertas do AVG.

**Bateria posterior, somente após autorização específica:** uma atualização por vez, com versão antes/depois e editor/arquitetura; uma desinstalação inofensiva com registro de estado e preferências preservadas. Não repetir fornecedor automaticamente quando pendente. R4 e qualquer operação de driver continuam indisponíveis; não executar reparos reais ou exclusões como parte do roteiro de consulta.

## Checkpoint GitHub

A candidata é .42 TESTE e o PR permanece Draft. A CI deve aprovar o código final antes de a etapa de pacote começar. A conclusão será registrada aqui após os jobs finalizarem; não usar artefato antigo da .41 como substituto.
