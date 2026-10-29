# CHAT ALTO — M3/G8/G1: classificação conservadora de pending — 29/10/2026

## Escopo e baseline

- Repositório: `walissonminel-1991/gerenciador-final`
- Branch experimental: `experimento-g5-ipc-chat-alto-20261029`
- HEAD inicial confirmado: `8f7c99b818a2a72431c107c4c40b2ef5edeea5a6`
- PR #65, branch `work-integracao-final-20261027`, HEAD preservado:
  `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`, Draft, sem merge.
- Suíte M3 anterior: CI [40891129725](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40891129725), 10/10. Não foi reexecutada por esta missão.
- **Suíte específica atual:** CI [40892783194](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40892783194), commit
  `c4240dcb5b85b1ac93b9ae7af9845b9ffd31dd2b`, **success**.
- WORK utilizado: zero. Versão GF, PR #65, main, BETA .66, deploy, release e flags de telemetria: **inalterados**.

## Problema abordado

`PersistentState.Load` recusa todos os arquivos `pending-*` com
`STATE_INCOMPLETE`, inclusive arquivos completos após `Flush(true)` que
não foram promovidos. A recusa é correta e permanece intocada, mas não
distingue os casos para diagnóstico. A subprova acrescentou um inspetor
**somente de leitura** em
`prototipos/autoatualizador-seguro/m3-pending/PendingInspection.cs.txt`
e testes independentes `PendingTests.cs.txt`.

O inspetor exige sandbox marcada sob diretório temporário, lock exclusivo
de instalação, nomes canônicos dos arquivos de geração e limite de
quantidade. Valida cada registro do prefixo persistido com
`StateCodec.Unwrap`, `StateCodec.Follows`, digest de encadeamento,
gerações contíguas e não reutilização de transação/nonce. Para o
`pending`, verifica envelope/estrutura, geração esperada, digest do
antecessor e transição de estado, sem registrar o arquivo como commit.

Por fim, consulta o seletor de fixture por `FixtureFiles.ReadSelector`,
confere a fase da transação e **pina/valida os dois bundles** com os
mecanismos NTFS existentes da Etapa 2. Falha em qualquer dependência é
classificada como evidência incompatível e **não** como candidata segura.

Classificações disponíveis:
`PlausibleSealedUncommitted`, `MalformedOrTruncated`,
`ForeignOrStale`, `SelectorOrBundleInconsistent`,
`UnknownLedgerEntries`, `MultiplePending`,
`InvalidCommittedPrefix` e `NoPending`. **Todas** as avaliações
mantêm `Blocked=true` e `Mutated=false`. Uma classificação
estruturalmente plausível não representa confiança criptográfica,
aprovação, liberação de gate, autorização de escrita nem recomendação de
promover/apagar o pending.

## Evidência Windows — testes negativos isolados

CI Windows 2022 [40892783194](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40892783194),
.NET SDK `10.0.402`, dependências `Tuf 1.1.1` e
`BouncyCastle 2.7.0` fixadas em `packages.lock.json`, restore
`--locked-mode`: **12 testes aprovados, 0 falhas, 0 warnings e 0 errors**.

1. `PEND-01` — registro sealed de próxima geração: plausível mas **bloqueado**.
2. `PEND-02` — registro truncado: recusado.
3. `PEND-03` — geração futura/incorreta: recusada.
4. `PEND-04` — transação/identidade alterada: recusada.
5. `PEND-05` — dois registros pendentes: recusados.
6. `PEND-06` — arquivo desconhecido no ledger: recusado, preservado.
7. `PEND-07` — geração do prefixo persistido corrompida: recusada.
8. `PEND-08` — sem pending: apenas diagnóstico; não concede autorização.
9. `PEND-09` — lease exclusivo detido por concorrente: inspeção recusada.
10. `PEND-10` — nome não canônico de pending: recusado.
11. `PEND-11` — envelope válido, mas seletor inconsistente: recusado.
12. `PEND-12` — bundle adulterado: recusado.

Os testes comparam inventário de nomes + SHA256 de **todos os arquivos
das sandboxes antes/depois da inspeção** e verificam que preferências,
legado e arquivos desconhecidos permanecem preservados. O relatório
registra `pending_auto_promoted=false`,
`pending_auto_deleted=false`, `inspection_preserved_bytes=true`,
`product_built=false`, `product_executed=false`,
`operational_gates_closed=0`, `durable_power_loss_tested=false`,
`external_freshness_witness=false`.

O workflow `.github/workflows/autoatualizador-m3-pending-isolado.yml`
roda somente se arquivos desse subprotótipo ou o próprio workflow
mudarem; o teste M3 anterior não foi reexecutado. Verifica adicionalmente
que nenhuma classe do novo protótipo entrou nos Compile items do GF.
Nenhum dado de diagnóstico é enviado a serviços externos.

## Limites críticos

- O checksum e o encadeamento são verificações de integridade local,
  **não** detectam com garantia uma reescrita coerente por um adversário
  do mesmo usuário. A exclusividade do lease não impede que um
  processo malicioso fora do protocolo altere arquivos; leituras
  separadas podem sofrer corrida entre verificação e uso.
- O inspetor só lê e classifica fixtures sob sandbox temporária
  marcada. Não há liberação, promoção, exclusão, rollback automático,
  limpeza de `pending`, atualização nem instalação real.
- O teste não prova interrupção segura de I/O síncrono, fsync em
  armazenamento real, garantia sob queda elétrica, hardware fault
  injection, rollback completo de disco nem witness externo de
  frescor. **G8/G1 seguem bloqueados**, assim como G2, G5, G6 e
  todos os demais gates. A homologação física da BETA também não foi
  executada.
- A sonda usa identificadores de bundle e registros de laboratório;
  não demonstra identidade autenticada de release GF e não integra
  o autoatualizador ao produto.

## Próximo passo recomendado no CHAT ALTO

Planejar uma **prova negativa de corrida entre inspeção e uso do
registro/selector**, em ambiente descartável, ou um modelo de witness
externo de frescor G1 sem deploy ou credenciais. Evitar prolongar
suítes repetidas sem ganho de garantia. Uma solução real de proteção
contra rollback integral ou desligamento elétrico dependerá de
infraestrutura e homologação próprias; não utilizar o WORK sem
necessidade concreta e critérios verificáveis.
