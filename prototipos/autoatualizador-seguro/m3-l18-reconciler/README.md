# Lote 18 — reconciliador de estado local (protótipo inerte)

**NENHUMA integração com Gerenciador Final.** Este módulo de testes Windows/.NET 10 somente utiliza fixtures assinadas Stage11/Stage3 e as operações fictícias do L16. Nenhuma execução ou atualização GF real, READY, instalação, helper elevado, serviço, backend, chave de produção ou implantação.

## Fronteiras e semântica

A implementação `L18Coordinator` (não o supervisor de testes) inicia o proprietário e persiste intenção **antes** de iniciar efeitos. Registra a conclusão observada do processo e reavalia o estado real via L15/TUF/staged pinado + seletor NTFS + terminal auxiliar. Um registro local isolado não basta para confirmar NEW. A confirmação produz apenas `LocalConsistentObserved` (fixture), nunca READY operacional.

`Busy` significa lease ocupado, não altera o seletor. Quando a espera expira, o próprio coordenador grava `timeout`, cria o marcador local `l17-unknown.fixture.txt` e registra `unknown`, sem matar nem afirmar encerramento do proprietário. Um proprietário pode concluir tardiamente; reconciliação continua bloqueada. Se o processo de recuperação cai após `reconciling`, a próxima execução reobserva os arquivos e evita reaplicar o seletor; a presença de `owner-exited` observado permite reconhecer o estado inerte após provas suficientes. Se `owner-exited` não foi registrado, não infere seu conteúdo por mera existência de recibo.

O histórico explícito separa OLD observado, autorização, preparação de fixture L16 preexistente, staged verificado, intenção durável, switching, owner-launched, owner-exited, switched-unconfirmed, reconciliação e estados finais. Registros também vinculam identidade NTFS staged e digest de OLD. O PID associado a owner-launched é exclusivamente contexto diagnóstico; não é identidade de segurança. Após falha conhecida com OLD verificada, reconciliação posterior classifica OldVerified de forma idempotente.

As fases são registros imutáveis numerados de teste, com `FileMode.CreateNew`, `WriteThrough`, `Flush(true)`, validação de formato e sequência. Isso é persistência local **cooperativa**, não armazenamento antifadiga física, antirrollback, nem imune a adulteração pelo próprio usuário. A corrida entre scratch fechado e `File.Replace` no L16 segue aberta sob escritor hostil. Uma operação kernel I/O pendente que sobreviva ao processo não está excluída pelo lease: deve permanecer UNKNOWN na arquitetura operacional.

## Cobertura de laboratório

- NEW honesto: processo encerrado, byte TUF/staged pinado e seletor NEW conferidos; recibo apenas auxiliar; leitura repetida idempotente.
- Recibo NEW fabricado após kill sem `owner-exited` durável: UNKNOWN.
- Recibo depois adulterado: a reobservação deixa de reconhecer o resultado.
- TIMEOUT gerenciado internamente, BUSY observado enquanto owner ativo, conclusão tardia preserva UNKNOWN.
- Interrupção durante RECONCILING e nova reconciliação sem reaplicar a transação.
- Falha conhecida antes da troca é distinta de UNKNOWN; revalidação posterior reconhece OLD sem reaplicar escrita.
- Escrita restrita a nomes fixos sob root de fixture; canários pessoais fictícios fora do root preservados e arquivo de fase preexistente não é sobrescrito.
- As regressões L16/L17 permanecem isoladas e não são acionadas por alterações apenas neste módulo.

## Limites formais

A implementação **não** prova defesa contra escritor hostil do mesmo usuário, power loss, totais restaurações de disco/relógio, impersonação, race universal de namespace, controle de dispositivo/driver ou confirmação da saúde do GF. A ausência de efeitos pendentes no laboratório é premissa do proprietário cooperativo e do processo terminado; não há prova de término universal de I/O de kernel. Não há autoridade de conclusão independente; o histórico local pode ser falsificado ou substituído integralmente por escritor com as mesmas permissões.

**G1–G8: BLOCKED (0/8).** PR #65 Draft, main, BETA .66 e infraestrutura permanecem fora deste escopo.
