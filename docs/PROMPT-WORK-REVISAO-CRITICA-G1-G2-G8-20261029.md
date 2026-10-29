# WORK — REVISÃO CRÍTICA INDEPENDENTE DA IMPLEMENTAÇÃO — GF G1/G2/G8
**Missão única de leitura e parecer técnico. NÃO executar desenvolvimento.**

Atue como revisor crítico de arquitetura de atualização segura Windows/.NET, TUF, confiança persistente, rotação de chaves, segurança transacional e recuperação após falha. **Sua missão não é validar as conclusões do implementador por deferência; é tentar falsificá-las e identificar contraexemplos reais.** Não se apresente como auditoria externa formal ou como profissional contratado. Produza relatório apenas nesta conversa.

## 1. Fonte congelada e proteções

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch experimental para leitura: `experimento-g5-ipc-chat-alto-20261029`, ponto técnico consolidado no commit `9e616663d8574d48b122d22fc493799c75aa4c36`. Os commits posteriores contêm **somente** o dossiê e este prompt; verifique HEAD/diff sem editar.
- PR protegida [#65](https://github.com/walissonminel-1991/gerenciador-final/pull/65), Draft, branch `work-integracao-final-20261027`, HEAD de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- A `main`, BETA .66 e o backend/Worker/D1/telemetria são **fora do escopo**.
- **STRICT READ-ONLY:** não criar ou modificar arquivos, branches, commits, PRs, Issues, reviews, comentários, Actions, releases, secrets, contas, domínio, serviços, flags ou permissões; não dar merge, publicar, implantar, testar PCs, executar atualizador nem contatar terceiros. NÃO pedir autorização para ampliar escopo. Não executar CI ou reprocessar testes/vetores existentes. Caso não consiga ler uma fonte, registre-a como não verificada e prossiga com o restante.
- G1–G8 permanecem **BLOCKED, 0/8 gates homologados**. Uma resposta sua não modifica status, roadmap ou aceites.

## 2. Leitura dirigida — evite desperdício de execução

**Leia primeiro** o pacote `docs/CHAT-ALTO-DOSSIE-REVISAO-INDEPENDENTE-G1-G2-G8-20261029.md`. Ele contém matriz dos 9 riscos, SHAs, links de CI e mapa para fontes. Depois leia, nessa ordem:

1. `docs/WORK-AUTOATUALIZADOR-ETAPA13.md` (já preparou uma revisão G2, mas ela **não aconteceu**);
2. `docs/WORK-AUTOATUALIZADOR-ETAPA12.md`, `docs/evidencias/autoatualizador-etapa12-vetores.json` e `docs/evidencias/autoatualizador-etapa12-windows.json`;
3. `docs/CHAT-ALTO-G1-MATRIZ-ARQUITETURAL-20261029.md` e `docs/CHAT-ALTO-G1-ROLLBACK-EPOCA-CHAVE-CHECKPOINT-20261029.md`;
4. `docs/CHAT-ALTO-M3-G8-G1-CHECKPOINT-20261029.md` e `docs/CHAT-ALTO-M3-TOCTOU-CHECKPOINT-20261029.md`;
5. Se houver dúvida verificável: fontes indicadas no dossiê (TUF Stage11/12; `TransactionPrototype`, `PersistentState`, `G1Preselect`, `G1TransactionModel`, `G1AckFixture`, `G1CryptoFixture`, `G1EpochAnchor`, `M3WriteRace` e `M3CommitGate`). **Não varra todo o repositório sem motivo.**

Considere as ações de CI historicamente aprovadas como evidência **apenas dos escopos testados**: G2 Etapa 11 [40859356385](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40859356385), G2 Etapa 12 [40864493929](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40864493929), G8 [40891129725](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40891129725), TOCTOU [40894656649](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40894656649), G1 transação [40898382162](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40898382162), preparo [40899933825](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40899933825), ACK [40901344959](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40901344959), P-256 [40902657571](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40902657571), epoch [40903933090](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40903933090). **Não repetir essas suítes.**

## 3. Questões obrigatórias para a revisão — foco em contraexemplos

**G1 — confiança e rollback.** Uma assinatura ECDSA/P-256 válida da chave antiga, emitida com nonce novo após rollback da chave pública/época, continua plausível no disco revertido. O anchor independente do protótipo é somente memória fora do snapshot e pode mentir se comprometido. Localize onde a implementação assume bootstrap seguro, epoch monotônica, identidade por instalação, segredo/chave autorizada, revogação, inscrição inicial sem TOFU, autenticidade de resposta e testemunha durável. Avalie se algum campo ou recibo é tratado como autoridade sem prová-la. Diga quais premissas exigem recurso externo, consentimento e homologação real.

**G1 — transação e ordem de seleção.** `FixtureSession.Begin` antigo pode selecionar candidato após `intent` **sem** compromisso G1 autenticado. A rota nova `G1Preselect` evita seleção, mas está isolada. `G1TransactionModel` usa CAS/ACK em memória; `G1AckFixture` grava checksum lateral **não assinado**, enquanto `G1CryptoFixture` verifica mensagens assinadas **separadamente**. Determine a menor ordem coerente de `bootstrap → intent persistente → publish durável/idempotente → ACK autenticado → prova de extensão → selecionabilidade → confirmação/rollback`; identifique especificamente estados `local-ahead`, `remote-ahead`, `pending`, timeout após commit, retorno tardio, fork e falha após rotação. **Não trate igualdade de recibos como permissão de execução**.

**G2 — TUF e divergências interop.** A Etapa11 verificou 13 de 15 comparações brutas. A Etapa12 reproduziu dois casos rotacionando chave apenas `timestamp` ou apenas `snapshot` (raiz1→2; papéis100→1) em que o .NET aceita e o Python TUF 7.0.1 fixado recusa. O implementador atribuiu o problema à **ausência de invalidação conjunta timestamp/snapshot** no caminho do Python, citando TUF 1.0.36 §5.3.11, com ablação diagnóstica. Analise autonomamente a aplicação da norma, a referência pinada, os pisos por época, a regra de `targets`, os vetores/limites de cache e se existe contraexemplo .NET dentro do perfil declarado. **Não chame 14/14 testes .NET de conformidade integral nem declare que Python está genericamente errado.** Informe tratamento mínimo para uma futura decisão de perfil/interoperabilidade. D6-02: aceite formal de manutenção permanece faltando.

**G8 — prazos e I/O.** A M3 observou `pending` após flush e `confirmed` já promovido depois de deadline. O lease NTFS é cooperativo, e hashes locais podem não detectar alteração temporária restaurada. Distinga `deadline de admissão` versus `efeito durável` e os resultados que exigem revisão, recusa permanente ou reconciliação. Demonstre por raciocínio e caminhos de código por que kill de filho após hook NÃO prova cancelamento de syscall de kernel nem resistência a power loss. Não declarar que G8 está fechado.

**Integração entre G1/G2/G8.** Verifique ordem de confiança: TUF precisa de raiz autêntica e pisos próprios; testemunha externa precisa identidade independente; deadline não pode ser confundido com rollback ou permissão. Liste ciclos de dependências, lacunas de G5/G6 e barreiras necessárias para que nenhum resultado de fixture vaze para o produto.

## 4. Formato obrigatório do único relatório

Entregue em português brasileiro, **somente no chat**:

1. **Síntese de até 10 linhas:** o que é impeditivo e seu nível de confiança.
2. **Tabela de achados ordenada por severidade**: ID, severidade, evidência precisa (arquivo/símbolo/SHA, teste se houver), ataque/corte, efeito, classe `demonstrado | inferido | não verificado`, correção mínima **proposta**, jamais implementada.
3. **Parecer G2 separado**: (a) interpretação normativa dos dois vetores; (b) divergência da referência intacta; (c) limite de conformance GF; (d) manutenção/decisão externa pendentes. Se evidência não for suficiente, dizer claramente.
4. **Contrato sugerido G1/G8** com fases e tabela de falhas relevantes, explicitando a última operação reversível, timeout ambíguo e política de preservação de evidências.
5. **Até cinco ensaios novos que REALMENTE aumentariam a garantia**, com teste esperado, falha que detectariam e ambiente mínimo (CI/infraestrutura externa/power loss). Não repetir os já aprovados.
6. **Até cinco lotes de trabalho priorizados** para o CHAT ALTO depois da revisão, cada um com gates dependentes e critérios de aceite objetivo.
7. **Pendências de decisão do mantenedor e de revisor humano independente**; não preencher identidade, certificação, aceite nem aprovação fictícios.
8. **Conclusão imutável:** G1–G8 BLOCKED; nenhuma alteração de código, CI ou repositório realizada; este parecer de segunda leitura não é aceite normativo externo nem homologação.

Evite comentários genéricos e recomendações que dependeriam de repetir todos os testes. Se identificar um erro crítico demonstrável, documente seu efeito e uma correção mínima, sem tentar corrigi-lo e sem publicar dados sensíveis. Se não houver acesso ao código completo, NÃO inferir ausência de validação: classifique `não verificado`.

**PARE aqui após entregar o relatório.** O CHAT ALTO fará a triagem das recomendações e decidirá quais mudanças isoladas justificam novas missões.
