# GERENCIADOR FINAL — Fase 1 — HOLD conservador, retenção OLD/NEW e veto de nova tentativa

**Data:** 30/10/2026. **Natureza:** contrato de pesquisa e especificação de **um ensaio ainda não executado**. Não é modo de produto, nova autoridade, recuperação operacional ou implementação de código.

## 1. Âncoras e objetivo preciso

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD original consultado: `f62a8be7d1625284c0b5ae30b8a4d777c0412cd5`. Último commit técnico validado: `5aa02f7decf93abfb7d03fa64c2ad9f5f057b96a`.
- CI Windows já existente: [41098083325](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41098083325), job `124208946182`, `completed/success`, 26/26 `PASS_CONTAINMENT`, zero recuperações automáticas.
- PR #65: `open/Draft/merged=false`; `main`, BETA .66, produto e infraestrutura fora da missão.
- Base da decisão: `docs/CHAT-ALTO-FASE1-H1-TESTEMUNHA-ANTIROLLBACK-MODELO-AMEACA-20261030.md` e `docs/CHAT-ALTO-FASE1-H1-CANDIDATOS-AUTORIDADE-DECISAO-NOGO-20261030.md`. H1, H2, H3 **não demonstradas**.
- Objetivo novo e delimitado: determinar se a **segunda tentativa cooperativa do mesmo C# L18** é recusada **depois** de uma transação que observou GF NEW real e concluiu em `UnknownBlocked`, antes de relançar OLD/NEW e sem sobrescrever os pacotes. Isso testa **não repetição automática sob estado local íntegro**, **não** anti-rollback contra o próprio usuário nem ausência de execução externa.

## 2. Mecanismo candidato: `HOLD_NO_AUTORETRY/0` (ESPECIFICAÇÃO; NÃO IMPLEMENTAR POR DEDUÇÃO)

**Quando:** a primeira tentativa observa NEW ou pode tê-lo executado, mas a quiescência independente não foi provada.

**Comportamento conservador:** manter OLD e NEW nos seus diretórios separados; não excluir OLD, não inverter o seletor automaticamente, não lançar OLD para health check, não iniciar uma segunda atualização/retry sobre a mesma transação enquanto o histórico local íntegro indica tentativa anterior. Permitir somente diagnóstico de dados que não execute GF, preservando o bloqueio `UnknownBlocked`. Nenhuma limpeza residual ou alteração de sistema deve ocorrer em nome desse fluxo.

**Fronteira dos efeitos:** a política se aplica às **ações cooperativas do coordenador do laboratório**, não a executáveis lançados fora dele, Windows Explorer, processos do usuário ou serviços delegados. Um arquivo de HOLD, aviso na UI, hash ou ACK same-user não pode ser interpretado como autoridade H1 ou permissão de rollback. Se o status for ilegível/ambíguo, **não** autorizar recuperação nem uma tentativa automática que pressuponha ausência de NEW. Não tentar reparar o estado apagando os registros.

**Referências verificadas no código existente:**
- `Phase1Recovery.cs.txt:185–210`: `prepare-only` é terminal depois de gravar diário/OLD/1. Não é uma preparação que um novo `RealOwner` possa simplesmente retomar; nenhuma retomada `prepare-only -> normal` foi validada.
- `Phase1Recovery.cs.txt:211–258`: execução normal cerca OLD/NEW com fences; após observar NEW, registra Exited e Unknown, mas **não restaura OLD**.
- `Phase1Recovery.cs.txt:278–380`: reconciliação revalida dados e faz `UnknownBlocked` antes dos ramos legados de rollback. Pode escrever um registro Unknown ou marcador fallback e saída diagnóstica; **não é uma operação estritamente read-only** em todas as trilhas.
- `L18Coordinator.cs.txt:264–270`: `BeginCore` só aceita histórico vazio e marcadores `L16Boundary.Uncertain`/`RecoveryBlocked` ausentes. Se essa guarda continuar verdadeira na execução, uma nova tentativa com histórico íntegro deve falhar antes de chamar `WriteRealSelector` e antes de criar observadores OLD/NEW.
- `Phase1Bridge.cs.txt:43–75`: gravação do seletor requer sequência/generation esperada; não deve haver reinicialização nem substituição sob HOLD.

**O que ainda não foi demonstrado em teste próprio:** um `RealOwner` novo **depois** de um `UnknownBlocked` real, com o mesmo root e novo processo, recusado antes do relançamento. Os 26 casos existentes validam reconciliação/replay, mas não exercitam diretamente esse retry cooperativo em uma raiz já usada.

## 3. Hipótese falsificável e ensaio Windows específico (NÃO EXECUTADO)

**Hipótese `P-HOLD-1` — somente estado local íntegro e dispatcher cooperativo:**
> Após `normal` observar OLD e NEW reais, concluir em Unknown e retornar `UnknownBlocked` na primeira reconciliação, um segundo `--real-owner` sobre o **mesmo root e transação** deve ser recusado ANTES de lançar um observador, alterar seletor, abrir novo ciclo de update ou modificar qualquer byte OLD/NEW.

**Procedimento para uma futura alteração experimental, se houver autorização para implementar e rodar:**

1. Preparar um único root descartável marcado sob `RUNNER_TEMP`, com os dois pacotes GF reais do runner, TUF de laboratório, pins NTFS e canário de usuário inerte.
2. Executar `RealOwner(normal)`, com observações OLD e NEW existentes, fences presentes, sem promover READY; confirmar `UnknownBlocked` em processo reconciliador independente.
3. Medir antes do retry: bytes do seletor, geração, hashes de todos os arquivos OLD/NEW, hash do canário, existência e conteúdo dos fences. Capturar também a contagem de registros de observação e histórico L18, distinguindo os diagnósticos que o próprio teste escreve.
4. Executar **um novo processo** `--real-owner` com o mesmo root/tx, reutilizando Job/gate do laboratório conforme os testes existentes. Exigir um bloqueio identificável de `BeginCore`, preferencialmente `L20_BEGIN_BLOCKED` **antes** de qualquer nova alteração de seletor/observação. A execução deve ser **não bem-sucedida como tentativa**, sem confundir isso com falha da suíte.
5. Reconciliar e repetir a consulta em novo processo; exigir `UnknownBlocked`; conferir novamente os mesmos hashes OLD/NEW e canário, seletor e fences. Distinguir alterações permitidas de logs de diagnóstico de mutações proibidas de pacote/seletor.
6. Ensaio complementar relevante: uma tentativa `prepare-only` terminada também não é retomável como `normal` pela simples reabertura do `RealOwner`, pois já deixou histórico L18. Documentar a limitação de continuidade, sem criar novo protocolo de resume nesta fase.

**Contraexemplo que refutaria `P-HOLD-1`:** o segundo owner consegue executar OLD ou NEW, sobrescreve `OLD/NEW`, move/zera o seletor, apaga artefatos de OLD, inicia um segundo ciclo ou retorna sucesso de atualização antes de observar e aceitar o bloqueio anterior. Outro contraexemplo seria a política permitir `prepare-only` como recibo para relançar OLD.

**Forma de evidência:** um novo caso separado `retry-after-unknown-retains-both`, se merecer CI, com saída estruturada informando `cooperative_retry_rejected_before_launch=true`, `old_and_new_bytes_preserved=true`, `selector_unchanged=true`, `unknown_replay_blocked=true` e explicitamente `same_user_rollback_resistant=false`, `external_launch_blocked=false`, `operational_ready=false`. Não aumentar o número de casos sem realmente executar o novo cenário e conferir o log do Windows.

**Escopo da prova:** só runner Windows descartável, processo/fixtures do laboratório; não há reboot/power loss, nem garantia de contenção de processos de fora, nem anti-rollback do estado local. O teste pode confirmar defesa contra retry cooperativo normal **sem** se transformar em demonstração de H1/H2/H3.

## 4. Caso adversarial proibido de virar garantia

Se U puder apagar o histórico/recibos/markers de decisão e reconstruir uma raiz que pareça uma tentativa nova, `BeginCore` poderá encontrar `Load.Count==0` novamente. Além disso, U pode executar NEW fora do dispatcher. Portanto **não** afirmar proteção global anti-retry ou `no-launch` contra o mesmo principal. O teste proposto deve usar estado íntegro e nomear essa condição; testar rollback integral novamente seria redundante com H1 e não tornaria o mecanismo apto a recuperação automática.

O status de HOLD não significa que NEW esteja funcional, nem que OLD tenha sido preservada em condições de recuperação fisicamente duráveis após queda de energia. Mesmo na rota normal atual a observação pode produzir efeitos fora do staging. Não inferir segurança operacional do Windows pela imutabilidade do pacote.

## 5. Critério de implementação/decisão

**Nenhuma implementação nesta rodada.** `P-HOLD-1` é nova na dimensão **reinício do owner após UNKNOWN**, diferente dos 26 replays do reconciliador; pode merecer um único ensaio negativo futuro, sem tocar `ReconcileReal` ou flexibilizar `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. Antes de autorizar essa CI, verificar se o novo teste exigirá alteração funcional ou somente assegurará uma guarda já existente; não executar a suíte inteira só para repetir fatos.

**Experiência futura do público:** se o GF algum dia tiver fluxo de atualização próprio operacional, uma transação ambígua deve suspender ações automáticas sobre a versão, manter OLD como recurso de diagnóstico, explicar o bloqueio em linguagem não técnica e exigir revisão explícita para qualquer ação destrutiva. Essa é **diretriz de design**, não UI implementada, e nenhuma decisão humana dispensa as provas técnicas exigidas para um rollback declarado seguro.

**Conclusão:** `HOLD_NO_AUTORETRY/0` é especificação de cooperação em estado íntegro, **NÃO** autoridade durable contra U. Mantêm-se Fase 1 **ABERTA**, `READY=false`, G1–G8 `BLOCKED 0/8`, zero recuperações automáticas. PR #65, main, BETA .66, TUF, B+A, NTFS/TOCTOU, infraestrutura, credenciais, telemetria e PCs pessoais intactos. Sem WORK, merge, release, deploy, serviço/helper/tarefa privilegiada, ACL/política Windows, TPM ou integração operacional.
