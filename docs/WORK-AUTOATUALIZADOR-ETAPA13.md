# Autoatualizador GF — Etapa 13: preparação da revisão independente

29/10/2026, America/Sao_Paulo. **Preparação concluída; revisão independente
blocked/não executada. G2=blocked; oito gates bloqueados, zero fechado.
Etapa 14 não iniciada.** PR65 aberta/Draft; branch work-integracao-final-20261027.
HEAD obrigatório conferido: `b2af290cb41402a0d6a1dc3988b44f599cf2b0f4`.
SHA final documental registrado na descrição da PR e na entrega.

## Disponibilidade efetivamente verificada

A PR retornou zero reviews, comentários de conversa, comentários inline e
revisores/equipes solicitados. Nenhum revisor foi disponibilizado/autorizado
nesta missão ou parecer verificável encontrado nos registros TUF consultados.
Essa conclusão limita-se ao escopo inspecionado; não afirma inexistência de
toda revisão externa. CODEOWNERS identifica o proprietário, mas não comprova
aceite específico de manutenção TUF. D6-02 permanece pendente.

Aplica-se a condição especial de encerramento: entregar a preparação e parar.
O Work não substitui o revisor. Não houve contato, convite, Issue pública,
pedido de acesso, nova auditoria de código ou bateria de testes.

## Dossie minimo para revisao

O objeto é o protótipo existente no SHA de entrada, com dois commits de código
validados: `8dc843ff69b66f55f6e87fab93bd9c71c1242ec5` (confiança persistente)
e `eafec1070bd7c35420741bad4560dc9be4a37d6e` (vetores/diagnóstico). Ordem mínima:

| Material | O que delimita a revisão |
| --- | --- |
| [Contrato/ameaças](WORK-AUTOATUALIZADOR-ETAPA0.md) e [POUF](../prototipos/autoatualizador-seguro/POUF.md) | gf-self-update/1, requisitos GFU-C01–C16; Ed25519, root2-de-3, papéis1-de-1, seis chaves distintas, sem delegações/consistent snapshots. POUF inicial é histórico; usar extensões abaixo para UTC/persistência |
| [Patch](../prototipos/autoatualizador-seguro/stage8/tuf-delta.patch), [preparação](../prototipos/autoatualizador-seguro/stage8/prepare_source.py), [manifesto](../prototipos/autoatualizador-seguro/stage3/tuf-source-manifest.json) | Tuf1.1.1, fonte c0f5ff9747aa9b00c45a964dca275be4066cd489; modificados apenas TufClient.cs, TufClientOptions.cs e TufResults.cs: UTC escalar, cliente único, término em timestamp igual; fachada sem refresh duplicado |
| [DurableEpochCache/TrustModel](../prototipos/autoatualizador-seguro/stage11/src/DurableTrust.cs.txt), [prova](../prototipos/autoatualizador-seguro/stage11/src/TrustFixtureProof.cs.txt), [composição](../prototipos/autoatualizador-seguro/stage11/src/TrustIntegratedPipeline.cs.txt) | Diário por raiz/papel, selos, revalidação desde âncora de fixture, hints não autoritativos; pisos/épocas, lease, bloqueio de estado desconhecido; confiança independente da confirmação inerte |
| [Etapa12](WORK-AUTOATUALIZADOR-ETAPA12.md), [bytes dos vetores](evidencias/autoatualizador-etapa12-vetores.json), [traces/resultados](evidencias/autoatualizador-etapa12-windows.json) | Duas rotações raiz1/papéis100→raiz2/papéis1; Python preserva o outro metadado100. Ablação aceita, sem corrigir upstream. Conclusão do implementador, não parecer independente |
| [Pins Python](../prototipos/autoatualizador-seguro/stage11/fixtures/oracle-source-pin.json), [lock BC](../prototipos/autoatualizador-seguro/stage11/packages.lock.json), [aceitação Etapa11](WORK-AUTOATUALIZADOR-ETAPA11.md) | BC2.7.0; Python7.0.1 no commit49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15, securesystemslib1.3.1/cryptography50.0.2/urllib3 2.8.0 só nos ensaios; cobertura e limites históricos |

Hashes de referência: patch LF
`6fc508a44ba6afb9e231601094b699f01d897da75f0de828f2819737c305591f`;
DurableTrust
`faad41f3ec587ee7ea21a8c53766ff431ced562453465a2118be34d13f302373`.
O [JSON desta etapa](evidencias/autoatualizador-etapa13.json) registra os hashes
dos 24 arquivos conferidos. Licenças MIT conforme recibos de pacote preservados:
manter LICENSE/THIRD-PARTY-NOTICES da fonte original, [licença das fixtures
Python](../prototipos/autoatualizador-seguro/stage11/fixtures/LICENSE-PYTHON-TUF-MIT.txt)
e LICENSE do GF. Não houve nova auditoria de nupkg/licenças ou atualização.

A cadeia autentica rootN+1 por thresholds antigo/novo. Troca de timestamp ou
snapshot inaugura época limitada e invalida ambos; targets isolado conserva
pisos. Admissão/avanços persistidos sobrevivem às recuperações supervisionadas;
escrita parcial ou desconhecida permanece bloqueada. A âncora é só fixture:
nenhum metadado de teste autoriza GF real.

Riscos a avaliar: rollback/replay/freeze/mix-and-match, confusão de épocas,
cache parcial, concorrência, UTC e I/O desconhecido. Limites: domínio integral
restaurável (G1), root/kernel comprometidos, energia/dispositivo, perfis fora do
perfil fechado, migração de cache legado, IPC/ciclo GF. Flush/rename/kill não
demonstram durabilidade física. O replay dos bytes da Etapa12 usa UTC histórico;
observar seu limite de relógio, sem contornar o piso UTC persistente.

As evidências reutilizadas são 45 casos históricos da run40859356385 e
14 casos .NET/16 observações Python da run40864493929. Metadados das runs foram
conferidos, sem reexecução. Os 13 acordos de15 da Etapa11 não são conformance
integral. A [TUF1.0.36](https://theupdateframework.github.io/specification/v1.0.36/)
e seu pin constam no recibo da Etapa12; nenhuma nova adjudicação normativa aqui.

Questões ao revisor: **A/B**, aplicação de §5.3.11/invalidação conjunta;
**C**, pisos por época/targets derivado; **D**, diagnóstico Python versus outra
condição normativa; **E**, avanço persistente/recuperação; **F**, ataques acima;
**G**, correções/condições para adoção. Parecer deve registrar identidade e
qualificação/independência, SHA/escopo/data, achados reproduzíveis e decisão por
questão. Nenhum campo foi preenchido com pessoa ou aprovação fictícia.

## Manutenção: proposta concreta, sem aceite

D6-02 requer pessoa/equipe com aceite explícito para patch, adaptadores de
UTC/ownership, diário/épocas e composição, delimitando suporte e prazos.
Proprietário, CODEOWNERS e Work não foram designados automaticamente.
Responsabilidades propostas para esse aceite:

- Acompanhar avisos oficiais TUF/upstream/dependências; revisar diff, pins,
  locks e licenças antes de atualizar, com avaliação separada.
- Executar só garantias afetadas: clock/refresh→casos Etapa8; épocas→vetores
  Etapa12; diário→casos pertinentes Etapa11; composição→quatro casos inertes.
- Tratar vulnerabilidades conforme [SECURITY.md](../SECURITY.md), em canal
  privado autorizado, se disponível; caso contrário aguardar decisão, sem
  Issue pública de exploit/credenciais. Prazos devem ser acordados, sem SLA fictício.
- Separar manutenção de custódia; recuperar/rotacionar apenas confiança
  autenticada e autorizada. Não aceitar resets genéricos ou presumir término de I/O.

O aceite deve identificar responsável, consentimento/data, escopo, obrigações
e prazos. Parecer independente e decisão de adoção continuam separados.

## Requisitos G2

| Requisito | Resultado | Evidência e limite |
| --- | --- | --- |
| Fonte/contrato/dependências rastreáveis | passed | Conferência de integridade; não auditoria nova |
| Assinaturas/thresholds/referências/raiz contígua | passed | Casos Etapas11/12, somente perfil fechado |
| UTC único/cliente único/timestamp igual | passed | Etapa8 e provas preservadas; sem reteste |
| Persistência por papel e recuperação observada | passed | Diário/cortes Etapas11/12; não energia/dispositivo |
| Pisos por época nas duas rotações | passed | Vetores Etapa12; conclusão do implementador |
| Confiança independente da confirmação | passed | Composição inerte Etapa11; não ciclo GF |
| Concordância bruta nas duas rotações Python | failed | Diferenças reproduzidas; não prova isoladamente erro .NET |
| Qualificação pertinente de conformidade/interoperabilidade | blocked | Decisão revisada sobre cobertura/perfil/referência ausente |
| Suíte externa integral de conformidade | not-executed | Cobertura pertinente ainda deve ser decidida; sem declaração integral |
| Revisão independente qualificada | blocked | Sem revisor/parecer verificável no escopo consultado |
| Manutenção formal aceita | blocked | D6-02 pendente; CODEOWNERS não é aceite |
| Aceite de persistência/recuperação operacional | blocked | Faltam revisão/aceite e evidências pertinentes G1/G8 |
| Adoção operacional autorizada | blocked | Protótipo isolado; demais gates abertos |

Resultados positivos delimitam **subprovas técnicas**. Revisão independente,
manutenção aceita e adoção autorizada continuam ausentes; uma condição não
substitui outra. A falha de concordância não foi apagada nem tomada como
autoridade normativa isolada. **G2 não fechado.**

## Dependências e caminho mínimo

| Gates | Ação concreta | Classe | Limite/decisão |
| --- | --- | --- | --- |
| G2 | Dossiê, hashes e critérios de aceite | implementável agora | Preparado nesta etapa; não é parecer |
| G2 | Revisor qualificado independente | dependente de ambiente externo | Recurso humano autorizado ausente |
| G2 | Aceite de manutenção e decisão de perfil/referência | dependente de autorização | Requer decisão explícita e aceite do responsável |
| G7/G3 | Autorizar âncora/custódia/bootstrap futuro | dependente de autorização | Sem gerar chaves; .66 não vira alvo retroativo |
| G7 | Custodiantes/meios separados/cerimônia | dependente de ambiente externo | Depois da autorização; nada provisionado |
| G3/G4 | Pacote GF/PE/runtime de laboratório | já comprovada | Etapa10; não autentica alvo futuro nem fecha gates |
| G3/G4 | Candidato futuro e editoria exata | dependente de autorização | Exige G2/âncora e pacote autorizados |
| G4 | Dependências/assinaturas/revogação aplicáveis | dependente de ambiente externo | Bytes autorizados e Windows; beta-unsigned preservado |
| G6/G8 | Adaptadores isolados de saída/prontidão/rollback | implementável agora | Em missão própria; não executado/integrado aqui |
| G5/G6 | IPC/contextos reais e participantes GF | dependente de ambiente externo | Windows descartável/contextos efetivos e ensaio autorizado |
| G1/G8 | Energia/armazenamento/I/O desconhecido | dependente de ambiente externo | Laboratório e qualificação técnica ainda necessários |
| G1 | Frescor autenticado fora do estado restaurável | dependente de ambiente externo | Decidir/desenhar testemunho; pode ser manual, sem impor serviço online |

As classes são de subtrabalhos: **nenhum gate completo foi comprovado como
dependente apenas de uma autorização**. G2/G7 têm decisões/recursos externos;
G1/G5/G8 também precisam de qualificação técnica; G3/G4 exigem insumos e
validação; G6 ainda requer implementação e ciclo real.

Menor caminho: disponibilizar revisor e aceite de manutenção; decidir o
perfil/referência e corrigir somente achados demonstrados; preparar em missão
própria adaptadores e ambientes IPC/armazenamento/frescor; com autorizações
pertinentes, qualificar âncora/custódia, pacote futuro, ciclo conjunto e bootstrap
manual de outra entrega. Nenhuma contagem fixa de etapas garante esses gates.

## Registro e encerramento

Verificações novas: consultas read-only,24 arquivos/Git blobs, hashes dos dois
JSON Etapa12,20 blobs do catálogo e9 conjuntos de referências. **Zero código
alterado, testes novos/repetidos, vetores recriados ou runs novas.** Uma busca
usou nome inexistente do recibo Etapa6; corrigida para o arquivo de dependências
existente, sem inferir aprovação. Falhas históricas permanecem intactas.

[Evidência JSON](evidencias/autoatualizador-etapa13.json) e [matriz G1–G8](evidencias/autoatualizador-etapa13-gates.json)
registram verificações, requisitos e decisões. Dossiê é este relatório, sem
documento paralelo. Alterados somente relatório, esses dois JSON, checkpoint e
PROGRESSO; históricos e IDs/done preservados.

Preservados .66/fonte GF/canal privado, main410d65e2,13 workflows,27/42 e físico
parcial; AVG/Windows, consentimento/privacidade, remoto desativado, flags no,
drivers inativos, VLC inventário e COM opcional/consultivo. Sem contas,
credenciais, permissões, trust store, infraestrutura/Worker/D1/DNS, chaves
operacionais, integração GF, PCs pessoais, merge/Release/deploy/instalação
operacional/publicação. **Concluída somente a preparação; revisão independente
não homologada. Etapa14 não iniciada. Encerrar após salvar/verificar na PR65 Draft.**
