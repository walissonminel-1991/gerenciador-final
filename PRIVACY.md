# Privacidade

O código-fonte atual não implementa contas de usuário, telemetria própria ou envio de um inventário do computador a servidores operados pelo projeto. Essa constatação depende do código atualmente publicado e deve ser revista a cada nova versão.

O diagnóstico de rede consulta `www.microsoft.com` por DNS e tenta estabelecer conexão TCP à porta 443; testes adicionais de gateway são feitos na rede local. Isso pode produzir tráfego e logs junto aos provedores de rede.

Quando o usuário executa atualizações, WinGet e Windows Update podem consultar serviços da Microsoft, fornecedores de aplicativos e suas fontes configuradas, sujeitos às políticas próprias desses serviços. Operações administrativas podem gerar logs no Windows. Os comandos de manutenção podem alterar arquivos temporários, configurações ou serviços do sistema mediante consentimento.

Exportar informações de hardware para PNG é uma ação local iniciada pelo usuário. Verifique o conteúdo antes de compartilhar relatórios ou imagens.

O instalador Inno Setup não cria exclusões em antivírus nem regras adicionais no firewall.

Não enviamos relatórios automáticos de erros para um servidor do Gerenciador Final nesta versão. Para informações de privacidade de serviços da Microsoft e de terceiros, consulte os respectivos fornecedores.
