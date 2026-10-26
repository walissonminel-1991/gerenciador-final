# WORK18 — ampliação das atualizações seguras

Base conferida: PR #59 aberta e Draft, branch `integracao-lotes1-4-work14-17-20261025`, HEAD `28beb9607e85d208af4c0864d768a6db7fb5296a`, v5.13.11.64 TESTE. As execuções [40241712164](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40241712164) (validação técnica da fonte `5793ce1f`) e [40242837652](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40242837652) (HEAD documental) estavam aprovadas. Branch própria `work18-ampliacao-atualizacoes-seguras`, candidata .65, base da PR experimental: a branch da integração. Main e integração preservadas, sem merge/release estável. Roadmap **27/42**, sem encerramento de U8/R7/R4.

## Diagnóstico do caminho completo

| Etapa | Evidência exigida / resultado da auditoria |
| --- | --- |
| Inventário | ARP/MSIX com identidade, versão e contexto; entradas de gerenciadores mantêm propriedade externa. Não deduzir instalação só pelo nome. |
| Identificação local | `AutomaticAppInstallation`: raiz exclusiva, PE real, produto/versão, arquitetura e motor registrados. Serviços, runtimes e servidores genéricos continuam fora do contrato desktop. |
| Correspondência | `PackageCatalogIndex`: índice Microsoft MSIX assinado, validade, produto/editor e identidade única; rotas ProductCode/PFN/ID consultivas não autorizam executar. |
| Versão | Comparação numérica estrita, superior e estável; prévias, versões não comparáveis, conflito entre fontes e coexistência continuam bloqueados. |
| Manifesto | SHA do manifesto ligado ao índice autenticado, identidade/versão exatas, YAML limitado, sem alias, scripts ou comandos externos. Consultar versões via GitHub HTTPS é uma fonte consultiva distinta. |
| Instalador | Motor/arquitetura/contexto coerentes, URL HTTPS pública e SHA-256. Duas lacunas corrigidas: MSI sem Scope e modo interativo explícito descartado por parâmetros exclusivamente silenciosos. |
| Receita | Parâmetros gerados localmente; novo MSI registra `ScopeFromPayload`, obrigação de comprovação nativa, nunca inferência de contexto a partir do nome. |
| Autorização | Selo privado do scan, objeto original da receita, binding da instalação, alvo superior e validade curta. Cópia, alteração ou expiração não transfere autorização. |
| Interface | `Atualizar` / `Acompanhar` apenas com receita aprovada; consultas exibem motivo na linha, Store/Windows e gerenciadores externos preservam fluxo oficial. |
| Pré-lançamento e conclusão | Recibo local de download, hash, Authenticode/cadeia, mesmo assinante/editor, MSI Property/Summary/Upgrade Table, revalidação do inventário, consentimento para fechamento/UAC. Nova versão real e instância única precisam ser confirmadas; retorno zero sozinho permanece Indeterminate/Pending. |

Os zero elegíveis do PC FORTE não decorreram apenas do texto do botão. O log registra: 86 identidades sem correspondência automática, 29 sem versão superior, 3 recusas de binário/versão, 48 exclusões por política/contexto, 1 instalação ilegível, 1 manifesto não elegível e 1 consulta externa. São contagens de uma etapa, não nomes de programas nem autorização para deduzir qual recusa pertence a cada aplicativo. A sessão terminou com 10 descobertas, 4 em revisão (3 ambiguidades e 1 mudança de versão principal na matriz de capacidades), zero silenciosas e zero supervisionadas elegíveis.

## Implementação concreta

1. **MSI/WiX sem Scope no manifesto:** selecionar somente se contexto local e arquitetura já estiverem comprovados. As novas rotas MSI exigem também cache instalado, assinante local e exclusividade de UpgradeCode, inclusive entre nomes diferentes. Antes do lançamento, o MSI de hash e assinatura válidos precisa comprovar `ALLUSERS` e contexto igual ao instalado, ProductCode, UpgradeCode, fabricante, produto, versão superior e arquitetura por APIs nativas. `ALLUSERS=2`/contexto desconhecido, divergência explícita de Scope, serviços ou formato EXE permanecem recusados. Não passar propriedades improvisadas para mudar o contexto do MSI.
2. **NSIS, Inno, MSI e WiX com acompanhamento:** quando o manifesto declara `interactive`, mas as flags silenciosas não são aceitas, selecionar a apresentação interativa local e descartar integralmente essas flags. `Custom`, `Upgrade`, `InstallLocation`, `Log` e `Repair` continuam impedindo a promoção. Sem modo interativo explícito não há fallback. NSIS usa argumentos vazios; Inno mantém controles de reinício/fechamento/contexto; MSI usa `/norestart`. O botão é **Acompanhar**, sem alegação de atualização silenciosa.
3. **Reforços:** mudança de ProductCode exige prova de major upgrade também na rota de nome exato, além da rota MSI com alias. UpgradeCode declarado pelo manifesto precisa coincidir com o payload; versão alvo deve ser superior. Assinante da instalação/pacote também precisa corresponder ao editor normalizado.
4. **Motivo por instalação:** o pipeline conserva códigos de recusa ligados ao binding local; só um scan emitido recebe esses dados. A interface não repete leituras de disco durante o desenho; usa as capacidades/avaliação da consulta. Contagens/códigos fixos continuam sendo os únicos dados dessa avaliação enviados ao diagnóstico.
5. **WinGet COM:** diferenciar contexto elevado recusado, falha de conexão, ativação, classe não registrada e acesso negado. Sem textos de exceção, conta, caminhos, reparo, CLI ou elevação automática. Catálogo HTTPS segue independente; presença do pacote/alias no PC FORTE não comprova API disponível. A causa específica daquela sessão continua indeterminada porque o leitor antigo só registrou `unavailable`.

## Candidatos reais identificáveis

Fonte: diagnóstico `GerenciadorFinal-Diagnostico-v5.13.11.64.log` e captura `image(20261025-231120).png`, fornecidos pelo usuário. O relatório local só possui contagens; não contém fabricante, caminho, PE, ProductCode ou arquitetura individual. As fontes públicas abaixo acrescentam evidência sobre distribuição, não sobre o estado da instalação pessoal. Não se baixou/executou instalador nesses computadores.

| Candidato | Instalada → descoberta / origem observada | Formato e identidade demonstráveis | Impedimento e correção possível | Antes → após WORK18 / classificação |
| --- | --- | --- | --- | --- |
| EA app | 13.809.0.6322 → 13.813.1.6327 / catálogo WinGet HTTPS | Manifesto público: Burn x64 machine, SHA, `uninstallPrevious`, propriedades próprias; instalação local não corroborada | Motor Burn e remoção prévia sem contrato do GF; exigiria suporte específico com migração/retomada, fora desta ampliação desktop | Site oficial → Site oficial; gerenciamento externo pelo fabricante |
| K-Lite Mega Codec Pack | 20.0.5 → 20.1.0 / catálogo WinGet HTTPS | Manifesto público: Inno machine, mesmo payload para x86/x64 e SHA; assinatura e executável do produto instalado não fornecidos | Pacote composto; não provar arquitetura pelo nome nem escolher o player como identidade de todos os codecs. Código de recusa individual da sessão não foi registrado | Site oficial → Site oficial; sem evidências suficientes para execução |
| Plex Media Server | 1.43.4.10903 → 1.43.5.11029 / catálogo WinGet HTTPS | Manifesto público: Inno x64 machine, modos interativo/silencioso, ARP `_is1`, SHA; contexto local individual incompleto | Política genérica exclui servidores; precisa tratar serviços, dados do servidor e retomada com contrato específico | Site oficial → Site oficial; gerenciamento externo pelo fabricante |
| Jellyfin Server | 12.1.0 → 12.3 / GitHub oficial, conforme captura | Tag/versão descoberta; não prova manifesto Windows executável, formato, arquitetura ou assinatura da instalação | Servidor fora da política desktop; comparar release do servidor e instalador Windows antes de criar contrato. Não supor equivalência de tag/instalador | Site oficial → Site oficial; sem evidências suficientes para execução |
| Outlook for Windows | 1.2025.416.400 → 1.2026.812.100 / catálogo WinGet | Manifesto público MSIX, PFN Microsoft, x64/x86/ARM64; arquitetura local não fornecida | Fluxo oficial Store/MSIX, sem usar atualizador Win32 genérico | Ver na Store → Ver na Store; gerenciamento externo |
| Power Automate | 1.0.2159.0 → 2.73.00155.26278 / RuckZuck | UI da sessão encaminhou a Windows/Store; não há PFN, PE ou arquitetura individual na evidência | Não comparar edição Store e Desktop como se fossem a mesma instalação; sem manifesto autenticado vinculado a essa instância | No Windows → Gerenciar; gerenciamento externo, revisão da correspondência ainda necessária |
| AIDA64 Extreme v7.50, Discord, Free Download Manager | Versões alvo não exibidas; fontes/compatibilidade em revisão na captura | Sem identidade individual suficiente para atribuir cada um dos códigos agregados | Preservar revisão; não inventar versões ou converter nomes em receitas | Como fazer → Revisar (quando não houver destino oficial específico); revisão necessária |
| Proton Pass (parcialmente visível) e demais candidatos não inteiramente exibidos | Sem dados completos na captura e logs anônimos | A presença do nome não prova que integra os dez resultados nem permite recuperar a versão alvo | Preservar consulta; novo diagnóstico local por app pode ajudar sem upload de nomes/caminhos | Sem classificação operacional individual demonstrada; evidências insuficientes |

Nenhum destes programas é declarado recém-homologado ou atualizado pela WORK18. A matriz de fixtures abaixo demonstra novos contratos condicionais, não dez botões novos no PC FORTE.

## Matriz comparativa reproduzível

`scripts/measure_work18.py` compila **a mesma fixture** contra `28beb960` e o HEAD candidato, incluindo PE real inerte, leitura do motor, emissão temporária e seleção da ação. Não apenas compara números codificados. Cache MSI e assinante local das fixtures são simulados; nenhum payload real recebe autorização final. A execução Windows e os resultados finais ficam registrados na seção de evidências.

| Grupo de cenários | .64 antes | .65 esperado, a comprovar na CI |
| --- | --- | --- |
| Controles existentes: NSIS, Inno, MSI, WiX silenciosos e NSIS interativo | 4 Atualizar + 1 Acompanhar | 4 Atualizar + 1 Acompanhar |
| MSI e WiX, Scope omitido, cada um em machine/user | 4 recusados | 4 Atualizar, obrigando contexto no payload |
| NSIS, Inno, MSI, WiX com interativo explícito e flag silenciosa desconhecida | 4 recusados | 4 Acompanhar, sem executar a flag desconhecida |
| 15 controles negativos | 15 recusados | 15 recusados |
| Total / 28 fixtures | 5 ações elegíveis antes de validar payload | 13 esperadas; ganho de 8 (4 Atualizar + 4 Acompanhar) |

Negativos: arquitetura errada, Scope explicitamente divergente, Scope ausente em NSIS/Inno, flags desconhecidas sem interativo, Custom/Repair/InstallLocation, dependências, remoção prévia, instaladores ambíguos, HTTP e hash inválido. Além da matriz, testes específicos verificam assinatura ausente, manifesto alterado, MSI de outro produto/fabricante/família/contexto/arquitetura/versão, selo/cópia/expiração, alteração da instalação, duplicação, versão não confirmada e repetição. A regressão anterior continua cobrindo hash incorreto, Authenticode/cadeia, redirecionamento, fechamento recusado, UAC, cancelamento, privacidade, dados pessoais e arquivos compartilhados.

| Indicador do PC FORTE | Antes, sessão .64 | Depois |
| --- | --- | --- |
| Aplicativos analisados | 169 | Sem nova medição física |
| Atualizações descobertas | 10 | Sem nova medição física |
| Silenciosas elegíveis | 0 | Sem nova medição física |
| Supervisionadas elegíveis | 0 | Sem nova medição física |
| Revisões / ambiguidades | 4 revisões; 3 ambiguidades na matriz | Sem nova medição física |
| Consultivas | 216 operações entre atualização/desinstalação/resíduos; não são 216 programas ou updates | Sem nova medição física |
| Segurança | Proteções da .64 | Preservadas e reforçadas; aprovação final depende da CI |

## Validação e evidências

Estado inicial WORK18: aplicação e testes compilam no SDK 10.0.402, sem erros/avisos; seleção determinística roda em Linux; 54 testes de ecossistemas aprovados localmente. Não são execução Windows nem homologação física. A CI obrigatória executa:

- `dotnet run --project TestesAtualizador -c Release -- --work18` e `--automatic`, `--work17`, `--work13`, `--lote3`, suite completa de instaladores.
- `dotnet run --project TestesLayout -c Release -- --work18`: 1440×900 e 2560×1440, DPI 100/125/150/200%, desenho da lista real, motivos/botões e ausência de selos sintéticos no produto.
- `python scripts/measure_work18.py` e comparações imutáveis WORK14/16/17.
- Seis mutações adicionais (Scope MSI, expiração, assinatura, flags interativas , prova local MSI e major upgrade de nome exato), preservando todas as mutações anteriores.
- Regressões completas dos Lotes 1–4, inventário, drivers somente leitura, desinstalação, resíduos, rede, MSIX, privacidade, encerramento; downloads oficiais autenticados sem execução, política e pacote x64 self-contained.

CI final, commit exato, matriz gerada e pacote/hash serão registrados aqui após o término. **Enquanto esta seção não contiver aprovação final, a missão não está tecnicamente concluída e nenhum pacote WORK18 é recomendado.**

## Homologação e pendências

1. Primeiro validar num Windows descartável a instalação/atualização de uma aplicação assinada por fabricante confiável que utilize um dos novos contratos; as fixtures e a leitura do MSI não equivalem a atualização real de fornecedor. Não instalar nada nos PCs pessoais nesta missão.
2. Nos PCs FORTE/FRACO, posteriormente e com autorização própria: consultar .64/.65 sobre o mesmo inventário e comparar ações/motivos; conferir janela escura, rolagem e DPI. Nenhuma contagem posterior foi inferida.
3. Só depois, mediante consentimento específico, homologar atualização de um app não crítico e conferir versão final, escopo, ausência de duplicatas e dados preservados. Fluxo supervisionado pode mudar escolhas no instalador; divergência posterior permanece Pending/Indeterminate, sem repetição automática.
4. Acompanhar nova razão COM no PC FORTE; o PATH ausente não basta para explicar a falha da ativação COM e não autoriza reparo do WinGet.
5. Burn/EA, servidores Jellyfin/Plex, pacote de codecs e versões de Store continuam necessitando contratos específicos. Não foram adicionados instaladores unsigned nem exceções à assinatura. A exceção histórica e fixa do 7-Zip não foi ampliada. AVG continua **não homologado**, independentemente de uma sessão sem alertas.

## Fontes primárias

- [Windows Installer ALLUSERS e contexto](https://learn.microsoft.com/en-us/windows/win32/msi/allusers), [ProductVersion](https://learn.microsoft.com/en-us/windows/win32/msi/productversion), [Upgrade Table](https://learn.microsoft.com/en-us/windows/win32/msi/upgrade-table).
- [Inno: parâmetros oficiais](https://jrsoftware.org/ishelp/topic_setupcmdline.htm), [opções de privilégio/contexto](https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm).
- [Manifesto EA 13.813.1.6327](https://github.com/microsoft/winget-pkgs/blob/master/manifests/e/ElectronicArts/EADesktop/13.813.1.6327/ElectronicArts.EADesktop.installer.yaml), blob `a1d1f0a01224132734106507235010fe1f57021a`.
- [Manifesto K-Lite Mega 20.1.0](https://github.com/microsoft/winget-pkgs/blob/master/manifests/c/CodecGuide/K-LiteCodecPack/Mega/20.1.0/CodecGuide.K-LiteCodecPack.Mega.installer.yaml), blob `478664c8444c12243b92b457a07ddab18faeda97`.
- [Manifesto Plex 1.43.5.11029](https://github.com/microsoft/winget-pkgs/blob/master/manifests/p/Plex/PlexMediaServer/1.43.5.11029/Plex.PlexMediaServer.installer.yaml), blob `9a94cdaf27a28face5568a71f41468b1989e7e42`.
- [Manifesto Outlook 1.2026.812.100](https://github.com/microsoft/winget-pkgs/blob/master/manifests/m/Microsoft/Outlook/1.2026.812.100/Microsoft.Outlook.installer.yaml), blob `8b18c0a2f614dabd0b1f88638690c7350dde467f`.
