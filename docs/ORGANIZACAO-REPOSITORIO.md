# Organização do repositório — GERENCIADOR FINAL

Este documento diferencia **arquivos ativos**, **evidências históricas** e
**artefatos temporários**, para manter o projeto fácil de entender e revisar.

## Ponto de entrada

- [README principal](../README.md) — propósito, limitações, compilação e versão
  experimental vigente
- [ROADMAP](ROADMAP.md) — checkpoints funcionais; progresso 26/42
- [PROGRESSO.json](PROGRESSO.json) — estado dos 42 checkpoints em formato legível
  por ferramentas; R4 continua suspenso, sem remoção de resíduos
- [CHANGELOG](../CHANGELOG.md) — histórico de alterações por versão
- [TESTES](TESTES.md) — procedimento geral, segurança e validações
- [Revisão da faxina e roteiro vigente](OPERACAO-FAXINA-FINAL.md) — candidata .25; último teste real .24

## Código e testes

- Código do produto: arquivos `.cs` na raiz e catálogos JSON
- Projetos de regressão: `TestesMotor/`, `TestesCatalogo/`,
  `TestesMultifontes/`, `TestesMsix/`, `TestesAtualizador/`,
  `TestesResiduos/`, `TestesLayout/`, `TestesDrivers/`,
  `TestesFechamento/`, `TestesTelemetria/` e `TestesSeguranca/`
- `scripts/test_politicas_seguranca.py` — políticas de proteção em CI
- `prototipos/telemetria-worker/` — simulação/testes isolados, sem envio
  de diagnósticos em produção

## Somente três workflows CI ativos

1. `.github/workflows/build-windows.yml`: publicação portátil e integridade
2. `.github/workflows/validar-candidata-windows.yml`: regressões Windows
3. `.github/workflows/triagem-diagnostico-github.yml`: privacidade e segurança

Não criar workflows adicionais por versão. Nomes de pacotes são derivados do
projeto e do commit. Consulte [Actions](ACTIONS-ATUAIS-E-HISTORICAS.md).

## Evidências históricas

Estudos de segurança, missões Work 1–3, benchmarks e relatórios agregados
anteriores permanecem em `docs/` para auditoria, sem necessidade de mover
cada versão para uma pasta própria.

Os **38 workflows antigos de compilação**, **dois workflows de branch
descontinuada** e **dez guias LEIA-PRIMEIRO v5.12** foram retirados
da árvore atual. Seu conteúdo pode ser recuperado pelos commits anteriores
no GitHub, sem reescrever o histórico. A exclusão dos arquivos da árvore
ativa **não** apaga automaticamente objetos Git, runs, logs ou ZIPs hospedados
pelo GitHub Actions. Artefatos de teste têm retenção curta no workflow.

## Regras para não acumular novamente

- Apenas atualizar README, CHANGELOG, ROADMAP e PROGRESSO com informação
  vigente; evitar repetir cronologias completas em cada um.
- Registrar fatos importantes por bateria em documentos agregados na pasta
  `docs/`; não publicar logs brutos, nomes de perfis, caminhos privados,
  capturas com dados pessoais ou identificadores de hardware.
- Não adicionar LEIA-PRIMEIRO versionado nem workflow de compilação
  versionado na raiz. O guardrail de CI verifica essas condições, inclui extensões `.yml` e `.yaml` e permite novos fluxos apenas mediante revisão da lista explícita.
- Saídas de compilação, `LayoutCaptures/`, caches e logs permanecem
  ignorados pelo Git. Distribuir binários exclusivamente como artefatos
  de teste temporários ou Releases aprovadas.
- Para mudar/remover dependências, componentes, contratos ou testes,
  exigir revisão técnica; limpeza de repositório não autoriza
  modificar o Windows, apagar dados locais ou abrir funções destrutivas.

## Situação atual de teste

A v5.13.11.24 demonstrou que o MSIX pode falhar com
`COMException(0x80040154)` mesmo após o retry e reduzir
temporariamente a cobertura de 74 para 55 entradas no PC fraco.
Os aplicativos ausentes da lista **não** foram comprovadamente removidos.
A investigação de resíduos permanece parcial/somente leitura.
PR #40 continua Draft; `main` não deve receber código experimental
sem revisão e testes supervisionados.
