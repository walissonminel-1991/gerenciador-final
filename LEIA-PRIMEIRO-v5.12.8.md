# Gerenciador Final v5.12.8 TESTE — motor de consulta otimizado

Esta é uma versão funcional de teste baseada na v5.12.7.3, preservando a interface e o instalador supervisionado.

## O que mudou
- **Cache RuckZuck de 10 minutos somente em memória** após uma consulta bem-sucedida. O catálogo não é salvo em disco; fechar o aplicativo descarta o cache.
- **Verificar atualizações** solicita dados novos por HTTPS, mesmo dentro dos 10 minutos; é uma atualização manual real, sem usar o cache.
- Depois de instalar um programa, o Registro do Windows é relido; a comparação é repetida usando catálogo recente em memória, sem baixar novamente os mesmos 631 pacotes, e o filtro do usuário é restaurado.
- **Conexões HTTPS reutilizadas** durante a sessão para evitar estabelecer uma nova conexão TLS a cada consulta.
- **JSON analisado uma só vez** no formato normal do provedor; continua aceitando formato de texto JSON encapsulado.
- **Comparação de versões numéricas normalizada**: 1.2 = 1.2.0 = 1.2.0.0, evitando falsos positivos.
- Novas métricas de diagnóstico: fonte (memória ou HTTPS), duração da consulta, da comparação e total da operação. Nenhum nome de aplicativo é enviado ao serviço.
- Mantidas as proteções: editor e assinatura Authenticode dos instaladores; SHA-256 fixado no OBS; scripts remotos não são executados.

## Limitações
- A fonte RuckZuck oferece cobertura parcial; 631 entradas não representam todos os aplicativos Windows. O botão Verificar atualizações não confirma a ausência de atualizações em outros catálogos.
- A primeira consulta depende da velocidade do serviço e da rede. Os resultados de desempenho do CI são sintéticos e não representam a latência real no computador de destino.
- As receitas oficiais para instalar programas permanecem limitadas às já validadas (Visual C++ 2013 x86/x64 e OBS 32.2.2). Adobe Acrobat e Plex usam sites oficiais como alternativa. Ações potencialmente destrutivas continuam exigindo confirmação.
- Não há download, execução nem instalação automática em segundo plano; inicializar o Gerenciador apenas consulta versões e Windows Update.

## Testes automatizados
- Motor de atualização e receitas: 12 casos.
- Comparador existente e regressão de Visual C++: 18 casos.
- Novo conjunto: 14 casos de cache, concorrência, cancelamento, segurança de domínios e JSON, versões equivalentes e desempenho.
- Downloads Windows CI: conferência de assinatura Microsoft x86/x64, SHA-256 + assinatura OBS. Os instaladores são **baixados mas não executados** nos testes CI.
- Consulta HTTPS real RuckZuck no CI somente leitura.
- Publicação portátil x64 no GitHub Actions.

## Como testar
1. Extraia o artefato em uma pasta nova; deixe o AVG ativo.
2. Abra o Gerenciador; observe a consulta automática e anote o tempo até aparecerem os candidatos.
3. Clique em **Verificar atualizações** e confira que o registro informa `fonte=https` (nova consulta).
4. Em **Atualizar lista**, confirme a leitura de programas instalados sem execução de instaladores; se uma atualização foi concluída, a lista de candidatos deve se recompor.
5. Se for testar instalação, faça **somente uma por vez**, começando por um programa cuja receita já esteja validada, e evite repetir o Visual C++ caso já esteja atualizado.
6. Confira o arquivo `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.8.log`. Agora ele inclui `duracao_ms`, `comparacao_ms`, `total_ms` e `fonte`.
7. Se ocorrer um alerta de segurança, interrompa o teste e envie a captura. Não desative o AVG.

Branch: `teste-motor-v5.12.8`.
