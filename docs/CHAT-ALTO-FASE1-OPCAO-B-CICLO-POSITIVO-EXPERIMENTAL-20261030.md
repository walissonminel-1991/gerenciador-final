# GERENCIADOR FINAL — Fase 1 — contrato B e ciclo positivo experimental, NÃO operacional

**30/10/2026.** Contrato revisto **por autorização expressa do mantenedor**: a Fase 1 pode adotar B+A portátil, **sem recuperar automaticamente OLD quando história ou segurança de recuperação forem inconclusivas**, inclusive após crash/reboot. Preservar OLD/NEW e evidências; HOLD é contenção, não recuperação. Não muda autenticidade, controle de bytes executados, proteção de dados, TUF, NTFS, TOCTOU ou confiança/READY operacional.

## 1. Evidência e estado

- Branch exclusiva `experimento-g5-ipc-chat-alto-20261029`, HEAD técnico validado `fd88cd30a2dee25576fc76df6c6c6d06636e0acc` (primeiro commit `e7347a0` corrigido apenas no encaminhamento de parâmetro do adaptador Python) (somente cinco arquivos no laboratório, sem alterações no produto).
- CI L18 prévia [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790), job 124252340866, **27/27 PASS_CONTAINMENT**, zero recuperações. Os 27 foram mantidos.
- Primeira CI [41133540540](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41133540540): **FAILURE** (job `124330251580`); a compilação passou, mas o primeiro caso falhou porque o adaptador Python `observed_process` não aceitava o novo argumento `expected_inventory_name`. Commit de correção `fd88cd30` alterou somente a assinatura e o encaminhamento do argumento, sem modificar coordenador, guard ou asserções. **CI final [41133853256](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41133853256), job `124331302448`, `completed/SUCCESS`, 29 casos individuais, 29 nomes únicos, 28 `PASS_CONTAINMENT`, 1 `PASS_EXPERIMENTAL_ACTIVATION`, zero falhas e zero recuperações automáticas.**
- Fase 1 **ABERTA**, READY=false, G1–G8 BLOCKED 0/8, sem produto distribuído.

## 2. Critérios verificáveis revisados (sem antecipar Fases 2–4)

| Fase 1 sob opção B: critério | Prova exigida | Estado |
| --- | --- | --- |
| Pacote e identidade | TUF de laboratório, bytes/objetos OLD/NEW, seletor único C# L18, pins NTFS, proteção TOCTOU e vínculo ao processo realmente criado | Testes de arquivo/pin/seleção existentes; **medida de bytes carregados/TOCTOU geral ainda pendente** |
| Ciclo positivo restrito | NEW selecionada por L18; GF OLD/NEW reais executados; verificação de inventário relevante e observação externa; somente o owner original vivo registra evidência experimental; nunca operar READY por booleano isolado | **Prova positiva limitada validada na CI 41133853256; NÃO READY operacional** |
| Rejeição funcional | Sem item de Registro de teste, o mesmo caminho não pode gravar evento `new-observed` ou declarar resultado positivo | **Caso de rejeição validado na CI 41133853256** |
| Falha/ambiguidade | ReconcileReal mantém `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`, fences e NEW/2 histórico sem relançar OLD; `UnknownBlocked` e retry cooperativo bloqueado | 27 contenções anteriores; replay positivo/negativo novo a confirmar |
| Crash/reboot | A opção B dispensa **apenas** recuperação automática quando não houver história/segurança demonstradas; retenção de OLD/NEW, proibição de limpeza/rollback/retry automático e diagnóstico são obrigatórios. Não confundir crash sintético com reboot/power-loss | Histórico testado parcialmente; **reboot físico, persistência e experiência do usuário pendentes de fase/ensaio aplicável** |
| READY operacional/G1–G8 | Qualificação de segurança Fase 2, integração Fase 3 e homologação Fase 4 | **NÃO autorizado**, `READY=false`, G1–G8 0/8 |

A promessa retirada é apenas **rollback automático inseguro** quando falta prova; não se retira obrigação de demonstrar um ciclo funcional nem se trata HOLD como sucesso.

## 3. Implementação candidata — transação no mesmo L18

- C# `RealOwner("positive-trial")` mantém a mesma entrada e o mesmo seletor `old/1 -> new/2`, a mesma estrutura B+A, manifesto TUF/3, NTFS pins, journal e fences. OLD é executada antes da seleção; NEW é executada depois do NEW fence.
- **Saúde funcional além do nonce de UI:** o observador independente Python cria **uma chave inerte e temporária somente em HKCU**, dentro de `Software\Microsoft\Windows\CurrentVersion\Uninstall\GF-PHASE1-INVENTORY-<aleatório>`; grava apenas `DisplayName`, `DisplayVersion` e `Publisher`, **sem `UninstallString`**, sem instalador, sem manutenção ou alteração de política/ACL. O GF real precisa descobrir a entrada em `ScanInstalledPrograms`, incorporá-la em `LoadInstalledAsync` e fazer o filtro WinForms responder à pesquisa positiva e a uma pesquisa contraditória negativa. O nome aleatório não vai para o relatório; somente booleanos. `try/finally` remove a chave do laboratório após a observação; não apagar chaves preexistentes.
- C# examina o relatório da observação, além de revalidar manifesto/pins, selector NEW/2 e Job Object. **Somente o owner original, ainda vivo**, pode registrar as fases L18 já existentes `Exited -> Reconciling -> New`, detalhe `real-new-experimental-trial-accepted-not-ready`. Não existe segundo seletor, novo coordenador, receipt READY ou promoção operacional.
- `positive-trial-negative` observa a NEW com a identidade de inventário deliberadamente **ausente**. A ausência deve causar erro no observador e impedir o journal `New`; permanece NEW/2 com fence e histórico inconclusivo, sem executar OLD.
- Tanto após o caso positivo quanto negativo, **um novo ReconcileReal precisa retornar `UnknownBlocked` duas vezes**, com `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, e não autorizar recuperação/cancelamento automático. Um estado `New` experimental **não é testemunha antirollback** quando restaurável por U.

### Causa nova e contraprova

- **Hipótese:** em um GF real e no runner descartável, a entrada HKCU de inventário criada pelo observador chega à interface por uma função efetiva de inventário, e o owner C# distingue a ausência dessa entrada. Resultado positivo não é autorizado se a prova estiver ausente.
- **Contraexemplo buscado:** NEW não leu a chave do Registro, foi filtrada erroneamente, a pesquisa não reagiu, o observador inventou um sucesso ou o journal `New` foi persistido no caminho negativo; qualquer um refuta o contrato e bloqueia a CI.
- **Decisão se passar:** reconhecer exclusivamente `PASS_EXPERIMENTAL_ACTIVATION` para NEW no owner vivo, distinto de `PASS_CONTAINMENT`; ainda é proibido afirmar READY operacional, quiescência de brokers ou cold recovery.
- **Decisão se falhar:** candidata bloqueada, não reduzir asserções; identificar defeito exato antes de corrigir.

## 4. Comportamento e limites de segurança

| Situação | Seletor / OLD / NEW | Decisão |
| --- | --- | --- |
| Preparação normal, antes de execução | `old/1`, pacotes em dois diretórios | `prepare-only` permanece terminal, não retoma automaticamente |
| Owner vivo, autenticação e prova experimental positivas | `new/2`, OLD mantida, NEW selecionada, dois fences mantidos; jornal `New` como observação limitada | `PASS_EXPERIMENTAL_ACTIVATION` no relatório do runner **apenas**, não READY nem recuperação pós-crash |
| Health negativo ou interrupção durante NEW | `new/2` e fences preservados quando já cruzados, OLD intacta | Erro/UNKNOWN; nenhuma transição positiva ou rollback automático |
| Nova instância do owner/reconciliador ou reboot com história ambígua | Manter evidências, `UnknownBlocked`, não apagar NEW/OLD | HOLD para diagnóstico; **não chamar recuperação** |
| Novo lançamento externo por mesmo usuário ou broker tardio | Fora do domínio do L18 | H2/H3 NÃO provadas; nenhuma autoridade para retorno automático OLD |

**Adversário e lacuna:** outro processo same-user pode restaurar journal, seletor e fences e executar NEW fora de L18. O observer Python e a chave de teste HKCU também executam no mesmo principal, portanto seus registros **não são evidência criptográfica independente contra U**. O ensaio só testemunha função real na sessão corrente do laboratório. Não há medida kernel independente dos bytes **carregados em memória**. As verificações de arquivo em disco e pins são necessárias, mas não suficientes para READY operacional. H1/H2/H3 continuam abertas. Ninguém pode usar a presença de `New` no diário como permissão de rollback após crash.

## 5. Diagnóstico acessível ao usuário (design, NÃO UI implementada)

Mensagem proposta para uma futura experiência: **“A atualização do Gerenciador Final não pôde ser confirmada com segurança. Mantivemos os arquivos das duas versões. Para proteger seus dados, não iniciaremos outra tentativa nem restauraremos automaticamente a versão anterior. Consulte o diagnóstico e obtenha assistência antes de realizar mudanças.”**

Não prometer que restauração manual sempre será segura. Não oferecer botões de rollback, limpeza ou `tentar novamente` nesse estado sem protocolo seguro. Não requerer login, custo ou conhecimento de termos como TUF, TOCTOU, fence ou Job.

## 6. Próxima qualificação independente

**WORK somente após CI nova validada, sobre SHA técnico congelado** e questão adversarial: a prova positiva realmente atravessa HKCU→inventário→filtro, está protegida contra falsas classificações no caminho negativo, e o evento `New` experimental pode ser mal-interpretado como autorização externa de recuperação? Verificar contratos de argumento, cleanup HKCU, evento `New` vs `UnknownBlocked`, identidade da imagem e efeitos fora do Job. Revisão não modifica código e não é qualificação externa formal.

**Sem** merge, release, deploy, integração produto, permissões elevadas, serviço/helper, ACL/política Windows, TPM, backend, telemetria ou PCs pessoais.

## 7. Validação final e limites preservados — CI 41133853256

**SHA técnico congelado para revisão adversarial:** `fd88cd30a2dee25576fc76df6c6c6d06636e0acc`. Run [41133853256](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41133853256), job `124331302448`, tentativa 1, `completed/success`.

**Relatório Windows inspecionado:** exatamente **29 registros e 29 nomes de caso exclusivos**, `expected_cases=29`, `failed=0`; 28 `PASS_CONTAINMENT` e 1 `PASS_EXPERIMENTAL_ACTIVATION`. O workflow manteve os 27 casos de contenção anteriores e exigiu evidências positivas e negativas individualizadas; nenhuma asserção foi desabilitada. Zero mensagens `##[error]`.

- `positive-trial-inventory`: `original_live_owner_positive_recorded=true`, `registry_to_inventory_to_ui_probed=true`, `seeded_registry_inventory_proved=true`, `l18_new_generation_two_selected=true`, `real_old_and_new_functionally_observed=true`. Também `old_and_new_bytes_preserved=true`, `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`, `outcome=UnknownBlocked` e `replay=UnknownBlocked`.
- `positive-trial-inventory-absent`: `missing_inventory_rejected=true`, `seeded_registry_inventory_proved=false`, `original_live_owner_positive_recorded=false`, com NEW/2 histórica, OLD/NEW preservados e reconciliação/replay `UnknownBlocked`.
- Ambos: `block_reason=REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, `external_launch_blocked=false`, `independent_recovery_authority=false`, `h1_proven=false`, `h2_proven=false`, `h3_global_proven=false`, `after_crash_success_proven=false`, `automatic_recovery_authorized=false`, `operational_ready=false`.

**Estado formal do relatório:** `phase1_complete=false`, `operational_ready=false`, `operational_gates_closed=0`, `real_power_loss_tested=false`, `supervisor_loss_recovery_proven=false`, `new_effects_quiescence_proven=false` e `production_tuf_authority=false`.

**Pendências materiais:** o experimento positivo verifica UMA função relevante (HKCU ARP→inventário→pesquisa), **não** a completude de todos os motores. O observador usa caminho de imagem e SHA de disco, **não** atestação independente de bytes efetivamente carregados na memória. Históricos locais, nonce do teste e registros Python não são autoridade H1; lançamento externo H2 e efeitos delegados H3 continuam sem contenção independente. A experiência de orientação ao usuário ainda é desenho, sem UI operacional. Não habilitar retorno a OLD, promoção ou release em nenhuma condição.

**Encaminhamento único:** congelar `fd88cd30` para auditoria WORK em **somente leitura**, questionando se uma entrada HKCU temporária pode gerar falso positivo de inventário, se o `New` experimental poderia ser reapresentado como autorização indevida, se houve limpeza segura e se os guards de crash/replay e pins continuam suficientes para o escopo anunciado. Só depois de avaliar contraprovas propor o próximo incremento. Não gastar WORK repetindo H1/H2/H3 genericamente.
