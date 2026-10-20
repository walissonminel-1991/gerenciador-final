# Privacidade

A partir da candidata v5.13.11.1, o Gerenciador Final oferece **coleta opcional exclusivamente local** de contagens agregadas de categorias técnicas, mediante consentimento explícito na primeira execução. Isso não cria contas de usuário e **não habilita nenhum envio pela internet**. A escolha pode ser alterada no menu da bandeja (**Privacidade e diagnóstico**). Recusar não reduz as funcionalidades do programa.

O resumo é limitado ao identificador de versão do GF, tipo de encerramento normal e contagens das categorias técnicas permitidas. Não inclui inventário, nomes de programas, usuários, caminhos de arquivos, comandos, logs completos ou identificador persistente do computador. Armazenamento local: `%LOCALAPPDATA%\\GerenciadorFinal\\Privacidade`, com um relatório rotativo, expiração de sete dias e exclusão quando o usuário revoga o consentimento. A preferência mínima é mantida localmente para não repetir o aviso.

O diagnóstico de rede consulta `www.microsoft.com` por DNS e tenta estabelecer conexão TCP à porta 443; testes adicionais de gateway são feitos na rede local. Isso pode produzir tráfego e logs junto aos provedores de rede.

Quando o usuário executa atualizações, WinGet e Windows Update podem consultar serviços da Microsoft, fornecedores de aplicativos e suas fontes configuradas, sujeitos às políticas próprias desses serviços. Operações administrativas podem gerar logs no Windows. Os comandos de manutenção podem alterar arquivos temporários, configurações ou serviços do sistema mediante consentimento.

Exportar informações de hardware para PNG é uma ação local iniciada pelo usuário. Verifique o conteúdo antes de compartilhar relatórios ou imagens.

O instalador Inno Setup não cria exclusões em antivírus nem regras adicionais no firewall.

Nesta versão **não enviamos** diagnósticos automaticamente. Existe somente um modelo de servidor privado, não publicado nem configurado. Um eventual envio futuro exigirá novo consentimento específico e divulgação de finalidade, hospedagem, prazo e canal de contato. A infraestrutura HTTPS pode processar IP e metadados de rede, ainda que não incluídos no resumo; portanto não prometemos anonimato absoluto. Consulte [o plano](docs/DIAGNOSTICO-COLABORATIVO.md) e, para serviços externos, as políticas dos respectivos fornecedores.
