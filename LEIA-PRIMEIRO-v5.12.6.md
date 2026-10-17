# Gerenciador Final v5.12.6 — TESTE de integração RuckZuck

Este ZIP é **código-fonte**, não é executável. Base de funcionalidades: v5.12.5.3; motor de consulta: laboratório RuckZuck v0.1.1 validado no Windows e com AVG ativo (consulta somente leitura).

## O que foi alterado

- O botão **Com atualização** pede consentimento e consulta `geturl`/`getcatalog` por HTTPS.
- A lista de aplicativos instalados nunca é enviada ao RuckZuck; a comparação ocorre localmente.
- O filtro **Com atualização** mostra apenas **possíveis atualizações**; nomes, versões e fabricantes são comparados de forma conservadora.
- COM WinGet e pacote NuGet de interop foram retirados. A consulta não inicia `winget.exe`, `cmd.exe`, PowerShell nem instaladores.
- **Atualizar todos**, **Atualizar selecionados** e atualização individual permanecem **DESATIVADOS**. Não é seguro automatizar instalações com base em comparações ainda não auditadas.
- Desinstalação utiliza somente o desinstalador registrado pelo fabricante, com confirmação. As funções de limpeza, reparo, rede, energia e sistema continuam na base existente.
- No caso de falha remota, a lista de aplicativos locais continua visível e o status informa **não verificado**.

## Como gerar EXE pelo GitHub

1. Crie uma branch separada `teste-completo-ruckzuck-v5.12.6` no GitHub.
2. Envie o **conteúdo descompactado do ZIP** para a raiz dessa branch, mantendo `.github/workflows/compilar-gerenciador-v5.12.6.yml`. Remova da branch os módulos antigos `WinGetCatalogoCom.cs` e `FabricaPacotesCom.cs` se eles existirem.
3. Em Actions, abra `Gerenciador Final v5.12.6 RuckZuck - Windows x64`, aguarde a execução verde e baixe o artefato.
4. Extraia tudo para **uma pasta nova**; execute `GerenciadorFinal.exe` mantendo o AVG ativo. **Não desative o antivírus nem adicione exclusão.** Caso haja alerta, interrompa o teste.
5. Teste primeiro **Atualizar lista**. Em seguida clique **Com atualização**, leia a janela e autorize. Confira os possíveis candidatos e suas versões. Não tente instalar pelo programa nesta fase.
6. Se houver erro, compartilhe o arquivo de diagnóstico gerado em `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.6.0.log` (pode conter o nome de usuário no caminho se exposto em captura).

## Limitações

A quantidade de candidatos no RuckZuck pode coincidir com a do WinGet sem representar os mesmos pacotes. Aplicações Store podem não constar do Registro nem do catálogo RuckZuck. A versão é **experimental**; compilação Linux não substitui Windows e a ausência de alerta AVG não prova ausência de malware. A execução e assinatura em Windows continuam necessárias.
