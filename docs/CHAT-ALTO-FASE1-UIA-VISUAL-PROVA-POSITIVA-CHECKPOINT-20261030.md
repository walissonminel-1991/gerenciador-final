# CHAT ALTO — GF Fase 1 / Opção B — checkpoint de investigação UIA e visual

Data: 30/10/2026. Natureza: incremento experimental **de diagnóstico de prova positiva**, não ativação do autoatualizador nem READY.

## 1. Base e integridade

- Branch de escrita única: `experimento-g5-ipc-chat-alto-20261029`.
- Base contratual: `docs/CHAT-ALTO-FASE1-PROVA-POSITIVA-EXTERNA-PLANO-20261030.md`, HEAD de entrada `63fb95a4ac308cfd8a25516f05a8ff63263afbba`.
- Último SHA técnico **integrado e validado anteriormente** (A1–A5): `e0b8bc414d6756f542b7b3af6e82a820b34aa70b`; [CI 41143130572](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41143130572) SUCCESS, 27 históricos + 2 novas contenções.
- Novo incremento UIA/MSAA: `64fd93d5b49024f42f783906a9fcfd33aebf8110`.
- Novo incremento visual exploratório: `2917297a83e95588383993ca74f26b8e9f9e00c3`.
- Diagnóstico visual adicional: `379b815546ee86c374dc2eab1c9e0db33391e99d`.
- Os arquivos adicionados são **exclusivamente experimentais**. Nenhum código, layout ou acessibilidade do aplicativo GF foi alterado. Nenhuma mudança de seletor, reconciliador, TUF, NTFS, fences ou transações.
- A revisão de logs abaixo refere-se às CIs **dirigidas**; não confundir com homologação transacional completa.

## 2. Ensaio externo UI Automation/MSAA

[CI 41145899724](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41145899724), job `124370812363`: **SUCCESS**. É SUCCESS de estudo de viabilidade, **não** de prova de item.

Observado no Windows descartável:
- GF REAL executado com confirmação de imagem/processo.
- Texto das pesquisas positiva e negativa efetivamente ecoado no controle EDIT.
- UI Automation respondeu, raiz vinculada ao processo certo; **0 elementos com nome exato esperado** na pesquisa positiva e **0** na negativa.
- MSAA acessível na raiz, com `msaa_root_children_present=false`.
- `uia_list_ancestor_qualified=false`, `positive_ui_proof=false`.
- Nenhum texto de aplicativo não relacionado, nonce, HWND, PID, screenshot ou conteúdo acessível bruto publicado no log.
- Este resultado **não demonstra impossibilidade universal** de UIA/MSAA: mede o caminho efetivamente consultado na sessão experimental, com `--tray` e a árvore de acessibilidade exposta então.

## 3. Ensaio visual restrito e contraprovas de proveniência

Implementação isolada fora de `phase1-composed/**`, sob `phase1-visual-probe/`, com workflow dedicado, para evitar nova regressão histórica a cada ajuste da investigação.

[CI 41146267080](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41146267080), job `124371983239`: **SUCCESS de viabilidade**, sem pixels lidos.

[CI 41146474665](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41146474665), job `124372643011`: **SUCCESS de diagnóstico**, com:
- `real_gf_verified=true`.
- `verified_window_visible=true`, `verified_window_foreground=true`.
- `structural_list_region_found=false`.
- Razão: `LIST_STRUCTURE_NOT_QUALIFIED`.
- `pixel_samples_complete=false`, pesquisas de imagem não testadas; `item_name_visually_proven=false`.

O observador **recusou capturar** qualquer região cuja identidade de lista não tenha sido demonstrada. Não houve captura de tela inteira, imagem gravada em disco, hashes exportados ou evidência visual positiva. Nenhum resultado de mudança de pixels deve ser alegado.

## 4. Regresso histórico

A introdução do arquivo `phase1-composed/uia_probe_real_gf.py` acionou automaticamente a CI transacional [41145899676](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41145899676) no SHA `64fd93d5b49024f42f783906a9fcfd33aebf8110`. Na última conferência, **estava in_progress**, na etapa de 29 casos; não declarar sucesso sem conclusão e logs.

As alterações visuais posteriores ficaram em pasta fora do gatilho do workflow histórico; nenhuma segunda suíte transacional foi solicitada para esse ajuste. O último SUCCESS transacional confirmado continua `41143130572` no SHA `e0b8bc414d6756f542b7b3af6e82a820b34aa70b`.

## 5. Impedimento atual e próximo incremento mínimo

O GF usa `DarkProgramList : Control`, com itens desenhados por `OnPaint` e `TextRenderer.DrawText`, sem HWND de linha acessível já provado. UIA/MSAA do teste não revelou o nome esperado. A tentativa de descobrir a região da lista via etiqueta de estado vazio e relação de irmãos falhou, mesmo com janela correta visível e em primeiro plano.

**Próxima hipótese a refutar:** localizar estruturalmente, de forma independente e sem produto modificado, o HWND de `DarkProgramList` através da hierarquia de janela, relações com o painel de pesquisa e geometria do controle/scrollbar. Registrar apenas contagens estruturais limitadas e booleanos; não enumerar dados pessoais. Rejeitar se não for único. Só depois, e em sessão realmente visível, testar amostras em memória da área restrita com negativos *seed ausente*, nome incorreto, campo EDIT apenas, estado stale/oculto e oclusão. Um simples delta/hash de pixels **não** é prova do nome exibido; exigir reconhecimento específico verificável e contraexemplos antes de qualquer `PASS_EXPERIMENTAL_ACTIVATION`.

Se o método externo não puder prover identidade específica sem modificar o produto, manter `POSITIVE_UI_PROOF_PENDING` e apresentar o limite ao mantenedor, sem contornar os testes. Somente após prova positiva específica, regressão completa e SHA congelado deverá haver nova auditoria WORK.

## 6. Invariantes

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas. HOLD é contenção.** Não alterar main, PR #65 Draft, BETA .66, produto, backend, política Windows, chaves de confiança, produção, release, deploy ou merge. Nada aqui prova H1/H2/H3, quiescência externa, bytes carregados em memória, recuperação segura após NEW ou autorização operacional.
