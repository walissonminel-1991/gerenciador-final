# GERENCIADOR FINAL — Lote 7: proposta de decisão G1, modelo de ameaça e recuperação
**29/10/2026 | ADR-7 | Status: RECOMENDAÇÃO CONDICIONAL, sem aprovação nem implementação**
**G1–G8 BLOCKED; 0/8 gates operacionais homologados**

## 0. Baseline, mandato e categorias de evidência

- Repositório `walissonminel-1991/gerenciador-final`; branch única de documentação `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de entrada conferido: `83ecab9ad6e985e28a9425ab0d55d9085fe4ac42`.
- Documento obrigatório: `docs/CHAT-ALTO-LOTE6-ADR-TESTEMUNHA-G1-INDEPENDENTE-20261029.md`. Evidência anterior: `docs/CHAT-ALTO-LOTE5-MESMOS-BYTES-TUF-STAGE2-L4-CHECKPOINT-20261029.md`, CI [40922528166](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40922528166), seis fixtures aprovadas, não witness operacional; parecer WORK G1/G2/G8 compartilhado pelo mantenedor. As fontes externas estão na seção 11.
- PR #65 Draft, `work-integracao-final-20261027`, SHA de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Preservar `main`, BETA .66 e sua infraestrutura de diagnóstico, telemetria desligada, endpoints, secrets, domínio e consentimento. Sem merge, release, deploy ou execução de serviço.
- **D** = demonstrado pela fixture existente, **H** = premissa assumida pelo desenho, **P** = pendente de decisão/ensaio. Nem este ADR, nem o sucesso da CI, encerram G1 ou concedem `CanSelect/CanConfirm`.
- **Somente documentação** neste lote. Nenhum protótipo, build, teste, nova CI, conta ou chave operacional.

## 1. Decisão técnica principal — RECOMENDAÇÃO, NÃO ACEITE

**Escolher como arquitetura de referência para um eventual piloto autorizado: witness G1 conectada, isolada da BETA, com checkpoint monotônico por instalação e recibos de `IntentCommit/TerminalCommit` serializados em Durable Objects com SQLite; manter Workers+D1 como alternativa funcional.**

**Perfil de ameaça inicialmente recomendado (sujeito à aprovação do mantenedor):** exigir proteção frente à restauração integral do **disco do cliente** e a falhas comuns de rede/crash/concorrência; exigir proteção contra replay/duplicação e alterações do mesmo usuário em arquivos **sob a premissa de que o cliente verificador e as raízes provisionadas permanecem honestos**. **Não declarar resistência ao comprometimento da conta, do operador, do código do cliente, do segredo de assinatura ou a snapshots simultâneos do servidor e seu segundo domínio.** Esses cenários continuam riscos explicitamente não cobertos; se a política do mantenedor os incluir como obrigatórios, um único DO/D1 **não é arquitetura suficiente**. Não confundir "fora do escopo do primeiro piloto" com segurança demonstrada; exigir aceitação **explícita** do risco.

**Papel do DO:** manter atomicamente `head + (installationId,phase,txId,claimDigest→receipt)`, tombstones e restrições de fase, com armazenamento persistente de SQLite por instalação. **Não** confiar em objetos JavaScript em RAM, lock de processo ou em mera assinatura de recibo; o estado atual precisa sobreviver reinício e ser consultado autenticadamente. O DO ser single-threaded e possuir storage transacional não resolve sozinho desenho de transações, autenticação de publicador, gestão de chaves, anti-equivocação nem restauração administrativa (PITR). Isso é **candidato para estudo**, não homologação do produto nem serviço autorizado.

**Razões:**
1. Lotes 3–5 já ligaram, somente em fixtures, o digest TUF `root/timestamp/snapshot/targets→ZIP→bundle`, `Stage2 intent`, operação Stage9 e duas fases CAS. O gargalo remanescente é a **independência/durabilidade/autoridade**, não mais uma suíte de hashes em RAM.
2. Uma witness hospedada fora do disco detectaria rollback local **se** a inscrição, raiz/verificador, sequência e estado do serviço ainda fossem confiáveis.
3. A arquitetura deve oferecer fail-closed **somente na autoatualização do GF**, preservando atualização de outros aplicativos, Windows Update e funções independentes.
4. Caso a ameaça inclua administrador da witness malicioso, PITR e chaves sob um só operador podem permitir história regressiva assinada; prova externa de continuidade, operada por domínio independente e monitorada, passa a ser requisito **antes de homologar esse adversário**, não melhoria opcional posterior.
5. **Nenhuma plataforma gratuita elimina a necessidade de decisão sobre custódia, recuperação, legislação e manutenção.**

## 2. A1–A7 — limites aceitos como hipótese, pendências e adversários

| ID | Ameaça | Proteção exigida no perfil inicial proposto | Limite não coberto e decisão |
|---|---|---|---|
| **A1** | Restauração integral do Windows/arquivos/pin/estado | **Obrigatória**: head externo honesto e chave G1 previamente confiada devem rejeitar epoch/seq/head antigo. Nunca confiar no pin restaurado sem prova de continuidade | Se o invasor também substitui e **executa um verificador arbitrário**, não se pode obrigá-lo criptograficamente a consultar a witness; exigir cadeia confiável de distribuição e escopo de verificador honesto |
| **A2** | Escritor local não cooperativo, malware mesmo usuário ou administrador | **Parcial**: assinaturas de servidor, autenticação do publicador, CAS e ligação da identidade do bundle até o uso; TOCTOU precisa G8/G5/G6 | Malware com privilégio suficiente para substituir processo/controles e roubar credenciais está **fora da garantia inicial**, não é declarado resolvido |
| **A3** | Reinstalação, clone, troca de hardware, limpeza de TPM | **Fail-closed** para reenrollment não comprovado; fluxo de recuperação/custódia manual no piloto com prova adicional | Sem identificador não clonável/identidade independente não é possível distinguir instalação nova de clone antigo com segurança apenas por UUID de disco; política de inscrição futura deve evitar negação indevida de serviço |
| **A4** | Timeout, resposta perdida, concorrência e reboot do backend | **Obrigatória**: CAS idempotente **durável**, mesma resposta para `phase+txId+claimDigest`, conflito de corpo e recuperação após restart | Ainda **P**; L5 apenas provou modelo em RAM, não servidor |
| **A5** | Comprometimento de operador, banco, assinatura ou mesmo domínio de cloud | **Não coberto por single-witness**; opção futura de segundo domínio/registro de transparência com monitor independente | Não encerrar G1 sob A5 sem segunda autoridade, provas de consistência/fork e recuperação root offline testadas. A5 requer **decisão explícita do mantenedor** |
| **A6** | Falha elétrica/rename/flush NTFS, I/O tardio ou energia da witness | Bloqueio/quarentena/reconciliação por estado desconhecido; não promover `pending` | **P**: validar hardware e testemunha durável. `File.Move` observado antes de pausa não prova efeito posterior ao deadline |
| **A7** | Rastreamento, correlação, vazamento e uso indevido de metadados | **Obrigatória**: minimização, finalidade própria, controles de acesso, retenção, exclusão e avaliação LGPD | Pseudônimo com IP/horário/credencial pode permitir associação; nenhuma tabela pode ser denominada anônima por conveniência |

**Premissas para eventual piloto restrito:** verificador distribuído por origem confiável (ainda não provada), servidor e operador honestos, cliente conectado no momento de atualização e público de avaliadores explicitamente autorizado. Se o mantenedor não aceitar essas premissas, o piloto seguro **não tem desenho selecionado**.

## 3. Bootstrap sem TOFU e enrollment: distinguir três coisas

**(i) Autoridade da witness.** O binário/atualizador precisa conhecer `R_G1` por canal de distribuição independente, autenticado **antes** de qualquer primeira resposta; a raiz editorial `R_TUF` não pode depender circularmente de G1. Política de mudança de chave `R_G1` exige continuidade autenticada, versão/época e capacidade de revogação; uma versão antiga do programa não pode ser tratada como prova de chave atual. **Não criar chave privada operacional no binário ou neste ADR.**

**(ii) Identidade da instalação.** `installationId` aleatório pseudônimo e `enrollmentEpoch` registram um sujeito lógico; sozinhos **não provam** equipamento nem impedem reidentificação após reinstalação. Para piloto por convite, proposta **condicional**: emissão de credencial de enrollment com uso único e curta validade, autorizado por mantenedor, ligada à chave pública por instalação e ao root G1 já autenticado. A credencial deve ser entregue em canal confiável e ficar inválida após uso. **Um código bearer isolado é copiável; não garante anti-clonagem**. Prova de posse da chave reduz replay, mas não demonstra integridade do sistema.

**(iii) Chave do dispositivo.** Se houver TPM adequado, permitir chave não exportável por Microsoft Platform Crypto Provider como **fortalecimento opcional** contra clonagem da credencial. TPM **não** substitui confiança externa, não autentica TUF, não prova honestidade do cliente e pode ser limpo/substituído. Em PCs sem TPM disponível, uma chave em software ainda é possível, **mas sua clonagem passa a ser limite assumido**, exigindo política de risco e recuperação no piloto; **nunca** permitir downgrade para enrolamento automático indiscriminado.

**Decisão real ainda requerida:** quem tem autoridade para aprovar inscrição inicial sem criar login para o usuário? Modelo recomendado somente para piloto fechado: mantenedor autoriza cada avaliador por canal separado, com one-time enrollment/recovery; operação supervisionada não se traduz automaticamente em produto público escalável. Para distribuição pública gratuita, é indispensável outro fluxo de bootstrap/recovery com custo/abusos dimensionados.

## 4. Recuperação após reinstalação, troca de hardware e perda da witness

Estados propostos: `NotEnrolled`, `EnrolledCurrent`, `RecoveryPending`, `Revoked`, `AnchorUnavailable`, `RootRecoveryRequired`. **Todos menos `EnrolledCurrent` impedem self-update**. A identidade antiga nunca é silenciosamente substituída por uma nova como resposta a rollback.

| Cenário | Conduta fail-closed e saída que exigirá comprovação futura |
|---|---|
| Reinstalar Windows mantendo chave/registro externo | Verificar prova de posse da mesma inscrição e `enrollmentEpoch` contra witness. Se TPM não puder reusar chave, **RecoveryPending**, não TOFU |
| Trocar placa-mãe/TPM ou limpar TPM | Revogar sessão/chave antiga via autoridade de recuperação separada; aprovar chave nova e **preservar head/releaseFloor/epoch**, nunca zerá-los; registrar `recoveryId` vinculado ao head anterior |
| Clonar diretório do GF para outro PC | Se ambos usarem a mesma credencial e não houver fator não clonável, não alegar distinção garantida; detectar concorrência/contexts contraditórios e bloquear; nenhuma "nova inscrição" automática |
| Perder apenas recibo local, compromisso remoto existente | Buscar recibo autenticado por `installation+phase+txId`, reapresentar mesma proposta; revalidar TUF/journals. Estado remoto-ahead **não** é prova de ataque nem autorização |
| Perder diário local inteiro, witness preservada | `RecoveryPending` até reconstrução por protocolo autenticado que não retroceda piso nem dê seleção. Não "ressincronizar" por reaproveitar bundle local |
| Perder banco do servidor ou restaurá-lo por PITR | Sem registro independente do último head, impossibilidade de provar não-regressão no domínio de operador. **Bloquear**, preservar evidências e submeter a decisão de custódia/recovery externa. Não criar witness vazia |
| Perder/comprometer chave de assinatura `R_G1` ou operador | Suspender autoatualização dependente; revogação e rebootstrap somente por root/autoridade **separados** da chave comprometida, com auditoria e aprovação; pode exigir nova distribuição verificada |
| Conta não tem cota/servidor offline | Bloquear apenas self-update, manter o `txId`/phase/`pending` e retry com backoff depois; não reiniciar orçamento G8 |

**Rascunho de cerimônia de recuperação por convite (não implementação):** solicitação humana → congelar sessão antiga → verificar prova de posse e canal de autorização independente → inspecionar head/recibos/piso e motivo da perda → emitir `recoveryId` de uso único e duração limitada → revogar credencial anterior **antes** de permitir nova publicação → registrar no mesmo head CAS uma transição `RecoveryCommitted` vinculada à época anterior → exigir consulta autenticada antes de reabrir self-update. A ordem e atomicidade requerem prova; o rascunho **não** resolve operadora maliciosa.

**Não escolher valores de TTL nem re-enrollment de forma silenciosa.** Se a autoridade original for completamente perdida ou comprometida, continuar bloqueado; um código de recuperação guardado na mesma imagem restaurável não resolve A1/A3.

## 5. Rotação, revogação, antifork e confiança do operador

- **Três planos de rotação independentes:** (1) `R_TUF` editorial e regras TUF por papéis/época, (2) `R_G1` e chaves de assinatura/recovery, (3) `devicePublicKey` da instalação. Alterar um não autoriza retroceder outro.
- `R_G1` deve ser raiz offline autenticada com política de quorum/dual-control a definir; recibo online assinado inclui `domain/schema, repoId, installation, enrollmentEpoch, keyEpoch, txId, phase, claimDigest, previousHead, nextHead, sequence, status, challenge`. Assinatura/challenge exigem proveniência e **não** garantem servidor monotônico malicioso.
- **Revogação:** servidor registra estado `Revoked`/tombstone e impedimento a nova publicação; eliminação física de registro precisa avaliação LGPD e de segurança. Se expiração ou revogação ocorrer entre intent e terminal, manter estado recuperável sem simular "terminal concluído"; exigir política explícita de fechamento.
- **Antifork sob operador honesto:** CAS atômico dá head único e conserva recibo. Sob **A5**, operadora pode restaurar snapshot e assinar novos heads: o CAS local dentro do mesmo domínio não detecta. Para cobrir A5, requer **domínio administrativo independente**, recebimento/monitoria de checkpoints assinados, consistência/anti-equivocação e resposta a bifurcação. Dois serviços sob a mesma conta/admin **não** equivalem a independência. Log público com pseudônimo+sequência pode violar minimização; preferir apenas commitments agregados ou outro mecanismo privado com monitor independente, após revisão.
- **Autenticação do publicador:** nunca colocar token global de escrita ou segredo de assinatura do servidor no EXE; credencial por instalação com prova de posse, rate limit e autorização explícita por fase. Sem esse mecanismo, atacante que conhece `installationId` poderia disputar CAS legítimo.

## 6. Retenção/privacidade LGPD — parâmetros somente para consulta

**Natureza:** um pseudônimo único e durável, IP e marca temporal podem permitir associação/correlação; tratá-los como **potencialmente pessoais**, com base legal, finalidade e minimização própria. Não presumir que só consentimento é base legal nem que o consentimento de diagnósticos BETA autoriza witness. A ANPD tem guia para análise de legítimo interesse e teste de balanceamento; **não foi feita análise jurídica nem definida base legal neste lote**.

**Payload candidato mínimo:** pseudônimo de instalação e época, identificação pública de chave (se houver), `head seq/phase/digest`, digest da proposta/recibo/txId, status de revogação, prazo de retenção. Não transmitir programas instalados, nomes de arquivos de usuário, caminhos pessoais, serial de hardware, IP em payload ou telemetria do GF. **IP de transporte e logs do fornecedor podem existir**, requer exame contratual; não prometer anonimato.

**Faixas preliminares a submeter ao mantenedor, NÃO políticas nem provas:**

| Classe de dado | Prazo *apenas sugerido para avaliar* | Restrição operacional e de privacidade |
|---|---|---|
| Logs operacionais com IP e timestamp | alvo de minimização de até **7 dias**, se fornecedor permitir; investigar logs/reserva de incidente e processamento pelo provedor | autorizações de acesso, pseudonimização e direitos dos titulares; não afirmar eliminação de backup antes de prazo do provedor |
| Recibos detalhados `(phase,txId,claimDigest)` | janela inicial ilustrativa de **90 dias**, sujeita a uma política máxima de offline/recovery mais longa ou mais curta | jamais remover referência necessária para idempotência **dentro** da janela aceita; após compactação, preservar provas resumidas de continuidade |
| Estado `head` ativo e metadados mínimos de inscrição | duração da inscrição ativa + período mínimo para recuperação definido em política | ao final, avaliar exclusão segura; apagar o head sem tombstone pode abrir reenrollment ilícito |
| Revogações e tombstones | **nenhum TTL fixado** antes de revisão jurídica e de risco de reenrollment; propor registro mínimo com prazo justificável, não retenção vitalícia automática | direito de exclusão e necessidade de prevenção de fraude podem colidir; decisão humana, base legal e procedimento transparentes |
| Backups, PITR e auditoria externa | conforme janelas verificadas do fornecedor e outra autoridade, não presumir mesmo tempo de logs ativos | D1 e DO SQLite oferecem PITR de até **30 dias** segundo documentação consultada, logo a eliminação dos dados ativos **não implica** exclusão imediata de cópias de restauração |

**UX:** antes da primeira conexão informar finalidade de segurança, dados mínimos, se a witness é necessária para autoatualizador, retenção validada, titular e canal de direitos; não pedir login GitHub; o fluxo de aceite não deve herdar SIM/NÃO da BETA. **Sem aprovação, nenhuma coleta.** A própria base legal e a necessidade ou não de consentimento específico demandam decisão independente.

## 7. Offline, compatibilidade, custos e produto gratuito

- Proposta condicional: **online somente para a autoatualização do próprio GF**, não para iniciar, atualizar outros aplicativos, desinstalar, limpar ou executar verificações locais independentes. Sem serviço: `Autoatualização do GF indisponível sem conexão. As demais funções continuam disponíveis.`
- Não revalidar um recibo local como se fosse frescor online; uma hora de relógio do Windows, assinatura TUF ou nonce guardado no mesmo disco não substitui a witness. Pacote offline/manual é projeto distinto e exige homologação própria.
- **TPM opcional** apenas se a compatibilidade com máquinas antigas e versão portátil permitir; falha de TPM gera `RecoveryPending` para credenciais vinculadas ao TPM, não eliminação de dados ou bloqueio de todo GF.
- DO SQLite Free é tecnicamente elegível (Workers Free, quotas), mas limite excedido provoca falha da operação. Docs atuais indicam **100.000 requests/dia** DO Free e PITR de até 30 dias; Workers+D1 tem limites próprios de leitura/escrita/armazenamento, sem garantia de SLO. **Custo mínimo estimado para piloto de baixa frequência pode ser US$ 0 se respeitar quotas**, mas não calcular isso como SLA, orçamento, disponibilidade nem preço garantido. Custos de domínio, chaves, recovery manual, auditoria, privacidade e suporte podem dominar o provedor.
- Mesmo separando G1 da BETA .66, se ambos dependem da mesma conta Cloudflare, **não existe independência de custódia contra A5**; para o perfil A1/A4 com operador honesto, separação lógica e de credenciais é requisito mínimo ainda não executado.

## 8. Matriz de decisões reservadas ao mantenedor — NENHUMA presumida

| Decisão | Default técnico recomendado ao avaliar um piloto | Alternativa e consequência | Aprovação atual |
|---|---|---|---|
| **M1: escopo A5** | A1/A4 e falhas locais na premissa de verificador honesto; **A5 explicitamente fora da garantia inicial** | Incluir A5 → segundo domínio auditado e custo/operação maiores; sem isso, **não iniciar piloto** | **PENDENTE — não aceita por silêncio** |
| **M2: conexão** | Obrigatória somente para **self-update GF** | Exigir offline absoluto → arquitetura conectada inviável como autorização de frescor; estudar mecanismo offline independente | **PENDENTE** |
| **M3: TPM** | Opcional; software fallback marcado como clonável/risco residual | Obrigatório → exclui alguns computadores; nenhum → risco de clone aumenta | **PENDENTE** |
| **M4: plataforma** | DO SQLite dedicado para estudo, Workers+D1 separado como plano B | Outra provedora/solução, com custo e CAS/persistência a verificar | **PENDENTE, sem compra/deploy** |
| **M5: enrollment público** | Piloto restrito com aprovação individual e credencial curta de uso único | Público sem login e sem operador → falta método seguro de impedir reapresentação como novo após clonagem; não fingir que UUID basta | **PENDENTE** |
| **M6: recovery** | Estado bloqueado até cerimônia autorizada, revogação e continuidade de head/época | Recuperação automática por dados do disco → vulnerável a A1/A3 | **PENDENTE** |
| **M7: privacidade e retenção** | Separar BETA; estudar logs ≤7 dias, recibos ~90 dias, head enquanto inscrito e tombstones justificados | Retenção inferior pode quebrar retry/offline; maior amplia exposição e obrigações | **PENDENTE; requer avaliação LGPD** |
| **M8: responsável/manutenção** | Nomear responsável por root offline, incidentes, quotas, expiração, D6-02/G2 e comunicação de erro | Sem mantenedor e resposta a incidentes, serviço não deve entrar em beta | **PENDENTE** |
| **M9: pilotos/despesas** | Proposta gratuita, explicitamente separada, **somente após nova autorização escrita** | Quotas insuficientes → reduzir coorte, falhar fechado, reavaliar orçamento; não reduzir segurança | **PENDENTE** |
| **M10: segunda auditoria** | WORK pontual somente quando contrato + riscos estiverem congelados, revisão humana qualificada para aceites G1/G2 | Não substituir avaliação normativa formal por aprovação automática do modelo | **PENDENTE** |

As escolhas acima são **recomendações para deliberação, não ações autorizadas nem decisões finalizadas**. Nenhuma conta, chave, Worker, Durable Object, D1, repo ou consentimento foi criada, alterada ou habilitada.

## 9. Critérios verificáveis antes de sair da fase documental

| Gate futuro proposto | Critério e ambiente de prova, ainda P |
|---|---|
| **C7-1** | Análise de A1–A7 assinada/aceita pelo mantenedor, incluindo A5 e verificador malicioso, sem transformar premissa em garantia |
| **C7-2** | `R_G1` provisionada/rotacionada por canal separado de `R_TUF`, com identidade de instalação e inscrição auditável sem TOFU; simulação de dispositivo clonado e código convite duplicado |
| **C7-3** | CAS `head+receipt` realmente persistente e transacional após reinício/processo novo, com concorrência, idempotência e corpo divergente; laboratório remoto explicitamente autorizado |
| **C7-4** | Assinaturas reais de **teste** sobre os mesmos bytes de Stage3/Stage9/Stage2/L4, nonce/época/identidade; chaves operacionais nunca no cliente |
| **C7-5** | Recovery de hardware/TPM limpo, banco restaurado e operador comprometido: **casos de falha fechada**; antirrollback servidor só se houver segundo domínio independente |
| **C7-6** | Prova de offline sem autoatualização e outras funções preservadas; manutenção G2/D6-02 e G5/G6/G8/NTFS/energia ainda independentes |
| **C7-7** | Base legal LGPD, registro de dados/cotas/retention/backup e responsável documentados, antes de ativar canal externo |
| **C7-8** | Aprovação explícita posterior e separada para eventual ambiente descartável; ausência de aprovação significa **sem deploy** |

**Não executar protótipo externo, Work ou CI neste lote**: faltam decisões de custódia, alcance adversarial e autorização. `G1–G8 = BLOCKED (0/8)` em todas as condições.

## 10. Conclusão técnica e limite de não-equivalência

**Recomendação única CONDICIONAL:** avançar, *caso o mantenedor aprove posteriormente o escopo e o piloto*, com `R_G1` autenticada fora do ciclo TUF, inscrição restrita sem TOFU, testemunha online DO SQLite transacional e identidade pseudônima; TPM opcional, recuperação supervisionada, recibo assinado sobre os bytes reais e operação offline somente para funções independentes. Sem autorização, **nenhuma implementação**.

A escolha é **INSUFICIENTE** para A5: uma restauração administrativa ou assinatura por operador comprometido consegue retroceder a testemunha. Se A5 for exigência, o ADR não recomenda aprovação: priorizar um segundo domínio de custódia e verificação com prova de sequência monitorada e política de recuperação independente.

**A propriedade mais importante ainda ausente:** proteger o próprio *verificador* e sua raiz de confiança frente a substituição deliberada, especialmente por binário antigo legitimamente assinado; um servidor remoto não consegue obrigar software malicioso ou substituído a consultá-lo. Esta limitação deve ser aceita explicitamente ou tratada por escopo de distribuição/verificador e revisão externa.

**Nenhum teste foi executado. Nenhum gate fechado. PR #65/main/BETA .66/infraestruturas preservados.**

## 11. Fontes externas oficiais revisadas na sessão (29/10/2026)

- Cloudflare Durable Objects SQLite: [transacional, privado e PITR](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/); [regras de concorrência](https://developers.cloudflare.com/durable-objects/best-practices/rules-of-durable-objects/); [pricing e quotas Free](https://developers.cloudflare.com/durable-objects/platform/pricing/). A documentação de SQLite registra que a restauração pode retroceder até 30 dias no próprio objeto; não é watermark imune ao operador.
- Cloudflare D1: [Time Travel e backups](https://developers.cloudflare.com/d1/reference/time-travel/), [pricing](https://developers.cloudflare.com/d1/platform/pricing/) e [API de restauração](https://developers.cloudflare.com/api/resources/d1/subresources/database/subresources/time_travel/methods/restore/). A referência oficial consultada em 29/10/2026 indica **até 30 dias** para D1 Time Travel em storage de produção. Isso corrige eventual inferência de que o prazo de restauração seria de somente 7 dias (7 dias pode aplicar-se a outros limites, como undelete, não ao PITR).
- Microsoft: [como o Windows usa o TPM](https://learn.microsoft.com/pt-br/windows/security/hardware-security/tpm/how-windows-uses-the-tpm); [limpeza e troca de TPM](https://learn.microsoft.com/en-us/windows/security/hardware-security/tpm/initialize-and-configure-ownership-of-the-tpm). A chave TPM pode ser não exportável, mas o TPM pode ser limpo/trocado e não substitui o controle externo de identidade.
- Governo brasileiro: [ANPD — guia de legítimo interesse](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia_orientativo_hipoteses_legais_tratamento_de_dados_pessoais_legitimo_interesse), [ANPD — bases legais FAQ](https://www.gov.br/anpd/pt-br/acesso-a-informacao/perguntas-frequentes), [Glossário ANPD — pseudonimização](https://www.gov.br/anpd/pt-br/documentos-e-publicacoes/glossario-anpd). Base legal e retenção não foram decididas; pseudônimo não equivale a dado anônimo.
- Revisar termos/custos quando houver autorização futura. As fontes e datas não certificam o desenho proposto nem asseguram disponibilidade do fornecedor.
