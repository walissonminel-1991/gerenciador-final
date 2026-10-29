# Integração final — Etapa 1: reconciliação das fontes

27/10/2026, America/Sao_Paulo. **Análise concluída; integração de código não iniciada.**
Repositório: `walissonminel-1991/gerenciador-final`.

Continuidade: [checkpoint](WORK-INTEGRACAO-CHECKPOINT.md),
[matriz](WORK-INTEGRACAO-MATRIZ.md) e
[recibo estruturado](evidencias/integracao-etapa1-reconciliacao.json).
O [adendo das Etapas 5–9](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md) integra o escopo futuro.

## Escopo e fontes fixadas

Esta missão sucede a pré-BETA 0–7, já encerrada. Foram lidos seus
[checkpoint](WORK-PRE-BETA-CHECKPOINT.md), [matriz](WORK-PRE-BETA-MATRIZ.md)
e [decisão da Etapa 7](WORK-PRE-BETA-ETAPA7-DECISAO.md), sem repetir seus testes
ou investigação. Nesta sessão são permitidas análise, branch isolada e PR Draft;
a combinação de código fica exclusivamente para a Etapa 2.

| Fonte | Branch | SHA conferido | Papel |
| --- | --- | --- | --- |
| #60 | `work18-ampliacao-atualizacoes-seguras` | `2bc9f4228fd448445fbf181652e5af93005def20` | Base comum .65 |
| #64 | `work-pre-beta-etapa0-20261027` | `357877e9acd628f84e5d9944e02e471fe319d1bc` | Origem da branch nova; pré-BETA revisada |
| #62 | `work-beta-retencao-issues-20261026` | `e530d724b7322963c8795b005f13ae6948a73e43` | Delta acumulado BETA .66, incluindo #61 |
| #61 | `beta-piloto-consentimento-envio-20261026` | `f64600748b45b0ddc7e39c530ac56809c26f7ea8` | Ancestral da #62; não importar novamente |
| #63 | `experimento-chrome-versionhistory-20261027` | `04f59750739c6bdbaeb0bacd86ed2b734fba1271` | Experimento Chrome já incorporado e corrigido na #64 |

Todas estão abertas e Draft na fotografia consultada. A branch nova é
`work-integracao-final-20261027`, criada exatamente a partir da #64 acima.
A PR de integração tem como base `work18-ampliacao-atualizacoes-seguras`,
permitindo revisar futuramente o delta completo contra a #60. Por isso seu
diff já inclui mudanças herdadas da #64: elas não foram implementadas outra
vez nesta sessão. As únicas alterações próprias desta etapa são documentos.

Main permanece `410d65e23546976db456337c71e4705f10217d36`; PR #40 permanece
`ef13aef49083f357f6a14f4de46f8bc976dbd830`, Draft. Nenhuma branch original é alterada.

## Comparação contra a base comum

Comparações GitHub por SHA e árvores recursivas completas, sem truncamento:

| Frente | Commits à frente da #60 | Arquivos alterados | Produto/configuração | Testes | Documentação | Workflows | Scripts | Protótipos |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| #64 | 16 | 64 | 21 | 17 | 25 | 0 | 1 | 0 |
| #62, acumulada | 40 | 62 | 15 | 6 | 11 | 4 | 4 | 22 |
| #61 | 21 | 21 | 7 | 2 | 6 | 1 | 0 | 5 |
| #63 | 6 | 5 | 2 | 2 | 1 | 0 | 0 | 0 |

Há **quatro arquivos compartilhados** entre #64 e #62, **60 exclusivos da
#64**, **58 exclusivos da #62** e uma união de **122 caminhos alterados**.
Isso mede arquivos, não defeitos, aplicativos ou checkpoints concluídos.
O recibo lista todos os caminhos, estados e contagens para reprodução.

A Etapa 7 antiga analisava a entrada `d01e377` e registrava 62 arquivos na
frente pré-BETA. O HEAD final `357877e` modifica também os dois arquivos
documentais acrescentados no encerramento daquela avaliação. A contagem
atual correta é 64; a interseção continua com quatro arquivos.

### Ancestralidade e mudanças redundantes

- #61 → #62: `ahead`, 19 commits à frente, zero atrás; merge-base é o SHA
  da #61. O histórico da #61 está contido na #62. Seus 21 caminhos alterados
  também pertencem ao delta acumulado da #62. Importar ambas repetiria trabalho
  e poderia recolocar versões antigas de consentimento/encerramento.
- #63 → #64: `diverged`, 16 commits à frente e seis atrás, merge-base #60.
  Não existe ancestralidade direta. A incorporação foi seletiva, comprovada
  pelas diferenças de conteúdo e pelo relatório anterior; não se deve
  interpretar a ausência do commit #63 como ausência da funcionalidade.
- A base .65 já contém Lotes 1–4 e WORK14–WORK18. Reimportar essas cadeias não
  faz parte da integração atual.

## Funcionalidades exclusivas a conservar

| Origem | Funcionalidade | Arquivos representativos | Limite mantido |
| --- | --- | --- | --- |
| #64 | Identidade e arquitetura local para consultas; resultados parciais e cancelamento preservados | `InstalledUpdateIdentity.cs`, `UpdateResultHistory.cs`, `Program.cs` | Arquitetura desconhecida continua inconclusiva; consulta não autoriza instalar |
| #64 | Fonte oficial Chrome Windows Stable e parser revisado | `OfficialReleaseSources.cs`, `CatalogoIdentidades.json`, testes multifontes | x86/x64 somente consultivos; sem receita de atualização nova |
| #64 | Correlação de drivers, evidências divergentes e histórico parcial | `DriverInventory.cs`, `DriverOfficialLookup.cs`, `DriverPublishedPackages.cs`, `DriverWindowsHistory.cs` | Sem instalação, remoção ou reversão |
| #64 | Pendência segura após início incerto do desinstalador e resumo correto | `RegisteredUninstaller.cs`, `UninstallBatch.cs`, `UninstallOperationProgress.cs` | Bloqueio de repetição e de limpeza sem confirmação preservados |
| #64 | Exclusão permanente e fronteiras de resíduos | `ProtectedApplications.cs`, catálogos, políticas e leitores | VLC somente inventário; dados pessoais/compartilhados protegidos |
| #64 | Evidência física consolidada e encerramento da missão anterior | `docs/WORK-PRE-BETA-*`, `docs/PROGRESSO.json`, `docs/TESTES.md` | Histórico parcial, sem homologar a nova composição |
| #62, incluindo #61 | Consentimento versionado, revogação e coordenação entre processos | `AnonymousTelemetry.cs`, `TelemetryConsentPolicy.cs`, `TelemetryPrivacyDialog.cs` | Sem receptor aprovado ou envio habilitado |
| #62 | Encerramento, Shutdown e recuperação sem confirmação de envio indevida | `BetaCloseCoordinator.cs`, `Program.cs`, `WindowChrome.cs` | Falha fatal/Shutdown não inicia transmissão; regressão futura obrigatória |
| #62 | Transporte e políticas de origem/aviso | `BetaEndpointPolicy.cs`, `BetaTelemetryUploader.cs`, `BetaTelemetryTransport.cs`, `BetaTelemetryPolicy.props` | Allowlist e hash revisado vazios, aprovações falsas; não configurar |
| #62 | Retenção, reconciliação e migrações privadas | `prototipos/beta-telemetria/*` | Código preservado sem executar rede, D1, migração ou deploy |
| #62 | Build .66 e contratos de privacidade/pacote | Projeto, manifesto, workflows, `InstallerFileGuard.cs`, verificadores Python | Build interna bloqueada; nenhum upload de EXE/ZIP BETA liberado |

## Chrome: decisão sobre a #63

Todos os cinco caminhos do experimento estão na #64. O arquivo
`TestesMultifontes/ChromeVersionHistoryTests.cs` possui o mesmo blob nas duas
fontes: `418ea3c1bc3cfc1cbfa447697f12cc9f0f5bcaf9`.

Os quatro demais são revisões deliberadas: parser com recusa de propriedades
JSON duplicadas e versão ASCII estrita; catálogo com exclusão permanente;
despachante com testes focais/completos; documentação com resultados e limites.
A #64 também acrescenta `ChromeVersionHistoryReviewTests.cs` e, depois, a
captura de arquitetura local usada pela tela. O recibo contém os cinco pares
de blobs para conferir essa conclusão.

**Decisão:** conservar a versão da #64, sem cherry-pick/merge/importação da
#63 e sem repetir seus testes ou consultar a API nesta etapa. A evidência
anterior de 196 testes multifontes não homologa instalação do Chrome.

## Reconciliação conceitual dos arquivos compartilhados

| Arquivo | #64 | #62 acumulada | Decisão para a Etapa 2 |
| --- | --- | --- | --- |
| `Program.cs` | `ProgramEntry` protegido, arquitetura capturada, adaptador consultivo, falha parcial e resumo de desinstalação | Remoção dos perfis físicos, privacidade pela bandeja, GF_BETA, coordenação de fechamento, Shutdown e bloqueio de novas operações | Preservar os dois conjuntos por método/propriedade; nunca substituir o arquivo inteiro por um lado. Revisar ciclo de vida/cancelamento junto aos estados pendentes |
| `TestesLayout/Program.cs` | `--work18` chama também `Stage3UpdateQueryTests`; modo `--etapa3` | `--beta-close`, testes do aviso, remoção do seletor de PCs e botão permanente de coleta | União dos despachantes e asserções; manter os testes novos da #64 e as regras de UI da BETA. Não apagar asserções para aprovar |
| `docs/PROGRESSO.json` | Seções e sete chaves de histórico/avaliação, listadas abaixo | `candidate` e `beta` | Reconciliação por chave e ID; preservar histórico, bloqueios e todos os 42 estados. Fonte/CI da integração devem ter registro próprio |
| `docs/ROADMAP.md` | Evidências físicas e fechamento pré-BETA; 27/42 | Registro da BETA .66 ainda não homologada | Conservar ambos como histórico datado e acrescentar estado da integração sem anunciar liberação |

O ensaio textual da missão anterior identificou conflito em PROGRESSO e
aplicação limpa dos outros três arquivos. Essa evidência é histórica. Nesta
Etapa 1 não foi aplicado patch, realizado merge de teste ou montada árvore
combinada. Sobreposição de arquivo e conflito textual não comprovam nem
refutam compatibilidade de comportamento.

### PROGRESSO: regras exatas de preservação

Comparação semântica dos três JSONs fixados contra a #60:

| Origem | Chaves raiz alteradas |
| --- | --- |
| #64 | `sections`, `latest_real_test`, `latest_real_test_pc_fraco`, `latest_real_test_pc_forte`, `historical_real_tests_before_pre_beta_etapa6`, `pre_beta_physical_evidence`, `pre_beta_final_assessment` |
| #62 | `candidate`, `beta` |
| Interseção | Nenhuma |

A decisão final acrescentou `pre_beta_final_assessment`; são agora **sete**
chaves pré-BETA, enquanto a avaliação anterior listava seis. A integração
futura deve preservar também essa avaliação, sem atualizar o número antigo
por cópia cega.

Regras da reconciliação futura:

1. Preservar as sete chaves pré-BETA, inclusive todo o histórico físico e
   as evidências por ID em `sections`. Não eliminar os registros .52/.24 ao
   manter .64/.65 como testes mais recentes.
2. Preservar a configuração e o estado bloqueado de `candidate`/`beta` da
   #62; nenhum endpoint, aprovação, flag ou liberação passa a verdadeiro.
3. Os 42 pares ID/`done` são idênticos nas três fontes: 27 verdadeiros e
   15 falsos. Conferir por ID, sem recalcular conclusão a partir de contagens
   de testes, nem aceitar regressão silenciosa por posição de array.
4. O objeto `candidate` da #62 ainda conserva `code_commit`/`ci_url` da
   WORK18 .65. São referências históricas, não aprovação da integração .66.
   Antes de atualizar esses campos no momento correto, conservar o registro
   anterior em evidência histórica. Registrar origem, novo SHA, status
   pendente e CI própria da integração; não copiar `approved` da #64.
5. Manter separadas avaliação histórica pré-BETA, futura candidata local e
   homologação conectada. Sucesso local não ativa o piloto privado/público.

Nenhum dos JSONs originais foi alterado e nenhuma projeção foi aplicada.

## Ordem segura definida para a Etapa 2

1. Retomar somente a branch nova, ler checkpoint/matriz e comparar novamente
   os HEADs com esta fotografia. Preservar alterações posteriores; não usar força.
2. Usar a #64 fixada como árvore de origem, com exclusão permanente e revisão
   Chrome. O código dessa árvore está intacto ao encerrar a Etapa 1.
3. Examinar o delta **#60 → #62**, já acumulando #61. Incorporar seus 58
   caminhos exclusivos, após conferir cada caminho contra a fotografia.
   Arquivos exclusivos de um delta podem já existir na base; não confundir
   exclusividade da alteração com arquivo novo.
4. Conciliar manualmente os quatro caminhos compartilhados conforme as
   decisões acima. Conferir os contratos e todos os caminhos de fechamento,
   não apenas ausência de marcadores de conflito.
5. Conservar projeto/manifesto .66, exclusões de testes, versão dos avisos,
   política de build e verificadores de pacote. Não definir endpoint ou
   aprovação; não executar scripts de build conectado ou implantação.
6. Preservar o conjunto de gates dos workflows: a separação de integridade
   externa na #62 não autoriza omitir gate, aceitar falha ou recolocar o
   alvo excluído. Conservar drivers em runners descartáveis e os testes
   de identidade, payload, consentimento, concorrência e preservação.
7. Conferir que todos os caminhos de transmissão continuam indisponíveis
   na configuração integrada, inclusive fechamento/erro/Shutdown e eventual
   consentimento remoto antigo. Preservar `PILOT_ENABLED=no` e
   `GITHUB_EXPORT_ENABLED=no`; não consultar estado operacional remoto.
8. Salvar os commits da integração e atualizar os dois documentos. Encerrar
   antes da Etapa 3; não disparar validação final ou criar distribuição.

Não importar #61 ou #63 novamente. Não alterar main/branches originais,
reescrever histórico, realizar merge de PR, publicar release ou deploy.

## Riscos e tratamento

São riscos de integração identificados por análise; a matriz de defeitos
da auditoria geral pertence à Etapa 6 do adendo e não foi antecipada.

| ID | Risco | Controle / evidência exigida |
| --- | --- | --- |
| RI1 | Substituir `Program.cs` e perder guardas de execução ou fechamento | Revisão por método na Etapa 2; regressões de pendência, cancelamento e Shutdown na Etapa 3 |
| RI2 | Apagar histórico físico ou promover roadmap/candidata pela fusão do JSON | União por chave/ID, estados 27/42 invariantes e referências de aprovação próprias |
| RI3 | Reimportar #63 e perder parser corrigido ou exclusão permanente | Manter blobs/revisões da #64 e comparar os cinco caminhos já mapeados |
| RI4 | Tratar ausência de endpoint como autorização futura de transporte | Allowlist/hash vazios, aprovações falsas e transmissões bloqueadas; provar caminhos negativos na árvore integrada |
| RI5 | Perder gates ao importar a reorganização dos workflows | Comparar comandos e condições; preservar testes, verificadores e upload bloqueado da BETA |
| RI6 | Confundir CI histórica com CI do novo SHA | Recibos independentes por árvore; integração não validada até a Etapa 3 |
| RI7 | Confundir evidência parcial com homologação AVG/física/conectada | Reusar provas anteriores apenas no escopo coberto; critérios físicos e privados continuam pendentes |
| RI8 | Publicar executável pela configuração de CI herdada antes da preparação autorizada | Etapa 1 documental com `[skip ci]`; preservar bloqueio da BETA e revisar geração/upload antes de executar CI futura |
| RI9 | Confundir integração técnica com o perfil restrito da BETA LOCAL | Preparação da Etapa 8 exige transmissão/coleta remotas desativadas, consultas oficiais por ação explícita, independência de Worker/D1/DNS e contrato de distribuição respeitado |

## CI consultada e verificação desta etapa

| Fonte validada | Run | Estado observado | Limite |
| --- | --- | --- | --- |
| #60, SHA `2bc9f42` | [40253446761](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40253446761) | success | Base individual |
| #61, SHA `f646007` | [40324802334](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40324802334) | success | BETA original |
| #62, SHA `e530d72` | [40409850849](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40409850849) | success | Workflow principal da BETA endurecida |
| #62, mesmo SHA | [40409850628](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40409850628) | failure histórica | Não reexecutada; não reabre o alvo permanentemente excluído |
| #63, SHA `04f5975` | [40507858102](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40507858102) | success | Experimento individual |
| Código pré-BETA `386e7cd` | [40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517) | success | Não é run do HEAD documental `357877e` |
| Integração | Nenhum | Não executada | Ainda não há código combinado |

Foram conferidos ancestralidade, caminhos/blobs, JSONs, estados do roadmap,
políticas de envio, configurações e workflows. Validam-se os novos documentos,
links internos e a igualdade de todos os blobs herdados da #64. Nenhum teste
de produto foi criado/repetido, nenhum SDK/instalador foi executado e nenhum
executável ou ZIP foi gerado. O commit documental usa `[skip ci]`; não atribuir
CI verde a esse HEAD nem reutilizar aprovação das fontes como prova da composição.

O AVG permanece ativo e sem homologação nova. Não houve operação em PC,
WinGet, driver, Worker, D1, DNS, Secret, transmissão ou serviço externo do
produto. Roadmap **27/42**, com os 15 checkpoints pendentes preservados.

**Parada após a Etapa 1. Próxima sessão: somente Etapa 2 — integração do código.**
