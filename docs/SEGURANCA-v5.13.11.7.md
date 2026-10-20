# Segurança e privacidade — v5.13.11.7 TESTE (20/10/2026)

## Princípios obrigatórios

1. Nenhum arquivo escolhido pelo usuário, comentário, anexo, screenshot, log, script, Issue ou PR de terceiros pode ser recebido como entrada executável do GF, seu atualizador ou qualquer agente automatizado.
2. A única telemetria experimental ativa no aplicativo é **local**, por consentimento voluntário `LocalOnly`. Não existem upload de diagnósticos, autenticação GitHub, envio em segundo plano ou formulário de Issues no aplicativo. O usuário pode manter todas as funções mesmo recusando.
3. O formato permitido contém exclusivamente `schema=1`, `version` do GF em formato validado, `exit` normal/incompleto e `counts` de 1 a 14 categorias técnicas fixas com números inteiros entre 1 e 100. Não recolher ou transmitir caminhos, nomes de programas, usuário, máquina, IP no payload, logs integrais, hashes da máquina, arquivos, capturas, serial, hardware, comandos, credenciais ou texto livre.
4. Relatórios locais expiram em até sete dias; timestamp futuro indevido também invalida. A revogação impede coleta na sessão e tenta remover o arquivo; erros de sistema de arquivos são avisados. Não prometer exclusão física de backups ou arquivos inacessíveis.
5. Nenhum agente ou Issue dispara atualização, assinatura, instalação, release, merge ou mudança em `main`. Correções de IA futuras exigem branch isolada, verificação de diffs, CI seguro, revisão humana e teste real antes de distribuir.
6. Programas executáveis só podem ser lançados quando a receita fixa tem URL HTTPS autorizada, identidade e arquitetura compatíveis, SHA-256 **fixado** e assinatura Authenticode validada; o hash e a assinatura são rechecados imediatamente antes da execução com arquivo protegido. Receitas sem hash, como o antigo atalho do Visual C++ 2013, ficam consultivas: **site oficial**, não instalação direta.
7. Windows Update é executado pelo serviço oficial do Windows somente após seleção, novo scan e coincidência de GUID **e número da revisão**; se a revisão mudar, exigir nova seleção. Não aceitar EULA silenciosamente nem reiniciar o PC pelo aplicativo.

## Alterações implementadas na PR #40

- Retirados menu `Revisar diagnóstico no GitHub`, método de criação de Issue e gerador de URL com payload no navegador.
- Eliminados parser de Issues públicas e gatilho `issues` do Actions. O workflow de CI agora **somente valida políticas e testes de código**; nenhum comentário, anexo ou texto enviado ao GitHub é ingerido como diagnóstico.
- Introduzida `DiagnosticPayloadPolicy` com esquema rígido, rejeição de chaves duplicadas e desconhecidas, contagens inválidas e arquivo excessivo.
- Prototipo do servidor `telemetria-worker` continua desligado: responde 503 a toda requisição, mesmo válida. Não há credencial no executável. Antes de qualquer ativação futura seriam necessários autenticação, antiabuso, proteções da infraestrutura e novo consentimento específico.
- Bloqueio de instaladores com SHA-256 ausente, sem desabilitar consultas e páginas dos fornecedores.
- Atualização Windows pelo par identidade/revisão; testes negativos para mudanças e IDs falsos.
- OIDC do Actions isolado no job de atestação da `main`; workflows legados remanescentes tiveram ações fixadas por commit e checkout sem persistência de credenciais.

## Limites e verificações pendentes

- O GitHub, navegador e sistema operacional continuam permitindo anexos/publicações fora do GF. **Não é tecnicamente possível proibir todos os arquivos manuais de usuários na internet com alterações no aplicativo.** O que foi bloqueado é o caminho de envio oferecido pelo GF e o consumo por automações do projeto.
- Um arquivo perfeitamente assinado e com hash correto ainda pode conter defeitos do próprio fabricante. Windows Update também pode causar regressões. Não existe garantia absoluta de que nenhuma atualização possa causar danos; backups, pontos de restauração, critérios de compatibilidade, confirmação, logs e validação em hardware real continuam importantes.
- Falhas de drivers, UAC, MSI em subprocessos, AVG/SmartScreen, reversão, ambientes heterogêneos e runtime .NET empacotado continuam sujeitos a verificações adicionais. **A validação de CI não substitui os testes em PCs reais.**
- Não há imposição automática de aprovação de PR nem regras de branch configuradas por este patch; proteger `main` com bloqueio de push direto, revisões obrigatórias e checks exigidos nas configurações do GitHub antes de permitir agentes.
- A auditoria estática e os testes automatizados reduzem riscos, mas não constituem uma prova de ausência de vulnerabilidades nem revisão humana linha a linha de todas as dependências.

## Verificação

- `TestesTelemetria`: schema, rejeição de extras/duplicados, consentimento, expiração, retenção e revogação.
- `TestesAtualizador`: fontes oficiais, receitas fixas, bloqueio de Visual C++ sem hash e rejeição de assinaturas/receitas alteradas.
- `TestesLayout`: GUID + revisão do Windows Update.
- `prototipos/telemetria-worker/test.mjs`: validação do envelope local; endpoint desativado sem escrita em base de dados.
- `scripts/test_politicas_seguranca.py`: ausência do envio manual no executável, ausência de ingestão de Issues em CI, hash obrigatório e isolamento de atestações.

Consultas e compilações das revisões específicas devem ser confirmadas nas execuções Windows mais recentes da PR #40; aprovações de commits anteriores não equivalem a validação desta candidata.

## Complemento posterior — v5.13.11.8

Acrescentada uma **simulação totalmente local**, com dados sintéticos e validados, acessível sem consentir. A visualização substitui JSON bruto por frases simples na janela de privacidade. Simulação não acessa disco/rede, não usa informações do usuário nem altera a preferência local; essa condição é verificada por testes de lógica e layout. Não constitui ativação do envio remoto. [Plano de experiência](EXPERIENCIA-CONTRIBUICAO-SIMPLES.md).
