# CHAT ALTO — Fase 1, Opção B — incremento corretivo A1–A5, candidata ainda não validada

Data: 2026-10-30. Documento de checkpoint de **implementação experimental**, não homologação.

Base adversarial WORK (imutável): `fd88cd30a2dee25576fc76df6c6c6d06636e0acc`; CI histórica `41133853256` SUCCESS, 27 contenções históricas, 1 negativo e 1 positivo experimental. Parecer WORK A1–A5 lido integralmente, simulações em memória distintas de Windows.

Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
Base documental anterior: `4233fc725550413c41da16d0616017283b7fd951`.
Último SHA técnico **candidato**: `85a955e7cf8da7c2e5eea6f7e5f4ad810a648cc6`.

## Alterações experimentais limitadas

- **A1:** `win32_process_observation.py` usa predicado puro `search_snapshot_proves`; ausência/ocultação do texto de vazio **não** indica item; para resultado positivo, precisa controle de lista identificado e testemunho explícito do item. O GF `DarkProgramList` atual é owner-drawn e não oferece HWND individual de itens; o caminho real passa `inventory_list_hwnd=None` e **recusa prova positiva**. `PASS_EXPERIMENTAL_ACTIVATION` temporariamente **não** é emitido. Uma alternativa confiável, sem modificação do produto, **não foi demonstrada**; nenhuma asserção forçada para true.
- **A2:** o controle negativo roda pelo mesmo predicado, independentemente da busca positiva; a decisão funcional não depende de `trial_registry_seed_created`. Ausência do item produz diagnóstico específico `TRIAL_EXPECTED_ITEM_NOT_OBSERVED`; item inesperado em modo não semeado é inconsistente.
- **A3:** `trial_registry_fixture` substitui OpenKey+CreateKeyEx por RegCreateKeyExW nativo exigindo `REG_CREATED_NEW_KEY`, sem sobrescrever chave existente; token proprietário e inspeção de todos os valores/subkeys. Limpeza passa a usar `RegDeleteKeyEx` com HKEY ainda aberto e subchave vazia, evitando re-resolução pelo nome, conforme uso equivalente na implementação de .NET. **Ainda sujeito à prova no Windows**. Escrever dados no mesmo objeto pelo mesmo usuário entre verificação e exclusão continua limitação de isolamento não resolvida.
- **A4:** identidade da fixture é registrada em arquivo temporário antes da primeira escrita HKCU; o owner C# vivo executa verificações/limpeza exata após término do observador; erro ou ambiguidade preserva registros e bloqueia transição `New`. Se o owner inteiro morre, não há promessa de limpeza automática; permanecem contenção e diagnóstico conservador.
- **A5:** `Phase1RecoveryTests.cs.txt` emite `unsafe_recovery_after_real_new=false` explicitamente nos dois novos casos; o gate de workflow usa o mesmo predicado tipado para casos reais e variantes adversariais (campo ausente, null, string false, bool true).

## Casos novos

- `search_oracle_adversarial_tests.py`: **11** cenários modelados, incluindo falso positivo A1 (aviso ausente, item ausente), row stale, static não vinculado, controles vazios e positivo sintético ancorado ao identificador da lista. Modelos NÃO são prova do GF real.
- `registry_fixture_adversarial_tests.py`: **4** ensaios para Windows descartável: colisão RegCreateKeyEx existente sem sobrescrita; exclusão por handle; mutação do objeto preservada; encerramento forçado de trabalhador com supervisão e limpeza exata. Nenhum deles deve ser descrito como executado até CI inspecionada.
- Suíte real C#: ainda **29 cenários**, preserva 27 históricos, enquanto os dois testes novos devem comprovar contenção e ausência de falso `New`. Sem testemunho visível de item, positivo será `POSITIVE_UI_PROOF_PENDING`, não sucesso funcional.
- Workflow: novas asserções A1/A2, A3/A4, A5 e 27 históricos. **Nenhuma alteração de produto**.

## Estado do checkpoint — não confundir implementação com evidência

Na última conferência, CI da candidata `41141320091` estava **pending**; `41140714543` (commit intermediário antigo) seguia `in_progress`. A aprovação histórica `41133853256` é anterior a este diff e **não valida** correções A1–A5.

Não declarar nenhuma suite nova PASS sem GitHub Actions SUCCESS + logs inspecionados. Se falhar: identificar estágio/caso real e fazer apenas correção estritamente necessária. Não aceitar RUN cancelled/pending como êxito.

## Condições de avanço

1. Confirmar HEAD técnico exato e CI associada, verificar compilação e casos A1/A2 e A3/A4/A5 por nomes, além dos 27 históricos e dos novos casos C#; não inferir pelos totais.
2. Se WIN32 list owner-drawn impedir testemunho de item, manter `POSITIVE_UI_PROOF_PENDING`; estudar alternativa de acessibilidade externa realmente verificável **sem alterar produto** ou pedir nova decisão de escopo. Não substituir por bool/receipt.
3. Auditoria WORK **somente após** CI validada, sobre SHA congelado; não repetir H1/H2/H3 genericamente.

Estado formal invariável: **Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, zero recuperações automáticas autorizadas.** Arquitetura B+A portátil preservada; sem helper privilegiado, TPM, ACL/políticas, backend, GF LOW, integração, merge, release ou deploy.
