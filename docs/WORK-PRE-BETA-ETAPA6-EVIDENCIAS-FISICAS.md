# Pré-BETA — Etapa 6: evidências dos PCs FORTE e FRACO

27/10/2026 (America/Sao_Paulo). Branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Base documental conferida: `3b6061c33df54cd2b5689a1cabacb866e9a28ff4`.
**Consolidação concluída; homologações parciais; roadmap 27/42; Etapa 7 não iniciada.**

## Resultado e procedência

As cinco fontes E1–E5 foram recuperadas e relidas integralmente para esta
consolidação, sem novos testes nos computadores. Os agregados verificáveis,
linhas de origem e avaliações estão na
[evidência estruturada](evidencias/pre-beta-etapa6-fisica.json).
Os logs brutos, nomes identificáveis dos históricos e IDs privados não são
publicados. Relatos do mantenedor permanecem separados de medições registradas.

| Fonte | Conteúdo disponível | Alcance e limite |
| --- | --- | --- |
| E1 | Diagnóstico .64, PC FORTE, 25/10/2026, 153 linhas | Inventário, desempenho, pacotes, drivers e encerramento; versão no cabeçalho, sem SHA/hash da build |
| E2 | Diagnóstico .65, PC FRACO, 27/10/2026, 127 linhas | Inventário, desempenho, drivers, bandeja e encerramento; versão no cabeçalho, sem SHA/hash da build |
| E3 | Relatório local de confiabilidade, 14 linhas | Inventário 74 completo; base/comparação com zero mudanças; repete os eventos de E5 |
| E4 | Primeiro histórico local, três linhas | 27/10, 11:56:18→11:59:45 UTC: 74, zero novos/não observados/alterados |
| E5 | Segundo histórico local, três linhas | 27/10, 12:15:04→12:16:00 UTC: 74, zero novos/não observados/alterados; mesma sessão de E3 |

E3/E4/E5 não registram versão internamente; a associação ao PC FRACO/.65 vem
do contexto fornecido pelo mantenedor. E3 e E5 não contam como dois testes
independentes. A versão .65 também é mantida nos commits pré-BETA posteriores:
o número de versão, sozinho, **não homologa o código 386e7cd**. As referências
aos artefatos antigos permanecem contextuais, sem inventar um vínculo de hash
que esses logs não contêm.

## Comparação física consolidada

| Indicador | PC FORTE / E1 / .64 | PC FRACO / E2 / .65 | Interpretação |
| --- | --- | --- | --- |
| Windows / processo / runtime | 10.0.26300.0; x64; .NET 10.0.13 | 10.0.26200.0; x64; .NET 10.0.13 | Windows, hardware e versão do GF diferem |
| Inventário principal | 169, repetido; zero erros de Registro | 74, repetido; zero erros de Registro | Estabilidade da contagem não prova cobertura universal nem equivalência com outra lista |
| MSIX | 27, status ok, zero falhas | 19, status ok, zero falhas | Já incluídos nos totais 169/74; não somar novamente |
| Com desinstalador / pré-classificação silenciosa | 130 / 61 | 54 / 40 | Pré-classificação exige preflight; não são remoções executadas |
| Candidatos / fontes consultadas OK | 10 / 13 de 13 | 8 / 4 de 4 | Descoberta consultiva, sem comprovar payload executável |
| Atualização silenciosa / supervisionada elegível | 0 / 0 | 0 / 0 | Candidatos não viram atualizações homologadas |
| Diagnóstico de escopo amplo | 267 instalados | 122 instalados | Escopo diferente do inventário principal; não indica ganho de cobertura |
| Encerramento | Normal, L153 | Normal, L127 | Comprova término dessas sessões, sem diagnosticar AVG |

Inventário/MSIX: E1 L14–L17 e L76–L79; E2 L14–L17 e L64–L65.
Fontes/candidatos: E1 L62 e L137; E2 L55/L106. Capacidades: E1 L38,
E2 L36/L87. O resultado MSIX .65 supera a fotografia histórica .24 daquele
computador; não comprova causa/resolução universal do antigo erro.

## Desempenho: amostras disponíveis e lacuna objetiva

| Medida registrada | PC FORTE | PC FRACO |
| --- | --- | --- |
| Startup interno | 376,16 ms | 667,98 ms |
| CPU do processo durante Startup | 359,38 ms | 593,75 ms |
| Inventory, três amostras na mesma sessão | 621,03 / 479,74 / 481,42 ms | 1.335,91 / 953,82 / 959,49 ms |
| Updates, duas amostras | 7.422,66 / 8.755,21 ms | 12.358,66 / 7.197,53 ms |
| Working set depois do Startup | 62,57 MiB | 58,19 MiB |
| Working set depois do primeiro Inventory | 91,11 MiB | 86,18 MiB |
| Working set da última amostra | 236,16 MiB | 165,77 MiB |
| Maior pico da sessão registrado | 238,66 MiB | 169,11 MiB |

MiB = bytes / 1.048.576; bytes exatos constam do JSON. Fonte: E1
L8/L13/L63/L75/L139/L149; E2 L8/L13/L56/L63/L107/L124.
`LocalPerformanceMetrics` mede a diferença de TotalProcessorTime durante a
etapa: `cpu_ms` não é percentual nem medição de repouso. Startup interno não
mede todo o tempo percebido até inventário e consultas concluídos. Picos e
amostras após consultas não bastam para concluir vazamento ou consumo ocioso.

Há um Startup por PC, não três sessões equivalentes. O relato de aproximadamente
60 MB no PC FRACO não fornece a série de repouso. O critério já registrado em
[TESTES](TESTES.md), Bateria A, item 10, pede sessões da mesma build e CPU/RAM
em 30/60/120 s. Essa parte não está em E1/E2. P5 avançou em evidência, mas
continua aberto. Não calcular ganho/regressão do código pela diferença entre PCs.

## Drivers: leitura não equivale a interpretação validada

| Evidência | PC FORTE | PC FRACO | Limite |
| --- | --- | --- | --- |
| Associações / INFs | 166 / 63 | 119 / 53 | Dispositivos, associações e pacotes têm denominadores diferentes |
| INFs compartilhados / associações sem INF | 19 / 3 | 17 / 3 | Nenhuma dessas categorias autoriza limpeza |
| PnP no diagnóstico | 164 | 117 | A diferença em relação ao WMI não prova dispositivo faltando ou lixo |
| Mapeamento detalhado de pacotes | 63/63 mapeados; parcial=1 | Sem registro detalhado equivalente | Inventário parcial=False não torna todo o histórico/Store completo |
| Histórico próprio | Primeira base, zero alterações | Primeira base, zero alterações | Não equivale ao histórico SetupAPI completo ou backup para rollback |

E1 L140–L144/L151; E2 L108–L110/L125. E1 L143 ainda registra três nomes e
fornecedores ausentes, uma versão ausente, duas incomuns, uma assinatura e duas
presenças indeterminadas. Não inferir correção desses atributos pela contagem.
Falta conferência humana dos vínculos e da interpretação em ambos os PCs.
D2/D4 continuam abertos. A consulta Windows Update geral 1/0 não comprova
oferta positiva específica de driver para G1. G2 exige o pacote inteiro.
CanInstall/CanMaintain continuam false; G3/G4/G5 ficam desativados.

## Interface, estabilidade e relatos

E1 L66 registra 2560×1440 e DPI 120 (125%); E2 registra ocultar/restaurar a
bandeja em L113–L119. A resolução 1440×900 do FRACO é informação do mantenedor;
seu DPI não está em E2. Logs de visibilidade/bandeja não demonstram cortes,
legibilidade ou foco na imagem. As cinco fontes não contêm capturas com
aprovação visual vinculada ao artefato e DPI dos dois PCs. Relatos visuais
anteriores continuam aproveitáveis quando essa vinculação for recuperada.

O mantenedor relatou 11 de 12 testes sem falha observada; **T11, iniciar com
Windows, não foi executado**. É relato, não 11 verificações automatizadas.
Ativar a opção (E1 L68) ou restaurar a bandeja (E2) não comprova boot/login.
V5 continua aberto. T11 é uma pendência específica; não exige repetir os
outros onze itens nem reiniciar o computador nesta missão. A aprovação visual
de V5 é avaliada separadamente de T11; não se cria um critério novo de roadmap.

E1/E2 terminam normalmente. Isso aproveita evidência de estabilidade, mas
ausência de alerta em uma sessão não explica nem resolve o incidente AVG.
P6 exige a causa/resolução e evidência aplicável ao pacote exato. Não provocar
o alerta, criar exceções, desativar proteção ou reenviar arquivo para obter prova.

## Avaliação individual dos checkpoints ainda abertos

| ID | Evidência aproveitada | Falta para concluir; estado após a Etapa 6 |
| --- | --- | --- |
| I7 | Leituras repetidas 169/74; MSIX 27/19 ok; E3–E5 estáveis | Conferência de cobertura com referência local do mesmo escopo e explicação de ausências/duplicidades nos dois PCs; **aberto** |
| P5 | Startup, CPU por etapa e RAM/tempos medidos nos dois PCs | Artefato/condições vinculados e série comparável de repouso prevista no roteiro; **aberto** |
| P6 | Encerramentos normais e relato sem falha | Causa/resolução AVG e evidência do hash exato, sem contornar proteção; **aberto** |
| V5 | Resolução/DPI FORTE, bandeja FRACO e relatos | Aprovação visual vinculada a artefato/resolução/DPI em ambos; T11 permanece não executado; **aberto** |
| D4 | Associações/INFs, compartilhamento e limites conhecidos | Conferência humana dos vínculos, atributos inconclusivos e histórico em ambos; **aberto** |
| D2 | Mapeamento parcial do FORTE e histórico próprio inicial | Cobertura/interpretação verificável do histórico Windows e pacotes reais; **aberto** |
| R4 | Implementação/fixtures da Etapa 4; políticas nas sessões antigas | Homologação física de remoção/preservação e cobertura específica, sem operação pessoal nesta missão; **aberto** |
| R6 | Casos históricos continuam protegidos | Propriedade exclusiva, dependentes e uso das amostras reais ambíguas; **aberto** |
| R7 | Histórico estável e desinstaladores disponíveis | Ciclo antes/depois de desinstalação e preservação nos dois PCs; zero mudanças não é prova desse ciclo; **aberto** |
| U8 | Relato 7-Zip .52 já preservado; candidatos 10/8 | Homologação específica restante de instalação/versão nos dois PCs; **aberto** |
| G1 | Consulta oficial e fixtures anteriores | Oferta positiva real com versão/compatibilidade completas; **aberto** |
| G2 | Prova anterior limitada a INF/catálogo | Payload completo, origem, assinatura e compatibilidade; **aberto** |
| G3 | Estudo da Etapa 5, sem execução | Instalação/confirmação homologadas em ambiente especificamente autorizado; **desativado/aberto** |
| G4 | Limites de suporte/recuperação estudados | Recuperação e reversão suportadas e comprovadas; **desativado/aberto** |
| G5 | Dependências G1–G4 identificadas | Fluxo supervisionado em ambiente autorizado, após os pré-requisitos; **desativado/aberto** |

Nenhum checkpoint mudou de estado. **27/42 (64,3%), 15 pendentes.**
Não foram criados requisitos adicionais para substituir os critérios vigentes;
as lacunas acima correspondem a ROADMAP/TESTES e às etapas anteriores.

## Roteiro mínimo remanescente, sem execução nesta sessão

Reutilizar qualquer prova existente que satisfaça o ponto específico antes de
pedir nova medição. Registrar versão **e identidade do artefato**, resolução,
DPI, ambiente e resultado agregado; IDs/caminhos privados ficam no PC.

| Bloco focal | Somente o ponto ausente | O que não precisa ser refeito |
| --- | --- | --- |
| F1 — I7/D2/D4, leitura | Conferir localmente escopos equivalentes de aplicativos e vínculos de rede/vídeo/storage/um INF compartilhado, se presentes; registrar divergências e cobertura do histórico | Não repetir inventários completos apenas para obter novamente 169/74; não comparar contagem WMI com PnP como listas idênticas |
| F2 — P5 | Aproveitar sessões da mesma build já existentes; completar somente as amostras de abertura/repouso 30/60/120 s faltantes, nas condições do item 10 de TESTES | Não repetir consultas de atualização/desinstalação nem medir versões diferentes como se fossem o mesmo artefato |
| F3 — V5/T11 | Recuperar aprovação/capturas recortadas de controles, leitura, foco e rolagem no DPI real; confirmar só T11 quando houver boot/login futuro autorizado | Bandeja ocultar/restaurar já registrada; não repetir T01–T10/T12 nem reiniciar agora para fabricar evidência |
| F4 — P6 | Relacionar detecção original, hash e retorno/resolução AVG já disponíveis; exigir resultado aplicável ao pacote exato para homologá-lo | Não repetir o bloqueio, reenviar ZIP, criar exceções ou desligar proteção |

R4/R7/U8 exigem operações específicas futuras, com autorização, backup e
ambiente apropriado; esta etapa não as executa nem solicita uma bateria em
PC pessoal. R6 exige primeiro evidência de propriedade, sem exclusão. G1/G2
continuam dependentes de prova específica; G3–G5 não geram roteiro de escrita
nos PCs nesta missão. Os quatro blocos focais são planejamento remanescente,
**não testes executados ou aprovação para operações futuras**.

## Metadados corrigidos e validação

PROGRESSO passa a refletir a sessão física mais recente .65/FRACO e registra
a .64/FORTE separadamente. Os antigos registros .52/FORTE e .24/FRACO foram
preservados integralmente em `historical_real_tests_before_pre_beta_etapa6`;
o relato de atualização 7-Zip não foi apagado. Os cinco checkpoints
prioritários recebem evidência atual, mantendo todos os 42 valores done.
Campos de versão, candidata, artefato, CI de distribuição e hashes anteriores
permanecem intactos. ROADMAP/TESTES apontam a consolidação e o roteiro focal,
preservando as baterias anteriores como histórico, sem reordenar o produto.

Validação documental e de metadados: agregados/linhas conferidos nas cinco
fontes, JSON/referências/IDs consistentes, histórico preservado, booleans
27/42 inalterados e verificador de metadados de pacote existente aprovado.
Diff restrito a sete Markdown/JSON, sem alterações fora de documentação.
Zero testes de produto novos/reexecutados; zero build ou sessão física nova.

CI [40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517)
reconferida: código `386e7cd5263266a0478e60139e59b036f1d69e31`, tentativa 1,
success, seis jobs obrigatórios aprovados e dois condicionais skipped. A
Etapa 5 foi somente documental, sem execução de CI; não é falha nem build.
O novo recibo também usa `[skip ci]`; não atribuir a ele uma nova validação de
produto. PR #64 continua Draft; referências #60/#61/#62/#63 e main conferidas
e preservadas, inclusive falha externa histórica da #62, sem reinvestigação.

Exclusão permanente do VLC preservada, sem teste/consulta/verificação/botão
novo. Drivers desativados, PCs e dados pessoais intactos, sem elevação/ACL,
reinício, WinGet, merge/release/deploy ou acesso à BETA/Worker/D1/DNS/flags.
**Parada antes da Etapa 7**, que avaliará integração e decisão técnica em
outra sessão, sem autorização de publicação automática.
