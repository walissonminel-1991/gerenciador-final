# CHAT ALTO — M3 / G1: testemunha de frescor independente (somente modelo) — 29/10/2026

## Estado e evidência

- Repositório: `walissonminel-1991/gerenciador-final`
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`
- HEAD inicial: `6072fcb448f80d40636573b50cccdf634f6ea09a`
- Commit do **código aprovado**: `e65f9630bc49ef2b333f3c8643a061ca3d1600a7`
- CI: [40896067869](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40896067869), Windows 2022, **SUCCESS**; 11 PASS / 0 FAIL / 0 warning / 0 error.
- PR #65 Draft, branch `work-integracao-final-20261027`, HEAD preservado: `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. `main`, BETA .66, backend, Worker/D1, telemetria, instalador e canal de atualização **não modificados**.
- Nenhum WORK foi utilizado. Nenhum merge, release ou deploy.

## Hipótese de ameaça G1 — adversário pode restaurar todo o armazenamento local

As Etapas 2 e M3 anteriores mostraram que um diário encadeado e o seletor podem estar coerentes depois de uma restauração integral. Se um adversário reverte o diretório inteiro para uma cópia histórica íntegra, hashes locais e nomes de arquivos não permitem provar que um estado mais novo já existiu.

Foi construído em `prototipos/autoatualizador-seguro/m3-g1-witness/` o modelo `G1WitnessModel.cs.txt` e a suíte `G1WitnessTests.cs.txt`, compilados pela CI exclusiva `.github/workflows/autoatualizador-m3-g1-witness-inerte.yml`.

O código usa o **mesmo** `PersistentState` e `FixtureSession` da Etapa 2; o `G1LocalEvidence.Capture` lê a geração final, digest da geração, piso monotônico de sequência e seletor real da fixture sob o lease. A testemunha `G1TrustedMemoryWitness` guarda o último checkpoint em um **objeto separado na memória do processo de teste**, sem ler o diretório que depois é restaurado.

O modelo de verificação consulta `IG1WitnessModel` usando nonce de desafio de teste e identificador da instalação. Detecta:
- geração/sequência local inferior ao último checkpoint como `G1_LOCAL_ROLLBACK`;
- geração equivalente com digest/seletor divergente como `G1_LOCAL_FORK`;
- testemunha indisponível, não inicializada ou que lança timeout como `G1_WITNESS_UNAVAILABLE`;
- resposta repetida com nonce antigo ou instalação distinta como `G1_REPLY_REPLAY_OR_IDENTITY`;
- testemunha atrasada em relação ao estado local como `G1_WITNESS_BEHIND`, sem aprovação otimista.

A atualização da testemunha modelada proíbe regressão de geração/sequência e ambiguidade no mesmo número de geração. A igualdade observada **não é autorização** de atualização, confirmação, promoção ou restauração. Não existe protocolo de publicação ou autenticação de testemunha real.

## Testes dirigidos: 11 PASS

1. `G1-01`: checkpoint local e de testemunha coincidem antes e depois de avanço legítimo, sem autorização operacional.
2. `G1-02`: arquivos da instalação temporária são copiados **byte a byte** em memória antes da atualização; `FixtureSession.Begin` avança duas gerações e eleva o piso; a testemunha observa o avanço. O teste restaura integralmente os **arquivos locais e seus nomes** e comprova que `PersistentState.Load` aceita a cadeia antiga com transação nula. A testemunha independente recusa com `G1_LOCAL_ROLLBACK`. **Não foi criado snapshot físico do volume nem alterado hardware.**
3. `G1-03`: após restauração, uma nova transação Stage2 gera histórico válido com mesma geração/piso mas novo digest; testemunha recusa `G1_LOCAL_FORK`.
4. `G1-04`: consulta indisponível falha fechada.
5. `G1-05`: timeout da testemunha falha fechado.
6. `G1-06`: replay de resposta antiga a desafio com novo nonce é recusado.
7. `G1-07`: testemunha ainda não inicializada não é substituída por confiança no primeiro uso.
8. `G1-08`: checkpoint externo atrás do local não permite continuar por suposição.
9. `G1-09`: testemunha honesta modelada impede regressão ou dupla história no mesmo contador.
10. `G1-10`: resposta cruzada de outra instalação é recusada.
11. `G1-11`: uma testemunha comprometida pode **ecoar o estado local revertido** com o nonce atualizado e enganar a comparação; a suíte registra esse fato como limite, nunca como autorização.

O relatório `G1_WITNESS_FINAL_REPORT` registra:
`full_local_byte_snapshot_accepted_locally=true`,
`independent_memory_head_detected_local_rollback=true`,
`same_generation_fork_refused=true`, `outage_denied=true`,
`replayed_challenge_denied=true`,
`witness_regression_refused=true`,
`witness_equivocation_refused=true`,
`compromised_witness_can_lie=true`.
Também registra `witness_authentication_implemented=false`,
`real_independent_witness_verified=false`, `power_loss_tested=false`,
`operational_gates_closed=0`, `product_built=false`,
`product_executed=false` e `no_network_or_persisted_witness=true`.

## Lacunas que impedem homologação de G1

1. **Autenticidade e identidade**: o contrato `IG1WitnessModel` é uma *hipótese de fonte autenticada*, não uma garantia implementada. Os campos `nonce` e `InstallationId` não fornecem autenticação; sem transporte autenticado, assinatura verificada e chaves fixadas/rotacionadas com segurança, um adversário pode forjar uma resposta. **Nenhuma chave privada, segredo ou serviço foi criado.**
2. **Independência e durabilidade reais**: um objeto em memória no runner não equivale a uma testemunha resistente a rollback de disco, reboot, falha física, falha de rede ou comprometimento do operador. Não há witness remoto, storage separado homologado, monotonic counter protegido, TPM ou protocolo de consenso.
3. **Inscrição inicial e perda da testemunha**: a falta de checkpoint externo não pode degradar para confiança oportunista na cópia local. É necessário processo seguro de bootstrap, recuperação, identidade por instalação e mudança de dispositivo, todos não projetados operacionalmente nesta rodada.
4. **Frescor de resposta**: o nonce evita apenas replay de resposta de outro desafio no modelo. Não há prova de unicidade pós-reboot, expiração de resposta, limite de tempo remoto, prova de estado global mais recente ou proteção contra testemunha comprometida que produz resposta nova falsa.
5. **Commit de duas partes**: falta coordenar de modo transacional a persistência local e o avanço da testemunha externa. Há falhas ambíguas se o checkpoint externo avança e o local é revertido por crash, ou se o local avança e a testemunha permanece atrás. Em ambos os casos o modelo bloqueia e requer reconciliação, em vez de declarar sucesso.
6. **Disponibilidade e privacidade**: o fail-closed pode tornar o autoatualizador indisponível quando a testemunha falhar; um serviço real também exigiria desenho de dados mínimos, consentimento, retenção, limites, ameaça DoS e revisão independente.
7. **G8**: cancelamento de I/O síncrono e prazo transacional absoluto continuam não provados, sem relação automática com o resultado da testemunha.

**Decisão formal: G1/G8 BLOCKED; G1–G8, 0 gates homologados.** Este exercício provou somente consequências lógicas sob hipótese de witness independente e íntegro, testadas contra arquivos inertes no runner Windows. Não há proteção de produto demonstrada.

## Próxima ação recomendada

Parar a repetição de testes locais equivalentes. Preparar, no CHAT ALTO, uma **matriz de design e decisão para G1**: fonte autenticada de frescor (assinatura/chave pinada, garantia de sequência, testemunha resistente à restauração), semântica de inscrição inicial, transação entre diário e witness, recuperação de estado ambíguo, limite de bloqueio offline e superfície de privacidade. Usar WORK apenas quando houver um contrato concreto a submeter a revisão independente, especialmente arquitetura TUF/âncora de confiança; não ativar infraestrutura sem autorização explícita. Preservar o estado da PR #65.
