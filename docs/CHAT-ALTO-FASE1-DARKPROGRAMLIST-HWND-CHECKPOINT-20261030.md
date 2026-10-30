# CHAT ALTO — GERENCIADOR FINAL — Fase 1, Opção B

## Checkpoint: vínculo estrutural do HWND de DarkProgramList — sem prova positiva do item

**Natureza:** fechamento técnico limitado do incremento de descoberta de controle, sem promoção de READY.

### 1. Estado e versões

- Branch autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `9476b0b8b2fa89903a6ecd1d443cb65c984c0253`. Não havia alterações posteriores antes de escrever.
- **SHA técnico congelado desta investigação:** `756c8bf37853ef564c9ecfef4ca75424c46bd4c5`.
- [CI dirigida 41146911715](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41146911715): `completed/success`, job `124374037245`, tentativa 1.
- Última CI transacional completa previamente confirmada: [41143130572](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41143130572) no SHA `e0b8bc414d6756f542b7b3af6e82a820b34aa70b`.
- A regressão histórica [41145899676](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41145899676), de commit intermediário `64fd93d5b49024f42f783906a9fcfd33aebf8110`, continuava **in_progress** na última checagem; não é aprovada por inferência.

### 2. Hipótese e correção

O estudo anterior buscava a etiqueta `_emptyState` durante uma pesquisa com resultado esperado. `Program.cs` ajusta `_emptyState.Visible = match.Length == 0`. Logo, o rótulo usado como âncora pode não ter HWND visível na pesquisa positiva. A falha anterior de estrutura era compatível com isso.

O observador agora faz a **pesquisa negativa primeiro**, confirma que o texto de aviso está visível e procura exatamente um controle irmão sem texto e com classe de janela WinForms dentro do mesmo painel, com limites geométricos coincidentes; recusa zero/múltiplos candidatos. O candidato é vinculado ao PID observado, retângulo da janela principal e pesquisa corrente. Em seguida, a pesquisa positiva ocorre e é exigido que o HWND, pai, classe e identidade de processo permaneçam os mesmos.

**Arquivos técnicos alterados:** `prototipos/autoatualizador-seguro/phase1-visual-probe/visual_feasibility.py`; `visual_structure_negative_tests.py` (novo); `.github/workflows/gf-phase1-visual-readonly-probe.yml` (teste dirigido). Não há alteração do aplicativo, de `phase1-composed`, C#, reconciliador, TUF, seletor ou diário.

### 3. Evidência extraída da CI real

Log da execução `41146911715`:

- `GF_PHASE1_LIST_STRUCTURE_MODEL`: 9 cenários modelados, 8 inválidos rejeitados, `passed`; não são testes reais contra múltiplas janelas maliciosas.
- `GF_PHASE1_VISUAL_FEASIBILITY`:
  - `real_gf_verified=true`
  - `verified_window_visible=true`, `verified_window_foreground=true`
  - `positive_query_echo=true`, `negative_query_echo=true`
  - `structural_list_region_found=true`
  - `list_handle_persisted=true`
  - `owned_list_roi_verified=true` (vínculo ao processo, **não prova forte de ausência de sobreposição interna do mesmo processo**)
  - `pixel_samples_complete=false`, `item_name_visually_proven=false`
  - `automatic_recovery_authorized=false`, `operational_ready=false`.
  - Razão explícita `LIST_HWND_IDENTIFIED_ITEM_IDENTITY_UNPROVEN`.

### 4. Interpretação e limites

**Aceito somente:** candidato de HWND de lista identificado de forma única e coerente com a topologia conhecida do produto, no Windows descartável testado, e que persiste após mudança da busca. A identificação é estrutural, baseada no código fonte da aplicação e controles WinForms; não é uma prova independente do tipo CLR interno contra outro código adulterado no mesmo processo. Nenhum item individual foi observado por UIA/MSAA.

**Não demonstrado:** pixels da lista, glifos do nonce desenhados, correspondência entre o item da fixture HKCU e a linha visível, autenticação dos bytes efetivamente carregados, isolamento hostil same-user ou recuperação segura. A tela pode ter outro controle do próprio GF sobreposto em determinadas transições; um hit test de apenas PID seria insuficiente para a próxima etapa.

### 5. Próximo incremento estritamente experimental

1. Confirmar que após a pesquisa **positiva**, quando `_emptyState.Visible` passa para false, os pontos de hit test retornam **o HWND da lista ou um descendente identificado**, nunca apenas “mesmo PID”.
2. Validar topologia e posição contra deslocamento, ocultação, janelas sobrepostas, janela errada, troca de handle/parent e resize/DPI. Falhar fechado em todos os casos.
3. Só então estudar em memória a área restrita da primeira linha do GF. **Diferença ou hash de pixels não é evidência de nome específico.** Necessária uma metodologia independente e falsificável de reconhecimento de glifos do desafio, com controles negativos reais (seed ausente, item errado/parecido, conteúdo stale, busca apenas no EDIT). Sem persistir imagens ou textos pessoais.
4. Se não houver metodologia suficientemente robusta sem alterar o produto, manter `POSITIVE_UI_PROOF_PENDING`.
5. Não repetir 29 testes históricos para simples ajustes de pesquisa estrutural; executar regressão transacional ao integrar prova positiva e só então chamar WORK para auditoria independente.

### 6. Contrato invariável

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas autorizadas. HOLD é contenção.** H1/H2/H3 ainda não comprovados. Sem mudança em main, PR #65 Draft, BETA .66, produto, credenciais, serviços, políticas Windows, backend, merge, release ou deploy.
