# CHAT ALTO — Fase 1, opção B — incremento corretivo A1–A5 (contrato de implementação)

**Data:** 2026-10-30. **Natureza:** plano técnico e critérios de aceitação, **não implementação nem evidência nova de CI**.

**Base auditada (imutável):** `fd88cd30a2dee25576fc76df6c6c6d06636e0acc`. **CI base:** `41133853256`, job `124331302448`, sucesso, 29 casos (27 históricos + positivo + negativo). **HEAD documental visto antes desta escrita:** `4dcc83645e8c381e11eb7639a528e537c19ecdfb`. Origem: parecer adversarial WORK em somente leitura, 30/10/2026; cinco achados A1–A5. Simulações em memória **não são ensaios Windows**.

## Invariantes não negociáveis

Somente `experimento-g5-ipc-chat-alto-20261029`. Preservar B+A portátil, mesmo coordenador C# L18 e seletor, TUF/NTFS/TOCTOU, OLD/NEW, fences, journal, proteção de dados, exclusão de alterações em produto. `HOLD` é contenção, não recuperação. Fase 1 **ABERTA**, `READY=false`, G1–G8 `BLOCKED 0/8`, **zero** recuperações automáticas autorizadas, inclusive se um novo ensaio positivo passar. Proibidos serviço/helper privilegiado, TPM, ACL/políticas Windows, backend, GF LOW, integração, merge, release, deploy.

## Ordem técnica de correção e gates de refutação

### A1 — substituir o oráculo positivo de busca (prioridade bloqueante)

Código: `win32_process_observation.py`, `read_only_search_round_trip`.

1. Proibir deduzir presença a partir de `not (empty and empty[4])`; `WM_GETTEXT` da caixa e ausência do aviso **não provam item**.
2. Exigir observação **positiva do item exato** e do seu vínculo ao controle de resultados, além de prova de controle negativo. Identificar janela/controle e propriedade pelo PID antes/depois; resultados ambíguos, controle ausente, estado transitório, mensagem stale ou suporte insuficiente devem retornar **não comprovado**. Não registrar nome/nonce em log.
3. **Constatação técnica:** no código real, `DarkProgramList : Control` é desenhado pelo próprio controle (não linhas filhas do Win32). Não inventar garantia de item a partir de child HWND. Investigar APIs de acessibilidade compatíveis **sem alteração do produto**; se não houver testemunho positivo confiável, manter `PASS_EXPERIMENTAL_ACTIVATION` **bloqueado**, documentando limite em vez de mudar um booleano.
4. Testes do oráculo: (a) dado item correto e controle confiável, aceita; (b) item ausente e aviso vazio ausente, rejeita (contraprova A1); (c) controle de aviso faltante ou contraditório, rejeita; (d) eco correto de busca com lista stale/incorreta, rejeita; (e) pesquisa negativa realmente sem item, aceita apenas como controle negativo.
5. O primeiro gate é executar negativas dirigidas sobre implementação nova antes do ensaio positivo real Windows.

### A2 — positivo e negativo pelo MESMO avaliador

Código: `phase1_lab.py` (bloco de `trial_mode`), observador, testes C#.

1. Variar **somente** a existência legítima da fixture HKCU entre cenários. `trial_registry_seed_created` descreve setup e não deve ser cláusula fixa que determina o resultado de saúde.
2. Normalizar decisão de saúde: o mesmo predicado exige testemunho positivo do item e controle negativo; no caso sem fixture, procurar explicitamente motivo `EXPECTED_ITEM_NOT_OBSERVED`, não aceitar erro genérico ou ausência de semente como justificativa funcional.
3. Testes de refutação cruzados com observações simuladas: `seed=false, positives=true` não pode ser rejeitado **apenas** pelo indicador de setup; deve apontar inconsistência observacional; `seed=true, positive=false` rejeita por falta de prova; `seed=false, positive=false` rejeita **pelo oráculo funcional**, não por veto independente.
4. C# permanece sem `New` e retorno não zero quando saúde falhar; assert do motivo específico.

### A3 — criação e limpeza de HKCU sem apropriação de objeto alheio

Código: `phase1_lab.py`, `trial_registry_fixture`.

1. Substituir `OpenKey` + `CreateKeyEx` por criação nativa que reporte `REG_CREATED_NEW_KEY` de `RegCreateKeyExW`; falhar **antes de escrever** se `REG_OPENED_EXISTING_KEY` ou não houver prova de propriedade.
2. Manter handle e vincular metadados de identidade sob namespace estritamente temporário do supervisor. Nunca decidir propriedade por `DisplayName` copiável, nem apagar varrendo prefixos.
3. Reavaliar protocolo de exclusão por **nome**: comparação seguida de `DeleteKeyEx` tem TOCTOU. Quando exclusão do mesmo objeto não puder ser garantida frente a ator same-user, falhar fechado, preservar chave para diagnóstico e registrar resíduo **sem identificadores sensíveis**. Não afirmar proteção global ao mesmo usuário sem autoridade independente/ACL.
4. Testes: ator de laboratório cria chave antes da chamada nativa → recusa sem sobrescrita; troca entre conferência e limpeza → não excluir conteúdo substituto; chaves vizinhas reais/sintéticas nunca alteradas; nenhuma escrita de `UninstallString`. Intercalações de memória são testes de algoritmo, **também** executar ensaios Windows dirigidos em runner descartável com barreiras determinísticas.

### A4 — término forçado durante fixture

1. Registrar propriedade/identidade do marcador no supervisor **antes** do primeiro possível efeito; planejar barreira determinística de `fixture ativa`.
2. Matar só o observador enquanto supervisor permanece vivo; `finally` não executa após `TerminateProcess`. O supervisor deve inspecionar estado e, **se e somente se** demonstrada a identidade/exclusividade, limpar o recurso exato. Se ambíguo, reter para diagnóstico, bloquear replay/retry e **não apagar chave de terceiros**. Verificar ausência no final normal.
3. Ensaios Windows: corte antes da criação, após criação e durante observação, com e sem substituição adversarial; não aceitar `New`, rollback ou READY após corte. Diferenciar limpeza demonstrada de `HOLD` conservador. Não utilizar limpeza ampla por prefixo.

### A5 — esquema total e tipado de evidência

1. `Phase1RecoveryTests.cs.txt`: emitir `unsafe_recovery_after_real_new=false` explicitamente nos dois resultados após as asserções de `UnknownBlocked`, replay, seletor e preservação.
2. Workflow `gf-autoatualizador-fase1-real-lab.yml`: para **cada** resultado, exigir propriedade existente e `[bool]` exato, não fazer `!$_.campo` em campo possivelmente ausente. Rejeitar valor string `"false"`, `null`, propriedade ausente. Validar também `operational_ready` e classificação tipadas, sem reduzir os guards existentes.
3. Testes: relatório completo com `false` passa apenas se todos os outros gates passam; remover campo, trocar para `null`, string ou `true` deve falhar no gate.

## Fortalecimento adicional sem ampliar autoridade

Observador grava JSON local mutável que é lido duas vezes; considerar uma única leitura pós-exit, presença de desafio de tentativa e vínculo verificado de contexto, sem converter esse relatório em testemunha criptográfica contra usuário com mesmo nível de privilégio. Não alterar as decisões `ReconcileReal` ou autorizar operações por causa disso.

## Aceitação e checkpoint obrigatório

- Primeiro, demonstrar que as contraprovas A1–A3 e A5 são rejeitadas pela implementação nova (testes de mesa/algoritmo), e executar os cortes Windows A3/A4 no runner descartável; separar em relatórios `REPRODUCED_IN_MEMORY`, `WINDOWS_EXECUTED`, `NOT_EXECUTED`.
- Depois rodar casos positivos/negativos reais **e** todos os **27 casos históricos** com asserções intactas; dar nome e resultado individual a cada novo cenário. O total não deve ser fixado em 29 ao adicionar novos testes.
- Evidência positiva só é aceitável se o item for **realmente observado** na interface por mecanismo confiável; caso contrário marcar `POSITIVE_UI_PROOF_PENDING`, sem promover status ou manipular gate.
- Preservação de OLD/NEW, alvo assinado, bits da imagem em disco, fences, replay `UnknownBlocked`, privacidade, ausência de recuperação e classificação `READY=false` são gates imutáveis.
- Entregar SHA técnico congelado, diff restrito, CI Windows/run/job/status, resultados detalhados, cenários não executados, lacunas remanescentes e roteiro de próxima auditoria WORK **apenas após** CI concluída e inspecionada. CI verde sozinha não encerra Fase 1.

**Checkpoint atual:** especificação preparada; **nenhuma correção de código neste documento**, nenhuma CI nova, nenhum READY, nenhuma recuperação autorizada.
