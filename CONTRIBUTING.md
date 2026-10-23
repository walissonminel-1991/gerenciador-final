# Contribuindo

Obrigado por ajudar a manter o Gerenciador Final seguro e fácil de usar.

- Antes de grandes alterações, abra uma Issue explicando a necessidade.
- Faça pull requests pequenos, com motivação, testes realizados e capturas da interface quando relevante.
- Não inclua binários compilados, senhas, tokens, certificados ou dados pessoais nos commits.
- Não proponha desativar antivírus, UAC, firewall, Defender, logs de segurança, restauração ou serviços em massa com a justificativa de “otimização”.
- Alterações que excluem arquivos ou modificam rede/registro devem ter confirmação, mensagens claras e tratamento de falhas.
- Consulte o fluxo de compilação Windows e documente os testes; um build aprovado não substitui testes de segurança.
- O mantenedor verifica e aprova contribuições antes de integrá-las. A política de aprovação de releases assinadas será formalizada somente após aceitação no programa SignPath.

As discussões devem manter respeito e foco em fatos técnicos, sem exposição de dados privados dos usuários.

Para preparar o ambiente, use o SDK indicado em `global.json`. As suítes são
executáveis de console (não usam `dotnet test`); os comandos vigentes estão em
[TESTES](docs/TESTES.md). Antes de remover código, confira os curingas do
projeto, recursos incorporados, testes que vinculam fontes e chamadas por
reflexão. Uma alteração no pacote deve passar também pelos controles de
composição e licenças, mantendo NuGetAudit ativo e a distribuição self-contained.
