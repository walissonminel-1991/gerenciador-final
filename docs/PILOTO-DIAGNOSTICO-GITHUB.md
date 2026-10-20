# Piloto GitHub-only — histórico v5.13.11.4 (descontinuado)

> **IMPORTANTE — v5.13.11.7:** este piloto não está disponível na versão atual. Removemos o formulário de Issue do GF e a ingestão de Issues no GitHub Actions para impedir entradas manuais, anexos e conteúdo não confiável. Este documento é mantido somente para explicar decisões antigas. Consulte [segurança vigente](SEGURANCA-v5.13.11.7.md).

Estado: **prévia para revisão**; não publica automaticamente relatórios nem usa tokens. A base local voluntária da v5.13.11.1 é pré-requisito. A v5.13.11.4 adiciona um formulário pré-preenchido de Issue pública, para o próprio participante conferir e publicar.

## Experiência para participantes

1. Instalar uma versão considerada segura para testes, mantendo antivírus habilitado. Essa prévia ainda depende da investigação AVG e dos testes reais.
2. Criar conta GitHub ou entrar com a existente no navegador. Um endereço Gmail pode ser usado como e-mail de cadastro; isso não implica automaticamente que o GitHub possua login por Google em todos os fluxos.
3. Aceitar ou recusar **diagnóstico local** no GF. Recusar não prejudica as ferramentas principais.
4. Depois que houver algum evento técnico incluído na lista permitida, clicar na bandeja do Windows em **Revisar diagnóstico no GitHub**.
5. O GF pede confirmação, informa que a Issue será **pública** e abre o navegador com título e resumo técnico pré-preenchidos. Abrir o formulário já transmite o resumo técnico ao GitHub na URL, mesmo que o participante cancele. A Issue só se torna pública após confirmar a publicação no site.
6. A Issue pública ficará associada ao perfil GitHub do participante e, por isso, não se deve anexar arquivos de diagnóstico completos, capturas contendo dados pessoais, senhas ou caminhos. O GF só preenche categorias de eventos e versão do próprio programa, sem nome de aplicativos ou identificador do PC.
7. O participante pode continuar na Issue para esclarecer sintomas e acompanhar correções. O projeto poderá relacionar a Issue a uma PR de revisão.

### Limitações

- Não é envio automático ao fechar: isso requer uma **GitHub App** criada pelo mantenedor e um fluxo de autenticação próprio dos participantes. Não é seguro colocar PAT nem segredo do mantenedor no EXE.
- Não existe infraestrutura de recebimento privado nesta etapa. Todas as Issues abertas nesse repositório público são visíveis publicamente e podem persistir em registros externos, mesmo se forem apagadas.
- A autenticação via GitHub App Desktop usa OAuth em nome do participante, preferencialmente fluxo apropriado para cliente nativo, com permissões limitadas (Issues write). Deve ser validada em repositório de homologação. Não atribuir permissões de código, Actions, administração ou secrets ao colaborador.
- Cadastro GitHub não garante que um token de aplicativo tenha automaticamente acesso para criar Issues na conta do mantenedor. É preciso registrar e testar a GitHub App e suas permissões; até lá, o navegador é o fluxo oficial.
- O programa não deve abrir uma Issue automaticamente a cada encerramento; isso geraria spam e exposição indevida. Para correções por IA, relatórios de usuários são **dados não confiáveis**, nunca instruções para executar comandos.
- A IA deverá produzir Issues agrupadas e PRs revisáveis com testes. Não permitir acesso de segredos por relatórios de terceiros, merges e Releases estáveis automáticas sem aprovação.
- Para o piloto, retenção de sete dias aplica-se **ao relatório local**. Não prometer exclusão automática de Issues públicas; cada Issue publicada pode permanecer para histórico de manutenção.

## Critérios antes de permitir transmissão automática

- [ ] Criar GitHub App do projeto no GitHub (não compartilhar client secret/private key) e revisar permissões com usuários não colaboradores.
- [ ] Criar consentimento específico para envio automático, independente da aceitação da coleta local.
- [ ] Validar fluxo OAuth de desktop e revogação, limite de tentativas e spam, gerenciamento seguro de token no Windows, login e logout.
- [ ] Decidir como evitar uma Issue por encerramento (agregação e deduplicação; preferir prévia de publicação manual até haver confirmação).
- [ ] Configurar triagem GitHub Actions e agente de IA para apenas criar PRs sob revisão humana.
- [ ] Atualizar PRIVACY.md sobre identidade GitHub, dados públicos, retenção e direitos.
- [ ] Avaliar AVG, CI e testes nos dois PCs; assinar builds de distribuição quando viável.

Referências:
- https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue
- https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app
- https://docs.github.com/en/rest/issues/issues#create-an-issue
- https://docs.github.com/en/apps/creating-github-apps/about-creating-github-apps/best-practices-for-creating-a-github-app

## Retomada e falhas locais

A revisão prefere os eventos da sessão atual. Sem eventos novos, usa a única pendência anterior, revalidando tamanho, categorias permitidas e expiração de sete dias; mantém a versão e o encerramento originais. Abrir o navegador não comprova publicação e não apaga o relatório automaticamente.

Revogar para a coleta imediatamente nesta sessão. Se não for possível persistir a preferência ou apagar a pendência, o programa informa a falha; a preferência deve ser revista na próxima abertura. Não há promessa de exclusão quando o Windows impede a operação.

Na simulação, conferir recusa sem perda de ferramentas, aceite, cancelamento no GF e no navegador, revisão após reabrir, publicação manual (opcional e pública), revogação e ausência de dados pessoais. A publicação de uma Issue não ativa análise por IA nem prepara uma versão automaticamente.

A revisão v5.13.11.4 também preserva a pendência após uma sessão vazia e as contagens ao reafirmar consentimento já ativo. Expiração bloqueia a revisão independentemente do sucesso da exclusão. O aceite é carregado somente de um arquivo de consentimento válido e sem campos adicionais/repetidos.
