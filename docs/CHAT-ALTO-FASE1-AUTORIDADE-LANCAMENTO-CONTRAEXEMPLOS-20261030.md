# GERENCIADOR FINAL — Fase 1 — autoridade independente de lançamento

**Checkpoint de investigação — 2026-10-30 (America/Fortaleza); somente branch experimental; sem implementação operacional**

## 1. Base conferida, limites e evidências

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch única de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental conferido antes desta análise: `c3c7051ee474553004c0da7d99fd5e9f5c2969af`.
- Último commit **técnico** aprovado: `5fd3a583caf4f62c58d382e5fc31e314b44cdc1d`.
- Windows CI: [run 41064412191](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41064412191), job `124091955138`, `completed/success`, **19/19** (3 `PASS_RECOVERY_LAB_ONLY` e 16 `PASS_CONTAINMENT`). A execução anterior `41063285183` falhou por assert antigo de `selector-locked`; a correção conservou `RecoveryBlocked`.
- Esta rodada foi **análise estática** dos arquivos `Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt`, `Phase1Bridge.cs.txt`, `LabProcessJob.cs.txt`, README e workflow, com consulta às APIs Windows documentadas. **Nenhum teste Windows adicional foi executado**, e nenhuma conclusão adicional deve ser atribuída à CI antiga.
- Fase 1 **ABERTA**; `READY=false`; G1–G8 `BLOCKED 0/8`. Sem efeito sobre `main`, PR #65 Draft, BETA .66, Cloudflare/D1, telemetria, credenciais, releases ou máquinas pessoais.

## 2. Pergunta de segurança e modelo de ameaça

Precisamos distinguir, após perda do coordenador, (A) um genuíno **no-launch** de NEW e (B) NEW já executada, possivelmente com efeitos externos, seguida de adulteração de registros por outro processo do **mesmo usuário**. A prova deve sobreviver à perda do supervisor sem inferir ausência de execução a partir de PID/HWND, Job vazio, recibo/READY local, hash isolado ou `File.Exists`.

A leitura de `ReconcileReal` confirma defesa para NEW selecionada: falta do fence não permite rollback; `REAL_NEW_SELECTED_NO_INDEPENDENT_NO_LAUNCH_PROOF`. Se o journal ainda contém `real-new-effects-fenced-before-create`, responde `REAL_LAUNCHED_NEW_JOURNAL_FENCE_MISSING`. Diretório com nome de fence bloqueia com `REAL_NEW_FENCE_NOT_REGULAR_FILE`. Esses testes **não** criam uma autoridade positiva/negativa independente.

O corte `after-intent` deixa seletor `old` geração 1 e journal anterior ao launch. As três recuperações `PASS_RECOVERY_LAB_ONLY` são válidas **somente sob integridade presumida dos arquivos de controle e testemunha de Job retida no laboratório**. Não são garantias anti-adulteração mesmo quando um segundo coordenador inicia OLD real.

## 3. Contraexemplo de indistinguibilidade: rollback conjunto do estado local

Há duas histórias que um reconciliador limitado às evidências atuais não distingue. Este é um **contraexemplo analítico, ainda não reproduzido no Windows**:

| Etapa | História A — sem NEW | História B — NEW já executada |
| --- | --- | --- |
| Estado anterior válido | Após `after-intent`: seletor `old/1`, prefixo íntegro do diário, nenhum fence NEW | Mesmo estado anterior válido pode ter sido copiado antes da seleção |
| Execução | Coordenador morre antes de selecionar NEW | Coordenador seleciona `new/2`, persiste fence, registra launch, executa NEW e termina |
| Adulteração | Nenhuma | Processo same-user restaura **os bytes anteriores válidos** do seletor e do prefixo do diário, remove suffix `Launched/Exited` e fence; outros arquivos controlados pelo usuário podem voltar ao snapshot |
| Observação na reconciliação | `old/1`, diário aparentemente pré-lançamento, fence ausente, Job sem membros, pacote íntegro | **Os mesmos valores**; os efeitos de NEW no sistema não precisam ser reversíveis nem observáveis pelo diário |
| Risco | Reobservar OLD pode ser válido no laboratório | Retornar `OldRecoveredNoReady` como se não houvesse NEW seria um **falso no-launch** |

A adulteração anterior não é o caso já testado `joint-fence-journal-erasure`: naquele cenário o seletor **permanece em NEW**, permitindo o bloqueio atual. Um atacante que possui os arquivos pode também restaurar a cópia anterior, válida, do seletor para OLD e remover o suffix. `RequireRealSelector` valida campos e formato, mas não constitui anti-rollback externo. Revalidação TUF e pinagem NTFS dos **bytes da aplicação** comprovam integridade dos pacotes, não a história de execução.

Os objetos de Job retidos pelo supervisor conferem contenção/liveness durante seu tempo de vida, mas não fornecem atestado durável do histórico após destruição. E um Job vazio após NEW terminar não prova ausência prévia de execução ou inexistência de broker externo.

**Consequência formal:** se o adversário consegue restaurar todos os observáveis locais para um snapshot anterior e não existe informação confiável fora desse domínio, qualquer decisão baseada somente nesse estado retornará a mesma resposta para A e B. Nem cadeia SHA/HMAC com chave same-user, nem append-only lógico em pasta same-user, nem múltiplas cópias resolvem retrocesso do snapshot. Uma autoridade verdadeira requer *fronteira de confiança distinta* e ordenação durável de autorização/lançamento; ausência de registro só prova no-launch sob controle exclusivo de lançamento e garantia explícita de retenção.

**Nota de evidência:** a possibilidade de falso `OldRecoveredNoReady` decorre da análise de fluxo em `ReconcileReal` e de acesso mutável aos arquivos; **não foi declarada exploração reproduzida**. Realizar somente em runner Windows descartável, com fixture inerte, antes de decidir mudanças de código.

## 4. Candidatos examinados (nenhum aprovado nesta rodada)

| Candidato | Avaliação de segurança e compatibilidade |
| --- | --- |
| Mais fences, journal hash-chain, HMAC/assinatura com chave no perfil, ADS, cópias locais | **Reprovado**: mesmo sujeito pode reverter registros e chave ou reapresentar prefixo válido; não há monotonicidade externa. |
| Segundo processo, mutex, pipe, Job Object ou handle herdado same-user | **Reprovado como autoridade independente e durável**: útil para contenção enquanto handles confiáveis existem; perda do supervisor elimina a prova exigida e processos já terminados não são negados por Job vazio. |
| ACL NTFS administrada pelo próprio usuário e DPAPI CurrentUser | **Reprovado como fronteira contra o mesmo usuário**: owner pode alterar DACL; DPAPI CurrentUser é acessível ao mesmo principal. Continua útil para proteção acidental, nunca para anti-rollback hostil. |
| Auditoria Windows de criação de processo (4688), logs/Event Log/USN | **Não suficiente**: auditoria pode estar desligada; leitura/retention/cobertura e correlação devem ser demonstradas; **ausência de evento não prova ausência de processo**, nem cobre brokers ou effects. |
| TPM/segredo selado ou contador resistente a rollback | **Pesquisa futura, não solução pronta**: precisa de disponibilidade, provisionamento, autorização, anti-rollback e vínculo obrigatório ao lançamento; não assumir suporte nem transformar medição de disco em execução observada. |
| Task Scheduler / helper elevado temporário, com ledger protegido por outra identidade | **Único desenho local condicional digno de protótipo**, não aprovado: depende de privilégio/UAC, controle exclusivo do launch, ACL protegida, recuperação sob queda, identidade da imagem executada, modo portátil, remoção segura de tarefa e comportamento se o broker desaparecer. Tarefa criada no **mesmo contexto** do usuário não satisfaz o requisito; nenhuma tarefa foi criada. O serviço Windows já existente não significa autorização para instalar um serviço próprio. |
| Autoridade remota/servidor | **Não escolhido**: conexão não substitui atestação local e adicionaria rede, privacidade, operação e dependências à aplicação gratuita. |

Referências de semântica (não evidência de teste do GF):
- Microsoft, [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) — destruição por lifetime de handles/processos, limites de Job.
- Microsoft, [Take ownership of files or other objects](https://learn.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/take-ownership-of-files-or-other-objects) — owner altera permissões.
- Microsoft, [CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata) — âmbito same-user e LocalMachine.
- Microsoft, [Audit Process Creation policy](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-audit) — evento condicionado à política habilitada.
- Microsoft, [Security Contexts for Running Tasks](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks) — permissões e principal de tarefas.

## 5. Decisão e critério para qualquer implementação subsequente

1. **Não alterar o coordenador C# L18 nem a matriz de 19 testes nesta rodada**: nenhuma autoridade candidata demonstrou as garantias solicitadas. Alterar `selected == "new"` para permitir rollback seria regressão grave.
2. Preservar `UnknownBlocked` para NEW selecionada e todos os cenários ambíguos; não promover `PASS_CONTAINMENT` a recuperação fria ou ausência de launch. Os 3 resultados `PASS_RECOVERY_LAB_ONLY` permanecem **somente como ensaio benigno em fixture com supervisor/testemunha retidos**, não como segurança same-user.
3. Antes de alegar qualquer nova autoridade: demonstrar a **mesma decisão protegida** em cortes (antes/depois da seleção, antes/depois da autorização durável, antes/depois de CreateProcess, broker/owner killed, replay, perda de supervisor, rollbacks de todos os arquivos same-user), e provar que resultado incerto é bloqueio. Distinguir processo autorizado de execução manual possível, efeitos externos, READY e quiescência. Power-loss real e persistência física requerem campanha separada; simular kill de processo não os prova.
4. Próximo experimento de alto valor, **não executado nesta rodada**: variante `same-user-full-state-rollback-after-new` no runner descartável. Capturar estado `old/1` e histórico pré-select, executar NEW real, restaurar os control files para snapshots antigos, e observar se o caminho `OldRecoveredNoReady` seria alcançado. Registrar como **contraexemplo/bloqueio**, nunca como `PASS_RECOVERY`. Se confirmado, decidir se falha-fechada deve também desabilitar as três recuperações benignas no modelo hostil, preservando-as apenas como diagnóstico separado.
5. Um helper temporário privilegiado só merece implementação após prova de isolamento de identidade contra o mesmo usuário, gate único de execução, anti-rollback real e vida útil/recuperação definidos. Não implementar forma incompleta para ganhar uma classificação verde.

**Status final:** investigação técnica sem mudança funcional; nenhuma alegação de novos resultados Windows. `PHASE1_COMPLETE=false`, `READY=false`, `G1–G8 BLOCKED 0/8`. Sem WORK, merge, release, deploy ou integração operacional.
