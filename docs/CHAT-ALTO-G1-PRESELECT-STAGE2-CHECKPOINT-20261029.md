# GERENCIADOR FINAL — CHAT ALTO — G1/Etapa 2: preparo em disco sem seleção antecipada

**29/10/2026 — Checkpoint experimental. Nenhuma homologação operacional.**

## Estado e escopo

- Repositório: `walissonminel-1991/gerenciador-final`
- Branch experimental isolada: `experimento-g5-ipc-chat-alto-20261029`
- HEAD inicial confirmado: `7d0087391a7efd701a6f1b54ddf8e2f42f09bf71`
- Referência: `docs/CHAT-ALTO-G1-TRANSACAO-TESTES-CHECKPOINT-20261029.md`
- Código de ensaio validado: **`ff5aef148c4514b136739233a77d30e2f92e5db3`**.
- CI Windows 2022: [40899933825](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40899933825), **SUCCESS, 10/10 PASS, zero warnings/errors**.
- CI anterior [40899692020](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40899692020), falha de build por **três CA1416** no acesso Windows NTFS `WindowsFileGuard`; corrigido com anotação `SupportedOSPlatform("windows")` no método Windows-only. Não suprimimos análise de plataforma nem alteramos asserções de segurança.
- PR #65 Draft preservada no SHA `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Sem merge, release, deploy, alterações no GF, main, BETA .66, worker/D1, flags de telemetria, serviços, segredos, contas ou PCs pessoais. WORK não utilizado.

## Vulnerabilidade arquitetural investigada

Na implementação experimental antiga da Etapa 2, `FixtureSession.Begin` executa:

1. validador do inventário/selector; `Store.Append(intent)` com `Flush(true)`;
2. `Files.ReplaceSelector(...previous,candidate)`;
3. `Store.Append(selected)`.

**Não existe compromisso autenticado G1 anterior ao passo 2.** Esse fluxo original não foi modificado. A hipótese específica era demonstrar uma ordem alternativa em laboratório na qual a escrita durável de `intent` não cause exposição do candidato nem quando o processo morra entre as syscalls.

## Nova subprova isolada e invariantes testadas

Criados exclusivamente:

- `prototipos/autoatualizador-seguro/m3-g1-preselect/G1Preselect.cs.txt`: novo `G1Preselect.Prepare` de escopo restrito e uso apenas em fixture inerte, usa `FixtureSession` para lock exclusivo/pinagem de diretórios, checks de consentimento, selector e bundles, e `PersistentState.Append` **original** para escrever uma geração `intent` com piso de metadados avançado. **Não** chama `ReplaceSelector`, `Confirm`, `Recover`, `Begin` ou `selected`.
- `G1PreselectTests.cs.txt`: 10 testes novos no Windows, incluindo três subprocessos filhos com corte de escrita e encerramento real de **somente esses subprocessos de laboratório**.
- `G1Preselect.csproj`: compila fontes Stage2 originais e o modelo transacional **somente de memória** para simular commit de witness; não integra o projeto principal.
- `.github/workflows/autoatualizador-m3-g1-preselect-inerte.yml`: runner `windows-2022`, SDK exato `10.0.402`, `dotnet restore --locked-mode`, valida relatório e ausência dos fontes novos nos Compile items do GF.

### Invariantes e resultados

| ID | Invariante / ameaça | Resultado da CI |
|---|---|---|
| P1 | `intent` gravado como geração persistente Stage2, `confirmed` permanece bundle antigo, seletor não muda, `SelectionForFixtureInspectionOnly` recusa `RECOVERY_REQUIRED` | `PRE-01` PASS |
| P2 | Sem consentimento explícito, não cria nova geração | `PRE-02` PASS |
| P3 | Segunda tentativa não pode sobrepor uma transação `intent` pendente | `PRE-03` PASS |
| P4 | Filho é morto no hook `record-created`; sobra pending vazio, `Load` recusa `STATE_INCOMPLETE`, seletor antigo | `PRE-04` PASS |
| P5 | Filho é morto **após** `FileStream.Flush(flushToDisk:true)` no hook `record-flushed`, antes de `File.Move`; pending completo preservado, `Load` recusa, seletor antigo | `PRE-05` PASS |
| P6 | Filho é morto **após** `File.Move` no hook `record-promoted`; geração `intent` está completa e reabre; `selected/confirmed` não são expostos, seletor antigo | `PRE-06` PASS |
| P7 | Testemunha em memória avança por CAS, mas não existe mecanismo de ativação neste protótipo | `PRE-07` PASS |
| P8 | Testemunha em memória faz CAS e **perde resposta**; ainda não há seleção nem adoção automática de recibo | `PRE-08` PASS |
| P9 | Candidato inerte adulterado é rejeitado por identidade/hash antes de escrever a intenção | `PRE-09` PASS |
| P10 | Seletor de fixture contradiz head confirmado antes de iniciar; preparo é recusado, sem alterar ledger | `PRE-10` PASS |

O processo pai aguarda a mensagem de pausa real enviada pelo filho, encerra **apenas o próprio executável filho**, espera `HasExited` e aguarda liberação do lease antes de inspecionar a fixture. Não são usados os PCs pessoais nem um processo do Gerenciador Final.

### Relatório da CI

`G1_PRESELECT_FINAL_REPORT` apresentou:
`original_selector_preserved=true`,
`killed_worker_exited_before_inspection=true`,
`pending_blocks_journal=true`,
`flushed_pending_preserved=true`,
`promoted_intent_still_no_candidate_selection=true`,
`memory_witness_commit_did_not_activate=true`,
`no_real_witness_used=true`,
`stage2_flush_called_not_physical_durability=true`,
`operational_gates_closed=0`,
`real_external_witness_authenticated=false`,
`product_built=false`,
`product_executed=false`,
`physical_power_loss_tested=false`,
`selection_activation_supported=false`.

## Limitações técnicas e riscos não resolvidos

1. **`Flush(true)` não prova resistência física:** a CI provou chamadas reais do código Stage2 e o comportamento de processo interrompido em pontos **após** a syscall. Não testou corte de energia, corrupção de setores, persistência de metadados de diretório nem interrupção dentro de `FlushFileBuffers`.
2. **A testemunha não é real:** `G1AtomicMemoryWitness` não é autenticada nem durável; seu CAS não é compromisso externo. O teste `PRE-07/08` demonstra **ausência de seleção por desenho da API**, não a possibilidade de autorizar uma seleção segura depois de um compromisso autenticado.
3. **O piso Stage2 sobe durante intent:** é a autorização de metadados/TUF modelada pela Etapa 2; **não** significa que G1 autenticou a versão ou que ela foi instalada. Na integração futura, separar de modo inequívoco `trustFloor`, `witnessCommitted`, `ready` e `selected`.
4. **O caminho antigo continua presente:** `FixtureSession.Begin` ainda troca o selector após intent sem testemunha externa. O ensaio NÃO é uma correção do produto ou dessa implementação original. Antes de qualquer integração é obrigatório eliminar/encapsular o fluxo anterior e provar que nenhum chamador consegue ativar o candidato prematuramente.
5. **Não há ACK G1 persistente** nem checkpoint estável com prova de extensão; um CAS remoto confirmado e um diário local intent têm estados diferentes que exigem reconciliação autenticada sem autopromoção.
6. **TOCTOU e ameaça mesmo usuário** continuam: o lease de instalação é cooperativo e só prende handles usados durante a sessão; não é prova contra modificação maliciosa do diretório nem contra rollback integral, como demonstrado nos M3 anteriores.
7. **G8 e G2** permanecem independentes: cancelar uma espera não cancela necessariamente gravação síncrona já iniciada, e nenhum teste aqui valida assinatura TUF operacional.

**Decisão:** G1–G8 **BLOCKED, 0/8 gates homologados**. Não criar release, merge, deploy ou habilitar telemetria. Não aumentar o roadmap funcional do GF com provas de laboratório.

## Próxima fronteira com ganho de garantia

Um protocolo futuro precisará implementar `prepare/ack` e um **checkpoint G1 estável não executável** antes de qualquer seleção, preservando transação, epoch, nonce e prova autenticada. A próxima investigação recomendada é definir a correspondência entre `intent` persistida e `TxProposal` CAS e os estados de reconciliação `unseen/remote-ahead/conflict` após crash, **sem introduzir `ReplaceSelector`** até que haja autenticação e os gates independentes. Testes novos devem cobrir um ganho ausente, e não repetir os cutpoints aprovados.

**WORK** apenas para revisão independente do protocolo consolidado quando houver um ponto especializado e critérios concretos; não gastar carga para repetir fixtures do mesmo escopo.
