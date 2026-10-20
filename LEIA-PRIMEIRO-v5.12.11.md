# Gerenciador Final v5.12.11 TESTE — Cobertura Edge, Terminal e Proton

Versão de teste baseada na v5.12.10, mantendo interface escura, cache e o motor de instalação verificado.

## Por que o WinGet encontrou seis e o Gerenciador encontrou três?

O WinGet combinou fontes `winget` e `msstore`. A v5.12.10 usava o registro tradicional de desinstalação e provedores oficiais específicos; não era equivalente ao catálogo WinGet. Os resultados das duas consultas não devem ser somados cegamente porque OBS é comum às duas e Adobe/Plex só apareciam no catálogo RuckZuck.

## Três novas consultas somente leitura
- **Microsoft Edge Stable**: `https://edgeupdates.microsoft.com/api/products?view=enterprise`, selecionando somente `Product=Stable`, plataforma Windows e arquitetura do sistema. Rejeita Beta/Dev/Canary e versões de outros sistemas. Site alternativo: `https://www.microsoft.com/edge`.
- **Windows Terminal Stable**: `https://api.github.com/repos/microsoft/terminal/releases/latest`, versão estável publicada pela própria Microsoft. Não mistura Terminal Preview. Página oficial: `https://github.com/microsoft/terminal/releases`.
- **Proton Pass Desktop**: `https://raw.githubusercontent.com/ProtonMail/WebClients/main/applications/pass-desktop/CHANGELOG.md`, extraindo somente cabeçalho de versão estável do aplicativo desktop. Não usa o changelog do navegador nem repositórios de terceiros. Página oficial: `https://proton.me/pass/download`.

Os motores só consultam dados de programas previamente identificados por nome exato e fabricante confiável. URLs são fixas no código, sempre HTTPS. Nenhuma lista do usuário é transmitida às fontes, nenhuma instalação ocorre durante a busca e nenhuma linha de comando ou script é executado.

## Segurança e desempenho
- Respostas limitadas por tamanho, cache apenas em memória por dez minutos e sem arquivos residuais de catálogo.
- Falha em um provedor não oculta os resultados dos demais.
- O botão Atualizar continua restrito às antigas receitas com autenticação de instalador; novas fontes apenas apresentam a possibilidade de atualização e o Site oficial alternativo.
- Os provedores continuam em paralelo; apenas dois pedidos simultâneos à API GitHub, com cancelamento e timeout.
- Não alterar Windows Update, SmartScreen nem antivírus.

## Limitação importante
O inventário atual vem de entradas tradicionais de desinstalação. Aplicativos exclusivamente MSIX/AppX, como algumas instalações de Outlook e Windows Terminal, podem não aparecer na lista. A integração com Microsoft Store **não foi adicionada** nesta versão; Microsoft GameInput e Outlook ainda não possuem comparador específico confiável. Não declare cobertura igual ao WinGet sem implementar e testar essa etapa. Mesmo que a fonte do Terminal responda, a comparação só aparece se a instalação for reconhecida.

## CI verificado
- 34/34 testes de motores múltiplos, incluindo Edge Beta, Terminal Preview e fabricante Proton falso.
- 14/14 testes de catálogo/cache, 18/18 de comparação, 12/12 de receitas oficiais.
- 12 fontes oficiais específicas retornaram status `ok` em HTTPS GET real (inclusive as três novas); RuckZuck 631 entradas.
- Hash do OBS e assinaturas dos instaladores Microsoft validados por CI, sem executá-los.
- Aplicativo Windows x64 compilado e ZIP portátil produzido.

## Primeiro teste do usuário
1. Extraia ZIP em pasta nova com AVG ativado.
2. Aguarde a consulta automática. Confira Edge e Proton Pass em Todos, além do filtro Com atualização.
3. Compare as versões exibidas com a saída anterior do WinGet. Não suponha que GameInput/Outlook estejam cobertos.
4. Clique uma vez em Verificar atualizações e confira quantos candidatos aparecem e se houve falha isolada.
5. Sem necessidade de instalar nada. Se persistir a diferença, envie o log `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.11.log` e capturas dos dois filtros.
