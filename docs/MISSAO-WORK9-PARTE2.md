# MISSÃO WORK 9 — Parte 2: consolidação e candidata Windows

23/10/2026 · PR #40 Draft · branch piloto-diagnostico-github-v5.13.11.2.

## Ponto de partida confirmado

HEAD remoto/local `473a9558c33d12a3ce29c1ac51a6aed8730414ef`, posterior ao
checkpoint d065f9c6. Última CI aprovada da Parte 1: fonte
`c4b2fab68af1d35577f4f8b2e36214bc5ee9a4f9`,
[run 39980934418](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39980934418).
O commit 473a9558 só consolidou documentação; o produto era o mesmo da CI.
Main continuou em `410d65e23546976db456337c71e4705f10217d36`.

As [13 causas corrigidas, 27 asserções que falhavam e 42 controles novos da
Parte 1](MISSAO-WORK9-PARTE1.md) foram preservados. U8/R7/R4 continuavam abertos;
a missão não pressupôs homologação física. Versão de entrada .48 TESTE;
sequência reservada para esta candidata: **5.13.11.49 TESTE**.

## Alterações concretas

| Problema | Evidência/causa | Correção e validação |
| --- | --- | --- |
| Portátil sem licença GF, avisos de runtime, privacidade e guia (média) | ZIP anterior tem 274 arquivos; o verificador novo reprova sete ausências obrigatórias | Publish inclui sete documentos/avisos, três copiados dos próprios packs resolvidos. Cross-publish local e pacote final Windows passaram: 281 arquivos/280 hashes. O ZIP baixado também foi conferido independentemente, sem executar o EXE. |
| Conferência do pacote limitada a hashes gerados no mesmo job (média) | Não havia teste independente da cobertura exata, composição e origem do portátil | Verificador de leitura sem execução exige SHA de fonte/base/merge, versão, PE AMD64, self-contained, licenças e cobertura exata do manifesto. Onze testes adversariais incluem corrupção, omissão/extra, traversal, duplicata, certificado acidental, arquitetura e origem erradas. |
| README e organização davam instruções antigas (média) | Apontavam .42/.35 e afirmavam exclusão integralmente indisponível, embora existissem pilotos restritos posteriores | Estado corrente, pesquisa geral somente leitura, pilotos e homologação pendente separados; roteiro único para o próximo teste. Privacidade consolidada sem mudar consentimento/armazenamento. |
| Leitores antigos mantinham caminhos paralelos (baixa/manutenção) | IntegratedSystemReader.Processes/StartupEntries/WindowsUpdate sem chamadas diretas ou por reflexão; somente Computer é usado por DetailedHardware | Retiradas 168 linhas, interop de serviço e imports exclusivos; arquivo 257→89 linhas. Compilação com analisadores sem avisos/erros. Leituras atuais permanecem em RAM/StartupManager/WindowsUpdateClient. |
| Repetição de cenários na CI (custo/manutenção) | WORK9 rodava em três modos focados e novamente nas suítes; prévia WORK6 duplicada; testes Node repetidos no Windows e política Ubuntu | Suítes completas continuam obrigatórias, mutações preservadas, benchmark reutiliza build. Modos focados permanecem disponíveis. Concurrency de reutilizáveis inclui nome do chamador para não cancelar uma execução manual independente. |

Commits temáticos (todos filhos do HEAD inicial, sem rollback):

| Commit | Arquivos e finalidade |
| --- | --- |
| `68ba7824` | IntegratedSystemPanel.cs: três leitores sem uso, imports e interop exclusivo |
| `6df8479e` | GerenciadorAplicativos.csproj, app.manifest, global.json, build-windows.yml, avisos de terceiros, verificador e seus testes: candidata .49 e pacote |
| `2458e5c0` | validar-candidata-windows.yml e triagem-diagnostico-github.yml: deduplicação e concorrência |
| `2411e7ff` | README, CHANGELOG, PRIVACY, SECURITY, CONTRIBUTING, gitignore e documentação: nove consolidações, referências, roteiro, organização e benchmark compacto |
| `dba59231` | PRIVACY.md, verificar_pacote.py, test_pacote.py e registros: links válidos no portátil e teste adversarial |

Fonte final aprovada: **dba59231b8a8d3d3c2d075a7b794c5817136cdc3**.
O fechamento documental posterior modifica somente README, roadmap/progresso e
este relatório; não altera fontes, workflows, manifesto ou documentos distribuídos.

Antes do fechamento, a revisão do portátil detectou links relativos de privacidade
para documentos ausentes no ZIP. Um teste novo reproduziu duas aceitações
indevidas (link ausente e caminho externo); após a correção, os 29 testes Python
passaram. A conferência do publish também reprovou o PRIVACY.md anterior e
passou com os endereços oficiais completos. O verificador passou a conferir
links internos dos Markdown distribuídos, além dos hashes. A CI inicial da .49
não deve ser usada como aprovação dessa correção posterior.

## Inventário e faxina

Base medida com `git ls-tree -r -l 473a9558`: **254 arquivos / 2.780.038 bytes**
de conteúdo versionado, sem contar objetos históricos nem arquivos ignorados.
98 fontes C# na raiz (1.084.288 bytes), 50 fontes de testes (530.087), 56 arquivos
em docs (566.183) e dois recursos binários (394.166). O inventário completo é
reproduzível pelo comando; [classificação e referências](ORGANIZACAO-REPOSITORIO.md).

Foram conferidos curingas do SDK, exclusões Compile, GF_PROGRESSIVE, fontes
vinculadas dos testes, recursos JSON incorporados, reflexão, scripts, workflows
e links Markdown. Os três catálogos têm papéis diferentes e continuam. Scripts
de compilação, ZIP, instalador e assinatura não são duplicatas de finalidade.
O protótipo Node é usado pela CI e não foi confundido com serviço ativo.

Nove documentos foram consolidados, com conteúdo preservado, âncoras, hash
original e URL do arquivo no checkpoint anterior, em
[TESTES-HISTORICOS-PC-FRACO](TESTES-HISTORICOS-PC-FRACO.md): seis relatórios
TESTE-PC-FRACO de 20/10 e 21/10 (.18/.19/.20/.21/.23), checklist .17 e planos
.23/.24. Referências do changelog e roteiro .17 foram atualizadas. O roteiro
TESTE-CONSOLIDADO foi atualizado; missões, decisões de segurança e evidências
únicas foram mantidas. TESTES e OPERACAO-FAXINA-FINAL distinguem conteúdo atual
de arquivo histórico.

O JSON bruto do benchmark passou de **99.569 para 59.595 bytes**, sem alterar
qualquer valor/medição (igualdade estrutural conferida). ZIPs locais com prefixo
GerenciadorFinal entram no gitignore; não foi ignorado nenhum arquivo legítimo.
Nenhum EXE/DLL/ZIP/log de build estava rastreado, portanto nenhuma exclusão
nessa categoria é contabilizada. As 50 exclusões de missões anteriores não
entram nestes números. Não houve rewrite, force push, merge, remoção de branch,
tag, release, run ou artefato remoto.

Contagem final desta missão: **251 arquivos / 2.778.518 bytes** na árvore
versionada, contra 254 / 2.780.038. Redução líquida de três arquivos e
1.520 bytes. O total inclui os novos verificadores, licenças/índice e este
relatório: não confundir bytes líquidos com os 39.974 bytes poupados isoladamente
no JSON. O armazenamento do histórico Git não foi reduzido nem reescrito.

## GitHub Actions e regressões

Três workflows preservados, Actions fixadas por SHA, checkout sem credenciais
persistentes, contents:read e OIDC/atestação somente na main. SDK selecionado por
global.json, NuGetAudit segue ativo inclusive quando a fonte falha. Não foi
introduzido cache que dispense restore/auditoria. Como não há PackageReference
externo explícito, cache com lock de dependências fica para avaliação mensurada.
Os testes são console, não `dotnet test`: 11 projetos obrigatórios, TestesRede e
consultas/downloads ao vivo opcionais por dispatch. Nenhum instalador é executado.

O build continua dependendo de regressões e política da mesma revisão. Artefato
usa agora SHA curto da fonte; BUILD-INFO conserva também merge sintético/base/run.
SHA256SUMS cobre todos os arquivos além do próprio manifesto. Retenção: sete dias.

Validação local já executada: build Release com analisadores (zero avisos/erros),
cross-publish win-x64 self-contained, 29 testes Python aprovados (18 políticas +
11 de pacote), links relativos sem alvos ausentes, identidade estrutural do JSON
de benchmark e reprodução negativa no ZIP anterior. Cross-publish foi uma
árvore de trabalho editada e não é artefato distribuído nem teste Windows nativo.

**CI da .49 aprovada:** [run 39987151553](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39987151553), fonte dba59231; política 120529235838,
regressão 120529235636 e build 120530720736, todos success. O run inicial
39986611985 foi cancelado pela correção posterior dos links; não é usado como
validação final. Atestação main-only e jobs de fontes/downloads externos ficaram
skipped, conforme sua condição; não se afirma que fontes ao vivo foram testadas.

| Execução | Evidência efetiva |
| --- | --- |
| Windows: atualizador / multifontes / catálogo | 148/148, 159/159 e 17/17; modos focados já incluídos, sem contagem dupla |
| Windows: matching e identidade | 20/20 do motor e 18 controles de identidade |
| Windows: resíduos/desinstalação | 527/527, incluindo Registro HKCU/HKLM e Registry32/Registry64, folha/irmã e isolamento HKLM de mesmo caminho |
| Windows: MSIX / fechamento | 47/47 e 30/30; nenhum processo real encerrado |
| Controles negativos | 10/10 mutações rejeitadas por asserções pertinentes, não por erro de compilação |
| UI Windows | 132 combinações de resolução/aba, 22 cenários, DPI 100/125/150/200%, 12 ciclos de bandeja com HWND estável (578 ms no runner) |
| Demais regressões Windows | Segurança/WSC, drivers/WMI somente leitura, privacidade e diagnóstico local aprovados |
| Política Ubuntu | 29/29 Python (18 políticas + 11 pacote), 167 arquivos-fonte examinados e 10/10 testes Node |
| Publish e conferência externa ao runner | Manifesto Win32 .49/ativação SxS, metadados FileVersion de EXE e DLL .49, 281 arquivos e 280 hashes; runtime .NET/WindowsDesktop 10.0.13, AMD64, manifesto asInvoker |

Os 42 controles da Parte 1 passaram novamente dentro dessas suítes. A inspeção
visual das capturas atuais de 1280×720/1440×900 e do modal de resíduos confirmou
conteúdo escuro e ações acessíveis; a moldura nativa e setas de combos continuam
claras no Server 2022, portanto o requisito de escuro integral não foi declarado
concluído. A indisponibilidade transitória do ambiente local foi superada; o ZIP
final foi baixado e verificado antes do encerramento, sem execução.

**Artefato aprovado somente para TESTE:**
[GerenciadorFinal-v5.13.11.49-dba59231-TESTE-win-x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39987151553/artifacts/12516227552),
retenção até 30/10/2026 16:33:28 UTC. Fonte dba59231; merge sintético testado
`a88af1db245f302da8973d227ff5673ce51a62dc`; base 410d65e2. O digest do ZIP
baixado é idêntico ao digest informado pelo upload/GitHub:

```text
abcb3e228ab2f02197c57c247956d51afc834cfdf4a5d8a742190940ee569fc8
```

O EXE não foi executado no ambiente local; a conferência leu PE, metadados,
manifesto, conteúdo e hashes. CI aprovada não homologa AVG nem os PCs pessoais.

## Desempenho e composição

A Parte 2 não mudou o classificador medido na Parte 1. O ganho comprovado segue
limitado àquele benchmark ABBA, com os mesmos resultados e limites de cobertura:

| Entradas | Mediana anterior → corrigida | Alocações anteriores → corrigidas |
| --- | --- | --- |
| 5.000 | 48,4749 → 37,5246 ms | 23.833.224 → 20.814.824 bytes |
| 10.000 | 83,7683 → 78,9254 ms | 47.656.904 → 41.618.504 bytes |
| 20.000 | 160,8902 → 155,7602 ms | 94.110.160 → 82.031.760 bytes |
| 50.000 | 316,2532 → 224,3831 ms | 142.090.420 → 123.971.760 bytes |

Os 50 mil respeitam limite de 30 mil examinados; os demais continuam declarados
não examinados. Redução de alocação 12,7–12,8% não é redução de RAM residente de
todo o aplicativo. [Amostras, ambiente e método](benchmarks/WORK9-classificacao.json).

Baseline do portátil aprovado da Parte 1: ZIP **58.967.981 bytes**,
**151.285.646 bytes extraídos**, 274 arquivos, 268 DLLs. Maiores componentes:
Microsoft.Windows.SDK.NET.dll (26.341.408), System.Private.CoreLib.dll (16.033.576),
System.Windows.Forms.dll (13.715.240) e System.Private.Xml.dll (7.788.368).
O conjunto Windows inclui componentes de design/diagnóstico do runtime; não
foram removidos manualmente nem usados trimming/packer/single-file. O GF em si
ocupava 1.311.744 bytes de DLL. A adição de avisos aumenta ligeiramente o pacote;
não se promete redução de megabytes sacrificando o runtime completo.

CI baseline: regressão Windows **188 s**, publicação **78 s**, política **4 s**
(run 39980934418). Nesta revisão: regressão **188 s** (sem ganho observado), publicação **73 s** e
política **7 s**. São observações de runners, não benchmark causal; carga e caches
das VMs variam. A redução de duplicações diminuiu invocações de dotnet run de 16
para 12 no job principal, sem remover asserções, mas não foi traduzida em promessa
de aceleração. Não houve medição
física nova de startup, inventário/rede, RAM, CPU ou disco no PC FORTE/FRACO.
Não se atribui ganho a esses indicadores sem medir.

Comparação final do pacote: ZIP **58.986.440 bytes** (+18.459), extraído
**151.369.624 bytes** (+83.978), **281 arquivos** (+sete documentos/avisos).
A DLL própria caiu de 1.311.744 para **1.304.064 bytes** (−7.680) após retirar
código sem uso. Isso reduz IL distribuído; não prova menor RAM ou startup.
O aumento líquido do portátil é explicado pelos documentos/avisos adicionados,
sem trocar o runtime completo por dependência externa.

## Estado do produto e limites para distribuição

| Etapa | Implementação e evidência | Homologação |
| --- | --- | --- |
| Atualizações/U8 | Fontes consultivas, receitas restritas, assinatura/hash, arquitetura/escopo e pós-confirmação reforçados na Parte 1 | Instalação real supervisionada dos pilotos pendente; WinGet executor permanece desligado |
| Desinstalação/R7 | Identidade registrada, consentimento, MSI/EXE/MSIX consultivo, coexistência e cancelamento | Validação nos dois PCs pendente |
| Resíduos/R4 | Investigação geral somente leitura; pilotos específicos, Registro HKCU/HKLM 32/64 e exclusão de arquivo pelo handle validado | Homologação física pendente; alteração externa de Registro ainda não é transação atômica |
| Histórico de drivers | Consulta e correlação somente leitura preservadas | Mais amostras reais antes de ampliar cobertura |
| Gerenciamento de drivers | Não implementado nesta missão | Fontes oficiais, assinatura e rollback permanecem futuros |

Roadmap segue **27/42**, sem novo checkpoint funcional. Cobertura residual geral
é parcial por limites/profundidade/links/leitura. Configurações, histórico,
componentes compartilhados e locais desconhecidos não viram exclusão automática.
R4 zero específico não comprova ausência absoluta. Avaliação AVG/SmartScreen e
assinatura continuam pendentes; pacote é somente TESTE, não release estável.

A Parte 2 não redesenhou a UI. Regressões existentes preservam as proteções de
navegação tardia da Parte 1. Conteúdo escuro e layout em runners não comprovam
que todas as decorações nativas/barras/diálogos sejam escuros em Windows real;
revisão visual física permanece. Program.cs ainda concentra UI/orquestração e
AppServices; separação ampla foi evitada porque exigiria nova rodada funcional.
[Próxima bateria física mínima](TESTE-CONSOLIDADO.md).

## Checkpoint de retomada

Fonte/último commit validado: **dba59231b8a8d3d3c2d075a7b794c5817136cdc3**,
CI 39987151553 success, pacote .49 com digest e 280 hashes conferidos. O HEAD de
fechamento documental é registrado na PR #40; diff posterior restrito a
README/ROADMAP/PROGRESSO/este relatório. Main preservada em 410d65e2.

Concluído: preservação das correções da Parte 1, cinco commits temáticos,
consolidação de nove registros, leitores mortos removidos, pacote/licenças/links,
CI sem duplicações de cenários e validação x64 real no runner. Nenhuma correção
parcial de produto está apresentada como pronta. Refatoração ampla de Program,
fontes/downloads ao vivo nesta rodada, escuro nativo integral e medições físicas
continuam pendentes, explicitamente separados da aprovação técnica de TESTE.

Próxima ação exata: no workflow validar-candidata-windows, executar manualmente
verificar_fontes_reais e verificar_downloads sem executar instaladores; depois
seguir o roteiro de consultas comparáveis nos dois PCs e homologar U8/R7/R4 em
VM descartável antes de ampliar uso. Riscos para distribuição estável: Registro
externamente alterado sem transação atômica, cobertura geral parcial, falta de
homologação física e antivírus/assinatura. Não fazer merge nem release estável.
