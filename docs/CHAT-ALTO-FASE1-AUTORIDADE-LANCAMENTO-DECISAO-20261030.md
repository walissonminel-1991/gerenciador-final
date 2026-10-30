# GERENCIADOR FINAL — Fase 1 — Parecer de viabilidade de autoridade de lançamento

**30/10/2026 | Avaliação de arquitetura, sem implementação ou nova execução Windows**

## 1. Estado verificado diretamente no GitHub antes de escrever

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch autorizada para escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial idêntico ao informado: `d4f3d361d3ae2d92770f4562a3c5ba54a1404f6f` (ahead/behind 0 contra esse mesmo commit).
- Último commit **técnico** validado: `f250be5a32a9706551051da1b0b082f57b383d15`.
- CI Windows: [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779), job `124123487779`, `completed/success`, **20/20 PASS_CONTAINMENT**, **0 recuperações automáticas**, incluindo `same-user-full-state-rollback-after-new -> UnknownBlocked` e replay também bloqueado.
- Contraprova histórica: [41071494214](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41071494214), `failure` após o caminho anterior aceitar `OldRecoveredNoReady` com NEW já observada e o estado local restaurado. Não é uma CI verde.
- Dossiês lidos: `docs/CHAT-ALTO-FASE1-ROLLBACK-FULL-STATE-FAILCLOSED-20261030.md`, `phase1-composed/Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt`, `LabProcessJob.cs.txt`, `Phase1Bridge.cs.txt`, `README.md`.
- `ReconcileReal` contém uma barreira fail-closed explícita `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` após checagens prévias do seletor/fence/journal. As rotas `OldRecoveredNoReady` e `OldReobservedNoReady` não podem ser autorizadas por essa execução normal do reconciliador.
- Fase 1 **ABERTA**, `READY=false`, G1–G8 **BLOCKED 0/8**. Os 20 testes **não** comprovam cold recovery, quiescência, perda de energia nem autoridade de produção.

## 2. Propriedades separadas: quatro perguntas que não se substituem

**P1 — pacote autêntico:** bytes, manifesto e identidade de OLD/NEW correspondem ao TUF autorizado e aos objetos NTFS? O laboratório verifica parte disso. Não informa se NEW foi executada.

**P2 — história independente:** o registro de decisão e autorização não pode ser apagado, falsificado nem restaurado para um prefixo antigo por um processo do mesmo usuário? Essa propriedade **não existe** nos arquivos atuais.

**P3 — exclusividade de lançamento:** **toda execução capaz de produzir os efeitos de NEW** passa obrigatoriamente pela autoridade que registra o início da janela de efeitos? Atualmente não. Um processo que tenha permissão de executar o EXE pode iniciá-lo fora do coordenador. Uma autoridade que só mede o lançamento *pelo próprio updater* precisa declarar explicitamente esse escopo mais estreito; não pode afirmar que NEW nunca executou em toda a máquina.

**P4 — delimitação de efeitos e recuperação:** se NEW já pode ter executado, uma prova separada garante que seus processos, brokers delegados e I/O tardio estão encerrados antes de relançar OLD? Job vazio, PID, HWND, recibo, health superficial e saída normal não dão tal garantia.

**A recuperação automática pós-falha exige garantias adequadas de P2, P3 e P4, além de P1.** Mais persistência local, por si só, melhora disponibilidade contra erro casual, não estabelece uma nova fronteira de confiança.

## 3. Contraexemplo formal e limite de impossibilidade no modelo atual

- `H0`: OLD/1 é selecionada; o processo morre antes de NEW; não há efeitos de NEW.
- `H1`: parte do mesmo estado; NEW é selecionada e executada, podendo produzir efeitos externos; em seguida um processo same-user repõe os bytes de `old/1`, prefixo válido do diário e ausência de fence.
- O reconciliador observa os **mesmos artefatos locais** em H0 e H1. Objetos NTFS do pacote, hash do manifesto e Jobs eventualmente vazios também podem coincidir.
- Se toda entrada confiável do decisor está nesse domínio restaurável, nenhuma função de decisão baseada apenas nessas entradas consegue demonstrar `no-launch` em H0 sem poder aceitar falsamente H1. O run 41071494214 reproduziu a falha em fixture; o run 41073513779 confirmou sua contenção pelo bloqueio conservador.

**Segundo obstáculo independente:** mesmo que se construa um log imutável para o usuário, ele só atestará os lançamentos que passaram por ele, a menos que exista controle de execução que impeça `CreateProcess` direto de NEW e intermediários equivalentes. Tornar o ledger confiável não torna o dispatcher exclusivo.

**Limite de ameaça:** um adversário com privilégios de administrador sobre a máquina pode alterar configurações e contornar várias barreiras locais; não declarar proteção contra administrador hostil por UAC ou ACL. Um usuário padrão isolado de um principal administrador é caso diferente, mas exige instalação/autoridade confiável fora do perfil do usuário.

## 4. Avaliação das opções nativas (sem código novo)

| Opção | Ganho limitado | Falha para os requisitos completos | Decisão |
| --- | --- | --- | --- |
| Journal append-only lógico, SHA/HMAC, testemunhas duplicadas, Alternate Data Streams, registry HKCU | Detectar parte da corrupção acidental | Snapshot completo e chave same-user retrocedem juntos; não impedem lançamento direto | **Não implementar como autoridade** |
| DPAPI `CurrentUser` ou `LocalMachine` | Confidencialidade/integridade de dados conforme escopo | `CurrentUser` é acessível ao próprio usuário; `LocalMachine` permite outros usuários da máquina descriptografarem | **Não implementar como autoridade** |
| ACL NTFS em objetos dos quais o próprio usuário é proprietário | Proteção contra erros de outros usuários/processos | Proprietário tem direito implícito `WRITE_DAC` e pode alterar DACL; não fornece anti-rollback same-user | **Não implementar como autoridade** |
| Mutex, processo auxiliar, Job Objects e handles herdados | Contenção e correlação durante a vida dos objetos | Perda de handles/supervisor e execução direta eliminam prova histórica durável; brokers externos escapam | **Manter somente contenção de laboratório** |
| Event Log / evento 4688, USN, contadores de criação de processo | Telemetria forense quando disponível | Auditoria 4688 pode estar desligada; ausência de evento não prova não execução; não representa autorização atômica, nem efeitos externos | **Não implementar como autoridade** |
| Task Scheduler com tarefa criada no mesmo usuário | Despacho por componente Windows existente sem serviço próprio | Mesmo usuário pode alterar/excluir tarefa por padrão; sem exclusividade de execução e sem ledger protegido | **Reprovado** |
| Task Scheduler ou helper temporário **com outro principal protegido** + registro de estado protegido | Hipótese de fronteira de confiança e persistência fora do mesmo usuário | Precisa implantação privilegiada/UAC ou outro principal, ACL e ownership seguros, prevenção de launch bypass, tratamento de artefatos persistentes/orfandade, reboot e falhas. Ainda não demonstra P3/P4; conta admin hostil não é protegida | **Pesquisa condicional futura; não aprovado** |
| Política de controle de aplicações do Windows (App Control/WDAC/AppLocker) | Pode restringir quais imagens são executáveis sob política administrada | Gerenciamento/configuração do dispositivo, privilégios e potencial incompatibilidade com usuário doméstico portátil; não registra, por si só, efeitos transacionais | **Fora da instalação portátil padrão** |
| TPM ou armazenamento com contador monotônico | Em tese, resistência a reversão de certas evidências sob provisionamento correto | Disponibilidade, autorização, integração e atomicidade lançamento-contador não demonstradas; TPM não impede launch externo nem cobre efeitos tardios; não presumir que CNG dá contador NV utilizável | **Somente estudo, nenhum protótipo operacional** |
| Autoridade remota via servidor/GitHub | Separação física de estado e anti-rollback de registros | Rede, disponibilidade, operação, privacidade; continua faltando despacho obrigatório do executável local e consenso no crash gap | **Não adotar como atalho** |
| Transacted NTFS (TxF) | Transações de operações no NTFS | API desaconselhada para soluções novas e sem atomicidade com `CreateProcess`/efeitos externos; não estabelece anti-adulteração | **Não usar** |

**Fontes Windows oficiais consultadas** (sem equivalência com validação do GF):
- [Owner of a New Object](https://learn.microsoft.com/en-us/windows/win32/secauthz/owner-of-a-new-object) — owner tem `WRITE_DAC`.
- [CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata) — escopos `CurrentUser` e `LOCAL_MACHINE`.
- [Security Contexts for Running Tasks](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks) — permissões de registro/alteração e UAC.
- [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) — ciclo de vida dos Jobs e handles.
- [Audit Process Creation](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-audit) — política pode estar desligada.
- [Create processes](https://learn.microsoft.com/en-us/windows/win32/procthread/creating-processes) — lançamento por chamador autorizado.
- [App Control for Business](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/) — política de execução.
- [UAC same-desktop elevation](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-security/disable-user-account-control) — UAC não equivale a barreira contra same-user administrador hostil.
- [Transactional NTFS](https://learn.microsoft.com/en-us/windows/win32/fileio/transactional-ntfs-portal) — Microsoft desencoraja novos usos.

## 5. Critérios de aceite para *qualquer* autoridade futura

**A1 — fronteira comprovada:** somente identidade não controlada pelo adversário escreve o ledger; deve sobreviver a crash/restart do updater, e um rollback de todo o espaço gravável same-user não pode reproduzir uma história autorizada.

**A2 — exclusividade de despacho:** especificar e demonstrar se a garantia cobre **somente** lançamentos pelo coordenador ou **qualquer** lançamento de NEW. Se a alegação é universal, impedir bypass de execução com enforcement do SO. Não alegar universalidade com apenas logging.

**A3 — sequência de risco:** persistir `MAY_LAUNCH` *antes* de `CreateProcess`; se o broker morrer entre persistir e criar, o estado é **ambíguo e BLOQUEADO**, nunca deduzir `no-launch` de falta de `Launched`. O ledger deve ser monotônico e vinculado a instalação/transação/manifesto e identidade de imagem.

**A4 — cortes e adversário:** comprovar antes/depois de autorização, antes/depois de `CreateProcess`, interrupção do broker, perda de supervisor, reboot, snapshot restaurado, substituição de objeto, processo externo e replay. `success` somente para o mecanismo efetivamente provado; queda física/power-loss recebe qualificação própria.

**A5 — efeito/READY separado:** autorização de lançamento não atesta saúde operacional nem quiescência de brokers e I/O; P4 continua gate independente.

**A6 — compatibilidade:** sem cobrança, sem serviço GF permanente, sem configuração de segurança invasiva, sem prompt UAC obrigatório para uso normal, sem perda da portabilidade e sem artefato privilegiado órfão. Caso a arquitetura exija abandonar algum requisito, obter **decisão expressa do mantenedor antes de implementar**, nunca relaxar silenciosamente.

**A7 — teste negativo obrigatório:** restaurar todos os artefatos sob mesmo usuário para snapshot anterior após NEW executar; o estado ambíguo deve permanecer `UnknownBlocked`.

## 6. Decisão desta rodada

**NÃO IMPLEMENTAR** candidato de autoridade: nenhuma alternativa examinada demonstrou conjuntamente P2 e P3 dentro de todos os requisitos do GF portátil. A conclusão é *não viabilidade comprovada nas condições atuais*, não impossibilidade de qualquer desenho Windows com privilégios e pressupostos diferentes.

**Manter** o código técnico em `f250be5a32a9706551051da1b0b082f57b383d15`, `UnknownBlocked`, 20 contenções da CI anterior. Não introduzir fallback automático, nem recodificar recuperações de laboratório como seguras.

**Ação futura exclusivamente condicionada a nova hipótese concreta:** preparar, apenas no runner descartável e sob autorização de escopo, um desenho comprovável de identidade distinta + despacho não contornável. Caso a hipótese não satisfaça A1–A7, encerrar a avaliação dessa hipótese com diagnóstico e manter contenção. Não gastar CI repetindo a matriz já aprovada sem modificação de segurança.

Nenhuma alteração do código do GF distribuído, `main`, PR #65 Draft, BETA .66, Cloudflare/D1, credenciais, telemetria, releases ou PCs pessoais. Sem WORK, merge, release, deploy ou integração operacional.

**Evidência desta rodada:** inspeção do código e documentação oficial Windows; **sem nova CI executada e sem teste Windows adicional**. O workflow `gf-autoatualizador-fase1-real-lab.yml` é disparado apenas por mudanças no protótipo/workflow, não por este arquivo documental. Fase 1 **ABERTA**, READY=false, G1–G8 BLOCKED 0/8.
