# L20 — Barreira única de autorização e decisão NTFS (apenas inerte)

## Escopo
Somente protótipo C#/.NET 10 de fixture TUF Stage11/Stage3, sem GF real, READY, instalação, elevação, serviço permanente ou backend. O componente L18Coordinator passa a usar um `coordinator-lease` de arquivo, separado do lease L16 do proprietário, para serializar `Begin`, `Execute` e `Reconcile` entre processos cooperativos. Execute mantém a barreira durante espera e reconciliação. Begin segura adicionalmente o lease L16 durante a preparação; a passagem ao filho ocorre sem reivindicar atomicidade universal do handoff.

Antes de autorizar qualquer nova tentativa, `BeginCore` verifica ambos bloqueios locais L17/L19, integridade do diário e ausência de transação anterior. Um marcador de recuperação impedirá nova atualização, não apenas `Reconcile`.

`Probe` devolve objeto descartável com `WindowsFileGuard` aberto no seletor NTFS. `Reconcile` segura o handle durante toda decisão OLD ou NEW, mantém o primeiro pin durante reobservação posterior e rejeita divergência de identidade/conteúdo. As operações de arquivo por caminho são rechecadas, não declaradas completamente livres de TOCTOU.

## Evidências experimentais exigidas
1. Marcador de recuperação L19 impede `Begin` e preserva canário.
2. Segundo processo mantém lease de coordenador; `Begin` recusa e `Reconcile` devolve BUSY, depois OLD íntegra continua observável.
3. Troca/reescrita concorrente do seletor a partir de callback de observação é recusada pelo handle NTFS ainda aberto até a decisão OLD.
4. Proprietário L16 já detém lease e impede nova autorização.

Reexecutar regressões L18 (7) e L19 (5), pois L18Coordinator mudou. Sem repetir desnecessariamente L11–L17.

## Limitações
A gate local não é inviolável por escritor malicioso do mesmo usuário. Há janela de troca entre validação do arquivo de gate e sua abertura por nome, assim como riscos de namespace antes de adquirir um handle. Nenhuma garantia de resistência a restauração total, power loss, I/O de kernel pendente, reparse universal, certificados/assinaturas GF real ou READY. **G1–G8 BLOCKED (0/8)**.

Sem merge, release, deploy, CI em PCs pessoais ou uso de WORK nesta frente.
