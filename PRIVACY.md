# Privacidade

A coleta opcional do GF é **exclusivamente local**, desativada até consentimento
expresso e revogável. Recusar não reduz funcionalidades. O botão do cabeçalho
mostra Coleta local ativa, Coleta desativada ou Configurar privacidade; a bandeja
também permite revisar a escolha. A preferência mínima é mantida entre versões
e reinicializações para não repetir o aviso. Transmissão de diagnósticos pela
internet continua desativada e exigirá consentimento separado se implementada.

## Resumo opcional e consentimento

O resumo contém somente versão do GF, encerramento normal e contagens das
categorias técnicas permitidas. Não inclui inventário, nomes de programas,
usuários, caminhos, comandos, logs completos ou identificador persistente do
computador. Fica em `%LOCALAPPDATA%\GerenciadorFinal\Privacidade`: um relatório
rotativo, expiração de sete dias e tentativa de exclusão ao revogar. O programa
fechado não mantém serviço ou tarefa para apagar relatórios.

Esquema ambíguo/inválido deixa a coleta desativada até nova escolha. Uma sessão
sem eventos preserva a pendência anterior ainda válida. Revogar interrompe a
coleta mesmo se houver falha no disco; o aplicativo informa se não conseguir
persistir a escolha ou apagar o arquivo. Uma recusa não persistida precisa ser
revista ao reabrir. Relatórios expirados não podem ser revisados, mesmo quando
sua exclusão falha.

Simular sem enviar usa apenas ocorrências fictícias: não lê dados da máquina,
não grava arquivos nem faz conexões. Está disponível também a quem recusou.
O receptor experimental permanece desligado em produção; não há conta GitHub,
Issue, anexo ou formulário de envio no GF. [Arquitetura e critérios futuros](https://github.com/walissonminel-1991/gerenciador-final/blob/piloto-diagnostico-github-v5.13.11.2/docs/INFRAESTRUTURA-ENVIO-SEGURO.md).

## Diagnóstico da candidata TESTE

Separadamente do resumo opcional, a candidata mantém um registro técnico local
necessário à investigação desta versão. Não é enviado. Fica em
`%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos` ou, como alternativa, em TEMP;
identifica versão, ambiente, contagens, etapas, durações e tipos de falha, sem
nomes de aplicativos nem saída de comandos. O registro da versão é reiniciado
na abertura; versões anteriores podem permanecer. A expiração de sete dias do
resumo opcional **não** se aplica automaticamente a esses registros de teste.

## Rede e outros componentes

Na inicialização e ao consultar atualizações, há requisições HTTPS a RuckZuck e
fontes oficiais, incluindo GitHub, Mozilla e manifestos públicos de pacotes.
Não se executa a CLI WinGet. Windows Update consulta serviços Microsoft. Os
provedores recebem metadados normais das requisições, como IP, horário e recurso
consultado, sujeitos às suas políticas; o GF não envia inventário ou arquivos
de diagnóstico nessas consultas.

O diagnóstico de rede faz DNS e TCP 443 a `www.microsoft.com` e testes locais
de gateway. Operações administrativas podem produzir logs do Windows e mudar
arquivos temporários, configurações ou serviços após confirmação. O instalador
Inno Setup não cria exclusões em antivírus ou regras adicionais de firewall.
O runtime Windows e o relatório de falhas do próprio sistema seguem também os
[termos dos componentes Microsoft](https://github.com/walissonminel-1991/gerenciador-final/blob/piloto-diagnostico-github-v5.13.11.2/docs/THIRD-PARTY-NOTICES.md); desligar a coleta
opcional do GF não altera configurações globais do Windows.

Exportar hardware para PNG é uma ação local do usuário. Confira o conteúdo
antes de compartilhar imagens ou diagnósticos por canais externos ao GF.
