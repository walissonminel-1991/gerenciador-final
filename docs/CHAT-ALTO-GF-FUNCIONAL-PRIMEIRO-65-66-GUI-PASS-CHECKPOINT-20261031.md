# CHAT ALTO — Funcional primeiro: GF .65 -> .66 por clique real, Windows PASS

**Data:** 2026-10-31. **Prioridade:** primeiro concluir o ciclo funcional, depois incorporar e homologar as camadas de segurança. **Prontidão para distribuir:** NÃO; READY=false. **Experimento:** exclusivamente Windows descartável na branch `experimento-g5-ipc-chat-alto-20261029`.

## Objetivo provado, sem extrapolação

O GF **normal privado** 5.13.11.65, compilado com `-p:GfSelfUpdatePrivate=true -p:GfE2eLab=false`, abriu a **MainForm normal e visível** e recebeu um clique nativo independente no controle **Verificar GF**. A própria aplicação recuperou um ZIP 5.13.11.66 via HTTP `127.0.0.1`, verificou seu SHA-256 declarado pelo feed controlado, extraiu e verificou versão do pacote, lançou **segunda cópia da própria build GF** como coordenador fora da pasta substituída; handshake adquiriu handle do OLD, OLD encerrou, a instalação antiga foi **preservada** sob nome distinto, a NEW assumiu o caminho original, e o processo `GerenciadorFinal.exe` NEW mostrou MainForm visível e responsiva. Nenhum arquivo privado de usuário foi tocado.

**CI canônica:** [41229672721](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41229672721), SHA técnico `60258c0c1c11dcc62039f621fa2b2d118f242b38`. **SUCCESS: 2/2 jobs**, incluindo job funcional `124621089482`. Log:

```text
GF_PRIVATE_FUNCTIONAL_BUILDS_PASS old=5.13.11.65 new=5.13.11.66 normal_build=true no_e2e=true
GF_PRIVATE_FUNCTIONAL_ZIP_CREATED
GF_PRIVATE_FUNCTIONAL_OLD_GUI_CLICK_PASS visible_normal_old=true button=Verificar_GF READY=false
GF_PRIVATE_FUNCTIONAL_E2E_PASS old=5.13.11.65 new=5.13.11.66 downloaded_zip=true old_exited=true new_installed_original_path=true new_real_gui_responsive=true local_lab_only=true READY=false
```

**Prova alternativa anterior, por comando no mesmo GF (sem clique):** [CI 41229468316](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41229468316), SHA `f43e9d72cffc148df192b31bbbafb58c51992028`, **SUCCESS 2/2**; também apresentou `GF_PRIVATE_FUNCTIONAL_E2E_PASS`.

A CI de consulta normal privada sem GitHub token foi [41228407249](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41228407249), **SUCCESS**. O novo fluxo privado também preservou essa prova na CI canônica acima.

## Implementação funcional presente na branch

- `GerenciadorAplicativos.csproj`: compile condicional em **build normal privada** via `GfSelfUpdatePrivate=true`; sem inclusão na compilação distribuída padrão ou em modo `GF_E2E_LAB`.
- `Program.cs`: botão de atualização GF no cabeçalho; caminho funcional acionado por esse botão no laboratório, além de comandos de CLI para verificar o cenário e executar trabalhador da mesma build.
- `prototipos/autoatualizador-seguro/phase1-composed/GfPrivateSelfUpdateProbe.cs.txt`: consulta pública token-free, leitura apenas no modo padrão de teste privado.
- `prototipos/autoatualizador-seguro/phase1-composed/GfPrivateFunctionalUpdate.cs.txt`: download, checagem SHA-256 **não assinada**, extração, versão, cópia do próprio GF para trabalhador separado, handshake de processo, instalação preservando OLD e execução NEW **exclusivamente com guards de runner CI e feed localhost**.
- `prototipos/autoatualizador-seguro/phase1-composed/private_functional_feed.py`: apenas **servidor de fixture CI** em 127.0.0.1, não componente distribuído.
- `.github/workflows/gf-private-selfupdate-normal.yml`: prova build, criação de ZIP, feed local, clique nativo na MainForm OLD, instalação observada, versão OLD preservada, versão NEW instalada e observação Win32 independente da nova GUI.

## O que esta prova NÃO significa

1. **Ainda NÃO é atualização do GitHub real.** A origem funcional da instalação foi HTTP loopback local, com ZIP preparado de build .66 na própria CI; o GitHub não ofereceu release público .66. A Draft `429695537` permanece privada e intocada.
2. **SHA-256 fornecido pelo próprio feed NÃO autentica o editor.** A cadeia TUF operacional, proteção contra pacote malicioso, confiança persistente, TOCTOU e recuperação transacional precisam ser aplicados/homologados depois do caminho funcional da origem GitHub; jamais distribuir a demonstração insegura aos usuários.
3. **O teste NÃO promove `READY=true`** nem autoriza merge, release, deploy, recuperação, rollback, limpeza, retry ou uso em PCs pessoais.
4. **Python** foi empregado para fornecer o feed e observar os processos **apenas na CI**, nunca como pré-requisito do GF normal.

## Próximo único bloqueio funcional — sem desvio

Trocar a fixture HTTP 127.0.0.1 por **obtenção token-free de versão e ZIP autênticos do GitHub**, aproveitando a integração real já demonstrada. Nesta data o repositório possui somente a Draft `429695537` sem publicação; **não há release público adequado para a prova**. Não publicar nenhuma versão sem autorização. Preparar o mecanismo e uma fixture equivalente de GitHub/HTTP, preservando o êxito GUI; quando um release de teste puder ser autorizado, executar teste end-to-end GitHub→GF. Apenas após esta entrega funcional, integrar e homologar segurança para distribuição. Manter `READY=false` enquanto isso.

**Superfícies preservadas:** `main`, PR #65 Draft, BETA .66, Draft `429695537`, ZIP `694233137`, manifesto `694233139`, Cloudflare, PCs pessoais, H1/H2/H3. Sem WORK, merge ou publicação.
