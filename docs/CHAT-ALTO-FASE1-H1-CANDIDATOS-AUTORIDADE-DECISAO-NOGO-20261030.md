# GERENCIADOR FINAL — Fase 1 — H1: escolha de autoridade resistente a rollback sem administrador

**Data:** 2026-10-30. **Resultado:** `NO_GO_H1_WITHIN_CURRENT_PORTABLE_UNPRIVILEGED_ENVELOPE`.
**Natureza:** parecer de elegibilidade e critérios de futuro experimento, baseado na fonte existente e documentação técnica externa. Nenhum mecanismo novo foi instalado, executado ou testado.

## Âncora auditável

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial reconfirmado: `6cdbc3aff274d04894a4144207db46fe4155ebcf`.
- Último commit técnico validado: `5aa02f7decf93abfb7d03fa64c2ad9f5f057b96a`.
- CI Windows [41098083325](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41098083325), job `124208946182`, `completed/success`, 26/26 `PASS_CONTAINMENT`, nenhuma recuperação automática. Não foi disparada CI nesta rodada.
- PR #65 confirmada `open/Draft/merged=false`, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. `main` observada em `410d65e23546976db456337c71e4705f10217d36`.
- Ponto de partida: `docs/CHAT-ALTO-FASE1-H1-TESTEMUNHA-ANTIROLLBACK-MODELO-AMEACA-20261030.md` (U/A/R, W1–W7, F01–F12), além de `docs/CHAT-ALTO-FASE1-PREPARACAO-DADOS-FRONTEIRA-EFEITOS-WORK-20261030.md`.
- No código `Phase1Recovery.cs.txt`, `prepare-only` é caminho terminal de validação de dados, mas já grava diário/OLD/1; a primeira observação OLD e a NEW possuem seus fences; `ReconcileReal` permanece bloqueado por `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. Nenhum code path foi alterado nesta rodada.

## 1. Decisão e propriedade exigida

**Pergunta precisa:** existe, nos privilégios de um usuário Windows comum, com distribuição GF portátil e sem provisionamento privilegiado, serviço persistente, política Windows, credencial remota ou backend novo, uma testemunha concreta `A` que:

- conserve uma geração monotônica após a morte do coordenador e reboot;
- não possa ser restaurada, apagada, substituída ou reinicializada por U, inclusive via diretório-pai, ID de instalação e reinstanciação de namespace;
- confirme de forma durável `MAY_LAUNCH` **antes** da primeira capacidade de executar o candidato;
- possa ser consultada pelo reconciliador novo de forma atual, autenticada e inequívoca;
- seja operável sem elevador/UAC, segredo de produção, login do avaliador, mudança de configuração do Windows, risco desproporcional ao hardware ou dependência permanente de rede?

**Nenhum candidato avaliado teve essas propriedades demonstradas conjuntamente.** Isto é `NO_GO` para a integração **neste envelope**; não é uma prova de impossibilidade de todas as arquiteturas futuras.

## 2. Comparação dirigida à segurança, não a marketing de criptografia

| Candidato | Possibilidade real | Falha/condição decisiva | Classificação |
| --- | --- | --- | --- |
| Arquivo local com HMAC, hash-chain, DPAPI, Credential Manager ou contador em HKCU | Pode detectar edição acidental, perda ou divergência entre componentes | Uma cópia antiga válida do mesmo conjunto de arquivos/segredos de U ainda pode ser apresentada. O usuário controla estado e, em geral, é proprietário de seus objetos | `NOT_H1_AUTHORITY` |
| Arquivo NTFS/DACL criada por U | Isolamento contra **outros** usuários, controle de integridade local | O proprietário recebe implicitamente `WRITE_DAC`; U pode alterar a DACL e recriar objetos dentro dos pais que controla | `NOT_H1_AUTHORITY` |
| Event Log padrão | Fonte pós-crash útil para diagnóstico quando eventos existem | Retenção pode sobrescrever registros; a emissão pelo processo de U não atribui causalidade independente a L18 | `NOT_H1_AUTHORITY` |
| TPM 2.0: **contador NV real**, não apenas chave CNG | Um contador TPM NV tem tipo e comando específicos de incremento e é a classe de dispositivo mais próxima de uma geração não retrocedível | É preciso definir índice, obter autorização correta para ler/incrementar, impedir undefine/redefine e reset de namespace, confirmar comandos permitidos ao usuário padrão em cada Windows/TPM, durabilidade e retomada; nada disso foi homologado | `RESEARCH_ONLY_NOT_PROVISIONED` |
| CNG Microsoft Platform Crypto Provider | Pode resguardar chave não exportável no TPM | Chave não exportável **não é** contador monotônico; assinatura/criptografia de um snapshot não impede replay do snapshot anterior | `NOT_H1_ALONE` |
| Serviço dedicado sob LocalService/SYSTEM ou outro principal | Poderia oferecer armazenamento protegido e autenticar o histórico conforme um protocolo restrito | `CreateService` exige direito de criação no SCM, não assegurado ao usuário padrão; provisionamento, manutenção, recuperação, versionamento e IPC aumentam escopo e custo. Apenas separar o serviço não impede U de executar NEW fora do coordenador | `REQUIRES_NEW_AUTHORIZATION` |
| Backend remoto independente | Pode impedir U de restaurar histórico **no servidor** e atrelar gerações a instalação | Exige identidade/anti-replay, conectividade, disponibilidade, privacidade, política de retenção e gestão remota; não autorizado aproveitar Cloudflare/D1 BETA nem criar endpoint | `OUT_OF_SCOPE` |
| Task Scheduler no mesmo principal, mutex, Job Object | Coordenação de execução local ou observação dos processos incluídos | Um segundo processo do mesmo SID não é uma autoridade independente; Jobs não abrangem execução externa e não persistem após reboot | `NOT_H1_AUTHORITY` |

### Uma distinção que previne falso otimismo com TPM

O Windows TBS permite **acesso à API** por usuários padrão em versões modernas do sistema, mas aplica **bloqueio de comandos** e autorização do TPM/índice. A permissão de criar um *context* TBS **não** autoriza, por si, `TPM2_NV_DefineSpace`, `TPM2_NV_Increment`, `TPM2_NV_Read` ou `TPM2_NV_UndefineSpace` para um índice concreto. Mesmo quando os comandos são suportados, seus parâmetros, política de autorização e proprietários precisam ser verificados.

O armazenamento NV/counters é um recurso limitado **não virtualizado** e compartilhado por outros componentes do SO; não criar ou manipular índices no GF, tampouco ajustar TBS, TPM, BitLocker, políticas ou listas de comandos nesta missão. Há ainda riscos de indisponibilidade de TPM, reset, lockout e falta de segurança caso a autoridade para *undefine* permita a U reinstanciar o índice.

**Uma possível chave CNG não exportável não prova monotonicidade do estado.** Testar somente a geração de chaves, sem contador vinculado e protegido, não seria progresso em H1.

Fontes:
- Microsoft TBS — acesso padrão e autorização: https://learn.microsoft.com/en-us/windows/win32/tbs/using-tbs
- Microsoft TBS — comandos bloqueados/permitidos: https://learn.microsoft.com/en-us/windows/win32/tbs/command-blocking
- Microsoft TBS — counters/NV não virtualizados: https://learn.microsoft.com/en-us/windows/win32/tbs/resource-virtualization
- Microsoft — capacidades TPM/CNG de proteção de chaves: https://learn.microsoft.com/en-us/windows/security/hardware-security/tpm/how-windows-uses-the-tpm
- TCG/tpm2-tools — counter NV e comando `NV_Increment`: https://github.com/tpm2-software/tpm2-tools/blob/master/man/common/nv-attrs.md e https://tpm2-tools.readthedocs.io/en/latest/man/tpm2_nvincrement.1/
- Microsoft — proprietário e `WRITE_DAC`: https://learn.microsoft.com/en-us/windows/win32/secauthz/owner-of-a-new-object
- Microsoft — retenção de Event Log: https://learn.microsoft.com/en-us/windows/win32/eventlog/event-log-records
- Microsoft — direito SCM para criar serviço: https://learn.microsoft.com/en-us/windows/win32/services/service-security-and-access-rights

## 3. Contraprova que permanece relevante mesmo com A protegida

Supor que um TPM NV monotônico tenha `counter=100`, e após `Arm` tenha `counter=101`:

- H0: a transação que U afirma ser atual jamais lançou NEW **via L18**; mas U pode executar NEW externamente sem modificar o contador (H2 não está resolvida).
- H1: NEW executou via L18; U restaurou o diário/OLD/1 com `counter=100` e R lê `counter=101`. O descompasso permite detectar rollback **se** o vínculo de identidade e época ao contador tiver sido protegido antes, o índice não tiver sido recriado e R conseguir consultar o valor atual após crash.
- O valor `101` sozinho não autentica qual pacote/manifesto foi armado. Se a identidade/época ficar só em arquivo restaurável, U pode trocar o significado do número; A precisa autenticar a **ligação** com o escopo L18 ou a transação fica desconhecida.
- Se A não responder ou desaparecer, não comparar contra zero/default para autorizar OLD: `UnknownBlocked`.
- Mesmo prova de `MAY_LAUNCH` não certifica **quiescência** de processos ou serviços externos (H3 permanece falsa).

**Conclusão:** um contador independente seria, no máximo, componente possível de H1. Não satisfaz H2/H3, nem autoriza recuperação OLD.

## 4. Requisitos de elegibilidade antes de acionar WORK ou CI

Para qualquer futura candidata A, exigir documentação e teste novo distintos dos 26 anteriores:

1. **Proprietário/provisionamento:** nomear principal/autoridade que provisiona; demonstrar capacidade de configurar e manter sem alterações privilegiadas/segredos do usuário, ou pedir nova autorização explícita.
2. **Monotonicidade real e anti-reinstanciação:** provar `F01–F04, F08` inclusive remoção/recriação da identidade, pais e estado antigo. **Mock em memória não qualifica.**
3. **Precedência e corte:** crash antes/durante/depois de `Arm`, perda de ACK, falha de persistência e possibilidade de lançamento externo; provar que cliente não faz launch sem confirmação, mas continua bloqueado se a confirmação for inconclusiva.
4. **Reboot/power loss:** observar comportamento real da autoridade independente, sem inferir durabilidade de um `FlushFileBuffers` local. Não executar esses ensaios em PC pessoal.
5. **Operação sem dano:** falha de A deve impedir apenas recuperação automática da tentativa; nunca apagar OLD, arquivos de usuário, estado Windows ou chaves; TPM indisponível não deve degradar uso normal do GF.
6. **Custo/abrangência:** testar versões Windows e TPM, contas padrão, uso offline e limites de hardware, sem impor login ou serviço persistente não autorizado.
7. **Não confundir garantias:** completar H1 não permite marcar READY nem desloca H2/H3 para concluídas.

**Ponto de decisão:** até aparecer A concreto com essas propriedades demonstráveis, **não implementar** simulações de autoridade ou reexecutar os 26 casos só para aumentar contagem. Uma futura solicitação para serviço/TPM NV/ACL/backend deve especificar privilégio, alteração de sistema, rollback de sua própria configuração e avaliação de risco, com autorização separada do mantenedor.

## 5. Disposição final

- Resultado de pesquisa: `NO_GO_H1_WITHIN_CURRENT_PORTABLE_UNPRIVILEGED_ENVELOPE`.
- Candidato prioritário de estudo: **TPM 2.0 NV counter**, apenas **hipótese**, sem operação, provisionamento ou recomendação de integração.
- Alternativa de fronteira mais previsível em princípio: principal protegido por um serviço **explicitamente provisionado**, fora da autorização atual; custos de distribuição/segurança e H2/H3 independentes.
- Candidatos descartados: recibos/diários same-user, DPAPI/CNG isolados, Event Log, ACL controlada por U e Job como prova H1.
- Próxima atividade racional: **não investir WORK/CI em H1 até definição de A e permissões**. Voltar ao mantenedor com a escolha de envelope de produto: exigir infraestrutura/autoridade opcional ou aceitar que a recuperação automática diante de história ambígua fique desabilitada.
- `Phase1Recovery.cs.txt` e os 26 casos permanecem inalterados. Sem merge, release, deploy, WORK, ações em TPM, chaves, serviço, tarefas, política de Windows, Cloudflare/D1, telemetria ou computadores pessoais.

**Fase 1 ABERTA · READY=false · G1–G8 BLOCKED 0/8 · zero recuperações autorizadas · CI existente 26/26 PASS_CONTAINMENT.**
