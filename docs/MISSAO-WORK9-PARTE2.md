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
| Portátil sem licença GF, avisos de runtime, privacidade e guia (média) | ZIP anterior tem 274 arquivos; o verificador novo reprova sete ausências obrigatórias | Publish inclui sete documentos/avisos, três copiados dos próprios packs resolvidos. Cross-publish local passou 281 arquivos/280 hashes; validação Windows desta candidata ainda será registrada abaixo. |
| Conferência do pacote limitada a hashes gerados no mesmo job (média) | Não havia teste independente da cobertura exata, composição e origem do portátil | Verificador de leitura sem execução exige SHA de fonte/base/merge, versão, PE AMD64, self-contained, licenças e cobertura exata do manifesto. Dez testes adversariais incluem corrupção, omissão/extra, traversal, duplicata, certificado acidental, arquitetura e origem erradas. |
| README e organização davam instruções antigas (média) | Apontavam .42/.35 e afirmavam exclusão integralmente indisponível, embora existissem pilotos restritos posteriores | Estado corrente, pesquisa geral somente leitura, pilotos e homologação pendente separados; roteiro único para o próximo teste. Privacidade consolidada sem mudar consentimento/armazenamento. |
| Leitores antigos mantinham caminhos paralelos (baixa/manutenção) | IntegratedSystemReader.Processes/StartupEntries/WindowsUpdate sem chamadas diretas ou por reflexão; somente Computer é usado por DetailedHardware | Retiradas 168 linhas, interop de serviço e imports exclusivos; arquivo 257→89 linhas. Compilação com analisadores sem avisos/erros. Leituras atuais permanecem em RAM/StartupManager/WindowsUpdateClient. |
| Repetição de cenários na CI (custo/manutenção) | WORK9 rodava em três modos focados e novamente nas suítes; prévia WORK6 duplicada; testes Node repetidos no Windows e política Ubuntu | Suítes completas continuam obrigatórias, mutações preservadas, benchmark reutiliza build. Modos focados permanecem disponíveis. Concurrency de reutilizáveis inclui nome do chamador para não cancelar uma execução manual independente. |

Commits da implementação: `68ba7824` (leitores), `6df8479e` (pacote, versão e
verificador), `2458e5c0` (CI). Commits de consolidação e fechamento serão
registrados após a revisão final. A candidata ainda não está aprovada nesta
versão inicial do relatório; resultado efetivo da CI abaixo prevalece.

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

Contagem e tamanho finais serão fechados no checkpoint após a CI.

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
cross-publish win-x64 self-contained, 28 testes Python aprovados (18 políticas +
10 de pacote), links relativos sem alvos ausentes, identidade estrutural do JSON
de benchmark e reprodução negativa no ZIP anterior. Cross-publish foi uma
árvore de trabalho editada e não é artefato distribuído nem teste Windows nativo.

**CI da .49: pendente nesta gravação inicial.** Só fornecer o link de pacote após
regressões, política, publish e conferência independente do ZIP final aprovados.
Jobs externos ignorados não serão apresentados como fontes ao vivo aprovadas.

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
(run 39980934418). Novos tempos serão registrados como observação de runners,
não benchmark causal: carga e caches das VMs podem variar. Não houve medição
física nova de startup, inventário/rede, RAM, CPU ou disco no PC FORTE/FRACO.
Não se atribui ganho a esses indicadores sem medir.

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

Implementação e limpeza locais prontas para revisão; CI e ZIP final ainda a
conferir. Próxima ação: publicar os commits na branch experimental, aguardar as
três validações obrigatórias e registrar o SHA final/run/artefato/digest aqui e
na PR. Se falhar, não apresentar a candidata como aprovada. Sem operações nos
PCs pessoais e sem mudança na main.
