# Etapa 9 — Avaliação física parcial da BETA v5.13.11.66 no PC FORTE

28/10/2026 (America/Fortaleza). **Estado: PARCIALMENTE CONCLUÍDA, sem defeitos reproduzíveis relatados pelo mantenedor nos ensaios realizados.** Esta é uma homologação funcional **parcial** da configuração local de primeiro avaliador, não uma homologação irrestrita de segurança/AVG, mudança do Windows, pacote conectado ou publicação.

## Decisão e escopo

O mantenedor informou que fez o máximo de testes viáveis, não encontrou bugs ou erros perceptíveis e avaliou o programa como rápido e leve. Solicita registrar **conclusão parcial** e avançar, **sem repetir a mesma bateria** a menos que exista mudança no produto ou evidência nova de falha. Relato de ausência de falhas observadas não é prova de inexistência de defeitos; critérios sem evidência completa permanecem inconclusivos, sem impor repetição rotineira.

Pacote privado existente: v5.13.11.66, fonte `6df95fdd776644cd4b248b5cea61108974ca9ce9`, build privada `40716548530`, identificador `12828262039`. O pacote havia passado conferência independente Windows/Linux, mas o hash calculado **no PC do mantenedor** não foi anexado. Nenhuma evidência bruta de PC/identificação de usuário deve ser incluída no repositório público.

**Observado no PC FORTE, em capturas e arquivos fornecidos:** título da BETA correta, 196 aplicativos repetidamente inventariados, 27 MSIX (status ok), 162 dispositivos/associações de drivers, 14 possibilidades de atualização após consulta (8 em revisão na UI, 8/13 fontes bem-sucedidas), navegação pelas abas, diagnóstico consultivo de rede, visualização de espaço de três volumes, uso da bandeja com retorno e encerramento normal. O início interno informado no log foi ~491 ms (não representa o tempo total medido externamente). A impressão de leveza e rapidez é relato subjetivo consistente com os tempos pontuais; não substitui a amostragem RAM/CPU em repouso.

A falha de ativação do WinGet COM e cinco fontes de consulta indisponíveis/limitadas foram registradas como limitações consultivas tratadas sem interromper o aplicativo, **não** como falhas reproduzíveis novas. A consulta de drivers foi cancelada posteriormente com resultados anteriores preservados; INFs sem dispositivo observado não são pacotes órfãos. A ausência de vínculo explícito de histórico não é prova de inexistência de histórico.

## Matriz F9 — distinção entre execução relatada e comprovação

| ID | Resultado desta avaliação | Base e limite |
| --- | --- | --- |
| F9-00 | INCONCLUSIVO para hash local | O usuário assinalou a checagem, com versão visual correta; pacote/CI verificados no canal privado, sem saída de hash calculado no PC |
| F9-01 | INCONCLUSIVO para homologação AVG | Usuário não relatou alertas; não há registro de veredito/assinatura AVG nos bytes locais |
| F9-02 | APROVADO no escopo observado | Abertura, título, inventário e encerramento normal; reabertura independente não documentada |
| F9-03 | INCONCLUSIVO para amostras em repouso | Startup interno ~491 ms, medições pontuais da aplicação; 30/60/120 s com método uniforme não documentados |
| F9-04 | APROVADO no escopo visual observado | Capturas das abas sem defeito perceptível; não equivalem a matriz integral DPI/foco |
| F9-05 | APROVADO no escopo observado | 196 entradas estáveis, 27 MSIX sem erro; diferenças históricas entre versões não são defeitos automáticos |
| F9-06 | APROVADO no escopo consultivo observado | 14 candidatos, fonte parcial, cancelamento/resultado anterior preservado; nenhuma instalação de software validada |
| F9-07 | APROVADO no escopo somente leitura observado | 162 associações; investigação de INF exibida, mas assinatura/histórico de um INF específico não homologados |
| F9-08 | INCONCLUSIVO quanto a resíduos | Prévia de temporários/shaders foi mostrada, não equivale a auditoria de resíduos pós-desinstalação |
| F9-09 | APROVADO no escopo local observado | Relatórios e diagnóstico local produzidos; nenhuma prova de transmissão remota, sem homologação conectada |
| F9-10 | APROVADO para bandeja; T11 INCONCLUSIVO | Ocultar/restaurar comprovados; não há comprovação independente de reboot/autostart |
| F9-11 | APROVADO no encerramento observado | Log encerra normalmente; ausência de falha observada, não teste de estresse |

**Síntese:** avaliação do conjunto **PARCIALMENTE CONCLUÍDA**, com aceitação funcional limitada pelo mantenedor e sem novo bug reproduzível informado. Os critérios incompletos ficam registrados, **sem agenda de repetição integral** e sem promoção automática de checkpoints.

## Operações fora do roteiro estritamente consultivo

Os registros mostram execução real de limpeza de temporários do usuário (1.169 arquivos e 108 pastas processados, 40 preservados), limpeza de temporários Windows, cache Windows Update e limpeza de disco (saídas reportadas com código 0); as capturas mostram esvaziamento da Lixeira, início de DISM e serviço Windows Update/BITS. O mantenedor **não relatou efeitos adversos**. Código de saída 0 ou captura de progresso não comprovam todos os efeitos posteriores. Não repetir procedimentos mutáveis para gerar evidência, não presumir necessidade de reversão ou limpeza adicional. Caso surja incidente futuro, correlacionar com o registro existente, sem adivinhação causal.

## Política de continuidade

1. **Não repetir** a bateria F9/T01–T12, nem consultar novamente fontes/hardware apenas para aumentar números.
2. Reexecutar **somente testes afetados** por mudança de código, build/configuração, sistema relevante ou falha concreta reproduzível. Mudanças apenas documentais não geram homologação física nova.
3. Preservar o baseline imutável v5.13.11.66; alterações futuras devem ter commit, hash, CI e artefato próprios, com testes direcionados e reversão.
4. Não transformar a parcial em publicação conectada/pública; assinatura/AVG, backend, retenção, consentimento e outros avaliadores exigem decisões independentes. `PILOT_ENABLED=no` e `GITHUB_EXPORT_ENABLED=no` permanecem.
5. Roadmap oficial permanece **27/42**. Nenhum ID é promovido somente pelo conjunto de capturas; evidências contribuem aos critérios I7/P5/P6/V5/D4/U8, sem comprovação automática de todos seus requisitos.

## Próxima missão — autoatualizador seguro do próprio Gerenciador Final (a iniciar em sessão própria)

**Primeira etapa: desenho de contrato e ameaças, sem código implantado agora.**

- Uma única linha BETA e entregas futuras **somente no canal privado**, sem acesso público indiscriminado.
- Verificação de versão, manifesto autenticado/assinado, SHA-256 do pacote, vínculo à origem e verificação de integridade **antes** de oferecer instalação.
- Consentimento explícito do usuário antes de baixar/aplicar; sem atualizações silenciosas, sem tokens GitHub permanentes no EXE, sem contornar AVG/UAC ou permissões.
- Troca atômica e recuperação/rollback seguro mantendo dados, preferências e relatórios locais; nenhuma exclusão ou alteração em drivers.
- Casos de ameaça: downgrade, replay, URL adulterada, metadados expirados, pacote parcial, falha de rede, execução interrompida, versão conflitante, origem não autenticada e ausência de canal privado compatível.
- Ensaios inicialmente com fixtures, mocks e diretórios descartáveis; após mudança efetiva no produto, executar CI e apenas os testes físicos afetados.
- Meta de cadência de 5 dias é **condicional** a aprovação e qualidade, não obrigação de publicar. Revisão de retenção/limpeza dois dias após publicação, sem postergar expurgo obrigatório.

**Parar após o registro desta Etapa 9.** Nenhum build, release, merge, deploy, ativação conectada, workflow privado, limpeza ou teste físico adicional nesta sessão.
