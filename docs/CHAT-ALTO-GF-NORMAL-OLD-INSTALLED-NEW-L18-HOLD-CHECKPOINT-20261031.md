# CHAT ALTO — OLD aberto NORMAL instala e executa NEW real no caminho portátil, mesmo L18, termina em HOLD

**Data: 31/10/2026. Prova funcional específica concluída, Windows GitHub Actions descartável. LABORATÓRIO SOMENTE. READY=false.**

## Referências verificáveis

- Repositório `walissonminel-1991/gerenciador-final`; única branch escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `c9f13e8d0a6e13b3fb7a887a849b8a4332bb7394`.
- Commit de implementação experimental: **`472cde653f0386651033ed7da5386c6fff52cb4b`**.
- Commit de correção **apenas da verificação independente**: **`97e1c2699c03ede235b2d65b1a844d9fa392c395`**.
- **CI Windows final [41209614651](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41209614651)**, `completed/success`, SHA técnico `97e1c2699c03ede235b2d65b1a844d9fa392c395`.
- Job existente `gf-old-initiated-draft-real`: **124561667778**, `SUCCESS`; continua a comprovar corte físico entre renomeações e contenção conservadora.
- Job novo, `gf-normal-install-focused` / `OLD normal abre, L18 instala e observa NEW real em outro runner Windows`: **124562417060**, `SUCCESS`.
- CI preliminar [41209091834](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41209091834): `FAILURE` **no verificador adicional**, não prova aprovada. Causa e correção detalhadas abaixo.

## Uma única lacuna funcional resolvida

**ANTES:** OLD real `5.13.11.65` em abertura normal já baixava e autenticava a Draft e passava pela seleção protegida `NEW/2` antes de fechar, mas o cenário real de execução após substituição no **caminho portátil original** ainda não havia sido comprovado a partir dessa abertura normal. A CI principal preexistente executava o corte intencional ENTRE as duas renomeações, produzindo `HOLD`.

**AGORA:** o segundo job usa **outro runner `windows-2022`** e prepara apenas suas próprias dependências, GF OLD de laboratório e a **mesma ponte C# L18**. Não clona nem muta os diretórios ou seletores do primeiro job. O OLD abre sem `--tray` e sem `ShowWindow` sintético. A inspeção Win32 externa confirma o HWND próprio, imagem exata do processo e janela nativamente visível e responsiva. O `MainForm.Shown` real aciona o downloader `.NET HttpClient` já existente, que baixa os cinco assets autenticados da Draft real e privada, e em seguida inicia o verificador Python-TUF existente. A ponte já implementada `--real-install-focused` é acionada pelo mesmo OLD, com:

- `GF_E2E_HANDOFF_L18=1`, `GF_E2E_SELF_GRACEFUL_CLOSE=1`, `GF_E2E_SELECTED_NEW_CONTINUATION=1`, `GF_E2E_LIVE_INSTALL_SWAP=1`.
- `GF_E2E_SWAP_CUT_AFTER_OLD_MOVE=0` **somente neste runner**, preservando o corte ativo no job anterior.
- `GF_E2E_POST_OWNER_PERSISTENCE=1` para conservar a fixture e ler o **mesmo** seletor, manifesto transacional e launch fence após o encerramento do coordenador, exclusivamente como prova de laboratório.
- Sem outro downloader, outro motor de atualização, outro seletor, rollback, recuperação, cleanup, retry ou promoção a `READY`.

O L18 mantém o controle original de seleção `NEW/2`, fence de possível execução, observação do encerramento por handle original, verificação do conjunto completo de arquivos e renomeação **OLD original → backup**, **NEW autenticado → caminho portátil original**. Em seguida, executa e observa funcionalmente o **NEW instalado no local original**, com pesquisa/inventário somente leitura, mantendo `UnknownBlocked` após a observação. O OLD pré-update é preservado na cópia de backup.

**Os dois jobs foram aprovados no mesmo run final.** A primeira prova de corte físico continua ativa e aprovada; a nova prova positiva não a substitui.

## Logs literais do job positivo, 31/10/2026

```text
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_NORMAL_INSTALLED_NEW_BYTES_PASS old_normal=true old_graceful_exit=true installed_new_path=true files_old=284 files_new=284 installed_equals_tuf=true new_executed_installed_path=true readonly_health=true canary_preserved=true HOLD=UnknownBlocked recovery=false rollback=false cleanup=false retry=false READY=false
GF_E2E_NORMAL_INSTALLED_NEW_PASS old_normal=true new_version=5.13.11.66 installed_executed=true L18=same HOLD=true READY=false
```

Os logs confirmam o caminho executado e os checks pós-processo; a prova independente reextrai o NEW com o **verificador TUF existente**, compara todos os 284 arquivos pelo comprimento e SHA-256 com o diretório portátil instalado, compara os 284 arquivos OLD preservados com a linha de base anterior, verifica canário de dados e autentica ZIP/manifesto contra hashes fixos. Também lê novamente o mesmo manifesto transacional L18, seletor `new/2`, lançamento cercado, registro de instalação e evidência funcional `observed_from_live_installation=true`. **Nenhum recibo por si só concede READY ou autoridade externa independente.**

## Primeira falha observada e correção objetiva

- A CI inicial `41209091834`, no SHA `472cde653f0386651033ed7da5386c6fff52cb4b`, obteve uma janela NORMAL, saída graciosa do OLD `exit0` e `GF_DRAFT_AUTHENTICATED`. O L18 gerou seu relatório de instalação positiva, mas o verificador adicional falhou na **linha 48** do script Python embutido em `github_install_focused_normal.ps1` com `AssertionError`. **Não tratamos esta execução como prova concluída.**
- Erro: a asserção comparava `selector["manifest_sha256"]` — digest do **manifesto transacional L18**, assinado e vinculado a `installation/transaction` — com o SHA do **manifesto original da Draft**, que é outro documento autenticado para outra finalidade.
- A correção `97e1c2699c03ede235b2d65b1a844d9fa392c395` passou a calcular independentemente SHA-256 dos bytes `real-auth/GF-LAB-REAL-PACKAGE-MANIFEST.json` existentes na mesma fixture retida pelo L18, exigindo correspondência com `selector.manifest_sha256`, `schema=gf-real-package-lab/3`, instalação/transação, `selected_before=old`, `operational_installable=false` e 284 entradas em ambos os conjuntos. **Não removeu nem relaxou** o hash/ID original da Draft, o TUF independente, o confronto completo de bytes OLD/NEW ou as barreiras L18.
- A workflow inclui agora `github_install_focused_normal.ps1` na lista de caminhos permitidos para disparo; CI final `41209614651` aprovou **ambos os jobs**.

## Alterações exatas

Comparação documental de entrada `c9f13e8d0a6e13b3fb7a887a849b8a4332bb7394` → SHA técnico final `97e1c2699c03ede235b2d65b1a844d9fa392c395`: **dois commits técnicos, somente dois arquivos de laboratório**.

1. `.github/workflows/gf-github-draft-e2e.yml`: mantém o job anterior **intacto em seus testes** e adiciona um segundo job dependente, em novo Windows descartável, apenas para a instalação positiva. Não publica release nem altera o GF distribuído.
2. `prototipos/autoatualizador-seguro/phase1-composed/github_install_focused_normal.ps1`: executor rigorosamente limitado a `GITHUB_ACTIONS` e ao repositório explicitamente indicado; compila OLD `5.13.11.65` com `GfE2eLab=true`, prepara ponte L18 inalterada, realiza observação Win32 antes da passagem, verifica `NEW/2`, `FormClosed`, `Application.Run`, `exit0`, relatório L18, bytes/versões, canário, launch fence e `UnknownBlocked`.

**Código inalterado:** `Program.cs`, `GfOldDraftAcquire.cs.txt`, `Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt`, implementação L18, verificador TUF de produção e seletor persistente. O binário distribuído normal não contém o gancho `GF_E2E_LAB`.

## Ativos protegidos e limites

- Draft **429695537**, tag `gf-e2e-c5057bbbaf1d`: `draft=true`, `published_at=null`, cinco assets.
- ZIP `gf-e2e-new-win-x64.zip` asset **694233137**, tamanho **57.695.630 bytes**, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original `GF-LAB-REAL-PACKAGE-MANIFEST.json` asset **694233139**, tamanho **72.618 bytes**, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Somente `timestamp.json`, `snapshot.json` e `targets.json` de TUF de **TESTE** foram renovados pela workflow antiga; ZIP e manifesto originais retiveram IDs, bytes e tamanho.
- Proteções H1/H2/H3/TOCTOU **não foram enfraquecidas**, mas **não estão resolvidas como garantias operacionais**; efeitos externos do NEW, poder de recuperação, durabilidade física/reboot, confiança criptográfica de produção, ataque pelo mesmo usuário, retomada automática e tratamento de falha permanecem pontos abertos.
- Nenhum rollback, recuperação, limpeza, repetição automática, `READY`, merge, deploy ou publicação autorizados. O OLD permanece preservado. Sem toque em `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, infraestrutura pública ou credenciais. Sem WORK.
- A saída `UnknownBlocked/HOLD` é **contenção conservadora, não falha de download nem confirmação de atualização pronta para usuários**. Não se pode apresentar o GF distribuído como autoatualizável automaticamente.

## Próxima continuidade controlada

A prova do ciclo **MainForm normal OLD → Draft privada real → TUF TESTE → mesmo L18 NEW/2 → saída graciosa OLD → instalação NEW no caminho original → execução NEW e saúde somente leitura → HOLD** está concluída e **não deve ser repetida** sem um novo contraexemplo.

Próximo trabalho deve escolher apenas uma lacuna operacional de segurança ainda não comprovada (ex.: contraponto verificável sobre vínculo de bytes executados, durabilidade/efeitos externos ou transição de confiança após instalação), sem autorizar recuperação, rollback, limpeza, retry, READY, publicar pacotes ou usar PCs pessoais, e somente no Windows GitHub Actions descartável. O objetivo final permanece a autoatualização real segura e simples para usuários, não ampliar indefinidamente o laboratório.
