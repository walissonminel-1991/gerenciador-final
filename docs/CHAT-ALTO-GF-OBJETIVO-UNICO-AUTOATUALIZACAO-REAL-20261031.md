# GERENCIADOR FINAL — LINHA ÚNICA: AUTOATUALIZAÇÃO REAL

**Data:** 2026-10-31. **Prioridade absoluta:** fazer o GF já instalado detectar, obter, autenticar, aplicar e iniciar sua própria atualização com segurança. Este documento fixa a rota de execução; não é evidência de produto pronto.

## Compromisso de escopo

ÚNICO resultado de aceite: em Windows descartável, **GF OLD 5.13.11.65, aberto normalmente**, detecta uma atualização autorizada para **GF NEW 5.13.11.66**, apresenta/obtém consentimento quando necessário, baixa o pacote, verifica os metadados e **todos os bytes relevantes** antes da instalação, encerra o processo OLD corretamente, aplica a versão NEW por coordenador externo, inicia o GF instalado atualizado e verifica a versão e um contrato funcional mínimo de interface. Tudo deve funcionar **sem GitHub Actions, GH_TOKEN, repositório privado, Python instalado pelo usuário ou intervenção manual nos arquivos**, usando uma build privada para teste, sem promover release público.

Não marcar READY apenas por sucesso de downloader, PID, HWND, recibo ou CI isolada. Não mascarar falhas em HOLD como atualização bem-sucedida. Em falha de segurança ou histórico inconclusivo: bloquear ações perigosas, preservar os artefatos necessários, oferecer orientação acessível; não presumir rollback automático seguro.

## Diagnóstico factual de partida

- Branch de trabalho **exclusiva**: `experimento-g5-ipc-chat-alto-20261029`. HEAD documental de partida `9d83411c25be3cadc7dad63d70c9c017da92e5f0`.
- O ciclo experimental OLD→NEW já tem prova Windows real no CI [41226223387](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223387) e validação de composição [41226223439](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223439), ambos SUCCESS. Essas provas **não** demonstram integração ao aplicativo distribuído.
- Bloqueio funcional principal comprovado: `GerenciadorAplicativos.csproj` compila `GfOldDraftAcquire.cs.txt` somente com `GfE2eLab=true`; `Program.cs` inicia o cliente exclusivamente dentro de `#if GF_E2E_LAB`; o próprio workflow `gf-github-draft-e2e.yml` confirma sua ausência da build normal.
- `GfOldDraftAcquire.Run` exige `GITHUB_ACTIONS=true`, `RUNNER_TEMP`, `GH_TOKEN` e OLD fixa .65. A Draft 429695537 é privada e os ativos/manifesto são de laboratório (`operational_installable=false`), não um canal real para usuários sem token.
- Logo, **a próxima ação é integração operacional restrita, não nova auditoria abstrata sobre NTFS/TOCTOU ou memória**.

## Sequência obrigatória de execução

### ETAPA 1 — Ligar à compilação NORMAL privada de teste

Entregar um caminho explícito de atualização que seja compilado pelo GF normal de teste **sem** `GfE2eLab` e sem os guards exclusivos da CI. Reutilizar o que for reutilizável do cliente e do coordenador existentes; não duplicar launchers nem inventar arquitetura B+C. Não ativar a atualização para usuários do produto normal/público até que as demais etapas sejam aprovadas. Definir as interfaces mínimas entre UI, aquisição e coordenador existente; separar mecanismos de teste de verificações de segurança reais.

**Prova de saída:** build privada de Windows inicia a consulta de atualização a partir da interface normal e alcança o cliente sem variáveis CI; se o canal de testes estiver indisponível, retorna erro seguro e legível, sem tentar instalar.

### ETAPA 2 — Detecção, download e autenticação para usuário SEM TOKEN

Usar um canal de publicação acessível sem credenciais pessoais de GitHub, inicialmente simulado/servido de forma controlada no Windows descartável até autorização para publicar. Não promover nem alterar a Draft privada. Preservar a cadeia TUF e a verificação do pacote com vínculo entre metadados assinados, hash e bytes efetivamente instaláveis, sem aceitar metadados de teste como produção. Usar artefatos separados e manifestos autorizados para a nova build privada; se autenticação não estiver disponível, BLOQUEAR a instalação.

**Prova de saída:** build privada detecta uma `.66` autorizada, baixa bytes e recusa, sem execução, versão adulterada ou metadados inválidos.

### ETAPA 3 — Instalar NEW e iniciar no mesmo fluxo

Reutilizar transação/controle de processos já demonstrados, coordenação fora do processo que será substituído, fechamento legítimo do OLD, verificação do NEW instalado e abertura do GF. Não bloquear o app em loops ou exigir que usuário substitua arquivos à mão. Não implementar rollback/retry/limpeza automáticos onde a história for inconclusiva; HOLD mantém evidência e bloqueia sequência insegura.

**Prova de saída:** execução real no Windows descartável, com versão da build instalada e novo processo e UI observados; falha controlada preserva dados e não instala bytes não autenticados.

### ETAPA 4 — Aceitação de ponta a ponta (única referência de conclusão)

Partir de GF `5.13.11.65` aberto normalmente, sem `--tray` ou `ShowWindow` forçado; consulta, confirmação, download, assinatura/hashes, instalação e abertura do GF `5.13.11.66` no caminho real. Observação funcional independente mínima na UI atualizada, sem inferir READY de um recibo ou do PID. Também comprovar a recusa de adulteração de pacote e contenção segura na falha relevante. Não publicar/mergear neste teste.

**Prova de saída:** um relatório de aceitação com versão OLD, versão NEW, origem autenticada, caminho/versão instalada, execução/saúde observada, sucesso/falha e riscos remanescentes; artefatos e commit reproduzíveis.

## Regra de antidesvio

1. Próxima tarefa sempre corresponde à **primeira etapa ainda sem prova de saída**; etapas são sequenciais.
2. **Nenhum novo estudo** sobre pin NTFS, memória, WDDM, TOCTOU geral, protocolos auxiliares, arquitetura nova, UI lateral, drivers ou telemetria; exceção SOMENTE quando um teste da etapa atual demonstrar **defeito concreto impeditivo ou vulnerabilidade explorável**, com código, observação e impacto explícitos.
3. Não autorizar um novo lote só por curiosidade técnica. Correção mínima do defeito → regressão dirigida → retornar à etapa interrompida.
4. Uma CI verde não é o objetivo. Não reexecutar provas já aprovadas sem efeito sobre o percurso.
5. A cada resposta informar: **etapa atual / bloqueio concreto / mudança funcional / evidência de saída / próxima ação da mesma etapa**.
6. Registrar commits técnicos e checkpoints concisos apenas quando necessário para continuidade; não criar um relatório por hipótese.
7. Não executar WORK sem necessidade crítica justificada por falha concreta e autorização; não abrir um segundo plano.

## Restrições não negociáveis

Preservar `main`, PR #65 Draft, BETA .66, Draft 429695537, ZIP 694233137, manifesto 694233139, Cloudflare, PCs pessoais, H1/H2/H3, TUF, proteção NTFS/TOCTOU existente e dados pessoais. **Sem merge, deploy, publicação, release público, alteração do canal de usuário, recuperação/rollback/limpeza/retry não autorizados.** O trabalho acontece somente na branch experimental e em Windows descartável. Não remover verificações, mudar share mode, contornar assinatura ou divulgar token.

## Estado após fixação desta linha

**ETAPA ATUAL: 1 (integração à compilação normal privada). NÃO CONCLUÍDA.**
**READY=false.**
**Próxima tarefa autorizada dentro deste plano:** mapear precisamente o menor ponto de integração com o código existente, implementar a ligação da atualização a uma build privada de teste e executar prova de início da consulta sem `GfE2eLab`, sem credenciais de usuário, sem instalar pacote inseguro.
