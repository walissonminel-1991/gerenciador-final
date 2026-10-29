# GERENCIADOR FINAL — CHAT ALTO — LOTE 21 — CHECKPOINT TÉCNICO FINAL

**Data:** 2026-10-29. **Resultado:** concluído **somente como protótipo inerte**, sem integração ou execução do Gerenciador Final real.
**G1–G8 BLOCKED (0/8)**; nenhum risco residual foi formalmente aceito.

## 1. Referências verificadas e escopo
- Repositório `walissonminel-1991/gerenciador-final`; branch `experimento-g5-ipc-chat-alto-20261029`.
- Baseline congelado confirmado como HEAD antes de alterar: `05e509f7445e4556932e127eb7699e346747859a` (ahead=0, behind=0).
- **Commit técnico validado:** `76b29b38cc1d7a9b23d180fb47e14f7ebde47253`.
- `main` preservada `410d65e23546976db456337c71e4705f10217d36`; PR #65 permanece aberta em Draft, não mesclada, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- Nenhum merge, release, deploy, WORK, operação de PC pessoal, mudança em BETA .66, Cloudflare, Worker, D1, domínio, telemetria, consentimento ou credenciais. Sem alterações no GF operacional ou na documentação de fechamento de gates.
- **Modificado exclusivamente no motor compartilhado de fixtures:** `prototipos/autoatualizador-seguro/m3-l18-reconciler/L18Coordinator.cs.txt`.
- **Novos:** `prototipos/autoatualizador-seguro/m3-l21-stage-pin/L21Tests.cs.txt`, `L21StagePin.csproj`, `README.md`, `.github/workflows/autoatualizador-m3-l21-stage-pin-inerte.yml`, mais este checkpoint.
- L16/L17, TUF, WindowsFileGuard, bibliotecas e configurações de produção **não alterados**.

## 2. L21-A — bytes staged protegidos até a classificação
- `Probe` passa a transferir explicitamente a posse dos handles `WindowsFileGuard` do **staged e do seletor** ao objeto descartável `PinnedFacts`; não fecha o staged antes de `ReconcileCore` decidir OLD/NEW. Abandono por exceção fecha ambos.
- `PinnedFacts.Recheck` compara File ID, bytes exatos do staged contra a entrada autorizada Stage11/Stage3 e digest; conserva também a comparação da identidade NTFS e conteúdo do seletor. O mesmo primeiro pin continua aberto durante a reobservação posterior.
- Durante a decisão de sucesso, o pin de staged impede escrita e DELETE pelo compartilhamento NTFS de teste. Uma divergência observável retorna UNKNOWN em vez de aceitar `LocalConsistentObserved`.
- Evidência nova L21-01: após uma atualização inerte honesta, um callback entre `Probe` e a classificação tenta modificar o staged **in place**, mantendo o mesmo File ID; a tentativa é recusada. O teste não mantém um segundo handle próprio do staged durante a tentativa, evitando falso positivo.
- Evidência nova L21-02: depois de observado NEW, o teste altera os bytes do mesmo staged por escrita in-place (File ID NTFS confirmado inalterado) e repete a reconciliação; o resultado é UNKNOWN, não NEW. Canary de arquivo pessoal fictício preservado.

## 3. L21-B — pré-validação sem criar transação
- `ValidateExecutionArguments` confere prazo (>0 e <= 1 minuto), caminho exato igual ao `Environment.ProcessPath` atual, basename fixo de executável de fixture `GF.Updater.M3.L18Tests.exe`, existência e tipo do arquivo.
- `Execute` chama a pré-validação **antes** de adquirir a gate local, gravar a primeira fase, escrever `switching`, abrir uma operação concorrente ou lançar processo proprietário.
- Evidência nova L21-03: prazos zero/acima do limite, executável falso no diretório de teste com mesmo basename e caminho de executável ausente são recusados sem histórico transacional/intent/terminal; a versão OLD segue observável e uma tentativa legítima seguinte termina em `LocalConsistentObserved`. Canary preservado.
- **Limite:** igualdade de caminho com o processo atual é identificação da imagem de **teste**, não atestado por identidade de kernel, assinatura de código, medição do processo ou fronteira resistente a escritor do mesmo usuário.

## 4. Quatro CIs Windows verificadas no MESMO commit técnico
| Suíte | Execução | Resultado | Build |
| --- | --- | --- | --- |
| L21 novos | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40967413445 | 3/3 PASS, 0 FAIL | 0 warnings, 0 errors |
| L20 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40967413351 | 4/4 PASS, 0 FAIL | 0 warnings, 0 errors |
| L19 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40967413330 | 5/5 PASS, 0 FAIL | 0 warnings, 0 errors |
| L18 regressão | https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40967413375 | 7/7 PASS, 0 FAIL | 0 warnings, 0 errors |

Todas as execuções estão `completed/success` no HEAD `76b29b38cc1d7a9b23d180fb47e14f7ebde47253`. Foram verificados os logs de cada job (linhas `PASS`, `GF_Lxx_FINAL_REPORT`, compilação e ausência de `FAIL`).
**Total: 19/19 cenários em quatro CIs aprovadas.** Não extrapolar para Windows de usuários ou ciclo operacional real.

## 5. O que foi demonstrado, e o que permanece BLOQUEADO
**Demonstrado somente com fixtures e processos de laboratório:** staged pinado durante decisão, bytes autenticados e File ID rechecados, tentativa concorrente de alterar staged negada, alteração de mesmo File ID antes da reconciliação bloqueada, preflight inválido não deixa diário, nova tentativa válida, preservação das regressões L18–L20.

**Premissas do laboratório:** permissões NTFS do runner e modelo cooperativo WindowsFileGuard; L15 reconstitui confiança histórica de teste sem testemunha de frescor remoto; o arquivo de lease continua sujeito ao mesmo domínio de confiança do usuário. O código não verifica a saúde de um executável GF real.

**Não demonstrado/não oferecido:** defesa universal contra TOCTOU e adversário completo do mesmo usuário, recuperação frente a falha física de energia ou I/O kernel sobrevivente, âncora de frescor após restauração completa de disco/relógio, assinatura/custódia real de chaves, atestação de imagem executável operacional, IPC/READY de GF, ciclo de atualização real, preservação dos documentos dos PCs pessoais em ambiente não descartável ou fechamento de qualquer gate. `LocalConsistentObserved` continua um rótulo **exclusivo do protótipo**.

Arquitetura de referência B+A mantida: TUF público como autenticador de bytes, histórico local auxiliar, versões lado a lado e coordenador temporário sem elevação por padrão. Não introduz backend obrigatório, cadastro, chaves guardadas ao lado do GF, serviço residente nem TPM/DPAPI obrigatório.

**STATUS: LOTE 21 CONCLUÍDO COMO EXPERIMENTO INERTE. G1–G8 BLOCKED (0/8).**
