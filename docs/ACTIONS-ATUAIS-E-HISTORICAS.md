# GitHub Actions — manutenção e arquivo histórico

## Workflows ativos (somente três)

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

O teste `scripts/test_politicas_seguranca.py` passou a exigir o conjunto
*exato* dos três workflows ativos, verificando cada ação/permissão sem
relaxar a política de segurança. Isto impede novos arquivos de CI por
versão sem revisão explícita.

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
