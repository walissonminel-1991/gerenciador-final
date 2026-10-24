# Histórico de triagem GitHub — piloto suspenso

O piloto inicial de triagem de Issues públicas existiu temporariamente na PR #40, mas foi **removido na v5.13.11.7 TESTE** por não impedir que participantes acrescentassem texto, capturas e anexos no site. A versão de teste atual não oferece formulário de Issue no aplicativo nem processa o evento `issues` em GitHub Actions.

O antigo parser e seus testes de Issues foram retirados. O workflow que conserva o nome `triagem-diagnostico-github.yml` executa somente regressões de política de segurança sobre o código, sem receber relatórios de pessoas.

Telemetria continua apenas local mediante consentimento, com esquema rigoroso (`DiagnosticPayloadPolicy`) e sem endpoint implantado. A futura arquitetura de envio, se aprovada, deverá operar via payload estruturado, serviço autenticado e rate limit; nunca aceitar upload de arquivos, texto livre nem fornecer instruções diretas à IA.

As Issues públicas do GitHub e seus anexos são recursos da plataforma, fora do controle do executável GF; o projeto **não os ingere como diagnósticos**.

Consultar [segurança v5.13.11.7](SEGURANCA-v5.13.11.7.md).
