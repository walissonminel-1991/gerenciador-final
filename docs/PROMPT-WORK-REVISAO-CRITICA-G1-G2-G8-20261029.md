# WORK — GF: auditoria crítica G1/G2/G8 (somente leitura)

Atue como revisor de arquitetura de atualização segura para Windows/.NET, TUF, confiança persistente, criptografia, prazos e recuperação. **Procure refutar as hipóteses do implementador** e localizar contraexemplos verificáveis; não apenas confirmar relatórios. Sua entrega será um **único parecer no chat**. Esta segunda leitura NÃO substitui auditoria externa formal ou homologação.

## Fontes e limites obrigatórios

Repositório: `walissonminel-1991/gerenciador-final`. Leia `experimento-g5-ipc-chat-alto-20261029`; commit técnico congelado `9e616663d8574d48b122d22fc493799c75aa4c36`. Confirme HEAD e diff: os commits seguintes devem conter apenas o dossiê e este prompt. PR #65 Draft, branch `work-integracao-final-20261027`, HEAD de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.

**Estritamente READ-ONLY:** não editar código, commits, PRs, reviews, Issues, comentários, CI, releases, permissões, secrets, infraestrutura, Workflows ou telemetria; sem merge, deploy, testes em PCs ou contatos externos. NÃO executar nem repetir testes/CI. Não alterar main, BETA .66, PR #65 ou qualquer gate. **G1–G8 permanecem BLOCKED (0/8).**

Leia **primeiro** `docs/CHAT-ALTO-DOSSIE-REVISAO-INDEPENDENTE-G1-G2-G8-20261029.md`. Ele traz índice de código, matriz de 9 riscos, SHAs, CI e caminhos mínimos. Consulte, nessa ordem, somente o necessário:
1. `docs/WORK-AUTOATUALIZADOR-ETAPA13.md` (dossiê G2 existente; revisão qualificada ainda não executada) e `docs/WORK-AUTOATUALIZADOR-ETAPA12.md` com seus vetores/traces;
2. `docs/CHAT-ALTO-G1-MATRIZ-ARQUITETURAL-20261029.md`, `docs/CHAT-ALTO-G1-ROLLBACK-EPOCA-CHAVE-CHECKPOINT-20261029.md`;
3. `docs/CHAT-ALTO-M3-G8-G1-CHECKPOINT-20261029.md`, `docs/CHAT-ALTO-M3-TOCTOU-CHECKPOINT-20261029.md`;
4. Fonte específica indicada no dossiê **apenas** quando um ponto exigir verificação de código. Sem varrer o repositório inteiro.

Separe rigorosamente **D** (demonstrado em fixture), **H** (hipótese de confiança) e **P** (pendência operacional). CI `success` não equivale a sign-off de segurança.

## Cinco questões que devem ser respondidas

**Q1 — G2/TUF:** avalie independentemente as duas divergências .NET/Python TUF fixado sobre rotação unilateral de chave `timestamp` ou `snapshot` (root1→2, versões100→1). A Etapa12 atribui a diferença à falta de invalidação conjunta conforme TUF 1.0.36 §5.3.11: esta é a conclusão do implementador, **não** validação externa. Examine norma, bytes, trace, piso por época, `targets` e ablação. Distinga discrepância bruta da referência, conformance pertinente, perfil GF, manutenção formal D6-02 e autorização de adoção (ausentes). Não concluir que toda implementação Python ou .NET é correta/incorreta.

**Q2 — G1/autoridade:** o teste mostrou que pin público e época antigos, restaurados junto com o disco, ainda aceitam uma **nova** assinatura de uma chave antiga; a âncora em memória separada recusa, mas é só hipótese de independência. Defina requisitos mínimos para bootstrap sem TOFU, identidade da instalação, witness autenticada e durável, rotação/revogação, risco de chave comprometida, fork, recuperação de perda da witness e indisponibilidade. Uma assinatura válida com nonce novo não prova frescor.

**Q3 — G1/transação:** a `FixtureSession.Begin` da Etapa2 ainda troca o selector depois de `intent` sem compromisso G1. O `G1Preselect` isolado não troca. `G1TransactionModel` faz CAS/ACK em memória; `G1AckFixture` tem checksum **sem assinatura**; `G1CryptoFixture` autentica outra mensagem, **não o ACK antigo**. Proponha barreiras entre `intent → publish idempotente → compromisso externo → recibo/prova de extensão → seleção → confirmação`. Analise `pending`, crash antes/depois do ACK, resposta perdida após commit, `remote-ahead/local-ahead`, concorrência e rollback, sem sugerir autopromoção.

**Q4 — G8/prazo e TOCTOU:** M3 observou `pending` após flush e `confirmed` promovido apesar do timeout externo. O lock NTFS é cooperativo; alteração seguida de restauração pode passar no hash local. Distinga recusa de admissão tardia de efeito de I/O síncrono já iniciado. Defina reconciliação fail-closed e limites de `Kill` de subprocesso, `Flush(true)` e prazo absoluto, sem alegar preempção de syscall nem resistência a queda de energia.

**Q5 — integração:** mostre qualquer ciclo ou incompatibilidade de confiança entre G2/TUF, G1/witness, G8/deadline e G5/G6/IPC, especialmente qualquer API capaz de selecionar ou confirmar candidato antes das barreiras. Não confundir invariantes isoladas com garantias compostas no produto.

## Entrega obrigatória (sem mudar o GitHub)

1. Síntese executiva (máximo 10 linhas).
2. Tabela ordenada por gravidade: achado, código/arquivo+SHA/caso, impacto e ataque, classificação `demonstrado | inferido | não verificado`, correção mínima sugerida **sem implementar**. Reavalie criticamente as prioridades propostas no dossiê.
3. Parecer **separado** sobre G2: norma nos dois vetores, referência Python intacta, conformance ainda faltante, manutenção/revisor necessários.
4. Contrato mínimo G1/G8 com estados e resultados bloqueados após timeout/crash; defina ponto de não retorno e preservação de `pending`.
5. **Até 5 testes realmente novos** que elevem a garantia, com ambiente necessário (CI descartável vs. testemunha externa vs. teste físico) e condição objetiva de falha.
6. **Até 5 lotes priorizados** para o CHAT ALTO, com gate relacionado e critério de aceite mensurável.
7. Pendências que exigem decisão do mantenedor, privacidade, serviço ou revisor humano qualificado. Não invente aceite, responsável ou avaliação independente formal.

Se não puder ler fonte/código, classifique `não verificado`, sem presumir defeito ou ausência. Seja econômico, concreto e crítico. **Conclua declarando G1–G8 BLOCKED e nenhuma alteração realizada. Pare após o relatório.** O CHAT ALTO fará a triagem antes de qualquer ação.
