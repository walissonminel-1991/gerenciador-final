# CHAT ALTO — M3/G8/G1: subprova TOCTOU das evidências — 29/10/2026

## Ponto de partida e escopo

- Repositório: `walissonminel-1991/gerenciador-final`
- Branch experimental isolada: `experimento-g5-ipc-chat-alto-20261029`
- Baseline: `38c8ee651ecbddc2c6cde9f8795c3f39c51c6905`
- PR #65 Draft, branch `work-integracao-final-20261027`, HEAD preservado:
  `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- HEAD do código aprovado na CI: `f1d4480199a81ba9c1aebca2f0b37dd726f31438`.
- CI Windows 2022: [40894656649](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40894656649), **SUCCESS**.
- Nenhum WORK, merge, release, deploy, criação de contas, alteração de ACL, flags de telemetria, beta, produto principal ou infraestrutura externa.

## Problema demonstrado

`PendingInspection.Inspect` verifica a cadeia local sob `InstallationLease`,
mas a liberação do lease permite que um processo altere a seleção, a cadeia ou
os bundles antes de um uso subsequente. Mesmo com o lease detido, um processo
do mesmo usuário que **não segue o protocolo** consegue modificar arquivos
não mantidos abertos para escrita exclusiva. Um receipt classificado como
`PlausibleSealedUncommitted` não é nem deveria ser uma autorização.

A subprova adicionou apenas `prototipos/autoatualizador-seguro/m3-toctou/`:
- `M3EvidenceScope.cs.txt` — recibo de inspeção + fingerprint de
  evidências lidos sob **o mesmo lease**; revalidação consumível uma única
  vez; recusa troca de root; comparação após reacquisição do lease.
- `M3RaceTests.cs.txt` — processos filhos inertes que, nas sandboxes
  temporárias marcadas, fazem alterações controladas em pending, seletor,
  ledger ou bundle entre a inspeção e o reuso, inclusive durante o lease.
- `M3Race.csproj` — runner separado com fontes originais Stage2 e a
  classificação M3, dependências fixadas e restore locked-mode.
- `.github/workflows/autoatualizador-m3-toctou-isolado.yml` — Windows
  2022, SDK `10.0.402`, somente caminhos do novo protótipo, verifica
  ausência dos fontes M3 nos Compile items do GF.

A alteração única no código do inspetor anterior foi criar
`PendingInspection.InspectUnderLease(root,lease)` para capturar a
classificação e os hashes sem intercalar operações de um participante
**cooperativo**. `PendingInspection.Inspect(root)` conserva o contrato
original com seu próprio lease. Não se altera `PersistentState`,
`FixtureSession`, o GF nem o formato de diário.

O fingerprint inclui nomes relativos, identidade NTFS e SHA256 de cada
arquivo do ledger, seletor e arquivos de versões da fixture; limites,
guardas NTFS e ausência de subdiretórios inesperados são exigidos. O
inspetor nunca promove, remove ou modifica arquivos: o recibo mantém
`Blocked=true`. O fingerprint não é uma assinatura digital e não é uma
âncora independente de frescor.

## Evidências concretas — CI 40894656649

**8/8 PASS; 0 FAIL; 0 warning; 0 error.**

1. `RACE-01`: sem concorrente, revalidação preserva o bloqueio;
   tentativa de reuso do mesmo escopo é rejeitada.
2. `RACE-02`: segundo processo reescreve pending depois da liberação
   do lease; o recibo antigo é recusado como `M3_EVIDENCE_STALE`.
3. `RACE-03`: substituição do seletor depois da inspeção é detectada;
   inspeção nova também reconhece incompatibilidade.
4. `RACE-04`: inclusão de arquivo desconhecido no ledger é detectada.
5. `RACE-05`: adulteração do bundle candidato é detectada.
6. `RACE-06`: processo concorrente ignora `InstallationLease` e
   modifica o arquivo pending **enquanto o lease do inspetor está ativo**.
   A revalidação subsequente recusa os dados. Isso não prova que a
   alteração possa ser impedida pelo lease.
7. `RACE-07`: recibo de outra instalação não pode ser reutilizado.
8. `RACE-08`: segundo processo altera o pending, depois restaura os
   mesmos bytes no **mesmo arquivo**. A revalidação não observa o
   histórico da modificação e retorna a mesma classificação
   **bloqueada**. Essa invisibilidade é evidência explícita de
   insuficiência de hashes locais para G1, não uma autorização.

O relatório registra:
`stable_revalidation_never_authorized=true`,
`post_release_mutation_detected=true`,
`uncooperative_writer_during_lease_detected=true`,
`cross_installation_replay_refused=true`,
`transient_change_restored_undetectable=true`,
`evidence_receipt_is_not_authorization=true`,
`operational_gates_closed=0`,
`product_built=false`,
`product_executed=false`,
`independent_freshness_verified=false`,
`physical_power_loss_tested=false`.

## Fronteiras verificadas

- A revalidação reduz o risco de usar uma **observação antiga**, mas
  ainda existe uma corrida entre a última revalidação e qualquer uso
  posterior. Os handles de arquivo do fingerprint não ficam retidos
  permanentemente; o lease é cooperativo, não proteção para o caso de
  adversário do mesmo usuário.
- O cenário de alteração e restauração mostra que um adversário pode
  modificar conteúdo entre inspeções sem deixar mudança observável
  pelo fingerprint local. Identidade NTFS e SHA256 iguais não provam
  que o histórico permaneceu imutável. Um rollback de disco coerente
  pode igualmente escapar à observação local sem testemunha externa.
- Nada implementado na subprova autoriza atualização, consumo de
  pending, promoção, exclusão, rollback automático, liberação G1/G8
  ou deploy. O GF real nunca é compilado ou executado pelos testes.
- Não há prova de `Flush(true)` contra falha elétrica, fsync físico,
  fonte autenticada de frescor, proteção operacional anti-rollback,
  G5 entre contas ou homologação da BETA.

**Status:** G1–G8 permanecem **BLOCKED** (0 aprovados). O roadmap
funcional do produto é separado e não deve ser inflado por essas
subprovas de laboratório.

## Próximo ponto de decisão do CHAT ALTO

Priorizar uma arquitetura mínima de testemunha de frescor
independente para G1 **somente como especificação e modelo adversarial
inertes**, sem criar serviço, segredo, credencial, deploy ou conta.
Comparar evidências que uma testemunha externa conseguiria fornecer
com o contraexemplo de restauração integral do armazenamento local.
Não fazer novas rodadas de testes semelhantes sem uma garantia nova
identificável. Um sistema externo real e sua homologação exigirão
consentimento e autorização em etapa futura. WORK só quando uma
capacidade especializada concreta não for resolvível pelo Chat Alto e
pela CI.
