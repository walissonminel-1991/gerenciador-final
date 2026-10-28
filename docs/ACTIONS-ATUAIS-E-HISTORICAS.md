# GitHub Actions — manutenção e arquivo histórico

## Situação da candidata PR #65 — 27/10/2026

Branch `work-integracao-final-20261027`; [checkpoint](WORK-INTEGRACAO-CHECKPOINT.md),
[organização 5](WORK-INTEGRACAO-ETAPA5-ORGANIZACAO.md) e [validação 3](WORK-INTEGRACAO-ETAPA3-VALIDACAO.md).
Há **quatro arquivos** versionados e aprovados na lista explícita do guardrail:

| Arquivo | Gatilho/papel na fonte integrada |
| --- | --- |
| `build-windows.yml` | PR/push main/dispatch; chama regressões e política, e build depende de ambas |
| `validar-candidata-windows.yml` | workflow_call/dispatch; regressões Windows, consentimento e drivers somente leitura 2022/2025 |
| `triagem-diagnostico-github.yml` | workflow_call/dispatch; guardrails e Node/SQLite sintéticos, sem processar Issues automaticamente |
| `integridade-externa.yml` | PR/dispatch separado; integridade sem instalação e consultas adicionais condicionais |

Dois chamadores produzem duas CIs de PR; não esperar quatro runs independentes.
Fonte `6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8`: principal
[40604593820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593820)
e externa [40604593709](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593709)
aprovadas, tentativa 1; sete jobs obrigatórios success, dois condicionais skipped.
Fonte/merge sintético têm a mesma árvore, como documentado no recibo da Etapa 3.

No perfil GF_BETA, build interna mantém apenas manifestos/evidências no upload;
o caminho de pacote executável exige não BETA. Atestação continua condicionada
à main/não BETA e não é Authenticode ou aprovação AVG. Actions fixadas por SHA,
checkout sem credenciais persistentes, permissões e retenção de sete dias
foram preservados. Não há mudança de workflow/teste ou nova CI nesta Etapa 5.

A main consultada no SHA `410d65e23546976db456337c71e4705f10217d36` tem dois
workflows; seus arquivos e settings não foram alterados. Esta é conferência
da fonte, não afirmação de enabled/disabled nas configurações do servidor.

## Como interpretar o arquivo histórico abaixo

Os registros WORK9 e 21–22/10 descrevem suas revisões, inclusive a lista então
composta por três workflows na PR #40 e dois na main. Não são a lista atual
da candidata #65. A regra vigente continua lista explícita/revisão de finalidade;
não criar workflows por versão nem remover testes para simplificar CI. As
remoções históricas não foram repetidas. Nenhum run/artefato foi apagado.

## Consolidação WORK9 (.49)

O build depende das regressões e da política da mesma revisão. O SDK vem de global.json, Actions continuam fixadas por SHA e NuGetAudit permanece obrigatório. Os modos focados WORK9/WORK6 já fazem parte das suítes completas e não rodam duplicados; testes Node ficam na política Ubuntu. O benchmark reutiliza o build. Reutilizáveis incluem o chamador no grupo de concorrência para separar dispatch de chamadas por outro workflow.

Antes do upload, conferir composição, versão, SHAs fonte/base/merge, AMD64, runtime self-contained, licenças e cobertura exata de hashes. Atestação continua somente na main. O nome do pacote identifica a fonte; a base e o merge sintético ficam no BUILD-INFO. Retenção de sete dias. [Resultados e tempos reais](MISSAO-WORK9-PARTE2.md).

Os registros seguintes preservam as decisões das revisões anteriores.


## Workflows ativos na branch experimental do PR #40 (três)

| Arquivo | Finalidade |
| --- | --- |
| `build-windows.yml` | Publicar portátil Windows x64, verificar manifesto, hashes e criar artefato de teste |
| `validar-candidata-windows.yml` | Compilar e executar regressões reais/sintéticas Windows, UI, segurança, inventário, drivers e resíduos |
| `triagem-diagnostico-github.yml` | Conferir políticas estáticas de privacidade/segurança e simular receptor isolado |

Estes workflows são necessários e continuam com permissões mínimas de PR,
checkout sem credenciais persistentes e atestação condicionada à main.

## Remoção dos workflows antigos (21/10/2026)

Havia **38 arquivos `compilar-gerenciador-v*.yml`**, versões acumuladas que
apontavam a builds anteriores e eram mantidas apenas para reprodução
histórica. Outros dois arquivos, `consolidacao.yml` e
`validar-monitoramento-leve.yml`, eram voltados a branches antigas.
Esses **40 workflows históricos** foram removidos da árvore de
desenvolvimento; suas implementações, versões anteriores e commits continuam
acessíveis pelo histórico Git.

Isto não remove registros de execuções anteriores no GitHub Actions
nem arquivos ZIP de artefatos armazenados no serviço, que têm retenção
específica. Remover arquivos versionados também não apaga objetos Git
históricos nem diminui automaticamente o armazenamento GitHub do repositório.

O teste `scripts/test_politicas_seguranca.py` exige a lista explícita dos três workflows ativos, incluindo `.yml` e `.yaml`. Um teste com arquivos sintéticos comprova que um fluxo novo é recusado sem revisão da lista e pode ser aceito após aprovação explícita nela. Os controles de ações, permissões e credenciais continuam aplicáveis. Novos testes legítimos podem usar os fluxos atuais ou propor uma mudança revisada dessa lista.

## Política daqui para frente

- Não criar `compilar-gerenciador-vX.Y.Z.yml` a cada compilação.
  O mesmo workflow ativo produz pacotes identificados por versão/commit.
- Não criar novas páginas `LEIA-PRIMEIRO-vX.Y.Z.md` na raiz. Atualizar
  o README e o checklist corrente.
- Manter resultados agregados (sem dados pessoais) e notas históricas
  necessárias dentro de `docs/`, com índice em
  [Organização](ORGANIZACAO-REPOSITORIO.md).
- Em cada mudança de CI, verificar as três Actions da mesma revisão.
  Uma execução antiga não valida a versão nova.

**PR #40 ainda é Draft**, sem merge na main nem publicação de release.

## Revisão da faxina (.25)

O workflow Windows atual recuperou dois caminhos opcionais de `workflow_dispatch`:
`verificar_fontes_reais` e `verificar_downloads`. Usam consultas/downloads,
sem instalar ou executar os pacotes baixados. Os jobs falham se a verificação
falhar; uma execução de PR com esses jobs ignorados não comprova fontes ao vivo.
Scripts de distribuição têm `--print-version`, validado no Windows sem publicar.
`BUILD-INFO.txt` distingue SHA fonte, base e SHA do merge sintético testado.

Em **22/10/2026**, o [PR #41](https://github.com/walissonminel-1991/gerenciador-final/pull/41)
integrou na `main` exclusivamente as 50 exclusões históricas: 40 workflows
obsoletos e 10 guias versionados. [Compilação Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39768905807)
e [regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39768905843)
aprovadas na revisão da limpeza. A `main` possui **dois** workflows atuais
(`build-windows.yml` e `validar-candidata-windows.yml`); o terceiro,
`triagem-diagnostico-github.yml`, permanece somente na branch experimental.
O PR #40 continua Draft, sem merge do código .35. Não houve remoção de runs,
artefatos antigos, tags, releases ou branches. [Diagnóstico consolidado](OPERACAO-FAXINA-FINAL.md).
