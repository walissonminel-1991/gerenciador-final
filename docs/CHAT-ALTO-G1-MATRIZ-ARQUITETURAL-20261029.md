# GERENCIADOR FINAL — G1: matriz arquitetural e decisão de confiança
**CHAT ALTO | 29/10/2026 | ADR experimental, não aprovado para produto**

## 0. Fonte, baseline e controle de mudanças

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de partida conferido: `b1211c2bd17b333a2a3e032d48dad62265340c41`.
- Documento de origem: `docs/CHAT-ALTO-M3-G1-WITNESS-CHECKPOINT-20261029.md`.
- Código experimental de referência: `prototipos/autoatualizador-seguro/m3-g1-witness/G1WitnessModel.cs.txt` e `G1WitnessTests.cs.txt`.
- CI de **código já realizado**, não reexecutada: [40896067869](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40896067869), código `e65f9630bc49ef2b333f3c8643a061ca3d1600a7`, Windows 2022, **11/11 PASS, 0 warnings, 0 errors**.
- PR #65 Draft preservada em `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; `main`, BETA .66, telemetria, Cloudflare e releases fora do escopo.
- **Esta missão é exclusivamente documental:** nenhum novo código, teste, build ou CI executado. Não apresentar a CI precedente como validação deste documento.

## 1. Vocabulário probatório — não confundir três níveis

| Nível | Significado | O que existe no repositório |
|---|---|---|
| **D: demonstrado em fixture** | Asserção exercitada no runner Windows com diário inerte e testemunha na memória | G1-01 a G1-11, CI acima |
| **H: hipótese do desenho** | Premissa que o modelo usa, mas não implementa nem autentica | Witness independente, honesto, durável, acessível e com identidade confiável |
| **P: pendência de segurança** | Contrato, prova ou homologação sem a qual o G1 operacional não pode ser liberado | Bootstrap, chaves e rotação, resistência a rollback da witness, protocolo transacional, queda de energia e recuperação |

**Invariantes obrigatórios do produto futuro (propostos, ainda não implementados):**

- **I1 — sem retorno silencioso:** nenhuma instalação, seleção de bundle ou confirmação pode confiar apenas em `PersistentState.Load` após uma possível restauração integral.
- **I2 — autenticidade antes da comparação:** receber campos `nonce`, `installationId`, geração ou hash não autentica a testemunha. A assinatura/identidade deve ser verificada por uma âncora já confiável.
- **I3 — monotonicidade da testemunha:** o checkpoint externo não pode recuar; concorrência, duplicação de solicitações e replays precisam ser atômicos e inequívocos.
- **I4 — falta de prova = bloqueio apenas do autoatualizador:** sem witness confiável ou diante de divergência, não atualizar, confirmar, promover pending, reduzir piso nem adotar um arquivo antigo. Não confundir isso com bloquear funcionalidades não relacionadas do GF.
- **I5 — rollback vs crash ambíguo:** uma divergência não é prova de ataque nem de sucesso; manter evidências e exigir reconciliação autenticada.
- **I6 — igualdade não autoriza uso:** concordância de heads é condição necessária, mas não suficiente, para TUF, validade temporal, consentimento, integridade de bundle, G5/G6, G8 e instalação.
- **I7 — separação de domínios:** o witness não deve herdar automaticamente credenciais, segredos, consentimento ou retenção da telemetria BETA.

## 2. Matriz arquitetural de decisão G1

As decisões abaixo são **propostas conservadoras de arquitetura**, não decisões aprovadas para implementação.

| ID | Dimensão | Contrato proposto / decisão provisória | Evidência D | Principal pendência P e critério de avanço |
|---|---|---|---|---|
| G1-A | Autenticidade da resposta | Assinatura de recibo por chave de witness fixada em raiz previamente confiável; vincular domínio, protocolo, instalação, desafio, contador, digest e fase. TLS isoladamente não substitui prova de estado | G1-06 e G1-10 recusam nonce/instalação errados **sob hipótese de witness autêntica**; G1-11 mostra falsificação viável por testemunha comprometida | Definir formato canônico, algoritmos, verificador, distribuição/rotação/revogação de chaves, agilidade e testes de falsificação; não usar nonce como assinatura |
| G1-B | Independência e resistência a rollback | Witness durável em domínio de falha distinto do diretório local; estado por instalação com avanço monotônico atômico e política anti-equivocação | G1-02 mostra rollback local íntegro recusado por memória independente; G1-03 recusa fork | Demonstrar que persistência da witness não é restaurada junto com PC e que operador comprometido não consegue produzir passado com assinatura válida; avaliar proteção por hardware/quórum/auditoria conforme ameaça real |
| G1-C | Inscrição inicial (*bootstrap*) | **Não aceitar TOFU automático** a partir de uma cópia local desconhecida. Inscrição apenas com identidade/âncora autenticada e autorização explícita em fluxo futuro | G1-07 falha fechada quando não há registro | Projetar transferência de dispositivo, reinstalação, perda da testemunha e recuperação com trilha auditável, sem sobrescrever piso anterior; aprovar privacidade/consentimento antes de qualquer canal |
| G1-D | Identidade e privacidade | Identificador pseudônimo de instalação, mínimo necessário; separar identidade da release TUF, usuário humano e diagnóstico; limitar correlação | G1-10 vincula respostas a instalação no modelo | Modelagem de dados, retenção, exclusão, revogação, metadados de acesso, vazamento de presença/versão, DoS, LGPD e revisão de privacidade antes de usar rede |
| G1-E | Frescor e replay | Consulta viva obrigatória com desafio criptograficamente aleatório vinculado à sessão/instalação e resposta assinada; recusar respostas antigas, expiradas ou inconclusivas; não confiar em cache local para atestar frescor | G1-06 detecta replay de nonce anterior | Definir unicidade após reboot, algoritmo/entropia, janela de resposta, relógio confiável, semântica de sequência e resistência a *freeze*: um servidor malicioso pode assinar resposta antiga com nonce novo |
| G1-F | Relação local–witness | Comparar âncora de checkpoint **claramente definida**; geração, piso aprovado, digest, identidade e seletor devem ter semântica estável. Não equiparar automaticamente último arquivo do ledger a último checkpoint externo | G1-01 (igualdade), G1-02 (regressão), G1-03 (fork), G1-08 (witness atrasada) | Definir os campos exatos e sua canonicalização; uma geração local adicional de recibo/ack não pode produzir falso conflito por erro de protocolo |
| G1-G | Coordenação transacional | Preparar localmente de modo **não executável**, registrar intenção vinculada ao txId, buscar compromisso durável na witness, registrar recibo local e reconciliar. Não alegar transação atômica distribuída entre NTFS e witness | Etapa 2 modela `intent/selected/confirmed/rollback`; G1-08 recusa witness atrás | Especificar máquina de estados, idempotência, compare-and-swap do último head, crash points e prova de que nenhuma seleção/confirm é exposta antes das condições exigidas |
| G1-H | Indisponibilidade | **Fail closed** para autoatualização que dependa de witness: timeout, offline, 404/not enrolled, assinatura inválida, expiração e resposta ambígua não viram aprovação local | G1-04, G1-05, G1-07 | Definir UX sem tela extra, diagnósticos locais mínimos, política de retry com backoff e limite, operador de recuperação sem rebaixar piso; GF básico permanece utilizável |
| G1-I | Recuperação ambígua | Preservar dados e bloquear confirmação quando witness/local divergem. Diferenciar `remote ahead`, `local ahead`, fork e `pending` em vez de apagar evidência | G1-02, G1-03, G1-08 e M3 `pending` mostram divergência real | Fluxo autenticado de reconciliação com recibos e continuidade de cadeia; critérios de retorno ao estado seguro e ações proibidas explícitas |
| G1-J | Não equivocar / comprometimento | Considerar testemunha maliciosa no modelo. Assinatura por **única** chave comprometida não garante não-equivocação; avaliar proteção por quórum/transparência/contador protegido, conforme risco e custo | G1-09 impede equivocação só no objeto honesto; G1-11 comprova que fonte comprometida mente | Definir adversário, capacidade de auditoria/revogação, política de quorum e reação a assinaturas conflitantes; não proclamar segurança com mera TLS |
| G1-K | Limite com G8 e TUF | Witness de frescor **não** é motor de atualização, autorização de execução, antimalware nem cancelamento de I/O síncrono | M3 G8 expôs janela entre checagem e persistência; G1-01 somente compara igualdade | Provar G2/TUF, G5/G6, integridade da seleção sob NTFS e G8 separadamente antes de integração |
| G1-L | Custo / implantação | Nenhum endpoint, conta, chave, deployment ou coleta é autorizado nesta fase. Primeiro congelar contrato e ameaças; depois revisão independente em momento de maior valor | CI prova apenas o modelo inerte | Aprovação explícita do mantenedor sobre dependência online, privacidade, custo e impacto para o usuário; *WORK* somente com pacote de revisão delimitado |

## 3. Estados transacionais e matriz de crash — desenho não implementado

A testemunha modelada guarda um head; **ela não faz um commit remoto real**. Para não esconder estados intermediários, um protocolo futuro precisaria separar `head comprometido externo` de `gerações locais de preparação/ack`, e vincular a transação a um identificador único. Se uma linha abaixo for ambígua, a decisão de segurança é bloquear, preservar e reconciliar, não reconstruir confiança por palpite.

| Ponto de interrupção | Diário local possível | Witness possível | Conduta conservadora proposta |
|---|---|---|---|
| Antes de qualquer preparo | Estado antigo estável | Head antigo igual | Consulta autenticada; somente avaliar condições; sem aprovação implícita |
| Depois de escrever `intent` local, antes de enviar ao witness | Local adiantado ou `pending` | Antigo | Bloquear e investigar; não lançar candidato nem excluir `pending` |
| Solicitação enviada, sem resposta | Local preparado | Antigo **ou** novo (resultado desconhecido) | **Não** presumir que falhou; buscar recibo/idempotência em consulta autenticada posterior, permanecendo bloqueado |
| Witness gravou novo head, processo caiu antes do ack local | Local antigo/preparado | Novo | Tratar como *remote ahead*; não reduzir witness, não chamar automaticamente de ataque nem recuperar antecessor |
| Ack local registrado, mas atualização/selector não concluída | Local indica fase intermediária | Head avançado | Reabrir como recuperação restrita; usar provas de transição e verificação de bundle; sem promoção silenciosa |
| Seletor trocado/confirmado e worker caiu | Confirmado ou pendente | Igual, atrasado ou avançado | Reconciliar a fase + verificação de identidade/lease/tempo G8; não confundir seleção com aprovação de segurança |
| Estado local restaurado a snapshot anterior | Cadeia antiga íntegra | Head novo | Rejeitar `G1_LOCAL_ROLLBACK`; a testemunha não pode recuar |
| Dois históricos locais válidos da mesma geração | Digest/seleção diferente | Head de um deles | Rejeitar `G1_LOCAL_FORK`, preservar evidências e investigar |
| Witness indisponível na inicialização ou no commit | Qualquer | Não verificável | Bloquear operações dependentes; sem fallback por cache |
| Witness comprometida ou com assinatura conflitante | Qualquer | Respostas potencialmente falsas | Isolar confiança, revogar conforme governança futura; **nenhum** automático `trust reset` |

**Questão arquitetural a resolver:** se o witness ancora cada geração local, os registros locais posteriores de recebimento do recibo geram diferença artificial. O contrato deve definir o checkpoint estável e prova de extensão do ledger, e não simplesmente impor igualdade cega do último número de geração em todo ponto de crash. A suite G1 de 11 casos **não prova** essa coordenação.

**Regra de segurança para reprocessamento:** solicitar novamente a situação de `txId` deve ser idempotente, autenticado e distinguir **não visto / preparado / comprometido / conflitante**. Um timeout do cliente não implica ausência de commit no witness.

## 4. Proposta de protocolo mínimo para futura revisão independente

*É um contrato de referência para análise; não é implementação aprovada.*

1. **Identidade autenticada:** ancorar a identidade da instalação e as chaves da witness sem confiar só na instalação já comprometida. Vincular formato de resposta a propósito e versão do protocolo (separação de domínio).
2. **Estado externo monotônico:** exigir `compare-and-swap` em `installationId`, `priorWitnessEpoch` e `priorCommittedDigest`; retornar erro de concorrência em vez de sobrescrever. Epoch não poderá recuar; assinatura protege autenticidade, mecanismo externo protege a atualização monotônica.
3. **Preparação local restrita:** registrar transação `txId` e checkpoint candidato em estado incapaz de ativar um binário. Validar integridade do candidato e a origem TUF em etapa separada.
4. **Publicação idempotente:** atualizar witness e gerar prova autenticada vinculada a `txId`, origem, destino, epoch, checkpoint estável, identidade e expiração, sem aceitar instruções de uma testemunha desconhecida.
5. **Reconciliar o resultado antes de usar:** após ack confirmado, registrar recibo de forma consistente no diário; o recomeço após crash deve consultar estado autenticado e não avançar por mera ausência de erro.
6. **Ativação somente após gates independentes:** quaisquer verificações finais de TUF, permissões, processo, consentimento, timeout absoluto e rollback continuam obrigatórias. Não fornecer uma API `IsTrusted=true` consumível diretamente pelo atualizador.

**Não especificado/não validado**: assinatura e rotação, armazenamento externo, idempotência/serialização reais, garantia de não-equivocação, crash consistency entre meios, relógio, identidade persistente, reimaging, revogação, recuperação após perda do witness, hardening contra escritor do mesmo usuário, ACL/TPM e provas de falha elétrica. O procedimento de recuperação deve tratar essas lacunas antes de qualquer implementação operacional.

## 5. Critérios objetivos de aceite e evidências exigidas

| Lote futuro | Critério mínimo demonstrável | Evidência necessária | Status |
|---|---|---|---|
| E1 — ameaça/privacidade | Modelo do adversário e política de dados/consentimento aprovados | ADR de risco, revisão de privacidade e autorização do mantenedor | **Pendente** |
| E2 — autenticidade | Resposta falsa, nonce antigo, assinatura inválida, chave revogada, identidade errada e rotação adversarial sempre rejeitados | Fixtures assinadas com chaves **somente de teste**, verificação independente e revisão criptográfica | **Pendente** |
| E3 — monotonicidade externa | Reboot/rollback local completo incapaz de regredir head externo; concorrência e equivocation tratadas | Storage/witness independente em ambiente descartável, simulação e auditoria de corridas | **Pendente** |
| E4 — bootstrap | Perda de registro nunca vira TOFU; recuperação de máquina e migração exigem autorização segura | Casos negativos com identidade desconhecida, migração, witness perdida e ataque de reaproveitamento | **Pendente** |
| E5 — coordenação | Cada corte de crash antes/depois de preparo, publish, ack e seleção termina em estado auditável e sem confirmação indevida | Matriz de fault injection com idempotência, prova de cadeia e comparação autenticada | **Pendente** |
| E6 — indisponibilidade | Offline/timeout/freeze bloqueiam só autoatualização; sem rebaixar piso | Testes com indisponibilidade prolongada, retry e UX acessível | **Pendente** |
| E7 — integração e isolamento | Nenhum código do protótipo compila no produto sem autorização; G2/G5/G6/G8 independentes permanecem obrigatórios | CI Windows, revisão de código, homologação e critérios dos outros gates | **Pendente** |
| E8 — durabilidade física | Comportamento de flush, queda elétrica e recuperação validado separadamente | Ambiente descartável/fault injection física, não inferido de `File.Flush(true)` | **Pendente** |

Uma prova de E2–E8 não deve ser declarada homologação por documentação ou testes em memória. A suite atual comprova apenas **D**, sob hipóteses explícitas.

## 6. Decisão operacional desta rodada e próximo trabalho

**Decisão:** encerrar a exploração repetitiva do mesmo contraexemplo. Manter G1–G8 **BLOCKED (0/8)**, sem integrar witness ao GF. Este ADR contém opções e requisitos; **não constitui escolha nem autorização de infraestrutura**.

**Próxima subprova que agregaria garantia real:** um **modelo inerte de idempotência e crash de publicação/ack** (E5), com eventos `não visto/preparado/comprometido`, cortes entre escrita local e witness e testes negativos para estado remoto avançado, estado local adiantado e dupla publicação concorrente. Só desenvolver se o contrato da máquina de estados for definido; evitar repetir G1-02/G1-03 de snapshot local.

**Momento adequado para WORK:** após consolidar o protocolo de transição/recibo e a política de ameaça, pedir **uma única revisão independente** da consistência e do trust bootstrap, com SHA, contraexemplos e critérios objetivos. Não usar WORK para redigir novamente a matriz ou executar as mesmas 11 fixtures.

**Fronteira preservada:** nenhuma criação de serviços, endpoints, domínio, segredos, contas, deploy, release, merge, alteração de permissões, manipulação de PCs pessoais, telemetria ou beta; nenhum código de produto alterado. As garantias reais de G1 e G8 continuam por demonstrar.
