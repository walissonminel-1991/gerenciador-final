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
