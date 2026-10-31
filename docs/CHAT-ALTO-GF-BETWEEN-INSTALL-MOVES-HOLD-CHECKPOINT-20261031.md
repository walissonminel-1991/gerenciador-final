# CHAT ALTO — Interrupção exata ENTRE as duas renomeações físicas — HOLD comprovado

**31/10/2026. Windows GitHub Actions descartável. Não produção.**

## Identidades verificáveis

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch única autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- **HEAD técnico validado:** `b78b43ad4ec0211ffbf90a32041977901542c3d0`.
- **CI de prova final:** [41199620684](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41199620684), job `124533020752`, **completed/success**. O commit técnico testado é `b78b43ad4ec0211ffbf90a32041977901542c3d0`.
- Release preexistente: Draft ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, cinco assets.
- ZIP NEW imutável: `gf-e2e-new-win-x64.zip`, asset `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto imutável: `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Os únicos assets atualizados nas CIs são os três metadados TUF de **teste**. Nenhuma publicação, merge ou deploy.

## Objetivo exclusivo e método real

Comprovar que a execução experimental da **mesma transação L18** sobre uma instalação portátil real, ao ser interrompida **após** `Directory.Move(live,retained)` e **antes** de `Directory.Move(incoming,live)`, conserva todas as cópias e os dados e bloqueia qualquer restauração, limpeza, relançamento ou READY quando os efeitos externos não são verificavelmente delimitados.

O GF OLD real `5.13.11.65`, iniciado por `MainForm.Shown`, baixou e validou o pacote NEW `5.13.11.66` da Draft preexistente por HTTPS e Python-TUF **experimental**. O mesmo `L18Coordinator.RealOwner`, com o seletor original `new/2`, journal, pins NTFS e `Job Object`, aguardou o encerramento gracioso do OLD. A primeira renomeação preservou OLD em `RUNNER_TEMP/gf-e2e-old-preserved`. No gap, o **corte existente `RealCut("between-install-moves")`** publicou o marcador durável e o supervisor encerrou **somente o processo owner** pelo handle retido, com `ExitCode=91`. A segunda renomeação foi impedida; o pacote NEW permaneceu em `RUNNER_TEMP/gf-e2e-authenticated-new`.

O próprio reconciliador L18 foi chamado duas vezes após a interrupção usando seus Jobs/identidades e retornou `UnknownBlocked` em ambas, devido ao fence de NEW e à ausência de autoridade independente para garantir que os efeitos executados estejam terminados. O journal guarda fase `unknown`; a seleção não foi alterada.

## Verificação independente da CI 41199620684

Todos os passos importantes concluíram com **success**: build/isolamento `GF_E2E_LAB`, preparação dos testes TUF, inicialização do OLD, autenticação NEW, interrupção física no gap, validação autônoma dos diretórios e negativos.

Os logs realmente registraram:

```
GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false
GF_E2E_OLD_INITIATED_DOWNLOAD version=5.13.11.65 draft=gf-e2e-c5057bbbaf1d dotnet=true assets=5
GF_DRAFT_AUTHENTICATED ... extracted_files=284 ... operational_ready=false
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_CUT_L18_REPLAY_HOLD original_selector=new/2 outcome=UnknownBlocked replay=UnknownBlocked rollback=false retry=false cleanup=false READY=false
GF_DRAFT_NEGATIVES ... modified rejected=true ... interrupted BadZipFile rejected=true
```

Além dos logs, o script independente da CI compara integralmente hashes e tamanho dos 284 arquivos OLD com a base anterior ao startup do GF de laboratório, os 284 arquivos NEW com o manifesto original da Draft, e confirma o canário `PERSONAL-KEEP-UNCHANGED.txt` byte a byte. Verifica que o caminho original `gf-e2e-old` está **ausente**, que não existe recibo de instalação concluída, que não houve observação nem execução NEW após o corte, que o seletor é `new/2`, com os fences OLD/NEW preservados, e que as duas reconciliações retornaram `UnknownBlocked`. Exige também `rollback_authorized=false`, `rollback_performed=false`, `automatic_retry_authorized=false`, `cleanup_performed=false`, `operational_ready=false`.

### Histórico técnico desta rodada

- Commit `c694547afe4e9896e3ee424b41ad6c0e4c499569`: implantação inicial do corte físico depois da primeira movimentação do diretório e verificador no workflow. CI [41198628948](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41198628948) falhou por relatório não observado no prazo; não aceitar essa CI como prova.
- Commit `af049f2874a9c5c1e79ca3b4317b89d8bb8fce7d`: diagnóstico de estágios e detecção mais rápida de falhas; CI [41199126318](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41199126318) falhou por `CS0103` na variável `cutStage`, antes de executar a prova.
- Commit `572707e32e304e60806fd4fdd21f96201b64fbad`: corrigiu o escopo da instrumentação; CI [41199324307](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41199324307) mostrou `UnknownBlocked` e razão `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, mas falhou devido a uma asserção indevida que esperava marcador de fallback não criado para um journal íntegro.
- Commit `e5b5a697a762d8394c1350636cf5b3f91bb4996c`: conferência da fase `unknown` no mesmo journal L18; CI [41199579360](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41199579360) passou pelo L18, mas falhou em verificação Python do **antigo** marcador de fallback.
- **Commit validado `b78b43ad4ec0211ffbf90a32041977901542c3d0`**: o workflow passou a verificar a fase durável `unknown` no diário correto em vez do marcador L19 reservado a histórico danificado. CI `41199620684` **SUCCESS**. Não foi necessário mudar a lógica de recuperação.

Nenhuma falha transitória histórica é declarada corrigida além do ponto verificável. A antiga CI `41195335850` havia relatado `IOException` sem causa precisa; o diagnóstico de fase/HResult existe, mas a causa daquela ocorrência segue não comprovada.

## Conclusão estrita

**Comprovado:** interrupção voluntária por encerramento do processo owner exatamente entre as duas renomeações de diretório, OLD e NEW completos preservados em caminhos distintos, dados de teste intactos, caminho original vazio, mesmo seletor/journal/fences intactos, duas reconciliações `UnknownBlocked`, nenhuma restauração, limpeza, repetição ou `READY`.

**Não comprovado:** durabilidade em queda física de energia/Windows, retomada segura quando o caminho executável está vazio, estado READY operacional, proteção TOCTOU contra mesmo usuário, H1/H2/H3, efeitos fora dos Jobs, chaves TUF de produção, e interrupção HTTP real (o negativo existente trunca ZIP). O token para Draft pertence apenas ao runner GitHub Actions. O resultado `SUCCESS` é de **contenção de laboratório**, não de atualização bem-sucedida para usuários.

**Próximo objetivo sugerido, mas não autorizado nesta rodada:** definir uma prova de tratamento **sem recuperação automática** para o caminho de instalação ausente após crash, com diagnóstico acessível ao usuário. Não adotar automaticamente rollback/limpeza nem comprometer dados pessoais. Não criar arquitetura paralela.

**Limites preservados:** não tocar `main`, PR #65 Draft, BETA .66, Cloudflare, computadores pessoais, produção; não merge, deploy, publish/release, credenciais persistentes, READY ou WORK. Não foram necessários recursos WORK.
