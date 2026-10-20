# Gerenciador Final v5.12.6.2 — simplificação da lista

Esta versão de teste usa o motor HTTPS RuckZuck que funcionou na v5.12.6.1, mas simplifica os controles.

## Controles da aba Aplicativos
- **Atualizar lista**: atualiza somente os aplicativos registrados no Windows, sem consulta externa.
- **Verificar atualizações**: consulta RuckZuck por HTTPS quando solicitado. A lista de aplicativos permanece local; o serviço remoto recebe dados normais da conexão (por exemplo, IP). O resultado aparece automaticamente no filtro **Com atualização**. Durante a consulta, o botão muda para **Cancelar consulta**.
- **Todos / Com atualização / Marcados**: três formas de visualizar a mesma lista.
- **Selecionar vários / Desinstalar selecionados / Limpar seleção**: recursos de seleção e desinstalação anteriores, que exigem confirmação nas ações destrutivas.
- Foram removidos **Sites oficiais**, o filtro dedicado **Incertos** e o botão inoperante **Atualizar todos**. Correspondências incertas não são classificadas como atualizações.
- A consulta não abre caixa modal de confirmação, não executa WinGet, COM, scripts remotos ou instaladores.

## Limitações importantes
RuckZuck e WinGet possuem catálogos e critérios diferentes. A quantidade de candidatos (por exemplo, cinco versus seis) pode variar e não significa que a lista RuckZuck seja exaustiva. Atualização automática de terceiros ainda não foi implementada: um catálogo de versões não fornece garantia suficiente para executar instaladores.

## Teste no Windows
1. Baixe o artefato compilado na execução da branch `teste-simplicidade-v5.12.6.2` no GitHub Actions.
2. Extraia **todos os arquivos** em uma pasta nova e deixe o AVG ativo. Interrompa o teste se o antivírus bloquear.
3. Abra `GerenciadorFinal.exe`; confirme a listagem local.
4. Clique em **Atualizar lista**: não deve haver popup nem acesso à rede.
5. Clique em **Verificar atualizações**: a consulta deve ocorrer na mesma interface, sem popup, e mostrar apenas candidatos conhecidos pelo catálogo.
6. Confira os filtros **Todos**, **Com atualização** e **Marcados**, e teste o cancelamento de consulta se necessário.
7. Não execute desinstalações, limpezas, reparos, instalações nem mudanças de sistema neste primeiro teste.

Os testes do GitHub validam compilação e leitura do catálogo no ambiente CI; não substituem o teste com AVG no computador de uso. Os diagnósticos de teste continuam em `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos` sob o nome da versão.
