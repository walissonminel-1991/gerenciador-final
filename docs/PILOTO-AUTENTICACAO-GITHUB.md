# Piloto de autenticação GitHub com conta existente — outro PC (20/10/2026)

## Objetivo e estado

Avaliar o GERENCIADOR FINAL no computador disponível hoje, inclusive a experiência de consentimento/diagnóstico e, posteriormente, atualizações, desinstalações e reparos.

**v5.13.11.8 TESTE não realiza envios reais**. O botão **Simular sem enviar** utiliza somente dados inventados; **Ativar coleta local** guarda contagens técnicas apenas no PC. O receptor externo preparado na branch é um protótipo desligado por padrão, não conectado ao programa. Não atribuir à interface funções que ainda não existem.

O usuário propôs usar uma conta GitHub secundária. **Os Termos do GitHub limitam uma pessoa a uma conta pessoal gratuita; uma conta adicional de máquina gratuita é destinada somente a automação, não a atuar como participante humano.** Por isso, o plano seguro é testar com a conta pessoal existente em um **perfil separado do navegador** (sessão limpa). Outra conta pessoal só faz sentido se realmente pertencer a outra pessoa, com autorização própria. Essa autenticação será viável somente após registrar uma GitHub App, configurar um repositório de ingestão independente e implementar/testar um cliente com consentimento específico de transmissão. Não reutilizar o fluxo antigo de Issues públicas, que foi removido por segurança. Fontes: https://docs.github.com/pt/site-policy/github-terms/github-terms-of-service e https://docs.github.com/en/account-and-profile/concepts/account-management.

## Arquitetura do piloto (planejada, não implementada)

- **Repositório dedicado privado** sob controle do mantenedor, por exemplo `gerenciador-final-diagnosticos-piloto`. NÃO usar o repositório-fonte `gerenciador-final` como inbox. Sem executáveis, anexos, permissões de push ou gatilhos `issues`/ `issue_comment` em Actions. Texto em Issue nunca pode virar código, comando, build, alteração da main ou instrução a agente IA.
- **Conta GitHub pessoal já existente** do mantenedor, autenticada em sessão separada do navegador para simular consentimento e fluxos. Como o dono do repositório privado possui privilégios amplos na interface web, a GitHub App e seu token precisam ser restritos a `Issues: write` em **somente** esse repositório dedicado. Para testar o papel `triage` real, utilizar uma **outra pessoa** voluntária (conta pessoal própria), nunca criar segunda conta pessoal gratuita do mesmo titular. Nenhum token de envio deve permitir `Contents: write`, `Actions: write`, `Workflows`, `Administration` ou `Secrets`.
- **GitHub App** instalada apenas no repositório piloto e configurada para fluxo de dispositivo. Permissões mínimas: `Metadata: read` e `Issues: write` (confirmar no painel da app, usar instalação selecionada). Não usar PAT pessoal de escopo amplo, GitHub OAuth App com `repo` abrangente, `GITHUB_TOKEN`, senha ou segredo no EXE.
- **Fluxo de usuário**: clicar `Participar e enviar diagnóstico`, visualizar destino/categorias/possível identificação por conta, dar **consentimento específico** (independente da coleta local), autorizar na página oficial GitHub, revisar contagens e clicar `Enviar`. O aplicativo cria somente objeto JSON canônico validado internamente, sem aceitar caminho/arquivo, texto livre, anexos ou scripts. Confirmar êxito somente após resposta real da API.
- **Identificação**: Issues privadas registram o autor GitHub, horário e metadados de conexão no serviço. Não prometer anonimato absoluto; evitar identificadores desnecessários no payload. Consentimento deve informar esta diferença e apontar possibilidade de cancelamento/expiração.
- **Credenciais**: o Client ID da GitHub App pode ser público, mas o token de usuário pertence à sessão autorizada. Para o piloto, manter em memória, nunca registrar em log nem persistir. Evitar renovação silenciosa e exigir autenticação novamente em sessão futura. Revogação da coleta não apaga automaticamente Issues já criadas; informar e fornecer caminho de solicitação de exclusão.
- **Tratamento pelo mantenedor**: revisar a Issue somente como dado não confiável. O fluxo automatizado deve rejeitar uploads, corpos não canônicos, categorias proibidas, duplicações, anexos e URLs. Não há permissão de executar conteúdo, compilar ou publicar a partir de Issues. Manutenção via branch/PR separados, CI, revisão humana e testes reais.
- **Expansão futura**: contas GitHub secundárias são úteis para este piloto fechado, mas o produto final não deve exigir conta GitHub de pessoas com pouco conhecimento. Preparar recepção segura sem cadastro e proteção antiabuso separada; nunca embutir credencial de escrita compartilhada no EXE.

Documentação técnica externa: https://docs.github.com/pt/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app e https://docs.github.com/en/rest/issues/issues.

## Preparação sem a conta ainda cadastrada

1. Mantenedor decide se deseja criar repo privado e GitHub App exclusivos para teste. Não criar automaticamente nem convidar pessoas sem autorização específica.
2. Registrar permissões/consentimento na interface e testes negativos antes de compilar qualquer versão com envio. O atual v5.13.11.8 **não solicita login e não envia**.
3. Obter e validar App Client ID e ID do repositório destinado ao piloto, sem senha compartilhada; não assumir que um segundo login por si só libera envio.
4. Criar testes de cliente com `HttpMessageHandler` falso; cobertura obrigatória para recusa, logout, token expirado, erro 401/403/404/422/429, timeout, dupla confirmação, arquivo/log proibido e cancelamento.
5. Somente depois: validação de backend GitHub com **Issue contendo dados sintéticos** no repo dedicado privado; inspecionar se nada executou via CI ou alterou `main`. Não enviar dados reais até revisão LGPD e consentimento correto.

## Bateria deste dia — outro PC, não o PC FRACO

**Fase A — sem operações administrativas:** arquivo/compilação exatos da v5.13.11.8, AVG/Defender ativos; confirmar abertura, layout, navegação, bandeja e desempenho, botão `Simular sem enviar`, `Não participar/Desativar`, `Ativar coleta local` e reversão. Verificar que nenhum upload aconteceu; não forçar erros nem desabilitar proteção.

**Fase B — consultas sem modificar Windows:** inventário Win32/MSIX, atualizar lista, encontrar atualizações, analisar limpeza/resíduos em **somente leitura**, analisar rede, estado dos drivers, plano de energia. Registrar quantidades, mensagens, capturas sanitizadas e anomalias sem enviar nomes/caminhos ao esquema de telemetria.

**Fase C — manutenção individual posterior, após backup:**

- **Atualização:** testar primeiro em aplicativo **não crítico**, com instalador de fonte oficial e SHA-256 + assinatura aprovados. Exigir confirmação na UI, mostrar resultados e reconsultar versão. Não usar `Atualizar todos` nessa fase; não atualizar drivers, componentes do Windows, BIOS, VC++ não homologado ou Store à força.
- **Desinstalação:** escolher apenas programa de teste sem dados importantes, confirmar nome/alvo, criar restauração quando aplicável e observar tratamento de resíduos **somente leitura** inicialmente. Nunca remover perfis compartilhados ou saves, nem executar limpeza em lote.
- **Reparo:** iniciar com diagnóstico. Não executar reset de rede, DISM/SFC, limpeza de sistema, alterações de energia ou redefinição sem diagnóstico justificar, backup, ponto de restauração e confirmação explícita. Muitos reparos do Windows **não são garantidamente reversíveis**; anotá-los como testes avançados separados.

**Bloqueio imediato:** alerta AVG/SmartScreen, divergência de versão/hash/assinatura, solicitação de privilégio inesperada, aplicativo/instalador desconhecido, seleção duplicada ou caminho/arquivo fora do alvo. Não criar exceção de antivírus nem desativar proteção; registrar problema e interromper operação.

## Portões para avançar

- [x] Política de JSON, coleta local, simulação e regressão Windows testadas na PR #40.
- [ ] Observações da Fase A no PC disponível.
- [ ] Observações da Fase B no mesmo PC.
- [ ] Repositório e GitHub App específicos do piloto autorizados e configurados pelo mantenedor; usar conta pessoal existente e sessão isolada do navegador.
- [ ] Cliente de login/envio implementado, com consentimento independente e testes de segurança.
- [ ] Um envio **sintético** confirmado no repositório privado, sem automações de Issues.
- [ ] Um aplicativo de teste atualizado individualmente, confirmado e reavaliado.
- [ ] Uma desinstalação de teste feita e resíduos revisados sem excluir dados importantes.
- [ ] Reparos avançados estudados caso a caso e somente quando necessários.

NENHUM item pendente deve ser tratado como aprovado apenas porque a v5.13.11.8 compilou.
