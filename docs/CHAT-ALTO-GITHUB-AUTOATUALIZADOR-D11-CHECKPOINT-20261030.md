# CHAT ALTO — GERENCIADOR FINAL — D1.1: D0 vinculada ao TUF persistente

**Data:** 30/10/2026 no Brasil; CI concluída em 31/10/2026 UTC. **Natureza:** laboratório de confiança, não atualização funcional do GF.

## 1. Estado verificável

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `0e09d6435f2a7c8f07ddc88925af2fb4602faded`.
- **SHA técnico D1.1 validado:** `723e711ce51f7761a183c227649d1c026fda110c`.
- [CI Windows 41150329284](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150329284), job `124385073215`, tentativa 1, `completed/success`, `head_sha=723e711ce51f7761a183c227649d1c026fda110c`.
- [D0 aprovado: 41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551), 25 testes; [D1 aprovado: 41148601868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148601868), 33 adversariais. Estas aprovações são de laboratórios distintos, não homologação integrada de update operacional.

## 2. Integração mínima implementada

Novo diretório `prototipos/autoatualizador-seguro/github-trust-lab/`:

- `GF.D11.D0Probe.csproj` e `D0Probe.cs.txt` compilam **a mesma fonte original** `../github-metadata-lab/GitHubReleaseDiscovery.cs.txt` do D0, sem duplicar sua lógica de comparação. O executável de testes recebe exclusivamente JSON de Release fictícia em `RUNNER_TEMP` e retorna estado consultivo. Nenhuma fonte experimental entrou no projeto principal do GF.
- `d11_persistent_lab.py` gera metadados Root/Targets/Snapshot/Timestamp assinados somente com as **seeds determinísticas públicas de teste Stage11**, em duas gerações. O cliente python-tuf 7.0.1 mantém arquivos confiáveis no diretório do Windows descartável, cria dois objetos `Updater` sucessivos e verifica sequência de versões de papéis `[1,1,1,1]` para `[1,2,2,2]` (Root mantém 1).
- A versão devolvida pelo código C# do D0 deve ser igual à versão do manifesto autenticado pelo TUF, cujo contrato D1 exige repo fixo, canal stable, tag, arquitetura, nome do asset, tamanho, SHA-256 e flags `test_only=true` e `operational_installable=false`.
- O fetcher Python-TUF é **exclusivamente em memória**, com host artificial `gf-d11-test.invalid`. Nenhum pacote executável, GitHub API, rede ou instalador é consultado ou executado.
- Cada ataque adversarial usa uma **cópia independente** dos arquivos confiáveis de geração 2, impedindo que um refresh abortado contamine os casos seguintes.
- `.github/workflows/gf-github-d11-trust-lab.yml`: CI Windows descartável, .NET 10 e Python 3.13 fixados, dependências TUF pinadas, testes e guarda de isolamento de compilação.

## 3. Evidência e testes

A CI `41150329284` registrou:
```json
{"schema":"gf-github-d11-persist/1","status":"passed","positive_cycles":2,"adversarial_rejected":14,"persisted_role_versions":[1,2,2,2],"same_user_global_rollback_resistant":false,"cross_process_recovery_proven":false,"production_root":false,"network_requests":0,"executables_downloaded":0,"product_modified":false,"operational_ready":false,"automatic_recovery_authorized":false}
```

Os 14 cenários com recusa comprovada:
1. Replay de Timestamp v1 após v2.
2. Snapshot antigo injetado em nova geração assinada v3.
3. Targets antigos injetados em nova geração assinada v3.
4. Manifesto v1 com metadados v2.
5. Divergência de tag D0/D1.
6. Release draft detectada por D0.
7. JSON D0 com propriedade duplicada.
8. D0 sem versão mais nova.
9. Timestamp v3 assinado, porém expirado.
10. Timestamp ausente.
11. Targets de nova geração com bytes alterados.
12. Manifesto autenticamente assinado pela *chave de teste*, porém de arquitetura incorreta.
13. Manifesto autenticamente assinado pela *chave de teste*, porém de repositório incorreto.
14. Downgrade em relação à versão instalada.

**Correções intermediárias:**
- [41149999105](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41149999105) e [41150001655](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150001655) falharam antes dos ensaios TUF porque o projeto estava corretamente marcado `IsPublishable=false`, mas o workflow usava `dotnet publish`. Corrigido para `dotnet build`.
- [41150135512](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150135512) revelou expectativa incorreta: Python-TUF ignora metadados Snapshot antigos oferecidos em refresh de Timestamp **não alterado**, aproveitando cache confiável. O teste foi corrigido para simular nova geração v3 com Snapshot/Targets inconsistentes; cópias do histórico confiável isolam contraprovas. Nenhum controle criptográfico foi relaxado.
- `41150329284` é a CI técnica **válida**.

## 4. Limites e riscos pendentes

- A persistência observada é entre **instâncias de Updater no mesmo processo da suíte**, com diretório de metadados intacto. Não prova reinício de processo, crash/power loss, fsync, segurança transacional, ou que a cópia do diretório esteja protegida de escrita de mesmo usuário.
- **H1 não comprovado:** um usuário/atacante que substitua o conjunto completo de arquivos confiáveis pode reintroduzir um histórico anterior. O resultado explicitamente diz `same_user_global_rollback_resistant=false`. Isso proíbe afirmar proteção global antirrollback.
- O laboratório reutiliza D1 `validate_contract` após Python-TUF verificar o vínculo assinado por tamanho e SHA-256, mas não constitui um protocolo de instalação nem uma autoridade de produção. Chaves privadas de teste são derivadas de seeds que estão no repositório público.
- A fonte do D0 foi compilada no runner para JSON fictício, **não houve consulta GitHub real**. O D1.1 não recebe pacotes reais, não autentica bytes executados, não controla OLD/NEW, não comprova efeitos externos ou READY.
- Todos os resultados e quaisquer estados locais do laboratório são apenas observações; **não autorizam novas tentativas ou recuperação automática**.

## 5. Próximo incremento sugerido — D1.2 antes de D2

Validar regras de associação da Release oficial ao manifesto TUF, políticas de versões e estado confiável **após reinício de processo** no runner Windows, e refutar rollback completo do histórico same-user, usando fixtures inertes e sem alegar resistência quando não houver controle independente. Preservar as conclusões de Stage11 sobre rotação de chaves e estado persistente. A autenticação de produção exigirá uma raiz de confiança gerada e protegida fora do repositório, canais e prazo de expiração documentados; não inserir seeds privadas de produção em GitHub. Só então pesquisar D2 (download e preparação de bytes inertes autenticados).

## 6. Estado congelado

**Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, zero recuperações automáticas. HOLD é contenção.** H1/H2/H3 ainda não provados. Sem mudanças em main, PR #65 Draft, BETA .66, GF distribuído, Cloudflare, credenciais, GitHub Releases, merge, deploy, instaladores ou dispositivos pessoais. WORK reservado a auditoria de integração relevante.
