# Lote 2 — drivers somente leitura

## Base e isolamento

Em 25/10/2026, antes de editar: `main` em
`410d65e23546976db456337c71e4705f10217d36`; PR #40 aberta em **Draft**, HEAD
`ef13aef49083f357f6a14f4de46f8bc976dbd830`. O Lote 1 já tinha CI aprovada
no HEAD `7405d83f308a141d2a7f6c68cc5a5dee4a19067c`
([execução 40214100769](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40214100769)).

Esta branch parte desse Lote 1 e o preserva. [PR #55, Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/55),
base `work-lote1-confiabilidade-relatorios`, head `work-lote2-drivers-leitura`.
As frentes WORK16/WORK17 foram identificadas, mas continuam independentes;
não houve merge entre frentes nem na main. Sem release estável, alteração
de manifesto/UAC, elevação automática, instalação ou remoção de drivers.
Nenhum acesso aos PCs pessoais.

## Implementação técnica

| Área | Comportamento disponível | Limite objetivo |
|---|---|---|
| D2: hardware | WMI lê HardwareID do mesmo PNPDeviceID; preserva IDs e presença indeterminada | Não infere modelo, IDs ou versão a partir do nome |
| D2: histórico Windows | Leitura limitada de `setupapi.dev.log`, seção de dispositivo, data local, INF explícito e resultado; correlação exige dispositivo **e** INF | Logs rotacionados, formatos não reconhecidos e trechos sem conclusão permanecem parciais; não reconstrói todo o passado |
| D2: pacotes | `SetupGetInfFileListW` enumera INFs publicados; o round trip SetupAPI vincula cada INF ao Store | Não enumera diretórios órfãos do Store; sem dispositivo observado não significa pacote ocioso |
| G1: fonte oficial | Consulta `Type='Driver'` ao Windows Update, sem downloader/installer/EULA; ID de hardware exato, GUID/revisão e metadados obrigatórios | Oferta aplicável relatada pelo Windows não substitui verificação independente de SO/arquitetura/payload; versão numérica não é inventada a partir do título |
| G2: prova criptográfica | SHA-256 e igualdade entre INF publicado/Store; `SetupVerifyInfFileW` verifica o INF com seu catálogo; confiança não estabelecida é separada | Não verifica todos os arquivos do pacote, origem de download nem garante revogação online; `IsSigned` WMI continua separado |
| Interface | Drivers → Consultar drivers → selecionar item → Investigar; botões Versões oficiais e Verificar INF | Cancelamento/prazo preserva resultado anterior; tarefa nativa ainda encerrando impede consulta duplicada |

Nenhum resultado possui capacidade de instalar, excluir ou manter drivers.
Não são executados comandos de catálogo, pnputil, DISM, scripts de fabricante,
instaladores, downloaders ou carregamento de drivers. A seleção de servidor
WUA vale apenas para esta consulta Microsoft; falhas de política ou da fonte
não são contornadas por fallback. O auto-upgrade do agente é desabilitado
na instância de consulta. Consultar pode atualizar metadados/cache do próprio
serviço Windows Update, sem instalar pacote ou mudar sua configuração.

O relatório de tela contém IDs locais para conferência humana. Esses dados,
nomes de hardware, caminhos e linhas brutas do log **não são enviados ao
diagnóstico do GF nem persistidos pelo novo fluxo**. A consulta WUA utiliza
o serviço Windows Update e o tratamento de dados do próprio Windows.
O histórico próprio já existente continua guardando hashes/contagens,
sem IDs brutos. Nenhum novo envio GitHub ou telemetria foi criado.

## Investigação R6

O classificador histórico já preserva homônimos de fabricantes, caminhos
sobrepostos a instalações, dados pessoais/compartilhados, serviços de driver
e inventários parciais. A presença de duas fontes não comprova exclusividade.

Foram acrescentadas amostras **sintéticas**, não cópias de dados dos PCs,
para Netease, GOG.com, IObit, Microvirt e Vivaldi: dois contêineres divergentes
permanecem com atribuição desconhecida e sem permissão de exclusão, inclusive
com inventário parcial. Driver Store e histórico Windows também não entram
como autorização de limpeza de aplicativos. Nenhuma regra de exclusão foi
ampliada ou proteção removida para aprovar um teste.

Isso investiga a ambiguidade e comprova a preservação; **não resolve os
vestígios reais desses produtos**. Sem evidência concreta da instalação
original, proprietários/dependentes e uso atual, R6 continua aberto.

## Evidências e critérios

Primeira CI do código: [40218082183](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40218082183),
fonte `6a16780e12f686a310e88b4e54d38f10065dcd21`, integração de teste
`5b0a6fc2d7d870d9bd603feb1b2fc1e9b14f5872`.

Os jobs nativos Windows 2022 e 2025 passaram em 25/10:

| Evidência inicial | Windows Server 2022 | Windows Server 2025 |
|---|---:|---:|
| Verificações aprovadas no job de drivers | 144 | 144 |
| Associações WMI (consulta completa) | 66 | 62 |
| INFs publicados enumerados | 666 | 732 |
| INFs da amostra com assinatura confirmada | 24/24 | 24/24 |
| INF sintético sem catálogo | Recusado | Recusado |
| Consulta real ao Windows Update | Completa, 0 ofertas | Completa, 0 ofertas |
| Eventos aproveitados do log Windows | 0; leitura parcial | 0; leitura parcial |

Zero ofertas no runner **não valida oferta positiva de hardware real**.
Zero eventos aproveitados com leitura parcial **não é histórico completo**;
os formatos do log do runner são investigados separadamente, sem emitir IDs
ou caminhos. O contrato de associação positiva é exercitado por fixtures. A segunda
execução [40218542822](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40218542822),
fonte `2757fa69f3638ae0de7837d30acf98befe1761d4`, confirmou os mesmos
resultados nativos e aprovou as dez verificações de ambiguidade R6.
Os formatos emitidos pelo executável de testes não contêm IDs/caminhos:
foram observadas 2/4 seções Device Install e 24/26 seções totais nos runners;
a leitura continuou parcial. Não foi atribuída uma instalação antiga ao
INF atual por inferência. Campos explícitos ndv de INF instalado foram
acrescentados com um teste que recusa o caminho de entrada do instalador.
A primeira execução local aprovou 136 verificações de drivers; compilação
do produto com analisadores, zero erros/avisos. A CI usa o SDK fixado no
projeto; a compilação local auxiliar usou SDK 10.0.103, com cross-targeting.

Os jobs de drivers sempre exercitam a ABI real e gravam evidências por SO.
Se WUA não responder ou exceder o prazo, o teste registra **INCONCLUSIVO**;
isso comprova preservação do estado, não disponibilidade da fonte. As provas
positivas de assinatura e enumeração são obrigatórias: sua ausência falha o job.
Capturas do diálogo em 500×340 e 790×510 são produzidas pelo próprio WinForms
com dados sintéticos e sem iniciar consultas ou manutenção.

**D2, G1, G2 e R6 continuam desmarcados. Roadmap: 27/42.**

Para concluir D2, ainda validar cobertura/interpretação de histórico real e
pacotes não associados, sem converter ausência em desuso. G1 requer uma
oferta positiva com versão e compatibilidade completas. G2 requer integridade,
assinatura, origem e compatibilidade de **todo** o pacote. R6 exige resolver
amostras históricas concretas. A homologação física, D4/G5 e os checkpoints
U8/R7/R4 não são substituídos por fixtures ou VMs.

## Fontes primárias usadas no desenho

- [SetupGetInfFileListW e MULTI_SZ](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupgetinffilelistw).
- [Formato do cabeçalho de seção SetupAPI](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/format-of-a-text-log-section-header)
  e [rodapé](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/format-of-a-text-log-section-footer).
- [Verificação Authenticode de INF e confiança no editor](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/using-setupapi-to-verify-driver-authenticode-signatures).
- [SP_INF_SIGNER_INFO_V2_W](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/ns-setupapi-sp_inf_signer_info_v2_w).
- [Propriedades de ofertas WUA de driver](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iwindowsdriverupdate-properties)
  e [IUpdateSearcher.Search](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search).

A revisão visual das duas capturas da primeira CI confirmou botões dentro
do viewport. O relatório foi ajustado para reutilizar DarkLogView (barra
escura e foco inicial legível); a regressão usa o diálogo real e dados
sintéticos. O estado de CI de cada HEAD e as evidências finais ficam na
[PR #55](https://github.com/walissonminel-1991/gerenciador-final/pull/55).
