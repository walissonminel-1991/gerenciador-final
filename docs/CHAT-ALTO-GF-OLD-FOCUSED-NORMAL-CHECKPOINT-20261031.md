# CHAT ALTO — GF OLD comanda transição normal real OLD→NEW pelo mesmo C# L18

**Data:** 31/10/2026. **Escopo:** Windows descartável GitHub Actions, TESTE SOMENTE. Nenhum READY operacional.

## Identidade

- Repositório `walissonminel-1991/gerenciador-final`; **única branch de escrita** `experimento-g5-ipc-chat-alto-20261029`.
- Commit técnico final da prova focada: `604ead49add595aac4c1c2747931cba9d1c7c92a`.
- Release DRAFT preservada: ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`.
- ZIP NEW original: `gf-e2e-new-win-x64.zip`, asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original: `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Os três role-assets `targets.json`, `snapshot.json`, `timestamp.json` foram renovados exclusivamente com chaves TUF públicas de TESTE. Não mudar ZIP/manifesto nem publicar a Draft.

## Evidência de integração completa anterior: CI 41168904327

[CI Windows 41168904327](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41168904327), job `124442392769`, SHA `2a345e98a07b672c6922d70b43aedf47ac3357b1`: **completed/success**.

A aplicação **GF OLD real** `5.13.11.65`, em compilação `GfE2eLab=true`, iniciou `MainForm.Shown`, baixou do GitHub pelo próprio `.NET HttpClient`, invocou o Python-TUF de laboratório e depois lançou **o mesmo C# L18** por `ProcessStartInfo`; o workflow somente preparou os recursos e leu os relatórios. TUF independente autenticou 284 arquivos do GF NEW real `5.13.11.66`. O relatório validado pelo OLD passou **29/29** casos `PASS_CONTAINMENT`, `failed=0`, `READY=false`; o cenário normal ficou `UnknownBlocked` e preservou OLD e dados-canário. Dois testes negativos (`modified` e `interrupted`, com cópia do ZIP truncada) foram rejeitados, sem executar o NEW adulterado. A observação completa fica nos logs/recibos da CI, não em READY.

## Prova de transição normal focalizada: CI 41170214699

[CI Windows 41170214699](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41170214699), job `124446351193`, commit `604ead49add595aac4c1c2747931cba9d1c7c92a`: **completed/success**.

Alterações técnicas, sem criar nova arquitetura:
- `Phase1RecoveryTests.cs.txt`: adicionado **apenas** argumento `--real-normal-focused` à mesma aplicação C# L18; invoca `Scenario("normal",a)` já existente, usando **exatamente** o mesmo `RealOwner`, verificação de bytes e identidade, seletor OLD/NEW, diário, Job Objects e reconciliador; gera relatório `gf-real-single-normal-lab/1`, `passed=1`, `failed=0`, `PASS_CONTAINMENT`, `UnknownBlocked`, `READY=false`. A opção anterior `--real-suite` continua disponível com seus 29 casos.
- `GfOldDraftAcquire.cs.txt`: depois de baixar e autenticar NEW por TUF, o próprio GF OLD solicita um único cenário ao C# L18; valida `normal_owner_completed_without_crash`, `reconciler_new_process`, `old_and_canary_preserved`, `manifest_and_object_identities_rechecked`, `UnknownBlocked`/replay e ausência de qualquer autorização operacional. Recibo `gf-e2e-old-owns-l18-handoff/2`.
- Workflow da mesma CI prepara bridge C# e fixtures, inicia GF OLD pelo evento real `MainForm.Shown`, espera o recibo que o OLD produziu, verifica TUF novamente em diretório independente e confere os registros de L18, sem executar a transição outra vez. Os negativos são mantidos.

Evidências dos logs:
- `GF_E2E_GF_OLD_MAINFORM_SHOWN_TUF_AND_EXISTING_L18_COMPLETE focused_normal=PASS_CONTAINMENT ready=false`.
- `GF_DRAFT_AUTHENTICATED`: `extracted_files=284`, `downloaded_archive_sha256=3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`, `old_files_preserved=true`, `operational_ready=false`.
- `GF_E2E_OLD_OWNED_PYTHON_TUF_AND_L18 old=5.13.11.65 new=5.13.11.66 focused_normal=PASS_CONTAINMENT ready=false`.
- `GF_DRAFT_NEGATIVES`: `modified` e `interrupted` rejeitados, `downloaded_asset_executed_on_negative=false`, `rollback_authorized=false`.

A [CI 41169999263](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41169999263) falhou **apenas** na validação final do workflow, porque ainda exigia relatório antigo `gf-e2e-old-owns-l18-handoff/1`, `29` casos. O startup e a autenticação haviam aprovado. Corrigidos somente os campos esperados para `/2`, `1` no commit `604ead49add595aac4c1c2747931cba9d1c7c92a`; CI 41170214699 confirmou a correção completa. Não houve alteração do código do L18/coordenador para reparar a falha de relatório.

## Conclusões verificáveis e limites não resolvidos

**Prova positiva restrita:** GF OLD real em `MainForm.Shown` iniciou, com comunicação HTTPS e TUF de TESTE, uma transição `normal` em uma instalação **privada de laboratório** executada pelo coordenador C# real. O fluxo testou OLD e NEW reais e impôs a mesma transação/seletor/observação/recuperação do L18. Nenhuma liberação de `READY`, rollback ou auto-limpeza.

**Ainda NÃO demonstrado:**
- O GF instalado no computador de um usuário substituir sua **própria instalação real** e reabrir a interface NEW como versão ativa de uso contínuo. O L18 atualmente instancia diretórios/seletores de instalação fixture e observa OLD/NEW em laboratório.
- Autoridade TUF de produção: as chaves de laboratório são públicas; o token GitHub da Draft é efêmero, restrito à CI e nunca pode ser enviado ao GF distribuído.
- Teste de interrupção **HTTP ao vivo**: o negativo atual usa ZIP truncado. Ainda não é equivalente a corte da conexão no meio do download.
- Prova positiva de item específico da interface personalizada; `READY` operacional, recuperação segura em power loss, H1/H2/H3, proteção contra reversão pelo mesmo usuário/TOCTOU não estão demonstradas.
- O processo OLD real da inicialização foi encerrado pela CI de teste quando necessário; esse encerramento de limpeza não constitui prova de desligamento gracioso do próprio processo de startup. O cenário `normal` interno ao L18 verifica desligamento gracioso dos GF OLD/NEW que ele observa, não do OLD original da CI.

## Próximo objetivo EXCLUSIVO

Demonstrar em um único runner Windows descartável, **sem criar outro motor**, que o mesmo GF OLD, após autenticar o NEW, transfere controle para o coordenador L18 e **encerra sua própria instância de forma graciosa**, com uma seleção NEW real reutilizável e verificação independente de que a janela funcional da versão NEW é apresentada. Distinguir estado de laboratório e operação distribuível; qualquer evidência inconclusiva deve terminar em HOLD/UnknownBlocked. **Não implementar rollback automático nem distribuir executáveis antes dessa prova.** Não repetir os 29 cenários sem regressão concreta.

## Restrições permanentes

`main`, PR #65 Draft, BETA .66, Cloudflare e computadores pessoais intocados; sem release público, merge, deploy, credenciais persistentes ou alteração do código de produção. Nenhum WORK consumido. Preservar H1/H2/H3 e TOCTOU como não demonstrados, nunca promover `UnknownBlocked` a `READY` ou recuperação. Continuar com botões de avanço e CI.
