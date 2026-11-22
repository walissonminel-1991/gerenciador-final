# GF — referências funcionais abertas e implementação original (VF06)

**Data:** 22/11/2026. **Linha:** VF06 (onda 1), subordinada ao P0 de 23/11. **Origem interna:** PR #101 HEAD `7671e994d2bc7d7d7f81129e8628676082aeadc6` no momento da criação. **Sem merge.**

## Regra de independência

Estudar **funcionalidades descritas publicamente** por projetos abertos. Escrever código próprio para .NET 10 e Windows Forms, sem copiar/adaptar trechos de código-fonte, importar classes, bibliotecas ou binários dos projetos. Utilizar somente recursos documentados do Windows e os motores existentes do GF. Registrar evidência de resultado no SHA exato antes de alterar os placares.

| Referência | Funcionalidade observada na documentação pública | Já existe no GF | Lacuna objetiva / decisão |
|---|---|---|---|
| [UniGetUI](https://github.com/Devolutions/UniGetUI) | Instalar, atualizar e desinstalar via vários gerenciadores | Catálogo de fontes, executores verificados e **Atualizar todos** na #101 | **Não** criar outro atualizador; posteriormente comparar resultado individual, cancelamento e fontes |
| [Bulk Crap Uninstaller](https://github.com/BCUninstaller/Bulk-Crap-Uninstaller) | Desinstalação em lote, apps Store, resíduos, entradas órfãs | Lote, Win32, inventário MSIX, resíduos controlados | VF06: observar itens ocultos sem conceder remoção; desinstalação MSIX por rota suportada **ainda pendente** |
| [Bloatynosy](https://github.com/builtbybel/Bloatynosy) | Revisar apps e selecionar alterações com resultado | Inventário e confirmação de operações | Identificar bloatware/bundleware por **evidência**, não apenas palavras no nome; não remover componentes nativos indiscriminadamente |
| [WinGet](https://github.com/microsoft/winget-cli) | Gerenciamento oficial de pacotes Windows | Contratos e integração parcial já existentes | Não introduzir execução irrestrita nem misturar catálogo e autorização de instalação |
| [OpenDriverUpdater](https://github.com/OpenDriverUpdater/OpenDriverUpdater) | Busca de drivers, verificação, instalação | WUA/PnP e catálogo próprios em PRs de Drivers | **Fora desta PR**. Não reabrir G2 NO-GO nem tratar INF como pacote integral |
| [Driver Store Explorer](https://github.com/lostindark/DriverStoreExplorer) | Inventário de Driver Store, backup, remoção | Consulta dispositivo/INF e recuperação parcial | **Fora desta PR**. Remoção de driver não autorizada sem recuperação verificada |

## Implementação funcional desta PR

- Na aba **Aplicativos**, controle **Ocultos** sob demanda e **somente leitura**.
- Consulta as entradas ARP nas visões 32/64 bits e nos escopos de máquina/usuário atual **usando o scanner existente**.
- Mostra apenas entradas marcadas `SystemComponent=1` e não classificadas como registros de atualização, nome longo ou item explicitamente protegido. O filtro não é uma prova de que o componente possa ser removido.
- Modal mostra contagem, nome, versão e editor, com aviso de inventário parcial quando houver falhas de leitura.
- Não altera a lista principal, candidatos de atualização, autorização de desinstalação, fluxo de lote, Windows Update, Drivers ou infraestrutura.
- Nenhum aplicativo/driver é instalado, desinstalado, baixado ou removido pela consulta.

## Próximas entregas funcionais VF06 (não implementadas nesta PR)

1. Avaliar elegibilidade de desinstalação de aplicativos MSIX do usuário por **identidade exata**, preservando frameworks, dependências, aplicativos protegidos e estados parciais; usar apenas API do Windows documentada.
2. Aplicar prévia, confirmação específica, execução, cancelamento quando suportado e pós-verificação à identidade autorizada.
3. Identificar entradas ocultas **elegíveis**, sem concluir que `SystemComponent=1` por si só permite desinstalar.
4. Publicar matriz por versão/edição Windows para Edge, OneDrive e Copilot. Não presumir remoção suportada.

## Critérios de verificação desta entrega

- Compilar .NET 10/Windows Forms em runner Windows no SHA exato.
- Com fixture de ARP, confirmar que itens `SystemComponent=1` aparecem apenas na consulta explícita, enquanto atualizações, nomes inválidos e componente protegido não viram candidato de remoção.
- Confirmar que o botão não modifica inventário principal, fila, proteção, nem habilita desinstalação.
- Conferir modal em largura normal e compacta, especialmente descrição e fechamento.
- Sem homologação física: **VF06 permanece parcial; VF 0/16 e roadmap 27/42 permanecem inalterados**.

## Preservação

Não tocar em `main`, PR #97/#99/#101/#102, G2, G5, GF_BETA, Workers, cron, secrets, workflows e distribuição. PR Draft empilhada acima do HEAD original da PR #101; não realizar merge ou release.
