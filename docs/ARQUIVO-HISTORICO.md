# Arquivo histórico do GERENCIADOR FINAL

**Biblioteca de referência — não substitui o estado operacional, os testes físicos ou uma homologação.**

Esta biblioteca facilita pesquisar relatórios antigos sem perder seus vínculos. **Nove relatórios foram movidos** para uma pasta de arquivo e **13 continuam em seus caminhos originais**, pois são referenciados por testes de metadados ou pelo registro estruturado `docs/PROGRESSO.json`. Todos os 22 aparecem no índice.

## Navegação

- [Missões históricas: 9 arquivadas, 13 preservadas no local original](historico/missoes/README.md)
- [Roadmap](ROADMAP.md) e [Testes](TESTES.md) — conferir sempre a data, branch e commit de cada resultado
- [Checkpoint da integração](WORK-INTEGRACAO-CHECKPOINT.md)
- [Checkpoint do autoatualizador na PR #65](WORK-AUTOATUALIZADOR-CHECKPOINT.md)
- [Evidências](evidencias/) e [benchmarks](benchmarks/) — preservados nos diretórios originais

## Política de consulta

- Relatórios de uma missão antiga servem como pesquisa e não autorizam novas operações no PC do usuário.
- Os links internos dos documentos movidos e os links externos afetados foram reescritos para os novos endereços.
- Documentos exigidos por `scripts/test_pacote.py` e caminhos armazenados em `docs/PROGRESSO.json` **não foram relocados**; não enfraquecemos os testes nem reescrevemos o registro de progresso.
- Evidências, TUF/NTFS/L18, consentimento, rollback, versões e fontes permanecem intactos.
- A proposta documental deriva da PR #75 Draft no SHA `4452f75c84527882977ce7d75b459ee35d5cde2a`; `main`, PR #65, G5 e roadmap 27/42 não foram alterados.
