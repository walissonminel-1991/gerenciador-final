# GF — CHAT ALTO — Lote 6: ADR comparativo de testemunha G1 independente
**2026-10-29 · Status: PROPOSTA, não aprovada · Somente documentação**
**G1–G8: BLOCKED / 0 de 8 gates operacionais homologados**

## 0. Controle de mudanças, evidência e linguagem de confiança

- Repositório: `walissonminel-1991/gerenciador-final`. **Branch exclusiva** `experimento-g5-ipc-chat-alto-20261029`, HEAD inicial verificado `b817b87e0bd676cc81ab89c823b4968d3ffe8b19`.
- Linha protegida: PR [#65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65), branch `work-integracao-final-20261027`, HEAD observado `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; `main`, BETA .66, Worker/D1 privados de telemetria e flags intocados.
- Evidência prévia: `docs/CHAT-ALTO-LOTE5-MESMOS-BYTES-TUF-STAGE2-L4-CHECKPOINT-20261029.md`, L5 CI Windows [40922528166](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40922528166) (6/6 PASS, **somente** fixtures); e parecer WORK G1/G2/G8 compartilhado pelo mantenedor no CHAT ALTO.
- Este ADR utiliza fontes técnicas externas consultadas em 29/10/2026, listadas na seção 12. **Não é auditoria formal, pesquisa de mercado contratada nem decisão de deploy.** Não atribui preço garantido. P = pendência; D = demonstrado em fixture; H = hipótese do modelo.
- **Sem CI**: nenhum código alterado. Não ativar serviço, conta, chave, endpoint, domínio, telemetry, consentimento ou release. O programa principal não muda.

## 1. Pergunta e resposta executiva

**Pergunta:** como impedir que um Windows restaurado integralmente retorne a uma época/raiz antiga, reconheça uma chave revogada ou retome operação já comprometida, com baixo custo e privacidade, sem atribuir à própria máquina restaurável a autoridade de frescor?

**Resposta condicionada:** uma **witness lógica independente, online para o autoatualizador, com CAS serializado e estado por instalação em armazenamento transacional durável** é a opção de menor complexidade funcional para um piloto. Para o desenho específico G1 `IntentCommit → TerminalCommit`, **Cloudflare Durable Objects com SQLite, em ambiente G1 separado e NÃO implantado**, é candidato arquitetural preferencial a ser avaliado; Workers+D1 é alternativa viável para consulta/gestão. A escolha **não representa aprovação**: ambos podem ter dados restaurados por um administrador do mesmo domínio de custódia, e a chave de assinatura do serviço pode ser comprometida. **Um segundo domínio de testemunho/auditoria** passa a ser requisito se a ameaça incluir operador/cloud comprometidos, apagamento seletivo ou equivocation. Sem segunda autoridade, o modelo deve assumir operador honesto e registrar esse limite.

Nunca compartilhar armazenamento, credenciais, Worker, banco ou consentimento com a telemetria BETA .66. Não se comprometer ainda com Cloudflare ou qualquer fornecedor. Modo offline não deve fazer autoatualização dependente de G1; **as demais funções independentes do GF continuam disponíveis**.

## 2. Adversário: decisão obrigatória antes de implementar

| Ameaça | Capacidade | Com uma witness única, o que se poderia garantir? | Resíduo |
|---|---|---|---|
| A1 — restauração do disco do PC | substitui **todos** os arquivos e pin públicos locais, inclusive verificador antigo; pode preservar chave privada antiga | se **e somente se** raiz/verificador atual continuarem autenticáveis fora da restauração, o head externo honesto rejeita epoch/head antigo | binário antigo pode pin-ar chave G1 antiga; exige cadeia de evolução da raiz e política para cliente legado |
| A2 — malware no usuário local | altera diretórios/selector, reordena chamadas, reproduz requests; pode copiar id em disco | CAS com autenticidade do **publicador** e vinculação à instância atual dificulta conflito; não controla malware admin | mesma sessão pode assinar operação indevida se verificador/controlador comprometido |
| A3 — reinstalação, clonagem e troca de hardware | recupera imagem antiga e tenta registrar `installationId` novo | sem mecanismo não clonável/autoridade de recuperação, **não há prevenção robusta de reenrollment** | identidade local aleatória não comprova dispositivo; bootstrap aberto permitiria contornar revogação |
| A4 — indisponibilidade/rede/timeout | perde resposta após CAS e/ou fica offline | consulta autenticada por `installationId+txId+phase` retorna recibo **durável**, sem duplicar | disponibilidade, janela de consulta e retenção dos recibos |
| A5 — operador da witness comprometido | restaura banco, usa segredo de assinatura, apresenta dois heads incompatíveis | **não resolvido pela witness única**, mesmo com assinaturas e TLS | requer outro domínio verificador, registro independente, transparência/quórum/controle de custódia |
| A6 — falha elétrica do PC, armazenamento defeituoso | interrupção durante `Flush(true)`/rename e gravações Stage2 | detecção e contenção **propostas** com estados `UNKNOWN`; sem prova física | requer laboratório descartável e critérios G8 |
| A7 — vazamento/uso indevido de metadados | correlaciona checks, instalações, IP, versões e horários | minimização, separação de finalidades, logs/retention limitada | pseudônimo **não equivale a anonimização** pela LGPD |

**Limite de modelo:** nenhuma solução puramente local protegida por chave/contador restaurável no mesmo disco garante antirrollback contra A1. TPM pode elevar o custo de clonagem de chave, mas não automaticamente de todo estado nem resolve semântica de confiança G2/G1. Fonte Microsoft: `https://learn.microsoft.com/pt-br/windows/security/hardware-security/tpm/tpm-fundamentals`.

## 3. Matriz de alternativas (custos e riscos, não aprovação)

| Opção | Custo indicativo em 29/10/2026* | CAS/idempotência e operação | Privacidade/offline | Limite decisivo | Avaliação provisória |
|---|---|---|---|---|---|
| **A — Workers + Durable Objects SQLite, 1 objeto por instalação** | **US$0** dentro das cotas Free; Workers Paid base **US$5/mês** se necessário, mais excedentes | armazenamento privado/transacional/fortemente consistente por objeto; um ponto natural de serialização, persistir head + mapa `(phase,txId)`; restarts de JS não devem perder dados persistidos | não offline; identificador pseudônimo por DO, exposição de acesso, logs de rede | armazenamento possui mecanismo de recuperação de ponto no tempo; **não** é monotônico contra operador comprometido | **Preferência condicional para estudo**, sem deploy |
| **B — Workers + D1 separado** | **US$0** dentro das cotas Free; mesmo piso Workers Paid US$5/mês se migrar | transação SQL **única** precisa ligar CAS/receipt/idempotência; usar operação condicional e constraints, jamais read-then-write fora de transação | consultas/gestão centralizadas facilitam retenção, mas ampliam o raio de falha do banco | D1 tem `time-travel restore` que sobrescreve estado anterior; auditoria externa exigida contra rollback por operador | **Alternativa sólida**, especialmente para múltiplos registros |
| **C — GitHub privado como ledger da witness** | sem tarifa de API base aqui estimada para protótipo; limites/abuso e governança aplicáveis | gravação de arquivos com SHA/409 pode detectar conflitos locais, mas Git não oferece semântica natural de CAS global por instalação, recibo de assinatura nem disponibilidade adequada | clientes **não** devem carregar PAT; acesso privado requer mediador/servidor; logs/eventos e retenção | repositório/branches administráveis, commits podem ser reescritos, não impedem rollback do operador | **NÃO usar como autoridade primária**; possível segunda publicação/auditoria independente |
| **D — TPM/Platform Crypto Provider local** | em hardware existente, serviço sem custo; hardware compatível **não garantido** | permite chave ligada ao dispositivo e atestação em contextos suportados; NV monotônico precisaria prova de suporte, permissões, limpeza e desgaste por modelo | pode auxiliar offline, mas sem testemunha do estado distribuído; risco de exclusão por máquinas antigas | TPM sozinho **não é** witness externa e não valida retroativamente estado Stage2 | **Complementar opcional**, nunca obrigatório neste estágio |
| **E — log de transparência independente / duas testemunhas** | software/serviço pode ser aberto; **custo operacional e disponibilidade não estimados** sem solução concreta | provas de inclusão e consistência, detecção de equivocação se monitoradas por entidade independente; não substitui CAS da opção A/B | dados publicados podem ser correlacionáveis e difíceis de apagar; offline verifica provas antigas, não frescor atual | Rekor público registra eventos de assinatura, **não** oferece contrato de `IntentCommit/TerminalCommit` por instalação nem privacidade apropriada por padrão | **Camada de auditoria futura** contra A5, não backend primário |
| **F — somente disco Windows, GitHub Release ou token local** | US$0 de backend adicional | nenhum piso externo confiável; assinatura TUF autentica metadados mas não torna disco restaurado monotônico | offline, simples | falha no contraexemplo G1 EPOCH-02 | **Rejeitar para self-update G1 protegido** |

*Preço não é orçamento: quantidades limitadas e custos adicionais de domínio, operação, manutenção, retenção, segurança, suporte, política de privacidade e laboratório **não foram calculados**. Sob Free, exceder cotas pode resultar em erros/bloqueio de consultas em vez de cobrança; não tolerar fail-open.*

**Fontes de custos atuais:**
- Cloudflare Workers: `https://developers.cloudflare.com/workers/platform/pricing/` — plano Paid inicia em **US$5/mês**.
- Durable Objects: `https://developers.cloudflare.com/durable-objects/platform/pricing/` — Free com SQLite, **100.000 requests/dia** e duração limitada, quotas próprias de leitura/escrita/armazenamento; `https://developers.cloudflare.com/durable-objects/platform/limits/`.
- D1: `https://developers.cloudflare.com/d1/platform/pricing/` — Free com **5 milhões linhas lidas/dia, 100.000 escritas/dia, 5 GB total**; `https://developers.cloudflare.com/d1/platform/limits/` — Free **500 MB por DB, até 10 bancos**, recuperação 7 dias.
- DO SQLite comporta limites de armazenamento: 5 GB por conta Free; cada objeto tem custo mínimo de metadados em SQLite, que cresce com cardinalidade; revisar capacidade real.
- GitHub API: `https://docs.github.com/en/rest/repos/contents`, `https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api` — SHA exigido para atualizar arquivo; endpoints concorrentes podem conflitar; consultas anônimas públicas geralmente 60/h; escrita privada exige autenticação.

### Estimativas de carga — apenas cenários, não previsão

Um **piloto ilustrativo** de 100 instalações, **2 verificações/semana**, **3 chamadas por verificação** (head/intent/terminal, ignorando bootstrap/retries) gera aproximadamente `100 × 2 × 3 × 52/12 ≈ 2.600 requests/mês`. Considerar retries, dois commits, health/audit, DDoS, inicialização, cache e requests intermediários; um limite gratuito não é garantia de segurança nem um SLO.

Para **1.000 instalações fazendo 3 chamadas/dia**, aproximadamente **3.000 chamadas/dia**, abaixo do limite nominal 100.000 requests/dia de Workers/DO Free; custos reais dependem de consultas SQL, linhas acessadas, bancos/DO, armazenamento e quotas compartilhadas com outros workloads. Um estado por instalação com `head+map txId` e **tombstone de revogação** não deve ser apagado arbitrariamente para economizar espaço.

**Cenário de armazenamento meramente ilustrativo:** 10.000 instalações × 2 KB por head/tombstone ≈ 20 MB de payload, **excluindo** índices, SQLite, recibos por transação, backups, logs, overhead de objeto (que pode ser considerável) e metadados de acesso. Não usar esse número como capacidade garantida.

## 4. Bootstrap sem TOFU — impossibilidade e proposta condicional

**Impossibilidade a reconhecer:** um `installationId` gerado somente no disco, sem vínculo não clonável ou registro confiável anterior, **não permite distinguir após restauração uma instalação genuinamente nova de reinstalação fraudulenta**. Não resolver dizendo que JWT, TLS, SHA ou nonce por si provam identidade. O usuário não é obrigado a usar conta GitHub, e o GF não deve incorporar tokens privilegiados nem depender do login de avaliadores.

**Contrato futuro:**
1. Distribuição inicial já traz âncoras públicas **verificadas por canal independente**: TUF editorial `R_TUF`, root G1 de testemunha `R_G1`, versão e política de rotação. A raiz G1 **não deve derivar exclusivamente** de TUF que depende de G1 para antirrollback. Uma cópia antiga da aplicação pode conter pin G1 antigo; a cadeia de rotação deve preservar continuidade sob raiz offline independente e revogação do servidor. Antigo verificador comprometido continua difícil: risco explícito de A1/A2.
2. Enrolamento **somente por autorização autenticada** (por operador/política/atestação de hardware quando disponível), transacionado para `installationId + enrollmentEpoch + devicePublicKey (opcional)`, sem reconhecer automaticamente nova identidade após revogação.
3. Uma chave do dispositivo idealmente gerada não exportável (TPM quando disponível) pode autenticar publicador sem login e reduzir cópia de credencial, mas **não** prova que o GF do sistema está íntegro nem impede software local malicioso de solicitar assinaturas autorizadas. Disponibilidade TPM e recuperação exigem fallbacks analisados, nunca bloqueio das funções básicas GF.
4. Reinstalação/troca de hardware **não** deve gerar `enrollmentEpoch=0` silenciosamente. Criar processo **separado, auditável e explicitamente autorizado** para recuperar inscrição ou revogar dispositivo antigo. Não definir ainda se será atendimento manual, código de recuperação, attestation ou outra alternativa: exige avaliação do mantenedor.
5. Não registrar **serial de placa-mãe, MAC, ID de Windows, login GitHub, HWID bruto** como substitutos automáticos da identidade. Pseudônimo aleatório separado do diagnóstico, mais prova de posse, pode reduzir exposição; a possibilidade de reset/clonagem precisa ser tratada como limite operacional.

## 5. Protocolo lógico mínimo proposto (SEM IMPLEMENTAÇÃO)

`EnrollmentValid → ReconcileCurrentHead → PreparedIntentLocal (no selector) → IntentCommitDurable → ReceiptVerifiedAndBound → TerminalLocalObserved (inert/blocked) → TerminalCommitDurable → NextIntentEligibleForEvaluationOnly`.

- **TUF** verifica editorialmente `root/timestamp/snapshot/targets`, hashes do ZIP, identidade do candidato e piso funcional não rebaixado. **G1** não cria release nem substitui root editorial. **G8** fecha admissão quando orçamento expira; não anula um CAS já comprometido. **G5/G6** continuam separados para identidade do processo e READY real.
- Receber somente `head` fresco não é suficiente; respostas G1 devem ser verificadas por raiz G1 previamente confiada, com assinatura sobre **bytes canonizados e domínio**, incluindo `protocol, repoId, installationId, enrollmentEpoch, txId, phase, claimDigest, priorHeadDigest, nextHeadDigest, sequence, witnessKeyEpoch, challenge/nonce, result/status`. Pin/challenge/assinatura local **não provam** independência nem monotonicidade do backend.
- Um único armazenamento transacional do backend deve atualizar **atomicamente** `head + (phase,txId,bodyDigest→receipt)` com CAS sobre `(seq,digest,phase,txId)`. Não basta lock em RAM, transação apenas no cliente ou `UNIQUE(txId)` sem fase.
- A interface do backend deve separar `publishIntent`, `publishTerminal` e `readReceipt(txId,phase)`; retries idempotentes recuperam **o mesmo recibo** após timeout pós-commit. Corpo divergente com mesmo txId/fase → conflito, head remoto-ahead → reconciliação. Timeout antes da observação remota → UNKNOWN, não `aborted`.
- **G8**: read/reconcile após timeout não reinicia orçamento nem autoriza ação tardia; prefixo do protocolo pode persistir, sem seleção. Permissão só seria avaliada depois de todos os gates comprovados (não implementados).
- **Assinatura de witness + TLS**: TLS protege transporte, mas não substitui binding da mensagem nem resistência do estado ao operador. ECDSA anterior em fixture assinou **outros bytes**; nenhum campo L5 é ainda operacionalmente assinado. Propor formato e vetores públicos de teste para revisão externa **antes** de qualquer chave real.
- Persistência local e remoto **não são uma transação atômica distribuída**; estado `Prepared`, `UNKNOWN`, `RemoteAhead`, `LocalAhead`, `Conflict` e `Quarantined` devem ser estados explícitos. Recibo de `IntentCommit` não autentica `rolled_back`, `selected` ou `confirmed`; `TerminalCommit` é uma segunda fase distinta.
- **Anti-equivocação**: sob ameaça A5, manter `high-water mark`/checkpoint assinado em domínio independente e verificar provas de extensão; proteger o canal de rotação de `R_G1`. Um banco com PITR, sozinho, pode retroceder ao sofrer restauração administrativa.
- Sem API `CanSelect` ou `CanConfirm` no protótipo; todos os modelos anteriores classificam `CommittedButNotAuthorized`.

## 6. Recuperação, revogação, rotação e retenção

| Falha ou evento | Conduta proposta, sem bypass |
|---|---|
| Sem conexão / D1 ou DO sem cota / HTTP 403/429/5xx | Bloquear **somente** autoatualização dependente da witness; conservar último estado/txId/pending; tentar depois com backoff, sem gastar novo nonce como prova de frescor retroativa |
| CAS remoto comprometido, resposta perdida | `UNKNOWN`, consulta autenticada pelo mesmo `txId/phase`, nunca reemitir nova tx para mascarar desconhecido |
| Recebe head atual mas não encontra recibo anterior | `CONFLICT` ou `RECOVERY_REQUIRED`, preservar provas; não deduzir ataque nem sucesso |
| DB D1/DO restaurado para versão anterior | comparar head com checkpoint **independente de domínio administrativo**; na ausência dessa âncora, classificar rollback de servidor **não detectável** e suspender autoatualização |
| Chave G1 expira/roda | cadeia de rotação autorizada por root G1 distinta da editorial; transição de epoch com recibos/contadores; chave antiga nunca volta a ser adotada por pin local revertido |
| Operador/segredo de assinatura comprometido | revogação e rebootstrap sob autoridade **independente** ainda não especificada; se todo o domínio G1 comprometido, recusa até restabelecimento auditado; assinatura do atacante pode parecer válida |
| Instalação clonada/reinstalada / TPM limpo | sem reenrollment automático; processo de recuperação autenticado, revogação de identidade antiga e preservação do piso externo |
| Pedido de exclusão LGPD | identificar finalidade/base legal e direitos; separar **histórico detalhado com TTL curto a definir** de **estado mínimo antirrollback e tombstones**, cuja retenção/exclusão exigem avaliação jurídica. Nunca preservar indefinidamente dados pessoais só para facilidade técnica |
| Falta de espaço ou manutenção | não truncar recibos necessários para idempotência sem acordo de checkpoint/compactação e retenção; falhar fechado na autoatualização até reconciliação |
| Falha elétrica NTFS / crash cliente | nenhum `pending` autopromovido; reabrir storage/handles, comparar witness e validar G8 em laboratório físico; `File.Move` após timeout não é comprovado só por pausa depois de rename |

**Retenção proposta para discussão, NÃO política aprovada:** identificador pseudônimo e head/tombstone com a menor duração necessária para prevenir reinstalação fraudulenta; recibos de idempotência com janela maior do que máxima recuperação/offline tolerada; logs de acesso e IP por período estritamente mínimo; depois agregação sem identificadores. **Não importar o TTL de 7 dias dos diagnósticos BETA .66**, nem consentimento prévio. Eventuais backups/PITR podem prolongar apagamento efetivo. Pseudonimização não implica anonimização (LGPD).

## 7. Offline e experiência do usuário

- GF normal: funcionalidades não relacionadas a autoatualizador **não devem depender de G1**, nem pedir conta ou conexão.
- Autoatualização conectada: só procurar/aplicar se a inscrição, autoridade, checkpoint, prazo e todos os gates estiverem demonstrados. Na ausência de conexão: `Autoatualização segura indisponível — sem alterar aplicativos. Tente novamente com a conexão ativa.` O erro não deve implicar que o Windows está inseguro ou exigir que desative antivírus.
- Atualizações de **outros aplicativos** e **Windows Update** possuem mecanismos próprios. Não bloquear essas funções por dependência artificial de G1 do autoatualizador do GF; rever políticas de segurança individuais.
- Modo totalmente offline com pacote externo assinado é **fluxo separado a projetar/homologar**, não uma permissão para ignorar witness. Um checkpoint previamente verificado com prazo não garante frescor contra rollback integral local, se não houver contrapeso de hardware/terceiro.
- Não fazer retry agressivo, popups ou UAC desnecessários; sem exigir login GitHub de avaliadores.

## 8. Privacidade, segurança e custódia: decisões antes da primeira conexão

A LGPD considera identificadores eletrônicos e pseudônimos passíveis de serem dados pessoais quando associados a pessoa identificável. **Consentimento para diagnóstico BETA ≠ base legal para witness de atualizações**; base legal, transparência, finalidade, minimização, exercício de direitos e retenção precisam de avaliação específica.

**Esquema mínimo candidato** (ainda não aprovado): `installation_pseudonym (128-bit random)`, `enrollment_epoch`, `authorized_device_key_id` quando houver, `witness_key_epoch`, `head_seq/phase/digest`, `txId/bodyDigest`, `receipt`, `revocation/tombstone`, `created/updatedAt`, `receipt_retention_boundary`. Evitar enviar lista de programas instalados, arquivos, logs de limpeza, paths, nomes de usuários, IP explicitamente em payload, HWID bruto, CPF, e-mails ou GitHub handles. **IP e horário podem estar em logs do fornecedor; não prometer invisibilidade completa**.

**Condições de segurança de custódia:** contas/roles e segredos de G1 separados de BETA; acesso de escrita restrito; recuperação de assinatura offline; pin/política de rotação versionados; limites por instalação e globais; auditoria de ações administrativas; não usar chave privada longa no EXE; não incluir publicador com token administrativo compartilhado. A compatibilidade com modalidade portátil e PCs fracos deve ser homologada.

## 9. Matriz de decisão e recomendação ao mantenedor

Notas abaixo são **qualitativas e provisórias**, não scoring quantitativo real de fornecedor:

| Critério | DO SQLite | Workers+D1 | GitHub principal | TPM local | Log/quórum independente |
|---|---|---|---|---|---|
| Baixa operação para piloto | **Boa** | Boa | Ruim para escrita de cliente | Boa se presente | Ruim sem monitor |
| CAS por instalação | **Adequado por desenho** | Adequado com transação SQL rigorosa | Frágil/intermediado | Não é CAS remoto | Complementar |
| Custo mínimo em uso baixo | **Free possível** | Free possível | Gratuito mas limites | Sem serviço extra | Variável |
| Antirollback contra disco do PC | Sim, **sob witness honesta + bootstrap confiável** | Idem | Limitado e com mediador | Parcial/somente hardware | Depende do protocolo |
| Contra restauração pelo operador da witness | **Não** | **Não** | Não | Não (ameaça distinta) | Potencialmente, se monitor e domínio independentes |
| Privacidade de estado por instalação | Melhor isolamento lógico | Banco central gerenciável | Risco se publicado/compartilhado | dados locais | ruim se público por instalação |
| Offline completo com frescor | Não | Não | Não | Não demonstrado | provas antigas verificáveis ≠ atualidade |
| Adoção agora | **Não** | Não | Não | Não | Não |

**Decisão proposta do CHAT ALTO (não aprovada):**
1. **Estudar DO SQLite separado** como primeira alternativa de menor complexidade de CAS por instalação; manter **Workers+D1 dedicado** como plano B caso exigências de consulta/retenção/migração justifiquem. A separação de BETA é obrigatória, mas **usar outra namespace no mesmo provedor não assegura independência administrativa**.
2. Não escolher endpoint/domínio nem provisionar nada nesta etapa. Especificar antes identidade/cliente, root offline, rotas e recuperação, grau de ameaça A1–A7, aprovação da privacidade e manutenção.
3. Se o escopo incluir operador comprometido ou corrupção administrativa, **não declarar G1 encerrável só com DO/D1**. Exigir segundo domínio de prova de sequência/consistência (auditoria assinada independente e verificável ou outro modelo), com custo e retenção definidos.
4. Permitir **zero login do usuário comum** apenas se o bootstrap/enrollment tiver prova própria; não usar JWT anônimo renovável para contornar revogação nem herdar consentimento BETA.
5. Enquanto essa arquitetura não for homologada, manter **self-update GF desativado**; nunca reduzir os requisitos de G1 para caber no plano gratuito.

## 10. Critérios objetivos de aceite futuros (nenhum cumprido aqui)

| ID | Exigência antes de mover G1; ambiente |
|---|---|
| W1 | Identidade/registro por instalação não resetável por mera cópia de disco + processo documentado de revogação/recovery; análise de ameaça e auditoria humana |
| W2 | Pin da raiz G1 provisionado fora de ciclo G1↔G2, rotação/revogação com continuidade, replay e endpoint malicioso; CI com assinaturas descartáveis + revisão qualificada |
| W3 | CAS de `head + receipt` em storage persistente: mesma fase/txId/body idempotentes após **reboot real da witness**, corpo divergente conflitante e perdedor concorrente; ambiente externo descartável explicitamente autorizado |
| W4 | Recebo exatamente o mesmo recibo **assinado** sobre os bytes compostos Stage3/Stage9/Stage2/terminal após queda de rede pós-CAS; verificador independente; chaves de laboratório |
| W5 | Restore do storage remoto para snapshot anterior resulta em detecção por domínio **externo à custódia restaurável**; teste de fork/equivocação com duas visões se A5 fizer parte da ameaça |
| W6 | Sem conexão/offline, sem root confiável, sem inscrição, sem recibo ou deadline expirado: zero seleção/confirm e nenhuma degradação das outras funções do GF |
| W7 | G2: parecer humano qualificado TUF/patch + D6-02 formal, persistência de época, vetor Python divergente adjudicado e `releaseFloor` separado |
| W8 | G8 e IPC: power-cut/NTFS em hardware descartável e processo real; G5/G6 READY vinculado a sessão/tx/processo/candidato e sem bypass `FixtureSession.Begin/Confirm` |
| W9 | Avaliação de privacidade e retenção, validação de custo/cotas sob carga e falha de quota, operador/papéis/custódia e política de exclusão; aprovação explícita do mantenedor |

**Classificação de evidências:** W1–W9 = **pendências operacionais**, não testes cumpridos. As 6/6 CI L5 demonstram somente integridade dos mesmos bytes **dentro da fixture**, não W1–W9.

## 11. Sequência futura sem gasto de WORK ou execução de infraestrutura agora

1. **Decisão do mantenedor**, idealmente com resposta sucinta a 4 itens: (a) online obrigatório só para self-update? (b) adversário inclui operador da witness? (c) uso de TPM é opcional ou proibido por compatibilidade? (d) existe autorização futura para um piloto isolado, **não nesta missão**, com identidade pseudônima própria?
2. **ADR de protocolo e ameaça revisado**: modelar root offline, formatos canônicos assinados, fluxos de enrolamento/revogação/recuperação e anti-equivocação; não criar chaves ou endpoints agora.
3. **Revisão humana qualificada e/ou WORK pontual** do contrato consolidado, se houver hipótese difícil explicitamente identificada. WORK não deve substituir aceite formal.
4. **Somente após autorização distinta**: protótipo externo descartável isolado, sem misturar BETA, e testes W1–W6 com storage real/cortes/restarts; sem autorização, manter documentação e testes offline.
5. Futuro planejamento de integração ainda depende de G2/G5/G6/G8, e não de free-tier ou regressão de CI.

## 12. Fontes externas verificadas (29/10/2026)

- Cloudflare [Workers Pricing](https://developers.cloudflare.com/workers/platform/pricing/) e [Limits](https://developers.cloudflare.com/workers/platform/limits/) — plano Free/Paid, US$5/mês de entrada, limites de requests.
- Cloudflare [Durable Objects Pricing](https://developers.cloudflare.com/durable-objects/platform/pricing/), [Limits](https://developers.cloudflare.com/durable-objects/platform/limits/), [Storage](https://developers.cloudflare.com/durable-objects/best-practices/access-durable-objects-storage/), [Rules](https://developers.cloudflare.com/durable-objects/best-practices/rules-of-durable-objects/) e [SQLite Storage API](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/) — Free SQLite, consistência transacional, reinício de memória, regras de concorrência e PITR.
- Cloudflare [D1 Pricing](https://developers.cloudflare.com/d1/platform/pricing/), [Limits](https://developers.cloudflare.com/d1/platform/limits/) e [Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) — quotas e **restauração destrutiva** para estado passado.
- GitHub [REST Repository Contents](https://docs.github.com/en/rest/repos/contents) e [REST API Rate Limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api) — sha/409/credenciais/cotas.
- Microsoft [TPM Fundamentals](https://learn.microsoft.com/pt-br/windows/security/hardware-security/tpm/tpm-fundamentals), [Platform Crypto Provider](https://learn.microsoft.com/en-us/windows/win32/seccertenroll/cng-key-storage-providers) — chaves ligadas ao TPM/limites de plataforma.
- [Sigstore Transparency Security Model](https://docs.sigstore.dev/about/security/), [Rekor Spec](https://github.com/sigstore/architecture-docs/blob/main/rekor-spec.md) — inclusão/consistência, testemunhas independentes e risco de privacidade em log público.
- [TUF Specification v1.0.36](https://theupdateframework.github.io/specification/v1.0.36/) — root fora de banda, validação de metadados e invalidação durante rotação.
- Governo do Brasil [tipos de dados LGPD](https://www.gov.br/funasa/pt-br/acesso-a-informacao/lei-geral-de-protecao-de-dados-pessoais-lgpd/tipos-de-dados-abordados-pela-lgpd), [ANPD](https://www.gov.br/anpd/pt-br/canais_atendimento/cidadao-titular-de-dados/denuncia-peticao-de-titular-referente-lgpd) — pseudônimo não equivale a anonimização.

**Fontes externas são informativas, não certificação de plataforma ou garantia de disponibilidade. Revalidar preços e termos na data da eventual decisão.**

## 13. Encerramento documental

Nenhuma alternativa escolhida para implantação; nenhum produto, CI, serviço, chave, secret, endpoint, conta, domínio, flags, consentimento, dados ou infraestrutura mudou nesta missão. A `main`, PR #65 Draft, BETA .66 e telemetria ficam preservadas. **G1–G8 BLOCKED — 0/8.**
