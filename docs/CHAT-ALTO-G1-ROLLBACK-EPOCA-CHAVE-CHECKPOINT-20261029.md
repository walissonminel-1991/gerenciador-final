# GERENCIADOR FINAL — CHAT ALTO — G1: rollback do pin local de confiança e da época
**29/10/2026 | Nova subprova adversarial inerte | 0/8 gates operacionais homologados**

## 1. Baseline, isolamento e resultados

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch experimental: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial verificado: `94537272b0dc5e64e5fa9ef1e3567bcb07d6c91d`.
- Checkpoint de entrada: `docs/CHAT-ALTO-G1-RECIBO-ASSINADO-CHECKPOINT-20261029.md`.
- **Commit do código aprovado na CI**: `1a203e8779601bd2d6b56998436432c9da83e24f`.
- **Windows CI**: [40903933090](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40903933090), **SUCCESS**, `windows-2022`, SDK `10.0.402`, `dotnet restore --locked-mode`; **10/10 PASS, 0 FAIL, 0 warnings, 0 errors**.
- PR #65 Draft HEAD confirmado antes da modificação: `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Reconferir após checkpoint.
- Nenhum código do GF principal, PR #65, main, BETA .66, telemetria ou infraestrutura foi alterado. Nenhum serviço, endpoint, token, chave privada operacional, deploy, release ou merge. WORK não foi acionado.

## 2. Lacuna nova demonstrada

A prova P-256 anterior testou assinatura, nonce, identidade, rotação e recusa de chaves antigas **na mesma sessão de verificador em memória**. Faltava simular o armazenamento local de **chave pública e epoch** e restaurá-lo junto com os demais arquivos da instalação, mantendo a referência de época nova **fora dessa restauração**.

Uma assinatura local só afirma que quem controla a chave privada correspondente assinou o conteúdo. Se a chave privada antiga ainda existir com o adversário, uma instalação cujo pin público e época foram revertidos pode aceitar uma **assinatura nova e criptograficamente correta da chave revogada**. O desafio aleatório novo **não** resolve o rollback da autoridade que decide qual chave é válida.

### Protótipo isolado

Novos arquivos:
- `prototipos/autoatualizador-seguro/m3-g1-anchor/G1EpochAnchor.cs.txt`: registro público `G1LocalEpochPin` (instalação, época, key ID, SPKI); `G1LocalEpochDisk` lê e valida esse arquivo **não confiável** em fixture marcada, usando `PersistentState.ReadFixtureFile`; `G1ExternalMemoryAnchorFixture` mantém checkpoint separado **somente na memória do teste**, fora do snapshot; `G1EpochReconciliation` faz comparação fail-closed sem autorização de atualização.
- `prototipos/autoatualizador-seguro/m3-g1-anchor/G1EpochAnchorTests.cs.txt`: dez casos negativos inéditos.
- `prototipos/autoatualizador-seguro/m3-g1-anchor/G1EpochAnchor.csproj`: compilação isolada reutilizando somente fontes Stage2, G1 preselect, G1 transaction e G1 crypto já aprovados.
- `.github/workflows/autoatualizador-m3-g1-epoch-rollback-isolado.yml`: CI Windows exclusiva; SDK travado; relatório exige todos os resultados e a ausência de códigos experimentais no `Compile` de `GerenciadorAplicativos.csproj`.

**Nenhuma chave privada foi persistida ou exportada.** Os dois pares P-256 são gerados pelo teste e permanecem em RAM; apenas os SPKIs públicos entram na fixture. O teste retém a chave privada antiga em memória **deliberadamente**, representando a hipótese adversarial de comprometimento/retensão da chave antiga.

### Propriedade contrastada

- **Sem referência independente:** após restaurar o diretório local ao snapshot antigo, `G1LocalEpochDisk.LocalSignatureAppearsValid` aceita a resposta recém-assinada pela chave antiga, incluindo um desafio novo e o vínculo Stage2. Isso **não** concede autorização, mas demonstra que a criptografia local **sozinha** não detecta a perda da rotação.
- **Com referência independente honesta:** a memória de `G1ExternalMemoryAnchorFixture` permanece na época 2, fora do snapshot da instalação. A reconciliação compara epoch e SPKI com ela e rejeita o estado local restaurado com `G1_ANCHOR_LOCAL_ROLLBACK`.
- **Se referência independente estiver indisponível, ausente ou for comprometida:** não há garantia de frescor. Os casos indisponível/ausente falham fechados; o adversário capaz de sobrescrever o próprio anchor pode mentir. O resultado que coincide continua `SameEpochAndKeyStillBlocked`, **nunca** `Authorized`.

Este é um experimento com **dois domínios de falha simulados no mesmo runner**. A memória separada não é um serviço remoto, TPM, contador monotônico físico, quórum ou persistência antirollback homologada.

## 3. Testes novos Windows — 10/10

| Teste | Contraprova / comportamento observado |
|---|---|
| EPOCH-01 | Rotação validada por chave antiga, escrita de pin público epoch 2 e reabertura a partir de disco: equality segue diagnóstico bloqueado |
| EPOCH-02 | **Snapshot byte a byte de todos os arquivos locais da instalação inerte**, rotação, avanço do anchor, restauração integral dos arquivos antigos; assinatura com chave antiga e novo nonce é **válida localmente**, mas a referência externa simulada recusa a regressão |
| EPOCH-03 | Após restauração, nova instância de anchor sem inscrição: `G1_ANCHOR_MISSING`, sem TOFU |
| EPOCH-04 | Anchor offline após restauração: `G1_ANCHOR_UNAVAILABLE`, sem fallback local |
| EPOCH-05 | Commit da época 2 no anchor antes de gravar pin local: `G1_ANCHOR_LOCAL_ROLLBACK` |
| EPOCH-06 | Pin local epoch 2 gravado antes do compromisso remoto: `G1_ANCHOR_LOCAL_AHEAD` |
| EPOCH-07 | Tentativa de regredir ou reutilizar checkpoint antigo recusada pelo CAS do **objeto em memória**, não por proteção física |
| EPOCH-08 | Substituição da chave pública com a **mesma época**, usando assinatura do novo par: válida localmente, mas `G1_ANCHOR_KEY_FORK` com anchor honesto |
| EPOCH-09 | Anchor de outra instalação não pode ser adotado: ausência de inscrição é bloqueio |
| EPOCH-10 | Anchor **deliberadamente comprometido** pode ser alterado para época/chave antiga e enganar a comparação. A suíte registra o limite e não autoriza execução |

O relatório `G1_EPOCH_FINAL_REPORT` confirma:
- `complete_local_snapshot_accepted_by_local_signature=true`
- `independent_memory_epoch_rejects_rollback=true`
- `missing_anchor_denied=true`, `anchor_offline_denied=true`
- `anchored_epoch_regression_denied=true`
- `remote_ahead_blocked=true`, `local_ahead_blocked=true`
- `equal_epoch_key_fork_denied=true`
- `compromised_anchor_can_lie=true`
- `selector_preserved=true`, `keyring_persisted_public_only=true`
- `private_keys_written_to_disk=false`
- `anchor_is_external_durable_service=false`
- `real_independent_epoch_verified=false`
- `product_built=false`, `product_executed=false`, `operational_gates_closed=0`
- `activation_authorized=false`, `physical_power_loss_tested=false`.

## 4. Limites que permanecem críticos

1. **O teste não reiniciou o Windows nem cortou energia.** Restaura bytes locais via I/O de fixture e reabre o arquivo público. Não prova durabilidade, segurança em `FlushFileBuffers`, nem comportamento em hardware real.
2. **O anchor é memória no mesmo processo de teste** e fica intacto por escolha do harness; essa separação é **hipótese** de domínio independente, não atestado de serviço ou relógio. Mesmo a chave privada nova continua descartável em RAM.
3. **Bootstrap de confiança não demonstrado operacionalmente.** Sem inscrição criptograficamente autenticada, o invasor pode apresentar a instalação revogada como uma instalação “nova”. O protótipo recusa ausência de anchor.
4. **Comprometimento da testemunha/anchor** pode permitir mentir ou regredir: CAS em um objeto honesto não demonstra armazenamento monotônico independente, transparência, quórum ou proteção contra equivocação.
5. **Uma assinatura antiga pode ser produzida novamente** quando a chave privada revogada não foi destruída. Não basta verificar nonce novo ou validade ECDSA.
6. **O resultado não se integra ao atualizador real:** não muda `FixtureSession.Begin`, `G1AckFixture`, formato do Stage2, TUF, IPC G5/G6, G8, UI ou seleção do candidato.
7. **A gravação do pin público no harness é intencionalmente simples**, para permitir restauração/ataque. Não deve ser tratada como estratégia de persistência operacional, nem integrada ao GF.

**Status formal: G1–G8 BLOCKED; 0/8 homologados.** As provas são complementares e não justificam ampliar o roadmap funcional.

## 5. Avaliação sobre o momento do WORK e próxima decisão

**Agora existe material técnico suficiente para uma única revisão independente focada**, caso o mantenedor decida consumir WORK: ADR `docs/CHAT-ALTO-G1-MATRIZ-ARQUITETURAL-20261029.md`, provas de CAS, sidecar, assinatura/rotação e restauração integral da chave pública, com CI e SHAs. A auditoria deve responder **especificamente**:

- Como vincular o pin de chave pública/época a uma identidade de instalação cujo disco possa ser completamente restaurado?
- Qual domínio de confiança independente seria necessário e como provar monotonicidade/indisponibilidade sem implantar serviço agora?
- Como garantir que `intent`, compromisso remoto, recibo autenticado, rotação e confirmação local sejam recuperáveis e não permitam seleção antecipada, sobretudo após crash?
- Quais garantias ficam faltando em G1 mesmo que assinatura, nonce, CAS e hash do diário estejam corretos?
- Quais critérios negativos e condições de aceite futuros devem ser exigidos para homologação física e integração G2/G5/G6/G8?

**WORK ainda não foi acionado**, preservando a carga. Sugestão de estratégia: elaborar um prompt de revisão independente com escopo congelado (sem mudanças de produto nem execução de infraestrutura) e entregáveis de matriz de riscos, correções propostas e prioridades. Não voltar a rodar as mesmas dez provas em outra pasta. Um novo teste é justificável só se acrescentar uma propriedade de ameaça identificável ainda não demonstrada.

**Preservar** PR #65 Draft, main, BETA .66, telemetria desligada, infraestrutura e todos os gates bloqueados; sem merge/release/deploy.
