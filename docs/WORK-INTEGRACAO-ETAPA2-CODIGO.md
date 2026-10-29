# Integração final — Etapa 2: código conciliado

27/10/2026, America/Fortaleza. Branch `work-integracao-final-20261027`,
[PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).
**Integração em fonte concluída; validação da árvore combinada pendente.**
Nenhuma build, suíte de produto, CI, distribuição ou teste físico nesta etapa.
Commit fonte: [`06d7dce0bb47944f1efd18a10785fa77f5f0c8f7`](https://github.com/walissonminel-1991/gerenciador-final/commit/06d7dce0bb47944f1efd18a10785fa77f5f0c8f7).
O fechamento posterior altera somente Markdown/JSON de continuidade; confira
o HEAD final da PR antes da próxima sessão.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) ·
[Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Reconciliação da Etapa 1](WORK-INTEGRACAO-ETAPA1-RECONCILIACAO.md) ·
[Recibo desta etapa](evidencias/integracao-etapa2-codigo.json) ·
[Adendo BETA LOCAL](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## Retomada e fontes

Entrada conferida: `275cfc396d22c8aff933a3879f0702d20596a2bc`.
Checkpoint/matriz/ordem de reconciliação/adendo lidos. PR #65 aberta/Draft,
base #60 inalterada. Main e PR #40 conferidas, sem alteração. Os HEADs das
fontes #60–#64 permanecem os mesmos da Etapa 1:

| Fonte | SHA | Uso nesta etapa |
| --- | --- | --- |
| #60 | `2bc9f4228fd448445fbf181652e5af93005def20` | Base comum da comparação |
| #64 | `357877e9acd628f84e5d9944e02e471fe319d1bc` | Código pré-BETA herdado e preservado |
| #62 acumulada | `e530d724b7322963c8795b005f13ae6948a73e43` | Delta BETA .66 incorporado |
| #61 | `f64600748b45b0ddc7e39c530ac56809c26f7ea8` | Já contida na #62; não reimportada |
| #63 | `04f59750739c6bdbaeb0bacd86ed2b734fba1271` | Revisão posterior já presente na #64; não reimportada |

A missão pré-BETA 0–7 não foi repetida. Nenhum merge de PR ou commit com
dois pais foi criado; a integração é um commit de conteúdo na branch nova.
As branches originais e seus históricos permanecem preservados.

## Composição aplicada

Dos 62 caminhos alterados pela BETA acumulada contra a base comum, 58 eram
exclusivos de seu delta e quatro compartilhados com a pré-BETA. Foram
incorporados os **58 caminhos exclusivos**, sendo **57 blobs idênticos à
#62** e **um workflow adaptado para preservar um gate obrigatório anterior**.
Os quatro compartilhados receberam a reconciliação descrita abaixo.
O recibo contém caminhos, blobs de origem/resultantes e as verificações.

| Área | Resultado em fonte |
| --- | --- |
| Consulta, identidade, arquitetura e cache | Conservados os arquivos/correções #64 e a ligação de identidade em `Program.cs` |
| Chrome Stable | Fonte, parser revisado, catálogo e testes #64 preservados, sem importar novamente #63 |
| Desinstalação/resíduos | Conservados Pending, bloqueio de repetição/limpeza sem conclusão, resumo e fronteiras de dados #64 |
| Drivers | Consulta e histórico #64 preservados; nenhuma instalação/remoção/reversão disponibilizada |
| Consentimento e diagnóstico privado | Incorporados aviso/política, revogação, coordenação, transporte e retenção #62; configuração operacional continua bloqueada |
| Encerramento/bandeja | Incorporados coordenador, Shutdown, cancelamento e recuperação #62 junto às guardas #64 |
| Interface | Preservada remoção dos seletores PC FORTE/PC FRACO e do botão permanente de coleta da BETA; privacidade pela bandeja |
| Projeto/pacote | Projeto e manifesto .66, GF_BETA, avisos, licenças, contratos e verificadores #62 conservados |
| CI | Mantidos testes dos dois lados e separação de integridade externa; gate obrigatório de drivers preservado conforme seção própria |

Esta composição ainda não foi compilada nem homologada. A versão de fonte é
**5.13.11.66**, com perfil BETA interna bloqueada; não é o pacote BETA LOCAL
da futura Etapa 8. Os requisitos desse perfil local continuam no adendo.

## Arquivos compartilhados conciliados

### Program.cs

Conferidos os deltas contra ambos os lados após a conciliação de três fontes.
Não houve conflito textual. Permanecem na mesma unidade:

- Propriedades protegidas de `ProgramEntry`, captura de arquitetura local,
  adaptador `InstalledUpdateIdentity.Create` e contagem de falha parcial #64.
- Resumo por `UninstallResultSummary.Status`, sem regressão dos executores,
  estados pendentes ou proteções de resíduos da #64.
- `BetaCloseCoordinator`, bloqueio de novas operações durante fechamento,
  tratamento de Shutdown e persistência de melhor esforço em erro #62.
- Condições GF_BETA da interface e caminho de envio condicionado a
  `BetaTelemetryUploader.IsConfigured`; a configuração de envio continua vazia.

Conciliação textual não comprova comportamento conjunto. Cancelamento,
Shutdown, pendências e privacidade devem ser exercitados na Etapa 3.

### TestesLayout/Program.cs

Conservados `--beta-close` e os testes BETA de aviso/rolagem/perfis, junto
à chamada de `Stage3UpdateQueryTests` em `--work18` e ao modo `--etapa3`.
Nenhuma asserção de qualquer lado foi removida para aprovar a composição.
Esses testes foram integrados ao despachante, mas não executados nesta sessão.

### docs/PROGRESSO.json

Conservadas as sete chaves alteradas pela pré-BETA e os objetos `candidate`
e `beta` da #62, sem alteração semântica dos valores de origem:

`sections`, `latest_real_test`, `latest_real_test_pc_fraco`,
`latest_real_test_pc_forte`, `historical_real_tests_before_pre_beta_etapa6`,
`pre_beta_physical_evidence`, `pre_beta_final_assessment`.

Todos os demais campos históricos da pré-BETA também foram preservados,
exceto `candidate`/`beta`, cuja origem é a BETA .66 conforme a decisão.
Os 42 pares ID/done são idênticos entre fontes e resultado: **27/42**.
Registros físicos antigos, evidências parciais e avaliação pré-BETA continuam.

O objeto novo `integration_final` identifica esta composição, as fontes,
o commit de código registrado no fechamento, estado pendente, CI não
executada e ausência de artefato/liberação. Os campos de código/CI/contagens
herdados em `candidate` continuam históricos da WORK18 e são identificados
explicitamente como tais. Nenhuma aprovação antiga foi atribuída à integração.

### docs/ROADMAP.md

Conservados os registros BETA .66 e pré-BETA, além das evidências e todos
os estados de checkpoints. Acrescentado aviso atual da Etapa 2, apontando
este relatório e a PR #65, com validação pendente e parada antes da Etapa 3.
Nenhum checkpoint foi encerrado ou aberto artificialmente.

## Ajuste necessário nos workflows

A importação literal da #62 retiraria `--official-live` do job obrigatório
`drivers-leitura`, nos runners Windows 2022 e 2025. A consulta oficial ficaria
apenas no workflow externo opcional. Foi restaurado **o comando da fonte
pré-BETA** no workflow conciliado, sem executar o job:

`dotnet run --project TestesDrivers/TestesDrivers.csproj -c Release -- --official-live`

Permanecem `Tee-Object`, verificação de código de saída, evidência do runner,
matriz 2022/2025, limites e permissões originais. Não foi criado executor de
drivers nem autorizado download/instalação. O despachante de testes foi
somente lido para conferir o significado dos argumentos, sem executar APIs.

Comparação estrutural dos comandos anteriores: **42 de 43 blocos idênticos**;
o restante é a consulta opcional que passa a usar `--live --official-live`,
preservando `--live` e acrescentando a consulta oficial. Essa comparação mede
comandos preservados, não testes aprovados. Gates BETA de consentimento,
endpoint, retenção, empacotamento e bloqueio de upload permanecem da #62.
Nenhum workflow foi executado ou enfraquecido nesta etapa.

## Configuração e proteções

`BetaTelemetryPolicy.props` continua com allowlist vazia e hash revisado
vazio. Endpoint, contato e hash de aprovações permanecem vazios; aprovações
de privacidade/homologação/distribuição continuam falsas. O uploader retorna
sem iniciar transporte se não houver endpoint autorizado. Essas condições
foram conferidas em fonte; provas negativas de execução ficam para a Etapa 3.
O código do piloto privado foi preservado para revisão futura, sem ativá-lo.

`PILOT_ENABLED=no` e `GITHUB_EXPORT_ENABLED=no` permanecem no exemplo
versionado. Worker/D1/DNS/Secrets e estado operacional remoto não foram
consultados nem alterados. Nenhuma migração, script conectado ou manutenção
remota foi executada.

Todos os arquivos não envolvidos no delta/continuidade permanecem no mesmo
blob da entrada, incluindo a revisão Chrome, proteção/exclusão permanente,
políticas de execução, fronteiras de dados e gestão de drivers desativada.
VLC continua somente inventário, sem ações, novos testes específicos,
consultas ou verificações. WinGet COM continua opcional, não bloqueante e
consultivo, sem reinvestigação. AVG e demais proteções não foram alterados.

## Verificações preliminares desta etapa

Foram feitas conferências estáticas/de integridade para salvar uma composição
rastreável. Elas não são a regressão/CI da Etapa 3:

| Conferência | Resultado | Limite |
| --- | --- | --- |
| Cópias das fontes | 70/70 conferidas pelo SHA Git | Não executa código |
| Importação de exclusivos | 57 blobs iguais à #62; um workflow com adaptação delimitada | Conferência de composição |
| Código e layout compartilhados | Deltas de ambos os lados preservados; sem marcadores de conflito | Não comprova compilação/interação |
| Projetos | 105 referências explícitas resolvidas; 92 fontes C# de testes cobertas pelas exclusões do produto | Conferência XML/caminhos, sem MSBuild |
| Versão | Projeto/manifesto .66 e GF_BETA coerentes; produto sem GF_PROGRESSIVE | Sem gerar assembly |
| JSON/histórico | Sete chaves pré-BETA, candidate/beta e 42 estados preservados | Roadmap 27/42; sem homologação nova |
| CI | Comandos/gates preservados conforme seção anterior | Zero run novo; nenhuma aprovação da integração |
| Privacidade | Configuração vazia, aprovações falsas, flags no | Estado remoto não inspecionado |

A árvore do commit fonte foi relida no GitHub: **433 blobs conferidos**,
**367 blobs herdados fora do escopo intactos**, 66 caminhos esperados e sete
refs originais preservadas. Um único pai; nenhuma CI ou artefato criado.
O recibo posterior registra o SHA fonte em cinco Markdown/JSON sem alterar
código/testes/workflows. O HEAD documental não é build aprovada; a CI futura
deve identificar seu SHA efetivamente executado. Commits usam `[skip ci]`
para respeitar a parada antes da validação final.

## Problemas conhecidos e próxima sessão

- Compilação, regressões Windows, testes sintéticos de interação, desempenho,
  integridade de pacote e CI da própria composição ainda não executados.
- Aprovações das PRs individuais não aprovam esta árvore. A falha externa
  histórica da #62 continua histórica, sem reexecução ou reinvestigação.
- AVG, testes físicos vinculados ao pacote e homologação conectada permanecem
  pendentes. Não há pacote novo ou assinatura/homologação declarada.
- A BETA LOCAL restrita ainda depende das Etapas 4–8 e do contrato de
  distribuição. Coleta/transmissão remotas, consultas explícitas e independência
  da infraestrutura deverão ser comprovadas nesse perfil antes de entrega.

**Encerrar após a Etapa 2. Próxima sessão: somente Etapa 3 — regressões e CI
da árvore integrada, pelo SHA exato.** Nenhum merge, release, deploy,
distribuição, operação em PC pessoal, instalação de driver ou envio realizado.
