# Autoatualizador GF — Etapa 7: decisões e desbloqueio direcionado

28/10/2026, America/Fortaleza. **Revisão e decisões técnicas concluídas no
escopo documental. Zero de oito gates operacionais fechado; oito bloqueados.
Etapa 8 NÃO INICIADA.** PR #65 aberta/Draft, branch
`work-integracao-final-20261027`. Entrada
`bee4bebd5c5714bae7430a9249c961e1b85af523`, árvore
`60a061a244098e83a69d86eaaed5b1cc5ed83640`.

Lidos o checkpoint vigente, o relatório/dossiê da Etapa 6, D6-01 a D6-07 e os
trechos pertinentes do contrato **gf-self-update/1, revisão 1, intacto**.
A inspeção adicional foi restrita ao cliente TUF fixado, patch experimental,
adaptador de fixture e prazo/I/O. Não houve teste novo ou repetido, build, CI,
troca de biblioteca, adoção de patch ou alteração de código/workflow.

## Decisões técnicas registradas

| ID | Decisão dentro deste escopo | Evidência e limite |
| --- | --- | --- |
| A7-01 | Manter bloqueado o requisito forte de antirreplay após restauração integral enquanto faltar referência independente autenticada | O contraexemplo anterior e a indistinguibilidade de observações locais continuam válidos. Não retirar garantia nem escolher serviço/protocolo por inferência |
| A7-02 | Selecionar, para o desenho da próxima revisão isolada, adaptação mínima da fonte MIT já fixada, com UTC imutável por tentativa e cliente de uso único | O pacote original não injeta tempo; o experimento aceita provider genérico. Sem pacote novo, implementação ou adoção operacional; manutenção/aceitação pelo mantenedor ainda pendentes |
| A7-03 | Exigir término explícito do ciclo TUF quando timestamp tem mesma versão; evitar o par de chamadas que atualiza metadados duas vezes | Achado estático R7-TUF-02: `return` local não interrompe o fluxo externo da fonte fixada. Não é exploração executada nem correção aplicada |
| A7-04 | Um proprietário controla admissão, início, término observado e recursos de cada I/O; prazo vencido fecha a admissão e invalida o resultado para commit | `WaitAsync` não prova término do trabalho. Desenho inclui corrida de cancelamento, tarefa que bloqueia antes de retornar e operações sem cancelamento |
| A7-05 | Resultado de I/O desconhecido mantém tentativa impedida de aplicar, limpar ou repetir; recuperação depende de reconciliação e término comprovados | Não prometer encerramento de driver, durabilidade de uma marca ou liberação segura por timeout. Os ensaios de armazenamento/contexto continuam externos |
| A7-06 | Preservar beta-unsigned, importação explícita, separação G7a/G7b e bootstrap manual futuro em pasta nova | São escolhas existentes do contrato, não novas aprovações de certificado, chaves, canal ou pacote |

As decisões A7 são de arquitetura/revisão documental. Não são autorização de
produção, parecer de um segundo revisor, homologação ou fechamento de gate.
O detalhamento está na [revisão do relógio](WORK-AUTOATUALIZADOR-ETAPA7-RELOGIO.md)
e no [desenho de prazo/I/O](WORK-AUTOATUALIZADOR-ETAPA7-PRAZO-IO.md).

## D6-01 a D6-07: decisão técnica versus autorização

| D6 | Resultado da Etapa 7 | Decisão ou recurso que continua faltando |
| --- | --- | --- |
| D6-01 | Rejeitada solução somente local para a garantia forte; manter bloqueio e contrato | Mantenedor/revisão de segurança: escopo e mecanismo autenticado fora do domínio restaurado. Qualquer revisão de garantia requer decisão explícita; nenhuma foi tomada |
| D6-02 | Rota técnica A7-02 e condições de aceitação definidas; revisão encontrou fluxo pendente além dos sete pontos de relógio | Mantenedor: aceitar responsabilidade de manutenção e designar revisão. Implementação/delta e evidência interoperável continuam ausentes; nenhuma alteração de dependência |
| D6-03 | Não repetir runner administrativo como substituto do contexto necessário | Laboratório autorizado: disco NTFS persistente/controlador externo e contas/sessões padrão reais previamente preparadas. Nada foi provisionado |
| D6-04 | Manter G7a como insumo e G7b como saída posterior | Autorização específica do mantenedor, responsáveis e meios offline para cerimônia. **Criação de chaves operacionais não autorizada nem realizada** |
| D6-05 | Separar revisão de validador de qualificação de participantes GF | Mantenedor/desenvolvimento: definir e fornecer candidato/protocolo isolados. Não atribuir versão nem transformar a .66 em alvo gerenciado |
| D6-06 | Nenhuma decisão pendente impede o ramo beta-unsigned por falta de certificado | Só se o mantenedor desejar mudar futuramente para modo obrigatório: política incorporada de publicador/revogação/timestamp. Não adotada |
| D6-07 | Adotado desenho de admissão/ownership/drenagem/quarentena para futura implementação isolada | Revisão de engenharia do delta e evidência de I/O real/armazenamento. Garantia de término forçado em dois minutos foi rejeitada, sem relaxar os limites de validação |

Não há pedido genérico de nova aprovação nesta sessão. As dependências acima
são registradas para decisão específica posterior, sem execução dependente
presumida. O conector não bloqueou a gravação; faltam insumos operacionais.

## Estado dos gates após a revisão

| Gate | Bloqueio remanescente | Preparação acrescentada |
| --- | --- | --- |
| G1 | Armazenamento real e frescor fora do estado restaurado | Separação mantida; nenhum recibo antigo/hashing substitui novidade |
| G2 | Implementação revisada, manutenção aceita, fluxo TUF e evidência de adoção | Mapa de sete locais, cliente por tentativa, retorno de timestamp igual e limites de cache/rotação explicitados |
| G3 | Âncora operacional e pacote GF exato autenticado até o estado | Não usar sonda/CI ou .66 como autorização de alvo |
| G4 | Classificação do PE GF autenticado no modo declarado | Beta-unsigned preservado; nenhum downgrade de falha assinada |
| G5 | Positivo padrão e negativas entre sessões/usuários reais | Ambiente necessário identificado, sem nova run nem manipulação de token/UAC |
| G6 | Participantes GF, carregamento tardio/inserção, ciclo transacional e recuperação | Prazos de validação, confirmação e recuperação distinguidos; nenhum ensaio de sonda contado como GF |
| G7 | Cerimônia/custódia/âncora e bootstrap final validados | Autorização e meios continuam faltantes; slots intactos/vazios |
| G8 | Pipeline implementado e término real de I/O/durabilidade comprovados | Máquina de estados conceitual, catálogo de operações e critérios de delta preparados |

O grafo de insumos da Etapa 6 permanece válido. A rota técnica de G2 e o desenho
de G8 podem orientar trabalho isolado futuro sem aguardar chaves; isso não
antecipa G3/G6/G7, modifica o manifesto ou libera uma aplicação operacional.
Critérios futuros neste relatório não foram executados e não são testes aprovados.

## Evidências, preservação e encerramento

[Matriz de decisões e achados](evidencias/autoatualizador-etapa7-decisoes.json) ·
[Recibo e verificações documentais](evidencias/autoatualizador-etapa7.json) ·
[Checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).
Proveniência por commit/hash e referências primárias estão nos documentos
específicos. Dois arquivos upstream necessários à revisão foram recuperados e
conferidos contra os hashes já fixados; nenhuma suíte ou auditoria de pacote
foi repetida. Os achados são de inspeção estática e análise de desenho.

BETA **v5.13.11.66**, fonte `6df95fdd776644cd4b248b5cea61108974ca9ce9`,
run privada 40716548530/tentativa 1/artefato 12828262039 e ZIP SHA-256
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`
preservados, sem download/build/requalificação. Canal privado, retenção/acessos,
roadmap **27/42**, mesmos 42 IDs/done e avaliação física parcial intactos.
Históricos e evidências anteriores mantidos; zero repetição física.

AVG/Windows/proteções, consentimento/dados, VLC só inventário/zero consultas ou
ações específicas, COM opcional/consultivo e drivers instalar/remover/reverter
inativos preservados. Remoto bloqueado, PILOT_ENABLED=no/GITHUB_EXPORT_ENABLED=no.
Sem Worker/D1/DNS, infraestrutura, contas, token/ACL/UAC/privilégios, trust store,
chaves operacionais, integração ao produto, bootstrap instalado, merge,
Release ou publicação. Verificações de JSON/links/preservação são documentais,
sem acrescentar pontos ou cobertura operacional.

**Encerrar após salvar/conferir na PR #65. Não iniciar a Etapa 8.**
