# Lote 22 — OLD fixada até a decisão (protótipo inerte)

**NÃO há execução, instalação ou atualização do aplicativo GF real.** Este componente só demonstra uma garantia local NTFS em fixture com texto inerte dentro do diretório L14 descartável.

## Alterações

`WindowsFileGuard.OpenLabOld(lab)` é uma entrada **limitada ao caminho fixo `L14Model.Old(lab)`**. Exige `L14Model.RequireRoot`, usa o algoritmo NTFS já existente (cadeia de diretórios pinada, no-follow, File ID, single-link, volume e ACL). A rota padrão `Open` continua exigindo seu marcador `FixtureSandbox.RequireRoot`; não existe `OpenLab` genérico.

`L18Coordinator.Probe` abre OLD **antes** do `L15Binding.Guard` para não criar uma janela de escrita entre a leitura de laboratório e o primeiro pin. O `PinnedFacts` é dono dos handles OLD, staged e seletor até a decisão. `Recheck` valida File ID e bytes exatos de OLD, além dos checks anteriores de staged e seletor. Exceções fecham os handles.

## Testes direcionados

- L22-01: callback `selectorObserved` tenta escrita in-place com o mesmo File ID OLD após a inspeção inicial. Só o reconciliador mantém o pin; a escrita é bloqueada; `OldVerified` exige OLD intacta e canário pessoal fictício preservado.
- L22-02: conclusão inerte NEW seguida de novo reconhecimento NEW com o mesmo ataque a OLD. Escrita bloqueada, NEW local consistente e OLD intacta.
- L22-03: bytes OLD alterados **antes** da reconciliação, preservando File ID NTFS. Resultado UNKNOWN, não OLD; restaura exclusivamente a fixture OLD para validar o teardown e confirma bloqueio local conservador na segunda chamada.

Executar as regressões L18 (7), L19 (5), L20 (4), L21 (3) porque `L18Coordinator` e o adaptador NTFS foram afetados.

## Limites explícitos

Isto é proteção de abertura/leitura no Windows runner descartável, **não** prova de resistência a escritor hostil completo do mesmo usuário, reparse universal, alteração anterior à abertura, whole-disk restore/clock rollback, power loss, I/O kernel sobrevivente, assinatura de PE, READY, IPC, execução de GF ou homologação operacional. Nenhum gate G1–G8 é fechado.
