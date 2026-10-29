# GERENCIADOR FINAL — CHAT ALTO — Lote 14
## Duas versões inertes, interrupções de processos Windows e recuperação idempotente
**29/10/2026 · checkpoint após CI aprovada · G1–G8 BLOCKED (0/8)**

### 1. Escopo e estado inicial verificado
- Repositório: walissonminel-1991/gerenciador-final.
- Branch experimental exclusiva: experimento-g5-ipc-chat-alto-20261029.
- HEAD inicial exato: a1dce03bf3ab5e3a82d801e7467a8567d5ae9561; comparação inicial idêntica, sem alterações concorrentes.
- Checkpoint do Lote 13 relido naquele HEAD: docs/CHAT-ALTO-LOTE13-NTFS-ACL-TOCTOU-UNKNOWN-CI-CHECKPOINT-20261029.md.
- PR #65 preservada em Draft, aberta e não mesclada; branch protegida work-integracao-final-20261027, HEAD de referência ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc.
- Nenhuma ação realizada em main, PR #65, BETA .66, Cloudflare, Worker/D1, domínio, consentimento, telemetria, chaves, PCs pessoais ou dados reais. Não houve WORK, merge, deploy, release ou build do GF principal.

### 2. Implementação técnica na branch experimental

Cinco arquivos adicionados:
1. prototipos/autoatualizador-seguro/m3-l14-recovery/L14Recovery.csproj — runner .NET 10 standalone, EnableDefaultItems=false, fontes .cs.txt explícitas, TreatWarningsAsErrors=true, IsPublishable=false; sem dependências externas de pacotes.
2. L14Model.cs.txt — fixture Windows somente texto em TEMP, com duas versões de texto old.fixture.txt/new.fixture.txt, seletor active.fixture.json, recibos intent.fixture.json/terminal.fixture.json, guardas de raiz e marcador, trava exclusiva de arquivo, Flush(true), substituição de UM único seletor por File.Replace, reabertura e retorno tipado estritamente observacional.
3. L14RecoveryTests.cs.txt — 21 cenários, processos filhos reais interrompidos com Kill(entireProcessTree:true), processo novo para recuperação, verificação após cortes e idempotência.
4. README.md — contrato e limitações expressas: nenhuma integração com GF, atualização, elevação, I/O externo ao sandbox ou alegação de atomicidade multiarquivo/power loss.
5. .github/workflows/autoatualizador-m3-l14-recuperacao-processo-inerte.yml — CI Windows 2022, SDK .NET 10.0.402, checkout sem credenciais persistentes, relatório fail-closed, checagem de vazamento de fontes na compilação do projeto principal.

**Commit técnico inicial:** 55a4ffbdbed1f885a693a84680ff68b5bd0460b2.
**Commit de correção da nulidade:** fefbecd1418b6de2c7247c7914f5b166eb13aa2c.
**Nenhum arquivo preexistente do repositório foi modificado.** A correção alterou somente a fonte de testes L14 recém-criada.

### 3. Algoritmo de laboratório, não uma transação de produção

As duas versões são bytes fixos de texto com marcadores NO-EXE-NO-DLL. A operação mantém trava FileShare.None dentro de sandbox cujo nome é gf-u2-l14-UUID e que exige marcador .gf-u2-fixture. O processo de teste produz, em ordem, stage de NEW, recibo de intenção, arquivo temporário do seletor, File.Replace do único seletor e recibo terminal. Os pontos de corte são emitidos somente após a etapa de interesse, com o processo parado e posteriormente morto de verdade pelo pai.

A recuperação é executada em novo processo, após a terminação do processo original e a liberação da trava:
- Sem intenção: selector old reconhecido, não ocorre promoção.
- Com intenção, sem terminal: se o selector aponta para new, o teste restaura o selector old; com old já selecionado, não reexecuta promoção. Reabrir novamente não altera o selector.
- Com terminal íntegro, selector new e bytes das duas versões íntegros: devolve somente NewInertObservedBlocked, nunca readiness, instalação ou execução.
- Diante de adulteração de seletor, recibos, candidato ou old: UNKNOWN bloqueado, sem recuperação otimista ou alegação de segurança operacional.
- A versão antiga é mantida em disco no diretório de fixture mesmo quando selector fictício aponta para new.
- O timeout da espera não extingue o processo; uma segunda recuperação concorrente encontra a trava ocupada e não atua.

O File.Replace corresponde a um único arquivo sob condições concretas do runner e **não demonstra atomicidade da árvore de instalação**, semântica de storage durante falha elétrica, execução do aplicativo após atualização, recuperação de processos reais nem proteção contra autoridade adversária de mesmo usuário. A validação de recibos é local e NÃO criptograficamente independente. Tempo, assinaturas e fonte TUF não são o foco do L14.

### 4. Evidência rastreável da CI

Primeira execução, **falha de compilação** (antes de rodar testes): https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40941118153, HEAD 55a4ffbdbed1f885a693a84680ff68b5bd0460b2. Erro CS8602 por possível nulo na linha de saída do child; corrigido com guarda que lança erro quando não existe resposta.

Execução de validação pós-correção: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40941317275, HEAD fefbecd1418b6de2c7247c7914f5b166eb13aa2c. Job two-versions-process-crash terminou SUCCESS.

Logs CI: Build succeeded, 0 Warning(s), 0 Error(s). Linha GF_L14_FINAL_REPORT: **passed=21, failed=0**; flags de processos reais mortos, reinício em processo distinto, trava concorrente, rollback de selector pré-terminal e timeout sem cancelamento = TRUE. Flags de atomicidade multiarquivo, power loss físico, instalação, build/execução do GF, helper privilegiado e gates fechados = FALSE ou zero. Check Compile não encontrou L14 incorporado no executável principal.

| IDs | Caso demonstrado |
|---|---|
| L14-01 | Inicialização sem intenção ou NEW mantém OLD |
| L14-02 | Morte antes do staging; nova execução observa OLD |
| L14-03 | Morte após gravar NEW com Flush(true); nova execução observa OLD |
| L14-04 | Morte após gravar INTENT; nova execução observa OLD |
| L14-05 | Morte após flush de selector temporário, antes de File.Replace; OLD |
| L14-06 | Morte após File.Replace, antes do terminal; nova execução restaura OLD, generation=3 |
| L14-07 | Morte após terminal íntegro; NEW somente observado, OLD guardado |
| L14-08/09 | Reabertura/recuperação repetida não muta selector novamente |
| L14-10/11/12 | Intent/selector/terminal adulterados causam UNKNOWN bloqueado |
| L14-13/14 | Bytes de NEW adulterados ou ausentes após terminal causam UNKNOWN |
| L14-15 | Intent de outra instalação de fixture é recusado |
| L14-16 | Recuperador concorrente com lease ocupado é bloqueado |
| L14-17 | Timeout da espera não termina processo ainda vivo |
| L14-18 | Corromper OLD gera UNKNOWN, sem reescrever automaticamente |
| L14-19 | Terminal artificial emitido com selector OLD causa UNKNOWN |
| L14-20 | Raiz fora do sandbox exclusivo não é aceita |
| L14-21 | Todos os seis pontos de corte conservam OLD intacto |

### 5. Demonstrado / hipótese / pendente

**D (demonstrado):** kill real do apphost de teste em Windows; reabertura de arquivos de texto após término em processo separado; retorno conservador e idempotência no estado de fixture; File.Replace de um selector real no NTFS da CI; bloqueio de lease concorrente; 21/21 casos de CI e isolamento de compile. Os bytes da versão OLD foram preservados em todos os cortes previstos.

**H (hipóteses):** o runner Windows e TEMP são máquinas/locais descartáveis confiáveis; os arquivos são inertes e pequenos; o uso de File.Replace é apenas um passo local; os testes não exercitam adversário com kernel/admin nem AV; Flush(true) não equivale a power loss controlado; os recibos não constituem audit trail independente, nem autenticidade.

**P (pendências):** transação de árvores reais, observação de startup e saúde, IPC protegido, identidade e privilégio dos processos, ACL real em Program Files, junction e TOCTOU em ambiente hostil, persistência após crash do processo em meio a I/O nativo, interrupção do disco/energia, confiança root TUF e antirrollback após restore integral, G2 interoperabilidade e D6-02, G3/G4 proveniência, G5/G6 READY, G8 encerramento comprovado de handles e do I/O tardio. Nenhum dos gates G1–G8 foi fechado.

### 6. Decisão e próxima frente

O L14 ultrapassa testes puramente de estado em memória ao demonstrar interrupção e retomada de **processos Windows distintos**, com duas versões e File.Replace somente do seletor de fixture. Não há licença para promover esse comportamento para o produto GF. Antes de qualquer integração, auditar o desenho de recuperação diante de adversários, matriz de todos os caminhos de decisão, durabilidade dos recibos e privilégios. Uma próxima etapa experimental com valor mensurável seria ligar, ainda inerte, evidência TUF de fixture e identidade Windows pinada a uma geração de seleção, com revisão específica de concorrência e janela TOCTOU; depois usar WORK pontualmente como auditor crítico, não para repetir roteiros extensos.

**STATUS: Lote 14 concluído tecnicamente com 21/21 em CI Windows. G1–G8 permanecem BLOCKED (0/8). Sem WORK, merge, release ou operação sobre GF.**
