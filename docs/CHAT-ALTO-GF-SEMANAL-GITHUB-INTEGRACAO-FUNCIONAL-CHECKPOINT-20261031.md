# GERENCIADOR FINAL — Autoatualizador semanal / GitHub único / checkpoint funcional

**Data:** 2026-10-31. **Objetivo único:** ao abrir, consultar a cada 7 dias somente GitHub Releases oficial, comparar versão, baixar o ZIP portátil se mais novo, verificar integridade, encerrar OLD, substituir silenciosamente e reabrir NEW.

**Branch única:** `experimento-g5-ipc-chat-alto-20261029`. **HEAD técnico funcional aprovado:** `137b8494b595c702970043af04a85cdd4aae632f`. **READY=false; ainda não distribuível.** Não editar `main`, PR #65, BETA .66, Draft privada `429695537` ou seus assets.

## Código desenvolvido

- `GfGithubWeeklyUpdate.cs.txt` (somente `GfSelfUpdatePrivate=true`): data UTC da última tentativa em `%LOCALAPPDATA%/GerenciadorFinal/autoatualizacao/ultima-consulta.json`, intervalo de 7 dias, consulta adicional manual pelo botão, endpoint fixo `https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest`, não usa token.
- Seleção exclusiva de Release público estável mais novo, tag de versão GF em quatro segmentos, asset fixo `GerenciadorFinal-win-x64.zip`, checagem de tamanho e `digest` SHA-256, transferência do asset via API GitHub para pacote local; nenhum Cloudflare/servidor próprio em produção.
- `Program.cs`: consulta automática na abertura normal depois do inventário e opção manual **Verificar GF**; ações em background e fechamento WinForms normal quando pacote/worker estiverem preparados.
- `GfPrivateFunctionalUpdate.cs.txt`: cópia do mesmo executável GF para coordenador externo, handshake com handle do processo OLD, preservação da pasta OLD, instalação de NEW na pasta original e início de NEW. Agora há uma exigência de janela principal observada antes do recibo final no ramo GitHub, com HOLD se não comprovada.
- `GerenciadorAplicativos.csproj`: habilitação privada por `GfSelfUpdatePrivate=true`; compilação pública habitual continua **sem** o updater ainda não homologado.
- `.github/workflows/gf-private-selfupdate-normal.yml`: regressões de build normal privada, consulta pública sem token, persistência do intervalo semanal e ciclo .65 → .66 em runner Windows descartável. Feed HTTP localhost e ZIP preparado na CI são **somente fixture de teste**; não representam um segundo canal para usuários.

## Evidências

1. [CI 41230693970](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41230693970) — **SUCCESS**, incluindo persistência e segunda consulta suprimida por 7 dias.
2. [CI 41229672721](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41229672721) — **SUCCESS**, clique real em Verificar GF iniciou atualização funcional .65 → .66 por fixture, OLD encerrado, versão NEW original e GUI NEW responsiva.
3. [CI 41230997752](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41230997752) — **FAILURE somente no novo teste de trabalhador do ramo GitHub**: a regressão do fluxo GUI anterior passou, mas não surgiu recibo `gf-private-github-update/1` após a execução de `--gf-private-github-fixture`. Causa ainda indeterminada nessa CI; **não classificar como aprovado**.
4. Instrumentação mínima no mesmo trabalhador registra `phase.txt` e categoria de exceção apenas em CI, sem caminhos privados. [CI diagnóstica 41231343813](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41231343813) disparada para distinguir falha de swap, de launch ou de observação NEW; **resultado pendente na última consulta**.

## Correção funcional posterior — CONCLUÍDA e comprovada

A [CI Windows 41231564032](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41231564032), SHA técnico `137b8494b595c702970043af04a85cdd4aae632f`, terminou **SUCCESS em ambos os jobs**. Registros reais:

```text
GF_PRIVATE_FUNCTIONAL_OLD_GUI_CLICK_PASS visible_normal_old=true button=Verificar_GF READY=false
GF_PRIVATE_FUNCTIONAL_E2E_PASS old=5.13.11.65 new=5.13.11.66 downloaded_zip=true old_exited=true new_installed_original_path=true new_real_gui_responsive=true local_lab_only=true READY=false
GF_GITHUB_WORKER_FUNCTIONAL_PASS new=5.13.11.66 original_path=true backup_preserved=true mainform_verified=true fixture_only=true READY=false
```

A falha da CI 41230997752 foi localizada com observação mínima em [CI 41231343813](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41231343813): fase `NEW_WINDOW_OBSERVED`, erro `InvalidOperationException`. O NEW já estava instalado, seu backup OLD preservado e a janela observada; o próprio código do coordenador descartava o `Process` com `using(next)` e depois tentava ler `next.Id` para escrever o recibo. A correção única capturou `newPid` ANTES do descarte, comprovada na CI canônica acima. Não houve relaxamento de autenticação ou de fechamento.

A prova de consulta semanal e persistência local também passou na mesma CI. Portanto **o impedimento B1 foi resolvido**; segue pendente apenas o Release público real para teste extremo a extremo do canal único e, posteriormente, os controles de segurança pré-distribuição. `READY=false`.

## Continuidade 31/10 — protocolo GitHub e nome final do ZIP comprovados (sem publicação)

**SHA técnico `3008d2b508039b338b077a3555cef9707ab2c580`.**
**[CI Windows 41232138030](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41232138030): SUCCESS, 2/2 jobs.**

O teste chama o **mesmo analisador de Release e o mesmo downloader C#** usados no caminho privado de produção, substituindo somente as respostas de rede em memória na CI, com URLs idênticas às da API oficial:

- `GET https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest`, resposta simulada de Release público estável `v5.13.11.66`, asset exato `GerenciadorFinal-win-x64.zip`, tamanho e SHA-256;
- `GET .../releases/assets/<id>`, resposta simulada `302` para o host oficial `release-assets.githubusercontent.com`, download do ZIP .66 construído na CI;
- o SHA-256 correto permite preparar o pacote, enquanto o SHA-256 incorreto é rejeitado **antes de iniciar trabalhador ou instalar**;
- o trabalhador do GF confirma OLD encerrado, backup OLD preservado, NEW no caminho original e **janela principal NEW aberta**.

Registros reais encontrados no log do job funcional:

```text
GF_PRIVATE_FUNCTIONAL_OLD_GUI_CLICK_PASS visible_normal_old=true button=Verificar_GF READY=false
GF_PRIVATE_FUNCTIONAL_E2E_PASS old=5.13.11.65 new=5.13.11.66 downloaded_zip=true old_exited=true new_installed_original_path=true new_real_gui_responsive=true local_lab_only=true READY=false
GF_GITHUB_PROTOCOL_PASS official_api_urls=true metadata=true stable_release=true asset_302=true sha256=true tampered_rejected=true no_token=true fixture_only=true READY=false
GF_GITHUB_WORKER_FUNCTIONAL_PASS new=5.13.11.66 original_path=true backup_preserved=true mainform_verified=true fixture_only=true READY=false
```

A regressão confirmou também persistência do intervalo de sete dias e consulta sem token. **O ZIP do teste tem o nome exato do futuro asset estável, com executável e DLL na raiz; não foi publicado nem disponibilizado como Release.**

**Limite fundamental:** esse teste substitui respostas do GitHub por dados controlados na memória. Ele prova o *código do protocolo*, não prova download HTTPS de um **Release público real do projeto**. Na conferência desta rodada, o repositório ainda possui somente a Draft privada `429695537`, sem Release público. A publicação depende de autorização expressa e não foi feita. `READY=false`. A distribuição seguirá bloqueada até concluir, depois da prova funcional real, as camadas de segurança adequadas.

## Bloqueios concretos, sem desvio de objetivo

- **B1 RESOLVIDO:** trabalhador do ramo GitHub instalou NEW, preservou OLD e confirmou janela na CI 41231564032.
- **B2:** não há Release público no repositório. A única entrada atual é a Draft privada `429695537` (tag `gf-e2e-c5057bbbaf1d`) que não pode ser acessada sem autenticação; **nenhuma publicação de teste foi autorizada**. Logo, o caminho GitHub oficial está implementado, mas a prova positiva de download de Release público depende de uma publicação estável com o asset `GerenciadorFinal-win-x64.zip` e o digest SHA-256. Não inventar prova dessa etapa.
- **B3:** depois de comprovar funcionamento pelo canal oficial, completar e revisar antes da distribuição autenticação de editor/assinaturas, proteção contra alterações indevidas, recuperação e política de privilégios para instalações em diretórios protegidos. Não ampliar essa frente antes do ciclo funcional.

Não publicar, não mesclar, não usar WORK sem autorização, não usar PCs pessoais, não declarar READY antes das provas finais. Prioridade inalterada: concluir esses mesmos cinco passos.


## Continuidade verificada em 01/11/2026 — G5, escopo congelado

**Escopo autorizado:** somente autoatualização real do GERENCIADOR FINAL; sem merge, release, distribuição, telemetria, WORK ou alteração dos demais ramos/PRs. Revisão feita por leitura da branch **HEAD pré-checkpoint** `b3d382a1e443ee0274b6338cfbd446a6ee843450`, após os commits técnicos até `3008d2b508039b338b077a3555cef9707ab2c580`. O novo commit abaixo é **documental, não uma prova adicional de execução**.

### Evidência concreta

- [CI privada Windows 41232138030](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41232138030): 2/2 jobs SUCCESS no SHA técnico `3008d2b508039b338b077a3555cef9707ab2c580`; clique **Verificar GF** em OLD normal, versão .65→.66, novo processo com janela principal e ZIP de fixture; protocolo API GitHub + redirecionamento 302 + SHA-256 correto/errado, URLs reais com respostas **simuladas em memória**; persistência semanal sem token. Mensagens de evidência `GF_PRIVATE_FUNCTIONAL_E2E_PASS`, `GF_GITHUB_PROTOCOL_PASS`, `GF_GITHUB_WORKER_FUNCTIONAL_PASS`; `fixture_only=true; READY=false`.
- [CI OLD Draft/L18 41231963660](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41231963660): 6/6 jobs SUCCESS no SHA `5bf68bedf102da3a26671b04a94386861b16798f`. Em Windows descartável, OLD normal/GUI responsiva, aquisição da **Draft privada real** com TUF experimental, L18 instala NEW no caminho original e executa o NEW; testes positivos de handle NTFS e negativos antes/depois do launch e de corte transacional, preservando OLD e canário e usando `UnknownBlocked`/HOLD quando necessário. **A Draft nunca foi Release público**; `READY=false`.
- [CI composição 41232138108](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41232138108) SUCCESS. HEAD `b3d382...` contém apenas documentação posterior ao código comprovado, e nenhum workflow novo foi executado nesse HEAD documental.
- [API GitHub releases consultada em 01/11/2026](https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest): **404**. [Lista releases](https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases): só Release `429695537`, `draft=true`, nome `GF HOMOLOGACAO DESCARTAVEL - NAO PUBLICAR`; sem asset estável público `GerenciadorFinal-win-x64.zip`. Nenhuma criação, modificação ou publicação ocorreu nesta rodada.

### Bloqueios e fronteira de confiança (comprovados em código)

1. **B2 — entrada pública inexistente.** `GfGithubWeeklyUpdate.cs.txt` consulta `GET /releases/latest`, seleciona o ZIP estável fixo com digest e retorna sem instalação se obtiver 404. Não é possível demonstrar download público real com esse estado. A simulação do protocolo não deve ser contabilizada como tal. **Não publicar Release para desbloquear teste nesta missão.**
2. **G5 — junção de segurança ainda ausente no fluxo público.** No método `GfGithubWeeklyUpdate.RunAsync`, a versão pública é obtida e passada diretamente para `GfPrivateFunctionalUpdate.PrepareOfficialReleaseAsync`; esse método prepara o ZIP, extrai, copia GF para worker `--gf-private-github-worker`. Em `RunGithubWorker`, o worker aguarda o handle OLD, faz `Directory.Move(old, backup)`, `Directory.Move(stage, old)` e abre NEW, com observação da janela. **Esse caminho específico não exige manifesto TUF autenticado, diário/seletor transacional L18 nem pin NTFS entre aprovação e execução**; o digest copiado da mesma resposta pública do GitHub prova integridade da transferência, mas não autoridade independente do editor. O fluxo Draft/L18 comprovado em laboratório é outro caminho e não pode ser presumido automaticamente integrado ao caminho público.
3. **Proteção vigente:** `GerenciadorAplicativos.csproj` só compila esses arquivos em `-p:GfSelfUpdatePrivate=true`, exige perfil local, proíbe `GfE2eLab=true`; build usual não os contém. Portanto não há autorização de promoção. A workflow `.github/workflows/gf-github-draft-e2e.yml` possui operação de refresh/upload de metadados da Draft (`gh release upload --clobber`): **não disparar ou reexecutar** essa workflow neste escopo, pois a instrução proíbe mudanças em releases.

### Único próximo trabalho técnico autorizado por este checkpoint

**Vincular o candidato adquirido pelo mesmo cliente oficial GitHub ao coordenador C# L18/TUF/NTFS/TOCTOU existente em ambiente descartável, sem usar `RunGithubWorker` como substituto do coordenador seguro e sem publicar release.** Critérios: seleção rejeitada se faltarem metadados TUF válidos ou vínculos dos mesmos bytes; OLD normal mantém GUI funcional durante download; fail-closed sem execução de NEW em falha; ao concluir, OLD encerra normalmente, L18 controla seletor e instalação com pin/diário e somente o NEW autenticado executa; testes negativos preservam OLD/dados e geram HOLD onde a recuperação for inconclusiva. Começar por mapear adaptador e contratos já existentes e implementar somente o menor elo testável, sem desativar proteção ou criar mecanismo paralelo. O teste público HTTPS positivo continuará bloqueado até existir publicação expressamente autorizada e segurança suficiente. **READY=false; roadmap 27/42 inalterado.**

**Mudanças desta rodada:** somente acréscimo a este checkpoint Markdown; nenhum código, CI, fixture, artefato de distribuição ou release alterado. Manter `main`, PRs #65/#75/#76/#77 Draft e repositórios históricos privados intactos.


## G5 — 01/11/2026: bloqueio de compatibilidade dos contratos de ligação GitHub → TUF/L18

**Missão:** integrar o resultado do analisador/downloader GitHub à autenticação TUF e ao MESMO coordenador C# L18/NTFS/TOCTOU para o OLD normal, sem publicar Releases e sem usar o trabalhador funcional como substituto de autoridade. **HEAD inspecionado antes desta análise:** `d8d1ea283ae2aec8e5cd9780a37dbe5c54a85595`. **Resultado: BLOQUEADO PARA IMPLEMENTAÇÃO FUNCIONAL SEGURA — sem modificações funcionais nesta rodada.**

### Contratos inspecionados diretamente

1. **Fronteira de compilação explícita** em `GerenciadorAplicativos.csproj`: `GfSelfUpdatePrivate=true` inclui `GfGithubWeeklyUpdate.cs.txt` e `GfPrivateFunctionalUpdate.cs.txt`; `GfE2eLab=true` inclui `GfOldDraftAcquire.cs.txt`. O target `ValidateGfPrivateSelfUpdate` rejeita expressamente os dois ao mesmo tempo com `GF_PRIVATE_UPDATE_E2E_COMBINATION_FORBIDDEN`. Não suprimir esse gate.
2. **Contrato da aquisição pública**: `GfGithubWeeklyUpdate.Release(Version,AssetId,AssetLength,Sha256)` usa os campos de `GET /releases/latest` e o SHA-256 informado na mesma resposta pública. `RunAsync` chama `GfPrivateFunctionalUpdate.PrepareOfficialReleaseAsync`, que chama `--gf-private-github-worker`. `RunGithubWorker` faz duas renomeações de diretório e lança NEW, mas **não invoca** a autenticação TUF, o mesmo L18, o seletor/diário nem pin contínuo NTFS. A prova `GF_GITHUB_PROTOCOL_PASS` não concede essas autoridades.
3. **Contrato de autenticação do laboratório**: `GfOldDraftAcquire.HandoffAuthenticatedDownloadToL18Async` exige `GITHUB_ACTIONS=true`, executável `GF.Updater.M3.L18Tests.exe`, Python+artefatos TUF, `RUNNER_TEMP` e conjunto de cinco assets Draft, todos com identidades e tamanhos esperados; usa `github_draft_package.py verify --lab-old-variant` ANTES de chamar o L18. Esse verificador espera o manifesto assinado `GF-LAB-REAL-PACKAGE-MANIFEST.json`, `targets.json`, `snapshot.json`, `timestamp.json` e o arquivo `gf-e2e-new-win-x64.zip`; o manifesto e hashes fixos representam uma **Draft de laboratório**, não um Release estável. `github_draft_package.py` declara expressamente que sua raiz de assinatura é **pública/de teste** e que `operational_installable=false`.
4. **Contrato do coordenador**: `Phase1RealBridge.csproj` tem `IsPublishable=false`, `AssemblyName=GF.Updater.M3.L18Tests`, fontes fixadas para testes, incluindo L18–L22, trust fixture e Python-TUF. Reempacotar esse executável em compilação privada normal ou considerar seus marcadores locais como autoridade violaria a separação.
5. **Prova dos mesmos bytes está ausente no elo proposto**: o ZIP `GerenciadorFinal-win-x64.zip` do protocolo GitHub é construído em CI, com digest calculado dinamicamente; não está demonstrado que os bytes correspondam a um manifesto alvo TUF autenticado pela raiz persistente de produção. A versão/ZIP de Draft do adaptador é outra identidade de asset. **Não afirmar igualdade ou diferença exata sem comparação criptográfica; apenas que falta vínculo comprovado.**
6. **Release público não existe**: API `/releases/latest` retorna 404; existe apenas Draft privada `429695537`. Não publicar nem renovar metadados da Draft. A workflow `gf-github-draft-e2e.yml` contém `gh release upload --clobber`; a execução ou reexecução foi expressamente vedada neste escopo.

### Evidência Windows já existente, sem reexecução ou mutação de assets

- [CI 41232138030](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41232138030) **SUCCESS 2/2**: mesmo parser/downloader com respostas GitHub simuladas, redirecionamento e SHA; funcionamento do worker experimental isolado em Windows, `READY=false`. **Não é prova de TUF/L18 nesse caminho.**
- [CI 41231963660](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41231963660) **SUCCESS 6/6**: OLD real normal, aquisição Draft com TUF/L18 e controles adversariais incluindo pin NTFS, cortes e `UnknownBlocked`; `READY=false`. **Não é prova de protocolo público estável.**
- [CI 41232138108](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41232138108) **SUCCESS**: composição experimental, não autorização de publicação. Nenhuma CI foi disparada nesta etapa: a escrita foi exclusivamente documental e a workflow que atualiza metadados da Draft **não deve ser executada**.

**Decisão fail-closed:** não compilar os dois perfis juntos; não alterar os gates; não ligar `RunAsync` diretamente ao L18 de testes; não usar o worker funcional como caminho de atualização seguro; não transformar o manifesto de teste em autoridade operacional. Preservar `OLD`, dados pessoais, `HOLD/UnknownBlocked`, assinaturas/verificações TUF, pin NTFS e proteções do GitHub. **READY=false**, roadmap 27/42.

**Único próximo passo técnico:** criar/provar primeiro, estritamente no laboratório isolado e **sem tocar na compilação normal**, um contrato de entrega imutável que vincule o SHA-256 do ZIP recebido pelo mesmo downloader GitHub à identidade de alvo assinada por TUF, com conjunto de arquivos verificado e autoridade OLD→L18 existente. A validação negativa deve rejeitar manifesto ausente, ZIP de outro digest, metadados fora de confiança e tentativa de invocar o trabalhador de substituição não homologado. Só após haver prova dessa compatibilidade reavaliar uma integração funcional mínima; não abrir novos caminhos de atualização.

**Escopo deste commit:** 1 arquivo Markdown, sem código/testes/workflows/CI novos; nenhuma publicação, alteração Draft/asset, merge, distribuição ou mudança de telemetria. Preservados `main`, PRs #65/#75/#76/#77 Draft, infraestrutura privada histórica e G5.


## G5 — 01/11/2026: vínculo dos bytes GitHub simulado → manifesto TUF experimental PASS

**Estado técnico antes do checkpoint:** `4f94e9312041ea907a675603dcaa86886d3fca64`, branch única `experimento-g5-ipc-chat-alto-20261029`. **[CI isolada 41275664034](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41275664034): completed/SUCCESS**, job `Existing C# GitHub client to TUF file-set, no L18` SUCCESS; logs verificados.

**Objetivo deste ensaio:** usar o mesmo código real `GfGithubWeeklyUpdate.FindReleaseAsync` e `DownloadAssetWithClientAsync` por reflexão em assembly CI-only (sem cópia desses métodos) para adquirir o ZIP de fixture por URLs/redirect da API oficial simulados em memória, então verificar o arquivo recebido com `github_draft_package.py verify` e a cadeia TUF **pública e exclusiva de teste** existente. Nenhum serviço ou processo de atualização do GF é iniciado.

**Arquivos novos em HEAD:** `.github/workflows/gf-github-tuf-binding-inert.yml` (somente push na branch G5 e paths específicos; `permissions: contents: read`, sem upload de release/assets ou artefatos); `prototipos/autoatualizador-seguro/phase1-composed/github-tuf-binding-lab/GithubTufBindingLab.csproj`, `GithubTufBindingLab.cs.txt`, `check_binding.py`. O teste contém stubs inertes: `PrepareOfficialReleaseAsync` somente lança exceção; nenhum `RunGithubWorker`, nenhum L18, nenhum `Process.Start` de OLD/NEW. A fonte `.cs.txt` fica fora da inclusão automática de C# no produto; verificação de MSBuild na CI passou. Os quatro arquivos são exclusivos do laboratório. O downloader privado original recebeu apenas a anotação de nulidade `[DoesNotReturnIf(false)]` no argumento de `Require`: não alterou lógica nem reduziu validações.

**Evidência no log Windows:** `GF_GITHUB_TUF_BUILD_ISOLATION_PASS`; `GF_GITHUB_TUF_SYNTHETIC_BUILDS_PASS old=5.13.11.65 new=5.13.11.66`; `GF_GITHUB_TUF_TEST_ASSEMBLY_PASS`; `GF_GITHUB_TUF_BINDING_PASS same_downloaded_zip_sha=true signed_file_set=true tuf_test_root_only=true zip_sha_in_tuf_target=false bad_zip=REJECTED missing_manifest=REJECTED missing_targets=REJECTED forged_targets=REJECTED actual_github_network=false worker=false l18=false OLD_unchanged=true READY=false`; `GF_GITHUB_TUF_FINAL_PASS`. O verificador TUF de teste aprovou o conjunto completo de arquivos descompactados do **mesmo ZIP recebido**, cujo hash SHA-256 coincide com o ZIP original gerado no runner. Negativos: digest GitHub declarado incorreto; ZIP alterado; manifesto ausente; targets ausentes; targets adulterados. Tudo bloqueado antes de qualquer handoff.

**Regressões e correção mínima:** a primeira [CI 41275510649](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41275510649) falhou somente na compilação do projeto isolado porque o compilador reportou `CS8600` em `uri=next` após `Require(...)`. A fonte privada já bloqueava redirecionamento inválido; o compilador não sabia que `Require` falha definitivamente. Foi adicionada anotação `[DoesNotReturnIf(false)]` na guarda (sem `NoWarn`, sem suprimir avisos). [CI privada 41275655009](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41275655009) **SUCCESS 2/2 jobs**, sem relaxamento de controles ou modificação do perfil público. A CI isolada final acima **SUCCESS**.

**Limitações que NÃO foram superadas:** (1) o alvo assinado pelo TUF de laboratório é `GF-LAB-REAL-PACKAGE-MANIFEST.json`, que contém hashes/nomes de **arquivos extraídos**, e **não o SHA-256 do ZIP como alvo assinado separado**; a igualdade do SHA externo usa referência local do fixture, não constitui autenticidade independente do editor; (2) a raiz TUF usa sementes de teste conhecidas e `operational_installable=false`; (3) não houve rede GitHub pública, Release estável, autenticação de editor operacional, handoff ao coordenador L18, pin NTFS de execução, instalação nem READY. **Não inferir produção/homologação.** As provas prévias TUF/L18/NTFS continuam independentes deste ensaio.

**Próximo elo único:** sem tocar em build público/privado normal ou publicar Release, acrescentar um contrato de entrega de alvo imutável entre ZIP recebido, manifesto assinado experimental e a **entrada existente** do L18, em laboratório, demonstrando que o L18 recusa metadados/bytes sem autoridade e nunca invoca o trabalhador alternativo. Não remover `GfSelfUpdatePrivate`/`GfE2eLab` mutuamente exclusivos; não inventar segundo seletor. Somente depois de evidência Windows do mesmo L18 estudar evolução de autoridade de assinatura/Release público com autorização explícita.

**Preservação:** `main`, PRs #65/#75/#76/#77 Draft, assets Draft, repositórios privados históricos, telemetria e roadmap **27/42** sem alterações. **READY=false**, sem merge, distribuição ou novo Release.


## G5 — 01/11/2026: pré-handoff inerte do cliente GitHub ao L18 (CI PASS) e incidente de workflow Draft

**Branch autorizada:** `experimento-g5-ipc-chat-alto-20261029`. **HEAD técnico testado** `65251c4b3557aa55748f1c8a97bb0c45c480e4d8`. Missão: vincular os mesmos bytes adquiridos pelo cliente GitHub simulado e os arquivos do alvo TUF de laboratório à **entrada inerte do mesmo executável L18**, sem autorizar seletor, diário, OLD, NEW, worker, swap ou distribuição.

### Prova Windows isolada e verificável

- [CI G5 41276102301](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41276102301): **completed / SUCCESS**, job `Existing C# GitHub client to TUF file-set, no L18` (nome histórico do job; nesta execução o preflight L18 foi compilado e invocado). Log: `GF_GITHUB_TUF_BUILD_ISOLATION_PASS`, `GF_GITHUB_TUF_TEST_ASSEMBLY_PASS`, `GF_GITHUB_TUF_L18_BUILD_PASS`, `GF_GITHUB_TUF_L18_GATE_MATRIX_PASS`, `GF_GITHUB_TUF_FINAL_PASS`.
- Nova fonte exclusiva de laboratório `Phase1GithubTufInert.cs.txt`, compilada em `Phase1RealBridge.csproj`. `Phase1RecoveryTests.Entry` encaminha **somente** o argumento `--real-github-tuf-inert` ao preflight. Ele só roda na CI Windows descartável, confere repositório/runner, valida SHA esperado do ZIP e manifesto, retém handles de leitura, invoca o **verificador TUF existente** (`github_draft_package.py verify`) sem token, reconfere hashes e conjunto completo de arquivos extraídos. O cliente C# é o mesmo `GfGithubWeeklyUpdate` usado na CI anterior. O método **não entra em** `L18Coordinator.RealOwner`, `PrepareRealAuthorization`, `ReconcileReal`, seletor, diário ou `RunGithubWorker`; não executa OLD/NEW. Os handles do novo preflight não devem ser confundidos com o pin NTFS operacional de L18–L22.
- Matriz registrada no log: `GF_GITHUB_TUF_L18_GATE_MATRIX_PASS positive=DATA_ONLY bad_zip=REJECTED missing_manifest=REJECTED missing_targets=REJECTED forged_targets=REJECTED wrong_zip_sha=REJECTED wrong_manifest_sha=REJECTED selector=false journal=false NEW=false READY=false`.
- Foram adicionadas as verificações à workflow **read-only** `.github/workflows/gf-github-tuf-binding-inert.yml` e ao teste `github-tuf-binding-lab/check_binding.py`. Não se misturaram `GfSelfUpdatePrivate` e `GfE2eLab`; o produto público/privado normal não inclui a nova fonte. Nenhuma CI desse fluxo possui permissão de escrever Releases. **Nenhum handoff operacional autorizado**.

**Limite exato:** a prova positiva revalida a cadeia TUF usando **a raiz pública de teste** e manifesto `gf-real-package-lab/3` com `operational_installable=false`. O SHA do arquivo ZIP é de uma fixture local e não está coberto por um alvo TUF independente; somente os arquivos extraídos estão no manifesto assinado. O preflight de entrada L18 não autoriza `RealOwner` nem prova confiança de produção, proteção TOCTOU/NTFS entre aceite e execução, recuperação segura ou download HTTPS real. **READY=false**.

### Incidente de acionamento automático de workflow da Draft — registrar, não ocultar

A edição em `Phase1RecoveryTests.cs.txt` acionou **involuntariamente** a workflow preexistente `.github/workflows/gf-github-draft-e2e.yml`, pois a lista `on.push.paths` dessa workflow inclui exatamente esse arquivo. Isso **contrariou a proibição expressa de executar workflows que modificam assets Draft**. [Run involuntário 41276060046](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41276060046), SHA `6162ae2aed7dbbddbd3d2d3b94bfa81f688f523c`. O conector GitHub desta sessão **não dispõe de operação de cancelar workflow**. O usuário foi avisado para cancelar no GitHub. Na última consulta da rodada, o run seguia `in_progress`; portanto, não presumir que esteja encerrado.

**Impacto confirmado por API GitHub:** etapa `Renovar metadados TUF de TESTE da Draft existente` já concluída. A Release `429695537` continua **Draft**, e os assets `gf-e2e-new-win-x64.zip` (ID `694233137`, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`) e `GF-LAB-REAL-PACKAGE-MANIFEST.json` (ID `694233139`, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`) permaneceram intactos. Porém **`snapshot.json`, `targets.json` e `timestamp.json` foram substituídos** em 2026-11-01 05:14:14 UTC, com novos asset IDs `697398853`, `697398858`, `697398857`, respectivamente. Não houve ação manual de publicar ou mesclar. **Não tentar reverter as três substituições sem autorização**; preservar IDs antigos no histórico e investigar qualquer efeito posterior. A Draft deve ser considerada alterada em seus metadados TUF de teste.

**Contenção obrigatória para o próximo turno:** conferir o estado final da workflow involuntária e a lista/identidades dos assets da Draft **antes de qualquer novo commit**. Não editar arquivos que constem de `on.push.paths` de workflows com `contents: write` sem comprovar que não serão disparadas; não reexecutar ou cancelar checks por atalhos; não modificar os metadados Draft. Caso o usuário ainda não tenha cancelado `41276060046`, a orientação manual permanece.

**Próxima lacuna de G5:** examinar, **somente por leitura**, a compatibilidade entre o preflight inerte e `PrepareRealAuthorization`/seletor L18 transacional. Não conectar o sucesso desta CI ao `RealOwner` sem contrato TUF e identidade persistente apropriados. Preservar `main`, PRs #65/#75/#76/#77 Draft, repositórios privados históricos, G5 e roadmap **27/42**. Sem merge, release público, distribuição ou telemetria. **READY=false**.


## Encerramento verificado da execução involuntária 41276060046 — 01/11/2026

**Escopo deste registro:** somente leitura do GitHub seguida de atualização do presente checkpoint Markdown; **nenhuma ação em workflows, Draft, assets, binários, main ou PRs**. Referência congelada anterior: `62b85fdde20199b47bd441f7359429095356ab2e`; branch G5 conferida sem divergência antes do commit documental.

**Estado final inequívoco:** [run GitHub Actions 41276060046](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41276060046), `head_sha=6162ae2aed7dbbddbd3d2d3b94bfa81f688f523c`, `event=push`, `run_attempt=1`, **completed/SUCCESS GLOBAL**, atualizado em 2026-11-01T05:20:52Z. **Não houve cancelamento**; todos os seis jobs foram **completed/SUCCESS**: (1) `gf-old-initiated-draft-real` ID 124758352645; (2) `OLD normal abre, L18 instala e observa NEW real em outro runner Windows` ID 124759024674; (3) `Modificar 1 byte do NEW instalado apos TUF e recusar CreateProcess antes do launch` ID 124759439673; (4) `Handle NTFS pinado bloqueia escrita e renomeacao e permite NEW autentico abrir` ID 124759439714; (5) `NEW original executa apos um byte alterado ENTRE hash e Popen, manter HOLD` ID 124759439717; (6) `Corte L18 depois da segunda renomeacao, ANTES do NEW observado em Windows descartavel` ID 124759439921. As seis conclusões de job foram confirmadas diretamente pela API, não inferidas do status de outros commits. O log do segundo job registra `GF_E2E_NORMAL_INSTALLED_NEW_PASS ... HOLD=true READY=false`; a execução foi de laboratório descartável, não promoção operacional.

**Impacto persistente da execução involuntária:** [Draft release 429695537](https://github.com/walissonminel-1991/gerenciador-final/releases/tag/gf-e2e-c5057bbbaf1d) permaneceu `draft=true`, `published_at=null`, tag `gf-e2e-c5057bbbaf1d`. Conferidos os **cinco** assets e seus digests declarados pela API GitHub:
- `gf-e2e-new-win-x64.zip`: ID **694233137**, size **57695630**, sha256 **3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e**, atualização de 2026-10-31T03:25:50Z; **ID/hash preservados**.
- `GF-LAB-REAL-PACKAGE-MANIFEST.json`: ID **694233139**, size **72618**, sha256 **a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d**, atualização de 2026-10-31T03:25:48Z; **ID/hash preservados**.
- `snapshot.json`: ID **697398853**, sha256 **53ae12387884974ce3e51531ca0d564fb6592c96be01fe1bef460defb61cfb02**; **substituído** em 2026-11-01T05:14:14Z.
- `targets.json`: ID **697398858**, sha256 **9db8d7bc941737db5c18fa17a8d49407d81cecadc71042a1a22af882cdbdbe04**; **substituído** no mesmo instante.
- `timestamp.json`: ID **697398857**, sha256 **90e2f862aff3c9306ff4a6c91d783e01deb6a7921a5163ea76426f9f91d31e4f**; **substituído** no mesmo instante.

**Causa comprovada:** a workflow `.github/workflows/gf-github-draft-e2e.yml` tem `on.push.paths` incluindo `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt`, `permissions: contents: write` e etapa `gh release upload ... --clobber` para três roles TUF; editar esse arquivo no commit técnico acionou acidentalmente o workflow de escrita. **O sucesso de todos os jobs não autoriza essa escrita retrospectivamente.** Não foi feita tentativa de reversão; não reexecutar a workflow, não tentar repor versões anteriores sem nova autorização e nova avaliação de confiança.

**Contenção alcançada:** run encerrada; nenhuma ação de cancelamento agora é cabível; Draft ainda privada; ZIP e manifesto originais retidos conforme IDs e digests públicos; metadados TUF **alterados** e registrados. A continuação segura do G5 começa por **inspeção somente leitura** das dependências e da lista `on.push.paths` das workflows com permissão de escrita, antes de qualquer edição técnica; definir um ensaio de contrato de autorização TUF→`PrepareRealAuthorization` sem arquivos gatilho de Draft e sem ligar o sucesso de preflight a `RealOwner`. **Sem nova escrita até provar isolamento dos gatilhos**, e sem transformar hash GitHub em confiança independente.

**Estado final do produto:** `READY=false`; roadmap 27/42; preservar `main`, PRs #65/#75/#76/#77 Draft, repositórios históricos privados, telemetria e nenhuma distribuição/Release público. Este registro é documental, não uma prova adicional nem autorização de recuperação/rollback.
