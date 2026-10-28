# Integração final — Etapa 7: correções e regressões

28/10/2026, America/Sao_Paulo. Branch `work-integracao-final-20261027`,
[PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).
**CONCLUÍDA no escopo técnico validado. Esta sessão executa somente a Etapa 7.**
Integração 1–4, organização 5 e auditoria 6 permanecem históricas.
Etapa 8 não iniciada; BETA LOCAL não preparada e conectada/pública bloqueada.

## Fonte e reprodução anterior à correção

Checkpoint, matriz, defeitos, avaliação 4, organização 5, auditoria 6 e adendo
relidos. Entrada: `89098a6669fadacd59e31fd8507bba9055a17a14`, árvore
`00c6107fef776e7ff4c88eb37f854a2003faa187`; 442 blobs conferidos por SHA Git.
Base da PR: `2bc9f4228fd448445fbf181652e5af93005def20` (#60), sem alteração.

Reprodução: **`f56c9303a66dfa9c3a29b220e38a9afe6f705b47`**,
[run Windows 40670973548](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40670973548),
job 122757340650. Compilou e executou os testes: **12 verificações, nove falhas**.
Não foi falha de compilação usada como reprodução. A fixture injeta um callback
no ponto que inicia HTTP/WUA, handlers HTTP fictícios e arquivos temporários
com consentimento schema 2/state 3 e aviso/hash atuais. Nenhum receptor, WUA
ou rede real é alcançado por essa fixture.

| Defeito | Antes, observado na CI | Correção e resultado aprovado |
| --- | --- | --- |
| GF6-001 / ALTO local | Callback de consultas automáticas chamado uma vez sem clique | Barreira compilada antes de iniciar HTTP/WUA; callback permanece em zero |
| GF6-002 / ALTO local | Consentimento herdado ativa gravador; saída/falha gravam; transporte fictício chamado; fechamento altera pendência | Perfil local bloqueia leitura de consentimento, coleta, gravação e transporte; preserva arquivos herdados sem renovação; revogação explícita mantida |
| GF6-003 / BAIXO | CS0649 em Program.cs:599, campo sem atribuição em GF_BETA | Declaração/usos alinhados a `!GF_BETA`, sem suprimir avisos ou restaurar botão retirado |
| GF6-004 / BAIXO | Dois GETs fictícios anunciam GerenciadorFinal/5.12.8 | Seis atribuições passam a usar a versão do assembly do produto |

O run vermelho também falha nos uploads `always()` de matrizes ainda não
produzidas, consequência da interrupção antecipada. Esses erros secundários
não foram contados como defeitos novos. A integridade externa do mesmo SHA,
[40670973372](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40670973372), passou.

## Correção da causa e preservação das duas linhas

`GfBetaProfile=local` é o padrão do projeto. Define `GF_BETA_LOCAL`, registra
o perfil no assembly e recusa valores desconhecidos e endpoint no perfil local.
`BetaRuntimePolicy` não lê preferência ou variável de ambiente capaz de ampliar
essa edição. A abertura conserva inventário e monitoramento locais e retorna
antes das tarefas de consultas automáticas. As consultas iniciadas pelo usuário
preservam seus caminhos e proteções; nada autoriza instalação pela descoberta.

O bloqueio de diagnóstico abrange `AnonymousTelemetry`, resolução de endpoint
e transporte, antes de ler/gravar arquivos, criar lease ou chamar HTTP. Vale
também nos handlers existentes de erro, encerramento e reabertura. Consentimento
remoto válido de outra edição não ativa o gravador. A edição local não expira,
apaga ou renova automaticamente suas pendências antigas. **Desativar**, por
ação explícita, conserva a revogação existente e tenta apagar a própria pendência.
Histórico técnico e relatórios locais independentes da coleta colaborativa
permanecem disponíveis. A simulação usa somente dados inventados.

O aviso local explica esse bloqueio e o botão de aceite fica indisponível.
O título identifica **BETA LOCAL TESTE — NÃO HOMOLOGADA**. O aviso remoto r3,
seu hash e as aprovações versionadas não foram alterados.

O perfil `internal` preserva os contratos tecnicamente aprovados da linha
BETA, inclusive consentimento/revogação, locks, pendência e fechamento. As
regressões antigas rodam em configuração `BetaInternal`, separada da `Release`
local; nenhuma expectativa antiga foi relaxada/removida. O script conectado
seleciona esse perfil somente após seus gates existentes, ainda pendentes.
Perfil interno sem endpoint em fixtures não é liberação da BETA conectada.

`ProductHttpIdentity` deriva User-Agent da versão do assembly. Os seis pontos
de RuckZuck, fontes oficiais, atualizadores verificados e Terminal compartilham
essa referência. URLs, allowlists, assinaturas, identidade, arquitetura e
autorização de instaladores permanecem intactas.

## Regressão encontrada durante a correção

O SHA intermediário `520e6d8db547ed03844ec80c78a80f498c667625` passou as
18 verificações locais, mas o [run 40672064936](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40672064936)
parou no teste existente `--print-version`: após incluir outro PropertyGroup,
a conversão PowerShell de `PropertyGroup.Version` devolvia uma coleção.
**GF7-001 / MÉDIO, pipeline de validação**, reproduzido pelo gate, sem build de
distribuição criada. O controle negativo de relatório L4 falha intencionalmente
antes desse passo e foi aprovado; não é a causa do run vermelho.

A causa foi corrigida selecionando Version por XPath e exigindo exatamente um
nó nos dois workflows. O teste dos três scripts é preservado e toda a CI roda
novamente em `c279b8617f76649e6aff3a664ec5efd074901a8b`. A integridade externa do
SHA intermediário, [40672064923](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40672064923), passou.
Não reutilizar esse resultado como aprovação do SHA final.

A atualização de `PRIVACY.md`, incluído na publicação, foi incorporada antes
da fonte final. O texto distingue o perfil local das políticas históricas;
por ser arquivo embarcado, exige nova CI da composição completa. O run
40672593443 de c279b861 foi substituído, sem transferir sua aprovação parcial.

O SHA 2d869f06 passou todas as regressões (18 locais, 127 privacidade,
15 janela, 36 mutações e demais suítes), mas o gate Win32 encontrou a mesma
GF7-001 em `TestesManifesto/Testar-Ativacao.ps1`: a versão era lida como
`5.13.11.66 `, com espaço do array. Evidência: run 40674027305, job
122772531281, falha antes da ativação SxS e de qualquer upload BETA. Corrigido
o consumidor restante por XPath/unicidade, preservando comparação exata e
CreateActCtxW. A busca de consumidores PowerShell agora não encontra mais
`.PropertyGroup.`; a CI completa é repetida no SHA final abaixo.

## Validação pelo SHA exato

Fonte corrigida final: **`6df95fdd776644cd4b248b5cea61108974ca9ce9`**;
árvore **`e29acb3619b85050a4cf5becaf81bfa3fdda0bd2`**.
Merge sintético da PR: `384f0a96acb8ccc5f99cccb0a930d8a9cedc50ad`,
com a mesma árvore e base #60. Esse merge é somente referência de teste do GitHub;
nenhuma PR foi mesclada. Aprovação da Etapa 3 ou das origens não é reutilizada.

| Evidência própria desta fonte | Estado |
| --- | --- |
| [40675784213 — Windows/principal](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213) | **completed/success**, tentativa 1 |
| [40675784155 — integridade externa](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155) | **completed/success**, tentativa 1 |
| Nova regressão `--beta-local` | 18/18 aprovadas; zero rede real/alteração de drivers na fixture |
| Python, pacote e guardrails | 47/47; 281 fontes examinadas |
| Node, receptores e retenção sintéticos | 136/136, sem Worker/D1/GitHub operacional |

Resultados próprios reconferidos nos logs desta fonte:

| Área / medida | Resultado |
| --- | --- |
| local_profile_assertions | 18 |
| updater_suite | 410 |
| multisource_suite | 196 |
| residual_uninstall_suite | 739 |
| msix_suite | 47 |
| shutdown_suite | 30 |
| privacy_transport_assertions | 127 |
| beta_window_assertions | 15 |
| receiver_retention_tests | 126 |
| staging_receiver_tests | 10 |
| python_policy_package_tests | 47 |
| catalog_suite | 17 |
| matching_suite | 20 |
| mutations_rejected | 36 |
| layout_resolution_tab_combinations | 132 |
| work18_layout_combinations | 8 |
| tray_cycles | 12 |
| windows2022_driver_assertions | 174 |
| windows2025_driver_assertions | 174 |
| package_hashes_verified_in_ci | 286 |

Não somar modos focados às suítes completas ou execuções nos dois runners.
Os sete jobs obrigatórios passaram; atestação e consultas externas adicionais
ficaram skipped por suas condições, sem tratá-las como testes executados.

As 12 verificações da reprodução foram mantidas e seis adicionais cobrem
identificação local na janela, primeira execução sem arquivos de coleta,
aviso/aceite, prévia, clique real da simulação e metadados sem endpoint/aprovações.
Não somar a repetição das 12 às 18 finais. O catálogo HTTPS é exercitado por
chamada explícita do teste com handler fictício; isso não é teste físico de
clique/consulta real. A regressão existente `--work18` conserva a projeção
consultiva na janela, com identidades e HTTP simulados.

A CI mantém os gates existentes: analisadores, segurança, hashes/assinaturas,
mutações em cópias descartáveis, inventários/MSIX, atualizadores, resíduos,
interface/RAM, encerramento e drivers somente leitura 2022/2025. Nenhum teste
de segurança foi removido. Compilação Windows x64 é validação técnica da
Etapa 7, sem preparação de entrega portátil ao mantenedor.

O verificador inspeciona o assembly efetivamente publicado sem iniciar o GF:
exige perfil local, origem exata e ausência de endpoint/aprovações conectadas.
O schema 1 de BETA-BUILD permanece igual. O workflow reconhece GF_BETA entre
todos os grupos de constantes, mantém o upload BETA somente de manifestos
e não publica EXE/DLL/ZIP de distribuição. Atestação continua condicionada
a main/não BETA e não comprova Authenticode ou AVG.

Conferência independente do artefato 12811032013: SHA-256 do ZIP de manifestos
`50b7a8c4d8bc15a826ebe2d54c7ada8f291d6cd2dbd19b8f805572f962c4c35c`,
três arquivos, 286 entradas no manifesto, dois hashes dos documentos conferidos.
Origem, perfil local e transporte desativado confirmados; nenhum executável no ZIP.
Isso não confere os 286 arquivos do pacote fora da CI nem prepara a entrega.

Ambiente de trabalho Linux sem .NET/PowerShell: C#/WinForms e publish são
executados somente nos runners descartáveis. Os testes Python/Node locais
não substituem a CI Windows da composição.

## Limites, integridade e parada

[Recibo estruturado e caminhos alterados](evidencias/integracao-etapa7-correcoes.json) ·
[defeitos e histórico](WORK-INTEGRACAO-DEFEITOS.md) ·
[checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [matriz](WORK-INTEGRACAO-MATRIZ.md).

Roadmap **27/42**, todos os 42 IDs/done e históricos/candidate/beta preservados.
Issue #48 permanece aberta; mitigação técnica não homologa o caso físico.
As consultas WUA desta fonte estão discriminadas por runner no recibo;
a evidência anterior, inclusive timeout 2022, permanece histórica. Consulta
vazia/parcial não comprova G1 em hardware com oferta nem autoriza payload
ou gestão de drivers.
AVG, assinatura, foco/DPI e consumo físicos continuam sem homologação da nova
candidata. Nenhuma operação em PC pessoal ou pedido de repetição física.

VLC segue somente inventário, sem ações/consultas/verificações ou testes
específicos novos. WinGet COM opcional/não bloqueante/consultivo, sem
reinvestigação; instalação/remoção/reversão de drivers indisponíveis.
AVG e proteções preservados. Flags `PILOT_ENABLED=no` e
`GITHUB_EXPORT_ENABLED=no`, aprovações, Worker/D1/DNS e origens intactos.
Sem merge, release, deploy, transmissão, distribuição ou execução de D01/D02.

Com a aprovação técnica registrada, uma próxima sessão autorizada pode executar somente **Etapa 8**,
conferindo novamente HEAD/CI e contrato de distribuição. Se este impedir entregar
executável, registrar bloqueio e propor via privada autorizada/verificável.
Não contornar o upload somente de manifestos. Sucesso técnico/local não libera
a BETA conectada ou pública. **Parar antes da Etapa 8 e não iniciar testes físicos.**
