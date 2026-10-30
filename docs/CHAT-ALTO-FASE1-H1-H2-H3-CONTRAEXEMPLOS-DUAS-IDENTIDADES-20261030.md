# GERENCIADOR FINAL — Fase 1 — Duas identidades: contraexemplos H1/H2/H3

**30/10/2026 — estudo de refutação e critérios de prova; somente documentação; nenhum helper privilegiado implementado.**

## 1. Estado e âmbito

- Repositório `walissonminel-1991/gerenciador-final`; única branch para escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD antes da investigação: `110b2d420ea8d5f0a4df6a20e5f24de3afa1eeee` (conferido diretamente).
- Último commit técnico validado: `f250be5a32a9706551051da1b0b082f57b383d15`.
- CI Windows [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779), job `124123487779`, `completed/success`, 20/20 `PASS_CONTAINMENT`, zero recuperação automática. Contraexemplo anterior em [41071494214](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41071494214) chegou a aceitar `OldRecoveredNoReady` após NEW executar e rollback do estado local; o runner terminou com `failure` em assert posterior.
- PR #65 confirmada `open`, `Draft`, `merged=false`. Sem mudança nela.
- Código experimental existente `ReconcileReal` permanece com `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` => `UnknownBlocked`. Não tocar `B+A`, TUF, NTFS, C# L18, seletor/fences ou código distribuído.

**Modelo de ameaça mínimo desta análise:** processo arbitrário controlado pelo usuário **U** que executa GF como **conta padrão**; conta administrativa **A** tem SID distinto e credenciais que o processo U não possui. U pode ler/copiar/lançar executáveis a que tenha permissão, alterar todo seu perfil e objetos que possui. A e SYSTEM não são hostis. **Não estender esta garantia ao mesmo usuário administrador com UAC**, a roubo das credenciais A ou a administrador/SYSTEM hostil.

## 2. Três proposições independentes

**H1 — Integridade do ledger contra rollback por U:** U não modifica, apaga, substitui, renomeia nem retrocede um registro de transações gravado sob propriedade exclusiva A, inclusive após desaparecimento do helper. H1 não implica ausência de launch, só integridade de estado em escopo delimitado.

**H2 — Despacho exclusivo:** para toda execução de NEW relevante à afirmação de segurança, o SO obriga passagem por autorização persistente anterior ao primeiro efeito possível. Não basta instruir a UI ou o updater a chamar o helper; U pode ignorar esse código e chamar `CreateProcess` de forma independente se a imagem continuar executável. Tampouco bastam hash da imagem, dono do processo, elevado UAC, mutex ou flag `MAY_LAUNCH`.

**H3 — Efeitos exauridos:** após `MAY_LAUNCH`, nenhuma autorização de retorno a OLD pode basear-se apenas no término da instância NEW/Job ou recibo local. É preciso delimitação de I/O e efeitos de processos delegados fora do Job, mesmo após perda de supervisor. O contrato READY é separado.

**Teorema de indistinguibilidade aplicável:** no modelo em que a decisão de lançamento está sob controle de U, um ledger íntegro de A *não observa* execuções de NEW feitas por U sem passar por A. Os históricos `no launch` e `launch direto não registrado` podem produzir a mesma leitura do ledger de A; um reconciliador que autorize OLD apenas dessa leitura pode aceitar falsamente o segundo. Isto é um argumento arquitetural, NÃO uma prova Windows do novo desenho.

## 3. Ataques e verificações de refutação (ainda NÃO executados)

| Código | Proposição | Preparação lógica | Ação adversarial de U | Aceite estrito / conclusão |
| --- | --- | --- | --- | --- |
| D01 | H1 | A cria arquivo novo em diretório NTFS controlado por A, descritor do owner verificado, sem direitos de U no arquivo **e nos pais** | U tenta `WRITE`, `DELETE`, `WRITE_DAC`, troca atômica/rename e recriação do prefixo do ledger | Todas negadas com evidência por handles e SID; se alguma passar H1 **refutada**. Não tocar diretórios reais do usuário |
| D02 | H1 | Ledger de A íntegro e autorização antiga | U restaura snapshot completo do perfil, registro HKCU, seletores/fences de fixture e toda memória persistida em sua conta | Estado de A não retrocede; retorno `UnknownBlocked` para histórico ambíguo. É H1 restrita, NÃO H2 |
| D03 | H1 | A cria ledger mas diretório-pai ou nome do arquivo ainda pertence a U | U renomeia/substitui diretório, troca reparse point ou nega persistência; tenta influenciar o próximo caminho aberto pelo helper | H1 **não reivindicável** enquanto objetos-pai e namespace não forem protegidos e revalidados |
| D04 | H2 | Mesmo ledger protegido de A, NEW continua executável por U no volume portátil | U copia NEW para diretório próprio ou executa `GerenciadorFinal.exe` diretamente, sem IPC/helper | Se a NEW produzir qualquer efeito relevante sem `MAY_LAUNCH` de A, H2 **refutada** mesmo com H1 intacta |
| D05 | H2 | Helper é invocado cooperativamente pelo GF, sem política do SO | U ignora CLI/IPC e inicia a mesma imagem por `CreateProcess`, ShellExecute ou outra via permitida | H2 **refutada** se não houver mecanismo SO de obrigatoriedade aplicável ao escopo exato |
| D06 | H2/H1 | Imagem ou dependência de helper elevada vem de pasta gravável por U | U substitui executável, DLL, argumento, reparse ou alvo entre aprovação UAC e abertura | Nenhum helper privilegiado pode ser liberado antes de provar os handles, código e namespace protegidos; não demonstrado |
| D07 | H2 | Binário marcado com integridade alta sem regra de execução explícita | U tenta iniciar imagem com token de integridade média | Não supor execução bloqueada: política MIC padrão contém `NO_WRITE_UP`, e `NO_EXECUTE_UP` é opção distinta. Teste do token/política é obrigatório |
| D08 | H3 | NEW autorizada e encerrada; os dois Jobs esvaziaram | Processo delegado **fora** do Job ainda altera fixture inerte após recibo de término | `UnknownBlocked`; evidência anterior `external-broker` já refutou Job vazio como atestado universal |
| D09 | H1/H2/H3 | Helper/proteção preparados, `MAY_LAUNCH` gravado antes de `CreateProcess` | Matar helper na janela entre gravação e criação; matar depois de criar; perder supervisor; reiniciar processo de consulta | Sempre `UnknownBlocked` se houver possibilidade de efeito; não inferir no-launch de recibo ausente |
| D10 | H1/H2 | U não tem credenciais de A; UAC solicitado | U cancela credenciais, credencial inválida ou helper indisponível | Bloquear operação de atualização; GF não pode pedir senha novamente indefinidamente nem tentar contornar elevação |
| D11 | Compatibilidade | Volume exFAT/FAT32 vs NTFS, portable sem instalação | Mover pasta portátil para outra unidade | Não depender de ACLs sobre o pacote exFAT/FAT32; qualquer ledger de A no NTFS do sistema deixa estado fora da pasta portátil |
| D12 | Fronteira | Usuário administrador com token filtrado, mesmo SID | Mesmo titular aprova UAC ou usa capacidade administrativa | Não declarar proteção contra o próprio administrador hostil; requer modelo distinto de D01–D11 |

**Importante:** D01–D12 são **roteiros de contraprova e aceite**, não casos novos executados, nem 32/32 na CI. Não alterar a contagem verificada **20/20**.

## 4. Contraexemplo decisivo antes de gastar CI

**Estado inicial**: A possui ledger intacto com `NO_LAUNCH_YET` e o executável NEW está num diretório que U pode executar. O helper A permanece desligado.

1. U executa NEW diretamente ou uma cópia permitida pelo SO, sem chamar helper A.
2. NEW realiza ao menos um efeito observado no laboratório.
3. U elimina/retrocede somente o seu próprio diário e seletor; não toca ledger A.
4. O novo reconciliador consulta A, recebe registro íntegro de `NO_LAUNCH_YET` e verifica bytes assinados.

Se interpretar o ledger A como prova global de que NEW nunca executou, o reconciliador aceita um histórico falso. Logo, **H1 verdadeira + H2 falsa não é autoridade global de no-launch.** A contraprova permanece válida mesmo quando A é totalmente honesto. Para refutá-la, o proponente precisa apresentar **enforcement do SO** ou restringir formalmente a afirmação a *lançamentos efetuados pelo próprio updater*, sem extrapolar para todo lançamento de NEW e seus efeitos.

Uma **política de execução do SO** poderia modificar H2 sob administração controlada, mas não é precondição aceitável silenciosa para o GF gratuito, portátil e voltado a usuários leigos. A política pode ter regras de exceção/cópia/host; a garantia exigiria testes reais de bypass e rollback e decisão expressa do mantenedor para qualquer alteração no Windows.

## 5. Particularidades do Windows que invalidam atalhos

- UAC: usuário padrão fornece **credenciais** de outra conta administrativa; administrador em modo de aprovação normalmente fornece **consentimento** para seu próprio SID/token elevado. Não confundir os modelos. Fonte: [How UAC Works](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/how-it-works).
- Owner de objeto tem `WRITE_DAC` implícito. H1 não pode residir no mesmo objeto de que U é owner; validar também direitos de substituição no parent. Fonte: [Owner of a New Object](https://learn.microsoft.com/en-us/windows/win32/secauthz/owner-of-a-new-object).
- MIC: política `NO_WRITE_UP` padrão não equivale a `NO_EXECUTE_UP`; label alto sozinho não transforma a imagem em dispatcher exclusivo. Fonte: [Mandatory Integrity Control](https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control).
- Named Pipe: DACL padrão não é contrato suficiente; servidor elevado precisaria descriptor restrito, `PIPE_REJECT_REMOTE_CLIENTS`, autenticação/impersonation adequada, validação de SID e de operações, e prevenção de replay/confused deputy. Fonte: [Named Pipe Security](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights) e [CreateNamedPipe](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-createnamedpipew).
- NTFS possui ACL/owner, exFAT/FAT32 não. Fonte: [File System Functionality Comparison](https://learn.microsoft.com/en-us/windows/win32/fileio/filesystem-functionality-comparison).
- Task Scheduler: dono/autor da tarefa normalmente pode atualizar/excluir; usar outra identidade exige regras de configuração/ACL, além de persistência administrativa; nada disso impõe H2. Fonte: [Security Contexts for Tasks](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks).

## 6. Fronteira de segurança, portabilidade e custo

H1 poderia ser comprovada apenas **sob precondições estritas**: conta padrão sem credenciais de A, A protege o próprio ledger e toda hierarquia de caminhos no NTFS do sistema, direito de alteração restrito, recuperação e owner explicitamente revisados, e ausência de admin/SYSTEM hostil. Mesmo nesse caso:
- registros de A fora da pasta portátil persistem após encerramento/remoção do GF;
- provisionar A, fazer UAC e limpar órfãos aumenta suporte e complexidade;
- o modelo não atende automaticamente a computadores de único usuário administrador;
- a imagem NEW continua executável por outros meios se não houver enforcement adicional;
- H3 permanece sem prova.

**Não recomendar mudar permissões de diretórios pessoais, ativar App Control/WDAC, criar serviços/tarefas, instalar outro usuário, armazenar senha administrativa, habilitar telemetria ou pedir UAC no PC do mantenedor.**

## 7. Decisão técnica e próximo gatilho de implementação

**Nesta rodada H1 é uma possibilidade condicional, não verificada; H2 é refutada para o desenho de helper cooperativo sem enforcement; H3 continua não demonstrada.** A combinação não autoriza tirar `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`.

Próximo ganho de segurança só justificaria CI *se* o projeto primeiro definir alcance do enunciado `no-launch` e apresentar prova de que o dispatcher é obrigatório nesse alcance. Se a precondição H2 não existir, não gastar a CI com a mera demonstração de que uma conta padrão não escreve um arquivo administrativo — isso não altera a decisão de recuperação. Se H2 for demonstrável em projeto alternativo, qualquer protótipo continuará exclusivo de Windows descartável e deverá abordar H3, o gap `MAY_LAUNCH`/CreateProcess, reinício, perda de supervisor, TOCTOU, rollback, artefatos e cleanup.

**Nenhuma implementação privilegiada. Nenhuma nova CI. Nenhuma recuperação reabilitada.** Preservar `main`, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria, PCs pessoais, C# L18, B+A, TUF, NTFS, fences/seletor. **Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8.**
