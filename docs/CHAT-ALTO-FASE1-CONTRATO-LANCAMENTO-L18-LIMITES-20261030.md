# GERENCIADOR FINAL — Fase 1 — contrato de lançamento L18 e limite de recuperação

**30/10/2026 · Revisão de fonte e reconferência de evidências existentes.**
**Decisão: não autorizar recuperação automática de OLD no envelope atual.**
Contrato abaixo é especificação de pesquisa, **não implementação, integração ou novo contrato de produto**.

## 1. Âncoras confirmadas antes da escrita

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial: `78bbdedc4afdc4f544bb6084e8a3386f1e3bd190`; árvore `b2a2928ea194de0ff040899960ea6beaa9a3ed96`.
- Último commit técnico validado: `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`.
- A comparação técnico → HEAD mostra quatro commits posteriores e somente três caminhos de documentação, sem diferença de código.
- [CI Windows 41089673054](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41089673054), tentativa 1, `head_sha=749ef3206eff67ea6118ef405aa6f34cb30ef0e8`, job `124179955658`, `completed/success`. Log reconferido: **22 registros de casos, todos PASS_CONTAINMENT, zero recuperações e failed=0**.
- PR #65 reconferida `open/Draft/merged=false`, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- Sete arquivos de fonte lidos nesta rodada tiveram seus snapshots locais conferidos contra os **blob SHAs do GitHub**. Identidades e referências exatas estão no [recibo](evidencias/fase1-contrato-lancamento-l18-20261030.json).
- Nenhum novo caso Windows, nova CI, build ou execução do GF nesta rodada. A contagem continua 22/22; as observações seguintes são de fonte.

Ponto de partida: [checkpoint de IDs de provedor após crash](CHAT-ALTO-FASE1-RECUPERACAO-ID-PROVEDOR-APOS-CRASH-20261030.md). MSI/MSIX/WUA ficam encerrados **por ora** como trilha de autoridade do autoatualizador: `NOT_A_RECOVERY_AUTHORITY`. Não reabrir essa busca sem uma propriedade nova que mude a decisão.

## 2. Resultado verificável: a fronteira não pode ser definida só como “antes de NEW”

### LC01 — O caminho normal já executa OLD antes do fence de NEW

Em `Phase1Recovery.cs.txt:179–194`, `RealOwner(cut="normal")` seleciona OLD/1, grava `OldLaunchFence` e chama `RunRealObserver(...,"--recovery-observe")`. Somente depois, em `:208–214`, seleciona NEW/2, grava `NewLaunchFence` e registra `real-new-effects-fenced-before-create`.

O observador não apenas lê metadados: `phase1_lab.py:435–444` deriva o pacote do seletor e chama `observed_process`; em `:276–279`, esse caminho cria `GerenciadorFinal.exe --tray`.

**Consequência para o contrato:** “NEW ainda não iniciou” não basta para chamar todo o período anterior de preparação sem efeitos. A própria sondagem de OLD é execução de produto e precisa ser contabilizada. O código atual já reconhece isso com o fence de OLD; **não foi encontrado aqui um bypass do bloqueio atual**. A conclusão impede apenas que um desenho futuro apague essa distinção ao separar PREPARE/ACTIVATE.

### LC02 — Um gate de UI/startServices chega depois de escritas

A ordem observável na fonte congelada é:

| Ponto | Fonte | Efeito/limite concreto |
| --- | --- | --- |
| Entrada normal antes de interpretar `--tray` | `Program.cs:26,64–66` | Chama `DiagnosticTrace.Start/Write` antes de criar MainForm |
| Inicialização do diagnóstico | `DiagnosticTrace.cs:39–56,70–94` | Usa pasta de diagnóstico em LocalAppData, com fallback TEMP; cria diretório, remove a entrada de log anterior e cria/grava/flush do novo log |
| Inicialização de campo da janela | `Program.cs:593`; `LocalReliabilityHistory.cs:25–42` | Constrói histórico local, cria arquivo e grava cabeçalho antes de um gate em `Shown` |
| Gates existentes | `Program.cs:712–715,794–818` | `startServices` limita caminhos de navegação/Shown, depois dessas inicializações |
| Entradas alternativas | `Program.cs:20–24` | Leitor COM e `--worker` são despachados antes da janela; um gate só na UI não os cobre |

A remoção mencionada é da entrada de diagnóstico prevista pelo próprio GF, **não uma alegação de exclusão de dados pessoais**. Escrita “externa”, neste contrato, significa fora do domínio de staging da tentativa, mesmo que o arquivo pertença ao GF.

**Conclusão:** `--tray`, janela oculta, `startServices=false`, um ACK em `Shown` ou um botão desabilitado não constituem bloqueio de efeitos. Isso também não demonstra que WUA, rede ou reparos foram executados nesta revisão: as políticas BETA existentes continuam condicionando esses caminhos.

### LC03 — Separação útil já existente, sem nova autoridade

`phase1_lab.py:363–387` termina o caminho `--preflight-only` antes de observar GF. Em `:394–419`, `--recovery-verify` revalida TUF e arquivos e retorna antes de `observed_process`.

Esses pontos podem orientar uma preparação baseada em **dados**, reutilizando TUF, manifesto e pins existentes. Não certificam todas as dependências do verificador, não são sandbox de I/O e não criam testemunho protegido. Não foi adicionado um segundo coordenador ou modo de execução do produto.

## 3. Contrato mínimo proposto: L18-GF-LAUNCH/0

**Estado: DOCUMENTADO, NÃO IMPLEMENTADO, NÃO OPERACIONAL.** Não altera `gf-self-update/1`, B+A ou o protocolo L18 existente.

**Identidade:** a tentativa vincula instalação, transação, manifesto autenticado, geração e objetos OLD/NEW verificados. Esses identificadores autenticam o objeto e o contexto; não autenticam, sozinhos, a história de execução.

**Preparação:** o coordenador trata o pacote candidato como dados. Não executa OLD nem NEW como teste de saúde e não carrega código do candidato em seu processo. As escritas de preparação ficam em um conjunto explícito de objetos da tentativa. A validação de arquivos não é READY. O fluxo normal atual, por executar OLD nessa etapa, **não satisfaz integralmente essa preparação proposta**.

**Primeira fronteira de efeitos:** um eventual `MAY_LAUNCH` protegido precisa preceder a primeira execução possível de código do pacote. Um gate instalado depois, em Main, no construtor ou em Shown, não certifica o período anterior. Não remover nem deslocar o fence existente nesta rodada.

**Testemunho W:** para sustentar recuperação depois de crash, W precisa:
1. vincular instalação/transação/manifesto/geração/fase;
2. resistir à substituição por um estado válido antigo no modelo de ameaça adotado, incluindo nomes e pais relevantes;
3. tornar ausência, leitura falha, identidade divergente e perda de proveniência motivos de bloqueio;
4. ter sua persistência e comportamento após perda do proprietário especificados e ensaiados.

O diário e os fences atuais **não instanciam esse W** contra rollback pelo mesmo usuário. Outro arquivo local, hash encadeado, GUID ou chave acessível ao mesmo principal não é admitido como substituto pela mera estrutura do dado.

**Escopo de despacho:** o coordenador pode afirmar quais chamadas ele próprio realizou. Para usar isso como autorização de OLD, é preciso ainda provar que execuções fora dele não conseguem atingir o domínio de efeitos relevante — por despacho obrigatório ou isolamento efetivo desse domínio. Excluir verbalmente lançamentos externos da definição de “transação” não isola arquivos, registro, brokers ou operações compartilhados.

**Após possibilidade de launch:** conservar `UnknownBlocked` enquanto não houver prova independente do encerramento dos efeitos relevantes. Morte de processo, Job vazio, retorno zero, recibo do próprio GF e observação funcional não satisfazem essa condição.

## 4. Avaliação H1/H2/H3 no envelope atual

| Hipótese | Estado conferido | Resultado do contrato estreito |
| --- | --- | --- |
| H1: história durável protegida contra rollback | O cenário existente `same-user-full-state-rollback-after-new` restaura estado pré-NEW; o reconciliador permanece bloqueado | Delimitar chamadas do coordenador não torna o diário anti-rollback |
| H2: autorização obrigatória para efeitos relevantes | `out-of-band-new-with-old-selected` executa NEW real fora do owner, sem mudar OLD/1, e continua bloqueado | A definição estreita só é suficiente se o domínio de efeitos também estiver isolado; isso não foi estabelecido |
| H3: efeitos exauridos após launch | Ensaios existentes de broker/handoff/worker tardio mantêm bloqueio; a fonte também contém execução de OLD pré-NEW | Proibir novos handoffs na UI não extingue os já possíveis, nem certifica I/O pendente |

**Limite lógico, não uma nova prova Windows:** dadas as capacidades já admitidas de restaurar todo estado local decisório e executar NEW fora do coordenador, duas histórias podem devolver ao reconciliador os mesmos metadados/estado local: (A) NEW nunca executou; (B) NEW executou e o estado decisório foi restaurado. Sem W protegido **e** cobertura do domínio de efeitos, restringir o nome da proposição a “launch L18” não distingue as histórias relevantes para autorizar OLD.

Não foi demonstrada compatibilidade conjunta de H1+H2+H3 com os mecanismos e permissões atuais. **Também não se afirma que todo autoatualizador portátil ou todo desenho sem serviço permanente seja impossível.** Um serviço, por sua vez, não resolveria sozinho H2/H3.

Portabilidade não foi redefinida como “nenhum AppData”: o GF já grava diagnósticos locais. O obstáculo é a falta de autoridade protegida e de delimitação efetiva de efeitos, não a mera existência desses arquivos. Nenhuma identidade privilegiada, política do Windows, serviço, tarefa ou backend adicional foi adotado para preencher essa lacuna.

## 5. Decisão por corte

| Situação | Decisão permitida neste estágio |
| --- | --- |
| Preparação de dados pelo owner original ainda vivo, sem lançamento de produto por esse fluxo | Pode finalizar/cancelar apenas a preparação dentro desse escopo; não é recuperação após crash nem certificado global de no-launch |
| Processo novo lê diário local pré-NEW/OLD/1 | `UnknownBlocked`; snapshot válido não prova a história |
| Futuro W protegido diz “antes de MAY_LAUNCH”, mas despacho/domínio de efeitos não estão provados | `UnknownBlocked`; H1 isolada não supre H2/H3 |
| MAY_LAUNCH persistido e crash antes de criar o processo | `UnknownBlocked`; a janela conservadora continua existindo |
| NEW iniciou, saiu normalmente ou apresentou UI funcional | `UnknownBlocked` até prova independente de efeitos exauridos; READY=false |
| OLD já foi executada como sondagem, ou há handoff/efeito fora do domínio controlado | Não chamar a preparação de “sem efeitos”; impedir nova recuperação por essa justificativa |
| Testemunho perdido, restaurado, inacessível, reapresentado ou sem vínculo | `UnknownBlocked`; não reconstruir autoridade por recibo, PID, HWND ou log |

No código atual, `ReconcileReal` conserva o bloqueio incondicional `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` em `Phase1Recovery.cs.txt:293–298`, antes das rotas históricas de recuperação. **Nenhum desses condicionais futuros foi convertido em autorização de execução.**

## 6. Encerramento desta rodada e critério para retomar implementação

**NO_GO_FOR_AUTO_OLD_RECOVERY_CURRENT_ENVELOPE**, rótulo documental, não novo código de runtime.

Salvar somente este checkpoint e o recibo. Não implementar flag de quarentena no GF, helper, ledger adicional same-user, AppContainer/token restrito sem prova, política de execução, serviço/tarefa ou autorização por logs de terceiros. Não alterar o reconciliador para explorar um retorno atualmente inalcançável.

A próxima mudança técnica só justifica CI se apresentar uma diferença concreta de mecanismo:
- uma autoridade de estado que sobreviva à contraprova de restauração completa;
- despacho obrigatório **ou** isolamento verificável dos recursos relevantes perante lançamento externo;
- bloqueio de efeitos anterior a qualquer execução de produto usada no preparo, cobrindo também OLD;
- um ensaio que possa refutar a nova propriedade, com escopo distinto dos 22 casos já aprovados.

Sem essa diferença, repetir a bateria ou simular um novo “provedor de autoridade” só aumentaria a contagem. **Nenhuma CI foi solicitada.** Os achados LC01–LC03 são rastros de fonte, não novos PASS de Windows nem uma auditoria exaustiva de todas as escritas.

Preservados TUF, B+A, NTFS, C# L18, main, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria e PCs pessoais. Sem WORK, merge, release, deploy ou integração operacional.

**Fase 1 ABERTA · READY=false · G1–G8 BLOCKED 0/8 · última CI: 22/22 PASS_CONTAINMENT, zero recuperações.**
