# CHAT ALTO — Gerenciador Final — Fase 1 / Opção B
## Próximo incremento experimental: testemunho externo positivo do item na interface

**Data:** 30/10/2026. **Natureza:** decisão técnica e contrato de testes. **Este documento não é implementação, prova positiva nem CI nova.**

### 1. Estado de entrada verificado

- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- SHA técnico congelado A1–A5: `e0b8bc414d6756f542b7b3af6e82a820b34aa70b`.
- CI Windows de referência: [41143130572](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41143130572), job `124361884219`, tentativa 1, `completed/success`.
- HEAD documental observado: `63a3cb2aa90cb9f01e486f5d6a4fc55448c8d390`, um commit documental após o técnico.
- 27 históricos preservados + 2 casos novos: 29 `PASS_CONTAINMENT`, 29 resultados/replays `UnknownBlocked`. A1/A2: 11 snapshots modelados; HKCU: 4 ensaios; supervisão C#: 2; A5: 8 entradas inválidas rejeitadas, uma válida aceita.
- Evidência positiva do item **PENDENTE**. Não houve `PASS_EXPERIMENTAL_ACTIVATION`.

### 2. Problema técnico delimitado

O aplicativo real utiliza `DarkProgramList : Control` (em `DarkControls.cs`) com renderização owner-drawn dentro de `OnPaint`, chamando `DrawProgramCard` em `Program.cs`. O nome é desenhado por `TextRenderer.DrawText` na própria superfície. Não há `HWND` de item individual já qualificado. O oráculo anterior de `win32_process_observation.py` passou a recusar a prova positiva, corretamente.

A ausência do aviso de busca vazia, o eco do `EDIT`, a contagem de apps, a existência do HKCU ou a mudança global de pixels **não** demonstram o nome exato desenhado na linha.

### 3. Ordem de implementação: capacidade, depois prova

**O1 — Probe de acessibilidade externa (primeiro, sem mexer no produto).**

1. Em Windows GitHub Actions descartável, usar cliente separado de MSAA/UI Automation (UIA), vinculado ao `HWND` da janela e PID do GF real; localizar estruturalmente o controle da lista sem confiar no texto ecoado em `EDIT`.
2. Não enumerar nem registrar nomes de aplicativos não relacionados. O observador compara em memória apenas o desafio gerado pela fixture HKCU; saída pública somente booleanos/enum de razão.
3. Verificar se o item exato possui nó acessível de lista/linha com nome atual, retângulo delimitador válido, não oculto, processo correto e atualização temporal após a pesquisa.
4. Experimentos: semente presente/ausente; `EDIT` com texto correto mas item ausente; item errado; vazio ausente; stale; oculto; nó estático fora do controle; outra janela/PID. **Toda ambiguidade é FAIL_CLOSED.**
5. Se UIA/MSAA não expuser a linha real (possibilidade alta por owner-drawn), reportar `UI_ACCESSIBILITY_ITEM_UNAVAILABLE` e prosseguir ao estudo visual, **sem fabricar um nó ou modificar o produto**.

**O2 — Estudo visual externo restrito, somente se O1 falhar.**

1. Captura efêmera **somente da região cliente do controle de lista** do processo de teste validado, nunca de tela inteira, documentos, navegador ou janelas alheias. Não salvar bitmap, texto pessoal, nomes instalados ou crops em artifacts/logs.
2. Identificar o `HWND` da lista por árvore pai/filho, classe WinForms, características e posição; confirmar PID e visibilidade/oclusão/escala, estabilidade do quadro e estado atual da busca. Em sessões não interativas, recusar prova.
3. Procurar evidência específica do **desenho do nome do desafio**, não apenas delta entre tela vazia e preenchida. Considerar comparação determinística de glifos/segmentos com o renderizador usado pelo GF e controles independentes. **Não tratar semelhança visual genérica ou hash de pixels como autoridade positiva.**
4. Negativos obrigatórios: mesmo texto só no `EDIT`; item ausente com aviso de vazio defeituoso; nome parecido; item em local errado; crop stale; imagem trocada após verificação; item oculto/obstruído; lista não pertencente ao PID. Se não houver método determinístico e falsificável, `POSITIVE_UI_PROOF_PENDING`.
5. A captura não pode virar testemunho criptográfico de bytes executados, prova contra adversário same-user, ou autorização de recuperação. Sem captura/telemetria remota.

**O3 — Integração mínima com o experimento existente somente após O1/O2 demonstrável.**

1. Reutilizar `RealOwner` e observador real, OLD/NEW, pins, fences, journal e TUF do laboratório. Não criar um segundo coordenador ou um recibo de READY.
2. Apenas o `RealOwner` original vivo pode registrar observação positiva *experimental* se desafio de sessão, PID, controle, item e negativos forem comprovados; gravação de relatório local não é autoridade independente.
3. A ausência de prova positiva, timeout ou qualquer diagnóstico inconclusivo obriga saída não zero, sem registro `New`, preservando OLD/NEW e `UnknownBlocked`/replay.
4. Mesmo com `PASS_EXPERIMENTAL_ACTIVATION` demonstrado, `READY=false` e **zero recuperação automática**.

### 4. Política de validação econômica

- Primeiro, um **probe dirigido** e seus negativos num Windows descartável; não repetir os 29 casos só para ver se a API expõe itens.
- Definir critérios verificáveis de seleção e rejeição, provar que falsos positivos anteriores A1/A2 permanecem impossíveis no oráculo novo.
- Só quando existir evidência positiva externa específica, integrar e executar a regressão completa: 27 históricos intactos + 2 novos + novos controles adversariais, com nomes e logs, SHA técnico congelado e CI.
- Encaminhar ao WORK **após** o experimento positivo demonstrável e CI verificada, para tentar refutá-lo. Caso não seja possível sem alterar o produto, comunicar ao mantenedor o limite em vez de promover um status.

### 5. Invariantes e escopo

**Fase 1 ABERTA. READY=false. G1–G8 BLOCKED 0/8. Zero recuperações automáticas. HOLD é contenção.**

Preservar arquitetura B+A portátil, TUF/NTFS/TOCTOU, restrições de privacidade, canário, OLD/NEW, journal/fences e proteção de bytes. **Sem** serviço/helper privilegiado, ACL/política Windows, TPM, backend, GF LOW, alteração do aplicativo, merge, release, deploy ou mudança da PR #65 Draft/BETA .66.

H1/H2/H3 permanecem não comprovados. A evidência visual pode sustentar somente um teste funcional do item no processo original; **não** prova READY operacional, despachos obrigatórios de NEW, persistência resistente a rollback do mesmo usuário ou quiescência de efeitos externos.
