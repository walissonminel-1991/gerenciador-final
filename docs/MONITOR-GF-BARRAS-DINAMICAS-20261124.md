# Monitor GF — frente isolada V2.6.9
Data: 24/11/2026. Base congelada: a2f07e13f74849d9295af884d52f982e1364ff21.
Referência visual aprovada pelo proprietário: GF_V2.6.9_Monitor_Referencia_Visual_Aprovada_2026-11-24.png (Biblioteca pessoal; imagem ainda não versionada no repositório). HTML V2.6.9 da PR #109 continua referência visual global.

## Escopo
- Monitor opcional e consultivo: modo compacto em linha única, ícones, CPU/GPU/RAM %, temperaturas CPU/GPU, expandir e fechar. Sem barras nesse modo.
- Expandido em dois painéis compactos: CPU/RAM/GPU/VRAM %, duas temperaturas, barras e legenda discreta. Sem GB, frequência, discos, uptime, manutenção, abas ou encerramento de processos.
- Azul GF, Dark e Claro reutilizam tokens V269VisualTokens; modo claro tem cores de estado mais escuras para leitura.
- Janela flutuante nativa, arrastável, alternância de modo, menu de sempre no topo, sem serviço, driver ou elevação. O host futuro controlará preferência, posição, visibilidade e bandeja.
- GfDynamicProgressBar independente: utilização 0–40 azul, 40–60 azul→verde, 60–75 verde→amarelo, 75–100 amarelo→vermelho; interpolação uniforme da cor do preenchimento. Escala não é alerta de falha para CPU/GPU em alta carga.
- Temperatura: âncoras 40°C/limite−25°C/limite−10°C/limite; se limite não comprovado, referência 40/60/78/95°C, não diagnóstico. Limite deve vir do mesmo componente e espécie de sensor. Temperatura desconhecida = —. Limite conhecido atingido não sofre atraso de suavização.
- EMA temporal de 3 s CPU/GPU/temperatura, 2 s RAM/VRAM; valor numérico bruto preservado, barra/cor suavizadas, amostra ausente ou troca GPU reinicia. Timer de apresentação de 250 ms, sem consulta de hardware; o timer de frescor invalida amostra que deixa de chegar.
- Progresso padrão azul e verde somente por sucesso confirmado; opção cromática invertida vermelho→amarelo→azul em execução, verde confirmado; falha/cancelamento são estados semânticos distintos, não inferidos pelo percentual. Indeterminado não inventa percentual.
- O adaptador recebe snapshots do motor existente. WDDM é motor mais ocupado, não uso total; DXGI precisa identificar adaptador único; VRAM dedicada exige denominador comprovado e mesma identidade; temperatura D3DKMT exige mesma chave física. Sem escolha por GPU 0, sem associação NVML por nome. CPU sem fonte continua indisponível. Limite D3DKMT ainda não está no contrato de saída; não inventado.

## Gates e integração posterior
- Nesta etapa: componentes novos, adaptador e testes no runner de layout existente. Sem tocar outras telas, motores, main, workflows, GF_BETA/G5 ou PRs #106/#113. A única edição de arquivo existente é a chamada de testes em TestesLayout/Program.cs, a reconciliar por inserção pontual na unificação.
- CI Windows e integridade pendentes até verificação do SHA exato. Necessário validar capturas dos três temas e DPI, ajustar proporções, legenda e contraste com a referência aprovada. Não chamar de fidelidade homologada antes das capturas.
- Próximas etapas: aprofundar testes de associação e envelhecimento por fonte, limites térmicos fornecidos pelo provedor, seleção explícita de GPU em host, broker de snapshots compartilhado sem timer de coleta duplicado, persistência local e integração em Configurações; em seguida adoção progressiva das barras em Início/Sistema/discos/progresso das operações, sem criar percentual sintético. Nenhuma coleta WMI pesada a cada segundo.
- O componente ainda não é instanciado pelo shell principal; esta PR é integração futura, não candidata distribuível. Não gerar release/ZIP, merge nem executar manutenção física.


## 24/11 — incremento de fidelidade, capturas e persistência isolada
- Compacto e expandido com mesma largura lógica (552 px), alturas iniciais 38 e 232 px; identificação CPU/GPU/RAM no compacto, barras alinhadas em linha na utilização, legenda de cores e separadores. Proporções ainda não homologadas por captura.
- Testes escrevem seis PNGs sintéticos MonitorGF-{AzulGf,Dark,Claro}-{Compacto,Expandido}.png em LayoutCaptures, publicados pelo workflow existente. Sem novo workflow nem dados de hardware.
- Preferências do monitor versionadas e locais (habilitação opt-in, modo, sempre no topo, opacidade 40–100%, encaixe, coordenadas). Caminho explicitamente fornecido pelo futuro host; arquivo separado, leitura limitada, escrita temporária + substituição, fallback seguro. Formulário expõe aplicar/capturar e ajusta posição à área de trabalho. Nenhuma ativação ou persistência automática no shell.
- Adapter verifica frescor de CPU, RAM, WDDM, DXGI e D3DKMT, sem renovar valor antigo; rejeita denominador DXGI para múltiplos índices físicos observados. UI rejeita replay e amostra velha, conserva watermark e invalida com relógio monotônico.
- A CI anterior 45404416680 foi cancelada na regressão pelo novo HEAD; integridade 45404416783 aprovada. Não atribuir aprovação global anterior aos novos commits. Capturas e CI do SHA final ainda pendentes. Limite térmico real depende do contrato do provedor; nenhuma alteração nas PRs #106/#113.


## 24/11 — gate de captura e correção visual
- HEAD f7165a3: CI Windows 45405273087 e integridade 45405273024 SUCCESS; regressão executou os testes Monitor GF. Checkout de PR: merge de teste 2f7db2d071384fa4f808ea62e0d2656a75f9586e, comprovado pelos logs. Nenhum merge efetivo.
- Artefato Residuos-layout (14925942444) recuperado e seis PNGs inspecionados. Compactos 552x38: ícones/textos/botões ausentes nos três temas. Expandidos 552x232: seis métricas e legenda presentes, barras retangulares e tipografia pouco hierarquizada. Gate visual REPROVADO.
- Correção: uma superfície ativa na árvore de controles; linhas explícitas no compacto; rótulos nomeados; títulos/valores em negrito; barras arredondadas por caminho GDI+; descarte dos painéis e fontes. Captura compacta agora exige cinco rótulos, cinco ícones, dois botões, valores esperados e pixels da cor de cada texto. O mecanismo de publicação existente permanece intacto.
- Correção ainda exige novos PNGs e CIs. Não avançar em integração ou sensores antes da confirmação visual. As cores continuam seguindo as âncoras aprovadas, não os números verdes ilustrativos da imagem.


## 24/11 — segundo gate e refinamento agrupado
- HEAD e0f3536: Windows 45407859381 e integridade 45407859287 SUCCESS, todos os jobs obrigatórios aprovados. Merge de teste 686b1ec3f6369cc6368856d7082ef1a9783b769f, checkout comprovado no log. Artefato 14924999798 contém seis PNGs: compacto 552x38 e expandido 552x232 em cada tema.
- Inspeção dos PNGs: compacto antes vazio corrigido nos três temas; seis barras e legenda visíveis, cápsulas arredondadas. Proporção próxima da referência. Contornos, pictogramas dos títulos, rótulos totalmente cromáticos, legenda de uso sem descritores e botões pequenos ainda divergiam.
- Refinamento: contornos arredondados, faixa externa de cor do tema, logo arredondado, identificação neutra e valor cromático no compacto, título com pictogramas, VRAM distinta, legenda com nomes e âncoras, botões maiores, barra térmica antes da coluna de valor. Mantidas as dimensões lógicas e a ausência de ações de manutenção.
- Dark vermelho #F2747E; teste de contraste >=4,5:1 sobre Card nos três temas, amostragem 0,01% e pontos de arredondamento dos canais. A interpolação direta RGB de azul para vermelho pode atravessar tons fora da escala e cair abaixo de 4,5:1; o controle passa a interpolar a coordenada da escala e só então calcular a cor. Nenhum atraso em frame crítico conhecido.
- Gate de captura inclui capacidade de acomodar leituras de três dígitos. Novo SHA exige CI e seis capturas; ainda não equivale a aprovação visual final. Sem mudança em sensores, shell, configurações globais ou workflow.


## 24/11 — correção dirigida da compilação
- HEAD 68e3a87: integridade 45409835552 SUCCESS; Windows 45409835626 FAILURE no job 138500321007, etapa de reprodução de contratos BETA LOCAL. O log comprova checkout de teste 018fd9ae117c0a1f7ccfc0fcfb9ecf027efb139e e erro WFO1000 em UiV269ResourceMonitor.cs:566, propriedade CaptionColor. Não houve execução dos testes de layout ou PNGs; ausência dos outros artefatos decorre da falha de compilação.
- Correção localizada: substituir propriedade de aparência exclusiva de runtime por campo privado e método SetCaptionColor, que invalida ao alterar. Sem desativar analisador, sem alteração de regras de build, workflows, motores ou paleta. Revalidação Windows e seis PNGs obrigatórias antes do aceite visual.


## 24/11 — recuperação das seis capturas e gate nativo
- HEAD cd33b19: Windows 45411500186 e integridade 45411500223 SUCCESS; todos os jobs obrigatórios, inclusive compilação x64. Etapas 38 e 46 concluídas. Checkout de teste 56cdca807bba0ae7a150c47d919f81b050307382 confirmado em log. Artefato Residuos-layout 14929851956 recuperado e seis PNGs inspecionados (552x38 e 552x232 em Azul GF, Dark e Claro).
- Conteúdo, proporções, seis barras, pictogramas, legenda e distinção de identificação/valor aprovados na inspeção. Cantos externos e logo ainda aparecem quadrados no DrawToBitmap. A implementação configura Region; o PNG retangular não comprova o recorte nativo. Não alterar PNG para simular fidelidade.
- Gate adicional na suíte existente: consultar a região Win32 apenas do HWND da janela e do logo GF próprios do teste; verificar quatro cantos excluídos e centro incluído, tanto na região gerenciada quanto nativa; liberar HRGN temporário. Sem captura da área de trabalho, coleta de hardware ou dependência adicional. Gate de pixels no expandido exige os seis valores e preenchimentos das seis barras. Preservados os seis PNGs brutos.
- Integração térmica e relay revisados localmente, mas mantidos fora do HEAD até conclusão do gate visual. Não alterar provedores de #106, #113, shell, main, workflows, G5 ou GF_BETA. Não gerar pacote distribuível.
