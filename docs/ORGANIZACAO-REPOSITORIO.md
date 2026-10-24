# Organização do repositório — GERENCIADOR FINAL

## Estado e entradas

[README](../README.md), [roadmap](ROADMAP.md) e [PROGRESSO.json](PROGRESSO.json)
descrevem o produto corrente. O [roteiro físico](TESTE-CONSOLIDADO.md) contém o
próximo teste; [TESTES](TESTES.md) contém os comandos automatizados. Resultados e
limites da missão estão em [WORK9 parte 1](MISSAO-WORK9-PARTE1.md) e
[parte 2](MISSAO-WORK9-PARTE2.md). PR #40 permanece Draft; main não foi alterada.

A pesquisa geral de resíduos é parcial e somente leitura. Os pilotos específicos
de limpeza não homologam R4. Atualizações/desinstalações reais, MSIX no PC FRACO
e AVG continuam exigindo validação própria. Roadmap preservado em 27/42.

## Inventário e referências

| Categoria | Uso efetivo e decisão |
| --- | --- |
| C# na raiz | Produto, incluído pelo curinga do SDK. Parciais de MainForm, modelos, serviços, verificadores e controles; não inferir código morto pelo nome do arquivo. |
| Três Catalogo*.json | Recursos incorporados com nomes lógicos explícitos: identidade, desinstalação e auditoria histórica. Catálogos têm responsabilidades diferentes. |
| Assets/ | Ícone usado pelo projeto/instalador e prévia referenciada no README. Únicos binários versionados; nenhum EXE/DLL/ZIP/log de build rastreado. |
| Testes*/ | 12 projetos de console, fontes vinculadas e ProjectReference no layout. TestesManifesto contém PowerShell; TestesRede tem caminho opcional. GF_PROGRESSIVE habilita hooks somente na montagem sintética. |
| scripts/ | Guardrails Python, mutações em cópias descartáveis e conferência do portátil. Não são dependências do aplicativo distribuído. |
| prototipos/telemetria-worker/ | Receptor desligado e testes Node/SQL/configuração de laboratório; não é serviço em produção. A CI referencia esses arquivos. |
| Scripts .bat/.cmd/.iss | Compilação de desenvolvimento, ZIP portátil, instalador opcional e assinatura local são finalidades distintas; preservados. Versões derivadas do projeto/executável. |
| .github/workflows/ | Três fluxos: regressões, política e build condicionado a ambos. Actions fixadas por SHA, checkout sem credenciais persistentes, permissões mínimas e retenção de sete dias. |
| Documentação vigente | README, LICENSE, SECURITY, PRIVACY, CONTRIBUTING, PUBLICACAO, USO-PORTATIL, TESTES, roadmap/progresso e avisos de terceiros. |
| Evidências históricas | Missões, decisões de segurança, benchmarks, testes reais agregados e [arquivo PC FRACO](TESTES-HISTORICOS-PC-FRACO.md); mantidos para auditoria. |

A auditoria conferiu inclusões Compile/Remove/EmbeddedResource, símbolos de
compilação, chamadas por reflexão no layout, recursos carregados por nome lógico,
referências dos workflows/scripts e links relativos. Não há PackageReference
comercial: os componentes resolvidos são os packs do SDK/runtime Microsoft.
A [atribuição de terceiros](THIRD-PARTY-NOTICES.md) distingue seus termos da MIT do GF.

## Faxina e preservação

A WORK9 consolidou nove relatórios/planos antigos do PC FRACO em um arquivo com
âncoras, conteúdo preservado, hash original e link ao checkpoint anterior.
Atualizou as referências e separou roteiros históricos do vigente. Os números e
commits estão no relatório da Parte 2. Nenhum histórico Git foi reescrito.

A remoção anterior de 40 workflows e dez guias v5.12 permanece registrada em
[Actions](ACTIONS-ATUAIS-E-HISTORICAS.md). Não foi repetida nem contabilizada como
trabalho desta missão. Runs, artefatos remotos, branches, tags e releases foram
preservados; remover arquivos da árvore não reduz objetos Git históricos.

Saídas bin/obj, Distribuicao, capturas, caches, logs e ZIPs locais ficam ignorados.
Não criar workflow nem LEIA-PRIMEIRO por versão. Novos fluxos precisam entrar na
lista explícita do guardrail após revisão. Evidências devem ser agregadas e sem
dados pessoais; preservar decisões únicas antes de consolidar documentos.
