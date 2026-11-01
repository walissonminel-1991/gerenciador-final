# GERENCIADOR FINAL — decisão de canal único (01/11/2026)

## Decisão vigente

**Não haverá distribuição BETA nem programa de avaliadores externos.** A preparação histórica da v5.13.11.66 e seus artefatos de laboratório **não autorizam distribuição, publicação, convite a participantes, transmissão de diagnósticos ou ativação de autoatualização**. As verificações continuarão em ambiente controlado até aprovação expressa de uma distribuição oficial.

O objetivo é manter **um único produto GERENCIADOR FINAL**, gratuito, portátil e seguro, sem introduzir uma linha paralela de releases que interfira na validação do autoatualizador. **Não existe homologação física ou aprovação de lançamento decorrente desta decisão**. O roadmap permanece **27/42**.

## Alcance da decisão

- **Apresentação e planejamento novos:** evitar tratar BETA como canal vigente, promessa de entrega, versão pública ou linha técnica concorrente. Empregar *testes locais*, *laboratório* e *futura distribuição oficial* quando aplicável.
- **Referências históricas:** manter nomes de arquivos, hashes, commits, relatórios, links, avisos de privacidade versionados e evidências de execuções antigas. Alterar esses registros falsificaria ou enfraqueceria a rastreabilidade.
- **Contratos técnicos ativos:** manter intactos, nesta fase, `GF_BETA`, `GF_BETA_LOCAL`, `GfBetaProfile`, `GfBuildChannel`, classes `Beta*`, `TelemetryConsent.RemoteBeta`, os gates de endpoint e consentimento, os testes de controle negativo, workflows e nomes de artefatos vinculados aos verificadores. **Eles não são autorização para lançar uma BETA; são identificadores que hoje sustentam restrições de segurança e compatibilidade.**
- **Repositórios auxiliares privados:** `walissonminel-1991/gerenciador-final-beta-agregados` e `walissonminel-1991/gf-beta-entregas` permanecem como registros históricos, sem ativação ou renomeação nesta etapa. O workflow de entrega privada exige o nome exato do repositório; mudar o nome sem migrar o contrato de identidade poderia invalidar verificações. Nenhum dado ou artefato deve ser publicado em função desta decisão.
- **Infraestrutura:** não implantar nem ligar Worker/D1, DNS, exportação GitHub, envio remoto ou configurações `PILOT_ENABLED` e `GITHUB_EXPORT_ENABLED`; preservar os bloqueios existentes.

## Segurança e autoatualização

O perfil compilado local atual bloqueia consultas automáticas de versões e diagnóstico colaborativo. **Retirar a palavra BETA da documentação não altera esse comportamento.** Não eliminar defines ou mudar a seleção de perfis para “habilitar atualização”: somente um fluxo de autoatualização real, autenticado, seguro e comprovado em Windows descartável poderá ser avaliado separadamente. Até lá, a aprovação de CI isolada não constitui homologação operacional.

Uma migração futura dos nomes internos, se útil, requer inventário completo de consumidores, compatibilidade de metadados/persistência, testes negativos e CI Windows global verde **sem reduzir os guardrails**. Renomear repositórios exigiria plano próprio para URLs, verificações de identidade, permissões e integrações; não é uma substituição textual.

## Delimitação desta mudança

Atualiza apenas o estado editorial do projeto e registra esta decisão. **Não altera C#, MSBuild, scripts, testes, workflows, configuração, estrutura de repositórios, dependências, versões, telemetria, arquivos de evidência ou protótipos.** Permanecem preservadas `main`, PRs #65/#75/#76, G5, os testes em andamento e o roadmap 27/42.

A preparação histórica de 26–28/10 continua consultável nas documentações existentes, mas não representa a orientação vigente de distribuição.
