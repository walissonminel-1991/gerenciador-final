# CHAT ALTO — GF-UPDATE-HOLD-001: diagnóstico seguro de instalação ausente após interrupção

**Data:** 31/10/2026. **Escopo:** laboratório privado GitHub Actions Windows, SOMENTE LEITURA após a interrupção. Não é recuperação operacional.

## Identidade

- Repositório: `walissonminel-1991/gerenciador-final`; única branch experimental `experimento-g5-ipc-chat-alto-20261029`.
- Commit técnico verificado: `d69cc26d7ef7bd96d728138447cda4de402cfbcf`.
- CI Windows `41200545344`, job `124535312766`: **completed/success**, SHA técnico `d69cc26d7ef7bd96d728138447cda4de402cfbcf`.
- [CI e logs oficiais](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41200545344).
- Release Draft existente `429695537`, tag `gf-e2e-c5057bbbaf1d`: `draft=true`, `published_at=null`, cinco assets.
- Pacote NEW original `gf-e2e-new-win-x64.zip`, asset `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto de teste original `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Somente metadados TUF de teste foram renovados na Draft, não o ZIP ou manifesto; nenhuma publicação/merge/deploy.

## Missão e alteração técnica

**Objetivo exclusivo:** reconhecer e explicar de modo simples e conservador que, após o corte no intervalo físico de renomeação, o diretório portátil originalmente usado pelo GF não existe. Não lançar GF, não reinstalar, não tentar recuperar OLD/NEW, não apagar dados, não promover READY.

Alterações no commit técnico acima:

1. `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt`: somente no cenário `PhysicalInstallBetweenMovesCut` do **mesmo L18**, depois de dois resultados `UnknownBlocked`, confirmação do motivo `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, seleção `new/2`, fences, ausência de NEW lançado, confirmação de processo owner morto, pins NTFS intactos e canário preservado, grava **exclusivamente** dois registros de orientação no `RUNNER_TEMP`: `gf-e2e-installation-missing-guidance.txt` (texto português para leitura humana) e `gf-e2e-installation-missing-guidance.json` (informações técnicas sem caminhos, credenciais ou dados pessoais). Usa o método `L18Coordinator.DurableRealFile` já existente, `CreateNew` e flush. Não altera recuperação, seletor, diário ou código de distribuição.
2. `.github/workflows/gf-github-draft-e2e.yml`: acrescenta somente **uma etapa de diagnóstico de leitura** após a prova preexistente de contenção. Reconfere contra o manifesto TUF original os 284 arquivos NEW, contra inventário pré-start os 284 OLD, verifica canário, seletor `new/2`, fences, resultado e motivo `UnknownBlocked`, ausência do caminho original e do recibo de atualização concluída. Verifica esquema JSON, mensagem portuguesa exata e todas as flags `false` de rollback/retry/cleanup/recovery/READY. Compara hashes e inventários antes/depois da leitura e imprime orientação e marcador de sucesso nos logs. Não executa GF nem o reconciliador novamente.
3. Não houve criação de outro seletor, de outra arquitetura de atualização, nem de um reparador.

## Aviso em português validado

**Código de diagnóstico:** `GF-UPDATE-HOLD-001`

> O Gerenciador Final não conseguiu terminar a atualização.
>
> O programa não pode ser aberto com segurança nesta instalação.
>
> Por segurança, nenhuma tentativa de reparo ou restauração foi feita.
>
> Os arquivos anteriores e o pacote novo foram preservados neste teste.
>
> Não apague pastas do programa nem tente reinstalá-lo por cima.
>
> Peça orientação ao responsável pelo Gerenciador Final.
>
> Informe o código GF-UPDATE-HOLD-001.
>
> Ainda não há uma recuperação segura comprovada.

**Forma de apresentação:** arquivo/registro de diagnóstico **externo no runner privado**. O GF.exe está ausente do caminho original; portanto, **não foi aberta uma janela do próprio GF**. O texto é uma prova de experiência de comunicação do estado em laboratório, não um recurso de suporte/distribuição funcional ao usuário final. Os arquivos de diagnóstico não foram distribuídos nem enviados para serviço remoto.

## Evidências de CI 41200545344

Job `124535312766` concluiu todos os passos relevantes `success`: build normal protegido por `GF_E2E_LAB`, TUF de teste, entrada `MainForm.Shown`, download/autenticação, corte físico do owner após a primeira renomeação, dupla contenção L18, verificação do diagnóstico e negativos adulterado/truncado.

Marcadores observados:

```
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_CUT_L18_REPLAY_HOLD original_selector=new/2 outcome=UnknownBlocked replay=UnknownBlocked rollback=false retry=false cleanup=false READY=false
GF_E2E_MISSING_INSTALL_READONLY_VERIFIED code=GF-UPDATE-HOLD-001 old_files=284 new_files=284 canary=UNCHANGED selected=new/2 original_path=ABSENT no_recovery_no_retry_no_cleanup=true READY=false
GF_E2E_USER_GUIDANCE_PT_BR_START
GF_E2E_USER_GUIDANCE_PT_BR_END
GF_DRAFT_NEGATIVES ... modified rejected=true ... interrupted BadZipFile rejected=true
```

A CI comprova, no runner, que:
- Os arquivos OLD e NEW permaneceram completos e intactos em **locais separados**.
- O caminho executável original ficou **ausente** e o NEW **não** foi executado depois do corte.
- O canário de dados e o seletor/fences não foram alterados pelo diagnóstico.
- O reconciliador já havia bloqueado em `UnknownBlocked` duas vezes; não houve rollback, retry, limpeza ou recuperação.
- O texto em português foi escrito corretamente, corresponde ao JSON e não divulga caminhos nem tokens.
- A execução do diagnóstico não requer login GitHub, embora todo o experimento continue dependente do token efêmero da CI para o download da Draft.

## Limites e pendências

**SUCCESS** desta CI significa **sucesso da detecção e comunicação do estado bloqueado**, não sucesso de autoatualização ou de recuperação. O programa ficaria sem executável no caminho original; o texto é acessível só na infraestrutura de teste. Nenhuma recuperação/manual segura foi demonstrada ou prometida.

Não foram demonstrados: durabilidade real em queda elétrica e reinício, recuperação em máquina de usuário, detecção segura sob corrupção de OLD/NEW/canário, H1/H2/H3, quiescência de efeitos externos, TOCTOU contra mesmo usuário, autenticidade TUF pública de produção, download de Draft sem token, e interrupção HTTP ao vivo (negativo `interrupted` continua ZIP truncado). `HOLD/UnknownBlocked` nunca vira autorização de rollback/limpeza/retry/READY.

**Próxima prova restrita sugerida:** demonstrar que o diagnóstico **não afirma preservação nem gera tranquilização falsa** se um dos arquivos ou o canário estiver ausente/adulterado. Essa hipótese deve ser testada por alteração de arquivos *inertes* na VM descartável, com rejeição fail-closed, sem reparo ou nova arquitetura. É uma sugestão, não parte da CI aprovada.

Restrições respeitadas: sem toque em `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, infraestrutura real ou chaves; sem release pública, merge, deploy, WORK ou coleta de dados pessoais.
