# CHAT ALTO — Limite observável NTFS pin → CreateProcess → imagem mapeada (amostra limitada)

**Data:** 2026-10-31. **Status:** experimento somente de laboratório Windows, instrumentação registrada; CI da amostra **pendente na última consulta**. **READY=false**. Não declarar a Fase 1 concluída.

## Base e CIs efetivamente consultadas

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental recebido `92c143d22928bf8bd80aea79f053e734f327b413`; checkpoint anterior `docs/CHAT-ALTO-GF-NTFS-PIN-L18-CAUSA-COMPROVADA-CHECKPOINT-20261031.md`.
- CI [41226223387](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223387), SHA `a587038a6aabfd414963d4854b0ae517f14c4588`: **SUCCESS, 6/6 jobs**, incluindo pin NTFS `124612165438` SUCCESS. Prova de compartilhamento, objeto de arquivo estável, original `Popen`, UI funcional e hashes de disco. **Não** prova bytes integrais executados.
- CI independente D2.4.4 [41226223383](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223383): SUCCESS.
- CI de composição anterior [41226223439](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223439) ainda **in_progress** na última consulta; não inferir SUCCESS.
- Único commit técnico desta rodada: [`49ec6b6a55fb3d2ea7f23ce0ca384b85efc22c53`](https://github.com/walissonminel-1991/gerenciador-final/commit/49ec6b6a55fb3d2ea7f23ce0ca384b85efc22c53), descendente direto do HEAD recebido. Arquivos alterados **somente no laboratório**:
  1. `prototipos/autoatualizador-seguro/phase1-composed/win32_process_observation.py`;
  2. `prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py`;
  3. `prototipos/autoatualizador-seguro/phase1-composed/github_install_focused_normal.ps1`.
- CIs disparadas **automaticamente pelo commit técnico**, sem rerun ou retry: [41227264731](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41227264731) (OLD/L18 e pin), [41227264679](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41227264679) (regressão D2.4.4) e [41227264831](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41227264831) (composição). Todas **sem conclusão na última consulta**. Nenhuma delas deve ser antecipadamente declarada aprovada.

## Questão específica e instrumentos mínimos

O hash de `GerenciadorFinal.exe` em disco, `QueryFullProcessImageNameW` e o processo legítimo gerado pelo mesmo `Popen` são evidências reais, porém não medem o conteúdo integral da imagem mapeada na memória pelo loader do Windows.

No **mesmo observador original**, estritamente com `GITHUB_ACTIONS=true`, repositório exato, flags `GF_E2E_NTFS_PIN_LAUNCH=1` e `GF_E2E_L18_OBSERVE_INSTALLED_NEW=1`, enquanto o processo próprio do teste ainda está vivo:

1. reutiliza o **handle retido por `subprocess.Popen`** (não abre processo arbitrário por PID);
2. `EnumProcessModulesEx` e `GetModuleFileNameExW` localizam somente o módulo cujo arquivo corresponde ao EXE instalado. Máximo de 128 handles enumerados; falha conservadora quando excedido;
3. inspeciona **no máximo 4096 bytes de cabeçalho PE** e **16 bytes do disco** no ponto de entrada nativo; verifica seção com `IMAGE_SCN_MEM_EXECUTE`;
4. `VirtualQueryEx` consulta se a página daquele endereço pertence a `MEM_IMAGE`;
5. `ReadProcessMemory` consulta **somente 64 bytes da base da imagem + 16 bytes do ponto de entrada** do processo próprio, comparando esses 80 bytes com as regiões correspondentes do EXE já autenticado.
6. Emite exclusivamente flags booleanas, contagem fixa de 80 bytes e motivo allowlisted; **não** registra bytes, nomes de DLL, caminhos privados, endereços, valores de memória, variáveis de ambiente ou stack.

O cenário `-PinNtfsLaunch` exige que esse testemunho exista com correspondência das amostras, **sem alterar** o `CreateFileW`, `GENERIC_READ`, `FILE_SHARE_READ`, `Popen` original, TUF, identidade do objeto, hash, verificação de renomeação ou observação funcional. Se não for verificável, o cenário deve falhar fechado com `GF_NTFS_PIN_MAPPED_IMAGE_SAMPLE_NOT_PROVEN`. Não remover essa guarda só para ficar verde.

Fontes oficiais para limites de inferência: [Microsoft — PE format](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format), [EnumProcessModulesEx](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-enumprocessmodulesex), [VirtualQueryEx](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualqueryex), [ReadProcessMemory](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory).

## Limites que NÃO mudam com a amostra

- A igualdade de **80 bytes** não identifica criptograficamente todos os bytes de código/dados mapeados, todas as DLLs, o runtime .NET, JIT, relocations e páginas alteradas após a coleta.
- `MEM_IMAGE` não implica necessariamente que páginas copy-on-write permaneçam compartilhadas ou iguais ao arquivo original (ver documentação `VirtualQueryEx`).
- O caminho reportado por um módulo não é um testemunho criptográfico independente do objeto NTFS nem da história de execução. Há corridas temporais entre a amostra e a execução, e a amostra não faz medição contínua.
- Mesmo eventual aprovação do novo job mostrará somente **correspondência do cabeçalho e 16 bytes de entrypoint em uma observação**. Não provará todos os bytes executados, resolução definitiva de TOCTOU ou prontidão de produção.
- `UnknownBlocked/HOLD` permanece contenção; `READY=false`, sem recuperação, rollback, limpeza, retry, merge, deploy, publicação ou WORK.

## Verificação subsequente autorizada

Ler a conclusão real do [run 41227264731](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41227264731), em particular o job NTFS, procurando `GF_E2E_OWNED_MEM_IMAGE_SAMPLE_PASS` e a checagem `GF_E2E_NTFS_PIN_WINDOWS_PASS`. Se falhar, colher apenas código sanitizado do erro real e manter a prova nova inconclusiva. Não alargar o teste sem contraexemplo objetivo e autorização. Conferir também CIs 41227264679/41227264831 e antiga 41226223439 para não ocultar pendências.

## Superfícies preservadas

Sem escrita na Draft privada `429695537`, ZIP `694233137`, manifesto `694233139`, `main`, PR #65 Draft, BETA .66, Cloudflare, computadores pessoais, H1/H2/H3, TUF/TOCTOU/NTFS, recebimento/instalação de produção ou estado operacional. **READY=false**. Este checkpoint documental não constitui homologação.
