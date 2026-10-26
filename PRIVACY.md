# Privacidade

## Piloto BETA .66 — não liberado

A branch experimental [PR #61](https://github.com/walissonminel-1991/gerenciador-final/pull/61) prepara o envio automático **após consentimento específico para transmissão**, apresentado ao entrar no programa. Não há opção pré-selecionada e recusar não reduz funções. A revogação é acessível em **Bandeja > Privacidade e diagnóstico**. A BETA remove o botão fixo de coleta do cabeçalho e os perfis de testes PC FORTE/FRACO.

O aplicativo transmite somente resumos validados (schema 1, versão, fechamento, até 14 códigos de falha e suas contagens, até 4096 bytes); jamais inventário, arquivos, nomes ou versões de aplicativos, logs completos, endereços de hardware, caminhos, credenciais, IP no corpo ou identificador persistente. No caminho de rede, o provedor pode processar o IP e metadados para entrega e antifraude. Em caso de falha o relatório pendente permanece no computador com retenção de sete dias; revogar impede novos envios e tenta excluir a pendência. Sem dados de contato/ID estável não é possível identificar individualmente agregados já recebidos; expiram no receptor após sete dias. Uma síntese diária **sem identificadores diretos** pode ser enviada pelo servidor a um **repositório GitHub privado**, somente após autorização de exportação separada. Em grupos pequenos, data, versão e categoria ainda podem permitir inferências sobre participantes: **não presumir anonimato estatístico**. A proposta desta auditoria prevê vencimento das sínteses no sétimo dia contado do dia UTC da coleta, exclusão definitiva na próxima manutenção e recuperação em caso de falha. O código não foi implantado nesta missão; a política precisa de aprovação antes do piloto. Atrasos de cron, indisponibilidade ou falta de permissão podem prolongar a permanência, devendo gerar tratamento operacional. Contagens menores que cinco são omitidas e as demais arredondadas; isso não equivale a cinco pessoas e não garante anonimato. Referências mínimas de exclusão são conservadas enquanto houver pendência. Backups e notificações dos provedores exigem política própria.

**O cliente desta branch continua sem URL de recebimento e não permite aceitar o envio.** Segundo o proprietário, a infraestrutura privada já foi preparada com as flags desligadas; a nova retenção permanece somente em código. Ainda faltam aprovação de LGPD/contato, comprovação de acesso para exclusão, monitoramento e validações antes de convidar pessoas. O plano de envio não substitui os avisos da versão .65 descritos abaixo.


**Comportamento da versão .65 TESTE (não BETA):** A coleta opcional do GF é **exclusivamente local**, desativada até consentimento
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

## Histórico técnico local do Lote 1

Separado da coleta opcional, o histórico técnico da candidata mantém um
arquivo novo por sessão em `%LOCALAPPDATA%\\GerenciadorFinal\\Historico`.
Contém apenas horários UTC, códigos de etapa e contagens — não armazena
nomes de programas, usuários, pastas, comandos ou identificadores do Registro.
A identidade usada para comparar inventários permanece apenas em memória.
Os arquivos não são transmitidos e **não possuem expiração automática**
nesta candidata; podem ser apagados pelo usuário com o GF fechado.
Se a gravação falhar, o relatório avisa e mantém somente os eventos
da sessão em memória. A prévia limita-se aos últimos 128 eventos; a
gravação aos primeiros 256 por sessão. O TXT é salvo apenas por
ação explícita do usuário. Veja [critérios e limites](https://github.com/walissonminel-1991/gerenciador-final/blob/268d13ea0713d17d8b78a545cb3ae9e8670eac28/docs/MISSAO-LOTE1.md).

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

### Descoberta automática de instaladores

O GF baixa `https://cdn.winget.microsoft.com/cache/source.msix` como arquivo de dados e confere a assinatura Microsoft. Não registra esse pacote nem executa WinGet COM/CLI. O índice é consultado localmente; os manifestos correspondentes usam caminhos públicos do catálogo. A lista do computador e seus caminhos não são enviados como parâmetros ou corpo de requisição. A URL pública do manifesto/pacote consultado identifica o produto ao servidor, como nas demais consultas de atualização. Downloads de instaladores só são iniciados após ação do usuário.

O índice e as autorizações ficam na sessão, com limites e validade. Arquivos temporários do catálogo são removidos no encerramento normal, quando não há consulta em andamento. Interrupções do sistema podem deixar o diretório temporário para a manutenção normal do Windows. Dados de assinatura do executável instalado são usados apenas para validar o fornecedor, sem upload de arquivos ou certificados pessoais.

### Consulta de drivers somente leitura (Lote 2)

A investigação lê IDs de hardware e o trecho limitado do log SetupAPI local
para correlacionar dispositivo e INF. O diálogo apresenta identificadores,
nomes, caminhos e signatário apenas na tela; esses dados e linhas brutas do
log não são enviados ao diagnóstico do GF nem gravados pelo novo fluxo.
O histórico próprio preexistente mantém hashes e contagens.

O botão Versões oficiais consulta metadados pelo serviço Windows Update
do próprio Windows, sujeito ao tratamento de dados desse serviço. O GF
não baixa nem instala pacotes de drivers, não aceita EULA e não muda a
configuração de atualização. Nenhum novo envio ao GitHub foi criado.
