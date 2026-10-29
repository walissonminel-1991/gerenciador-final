# Lote 19 — reconciliação real de UNKNOWN e diário vazio (somente protótipo inerte)

Implementação localizada no reconciliador `../m3-l18-reconciler/L18Coordinator.cs.txt`, aproveitando as fixtures assinadas de teste Stage11/Stage3, o L15/L16 e a suíte L18. **Não atualiza, executa ou instala o GF verdadeiro.**

## Regra da autoridade

A saída de `UNKNOWN` só é permitida quando já existe **evento de saída do proprietário observado pelo coordenador L18**, não existe timeout anterior nem marcador de incerteza de término não verificável, e a nova inspeção TUF de laboratório + staged/NTFS + seletor e recibo auxiliar concordam. Isso apenas demonstra uma **observação local** no domínio cooperativo de fixture; o mesmo usuário pode manipular arquivos de diário e recibos.

O prazo expirado não prova que I/O terminou. Se houve timeout e não há testemunha de término de todos os efeitos, `UNKNOWN` permanece bloqueado, mesmo depois de terminal tardio e desaparecimento de processo. Não existe uma autoridade independente para resolver esse caso, e o lote não tenta inventá-la.

## Mudanças mínimas

- Diário vazio é verificado antes de acessar o último evento. Retorna OLD somente com versão inicial válida e sem intent/terminal L16; caso contrário, bloqueia.
- `UNKNOWN` como evento do histórico não é um bloqueio automático eterno: conflito com `owner-exited` registrado pode ser reconciliado após surgirem evidências locais novamente coerentes. A transição é `unknown -> reconciling -> new-observed` ou OLD comprovado.
- Divergência nova de resultado já observado persiste evento UNKNOWN; leitura/escrita impossível do diário origina marcador `l19-recovery-blocked.fixture.txt` com `CreateNew`, `WriteThrough` e `Flush(true)`. Se também a persistência de fallback falhar, não é declarado um bloqueio durável fictício; a exceção propaga e a limitação deve ser diagnosticada.
- Recebimento de recibo não autenticado por autoridade independente NÃO basta para sucesso; `owner-exited` e observações locais continuam obrigatórios.
- Proteções do L17 contra terminal fabricado, staging TUF e seletor de fixture continuam; não foi adicionado backend, chave, serviço, helper elevado ou publicação.

## Evidências e limites

Cinco ensaios novos de alto valor: diário vazio OLD íntegra; diário vazio com NEW anômala e fallback persistido; NEW honesto seguido de recibo conflitante e nova reconciliação; diário inválido com fallback; falha na escrita do próximo evento, com canário preservado e UNKNOWN persistente. A CI L18 é acionada novamente por alteração no componente e cobre as 7 regressões relevantes.

Não demonstrados: segurança operacional GF, encerramento total de I/O no kernel, power loss, proteção do histórico contra atacante do mesmo usuário, frescor após restore integral, nem defesa universal NTFS TOCTOU. **G1–G8 BLOCKED (0/8)**. PR #65 Draft, main, BETA .66 e infraestrutura preservadas.
