# CHAT ALTO — GERENCIADOR FINAL — startup normal experimental inicia download de Draft

Data: 2026-10-31. **Missão única:** GF OLD instalado detecta, baixa, autentica e aplica GF NEW em Windows descartável, com contenção de falhas.

## Escopo e SHA

- Repositório: `walissonminel-1991/gerenciador-final`.
- **Branch exclusiva:** `experimento-g5-ipc-chat-alto-20261029`.
- **Commit técnico deste avanço:** `791e7a167bc27c63b4895e3496001e8488bd2dae`.
- Release experimental existente: ID `429695537`, `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`.
- ZIP NEW original: asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto TUF de laboratório original: asset ID `694233139`, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.

## Ponto de controle A — cadeia .NET→TUF→L18 fechada ANTES da alteração de startup

[CI Windows `41167267745`](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41167267745), job `124437422482`, commit `076e68ba15abe801b57f2185b73424005d59e093`, `completed/success`.

Comprova:
1. GF OLD real `5.13.11.65` iniciou `--gf-e2e-draft-acquire` e baixou por seu próprio `.NET HttpClient` os cinco assets do GitHub, com token efêmero da CI.
2. TUF de TESTE validou o manifesto e os 284 arquivos NEW `5.13.11.66` antes de execução; o OLD de laboratório foi vinculado independentemente pelo próprio L18.
3. O mesmo C# L18 executou OLD e NEW do pacote baixado no runner e aprovou `29/29` casos (todos `PASS_CONTAINMENT`), `failed=0`; o caso normal terminou com `UnknownBlocked`, `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`.
4. Os dois negativos (`modified` e `interrupted` [cópia ZIP truncada]) foram rejeitados, sem executar o pacote adulterado/truncado.
5. `operational_ready=false`, `phase1_complete=false`. A confirmação positiva do item customizado na UI não foi provada.

## Ponto de controle B — inicialização normal do aplicativo experimental

Commit `791e7a167bc27c63b4895e3496001e8488bd2dae`:
- Modificou somente `Program.cs` e `.github/workflows/gf-github-draft-e2e.yml`.
- Adicionou `#if GF_E2E_LAB` a `Program.Main` para ligar um observador de `MainForm.Shown` ao cliente de download de teste `GfOldDraftAcquire.Run`. Precisa da flag `GF_E2E_STARTUP_DRAFT_PROBE=1`, de `GITHUB_ACTIONS=true`, repositório exato e `RUNNER_TEMP`; nenhuma entrada nova é compilada nas edições normais.
- Preserva o `MainForm`, o fluxo BETA LOCAL de consultas desativadas, UI padrão, o mesmo cliente de download, o mesmo TUF e o mesmo coordenador L18. Não cria seletor ou arquitetura nova.
- A CI inicia `GerenciadorFinal.exe --tray` **sem** o subcomando `--gf-e2e-draft-acquire`, observa um recibo do evento real `MainForm.Shown`, exige o relatório de download .NET, encerra somente seu próprio processo de teste e prossegue para Python-TUF e L18. Um encerramento forçado de limpeza não é prova de fechamento gracioso.

**CI de startup:** [`41168060868`](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41168060868), job `124439824746`. No momento de elaborar este checkpoint:
- `Startup real MainForm.Shown do GF OLD inicia download autonomo de Draft` = **SUCCESS**.
- `Revalidar TUF da Draft NEW baixada pelo GF e extrair em local isolado` = **SUCCESS**.
- `Mesmo coordenador C# L18 executa OLD e NEW vindos do GitHub` = **IN_PROGRESS**.
- `Negativos de pacote real adulterado e transferencia incompleta` = **PENDING**.
- Portanto a CI de startup **ainda não** está concluída, apesar da prova de detecção automática estar aprovada. Consultar resultado final e logs antes de avançar.

## Limites invioláveis

- Somente Windows descartável do GitHub Actions, sem instalações em PC FORTE/FRACO nem fora do runner.
- Nenhum release público, novo rascunho, deploy, merge, alteração de `main`, PR #65, BETA .66 ou Cloudflare. Nenhuma promoção operacional.
- A Draft só é acessível ao usuário autorizado; `GH_TOKEN` da CI não pode ser entregue ao GF distribuído.
- As chaves TUF são públicas e exclusivamente laboratoriais; H1/H2/H3 e same-user TOCTOU não comprovados.
- `HOLD` / `UnknownBlocked` é contenção, não prova de recuperação, autorização de rollback, limpeza ou nova tentativa.
- A prova atual é apenas do início automático da **consulta e do download** pela interface experimental; a chamada ao coordenador L18 segue orquestrada pela CI. Ainda **não** há GF autossuficiente que aplica sua atualização de ponta a ponta sem orquestração externa.
- Nenhuma nova arquitetura, substituição de seletor ou fase paralela. WORK só deve ser chamado quando houver candidato E2E ou bloqueio crítico fundamentado.

## Ação exata para o próximo CHAT ALTO

1. Verificar `41168060868` — status final, logs de L18, relatório `29/29`, negativos e preservação de OLD/NEW.
2. Se CI SUCCESS: registrar resultado final e avançar **somente** na conexão entre a rotina de startup/download do GF OLD e o coordenador C# L18, num único Windows descartável, sem promoção de READY. Distinguir claramente o que foi iniciado pelo executável e o que ainda dependeu do runner. Evitar repetir testes já aprovados sem evidência nova.
3. Se falha: corrigir só o defeito do log, mantendo as restrições.
4. Continuar oferecendo botões diretos para `Continuar missão` e `Abrir CI`, sem exigir que o usuário reenvie contexto.

Este checkpoint registra apenas evidências comprovadas ou estados expressamente pendentes.
