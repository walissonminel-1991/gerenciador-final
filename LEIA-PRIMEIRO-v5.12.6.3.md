# Gerenciador Final v5.12.6.3 — inicialização automática

## Alterações
- Ao abrir a janela, carrega a lista de programas do Registro do Windows.
- Em seguida, inicia a consulta HTTPS ao RuckZuck em segundo plano, sem janela extra; os candidatos aparecem no topo da aba Aplicativos.
- Inicia a pesquisa oficial do Windows Update Agent em segundo plano, **somente leitura**. Mostra o total encontrado no cabeçalho da aba Aplicativos e utiliza o resultado ao entrar em Inicialização e sistema > Windows Update.
- Inicia amostragem de CPU/RAM de processos a cada cinco segundos em memória, mesmo quando a aba está fechada. Ao abrir Processos, exibe o histórico já coletado.
- Exibe um botão **Atualizar** ao lado dos programas com uma possível atualização; ele ainda **não instala** porque o catálogo RuckZuck não fornece por si só um método de instalação verificado. Se clicado, a interface informa que a instalação não está habilitada. O controle não abre sites, terminais nem executa scripts.
- As desinstalações, limpezas, alterações de energia e instalações selecionadas pelo serviço Windows Update continuam sujeitas às confirmações e proteções existentes.

## Limitações
- O catálogo RuckZuck tem cobertura parcial e não reproduz os resultados do WinGet; candidatos não são atualizações confirmadas. Não use a lista como prova de que todos os programas estão atualizados.
- Instalação automática de programas de terceiros ainda está em desenvolvimento: exigirá mecanismo confiável, assinatura, verificação de integridade e conferência pós-instalação. Até lá o botão Atualizar não é um instalador funcional.
- A pesquisa do Windows Update pode demorar, falhar ou depender de configurações do sistema; não deve bloquear a interface. O resultado só é conhecido após a consulta terminar.
- A amostragem de processos começa na abertura; são necessárias ao menos duas medições (cerca de cinco segundos) para mostrar CPU histórico.
- O executável de teste é portátil e não assinado. O AVG deve permanecer ativo; se bloquear, interrompa o teste. O programa grava diagnóstico no perfil local para facilitar análises.

## Teste recomendado
1. Baixe o artefato Windows x64 na execução aprovada do GitHub Actions desta branch e extraia todos os arquivos para uma pasta nova.
2. Inicie o programa com o AVG ativo. **Não clique em Verificar atualizações**: observe se a consulta RuckZuck aparece sozinha no registro e se surgem candidatos.
3. Confira no topo da aba Aplicativos o status do Windows Update: inicialmente consultando, depois total encontrado ou indisponível.
4. Espere de dez a vinte segundos; acesse Inicialização e sistema > Processos. O histórico deve conter mais de uma coleta.
5. Acesse Windows Update e veja se o resultado já está disponível sem solicitar nova busca manual.
6. Confira a presença do botão Atualizar nos candidatos, mas **não espere que ele instale**. Não execute desinstalações, limpezas nem instalações neste primeiro ciclo.
7. Se ocorrer erro, envie captura da tela e o trecho do diagnóstico em `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.6.3.log`.

**Observação:** o GitHub Actions valida compilação, o motor de comparação e a consulta RuckZuck num ambiente Windows de integração. Não valida a execução do Windows Update Agent nem o comportamento do AVG no computador de destino.
