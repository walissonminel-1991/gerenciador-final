# GERENCIADOR FINAL — Fase 1 — H1: testemunha de lançamento resistente a rollback same-user

**30/10/2026 — checkpoint de modelagem adversarial e critério de prova, não implementação.**
**Conclusão restrita:** H1 não é demonstrável usando somente registros persistentes que o mesmo principal U pode restaurar integralmente. Não se afirma impossibilidade geral de um autoatualizador portátil ou de toda forma de testemunho protegido.

## 1. Estado e evidência de partida

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial confirmado: `fc44249aec6494874d44e3989822d5108031dd43`.
- Último commit técnico validado: `5aa02f7decf93abfb7d03fa64c2ad9f5f057b96a`.
- Última CI Windows: [41098083325](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41098083325), job `124208946182`, `completed/success`, 26/26 `PASS_CONTAINMENT`, zero recuperações automáticas. **Nenhuma nova CI nesta rodada**.
- PR #65 previamente conferida aberta/Draft, sem merge, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- Código verificado no HEAD: `Phase1Recovery.cs.txt:185–210` mantém `prepare-only` sem observação OLD/NEW; `:217–245` grava fences OLD/NEW antes da observação; `:307–329` bloqueia recuperação por `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. `Phase1RecoveryTests.cs.txt:604–640` reproduz NEW real seguido de restauração do prefixo pré-NEW; o caso `out-of-band-new-with-old-selected` continua a demonstrar execução fora do coordenador.
- Os testes demonstram contenção, **não** a existência da testemunha H1 nem READY.

## 2. Ator U, possível autoridade A e observador R

**U (adversário same-user):** processo ou pessoa executando código com o **mesmo principal de segurança e privilégios do processo do GF**, não necessariamente administrador. Pode copiar/repor arquivos, nomes de diretórios, entradas de diário, seletor, fences, hashes, recibos e chaves acessíveis a U. Pode terminar/reiniciar processos GF; pode iniciar uma cópia de NEW fora do coordenador nos caminhos já observados no laboratório. Pode restaurar em conjunto os objetos locais que controla, inclusive um estado anterior estruturalmente válido. Considerar apagamento/substituição de diretórios e pais, não apenas alteração dos bytes de uma folha.

**A (autoridade hipotética):** um componente/estado pertencente a um domínio que U não consegue escrever, restaurar, substituir, reconfigurar, falsificar ou contornar **no mesmo escopo afirmado**. Precisa sobreviver à perda do solicitante e ter semântica de persistência documentada. **A ainda não existe no GF e seu provisionamento não foi autorizado.**

**R (reconciliador C# L18):** processo novo que consulta objetos locais, pacote TUF, pins e, hipoteticamente, A. Não pode considerar PID/HWND, sucesso da UI, recibo same-user, Job vazio ou ausência de registros como autoridade de no-launch.

**Limite do modelo:** U **não** controla kernel/TPM/LSASS nem um eventual A independente, caso tal A venha a ser demonstrado. Ataques de administrador, firmware ou comprometimento integral do SO não fazem parte desta prova de H1. Caso o GF continue sendo executado pelo próprio administrador, os privilégios reais do adversário precisariam ser reavaliados antes de qualquer promessa de isolamento.

## 3. Contraprova condicional: histórias indistinguíveis

Seja `L` a coleção completa de **entradas de decisão que U consegue restaurar** e `P` o conjunto de propriedades estáticas revalidadas (manifesto TUF, hashes e identidade dos pacotes). Duas histórias são possíveis:

- **H0:** preparação L18 concluiu, o OLD/1 foi gravado, NEW nunca foi iniciado **nessa história**. R observa `(L0,P)`.
- **H1:** a mesma preparação ocorreu; NEW realmente iniciou e terminou; U reverteu todas as entradas mutáveis de decisão para `L0`, preservando `P`. R também observa `(L0,P)`.

Para qualquer função de decisão que **somente** lê `(L,P)`, a saída não pode distinguir H0 de H1. Assinatura de manifesto, digest do diário, GUID de transação, MAC de DPAPI, contador salvo **junto com L** e nome de Job não mudam essa equivalência se o registro antigo continua válido. O par de histórias é uma contraprova à alegação geral de autorização de recuperação baseada apenas em `(L,P)`.

**Precisão:** não se está afirmando que todos os registros possíveis do Windows, efeitos de aplicação e metadados de tempo sejam iguais. A indistinguibilidade refere-se explicitamente **ao conjunto de evidências aceito como autoridade**. Um novo W protegido poderia distinguir H0/H1, mas teria de ser apresentado e testado; ausência de W preserva `UnknownBlocked`.

A restauração conjunta de seletor e diário foi demonstrada em CI anterior e preservada entre os 26 casos. **Esta seção é uma inferência formal apoiada no contraexemplo existente, não um 27º teste executado.**

## 4. Requisitos mínimos do testemunho W (contrato de pesquisa, NÃO instalado)

Uma interface hipotética poderia oferecer `Begin(installation,tx,target,epoch)`, `Arm(installation,tx,target,epoch,slot)` e `ReadStrong(installation,epoch)`. Esses nomes são especificação; não são novas APIs do runtime.

**W1 — Titularidade independente:** U não consegue escrever, apagar, restaurar, alterar a política de acesso, trocar identificador de instalação/namespace, destruir o armazenamento ou apresentar uma réplica antiga como estado atual de A. Proteger só a folha é insuficiente quando U controla o pai ou pode reinstanciar a identidade.

**W2 — Vínculo inequívoco:** A vincula `installation/tx/manifest/TUF-target/generation/slot` e uma geração que não pode retroceder/reinicializar silenciosamente; rejeita duplicatas, forks de transação e reinstalação deliberada usada para reaproveitar um estado pré-NEW.

**W3 — Antes da capacidade de executar:** o registro `MAY_LAUNCH` de OLD/NEW deve ser considerado durável por A **antes de o coordenador criar qualquer processo capaz de executar o pacote**. O ACK não pode ser somente recibo emitido por U. Falha no ACK = **não lançar** naquele fluxo e **não declarar recuperação segura** de uma tentativa incerta. O contrato não estende essa precedência a lançamentos externos se H2 continuar falsa.

**W4 — Consulta após perda e reinício:** R precisa identificar **o mesmo** A e ler uma resposta atual/autenticada depois de crash, perda de handle/supervisor, reinício e reapresentação de todo L. Indisponibilidade, erro, estado destruído ou ID ambíguo = `UnknownBlocked`.

**W5 — Sem equivalência inválida:** `W=PREPARED` não certifica globalmente que NEW não executou, porque H2 não foi estabelecida; `W=MAY_LAUNCH` não prova que NEW **de fato** iniciou, mas proíbe inferir que não iniciou; `W=EXITED` não comprova quiescência de efeitos externos sem H3.

**W6 — Persistência e temporização:** definir e verificar quando a alteração de A se torna durável, o que ocorre se o processo morrer durante `Arm`, como tratar atraso/duplicação/reordenação de ACK e se rollback de todo L pode ressuscitar uma geração antiga. Não presumir que `FlushFileBuffers` de U prove durabilidade de A.

**W7 — Sem poder destrutivo:** indisponibilidade de A não pode acionar exclusão de OLD, de arquivos pessoais, do Windows, de chaves ou da instalação GF; deve apenas impedir a **recuperação automática experimental**. A aplicação já instalada e os caminhos normais de manutenção/apoio permanecem fora deste ensaio.

## 5. Matriz falsificável para um candidato A real (não executada)

| Cenário | Propriedade que um futuro ensaio deveria refutar | Interpretação segura agora |
| --- | --- | --- |
| F01 — U restaura arquivo antigo completo de W | A deve continuar mostrando estado novo, nunca um antigo válido | `UnknownBlocked` |
| F02 — U substitui pais/nomes/identidade de instalação | A deve impedir substituição/re-enrollment que pareça geração original | `UnknownBlocked` |
| F03 — U apaga W, simula erro de leitura ou troca por tipo inválido | A não deve converter ausência em no-launch | `UnknownBlocked` |
| F04 — U duplica ou reapresenta ACK válido | Vinculação a geração/tx impede replay | `UnknownBlocked` |
| F05 — queda do owner **antes** de solicitar `Arm` | `PREPARED` só diz respeito ao fluxo controlado por A | `UnknownBlocked` sem H2 |
| F06 — queda **durante** `Arm`, antes de ACK | A pode ter registrado ou não; não inferir ausência pelo timeout | `UnknownBlocked` |
| F07 — ACK durável de `Arm`, queda **antes** de `Process.Start` | `MAY_LAUNCH` pode ser conservador mesmo sem criação de filho | `UnknownBlocked` |
| F08 — NEW iniciou, terminou, U reverte L inteiro | A distingue a geração/estado apesar do rollback de L | `UnknownBlocked` até H3 |
| F09 — NEW iniciou fora do coordenador (sem `Arm`) | H1 isolada **não** detecta execução não submetida | `UnknownBlocked` até H2 |
| F10 — broker tardio após saída de NEW | W pode ter lançamento verdadeiro, mas não atesta fim dos efeitos | `UnknownBlocked` até H3 |
| F11 — supervisor/handle morreu; outra instância do R lê A | Não aceitar ID de processo ou handle recriado como mesma autoridade | `UnknownBlocked` sem prova |
| F12 — reboot ou perda de energia durante commit | Verificar semântica real de persistência, sem assumir flush abstrato | `UnknownBlocked` sem prova |

**Regra de aprovação de H1:** um experimento deve demonstrar, diante das capacidades U relevantes, que o estado que A efetivamente persiste não regride e que R obtém o mesmo domínio de autoridade depois da perda do solicitante. A prova deve incluir o cenário **F08** e os cortes de persistência, com erros tratados como desconhecidos. Um mock inerte em memória não satisfaz isso; **não** contar F01–F12 como PASS atuais.

**Mesmo que H1 seja validada**, H2 (despacho/domínio de efeitos) e H3 (quiescência) precisam de provas distintas, e OLD não pode ser automaticamente relançado com base em H1 isolada.

## 6. Alternativas Windows: avaliação de elegibilidade, não implementação

| Candidato | Problema verificado | Decisão atual |
| --- | --- | --- |
| Arquivo NTFS, hash encadeado, GUID, cópia dupla ou DPAPI de U | O estado antigo permanece autenticado e pode ser reapresentado; proprietário geralmente tem poder de alterar DACL de seu objeto | **Insuficiente** para H1 no modelo U |
| ACL herdada ou arquivo protegido apenas com DACL criada por U | O owner detém `WRITE_DAC`; controlar a DACL da folha não evita restauração/recriação de seus pais | **Insuficiente** sem principal/objeto independente e proteção dos namespaces |
| Nível MIC baixo vs médio | `NO_WRITE_UP` pode proteger objetos de baixo para médio, mas GF/U ordinário tem integridade média; não cria automaticamente separação perante **o mesmo** usuário médio e não resolve launch externo | **Condicional e fora do envelope** |
| Event Log nativo | Registro/permissionamento de fonte requer condições administrativas; retenção pode sobrescrever registros; entrada escrita por U não prova execução | **Diagnóstico**, não testemunho monotônico atual |
| TPM com chave hardware | Proteger material da chave não equivale a contador monotônico anti-rollback do **estado**. TPM/TBS têm autorização e listas de comandos por nível, além de provisionamento e disponibilidade variáveis | **Pesquisa somente; nenhuma operação NV/provisionamento autorizada** |
| Processo/serviço protegido por outro principal ou backend remoto | Poderia oferecer fronteira de confiança **se** autenticasse U, cobrisse anti-replay e sobrevivesse ao crash; ainda não supre H2/H3 | Exige nova autorização; **não implementado** |
| Job Object / handle preservado | Evidência limitada ao domínio Job; não persiste contra perda completa do supervisor/reboot e não abrange execução externa | **Insuficiente** para H1 durável |

**Fontes oficiais consultadas:**
- DPAPI: https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata
- Proprietário e `WRITE_DAC`: https://learn.microsoft.com/en-us/windows/win32/secauthz/owner-of-a-new-object
- Mandatory Integrity Control: https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control
- Retenção do Event Log: https://learn.microsoft.com/en-us/windows/win32/eventlog/event-log-records
- Registros de fontes Event Log exigem admin: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventlog.source
- TPM Command Blocking: https://learn.microsoft.com/en-us/windows/win32/tbs/command-blocking
- Using TPM Base Services: https://learn.microsoft.com/en-us/windows/win32/tbs/using-tbs

**Nota TPM:** a TBS pode atender usuários padrão nas versões modernas do Windows, mas isso não concede automaticamente acesso a qualquer comando ou índice NV. A documentação da Microsoft prevê bloqueio por comando e por perfil de usuário. Nenhum comando foi submetido, nem foi verificada permissão efetiva em hardware específico. Não recomendar alteração de TPM, NV index, BitLocker ou políticas.

## 7. Decisão e transferência

- **Resultado:** `H1_LOCAL_SAME_USER_STATE_INSUFFICIENT` sob o modelo definido. Não é nova prova de impossibilidade absoluta; é um limite da inferência sobre o conjunto de evidências usado pelo L18.
- **Próximo passo útil:** antes de usar WORK ou CI, nomear uma candidata A concreta e demonstrar, com documentação e pré-condições, (i) quem a cria/controla, (ii) por que U não consegue retrocedê-la, (iii) como R lê estado após crash e reboot, (iv) qual teste **real** de F01–F12 seria seguro e autorizado, (v) como preservar experiência do usuário caso A falhe.
- **Sem candidato com estas propriedades, não investir em mais recibos, hash-chains ou testes de mock.** Manter comportamento atual de bloqueio; não forçar recuperação funcional insegura.
- **Nenhuma CI nova:** esta rodada é modelagem formal e pesquisa documental; executar os 26 casos inalterados seria redundante.
- **Restrições preservadas:** `UnknownBlocked`, `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`, TUF/B+A/NTFS/TOCTOU, C# L18, `main`, PR #65 Draft, BETA .66, Cloudflare/D1, credenciais, telemetria e PCs. Sem WORK, merge, release, deploy, helper/serviço/tarefa, ACL/política de Windows, chaves ou backend.

**Fase 1 ABERTA · READY=false · G1–G8 BLOCKED 0/8 · última CI 26/26 PASS_CONTAINMENT · zero recuperações.**
