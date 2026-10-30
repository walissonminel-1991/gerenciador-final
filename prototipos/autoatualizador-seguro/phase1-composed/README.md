# Fase 1 do autoatualizador — GF real em Windows descartável

Missão única em quatro fases. Este diretório é apenas uma ponte de medição entre os protótipos TUF/NTFS L1–L22 e os BYTES de pacotes reais do GF. Não modifica o código operacional do GF, nem cria um atualizador distribuível.

A CI compila o projeto GerenciadorAplicativos.csproj duas vezes com GfBetaProfile=local, variando apenas o metadado Version no MSBuild efêmero. Compilação nenhuma é publicada. O ensaio fica EXCLUSIVAMENTE sob RUNNER_TEMP em GitHub Actions Windows 2022.

O manifesto de todos os EXE/DLL/runtime do build NEW é assinado com as chaves públicas/determinísticas de TESTE já existentes em Stage11; python-tuf 7.0.1 verifica as assinaturas TUF, a referência temporal e os hashes de metadados. Essas chaves existem no repositório e NÃO autorizam atualizações de produção.

O ensaio rejeita modificação in-place de GerenciadorFinal.dll, revalida os bytes completos OLD/NEW e inicia a aplicação GF real com --tray na sessão descartável.

O observador Win32 **externo ao produto** consulta a identidade do executável associada ao PID por QueryFullProcessImageNameW, compara caminho e SHA256 da imagem de disco com o manifesto autenticado e procura a janela principal de título Gerenciador Final pertencente ao PID. Se encontrada, testa a resposta da thread UI por SendMessageTimeout(WM_NULL) e tenta encerramento gracioso por PostMessage(WM_CLOSE), aguardando a saída com limite temporal. Como o `MainForm.Shown` inicia o inventário assíncrono e o `FormClosing` pode cancelar fechamento durante `_busy`, o observador aguarda uma janela limitada de 10 segundos após encontrar o HWND e admite até dois pedidos `WM_CLOSE` separados por espera limitada. Isso não ignora o cancelamento do GF: se não houver saída normal após os pedidos, registra o bloqueio e mata exclusivamente a árvore criada no runner como **limpeza forçada, não como prova de shutdown**.

Além da janela, `observe_read_only_ui_controls` enumera exclusivamente os controles descendentes que pertencem ao PID do GF e retorna **somente booleanos** para textos estáticos esperados: cabeçalho do GF, campo de pesquisa, resumo de Apps com quantidade e botão habilitado `Atualizar e verificar`. Não coleta nomes de aplicativos, não interage, não registra textos pessoais e nunca executa manutenção. Esse resultado mede uma pequena fatia funcional da interface, não valida motores, rede ou recuperação.

**NÃO declarar READY**: HWND, WM_NULL, PID, retorno 0 e a presença desses controles demonstram no máximo inicialização da interface, thread de mensagens, exibição de estado do inventário e encerramento, não saúde integral dos motores do GF nem consentimento real em desktop de usuário. Identidade do executável observada via caminho+hash no disco não é atestação dos bytes carregados em memória e tem TOCTOU. GitHub Actions pode rodar na sessão 0 sem acesso ao desktop interativo. A CI deve explicitar session ID, acesso a desktop, presença de HWND, resposta WM_NULL, solicitação WM_CLOSE, saída graciosa e uso de fallback. Mesmo com qualquer sinal positivo, **NEW não recebe READY**. Após possível execução de NEW, o fence mantém a seleção histórica bloqueada, sem rollback cego. OLD só pode voltar a executar quando a reconciliação comprovar que NEW ainda não começou a produzir efeitos.

Limites intencionais: ainda não há contrato operacional autenticado de encerramento completo/READY independente, recuperação de I/O pendente, confiança de chaves de produção ou restauração após falha física. Nenhuma modificação foi feita no Program.cs ou MainForm, nem na BETA distribuída, main, PR #65, backend, credenciais ou PCs pessoais. G1–G8 permanecem BLOCKED (0/8).

Critério para encerrar a Fase 1: demonstrar, no MESMO marco, saída/identidade/saúde verificável do GF real, autenticação do pacote autorizada para laboratório e recuperação conservadora, sem usar recibo local como autoridade suficiente. Não avançar para Fase 2 até que a evidência exista ou um bloqueio objetivo seja registrado.

### Contrato mínimo de saúde SOMENTE LEITURA, não READY

O observador em Win32, sempre restrito ao HWND/identidade do próprio GF e ao runner descartável, encontra a caixa real de pesquisa no mesmo contêiner do rótulo `Pesquisar:`. Insere um filtro aleatório que não pode corresponder a um aplicativo, observa a mensagem de lista vazia causada pelo `TextChanged → RenderPrograms` REAL e restaura o filtro. Esta verificação testa um caminho funcional de leitura/inventário além de PID, HWND, mensagem ou recibo. **Não autentica o estado dos motores, nenhuma operação de atualização, efeitos tardios, bytes carregados nem READY operacional do autoatualizador**. Se o filtro não responder ou a reversão não puder ser observada, falha a CI e NEW não é autorizada.

## Fase 1 — seleção real exclusivamente pelo coordenador C# (experimento)

O L18Coordinator C# mantém o ÚNICO seletor efetivo do pacote GF real,
`src/l18-real-active.fixture.json`, sob a mesma exclusividade do diário
L18. As transições são OLD geração 1, NEW geração 2, OLD geração 3, com
identidade de instalação, transação e SHA-256 do manifesto TUF de laboratório.
O seletor L14 permanece apenas no modelo de fixtures; não determina o GF real.

O processo Python apenas lê o seletor do C#, autentica os arquivos e observa
os processos reais, sem escrever o seletor. O antigo caminho cooperativo
OLD_DONE/NEW_DONE foi removido da execução e do código experimental. Há apenas
uma entrada C# para testes do pacote GF real.

O ciclo normal usa o mesmo contexto durável, Job Object, pins NTFS e fences
que a recuperação. O coordenador mede OLD, seleciona NEW e mede NEW com
barreira persistente anterior ao lançamento. O ensaio verifica manifesto,
diário, hashes, canário, funções read-only e encerramento, mas **mantém NEW
bloqueada após sua execução** por ausência de prova independente de término
dos efeitos. Nos cenários anteriores ao lançamento, a recuperação de OLD
depende de evidências suficientes.
Isso NÃO é atualização operacional; não prova efeitos tardios, bytes
carregados na memória, recuperação após perda de energia nem exclusão de
ataque do mesmo usuário.

READY operacional exigirá um contrato autenticado de saúde do aplicativo,
vinculado à execução, que não se reduza a PID, HWND, log ou recibo da mesma
identidade local. A pesquisa de inventário é evidência funcional limitada;
a decisão final continua UNKNOWN, e G1–G8 permanecem BLOCKED (0/8).

### Critérios de READY operacional — exigência separada da seleção

O executável do ensaio é escolhido pelo valor persistido no seletor
`l18-real-active.fixture.json` do coordenador C#, e não pelo ramo
`if old/if new` do observador. Nos casos recuperáveis anteriores ao lançamento de NEW, o laboratório
**reinicia** os bytes reais OLD e testa novamente o filtro funcional. Isso mede
recuperação executável de um ciclo controlado, não resistência a falta de energia.

READY só poderá ser aceito se houver, de forma conjunta e verificável:
(i) autoridade TUF apropriada, vínculo da transação e antirrollback;
(ii) prova dos bytes efetivamente executados e isolamento da escolha até
o startup sem TOCTOU; (iii) verificações externas de funções efetivas do GF
além de PID/HWND/recibo local; (iv) comprovação independente de encerramento
dos efeitos pendentes e I/O tardio; (v) recuperação idempotente em crash,
restart e falha de energia preservando o OLD e o diário. Estas evidências
precisam vir de origens e controles que o próprio processo candidato
não consiga simplesmente falsificar.

**Qualquer requisito ausente = UNKNOWN/BLOCKED**, nunca READY por inferência.
Este ensaio não satisfaz o contrato completo, não abre G1–G8 e não conclui
a Fase 1.

### Convergência do fluxo normal — Fase 1, implementação experimental

O modo normal passou para o mesmo `RealOwner` e `ReconcileReal` dos cenários
de interrupção, sem o antigo `--run` cooperativo. O contexto, manifesto
TUF/3 OLD+NEW, pinagem, Job Objects e fences são idênticos. OLD é observada
antes de selecionar NEW; ambos os lançamentos são precedidos pelos seus
respectivos fences, e a saída normal do processo proprietário não promove
READY. Um novo processo reconciliador deve retornar `UnknownBlocked` após
NEW, mantendo sua seleção histórica como evidência até surgir prova suficiente.
Não declarar rollback pós-NEW seguro. A CI exige um caso normal sem injeção
de crash, os cinco casos pré-NEW recuperáveis e os seis bloqueios adversariais.
O limite do supervisor persistente e a ausência de testemunha independente
de I/O tardio continuam bloqueios objetivos. Nenhum fluxo operacional.
