# GERENCIADOR FINAL — Lote 8: contrato proposto de bootstrap, inscrição e recuperação G1
**CHAT ALTO · 29/10/2026 · CONTRATO DOCUMENTAL NÃO APROVADO · G1–G8 BLOCKED (0/8)**

## 0. Controle de escopo e classificação

- Repositório: `walissonminel-1991/gerenciador-final`; branch exclusivamente documental `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de entrada conferido: `a8c23dc50c1f080b9f203b223a71f29096487d13`.
- Fonte principal: `docs/CHAT-ALTO-LOTE7-DECISAO-CONDICIONAL-G1-AMEACAS-RECUPERACAO-20261029.md`, complementada pelo ADR do Lote 6, matriz G1 e parecer crítico WORK G1/G2/G8 compartilhado pelo mantenedor.
- Evidência existente: [L5 CI 40922528166](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40922528166), **seis testes de fixture** com TUF Stage11/Stage3, Stage9/Stage2 e CAS em memória L4. Não demonstra witness externa, bootstrap seguro, credenciais, recuperação operacional nem aprovação G1.
- PR #65 Draft, branch `work-integracao-final-20261027`, HEAD de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. `main`, BETA .66, telemetria desativada, Worker/D1 existentes e demais recursos são protegidos.
- **Não há autorização** para criar serviços, domínios, endpoints, secrets, chaves, contas, credenciais, CI, testes, builds, executáveis, deploy, merge, releases ou integração. Este lote altera **apenas este documento**.
- `D`: fato demonstrado em código/fixture anterior; `H`: hipótese condicional; `P`: requisito/decisão/prova ainda pendente. Tudo chamado de `protocolo` ou `estado` neste documento é **proposta**, não sistema implementado.

## 1. Problema e recomendação técnica sem aprovação implícita

O GF não deve considerar confiável uma cópia local de sua própria raiz G1, `installationId`, credencial ou epoch após restauração integral do PC. Uma witness remota honesta pode comparar checkpoints, **mas não pode forçar um verificador malicioso/substituído a consultá-la**, nem distinguir um clone com a mesma chave exportável sem outra fonte de identidade. Assinatura válida + nonce fresco **não** comprova que a autoridade não voltou a uma época revogada. O autoatualizador precisa permanecer **BLOCKED** enquanto faltar a prova.

**Recomendação única, expressamente condicional:** projetar primeiro um **piloto fechado e sem login GitHub**, com raiz G1 provisionada por canal autenticado independente da autoridade editorial TUF, credencial de inscrição de uso único, chave por instalação para prova de posse, head externo que não retrocede por restauração local e cerimônia supervisionada de recuperação. **TPM seria opcional**, não pré-requisito; usuários sem TPM terão limitação contra clonagem. Esta proposta **não escolhe Cloudflare** nem exige conexão por decisão unilateral: um backend conectado só poderá ser usado se o mantenedor aceitar essa dependência. A seleção de plataforma e a exclusão (ou inclusão) do operador comprometido no adversário ficam **PENDENTES**.

**Dois perfis de risco alternativos, ambos ainda sem aceite:**
- **Perfil A — operador honesto**: pretende detectar restauração do PC, replay, perda de resposta, concorrência e reboot da witness, assumindo cliente/verificador e raiz inicial confiáveis. Uma única witness durável **poderia** ser suficiente nesse escopo após ensaios reais.
- **Perfil B — operador ou domínio da witness comprometido (A5 obrigatório)**: precisa ainda de **outro domínio de custódia e continuidade**, monitor/auditoria antifork, root de recuperação independente e tratamento explícito de equivocation. Um único Durable Object/D1, ainda que assine respostas e guarde CAS corretamente, é insuficiente.

Nenhuma escolha entre A/B, online/offline, TPM/software ou operador de recuperação é considerada aceita por silêncio.

## 2. Identidades, autoridades e domínios diferentes

| Entidade proposta | Papel e referência | Proibição de equivalência |
|---|---|---|
| `R_TUF` — raiz editorial | Autenticar releases, alvos, versões, referências e metadados conforme TUF | Não emitir convites G1 nem reiniciar o piso de witness por reset de `timestamp/snapshot` |
| `R_G1` — raiz de autoridade | Autenticar chave de recibo de witness, política de chaves/revogação e fluxo de recuperação; **pin por canal independente** | Não obter confiança inicial exclusivamente de TUF que por sua vez dependa de G1 |
| `witnessHead` | Estado de `installationId+enrollmentEpoch`, sequência, fase, digest, piso, recibos e revogações | Head em RAM/SQLite restaurável pela mesma custódia **não é** prova antioperador |
| `enrollmentAuthority` | Autorizar quem pode criar ou recuperar um registro lógico no piloto | Um identificador enviado pelo cliente ou um JWT anônimo não prova autorização |
| `devicePublicKey` | Prova de posse por instalação; opcionalmente mantida em TPM; `deviceKeyEpoch` próprio | Chave de dispositivo não assina releases nem raiz G1 e não atesta integridade do Windows |
| `installationId` | Identificador aleatório pseudônimo de um **histórico lógico** | UUID no disco não é identidade física nem impede clonagem/reenrollment |
| `recoveryAuthority` | Autoridade independente e documentada para suspender/recuperar/revogar inscrição | Não usar somente a chave ou backup que está sendo declarado comprometido |
| `auditAnchor` (condicional A5) | Segundo domínio que observa sequência/digest e acusa fork/restauração | Duas tabelas ou Workers administrados pela **mesma conta** não são custódia independente |

Contadores obrigatoriamente separados: `tufEpoch`, `witnessKeyEpoch`, `enrollmentEpoch`, `deviceKeyEpoch`, `witnessSequence` e `stage2Generation`. Nenhum pode ser usado indistintamente como prova do outro; `releaseFloor` funcional não deve recuar por rotação de metadados TUF.

## 3. Bootstrap antes de qualquer primeira resposta — modelo não operacional

**Invariantes B8 (todos P):**
- **B8-01:** a distribuição inicial autenticada do GF estabelece `R_TUF` e `R_G1` **por vínculos independentes**, política/época de chaves e parâmetros de protocolo; **primeira conexão não autoriza TOFU**. Se a proveniência da raiz não puder ser comprovada, bloquear autoatualização.
- **B8-02:** a versão antiga do verificador pode carregar pin antigo, inclusive de chave revogada. O servidor não deve **revalidar automaticamente** cliente revogado; é preciso compatibilidade de versão/recuperação que nunca rebaixe a epoch G1, e distribuição/atualização de verificador confiável. Se software arbitrário controla a máquina, essa segurança não pode ser imposta remotamente.
- **B8-03:** chave de assinatura ativa da witness **não** é a raiz offline; uma assinatura de `receipt` deve referenciar `R_G1`, `witnessKeyEpoch`, `installationId`, `enrollmentEpoch`, `txId`, `phase`, `claimDigest`, `prevHead`, `nextHead`, `witnessSequence`, protocolo/domínio e desafio atual.
- **B8-04:** autenticação da resposta **e** do publicador são necessárias; TLS e assinatura de cabeçalho não substituem prova de posse, registro válido ou consistência histórica.
- **B8-05:** uma solicitação de nova inscrição deve ser recusada quando parecer forma de resetar uma revogação existente. Sem identidade não clonável/cadastro independente, **não existe algoritmo perfeito** para reconhecer uma instalação genuinamente nova versus clone silencioso. Isso é limite a aceitar ou mitigação extra a projetar, não problema resolvido por regex, UUID, IP ou MAC.

**Camada de custódia ainda sem decisão:** quem assina `R_G1`, como o verificador obtém essa raiz fora de banda, qual canal distribui correções quando uma versão antiga estiver comprometida e quem autoriza quebra de continuidade após desastre. Nenhum procedimento pode se autoautorizar usando exclusivamente o estado suspeito.

## 4. Inscrição de piloto restrito — máquina de estados e contrato de convite

Estados documentais, não código:
`Unknown → InvitationApproved → PossessionVerified → EnrollmentPrepared → EnrolledCurrent`.
Saídas de erro: `InvitationInvalid`, `DuplicateInvitation`, `IdentityConflict`, `ClonedCredentialSuspected`, `RecoveryRequired`, `RootUntrusted`, `WitnessUnavailable`; **todos bloqueiam self-update**.

**Convite proposto (sem criar um token agora):** aprovado por autoridade externa ao cliente, de uso único, escopo de piloto/identidade lógica, validade limitada, finalidade G1, desafio, raiz/epoch esperada, vínculo criptográfico à chave pública de dispositivo proposta, e restrição de reutilização. Não guardar convite permanente no EXE, no repositório, em logs ou no serviço de telemetria BETA. Código bearer isolado pode ser copiado; a prova de posse e controles de emissão são requisitos adicionais, não solução universal de clonagem.

Fluxo de inscrição futuro **proposto, sujeito à aprovação**:

1. Operador autorizado aprova inclusão no piloto **por canal separado da witness** e entrega uma capacidade de uso único sem credencial global. Identidade humana do avaliador não precisa ser transmitida para a witness; eventual registro de distribuição deve ser segregado, com finalidade LGPD própria.
2. Cliente verifica `R_G1` previamente provisionada, política/epoch e desafio vivo. Falta de raiz confiável significa `RootUntrusted`, nunca gerar pin por resposta do servidor.
3. Instalação propõe par de chaves próprio: **TPM quando disponível/aceito**, ou software quando explicitamente permitido. A chave privada não é enviada. Registrar a modalidade e sua limitação de clonagem sem transmitir HWID bruto.
4. A autoridade verifica convite não consumido, validade, associação ao novo `devicePublicKey`, presença de recuperação/revogação prévia e prova de posse do segredo local. Convite consumido e tentativa diferente → `DuplicateInvitation`; sem novo enrollment automático.
5. Registra atomicamente `installationId`, `enrollmentEpoch`, `deviceKeyEpoch`, `authorizedDeviceKeyId`, `head` genesis, `releaseFloor` inicial validado, `revoked=false` e tombstone/uso do convite. **Não** afirmar atomicidade entre eventual autoridade de convites e witness se forem stores separados; definir transição idempotente de preparo e reconciliação.
6. Recibo de inscrição deve ser **autenticado**, incluir digest da proposta, `enrollmentEpoch`, sequência inicial, autoridade G1, epoch de chave e identificador do convite consumido (não o token). Perda de resposta pós-commit → `EnrollmentUnknown`, reconsultar **a mesma inscrição**, não emitir novo `installationId`.
7. Autoatualização segue **BLOCKED** até validação do estado externo atual, do contrato TUF e dos demais gates. `EnrolledCurrent` é pré-requisito de avaliação, **não `CanSelect`**.

**Conflito inevitável da distribuição pública:** aprovação humana individual pode ser viável num piloto pequeno, mas não escala para todos os usuários gratuitos. **Não** declarar que a solução de convites resolve automaticamente o bootstrap público. Exigirá um modelo posterior específico.

## 5. Recuperação: critérios de autoridade e transições

Estados documentais: `EnrolledCurrent`, `RecoveryRequested`, `FreezePending`, `RecoveryAuthorized`, `KeyRotationPending`, `RecoveryCommitted`, `Revoked`, `RecoveryBlocked`, `RootRecoveryRequired`. **Nenhum estado de recuperação autoriza self-update**. Somente `EnrolledCurrent`, após provas atuais G1/G2/G5/G6/G8 completas, poderia futuramente ser elegível.

**Fluxo proposto para piloto fechado**, ainda não aprovado:

1. **Congelar** publicação de novos intents e registrar `recoveryId` proposto, motivo e identidade lógica anterior. Se o freeze não for durável ou o head estiver incerto, `RecoveryBlocked`.
2. Conferir **por canal independente** que a solicitação é legítima (ex.: operador humano designado, evidência de posse anterior se ainda disponível, autorização de convite de recuperação **separada**). **Quem perdeu todos os fatores não obtém confiança automaticamente**; definir procedimento de resolução e risco de sequestro.
3. Consultar o maior checkpoint confiável e recibos `IntentCommit/TerminalCommit`, `releaseFloor`, `enrollmentEpoch`, `deviceKeyEpoch`, revogações, `pending` e resultados ambíguos. Em caso de witness restaurada/comprometida, consultar **auditAnchor externo** se A5 integrar o escopo; sem esse recurso, **não** atestar continuidade.
4. Validar nova chave/posse (TPM novo ou software), com `newKeyEpoch > oldKeyEpoch` e transição `RecoveryCommitted` sobre **o mesmo head ou sua extensão verificável**; registrar atômica e irreversivelmente invalidação da chave anterior e novo vínculo. Revogação do dispositivo antigo é parte do mesmo contrato, não ação assíncrona presumida.
5. O recibo de recuperação assinado precisa conter `old/new key IDs`, `installationId`, `old/new enrollmentEpoch`, `recoveryId`, `previous/next head`, `releaseFloor`, autoridade que aprovou (identificador mínimo), epoch de assinatura, política de reutilização e status. **Não publicar nomes de avaliadores na witness**.
6. Após resposta perdida, consultar `recoveryId` e `claimDigest` autenticadamente; repetir o mesmo pedido não deve criar nova epoch ou revogar outra chave. Corpo divergente → conflito fail-closed.
7. Reabrir self-update **somente depois** de reconciliação com os diários locais; se os arquivos Stage2/TUF desapareceram, não reconstruir a confiança com um ZIP antigo nem trocar seletor por plausibilidade.

**Nenhuma recuperação pode usar o próprio token perdido ou uma chave revogada como autoridade única**. Não armazenar código de recuperação como único fator dentro do mesmo snapshot local. O procedimento exato, quorum, custódia offline e responsável são **P**.

## 6. Matriz de casos físicos e adversariais — nenhuma recuperação executada

| Situação | O que ainda pode ser demonstrado | Estado/conduta fail-closed proposta |
|---|---|---|
| Reinstalação Windows com chave não exportável perdida | Possível restaurar histórico apenas com autorização independente e testemunha intacta | `RecoveryRequested`; chave nova só após freeze/recovery; **nunca zerar head** |
| Troca de placa-mãe / TPM limpo | Chave TPM original pode ter sido destruída | `RecoveryBlocked` até aprovação independente, revogação da chave antiga e preservação de sequência/piso |
| Reinstalação preservando a mesma chave software | Prova de posse funciona, mas cópia clonada também pode tê-la | `IdentityConflict` se houver divergência; **não declarar anti-clone** por mesmo segredo |
| Clone simultâneo em dois PCs com a mesma credencial | CAS pode serializar operações e revelar conflito de contextos | `ClonedCredentialSuspected`; clonagem silenciosa sem disputa pode ser **indetectável** sem fator extra |
| Convite reenviado em outro dispositivo | Mesmo convite não pode ser consumido duas vezes | `DuplicateInvitation`; revogação/recovery somente por nova autorização |
| Resposta de inscrição ou recuperação perdida depois do CAS | Consulta idempotente do mesmo `enrollmentId/recoveryId` deve recuperar recibo | `EnrollmentUnknown/RecoveryBlocked` até prova; jamais duplicar inscrição |
| Expiração/revogação após `IntentCommit`, antes de terminal | Head remoto pode ter avançado sem efeito local definitivo | Preservar `pending` e recibos; `Revoked/RecoveryBlocked`, não inventar `TerminalCommit` |
| Witness online indisponível, 429, sem quota | Estado local pode estar atrasado ou remote-ahead | `WitnessUnavailable`; reconsulta posterior com mesmo txId; apenas self-update suspenso |
| Snapshot da witness restaurado por administrador honesto acidentalmente | Estado anterior potencialmente assinado após recuperação | Se existir `auditAnchor` independente, comparar; senão **história monotônica do servidor não demonstrada**; suspender |
| Operador malicioso com chave de assinatura atual | Pode assinar fork/head falso com nonce novo | **Single witness insuficiente**, `RootRecoveryRequired` e segunda autoridade; não classificar como ataque impossível |
| Perda da raiz offline G1 ou comprometimento total de custódia | Não há autoridade autenticada para continuar | Suspender, recuperar só por procedimento de distribuição independente ainda não especificado |
| Relógio ou rede arbitrariamente atrasados | Não transformam recibo antigo em head atual | Expiração/freshness requer política independente; offline não conceder permissão |

**Limites obrigatórios:** um TPM opcional não demonstra integridade de processo; um serviço remoto não pode forçar binário substituído a obedecer; mesmo uma witness durável sob única administração pode regredir via PITR. Estes limites **não são resolvidos** nesta rodada.

## 7. Revogação, rotação e antifork — três transições diferentes

**A. Rotação da chave de recibo G1 (`witnessKeyEpoch`):**
- `R_G1` offline, em custódia separada e ainda não definida, autoriza o conjunto de chaves/epoch válido. Preservar vínculo autenticado epoch anterior→nova, intervalo de aceitação, thresholds e política de revogação; assinatura sob chave antiga **isoladamente** não prova nova época se antiga estiver comprometida.
- Conservar `witnessSequence` e os recibos durante a rotação. Um pin público revertido com o disco deve encontrar **recusa do servidor/âncora independente**, e cliente desatualizado incapaz de validar deve permanecer bloqueado.
- Recuperação da raiz G1 comprometida não é rotação ordinária pelo mesmo segredo. Exige **outro root/canal de distribuição** validado, com política explícita sobre congelamento, rollback, fork e revogações.

**B. Rotação da chave da instalação (`deviceKeyEpoch`):**
- Com chave antiga ainda confiável, autorização da witness + prova de posse **das duas chaves** pode vincular a transição; se chave antiga perdida/comprometida, somente recuperação independente, não rekey autoaprovado.
- Chave antiga passa a revogada no mesmo commit que admite a nova e mantém `enrollmentEpoch` ou incrementa em regra documentada. Não criar nova instalação para evitar tombstone.
- Desativar TPM, trocar componente e operar em modo portátil não devem silenciar essa política; sem prova disponível, `RecoveryBlocked`.

**C. Revogação da inscrição (`enrollmentEpoch`):**
- Estado `Revoked` com tombstone mínimo, associado à identidade lógica anterior e ao último head verificável. Bloquear publicações de quaisquer chaves antigas, inclusive se o disco voltar à imagem original.
- Se houver `IntentCommit` aberto, registrar `RevokedWithPending` ou equivalente; não apagar `pending` nem presumir `rolled_back`/sucesso.
- Pedido de exclusão de dados pessoais requer política LGPD separada: não alegar retenção vitalícia de tombstones nem exclusão imediata de backups sem prova.

**Antifork sob A5:** dois bancos/objetos sob a mesma conta Cloudflare **não** formam segundo domínio de custódia. Se A5 for obrigatório, registrar checkpoints/consistência em custódia independente, monitorada e capaz de contestar restauração/fork, com política de privacidade para evitar publicação de identificadores. **Nenhum componente antifork externo foi escolhido ou criado.**

## 8. Privacidade, disponibilidade e usuário

- **Finalidade G1 exclusiva:** segurança de atualizações do próprio GF. Não unir com consentimento, UUID, coleta de logs, retenção de 7 dias ou infraestrutura da BETA .66. Base legal, informação ao titular, responsáveis, direitos e retenção são **decisões pendentes**; consentimento não é a única base legal possível e não pode ser herdado sem avaliação.
- **Conjunto mínimo proposto:** `installationId` pseudônimo e epoch; IDs públicos de chave; status de inscrição; `txId/phase/claimDigest`, `head seq/digest`, recibos e revogações; timestamps mínimos e políticas. **Não transmitir** catálogo de apps instalados, HWID bruto, nome de usuário, arquivos pessoais, CPF ou identificador GitHub de avaliador. IP de transporte e logs do fornecedor podem existir e exigem mapeamento próprio.
- **Retenção NÃO aprovada:** faixas discutidas no Lote 7 (logs curtos, recibos com janela superior ao máximo retry/offline, head durante inscrição, tombstones com justificativa jurídica), sujeitas a recuperação/idempotência; não copiar TTL da telemetria. Backups/PITR são cópias com janelas próprias e podem atrasar exclusão efetiva.
- **Online/offline em aberto:** se escolher testemunha online obrigatória, **somente autoatualização segura do próprio GF** dependeria dela. Funcionamento local do GF, desinstalações, atualização de outros aplicativos e Windows Update não herdam bloqueio G1. Se usuário exigir self-update sempre offline, esta arquitetura **não resolve frescor/rollback integral local**; especificar outro fluxo, não usar cache como prova atual.
- **Custos não aceitos:** comparação DO SQLite / Workers+D1 do Lote 6 continua apenas hipótese de fornecedor. Free-tier pode ser insuficiente para abuso/retries e não assegura continuidade, SLA, proteção do operador, suporte ou custos de auditoria. Nenhum domínio/conta selecionado.

## 9. Decisões do mantenedor — checklist de aprovação, não formulário nem aceite

**A condição de saída desta fase é uma decisão explícita**, não apenas este documento. As opções abaixo são alternativas de engenharia; **todas continuam PENDENTES**:

| ID | Decisão a aprovar | Recomendação técnica para piloto restrito | Se não for aceita |
|---|---|---|---|
| **M8-01** | Adversário: operador comprometido A5 deve ser coberto **desde o primeiro piloto**? | Não presumir; se sim, exigir segundo domínio antifork antes de piloto | Single-witness **não pode** ser apresentado como suficiente sem aceitação expressa do risco A5 |
| **M8-02** | Online obrigatório **só** durante self-update GF? | Estudar como regra de piloto, sem prejudicar demais recursos | Se self-update offline for exigência, redefinir arquitetura antes de implementação |
| **M8-03** | TPM de hardware obrigatório, opcional ou ausente? | Opcional e explicitamente classificado por nível de proteção | Se obrigatório, limita compatibilidade; se não usado, clonagem da credencial de software permanece ameaça |
| **M8-04** | Quem é a autoridade de convite e recuperação, e como provar identidade sem login do usuário? | Piloto por convite, decisão humana autenticada por outro canal | Sem autoridade definida, não emitir credenciais nem criar enrollment |
| **M8-05** | Recuperação de chave perdida/TPM trocado: fluxo supervisionado e fatores necessários | Freeze + autorização independente + revogação atômica + continuidade de head | Sem recovery, recusa permanente após perda de chave; recovery só pelo disco é inseguro |
| **M8-06** | Quem controla `R_G1`, responde a comprometimento e distribui nova raiz/verificador? | Custódia separada e recuperação offline de múltiplos fatores a discutir | Sem autoridade válida, assinatura e epoch não podem ser confiadas |
| **M8-07** | Política de perda/restauração do servidor, A5 e segundo domínio | Se A5 obrigatório, sem serviço único como fonte última | Sem prova de alta marca independente, rollback do servidor pode passar despercebido |
| **M8-08** | Identidade pseudônima, finalidade LGPD, base legal, retenção e exclusão | Minimização + canal de informação separado da BETA, revisão de dados pessoais | Sem avaliação de privacidade, não coletar nem conectar instalação |
| **M8-09** | Plataforma e orçamento do piloto (DO, D1, outro ou nenhum) | Considerar DO somente após escopo/risco definido; validar quotas e termos | Sem aprovação, nenhuma infraestrutura; não reduzir segurança para caber no plano gratuito |
| **M8-10** | Critérios de versão/inscrição, recuperação pública, suporte e responsável humano | Testar protocolo em ambiente descartável sob autorização futura separada | Piloto por convite não vira automaticamente produto público |
| **M8-11** | Análise do verificador comprometido (A1/A2) e distribuição confiável | Delimitar ameaça: root/verificador íntegros ou tecnologia de proteção independente | Não alegar proteção contra binário deliberadamente substituído |
| **M8-12** | Revisão qualificada e momento de usar WORK | Revisar contrato após opções aprovadas, antes de chaves/serviços reais | Parecer automatizado não é certificação nem substitui D6-02 G2 |

**Sem resposta não há aceite**, escolha de provedor, exclusão de A5, autenticação de inscrição, conexão obrigatória nem autorização implícita para criar serviços.

## 10. Critérios futuros para testes, sem execução neste lote

| ID | Evidência de aceite futura (todas P) |
|---|---|
| **E8-01** | Inscrição aprovada com convite de uso único, binding a chave pública e root G1 preexistente; convite duplicado, alterado, expirado ou troca silenciosa de `installationId` recusados |
| **E8-02** | Clone com credencial de software ou TPM, ameaça e detecção delimitadas sem alegar detecção de clone silencioso por magia |
| **E8-03** | Recuperação pós-TPM limpo e reinstalação sem reset do `witnessSequence`, `releaseFloor` e revogações; chave antiga recusada, mesmo após snapshot do PC |
| **E8-04** | Reboot real de witness com CAS e recibos persistentes; perda de resposta pós-commit e `txId` repetido com mesmo/diferente corpo reconciliados, inclusive entre fases |
| **E8-05** | Rotação e revogação de `R_G1` e chave de instalação com épocas separadas, política de root e anti-replay sem pin restaurável ser autoridade |
| **E8-06** | Se A5 entrar no escopo: restauração real do banco da witness e fork assinado detectados pelo **segundo domínio**; prova de independência administrativa e recuperação |
| **E8-07** | Falha de rede/quotas/credenciais/relógio mantém self-update bloqueado, preserva pending e deixa funções independentes do GF operantes |
| **E8-08** | Revisão de dados e retenção/backups/TPM/segurança Windows por pessoas qualificadas, avaliação G2 D6-02, G5/G6 READY e G8 físicas antes de qualquer integração |
| **E8-09** | Continuidade da autoridade verificada sob binário antigo/raiz restaurada — se verificador puder ser substituído livremente, declarar essa ameaça não solucionada, sem gate fechado |

**Não integrar** `FixtureSession.Begin/Confirm`, não prover `CanSelect/CanConfirm` e não mudar roadmap. O que foi provado nos Lotes anteriores são vínculos e CAS em **fixtures**; nenhuma das evidências E8 está cumprida hoje.

## 11. Referências e correções de precisão

Fontes herdadas e conferidas no repositório: ADR L6/L7, matriz G1 e parecer WORK. Fontes oficiais consultadas para esta decisão documental (29/10/2026):
- [Microsoft — Solucionar problemas/limpar TPM](https://learn.microsoft.com/pt-br/windows/security/hardware-security/tpm/initialize-and-configure-ownership-of-the-tpm): limpar TPM pode eliminar chaves associadas e dados protegidos; mudança de hardware exige recuperação planejada. Nenhum comando TPM foi executado.
- [Cloudflare — Durable Objects SQLite storage](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/): armazenamento transacional por objeto e **PITR em até 30 dias**; durabilidade não implica imunidade a restore administrativo. Não foi escolhido ou provisionado serviço.
- [Cloudflare — persistência de Durable Objects](https://developers.cloudflare.com/durable-objects/best-practices/access-durable-objects-storage/): estado em memória é perdido ao reiniciar/evict; persistir head+recibos exige storage próprio.
- [ANPD — Guia legítimo interesse](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia_orientativo_hipoteses_legais_tratamento_de_dados_pessoais_legitimo_interesse), [Governo Digital — guia LGPD, pseudonimização](https://www.gov.br/governodigital/pt-br/privacidade-e-seguranca/guias/guia_lgpd.pdf): base legal a avaliar caso a caso; pseudonimização não é anonimização automaticamente.

**Nenhuma fonte certifica o protocolo proposto.** Limites de provedor, modelos de custódia, risco de hardware e segurança operacional ainda precisam de qualificação humana antes de chaves ou serviços reais.

## 12. Resultado e restrições permanentes

**Entregável deste lote:** somente contrato documental proposto, esquema de convite, máquina de estados, roteiro de recuperação, matriz de clones/TPM, separação de chaves, A5 explícito e decisões reservadas ao mantenedor. Nenhuma das alternativas foi aceita, nenhum dado foi coletado, nenhum código escrito/testado e nenhuma nova CI foi executada.

**PR #65 Draft, main, BETA .66, infraestrutura, telemetria desativada, gates G1–G8 (0/8) preservados.** Revisão WORK pontual pode ser útil **após** o mantenedor deliberar escopo A5/online/TPM/custódia; não é necessária outra suíte sintética neste momento.
