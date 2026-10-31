# GERENCIADOR FINAL — Autoatualizador semanal / GitHub único / checkpoint funcional

**Data:** 2026-10-31. **Objetivo único:** ao abrir, consultar a cada 7 dias somente GitHub Releases oficial, comparar versão, baixar o ZIP portátil se mais novo, verificar integridade, encerrar OLD, substituir silenciosamente e reabrir NEW.

**Branch única:** `experimento-g5-ipc-chat-alto-20261029`. **HEAD técnico deste checkpoint:** `f77aecc316e121acafdc3cbfc7f2dbb5e3e208a1`. **READY=false; ainda não distribuível.** Não editar `main`, PR #65, BETA .66, Draft privada `429695537` ou seus assets.

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

## Bloqueios concretos, sem desvio de objetivo

- **B1:** terminar a validação funcional do trabalhador do ramo GitHub, corrigindo somente a falha observada em `41230997752`, repetir regressão dirigida e comprovar NEW visível.
- **B2:** não há Release público no repositório. A única entrada atual é a Draft privada `429695537` (tag `gf-e2e-c5057bbbaf1d`) que não pode ser acessada sem autenticação; **nenhuma publicação de teste foi autorizada**. Logo, o caminho GitHub oficial está implementado, mas a prova positiva de download de Release público depende de uma publicação estável com o asset `GerenciadorFinal-win-x64.zip` e o digest SHA-256. Não inventar prova dessa etapa.
- **B3:** depois de comprovar funcionamento pelo canal oficial, completar e revisar antes da distribuição autenticação de editor/assinaturas, proteção contra alterações indevidas, recuperação e política de privilégios para instalações em diretórios protegidos. Não ampliar essa frente antes do ciclo funcional.

Não publicar, não mesclar, não usar WORK sem autorização, não usar PCs pessoais, não declarar READY antes das provas finais. Prioridade inalterada: concluir esses mesmos cinco passos.
