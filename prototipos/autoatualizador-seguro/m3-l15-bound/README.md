# L15 — Stage11 TUF + Stage3 + NTFS + L14 recovery (INERTE)

Este projeto combina componentes de fixture existentes sem criar um autoatualizador real, API de READY, autorização de instalação ou binário GF distribuível.

**Cadeia técnica de ensaio:** Stage11 (metadata TUF e pacote assinados com chaves EXCLUSIVAMENTE de teste) → Stage3 (binding assinado entre target ZIP/entry, journal Stage9 e intenção Stage2) → WindowsFileGuard (open NOFOLLOW, identidade NTFS, links, ACL e hashes no momento da inspeção) → recibo de diagnóstico local que inclui também digest de textos L14 e identidade da transação → child Windows L14 que pode ser encerrado de verdade → recuperador distinto que repete todas as verificações antes de chamar a recuperação inerte do L14.

As verificações reabrem os registros Stage11 e recompõem o binding Stage3, lendo os mesmos bytes de ZIP e candidato com handles. O recibo L15 NÃO é uma assinatura independente, witness persistente, nova root, proteção antirollback após restauração integral, nem capacidade de impedir um adversário de mesmo usuário que reescreva todo o estado local. Os bytes do texto novo L14 NÃO pertencem ao target TUF assinado. Portanto isto valida uma *associação observacional*, não a origem criptográfica do binário GF futuro.

**Corrida controlada:** um child verifica o vínculo e pausa; outro processo substitui o arquivo assinado por um novo inode NTFS com bytes iguais; após retomar, o child verifica novamente e recusa. Isso NÃO prova eliminação de todas as janelas TOCTOU do fluxo L14, que não revalida TUF dentro do File.Replace. Outra janela poderia existir depois do segundo check. É intencionalmente mantido como gate BLOQUEADO.

Os testes só criam/modificam arquivos temporários com marcador gf-u2-l15 e gf-u2-l14. O runner Windows .NET é ele próprio executável de teste, não produto. Não há rede, assinatura operacional, criptografia local independente, instalador, privilégio, PowerShell de atualização, helper, chamada a APIs do GF, deploy, merge ou mudança da BETA. Não são realizados tests de energia desligada, manipulação de ACL real, Program Files, NTFS race arbitrária, TUF normativo interoperável nem crash de I/O de kernel.

**Interpretação:** casos que retornam UnknownBlocked são sucessos de recusa, não uma atualização exitosa. L14 pode restaurar um selector FICTÍCIO sem terminal; um terminal FICTÍCIO íntegro permite apenas observar conteúdo novo inerte, nunca executar. A versão antiga e os marcadores simulados do usuário permanecem preservados.

G1–G8 BLOCKED (0/8). WORK não utilizado.
