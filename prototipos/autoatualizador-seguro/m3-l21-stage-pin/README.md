# Lote 21 — Integridade contínua do staged e pré-validação sem efeitos

**Exclusivamente protótipo inerte Windows/.NET 10 com fixtures Stage11/Stage3 TUF de teste.** Não instala, lança ou atualiza o GF operacional; G1–G8 permanecem BLOCKED (0/8).

## L21-A
O `PinnedFacts` de `L18Coordinator` passa a ser dono de dois handles `WindowsFileGuard`, `stagePin` e `selectorPin`, e dos bytes exatos assinados de fixture. Ambos permanecem abertos entre `Probe` e a classificação (incluindo `Probe` reexecutado). `Recheck` exige a identidade NTFS original e a comparação de bytes exatos do staged autorizado, além da identidade e conteúdo do seletor. Caso haja divergência ou violação de sharing, a decisão é conservadora, sem `LocalConsistentObserved`.

O ensaio adversarial em NEW tenta uma modificação in-place (mesmo File ID) **no callback após Probe e antes da decisão**, sem manter outro pin do próprio teste: a escrita é negada pelo handle do reconciliador. Outro cenário modifica os bytes antes da reconciliação, preservando File ID NTFS; a observação passa a UNKNOWN. Nenhuma dessas evidências significa imunidade universal a escritores hostis do mesmo usuário, reparse/namespace TOCTOU ou power loss.

## L21-B
`Execute` verifica prazo, caminho exato e nome da imagem do fixture antes da aquisição da gate de coordenador e antes de qualquer histórico. O executável exige mesma resolução de caminho do `Environment.ProcessPath`, imagem esperada e arquivo regular existente. Essa é uma **allowlist de caminho/identidade da imagem de teste**, não autenticação criptográfica do binário ou garantia contra troca de nome pelo mesmo usuário.

Teste verifica prazo zero, prazo maior que o limite, imagem falsificada com mesmo basename em outro diretório, imagem inexistente, ausência de eventos de transação e nova tentativa legítima depois das recusas.

## CI e limites
O workflow `autoatualizador-m3-l21-stage-pin-inerte.yml` roda apenas em `windows-2022` com SDK/fixtures fixados, verifica três cenários novos e que o GF não compila o protótipo. A alteração no reconciliador aciona também as regressões L18 (7), L19 (5) e L20 (4). Confirmar resultados reais no GitHub, sem inferir aprovação.

**Não demonstrados:** saúde e atualização GF real, witness independente, resistência contra rollback de disco/relógio, adulteração completa do mesmo usuário, power loss, invalidação de caches TUF em todas as implementações, universal ausência de efeitos pendentes. Sem WORK, merge, release, deploy, BETA .66 ou infraestrutura.
