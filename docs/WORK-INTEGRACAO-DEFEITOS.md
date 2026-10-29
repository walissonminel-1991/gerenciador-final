# Integração final — matriz de defeitos

Auditoria da Etapa 6, 27/10/2026, America/Sao_Paulo.
Branch `work-integracao-final-20261027`, [PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).
Fonte auditada: **`db67cde412db306c316e79c0761e8dafef4b3b34`**.

[Relatório/14 eixos](WORK-INTEGRACAO-ETAPA6-AUDITORIA.md) ·
[Recibo](evidencias/integracao-etapa6-auditoria.json) ·
[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz de continuidade](WORK-INTEGRACAO-MATRIZ.md).

**Atualização da Etapa 7, 28/10/2026: os quatro GF6 corrigidos e validados no SHA `6df95fdd776644cd4b248b5cea61108974ca9ce9`.**
A regressão de pipeline GF7-001 também foi reproduzida/corrigida; CI própria aprovada.
As localizações/reproduções da auditoria abaixo referem-se à fonte anterior, preservada como evidência.
Nenhum CRÍTICO, MÉDIO ou EXTERNO demonstrado nesta auditoria. Isso não constitui
garantia de ausência de defeitos: Windows/AVG/físico têm os limites do relatório.

Na fonte auditada, os dois ALTOS eram lacunas confirmadas contra o adendo BETA LOCAL. Os contratos
da BETA interna validada permitem seus comportamentos; o perfil local restrito
ainda não existia nessa fonte histórica. A CI anterior continua aprovada no seu escopo.
A preparação/teste local exige resolver as duas lacunas e comprovar o perfil
restrito por testes próprios; não basta reutilizar a build interna.

## Resumo atualizado após a Etapa 7

| ID | Severidade | Escopo | Achado | Evidência | Estado / efeito |
| --- | --- | --- | --- | --- | --- |
| GF6-001 | ALTO | BETA LOCAL | Abertura inicia consultas de versões/WUA sem ação explícita | Rastreio estático da abertura até os provedores; testes de janela existentes usam startServices=false | CORRIGIDO; perfil local/CI própria da Etapa 7, limites físicos mantidos |
| GF6-002 | ALTO | BETA LOCAL | Consentimento remoto válido herdado mantém coleta/gravação de pendência mesmo sem endpoint | Gravador/handlers de falha/saída e teste de janela já aprovado na CI da composição | CORRIGIDO; perfil local/CI própria da Etapa 7, limites físicos mantidos |
| GF6-003 | BAIXO | Fonte integrada | Campo sem atribuição em GF_BETA produz CS0649 | Declaração/condicional e aviso já registrado na CI | CORRIGIDO; perfil local/CI própria da Etapa 7, limites físicos mantidos |
| GF6-004 | BAIXO | Clientes HTTP | User-Agent anuncia versões fixas diferentes da .66 | Seis pontos de construção de cabeçalho na fonte | CORRIGIDO; perfil local/CI própria da Etapa 7, limites físicos mantidos |

## Fechamento e defeito adicional da Etapa 7

[Relatório antes/depois](WORK-INTEGRACAO-ETAPA7-CORRECOES.md) · [CI Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213) ·
[integridade externa](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155) · [recibo](evidencias/integracao-etapa7-correcoes.json).
Reprodução `f56c9303a66dfa9c3a29b220e38a9afe6f705b47`: 9/12 falhas. Fonte final `6df95fdd776644cd4b248b5cea61108974ca9ce9`: 18/18 locais,
regressões internas/segurança preservadas e build x64 validada no mesmo SHA.
GF6-003 não aparece mais como CS0649; User-Agent observado 5.13.11.66.

**GF7-001 — MÉDIO / CI, CORRIGIDO.** Local: leitura de Version em
`.github/workflows/validar-candidata-windows.yml` e `build-windows.yml` e `TestesManifesto/Testar-Ativacao.ps1`.
Reprodução: executar o gate existente dos três scripts `--print-version` na
árvore intermediária 520e6d8d; `PropertyGroup.Version` vira coleção após adicionar
outro grupo e causa `Versao incorreta: Publicar-Portatil-Windows-x64.bat`.
Evidência: run 40672064936/job 122761057719, após 18/18 locais aprovadas;
segunda ocorrência no gate Win32, run 40674027305/job 122772531281, com regressões completas aprovadas.
Impacto: interrompe validação e pode produzir leitura ambígua na identificação
da build; publicação foi impedida pelos gates. Correção: XPath com exatamente
um nó Version, mantendo o teste. Run 40675784213 aprova esse gate e a CI completa.
Nenhuma nova remoção de teste, supressão ou contorno.

Recomendação atual: manter esses contratos como regressões. Nenhum defeito
CRÍTICO/ALTO conhecido permanece aberto no escopo técnico demonstrado; isso
não homologa AVG/PC ou assegura ausência universal de defeitos. Etapa 8 não
iniciada; BETA LOCAL não preparada e conectada/pública bloqueada.

## Reprodução e recomendações da auditoria 6 — histórico

## GF6-001 — consultas automáticas na abertura

**Localização:** [Program.cs:651](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/Program.cs#L651),
[Shown:788](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/Program.cs#L788),
[BeginStartupChecks:2501](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/Program.cs#L2501)
e `LoadUpdatesAsync` em 2302.

Reprodução por alcançabilidade, sem efetuar rede:

1. Considerar abertura normal, `startServices=true` (padrão), inventário
   não vazio e janela ainda aberta; nenhum clique em verificar atualizações.
2. `Shown` termina `LoadInstalledAsync` e chama `BeginStartupChecks`.
3. `RunStaggeredStartupChecksAsync` chama `LoadUpdatesAsync` e depois a consulta
   WUA de Windows Update. Não há gate de perfil local/ação explícita nesse caminho.
4. `LoadUpdatesAsync` cria os provedores RuckZuck/oficiais e inicia suas tarefas.

**Esperado no perfil local:** zero consultas externas até ação explícita do
usuário; depois, somente HTTPS oficial estritamente consultivo permitido pelo
perfil. **Observado na fonte:** a abertura alcança os provedores automaticamente.
WUA aqui é Windows Update, sem nova investigação de WinGet COM.

**Impacto:** descumpre a política de rede solicitada para a BETA LOCAL. Não
há evidência de envio de inventário ou execução de instalador nessa abertura.

**Evidência/limite:** confirmado por fluxo da fonte, não por abrir o GF em PC.
Os testes de janela existentes desabilitam serviços para isolamento; portanto,
seu resultado não prova o requisito novo de zero rede na abertura local.
Nenhuma consulta real foi disparada nesta auditoria.

**Recomendação:** criar uma barreira explícita de perfil local na composição
do aplicativo, conservar inventário/monitoramento locais e permitir consultas
somente pela ação autorizada. Na Etapa 7, reproduzir em janela/transportes WUA
e HTTP fictícios, verificar zero chamadas na abertura e as chamadas permitidas
após a ação. Preservar timeout, cancelamento, fontes excluídas e guardas de execução.

## GF6-002 — coleta remota herdada apesar de transporte desconfigurado

**Localização:** [Program.cs:40](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/Program.cs#L40),
handlers de falha em 56–73 e `FormClosed` em 774–784;
[AnonymousTelemetry.cs:31](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/AnonymousTelemetry.cs#L31),
`ReadConsent` em 92–99, `Record` em 165–178 e `SaveOnExit` em 313–328;
[DiagnosticTrace.cs:154](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/DiagnosticTrace.cs#L154).

Reprodução já representada nas fixtures existentes, para runner Windows
descartável; não executar no perfil pessoal do mantenedor:

1. Em pasta temporária da fixture, obter consentimento `schema=2/state=3`
   com versão/hash atuais do aviso. Não usar consentimento real ou endpoint.
2. Reabrir `AnonymousTelemetry(folder)`: a escolha válida volta como RemoteBeta.
3. Registrar um código permitido e encerrar normalmente ou chamar
   `SaveOnExit(..., normalExit:false)` na fixture de falha.
4. A pendência agregada pode ser gravada mesmo com `IsConfigured=false`.

**Esperado no perfil local:** a preferência remota herdada não ativa coleta
para envio nem nova pendência remota, inclusive em erro/encerramento. Histórico
e relatórios técnicos locais autorizados devem continuar disponíveis.
**Observado:** endpoint bloqueia o transporte, mas não o gravador RemoteBeta.
O aceite legado schema=1/state=3 ou aviso inválido continua recusado; a lacuna
trata de escolha herdada que ainda satisfaz o aviso atual, não de bypass do hash.

**Evidência:** [BetaCloseTests.cs:64](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/TestesLayout/BetaCloseTests.cs#L64)
recupera o consentimento e, em 68–73, exige explicitamente pendência preservada
em build sem endpoint. Esse caso integra as 15 verificações da janela BETA
aprovadas na CI da composição. `BetaTelemetryTransportTests.cs:165` também
comprova persistência de estado incompleto em falha. São evidências da CI
Windows do SHA `6b5b0e52`, com fontes funcionais idênticas; não foram reexecutadas
no Linux. O transporte real permanece desconfigurado e não houve envio.

**Impacto:** o perfil atual não satisfaz o desligamento de coleta/transmissão
remotas exigido para a BETA LOCAL. A ausência de endpoint sozinha é insuficiente.

**Recomendação:** aplicar política local independente do endpoint e de escolhas
antigas em inicialização, gravação, falha e saída; conservar as proteções da
futura BETA conectada e os relatórios locais. Na Etapa 7, testar consentimento
herdado válido, falha/Shutdown/saída e revogação com handlers fictícios, sem
apagar arquivos pessoais, flexibilizar notice/hash ou habilitar receptor.

## GF6-003 — CS0649 no perfil GF_BETA

**Localização:** [Program.cs:599](https://github.com/walissonminel-1991/gerenciador-final/blob/db67cde412db306c316e79c0761e8dafef4b3b34/Program.cs#L599),
atribuição em 941–950 sob `#if !GF_BETA`; símbolo GF_BETA no projeto, linha 15.

**Reprodução/evidência:** compilação da mesma fonte com GF_BETA, como na CI
Windows 40604593820, deixa `_privacyStatusButton` sem atribuição e emite CS0649.
O aviso já estava registrado nas Etapas 3–4. O build foi aprovado; a janela
BETA e privacidade/revogação pela bandeja foram testadas no escopo da CI.

**Impacto:** ruído de compilação e campo sem uso no perfil; nenhum defeito
funcional da bandeja ou necessidade de reintroduzir botão permanente foi demonstrado.

**Recomendação:** alinhar declaração/usos ao condicional na Etapa 7 se conveniente,
sem ocultar avisos, remover testes ou restaurar seletores/botão retirados.

## GF6-004 — versões divergentes nos cabeçalhos HTTP

**Localização e reprodução estática:** projeto anuncia `5.13.11.66`; as
construções de User-Agent ainda usam:

| Fonte | Linhas | Valor fixo |
| --- | --- | --- |
| RuckZuckCatalog.cs | 30 e 43 | GerenciadorFinal/5.12.8 |
| OfficialReleaseSources.cs | 250 e 266 | GerenciadorFinal/5.13.11 |
| VerifiedUpdaters.cs | 704 | GerenciadorFinal/5.12.18.1 |
| WindowsTerminalUpdater.cs | 105 | GerenciadorFinal/5.12.17 |

Todas as fontes estão fixadas pelo SHA de entrada e seus blobs no recibo.
Um handler fictício pode observar o cabeçalho sem rede; nesta auditoria a
constatação é por inspeção das seis atribuições. Não foi feita consulta a
fabricante ou inferida recusa/compatibilidade HTTP.

**Impacto:** identificação de versão inconsistente em rastreamento do cliente;
não altera SHA/hash, autorização de instalação ou identidade de aplicativo.

**Recomendação:** conferir se existe contrato documentado de versão de protocolo;
se for versão do produto, derivá-la de referência única/rastreável. Corrigir
conforme impacto, preservando allowlists e consentimento, sem teste ao vivo.

## Evidências inconclusivas e pendências — fora da contagem de defeitos

- **Issue #48, aberta:** fonte atual usa catálogo per-user somente HKCU e
  abertura com direitos de leitura/handle predefinido. `Work14RegistryTests`
  tem fixtures de token restrito e falhas por etapa na CI. Isso sustenta a
  mitigação técnica já incorporada, sem homologar o caso físico antigo.
  Não fechar a Issue nem repetir desinstalações/alterar ACLs para obter prova.
- **WUA histórica da Etapa 3:** 2022 excedeu prazo; 2025 sem ofertas. Os resultados
  específicos da fonte final da Etapa 7 estão no recibo, sem homologar G1 físico. Não atribuir EXTERNO sem causa comprovada;
  não comprovam G1/payload, e gestão de drivers permanece desativada.
- **AVG/assinatura/DPI/CPU/RAM físicos:** sem novos testes ou homologação.
  Limites de cobertura são registrados no relatório, não convertidos em
  defeitos inventados nem em aprovações.
- **Entrega local/contrato:** perfil corrigido/validado na Etapa 7; pacote e entrega ainda pendentes; não contornar
  restrições de distribuição ou liberar conectada/pública para superar bloqueio.

**Próxima sessão: somente Etapa 8. Correções técnicas da Etapa 7 concluídas; preparação/teste físico ainda pendentes. Roadmap 27/42 preservado.**
