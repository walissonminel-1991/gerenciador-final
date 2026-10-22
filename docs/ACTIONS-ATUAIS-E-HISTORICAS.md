# GitHub Actions — manutenção e arquivo histórico

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
