# CHAT ALTO — GF-UPDATE-HOLD-001 bloqueia afirmações falsas sobre OLD, NEW e canário

**Data:** 31/10/2026. **Condição:** laboratório descartável GitHub Actions Windows. **Resultado: SUCCESS dos testes negativos, não READY de atualização.**

## Identidades congeladas

- Repositório `walissonminel-1991/gerenciador-final`, única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- Commit técnico final **`bd91e6cf9b86dbfb84f857b658fc9d29ec153781`**.
- [CI Windows 41201982970](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41201982970), job `124539420349`, **completed/success** no SHA técnico acima.
- Release preexistente `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, 5 assets.
- `gf-e2e-new-win-x64.zip`, asset ID `694233137`, 57.695.630 bytes, SHA256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`: **inalterado**.
- `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset ID `694233139`, 72.618 bytes, SHA256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`: **inalterado**.
- Só os 3 roles TUF públicos de **TESTE** `targets/snapshot/timestamp` foram renovados pela CI. Assinatura de teste NÃO é autoridade de distribuição.

## Objetivo e defeito que esta rodada resolveu

O diagnóstico anterior `GF-UPDATE-HOLD-001` afirmava que arquivos OLD/NEW foram preservados após corte físico entre renomeações. O L18 já verificava os bytes das próprias fixtures e uma **etapa posterior** de CI verificava os diretórios portáteis, mas o código que escrevia o aviso positivo não exigia ele mesmo uma nova verificação exata de TODOS os arquivos físicos no momento da emissão. Isso poderia permitir uma afirmação positiva prematura diante de alteração ou ausência no backup, staging ou canário.

A alteração vinculou a **geração do aviso positivo** a um verificador de somente leitura, `RequireGuidancePhysicalIntegrity`, dentro do mesmo `Phase1RecoveryTests.cs.txt`, usando os arquivos esperados no `RealLabContext c.Files` do **mesmo L18 autenticado**, com validação completa do conjunto de caminhos, rejeição de reparse, tamanho e SHA256 para OLD e NEW, e comparação exata do canário. Só após TODOS esses testes passarem `WriteVerifiedHoldGuidance` pode criar os arquivos `.txt` e `.json` da orientação. Não foi criado outro seletor, fonte de confiança, reconciliador ou mecanismo de recuperação.

## Seis negativos de integridade física

O teste `VerifyGuidanceNegativeCopies` cria seis DIRETÓRIOS DESCARTÁVEIS e isolados sob `RUNNER_TEMP`; **nunca modifica os diretórios originais** `gf-e2e-old-preserved` e `gf-e2e-authenticated-new`, o canário original ou seletor `new/2`. Para cada cenário, chama o MESMO gerador de aviso `WriteVerifiedHoldGuidance` utilizado na prova positiva e exige a rejeição correta antes de qualquer escrita de afirmação positiva:

| Caso | Evidência exigida do L18 |
|---|---|
| `old-modified` | `REAL_GUIDANCE_OLD_BYTES_INVALID` |
| `old-missing` | `REAL_GUIDANCE_OLD_FILESET_INVALID` |
| `new-modified` | `REAL_GUIDANCE_NEW_BYTES_INVALID` |
| `new-missing` | `REAL_GUIDANCE_NEW_FILESET_INVALID` |
| `canary-modified` | `REAL_GUIDANCE_CANARY_INVALID` |
| `canary-missing` | `REAL_GUIDANCE_CANARY_INVALID` |

Todos os 6 casos precisam obter `blocked=true`, `positive_preservation_claim_written=false`, sem arquivos de orientação `.txt/.json` gerados nas respectivas cópias, e sem rollback, retry, cleanup ou READY. A prova negativa é salva em `gf-e2e-guidance-negative-results.json` apenas no runner temporário de teste. Após os casos, o próprio L18 revalida a integridade dos diretórios originais.

O workflow `.github/workflows/gf-github-draft-e2e.yml` ganhou um passo INDEPENDENTE de somente leitura, que confere o esquema, número de testes, seis códigos individuais, o estado de cada cópia alterada ou incompleta, a AUSÊNCIA dos arquivos positivos nos casos rejeitados e novamente os hashes físicos de todos os 284 arquivos OLD contra inventário pré-start e 284 arquivos NEW contra o manifesto original, canário e hash do seletor `new/2`. Exige `operational_ready=false`.

## Prova final verificada na CI 41201982970

Os passos importantes terminaram `success`: compilação de OLD e do mesmo L18, download pelo GF OLD iniciado pela MainForm, revalidação TUF de teste do ZIP, corte físico entre renomeações, contenção `UnknownBlocked` com replay, geração de orientação positiva para a condição ÍNTEGRA do laboratório, seis negativas de integridade, verificação independente e negativos anteriores do ZIP adulterado/truncado.

Marcadores reais dos logs do job `124539420349`:

```
GF_E2E_FIRST_DIRECTORY_MOVE_OBSERVED old_backup_present=true retry=false
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_CUT_L18_REPLAY_HOLD original_selector=new/2 outcome=UnknownBlocked replay=UnknownBlocked rollback=false retry=false cleanup=false READY=false
GF_E2E_MISSING_INSTALL_READONLY_VERIFIED code=GF-UPDATE-HOLD-001 old_files=284 new_files=284 canary=UNCHANGED selected=new/2 original_path=ABSENT no_recovery_no_retry_no_cleanup=true READY=false
GF_E2E_GUIDANCE_NEGATIVES_INDEPENDENT_PASS old_modified=BLOCK old_missing=BLOCK new_modified=BLOCK new_missing=BLOCK canary_modified=BLOCK canary_missing=BLOCK false_preservation_claims=0 original_bytes_unchanged=true selector=new/2 rollback=false cleanup=false READY=false
GF_DRAFT_NEGATIVES ... modified rejected=true ... interrupted BadZipFile rejected=true
```

**Conclusão limitada:** 6/6 rejeições, 0 declarações falsas de preservação, cópias alteradas ou incompletas exclusivamente no runner, OLD/NEW/canário originais não alterados, mesmo `new/2`, `UnknownBlocked`, nenhuma restauração, limpeza, reexecução automática ou READY.

## Histórico das três tentativas relevantes

1. Commit `5037891be84d1e9c2cb1d2a303a6236fc9375e75`, [CI 41201325119](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41201325119): **failure de compilação L18** (`CS1010`, newline literal não escapado em string de canário adulterado). Não chegou às negativas.
2. Commit `946934cd0a814a1e078ce917598318d19eb00395`, [CI 41201491821](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41201491821): L18 compilou; depois uma verificação independente tentou ler `gf-e2e-old-preserved` ANTES de o L18 assíncrono completar a primeira renomeação, emitindo `VERIFICATION_SOURCE_MISSING`. Esse erro **não** comprova falha de autenticidade dos bytes.
3. Commit `176bb23950dcd2dc9139541cba013bae6046578e`, [CI 41201733027](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41201733027): acrescentou polling ESTRITAMENTE READ-ONLY e limitado a 100 segundos para o primeiro backup aparecer, sem repetir atualização. CI passou pela revalidação, mas o gerador positivo recusou `RUNNER_TEMP` por exigir apenas SUBDIRETÓRIOS, `REAL_GUIDANCE_OUTPUT_OUTSIDE_RUNNER`.
4. **Commit técnico final `bd91e6cf9b86dbfb84f857b658fc9d29ec153781`**: permite exclusivamente `RUNNER_TEMP` ou subdiretórios dele como destino do aviso de laboratório; caminhos externos continuam negados. **CI 41201982970 SUCCESS**, incluindo todas as seis negativas.

**Pontos não apresentados como resolvidos:** antiga falha esporádica `IOException` da CI `41195335850` ainda não teve causa exata identificada. O teste `interrupted` do downloader é truncamento de ZIP, NÃO queda de conexão HTTP real.

## Limites e próxima direção

- O aviso continua sendo EXTERNO AO APLICATIVO, somente arquivo/JSON de laboratório sob `RUNNER_TEMP`; o executável do GF está ausente do caminho original após esse corte. Nenhuma janela de diagnóstico do produto real foi implementada ou demonstrada.
- **A verificação das cópias negativas não é prova de resiliência TOCTOU contra o mesmo usuário, nem de transação NTFS durável durante power loss**, de recuperação ou de autoridade TUF pública. `HOLD` continua sem permissão para rollback/limpeza/retry/READY.
- Próximo objetivo técnico **opcional** (se expressamente autorizado): teste de **interrupção HTTP REAL durante o download do ZIP pela instância GF OLD**, diferente de ZIP truncado artificial, demonstrando que OLD permanece utilizável e NEW NÃO é executado diante de rede incompleta. Foco na autoatualização funcional, sem arquitetura paralela nem ampliação de recuperação.
- `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, releases públicos, credenciais persistentes, merge/deploy e WORK **não foram tocados**.
