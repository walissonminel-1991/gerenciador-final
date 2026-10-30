# GERENCIADOR FINAL — AUTOATUALIZADOR SEGURO — FASE 1
## Checkpoint de encerramento gracioso e observação funcional de interface do GF real

**Data:** 2026-10-29  
**Repositório:** `walissonminel-1991/gerenciador-final`  
**Branch:** `experimento-g5-ipc-chat-alto-20261029`  
**Baseline confirmada:** `2fc71f47ccf3c7de3d32dfe2224efda954f184c7` (HEAD idêntico antes de alterar).

**STATUS FORMAL: FASE 1 AINDA ABERTA — laboratório real avançou, mas sem READY operacional nem conexão transacional completa L18–L22 ↔ pacote GF real. G1–G8 BLOCKED (0/8).**

## 1. Proteções de escopo confirmadas
- Main `410d65e23546976db456337c71e4705f10217d36` inalterada.
- PR #65 Draft aberta, sem merge, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- Sem WORK, merge, release, deploy, modificação do GF operacional (`Program.cs`, `MainForm`, `GerenciadorAplicativos.csproj`), BETA .66, Cloudflare, backend, credenciais, telemetria, PCs pessoais ou execução fora do Windows descartável.
- Arquivos alterados/adicionados somente em `prototipos/autoatualizador-seguro/phase1-composed/` e no workflow CI experimental, além deste checkpoint.
- Estratégia B+A com coordenador temporário sem elevação permanece **referência arquitetural**, não integração instalada. Nenhum risco residual aceito.

## 2. Commits técnicos desta continuação
1. `da098ebddd0a48ff1d784db4cf7ced540e9b9d2a`: adicionar observador Win32 externo ao produto, ligando janela real a PID e imagem de processo, `SendMessageTimeout(WM_NULL)`, `PostMessage(WM_CLOSE)`, saída limitada e fallback conservador. Validação de caminho do processo via `QueryFullProcessImageNameW` e comparação SHA256 de imagem em disco ao manifesto autenticado.
2. `d21c9f821306631870dfeb9d66d8c615a664d785`: respeitar a inicialização e `MainForm.Shown`/inventário assíncrono, aguardar até 10 s antes de fechar, permitir no máximo duas solicitações WM_CLOSE temporizadas. Nenhuma lógica de produto alterada.
3. `bdf4f2be0906cdf1695636f33080b3ecb201e76d`: inspeção **somente leitura** de controles filhos HWND do PID GF. Apenas flags booleanas: cabeçalho `GERENCIADOR FINAL`, rótulo `Pesquisar:`, resumo `Apps: [quantidade]` e botão habilitado `Atualizar e verificar`. Não coleta nomes de aplicativos ou outros textos, não clica, não instala e não atualiza.

## 3. Histórico CI Windows e resultados
### CI inicial da continuação — 40976025107
- https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40976025107 — `completed/success`.
- Windows runner: sessão **2**, acesso a input desktop e janela do GF confirmados. OLD/NEW com PID, caminho da imagem e hash em disco correspondentes ao manifesto, HWND de MainForm pertencente ao PID e `WM_NULL` responsivo.
- OLD: `WM_CLOSE` recebido, saída normal código 0, sem kill.
- NEW: recebeu `WM_CLOSE`, não encerrou no prazo inicial de 12 s; limpeza forçada exclusivamente de processo teste. Seleção OLD e canário preservados. Não confundir job aprovado de inspeção com fechamento comprovado em NEW.

### CI da correção de encerramento — 40976451640
- https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40976451640 — `completed/success`.
- OLD e NEW: **imagem e hash de disco vinculados ao PID**, título/owner de janela, thread WinForms responsiva, **uma chamada WM_CLOSE e saída normal 0 para ambos**.
- NEW e OLD encerraram sem necessidade de `taskkill`; todos os hashes OLD e canário verificados, retorno do seletor para OLD.
- O novo prazo inicial resolve a corrida observada em CI entre início do inventário e `FormClosing`. Não demonstra encerramento de todos os processos auxiliares ou conclusão de I/O kernel.

### CI da fatia de interface observável — 40976940204
- https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40976940204 — `completed/success` no commit técnico `bdf4f2be0906cdf1695636f33080b3ecb201e76d`.
- **OLD e NEW:** runner sessão Windows 2, imagem/PID e manifesto coerentes, janela real respondendo, **39 controles filhos do próprio PID**, `header=true`, `search_label=true`, `apps_summary_rendered=true`, `update_control_present=true`, `update_control_enabled=true`, `functional_inventory_ui_observed=true`.
- **OLD e NEW:** `graceful_shutdown_verified_for_owned_process=true`, 1 WM_CLOSE por processo, saída normal código 0, `forced_cleanup_required=false`.
- TUF experimental autenticou manifesto dos 284 arquivos de cada build; DLL adulterada rejeitada; nenhuma operação de manutenção executada; seletor regressou a OLD, OLD e canário intactos.
- Relatório final **deliberadamente** mantém `phase1_complete=false`, `ready_verified=false`, `automatic_update_authorized=false`, `operational_gates_closed=0`, `status=blocked_until_real_ready_and_independent_review`. Job verde significa sucesso da investigação e dos critérios parciais medidos — não READY operacional.

## 4. Delimitação crítica da evidência
- `WM_NULL`/HWND por PID estabelece fila de mensagens responsiva, não todos os motores funcionando.
- O conjunto de 4 tipos de controles observados demonstra uma **fatia real de interface e resumo de inventário**, mas não execução bem-sucedida de busca, desinstalação, atualização, drivers, nem saúde operacional completa.
- `QueryFullProcessImageNameW` e SHA256 validam o caminho e os bytes atualmente em disco, mas **não atestam os bytes já carregados na memória do processo**, nem eliminam a janela TOCTOU entre hash e `CreateProcess`.
- A sessão Windows usada nesta rodada **não estava indisponível**: sessão ID 2 com desktop acessível e MainForm observado. Portanto, é incorreto atribuir o bloqueio restante à inexistência de desktop.
- O experimento de CI é um **orquestrador temporário em Python** sobre pacotes reais, com assinatura TUF de laboratório; **não integra nem exercita como transação única o código de coordenação C# L18–L22 com a seleção/execução real do GF**. Esse vínculo é o principal bloqueio de composição ainda existente para fechar formalmente a Fase 1.
- A restauração do seletor/OLD é conservadora e comprovada em **sandbox descartável**, não recuperação de instalação GF dos usuários, nem durabilidade após desligamento elétrico.

## 5. Estado da missão única
- **Fase 1 — ABERTA**, mas entregou as duas correções experimentais solicitadas: saída graciosa de OLD e NEW + observação de função de interface além de PID. Falta demonstrar um **contrato de READY operacional confiável** e vincular essa observação/recuperação ao coordenador experimental numa transação composta única, ainda em Windows descartável. Evitar declarar CLOSED somente porque CI passou.
- **Fase 2 — auditoria WORK e qualificação G1–G8:** não iniciada.
- **Fase 3 — integração operacional ao GF:** não iniciada.
- **Fase 4 — homologação:** não iniciada.
- **Progresso formal da missão: 0/4 fases encerradas; avanço técnico significativo dentro da Fase 1.**

### Próxima ação ÚNICA na mesma Fase 1
Usar o pacote real e a observação Win32 agora provados **com o coordenador C# já desenvolvido L18–L22**, em um só ensaio transacional Windows descartável, com vínculo de bytes, identidade, close/health e retorno a OLD. Não criar outro motor paralelo, nem aceitar a presença de controles/recibo local como autorização final. Se para isso for necessário um contrato mínimo de saúde in-process, ele deve ser estritamente experimental e estar comprovadamente ausente da distribuição normal, com revisão dos limites de confiança. **Não iniciar WORK até existir esse candidato composto em CI.**
