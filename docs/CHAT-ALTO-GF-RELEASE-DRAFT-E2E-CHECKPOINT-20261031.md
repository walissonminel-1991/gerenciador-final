# CHAT ALTO — GERENCIADOR FINAL — origem GitHub Draft e pacote GF real (31/10/2026)

## 1. Resultado verificado

- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- SHA técnico executado: `c5057bbbaf1d264893c57ee524cf54b25ee0410a`.
- Windows GitHub Actions [CI 41164213329](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41164213329), job `124428146365`, `completed/success`.
- Release **Draft** ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, cinco assets; nenhum release público autorizado.
- Pacote executável real NEW `gf-e2e-new-win-x64.zip`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- TUF laboratório: `GF-LAB-REAL-PACKAGE-MANIFEST.json` SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`. Também `targets.json`, `snapshot.json`, `timestamp.json`.
- Builds reais: OLD `5.13.11.65` e NEW `5.13.11.66`, ambos self-contained win-x64 e com DLLs de versão efetivamente distintas.
- Manifestos contêm 284 arquivos por build; o arquivo NEW servido por GitHub foi baixado novamente e sua extração validada sob o manifesto assinado antes de ser entregue ao coordenador.
- O mesmo `Phase1RecoveryTests.Entry --real-suite` recebeu a pasta OLD e a **NEW extraída da Release Draft**, reutilizando o seletor único C# L18 e o observador real da interface.
- Log `GF_E2E_REAL_L18_FLOW old=5.13.11.65 new=5.13.11.66 source=GITHUB_DRAFT_DOWNLOAD scenario=normal result=PASS_CONTAINMENT hold=UnknownBlocked ready=false`.
- Relatório `GF_PHASE1_RECOVERY_REPORT`: exatamente 29 casos de contenção aprovados, `failed=0`. O caso normal registrou `normal_owner_completed_without_crash=true`, `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`, `reconciler_new_process=true`, `outcome=UnknownBlocked`, `replay=UnknownBlocked` e `operational_ready=false`.
- Rejeições antes de executar o pacote alterado: bit adulterado `rejected=true`; arquivo truncado (simulando transferência interrompida) `rejected=true`; nenhuma recuperação automática.

## 2. Limites importantes — proibição de inferir READY

1. A descoberta da Draft e o download foram feitos pelo **orquestrador de homologação GitHub CLI**, não pelo código de startup do GF OLD. Ainda falta conectar a detecção/download no próprio GF, sem trazer credenciais da CI ao usuário final. Portanto **autoatualização autônoma de uma instalação real não está demonstrada**, embora a cadeia experimental com pacote real esteja.
2. TUF usa seeds/chaves **públicas de laboratório**, sem raiz operacional. Não considerar essas assinaturas confiáveis para usuários reais.
3. Validação de interface limita-se ao contrato read-only existente: PID/caminho/hashes de disco, janela responsiva, controles e busca. Um testemunho positivo de item no controle customizado DarkProgramList permanece ausente; `positive-trial-inventory` passa como `PASS_CONTAINMENT`, não `PASS_EXPERIMENTAL_ACTIVATION` ou READY.
4. O teste de transferência interrompida é uma **cópia truncada após download**; cancelamento real durante fluxo HTTPS não foi demonstrado. Falha de encerramento graciosamente injetada no mesmo ciclo GitHub→OLD/NEW também não foi demonstrada.
5. A primeira CI executou, após autenticação, os dois testes negativos **antes** da matriz OLD→NEW, embora a ordem solicitada fosse positiva seguida de negativos. O workflow foi posteriormente reorganizado em código para essa ordem, sem alegar que uma segunda execução com a ordem revisada já ocorreu.
6. H1/H2/H3, same-user TOCTOU, bytes carregados em memória, brokers/I/O tardio, queda de energia, antirrollback externo e recuperação fria não estão provados. `UnknownBlocked` e HOLD retêm evidências; NÃO autorizar rollback, limpeza nem nova tentativa automática.
7. A CI de homologação original forneceu `GH_TOKEN` no ambiente do job inteiro. O workflow subsequente foi ajustado para escopar o token às etapas de publicação e download; **este ajuste posterior não tem CI própria**, portanto não substituir evidência da execução original.
8. A Release permanece Draft; nenhum merge, publicação, deploy, transmissão de telemetria, alteração de main, PR #65, BETA distribuída, Cloudflare ou execução em PC pessoal.

## 3. Diretriz de continuidade

A missão única só termina quando o GF OLD **pela sua própria lógica de teste** detectar o pacote remoto, solicitar download autenticado, controlar a transação e observar o NEW sem intervenção do orquestrador externo na decisão. Preservar L18, Python-TUF, B+A, HOLD, as mesmas fronteiras e o Draft já criado; não criar outro seletor nem iniciar outras fases. O workflow foi tornado manual após a prova inicial para evitar novas Releases Draft acidentais ao realizar commits na branch.
