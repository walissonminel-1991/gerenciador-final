# CHAT ALTO — Conexão HTTP realmente encerrada no meio do ZIP NEW pelo GF OLD

**Data:** 31/10/2026. **Status:** prova de falha de transporte no Windows Actions descartável **SUCCESS**; autoatualização de produção permanece não autorizada.

## Identidade e verificação

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- **Commit técnico comprovado:** `0fa782c19f1e3241229919307a9aa3be1ddf1789`.
- **CI final:** [41202775129](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41202775129), job `124541715888`: `completed/success`, testou SHA `0fa782c19f1e3241229919307a9aa3be1ddf1789`.
- Draft existente: ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`; **não houve publicação**.
- ZIP imutável `gf-e2e-new-win-x64.zip`: asset ID `694233137`, 57.695.630 bytes, SHA256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto imutável `GF-LAB-REAL-PACKAGE-MANIFEST.json`: asset ID `694233139`, 72.618 bytes, SHA256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Apenas os roles TUF de **TESTE** `targets/snapshot/timestamp` foram renovados pela CI; sem alteração do ZIP/manifesto, sem credenciais persistentes em GF.

## Objetivo exclusivo concluído

Executar no Windows descartável uma **desconexão TCP HTTP de verdade** enquanto o **GF OLD original 5.13.11.65** recebe o ZIP autêntico **NEW 5.13.11.66** por seu mesmo método `FetchAsset` de `.NET HttpClient`, preservando OLD e dados e bloqueando handoff, lançamento de NEW, rollback, limpeza e READY.

Essa prova **não** é o teste antigo `github_draft_package.py negative` que entrega um arquivo ZIP já truncado para um parser. Aqui a resposta HTTP anuncia **Content-Length de 57.695.630**, retorna **HTTP 200** e começa a transmitir os bytes autênticos. O servidor fecha a conexão TCP depois de **1.048.576 bytes**, antes do restante do corpo, causando falha de leitura em fluxo do cliente. A CI exige arquivo parcial (estritamente entre 0 e tamanho total), categoria de exceção de transporte, código de saída 8 do GF OLD, e relatório do servidor com fechamento de socket.

O servidor é **somente um injetor de falhas na interface 127.0.0.1 de Windows Actions**, não outro sistema de atualização, não CDN GitHub, e nunca recebe `GH_TOKEN`. Os metadados da Draft continuam consultados por HTTPS e validados normalmente. O servidor usa como fonte o **mesmo ZIP completo real da Draft**, previamente baixado via `gh release download` no runner e verificado por **SHA-256 e tamanho** antes de qualquer conexão. O cliente continua usando `SocketsHttpHandler` do GF, `ResponseHeadersRead`, leitura streaming em blocos, limite de tamanho e SHA256. Somente o **endereço do recurso ZIP** é redirecionado por flag `GF_E2E_HTTP_CUT_NEGATIVE=1` protegida por `GF_E2E_LAB`, `GITHUB_ACTIONS=true` e repositório fixo; não permite endpoint remoto arbitrário, não passa Authorization ao loopback.

## Alterações técnicas — somente laboratório

1. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt`: injeção de endpoint `http://127.0.0.1:<porta>/gf-e2e-new-win-x64.zip` apenas para ZIP e sob as guards de runner; o método real `FetchAsset` não foi duplicado. O destino do teste negativo é `RUNNER_TEMP/gf-e2e-http-cut-download`, distinto da transferência positiva. Exige que `GF_E2E_HANDOFF_L18`, `GF_E2E_SELF_GRACEFUL_CLOSE`, `GF_E2E_SELECTED_NEW_CONTINUATION` e a flag de corte de instalação não estejam ativos. Falha de rede gera `gf-e2e-http-cut-failure.json` com motivo e fase, sem token/URL CDN. Não escreve report de download completo.
2. `prototipos/autoatualizador-seguro/phase1-composed/http_cut_server.py`: servidor Python padrão `HTTPServer` ligado exclusivamente a `127.0.0.1`, lê ZIP com SHA/tamanho fixados e confirma ausência de cabeçalho Authorization; anuncia Content-Length total, entrega exatamente 1 MiB e desliga o socket TCP, escrevendo relatório pequeno de transporte.
3. `prototipos/autoatualizador-seguro/phase1-composed/http_cut_negative.ps1`: passo isolado de teste, roda **o mesmo GerenciadorFinal.exe OLD**, pela entrada de linha de comando já existente `--gf-e2e-draft-acquire`, com suas flags de laboratório e sem L18, depois de concluídos os testes positivos anteriores. Não inicia a interface, não tenta atualizar novamente o mesmo processo e não faz rollback/cleanup de instalação.
4. `.github/workflows/gf-github-draft-e2e.yml`: acrescenta somente a etapa de transporte no fim da CI já existente e monitora os dois novos scripts; mantém a CI anterior de troca física/HOLD, os seis negativos OLD/NEW/canário e negativos adulterado/truncado.

## Evidência confirmada da CI 41202775129

A CI foi **SUCCESS** e todas as etapas previamente aprovadas também passaram. O job registrou literalmente:

```
GF_E2E_FIRST_DIRECTORY_MOVE_OBSERVED old_backup_present=true retry=false
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_GUIDANCE_NEGATIVES_INDEPENDENT_PASS old_modified=BLOCK old_missing=BLOCK new_modified=BLOCK new_missing=BLOCK canary_modified=BLOCK canary_missing=BLOCK false_preservation_claims=0 original_bytes_unchanged=true selector=new/2 rollback=false cleanup=false READY=false
GF_DRAFT_NEGATIVES ... modified rejected=true ... interrupted BadZipFile rejected=true
GF_E2E_REAL_HTTP_CUT_OLD_AND_DATA_INTACT old_files=284 canary=UNCHANGED new_executed=false READY=false
GF_E2E_REAL_HTTP_CONNECTION_CUT_PASS pinned_zip=true declared=57695630 sent=1048576 received=1048576 dotnet_httpclient=true old_exit=8 L18_not_invoked=true READY=false
```

A auditoria independente da etapa negativa comparou os **284 arquivos OLD** preservados com inventário original completo calculado **antes** do processo OLD original; verificou canário `DO-NOT-TOUCH-MY-DATA\n`, seletor L18 `new/2` e seu SHA preservados, nenhum recibo de instalação concluída, nenhum NEW observado/executado, e somente **um arquivo ZIP parcial de exatamente 1.048.576 bytes** no diretório separado do teste. A ausência do caminho original decorre **do corte físico da etapa anterior da mesma CI**, e NÃO da falha de HTTP; o GF OLD da prova negativa executou a partir do backup preservado.

O erro da transferência foi tratado pelo GF OLD com **exit code 8**, na fase `asset_download`, sem credenciais no relatório e sem invocação de L18 pela tentativa negativa. O servidor não recebeu bearer e fechou seu próprio socket após 1 MiB. Não há READY, rollback, retry ou limpeza automáticos.

## O que NÃO foi demonstrado

- A falha HTTP foi genuína como **evento de transporte TCP**, porém **induzida na interface de loopback de Windows descartável**. Não houve interrupção real de `api.github.com` nem da CDN GitHub; não alegar robustez comprovada contra qualquer falha pública de internet, DNS, proxy, antivírus ou TLS.
- O processo negativo foi o GF OLD real, mas invocado pelo entrypoint CLI `--gf-e2e-draft-acquire`, **não pela `MainForm.Shown`**. Preservação da interface OLD visível e responsiva enquanto seu download falha não foi comprovada nesta prova. A CI positiva anterior já havia comprovado MainForm.Shown para transferência completa e handoff.
- O GF OLD da negativa foi executado a partir do **backup OLD** da experiência de corte físico; o estado sem executável no caminho original já existia antes da negativa. OLD foi preservado byte a byte, mas isso não representa uma instalação normal atualizável.
- O código é compilado somente em `GF_E2E_LAB`; **não existe descoberta pública de atualização para usuários** nem esquema de chaves TUF de produção. Token da Draft é efêmero da CI. `UnknownBlocked` e `HOLD` persistem, sem autoridade para recuperação/READY.
- Não demonstrados: H1/H2/H3, prova física de power loss, TOCTOU de mesmo usuário, pós-reboot, efeitos externos fora dos Jobs, durabilidade NTFS de produção, restrições de usuários sem conhecimentos técnicos.

## Próximo objetivo único, se solicitado

Demonstrar **no Windows Actions** que uma interrupção do mesmo tipo, induzida durante o fluxo iniciado pela `MainForm.Shown` de GF OLD, **não encerra a janela nem compromete busca/inventário e dados do OLD**, sem handoff L18 nem exibir aviso falso de sucesso. É a principal evidência faltante dessa negativa, não nova arquitetura. Se o teste exigir adaptação de localização de arquivos, preservar OLD em diretório descartável independente, sem usar o caminho original ausente da prova entre renomeações. Não promover para produção nem abrir novo roadmap.

**Invariantes preservadas:** `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, releases públicos, merge/deploy, credenciais persistentes, WORK, rollback/recovery/READY: nenhum tocado.
