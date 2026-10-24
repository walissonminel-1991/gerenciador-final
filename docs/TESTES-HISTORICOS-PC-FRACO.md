# Arquivo de testes do PC FRACO — outubro de 2026

Histórico consolidado na MISSÃO WORK 9. Os textos abaixo preservam resultados e planos das revisões indicadas; não são instruções para a candidata atual. Para o roteiro vigente, leia [TESTE-CONSOLIDADO](TESTE-CONSOLIDADO.md). Não houve nova execução física nesta missão.

- [TESTE-PC-FRACO-2026-10-20](#registro-1)
- [TESTE-PC-FRACO-2026-10-21-v5.13.11.18](#registro-2)
- [TESTE-PC-FRACO-2026-10-21-v5.13.11.19](#registro-3)
- [TESTE-PC-FRACO-2026-10-21-v5.13.11.20](#registro-4)
- [TESTE-PC-FRACO-2026-10-21-v5.13.11.21](#registro-5)
- [TESTE-PC-FRACO-2026-10-21-v5.13.11.23](#registro-6)
- [CHECKLIST-PC-FRACO-v5.13.11.17](#registro-7)
- [PLANO-PC-FRACO-v5.13.11.23](#registro-8)
- [PLANO-PC-FRACO-v5.13.11.24](#registro-9)

<a id="registro-1"></a>

## TESTE-PC-FRACO-2026-10-20.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-20.md) · SHA-256 original: `0708653fe4d03c1db38f586b97be93cdeacc26d22fbbe4ea1abf9f350e0b3479`

### Bateria PC FRACO — evidências de 20/10/2026

Status: **em investigação; não aprovada**. Este relatório contém apenas contagens técnicas e ações relatadas, sem perfis de usuário, dados identificáveis, nomes de dispositivos ou caminhos absolutos.

#### Segurança — P6 (bloqueio AVG)
- Durante a execução de v5.13.10.20, após minimização para bandeja e tentativa de restauração por duplo clique, o AVG exibiu a detecção comportamental `IDP.Generic`, realizou quarentena e interrompeu o uso normal.
- O log v5.13.10.20 termina após `DIAGNOSTICO_COMPLETO.FIM`, sem `APP.ENCERRAMENTO_NORMAL`. Isso é compatível com interrupção, mas não revela a causa da classificação.
- Uma coincidência temporal com a restauração da bandeja não comprova causalidade.
- Não restaurar/autorizar exceções nem repetir execução no PC até avaliação de integridade e resposta do antivírus. Preferir submissão para análise oficial pelo AVG.
- v5.13.10.21 adiciona apenas diagnóstico de transição da janela; não contorna o antivírus. **O bloqueio AVG não está resolvido.**

#### Inventário — I7
| Medida | v5.13.10.19 | v5.13.10.20 |
|---|---:|---:|
| Inventário total após desinstalação | 74 | 55 |
| Apps MSIX agregados | 19 | 0 |
| Status leitura MSIX | ok | COMException |
| Erros de leitura Win32 | 0 | 0 |

- A falha de enumeração MSIX foi detectada na segunda versão, não se sabe ainda se temporária ou relacionada ao ambiente.
- v5.13.10.21 passa a gravar o HRESULT sem mensagens sensíveis e contabiliza a falha. Não presume inventário completo quando MSIX indisponível.
- O Driver Booster deixou de aparecer na listagem de apps após nova atualização da lista (evidência das capturas).

#### Resíduos — R6/R7
- A primeira revisão pós-desinstalação observou um local e ainda viu registro relacionado; a confirmação do desinstalador pode ter ocorrido antes da remoção final.
- v5.13.10.20 reconheceu **3 locais específicos** de Driver Booster (duas pastas de dados e uma chave de Registro), **somente leitura**. Não comprova autorização para excluir.
- Outros grupos históricos examinados: GOG.com, IObit, McAfee, Microvirt — preservar enquanto a autoria/exclusividade estiver incerta.
- A análise geral da v5.13.10.20 foi **parcial** porque o inventário MSIX falhou. Ausência no inventário não prova remoção.

#### Drivers — D4
- Consultas v5.13.10.19 e v5.13.10.20: 107 associações, 49 INFs, três sem INF válido; sem novos/alterados/não observados desde a linha de base.
- O arquivo local `drivers-v1.json` contém esquema 1, 107 entradas resumidas e nenhum evento de mudança. Evitar compartilhar as identificações hash do histórico.
- É necessário comparar exemplos diretamente ao Gerenciador de Dispositivos; não inferir limpeza de pacotes.

#### Interface — V5
- O título da janela continuava mostrando v5.13.10.18 nas compilações posteriores por ser texto estático. Corrigido em código na v5.13.10.21.
- A rolagem clara da aba DRIVERS foi corrigida no lote v5.13.10.19; as barras de outras janelas deverão ser conferidas quando a execução voltar a ser segura.
- Nenhum teste real de desempenho ou restauração pode ser aprovado enquanto persistir a detecção.

#### Ações seguintes
1. Preservar quarentena, logs e referência ao alerta; enviar para análise oficial AVG.
2. Inspecionar integridade/procedência do pacote e resultados de ferramentas de segurança, sem recorrer a exclusões.
3. Após liberação/revisão, validar v5.13.10.21 em ambiente controlado e comparar HRESULT MSIX.
4. Confirmar inventário, preservação de resíduos e histórico de drivers antes de retomar operações modificadoras.

<a id="registro-2"></a>

## TESTE-PC-FRACO-2026-10-21-v5.13.11.18.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.18.md) · SHA-256 original: `c8bc927e92f08b560adab60d9babcb83874692bfdb48c23c23d956bd6a54b746`

### PC fraco — observações reais da v5.13.11.18 (21/10/2026)

Relatório **derivado de capturas e diagnóstico local fornecidos voluntariamente**.
Somente dados agregados: não republicar o log bruto, nomes de conta, identificadores
de dispositivos, caminhos pessoais nem `drivers-v1.json` neste repositório.

#### Ambiente e observação
- Windows x64; resolução física mostrada: **1440×900**, escala 100%.
- Aplicativo abriu e encerrou normalmente; nenhuma remoção de resíduos nem mudança de driver.
- Painel de privacidade abriu e a coleta local foi ativada pelo usuário; envio remoto desativado.
- Pesquisa de programas, filtros de drivers e filtros de resíduos funcionaram nas capturas.
- Bandeja: ocultação e restauração registradas; encerramento normal confirmado.

#### Métricas registradas pelo GF (uma sessão; não são benchmarks comparativos)

| Etapa | Tempo informado | Observação |
| --- | ---: | --- |
| Inicialização até janela | 1320,69 ms | Medida local, antes do consentimento |
| Primeiro inventário | 2013,67 ms | 74 itens no inventário |
| Inventário posterior | 1155,57 ms | Condições/cache podem variar |
| Primeira busca de atualizações | 6311,78 ms | RuckZuck 631 entradas |
| Busca posterior de atualizações | 1344,03 ms | Condições/cache diferentes |
| Descoberta geral de resíduos | 420,93 ms | 2000 entradas examinadas |
| Classificação de resíduos | 29,29 ms | Parte da descoberta, não somar tempos |
| Primeiro filtro de resíduos | 7,00 ms | 17 grupos na revisão exibida |
| Primeiro exame sob demanda | 31,95 ms | Apenas metadados |
| Pico de RAM do processo na sessão | 138,56 MiB | Não equivale à RAM total do Windows |

#### Inventário e atualizações
- 74 entradas de inventário, 54 desinstaláveis, 20 ocultas sem ação.
- Três **possíveis** atualizações em catálogo; nenhuma instalação validada por este teste.
- Uma atualização de Windows detectada; nenhuma atualização instalada.
- 108 associações de drivers, 50 arquivos INF, três associações sem INF; histórico
  com uma mudança adicionada relativa ao estado local prévio, não uma contagem
  de drivers novos ou removíveis. O JSON contém 108 chaves opacas, sem nomes/IDs brutos.

#### Resíduos: problemas observados
- Consulta geral terminou **parcial**: 12 origens, 2000 entradas examinadas, 16
  candidatos de descoberta, sete instalações a conferir e duas fontes inacessíveis.
  A revisão combinada exibiu 17 grupos e 57 locais; essas grandezas têm escopos
  diferentes e não devem ser somadas.
- **Falha de identidade:** subpastas genéricas com nome `Config` de dois
  aplicativos diferentes eram agrupadas como um suposto produto `Config`.
  Também apareceram grupos `Logs`, `Updates`, `Scripts` e `Providers`.
- **Falha de mensagem:** inspeção vazia exibia "Nenhum grupo nesta seleção",
  embora a seleção de produto continuasse presente.
- Permaneceram corretamente preservados dados de fabricantes/jogos, instalações
  não confirmadas, registros compartilhados e perfis pessoais.
- A parcialidade não deve desaparecer artificialmente: limites de profundidade,
  fontes não acessíveis e resultados não examinados continuam informados.

#### Correções preparadas na v5.13.11.19 TESTE
1. Nomes de subpastas genéricas não se tornam identidades independentes;
   evidências passam ao grupo do pai. Sem afirmar desinstalação ou exclusividade.
2. Testes sintéticos e de enumeração de diretórios/Registro com subpastas
   `Config` homônimas de fabricantes diferentes.
3. Texto distinto para lista de grupos vazia e inspeção de nível sem itens.
4. Exclusão real de resíduos continua indisponível.

#### Próximo teste controlado
Comparar na v5.13.11.19 o número de grupos/locais, ausência dos falsos
grupos genéricos, razões de parcialidade, tempos/RAM, suspensão/restauração
da bandeja e ausência de alerta do antivírus. Executar apenas operações de
leitura; não reinstalar, remover, reparar nem alterar drivers durante a bateria.

**Limite da evidência:** não há dados de eficiência de limpeza/desinstalação,
nem confirmação de cobertura integral do computador. A fonte é uma única
sessão de um PC real; screenshots não medem fluidez nem períodos de CPU.

<a id="registro-3"></a>

## TESTE-PC-FRACO-2026-10-21-v5.13.11.19.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.19.md) · SHA-256 original: `6361a37890a24b8ac786e30fb5c181193b3d3c15a97224d4b73a2ff6cbd7d8b5`

### Segunda bateria no PC fraco — v5.13.11.19 TESTE (21/10/2026)

Documento de evidências **agregadas**, extraídas de capturas e do arquivo de
etapas de teste fornecidos voluntariamente. Não anexar log bruto, contas,
caminhos pessoais, imagens com dados pessoais ou identificadores de hardware.

#### Resultado em 1440×900

- Janela aberta, filtros, pesquisa, classificação, inspeção sob demanda e
  encerramento normal. Nenhuma exclusão de resíduos, desinstalação ou
  alteração de drivers.
- Nomes de pastas genéricas `Config`, `Logs`, `Updates`, `Scripts` e
  `Providers` deixaram de ser exibidos como produtos independentes.
- Revisão combinada: **12 grupos**, **41 locais**. Bateria anterior: 17 grupos
  e 57 locais; a diferença é de agrupamento/classificação, não limpeza.
- Descoberta geral: 12 origens, **2.000 entradas examinadas**, 11 candidatos,
  3 instalações a conferir, 2 fontes inacessíveis, `parcial=True`.
  Não confundir grupos da revisão combinada com candidatos da descoberta.
- Seleção `Possível resíduo — revisar` não encontrou grupos: enquanto parcial,
  isso **não** significa que os dados sejam removíveis ou que nada exista.
- **Bug novo reproduzido pela captura**: ao escolher origem `Perfis do usuário`
  ou `Dados comuns`, a seleção de grupo era filtrada, mas a lista direita
  ainda exibia locais de outras origens. Ex.: grupo com ProgramData,
  AppData e Registro exibido por inteiro após selecionar uma origem.
- Outros achados seguem corretamente conservadores: Driver Booster, IObit,
  GOG.com, McAfee, HP e aplicativos de terceiros mantidos como dados ou
  instalações não confirmadas, sem licença para exclusão.

#### Métricas observadas da sessão (não são benchmark controlado)

| Etapa | Resultado |
| --- | ---: |
| Inicialização | 708,86 ms |
| Inventário inicial | 1.290,51 ms |
| Busca inicial de atualizações | 2.941,40 ms |
| Descoberta de resíduos | 294,66 ms |
| Classificação de resíduos | 29,34 ms (dentro da descoberta) |
| Primeiro filtro | 7,13 ms |
| Inspeções sob demanda | 8,77–22,56 ms |
| Pico de memória de processo | 134,2 MiB |
| Aplicativos inventariados | 74 |
| Possíveis atualizações | 2 (nenhuma instalada) |
| Associações de driver / pacotes INF | 108 / 50 |
| Alterações no histórico de drivers | 0 adicionados, 0 não observados, 0 alterados |

Um teste real em um computador não prova ausência de riscos em outras máquinas.

#### Correções planejadas na v5.13.11.20 TESTE

1. O filtro de **origem** e de **classificação** passa a selecionar também os
   locais individuais apresentados, não apenas os grupos.
2. Busca por termos presentes num local exige que o mesmo local corresponda
   à origem selecionada; os resultados não cruzam locais distintos para
   gerar falsos positivos.
3. Contadores de locais refletem os locais correspondentes aos filtros.
   Fotografia original e dados preservados.
4. Estado vazio tem mensagem distinta para filtros sem resultados e análise
   parcial sem candidato revisável, sem dizer que tudo está limpo.
5. Adicionar testes sintéticos e WinForms de interseção entre origem,
   classificação, pesquisa, contagem e navegação sem exclusão real.

#### Teste seguinte

Na próxima versão, repetir exclusivamente a análise somente leitura, selecionar
`McAfee` e alternar `Dados comuns`, `Perfis do usuário` e `Registro`:
somente os locais correspondentes devem aparecer à direita. Conferir também
interseção com `Compartilhado — preservar`, pesquisa, retorno a `Todas as
origens`, contagens e encerramento. Manter a análise parcial explicitada.

<a id="registro-4"></a>

## TESTE-PC-FRACO-2026-10-21-v5.13.11.20.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.20.md) · SHA-256 original: `67557c7199e0c927fbbe1bc3a9f3b6b9facc428f516106ed1eae252f5f6a0275`

### Terceira bateria real — v5.13.11.20 TESTE (21/10/2026)

Dados agregados provenientes de capturas e registro local fornecidos
voluntariamente para depuração. **Não** anexar arquivo bruto, capturas,
identificadores de usuários, aplicações pessoais ou hardware ao repositório.

#### Filtros e resíduos

**Correção confirmada no PC fraco:** o painel direito acompanha o filtro de
origem. Para um grupo representativo com evidências em
`ProgramData`, `AppData` e Registro, a seleção por origem apresentou:
- Todas: 3 locais;
- Dados comuns: 1 local correspondente;
- Perfis do usuário: 1 local correspondente;
- Registro: 1 local correspondente.

Interseções sem correspondência apresentaram mensagens vazias apropriadas,
sem afirmar que não há resíduos. Busca por fabricante e categorias funcionaram.
A investigação permanece **ANÁLISE PARCIAL** e **somente leitura**, sem
autorização de remoção.

#### Desempenho e resultados registrados (sessão única)

| Indicador | Resultado |
|---|---:|
| Inicialização | 622,01 ms |
| Primeira consulta do inventário | 314,00 ms |
| Primeira consulta de atualização | 2.759,57 ms |
| Descoberta de resíduos | 252,70 ms |
| Classificação (parte da descoberta) | 25,55 ms |
| Pico de memória de processo durante a sessão | 129,8 MiB |
| Entradas analisadas na descoberta | 2.000 |
| Fontes consultadas / inacessíveis | 12 / 2 |
| Possíveis atualizações catalogadas | 2 |
| Associações de drivers | 108 |
| Encerramento normal | Sim |

Os tempos não constituem comparação experimental controlada.

#### Problema novo de confiabilidade do inventário

O registro desta execução mostra:
- `MSIX.LEITURA status=COMException(0x80040154)`;
- 0 pacotes MSIX enumerados; falha de leitura;
- Inventário total desceu de 74 (versão anterior, com 19 aplicativos MSIX)
  para 55 (somente ARP + bibliotecas).
- O ARP não relatou falhas de leitura; nenhuma desinstalação de 19 aplicativos
  está comprovada por esses números.
- A janela principal indicava inventário sem advertência persistente; a lógica
  de proteção de resíduos já recebia o sinal `Incomplete`.

**Correção da v5.13.11.21:** uma nova tentativa controlada somente para
`REGDB_E_CLASSNOTREG` (`0x80040154`), sem comandos externos, elevação,
PowerShell ou busca em outros usuários. Se falhar, o aviso `INVENTÁRIO PARCIAL`
permanece visível na lista, tooltip e registro, inclusive depois de consultar
fontes online. A ausência de pacotes não indica que foram desinstalados.
Testes sintéticos distinguem falha transitória, falha persistente, permissão
negada e propagação da parcialidade à proteção de resíduos.

#### Próximo teste

Abrir o GF v5.13.11.21 no PC fraco em 1440×900, AVG ativo,
verificar no Registro do GF somente os campos `MSIX.LEITURA status`,
`repeticao` e `adicionados`. Confirmar se o inventário voltou a 74,
sem exigir exatamente 74 (o sistema pode mudar). Se ainda aparecer
`0x80040154`, confirmar o aviso persistente `INVENTÁRIO PARCIAL` e
repetir apenas o botão **Atualizar lista**. Não desinstalar, instalar,
limpar resíduos ou alterar drivers.

A solução de longo prazo para uma ativação WinRT permanentemente
indisponível exigirá diagnóstico isolado e sem dados pessoais; não presumir
que repetição sempre resolve a origem da falha.

<a id="registro-5"></a>

## TESTE-PC-FRACO-2026-10-21-v5.13.11.21.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.21.md) · SHA-256 original: `08f3ad8fb5ea5796ba00b5757074788738ac293e4709e7509075921aaa4c59c3`

### PC fraco 21/10/2026 — v5.13.11.21 TESTE (leitura MSIX restaurada)

Relatório técnico **agregado** de imagens e de registro de diagnóstico cedidos
voluntariamente, sem publicar o arquivo bruto, perfis, caminhos pessoais,
nomes privados de aplicativos nem identificadores de dispositivos.

#### Teste realizado

- Primeira consulta: `MSIX.LEITURA status=ok enumerados=123 aplicativos=19 adicionados=19 filtrados=104 falhas=0 repeticao=False`.
- Repetição manual por **Atualizar lista**: exatamente os mesmos indicadores.
- Inventário agregado: **74** registros, dos quais **19 MSIX** e
  **54 desinstaláveis**. A contagem representa categorias diferentes e
  não deve ser somada como total novo.
- A falha anterior `0x80040154` **não se reproduziu** na sessão. Não há
  prova de que a nova tentativa automática foi usada ou de que o erro não
  voltará; ambas as leituras concluíram na primeira tentativa.
- Consultas de driver: **108** associações, **50** INFs, **3 sem INF válido**,
  alterações no histórico: 0.
- Atualizações: **2** candidatos, nenhuma instalação realizada.
- Investigações de resíduos: **11** candidatos de descoberta geral,
  **12** origens consultadas, **2.000** entradas examinadas,
  **2** fontes inacessíveis, **parcial=True**. Nenhum resíduo removido.
- Diagnóstico completo final: 74 instalados, 0 desinstaladores ausentes,
  106 dispositivos consultados, parcial=False para essa etapa independente.
- Encerramento normal.

#### Tempos de execução registrados

- Inicialização: **719,94 ms**.
- Inventário inicial: **1.389,13 ms**; pós-atualização manual: **941,11 ms**.
- Atualizações iniciais: **3.204,70 ms**; após atualização: **1.519,60 ms**.
- Descoberta de resíduos: **886,48 ms**, depois **222,85 ms**.
- Filtro de resíduos: **3,2–6,4 ms** na maioria das interações.
- Pico de RAM do processo durante sessão: **144.580.608 bytes (~137,9 MiB)**.
- Estes números são de uma única sessão e não um benchmark controlado.

#### Problema de transparência identificado

A imagem mostra o usuário pesquisando por `MSIX` na aba Aplicativos, com
filtro **Possíveis** ativo, sem qualquer linha. Os 19 itens MSIX estavam
corretamente no inventário, mas eram `InventoryOnly` e não tinham ação
permitida, portanto **não apareciam em nenhum filtro padrão de ações**.
O campo de busca só pesquisa os itens já visíveis: o retorno vazio não
significa falha da leitura MSIX.

O mesmo termo na aba Drivers não corresponde a hardware ou INF; e o
explorador de resíduos não é um inventário de pacotes instalados.

#### Melhoria v5.13.11.22

Adicionar o filtro explícito **Inventário** na aba Aplicativos. Essa visão
consulta todas as entradas já armazenadas no inventário local, inclusive
Store/MSIX, e permite buscar por `MSIX` usando o campo da origem do
inventário. Entradas sem ação autorizada exibem **Somente leitura** em
vez de botão clicável; a tela **Todos** permanece focada em ações.

Não alterar as políticas de autorização: sem permissão para atualizar,
desinstalar ou executar comandos em itens inventariados. Testes de
regressão verificam o filtro, as ações proibidas e encaixe de quatro
filtros na janela em várias resoluções. Continuar trabalho separado
para distinguir resíduos genuínos de dados ativos, sem exclusão.

<a id="registro-6"></a>

## TESTE-PC-FRACO-2026-10-21-v5.13.11.23.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.23.md) · SHA-256 original: `4e70abb4670264d2ff72b240e666b58d3870e35ddbcc26637570aaaf19d7748e`

### Resultado agregado — PC fraco v5.13.11.23 TESTE (21/10/2026)

Este relatório resume capturas e diagnóstico cedidos voluntariamente. Não
publica logs originais, caminhos pessoais, nomes de perfis, IDs de dispositivos
ou listas de aplicativos.

#### Inventário e atualização
- Inicialização 696,54 ms; inventário 1.601,07 ms.
- 74 entradas, incluindo 19 MSIX; 123 pacotes MSIX enumerados,
  0 falhas, nenhuma segunda tentativa necessária.
- 54 desinstaláveis e 20 sem ação. Filtro Inventário mostra MSIX
  em somente leitura.
- 2 possíveis atualizações do catálogo, sem instalar nenhuma;
  1 atualização do Windows encontrada, sem instalar.
- 108 associações de drivers, 50 INFs, 3 sem INF;
  sem mudanças históricas identificadas.

#### Descoberta histórica
- 12 origens, 2.000 entradas, 11 grupos candidatos gerais,
  3 instalações a conferir; análise parcial.
- Motivos EXATOS: `ReadFailure+DepthLimit+LinkSkipped`.
  Não houve flag `EntryLimit` nesta consulta. 2.000 foi quantidade
  observada, não prova de limite global atingido.
- Após reunir fontes, explorador mostra 12 grupos e 41 locais;
  o resumo combinado contabiliza 2.110 entradas entre análises.
  As duas contagens têm escopos diferentes.
- A .23 nomeava fonte inteira indisponível por falha de leitura pontual;
  revisão .24 diferencia fontes lidas parcialmente de indisponíveis.
- Nenhuma exclusão, alteração de drivers ou instalação executada.
- Descoberta 579,03 ms; classificação 25,01 ms; encerramento normal.

#### Limites
Uma sessão real não é benchmark controlado. AVG não foi
certificado por esta consulta. Os resultados não provam que
qualquer vestígio está seguro para exclusão.

<a id="registro-7"></a>

## CHECKLIST-PC-FRACO-v5.13.11.17.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/CHECKLIST-PC-FRACO-v5.13.11.17.md) · SHA-256 original: `38df6129990531f0a08ad07bf0253af7fdeac3269e08e13eba36636733501bae`

### Checklist para preencher — PC FRACO / v5.13.11.17 TESTE

Data/hora local: __________ · Windows (versão): __________ · Resolução: **1440×900** (confirmar: ______) · Escala do Windows: ____% · RAM total: ____ GB · Antivírus e versão: __________

Não preencher nome do computador, usuário, números de série, IP, senhas ou caminhos pessoais. Resultados abaixo são locais. As colunas ficam em branco até a medição real.

#### Preparação

- [ ] Artefato final indicado na PR #40; versão e SHA-256 conferidos.
- [ ] .16 e .17 extraídas em pastas diferentes; uma versão aberta por vez.
- [ ] Manter EXE e arquivos adjacentes. Não instalar SDK no computador de testes.
- [ ] Antivírus/SmartScreen/UAC ativos; não executar pacote em quarentena nem criar exceção para forçar teste.
- [ ] Anotar estado comparável de outros programas e carga do Windows; manter uso normal registrado.
- [ ] Não usar botões de desinstalar, reparar, limpar temporários, instalar atualizações ou alterar drivers nesta bateria.
- [ ] Usar somente consultas, filtros e inspeções de resíduos; nenhuma alteração do Registro.

#### Comparação de duração e recursos

Executar uma passagem por versão; quando possível repetir três vezes e registrar mediana, distinguindo primeira abertura de reabertura. Não chamar uma passagem aquecida de “abertura fria”. Se as condições diferirem, registrar como não comparável.

| Medição | .16 — observado | .17 — observado | Condição/observação |
|---|---|---|---|
| Clique até primeira janela funcional (cronômetro) | ____ s | ____ s | fria / reabertura: ____ |
| Startup interno até Shown (diagnóstico) | ____ ms | ____ ms | .16 pode não possuir esta métrica |
| Inventário de programas | ____ ms/s | ____ ms/s | contagem e parcialidade: ____ |
| Consulta de atualizações, sem instalar | ____ ms/s | ____ ms/s | internet/carga comparáveis: ____ |
| Descoberta de resíduos — total | ____ ms/s | ____ ms/s | fontes/limites: ____ |
| Classificação de resíduos | ____ ms | ____ ms | etapa separada da consulta total |
| Pesquisa no conjunto de resultados | ____ ms | ____ ms | mesma busca, sem dados pessoais no relato |
| Troca de página | n/a | ____ ms | índice preservado, sem nova varredura |
| Inspeção de local escolhido | n/a | ____ ms | completa no nível / parcial: ____ |
| RAM antes da consulta | ____ MB | ____ MB | Gerenciador de Tarefas |
| Maior RAM observada durante consulta | ____ MB | ____ MB | observação manual, não confundir com pico da sessão |
| RAM após concluir e fechar revisão (aguardar ~10 s) | ____ MB | ____ MB | GC não é forçada; heap pode ser reutilizado |
| CPU observada durante consulta | ____% | ____% | faixa/carga dos demais programas: ____ |
| CPU acumulada da etapa no diagnóstico | n/a ou ____ ms | ____ ms | é tempo de CPU, não percentual |
| Alocações de processo na etapa | n/a | ____ bytes | objetos transitórios, não RAM retida |

Na .17, procurar linhas **DESEMPENHO.LOCAL** no diagnóstico exibido pelo GF. Etapas: Startup, Inventory, Updates, ResidualDiscovery, ResidualClassification, ResidualFilter e ResidualInspection. As linhas trazem somente etapa e números. Discovery inclui classificação; não somar os dois tempos. RAM/CPU/alocações podem incluir outras tarefas do próprio GF; pico é da sessão inteira. Campos ausentes são indisponíveis, não zero. Não enviar o arquivo bruto sem revisar seu conteúdo.

#### Navegação e clareza

- [ ] Janela e botões visíveis em 1440×900; texto essencial legível.
- [ ] Trocar de aba durante consultas não trava a interface.
- [ ] Cartões e rolagem escura funcionam sem pausas longas.
- [ ] Resumo diferencia grupos disponíveis, carregados nesta página e nas demais.
- [ ] “Próxima/Anterior” percorre os grupos e preserva a seleção ao voltar.
- [ ] Pesquisa encontra programa/fabricante além da primeira página.
- [ ] Filtros de classificação e origem operam sobre todos os grupos.
- [ ] Programa com muitos locais permite navegar pelas páginas de locais.
- [ ] Resumo informa fontes indisponíveis, limites, duplicatas e indícios isolados.
- [ ] Análise parcial vazia é distinguida de ausência de resíduos.
- [ ] Inspeção mostra metadados e motivos de preservação no mesmo painel, sem ler conteúdo pessoal.
- [ ] Profundidade/quantidade limitada fica explícita; botão não promete inspeção onde já está bloqueada.
- [ ] Botão **Cancelar** funciona durante consulta principal, filtro ou inspeção. Chamadas nativas podem precisar retornar.
- [ ] Resposta atrasada após cancelamento não substitui a seleção/fotografia atual.
- [ ] **Exclusão indisponível nesta versão de teste** permanece visível; nenhum botão funcional de remoção.

#### Preservação e ciclo da janela

- [ ] foobar2000: playlists/configurações/componentes preservados, se presentes.
- [ ] VLC: preferências/histórico/cache tratados como dados a conferir, sem remoção.
- [ ] Fabricantes/jogos e pastas compartilhadas preservados.
- [ ] Netease/Driver Booster/instalações portáteis exigem conferência.
- [ ] Minimizar para bandeja e restaurar durante consultas: sem duplicar janelas ou iniciar outra busca automaticamente.
- [ ] Fechar a revisão, reabrir a consulta e fechar/reabrir o GF: sem callback antigo ou dados apresentados como novos.
- [ ] Nenhum alerta de antivírus observado / alerta observado (descrever): __________

#### Registrar divergências

| Cenário | Esperado | Observado | Repetiu? | Mensagem/contagem agregada |
|---|---|---|---|---|
| __________ | __________ | __________ | sim / não | __________ |
| __________ | __________ | __________ | sim / não | __________ |

Antes de compartilhar capturas, ocultar caminhos/nomes privados. Não alterar ACLs, criar junctions ou fabricar resíduos em pastas reais. Esta bateria não autoriza exclusão, desinstalação ou mudanças de drivers.

<a id="registro-8"></a>

## PLANO-PC-FRACO-v5.13.11.23.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/PLANO-PC-FRACO-v5.13.11.23.md) · SHA-256 original: `fa681d51b56c6fe1b5eb553455067c4220d2810ad9f23d4e0b34165b539e245f`

### PC fraco — v5.13.11.23 TESTE: diagnóstico de parcialidade (plano de validação)

#### O que foi corrigido no código
- Resumo da investigação de resíduos passa a exibir **motivos específicos**
  de cobertura parcial (quantidade, tempo, permissão/leitura, profundidade,
  link ignorado, fonte indisponível etc.) em texto fixo, sem caminhos privados.
- A linha local `RESIDUOS_GERAIS.CONSULTA_FIM` ganhou `motivos=` com flags
  agregadas (separador `+`) para distinguir o motivo efetivo do teste.
- `ResidualScanBudget.Unavailable` não atribui a uma fonte anterior
  a indisponibilidade da fonte explicitamente indicada.
- Três testes adicionais de regressão cobrem o erro de atribuição,
  a distinção entre falha de leitura e indisponibilidade e os rótulos.
- Nenhuma rotina de desinstalação, exclusão, atualização, driver ou rede
  foi alterada por esta revisão.

#### Interpretação rigorosa do teste anterior (.21)
O log anterior registrou **2.000 entradas examinadas** e **2 fontes
inacessíveis** em uma consulta parcial; **2.000 é a quantidade observada,
não necessariamente um limite global configurado**. Há limites específicos
por origem, quantidade por subpasta, tempo e profundidade. Sem os motivos
agregados não é correto concluir qual limite encerrou a investigação.

#### Bateria segura no PC fraco (1440×900)
1. Extraia a nova candidata em pasta própria, sem substituir a versão anterior.
   Mantenha o AVG ativo e não crie exceções para contornar alertas.
2. Em Aplicativos > Inventário, pesquise `MSIX`. Conte as linhas e observe
   se os itens continuam rotulados como `somente leitura`; sem ações de
   remoção. Volte ao filtro Todos.
3. Abra Investigação de resíduos em **somente leitura**. Observe a primeira
   linha do resumo, especialmente `ANÁLISE PARCIAL — ...`; alterne fontes,
   classificações, buscas e paginação sem apagar nada.
4. Abra Resumo. Compare motivos e fontes indisponíveis com os resultados
   da tela; anote os motivos, não caminhos pessoais.
5. Feche normalmente; no log local sanitizado, localize
   `RESIDUOS_GERAIS.CONSULTA_FIM` e `motivos=`.
6. Registre tempo de abertura, RAM/CPU, integridade visual e qualquer
   intervenção do AVG. Não desative a proteção.

**Expectativa, não promessa:** o inventário pode repetir 74 apps / 19 MSIX
e drivers 108 se nada mudou no sistema. Limites e permissões podem variar.
O teste do retry MSIX sob erro 0x80040154 real ainda não foi reproduzido.

#### Estado de segurança
**Nenhuma exclusão de resíduo ou instalação/desinstalação autorizada nesta
bateria**. PR #40 continua Draft; nenhuma publicação ou merge na main.
Roadmap 26/42 até validação adicional. A implementação não valida o
comportamento no Windows antes de a candidata ser executada no PC fraco.

<a id="registro-9"></a>

## PLANO-PC-FRACO-v5.13.11.24.md

[Original no checkpoint 473a9558](https://github.com/walissonminel-1991/gerenciador-final/blob/473a9558c33d12a3ce29c1ac51a6aed8730414ef/docs/PLANO-PC-FRACO-v5.13.11.24.md) · SHA-256 original: `9a139319a9cbbc0c887d651a41c830aa5643a99d5984ecd971bace72f499ad5a`

### Checklist PC fraco — v5.13.11.24 TESTE

1. Extrair pacote .24 em pasta nova; manter AVG ativo. Se houver alerta,
   não criar exceção nem restaurar binário de quarentena.
2. Aplicativos > Inventário: pesquisar `MSIX`, confirmar linhas
   somente leitura; resultados anteriores 74 apps / 19 MSIX.
3. Abrir Investigação de resíduos somente leitura; clicar Resumo.
   Procurar distinção **fontes com falhas pontuais de leitura**
   versus **fontes não consultadas ou inacessíveis**.
4. Verificar que o aviso ANÁLISE PARCIAL continua e que filtros
   por fonte não misturam Registro, dados comuns e perfis.
5. No log sanitizado, verificar `motivos=`, `raizes_restritas=`,
   `fontes_leitura_parcial=` e
   `fontes_nao_consultadas_ou_inacessiveis=`.
   São contagens/flags; não publicar caminhos locais.
6. Medir RAM, duração da descoberta, rolagem e encerramento.
   Não alterar drivers, apagar resíduos, instalar ou desinstalar.

Os limites de segurança (dois níveis e sem seguir links) permanecem.
A cobertura parcial não é defeito suficiente para autorizar limpeza.
PR #40 permanece Draft; roadmap 26/42.

