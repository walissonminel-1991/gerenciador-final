# GERENCIADOR FINAL — CHAT ALTO — Lote 5: TUF assinado + Stage2 + CAS único sobre os mesmos bytes
**29/10/2026 · checkpoint pós-WORK · subprova experimental INERTE · G1–G8 BLOCKED (0/8)**

## 1. Baseline, evidências e preservação

- Repositório `walissonminel-1991/gerenciador-final`; branch **exclusiva** `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial lido/confirmado: `5768ec3164b18778de8d4be9ca5528ef1fe33dc3`.
- Contrato pré-código: `docs/CHAT-ALTO-LOTE5-CONTRATO-MESMOS-BYTES-TUF-STAGE2-CAS-20261029.md`, commit `e09a56fe31b8ab6d7da86d446708f87fb7172a54`.
- Código e workflow L5 submetidos até o commit **`99a60a14ad3cfcdfa4233676665658f712ab76d3`**; [CI Windows **40922528166**](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40922528166) **SUCCESS — 6/6 PASS, 0 FAIL, 0 warnings, 0 errors**.
- Alteração mínima e retrocompatível em modelo experimental L4: `L4TufProjection.ValidatedStage3BytesDigest`, `L4CompositeProjection.PreparedOperationDigest` e `CompositeCheckpointBytesDigest` (defaults vazios apenas para casos históricos). [CI regressiva L4 **40921817107**](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40921817107) **SUCCESS**, 12/12 em execução automática de push; não houve repetição manual da suíte L3.
- PR [#65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65), branch `work-integracao-final-20261027`, HEAD protegido `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; conferir novamente após salvar.
- Nenhum código do executável GF, `FixtureSession.Begin/Confirm`, `IntegratedPipeline`, `TrustIntegratedPipeline`, main, BETA .66, backend, flags, Worker/D1, domínio, telemetria, serviço, chave operacional, release ou deploy foi modificado. WORK não foi utilizado.

## 2. Diferença em relação aos Lotes 3 e 4

**L3** já fazia refresh TUF Stage11 autenticamente assinado por chaves **de teste** e vinculava a cadeia `root→timestamp→snapshot→targets→ZIP→manifest→bundle.fixture.txt` a um `Stage3TufTarget`. O intent e o terminal possuíam âncoras inertes **separadas**.

**L4** introduziu CAS de `IntentCommit → TerminalCommit` com um único head monotônico, retries idempotentes, recibos consultáveis e concorrência, porém sua entrada TUF era **inteiramente sintética** e não continha o digest da estrutura Stage3 completa nem da operação Stage9.

**L5** ligou as duas provas no mesmo processo descartável:

1. `DurableTufAttempt.Get` verifica a atualização `release2` com fonte TUF Stage8 fixada e patch de laboratório; `DurableEpochCache` registra papéis autenticados; `Stage3TufBinding.Bind` valida versão, referências, ZIP, conteúdo e manifesto.
2. `OperationJournal` Stage9 registra `admitted → authorized → prepared` com candidato/permit estritamente **inertes**, sem permissão real; `G1Preselect.Prepare` grava `intent` Stage2 mantendo selector antigo.
3. `InertCompositeBuilder.Build` relaciona `TrustView.Digest`, `OperationRecord.Digest`, `Stage2IntentDigest`, candidato e instalação; `InertCompositeAnchor` cria somente o predecessor **diagnóstico** Stage3. Essa âncora ainda é um objeto separado para materializar o checkpoint Stage3; **não é outra testemunha operacional**.
4. `L5FrozenProof.EvidenceFromStage3` congela `StateCodec.Encode(Stage3TufTarget)` completo e insere seu SHA no `L4TufProjection.ValidatedStage3BytesDigest`. Copia os SHA reais dos quatro papéis, ZIP e entry; inclui SHA dos bytes de `CompositeCheckpoint` Stage3 e digest do último `OperationRecord` Stage9 em `L4CompositeProjection`. Esses campos passam a fazer parte do mesmo `L4Evidence` que o `L4IntentClaim` consome. **Os bytes exatos estão vinculados ao payload**; apenas comparar nomes/versões não bastaria.
5. Antes do CAS de intent, `L5FrozenProof.RevalidateExactInput` reabre `DurableEpochCache`, reconstrói vínculo Stage3 com ZIP/BundleRef congelados, exige os mesmos bytes serializados, confere `InertCompositeBuilder.InspectBound` sobre Stage2/Stage9 e fixa identidade da entrada via `FixtureSession.Files.Pin`. Qualquer divergência bloqueia. A verificação de identidade não dá proteção eterna contra escritor não cooperativo/TOCTOU depois da liberação do pin.
6. `L4AtomicWitnessMemoryFixture.PublishIntent` recebe o **exato** `L4IntentClaim`/evidência congelada, faz CAS do head com txId/phase e retém recibo sob lock; perda de resposta **após** CAS não significa aborto. Reconciliar retorna `CommittedButNotAuthorized`.
7. Somente `FixtureSession.Recover` inerte, com selector antigo já preservado, registra `rollback_intent → rolled_back`. `L5FrozenProof.ReadRealRollbackTerminal` reabre arquivo Stage2, verifica txId/instalação/selector, chain `StateCodec.Follows` desde o **mesmo** `intent` e digest terminal exato. `L4AtomicWitnessMemoryFixture.PublishTerminal` compromete esse digest no **mesmo** head, referindo-se ao recibo de intent.
8. Nenhum retorno fornece `CanSelect`, `CanConfirm`, `Ready`, `Authorized`, instalação, rollback operacional ou release.

**Limitação formal:** o L4 modela recibos **sem assinatura** e witness **honesta em RAM**. Os campos `Stage3` são autenticados *pela fonte TUF de fixture*, não por um operador de witness. Os bytes são iguais dentro deste ensaio, mas isso não demonstra que possam atravessar uma fronteira de rede hostil sem certificado/assinatura, nem uma transação atômica entre diários distintos.

## 3. Novos casos executados — 6/6

| Caso | Contraprova/cutpoint e observação |
|---|---|
| **L5-01** | Refresh TUF assinado + Stage3 completo + operação Stage9 + `intent` Stage2 + vínculo dos SHA **integrais** Stage3/Stage9; CAS de intent **com resposta perdida após compromisso**. Reconsulta por txId recupera recibo, `Recover` inerte registra `rolled_back`, CAS de terminal **com resposta perdida após compromisso**; reconsulta/retry preservam head **seq2** sem duplo avanço. Seletor antigo intacto |
| **L5-02** | Modifica **somente `Stage3TufTarget.ManifestSha`**, campo ausente da projeção L4 antiga, mantendo `RootDigest` e `CandidateDigest` iguais; a nova vinculação do SHA de **todos os bytes Stage3** muda e `PublishIntent` recusa o corpo diferente **antes** de avançar |
| **L5-03** | Operação Stage9 muda legalmente de `prepared` para `settled` após bind, preservando TUF/intent; revalidação cruzada recusa por `COMPOSITE_OPERATION_OR_TUF_UNBOUND`, sem CAS |
| **L5-04** | Byte extra de whitespace JSON válido é acrescentado ao registro `intent` Stage2, preservando os demais arquivos; revalidação do digest composto recusa `COMPOSITE_CROSS_JOURNAL_ROLLBACK_OR_FORK`, sem CAS |
| **L5-05** | `IntentCommit` existe, mas Stage2 ainda está em `intent`; `PrepareTerminalClaim` recusa `L5_TERMINAL_NOT_SETTLED` e head continua na fase `intent` |
| **L5-06** | Após `TerminalCommit` sobre `rolled_back` real, alteração bytewise que preserva JSON interpretável faz `InspectTerminal` e tentativa de republicação recusarem `L5_TERMINAL_BYTES_CHANGED`; head witness mantém seq2 sem alteração |

A CI verificou `GF_L5_FINAL_REPORT`: `passed=6`, `failed=0`, booleanos positivos `true` para SHA completo Stage3, ambos os cutpoints, manifest extra, operação, intent, terminal ausente, byte-fork terminal, seletor antigo, refresh assinado de fixture e leitura Stage2; exigiu **false** para witness durável, recibo externo assinado, reboot da witness, power loss físico, serviço/segredo, produto e autorizações; `operational_gates_closed=0`.

## 4. Arquivos e escopo efetivo de mudanças

Novos arquivos:
- `docs/CHAT-ALTO-LOTE5-CONTRATO-MESMOS-BYTES-TUF-STAGE2-CAS-20261029.md`
- `prototipos/autoatualizador-seguro/m3-l5-composed/L5RealBytesBridge.cs.txt`
- `prototipos/autoatualizador-seguro/m3-l5-composed/L5RealBytesTests.cs.txt`
- `prototipos/autoatualizador-seguro/m3-l5-composed/L5Composed.csproj`
- `.github/workflows/autoatualizador-m3-l5-composicao-real-inerte.yml`
- Este checkpoint `docs/CHAT-ALTO-LOTE5-MESMOS-BYTES-TUF-STAGE2-L4-CHECKPOINT-20261029.md`

Modificação única em fonte **também experimental**:
- `prototipos/autoatualizador-seguro/m3-l4-cas/L4TwoPhaseModel.cs.txt` — três campos opcionais de digest para transportar prova completa; regras de CAS, fases, recibos e estado **não mudaram**. A CI automática antiga continuou aprovada, sem tornar os campos opcionais operacionais.

**Nenhum segredo/serviço**; Actions apenas leitura; build não compila L5 dentro de `GerenciadorAplicativos.csproj`. Sem drivers, limpeza, dados pessoais, UI, diagnósticos ou telemetria.

## 5. Riscos e decisões ainda abertos

- **G1:** witness externo real, esquema de assinaturas e bootstrap independente/sem TOFU; idempotência/CAS/epoch duráveis após restart; recuperação de forks e comprometimento de operador; proteção contra restauração conjunta local+witness; retenção e privacidade da identidade da instalação. Memória sob lock não fornece essas propriedades.
- **G2:** revisão humana qualificada do perfil TUF/patch/interop, decisão sobre referência Python divergente e D6-02. O refresh de fixture usa chaves públicas/testes da Etapa11, não assinaturas operacionais.
- **G8:** deadline único de tentativa, persistência do resultado desconhecido após timeout, `Flush(true)` e rename sob power loss; TOCTOU mesmo usuário/namespace após soltura de handles; homologação de máquinas descartáveis físicas.
- **G5/G6:** identidade autenticada do processo/candidato e READY efetivo, não prontidão sintética. Não executar `FixtureSession.Begin/Confirm` nem liberar selector.
- **Composição transacional:** commits ainda não atravessam o disco e witness de modo atômico; loss após CAS foi simulado, não houve restart real, falha elétrica nem quórum. A leitura de terminal `rolled_back` depende de journal Stage2 local honesto e de identidade de arquivo na sessão; sem testemunha durável essa hipótese não resiste a adversário que substitua todo o verificador.

## 6. Próxima decisão, sem consumo automático de WORK

O L5 fecha **a lacuna de transporte dos mesmos bytes em uma única fixture** entre Stage11/Stage3/Stage9/Stage2 e CAS de duas fases. O próximo ganho não é repetir hashes ou casos de checksum: deve ser definir domínio real de autoridade de G1 **antes** de integrar qualquer ação. Recomenda-se preparar um ADR comparando alternativas de witness independente (custos, privacidade, bootstrap, recuperação, offline) **sem contratar, provisionar ou implantar nada**; separar o que cabe em CI Windows dos ensaios que requerem infraestrutura externa ou revisor humano.

Pode-se programar uma **única revisão WORK pontual** quando houver decisão de arquitetura de witness para examinar, sem gastar Work com mais fixtures equivalentes.

**Status final obrigatório: G1–G8 BLOCKED — 0/8 gates operacionais homologados.**
