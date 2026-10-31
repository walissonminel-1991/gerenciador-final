# GERENCIADOR FINAL — Decisão CHAT ALTO após auditoria independente WORK D2.4

**Data local:** 2026-10-30, Fortaleza. **Estado desta rodada:** decisão arquitetural / checkpoint documental; sem alteração funcional, sem nova CI. **Caráter:** avaliação interna de engenharia, não homologação externa.

## 1. Referências congeladas

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch de escrita autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `692c07430beab5d5a1d43f231309780402bd240c`.
- SHA técnico D2.4: `a7e2a15a571a95ca0ed53124c8c3c2d89a446ee3`.
- CI D2.4: [41155677811](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155677811), SUCCESS, job 124401833809, tentativa 1: fluxo inerte positivo, 12 contraprovas de preflight, 14 de ponte, bypass direto de TUF **aceito**.
- Regressão D2.3: [41155563065](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41155563065), SUCCESS.
- Parecer WORK recebido do mantenedor como `Markdown colado(1).md`, auditoria somente leitura com inspeção dos fontes e CIs. A análise CHAT ALTO reconferiu na branch o bypass, o `finally` de limpeza, a dupla leitura do bootstrap e o gate de CI.

## 2. Deliberação expressa

**APROVADA A OPÇÃO 1 como arquitetura experimental corretiva: uma única sessão sob autoridade do coordenador C#, usando Python-TUF obrigatório para autenticação e exatamente um transportador .NET reutilizado da D2.3.**

Não avançar para D3 antes de executar e testar as correções D2.4.1–D2.4.4. Não criar nova implementação paralela de TUF nem segundo downloader. Não promover nenhuma prova isolada, hash em argumento, nonce, recibo de laboratório ou sucesso de CI à categoria de autoridade de instalação.

**Fronteira de confiança delimitada:** no ensaio, o processo C# deve possuir a decisão de admitir o estágio e orquestrar um verificador TUF filho da própria sessão. O único método de preparar dados deverá exigir o resultado de validação obtido pelo próprio coordenador nessa sessão; chamadas CLI diretas com manifesto+hash não podem entrar no caminho de preparação. Python-TUF não deve ser substituído por uma aprovação arbitrária emitida externamente. Mesmo assim, a solução **não estabelece isolamento criptográfico contra um adversário do mesmo usuário** capaz de modificar código, processos ou armazenamento. O empacotamento de Python é opção experimental, não decisão definitiva de distribuição portátil.

## 3. Matriz crítica do WORK, estado e ação

| ID | Achado | Estado da evidência | Resposta obrigatória |
| --- | --- | --- | --- |
| W24-01 | Invocação direta da ponte C# sem TUF | **Bypass comprovado em CI**; o C# aceita manifesto+SHA públicos, `d24_chain_lab.py` teste próprio | Remover `--d24` como entrada de preparo confiável; integrar validação obrigatória na mesma sessão, sem aceitar recibo/hash fornecido pelo chamador como prova. Contraexemplos de manifesto+whitespace+hash atualizado, sessão cruzada e processo filho falso. |
| W24-02 | `Stage` apaga arquivos preexistentes em `finally` | **Falha lógica estática, ainda sem reprodução Windows adversarial**; `FileMode.CreateNew`/rename falham mas cleanup remove nomes preexistentes | Corrigir primeiro, preservar canários de parcial e destino, mesmo sob falha. Limpar apenas objetos criados e ainda identificados inequivocamente pela tentativa; em identidade ambígua preservar e bloquear. |
| W24-03 | Bootstrap TUF conferido e depois relido | **Lacuna TOCTOU no código**; exploração concorrente não demonstrada | Ler um snapshot limitado e pinado uma única vez, passar exatamente os mesmos bytes à biblioteca TUF; testar troca entre etapas. |
| W24-04 | `HttpClient.Timeout` não cobre necessariamente body com `ResponseHeadersRead` | **Lacuna de deadline no código**; teste de corpo parado pendente | Deadline absoluto por tentativa e token propagado por conexão, cabeçalhos, streaming e verificação; cancelar em falha e não alegar retorno seguro se conclusão ambígua. |
| W24-05 | Caminhos sob `RUNNER_TEMP` e readback não garantem identidade NTFS | **Lacunas estáticas**; corrida/junction não reproduzidas | Reutilizar primitivas de NTFS dos laboratórios existentes somente após inspeção de adequação; testar ancestrais junction/reparse, troca de objetos, handles retidos e concorrência. Não apagar arquivos de identidade incerta. |
| W24-06 | D2.4 recomeça com TUF bootstrap v1 em cada preflight | **Limitação demonstrada pelo desenho** e contraexemplo D1.2 histórico | Sessão persistente versionada de v1→v2, serializar tentativas, impedir replay com histórico intacto; manter H1 global não comprovado. |
| W24-07 | Gate da CI aceita campos booleanos omitidos | **Lacuna estática** | Esquema estrito: todos os campos de segurança presentes, `is [bool]` quando pertinente, valores exatos, `null`/omissão/strings/zero recusados. |
| W24-08 | Filtros workflow não cobrem dependências importadas | **Lacuna de regressão** | Expandir `paths` para fontes D0, D1, D1.1/D1.2, D2, D2.3, bootstrap e workflow, ou adotar CI de integração explícita. |
| W24-09 | Contador de conexões gravado somente após `Stage` bem-sucedido | **Lacuna de evidência diagnóstica** | Contar conexões no `finally` e exigir observações de falhas; não usar zero como evidência de nenhum socket sem verificação. |

## 4. Incrementos corretivos sequenciais

### D2.4.1 — Preservação de arquivos e prazo, antes da autoridade

- Reproduzir os canários: parcial preexistente e destino preexistente. Ambos têm de permanecer com o mesmo conteúdo/identidade após a recusa, nunca apenas “arquivo final ausente”.
- Corrigir limpeza para provar posse do objeto criado; preservar se a identidade do objeto tiver se tornado inconclusiva. Registrar HOLD para estados de segurança/recuperação inconclusivos, sem classificar como rollback.
- Introduzir prazo absoluto, testes de corpo parado depois de headers e streaming lento, e coleta de conexões inclusive em erro.
- Manter regressão D2.3 3/3 positivas, 25 negativas e demais ensaios aplicáveis; jamais trocar os testes existentes por um contador reduzido para obter CI verde.

### D2.4.2 — Autoridade de sessão única

- Coordenador C# como única entrada de preparação de laboratório; inicia e acompanha verificador Python-TUF de identidade fixa e sessão delimitada, obtém decisão e controla quem pode preparar o estágio.
- Eliminar a autorização implícita por arquivo+SHA fornecido por chamador externo; o antigo `--d24` deve ser recusado como ação de preparo (ou ficar somente para teste adversarial que exige rejeição).
- Ler bootstrap pinado uma vez, usar exatamente os bytes verificados; bloquear em falha, ausência, saída do filho errada/incompleta, replay, hash recalculado pelo atacante, sessão divergente, troca de manifesto e cancelamento.
- Identidade/controle de processo e IPC devem ser testados, mas **IPC local não é isolamento de segurança same-user**. Não alegar autenticação operacional quando a chave de teste é pública.

### D2.4.3 — Histórico TUF persistente e caminho NTFS conservador

- Demonstrar duas gerações TUF em processos distintos, recusar replay com histórico intacto e serializar tentativas.
- Reproduzir deliberadamente a aceitação após reversão integral do histórico e manter `H1=false` na saída. Não prometer recuperação segura ao usuário quando faltarem provas.
- Testar junction ancestral, substituição concorrente, file ID/handle e limpeza após erro. Reutilizar controles de L18–L22 apenas se apropriados para o contexto de aquisição; sem retomar recuperação funcional do produto.
- Exigir que os bytes entregues ao coordenador sejam os mesmos autenticados ou classificar como UNKNOWN/HOLD. Não instalar ou lançar binários.

### D2.4.4 — Evidência consolidada e revisão independente

- Matriz composta de positivos e negativos dirigidos, CI Windows repetível, inclui regressões D2.3 e D2.4. Expandir gatilhos de dependências.
- CI deve verificar schema, presença, tipo e valores de todas as afirmações críticas; testar adulteração e omissão da própria resposta de teste.
- Conservar contraexemplos H1/NTFS same-user que ainda não tiverem remediação comprovada e rotular suas limitações.
- Congelar SHA técnico, CI e HEAD documental. Só então solicitar novo parecer WORK **somente leitura**, adversarial, sem gastar WORK em cada pequena correção.
- D3 (execução/seletor OLD/NEW) fica expressamente **bloqueada** até decisão posterior fundamentada; nenhuma conclusão de D2.4 abre G1–G8.

## 5. Restrições e orientação de custo

- A biblioteca python-tuf livre não exige pagamento recorrente nem protocolo próprio. Porém, incluir Python no EXE portátil pode elevar tamanho e falso positivo antivírus: decidir empacotamento apenas após medir viabilidade/portabilidade em Windows x64 e compatibilidade com a arquitetura B+A.
- Não colocar chaves privadas de produção em GitHub, mesmo em repositório privado. As seeds públicas existentes servem **somente** ao laboratório.
- Não criar Release pública, baixar executáveis, instalar, atualizar GF real, acionar recuperação, tocar em dados pessoais, main, PR #65 Draft, BETA .66 ou Cloudflare.

## 6. Estado final desta decisão

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD apenas contenção.** H1, H2, H3 e TOCTOU same-user continuam não comprovados. Esta decisão é documental e não muda código ou CI de laboratório.
