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
