# Gerenciador Final v5.12.12 TESTE — inventário MSIX e dois catálogos Microsoft

Este teste é uma evolução isolada da v5.12.11. Preserva a interface e o mecanismo atual de atualização supervisionada.

## Progresso observado em v5.12.11
- 142 programas tradicionais; 5 possíveis atualizações (Adobe, Edge, OBS, Plex e Proton Pass).
- 5 fontes oficiais específicas consultadas com sucesso; 631 entradas RuckZuck.
- A primeira consulta demorou 5048 ms e as seguintes 373, 519 e 347 ms, de acordo com o diagnóstico enviado.
- O WinGet mostrou seis atualizações: Edge, GameInput, OBS, Outlook, Proton e Terminal. GameInput, Outlook e Terminal não estavam no filtro do Gerenciador.

## Novas integrações nesta versão
1. Inventário local MSIX via `Windows.Management.Deployment.PackageManager.FindPackagesForUser(string.Empty)`, sem executar cmd, WinGet ou PowerShell, limitado ao usuário atual.
2. Inclusão somente de identidades exatas Microsoft `Microsoft.WindowsTerminal` e `Microsoft.OutlookForWindows`, `PublisherId=8wekyb3d8bbwe`. Não confundir Terminal Preview ou outros publishers. Ignorar pacotes já representados pela lista tradicional.
3. Registro do Windows: `Microsoft GameInput` com `SystemComponent=1` pode ser exibido como item de somente leitura, sem habilitar desinstalação.
4. Consulta HTTPS de **nomes de pastas de versão** do repositório comunitário `microsoft/winget-pkgs`, apenas para `Microsoft.GameInput` e `Microsoft.Outlook`. O Gerenciador não executa o cliente WinGet, não baixa YAML nem instaladores, não segue URLs extraídas da resposta.
5. A fusão dos resultados conserva um candidato por aplicativo e só sugere versão quando ela pode ser comparada numericamente. Novo catálogo aparece como `Catálogo WinGet`, isto é, fonte pública indireta mantida por Microsoft/comunidade, não API oficial da Store.
6. Entradas reconhecidas pelo inventário MSIX ou componente de sistema não podem ser desinstaladas nem selecionadas para desinstalação em lote. Nunca atualizar a Store automaticamente por meio deste protótipo.

## Limitações
- A enumeração MSIX pode não estar autorizada ou pode não encontrar Outlook/Terminal no PC. A falha retorna status seguro, mantendo a lista tradicional. O CI encontrou 32 pacotes sem correspondência, o que é esperado em um runner vazio.
- O catálogo comunitário WinGet e a Store podem disponibilizar versões diferentes em determinados momentos. Por isso mostramos sempre **possível atualização**, e não instalação garantida.
- `Microsoft GameInput` é componente especial: a indicação de versão, se encontrada, é somente leitura.
- A instalação automática continua restrita às receitas oficiais verificadas existentes (Visual C++ 2013 x86/x64 e OBS 32.2.2).
- Não há scripts executados, serviços adicionais instalados nem cache no disco. AVG e SmartScreen não foram desativados.

## Testes GitHub Actions
- 8/8 testes de inventário MSIX, identidades e deduplicação.
- 39/39 testes multifontes com GameInput/Outlook, editor falso, versão inválida e isolamento de falhas.
- 14/14 de cache; 18/18 de comparação; 12/12 do motor autorizador.
- 14 consultas reais HTTPS GET aprovaram, inclusive novos catálogos GameInput e Outlook; RuckZuck devolveu 631 entradas.
- Assinatura Microsoft dos Redistributables e hash/assinatura do OBS conferidos no CI, sem instalar.
- Compilação portátil Windows x64 aprovada.

## Procedimento de teste
1. Extraia o ZIP em pasta nova, com AVG ativo.
2. Inicie o app, aguarde a consulta automática. Verifique se há **Microsoft GameInput**, **Outlook for Windows** ou **Windows Terminal** em Todos/Com atualização.
3. Compare quantidades com a v5.12.11 (142 programas, 5 possíveis atualizações). **Não há garantia** de chegar a 6: essas listas têm fontes diferentes.
4. Abra `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.12.log` e observe `MSIX.LEITURA status=...` e `MULTIFONTES.CONSULTA_OK`.
5. Não instale/desinstale nada neste teste.
6. Se o AVG alertar, pare e envie captura; não desligue proteções.

Branch: `teste-inventario-msix-v5.12.12`.
