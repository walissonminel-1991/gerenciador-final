# Simulação controlada — Notepad++, WinMerge e resíduos históricos (20/10/2026)

## Origem e evidências observadas na v5.13.11.13

As capturas do teste e o log `GerenciadorFinal-Diagnostico-v5.13.11.13(1).log` demonstram os seguintes fatos, **sem inferir limpeza ou segurança absoluta**:

1. **Notepad++**: instalador oficial `notepadpp-x64-8.9.8.1` baixado e validado; pré-verificações aprovadas, execução com código 0 e nova versão confirmada. O inventário continuou em 175, compatível com substituição de versão.
2. **WinMerge x64**: desinstalador do fabricante iniciado; a entrada desapareceu do registro de desinstalação. Após a atualização do inventário, houve queda de 175 para 174 aplicativos. A revisão pós-desinstalação encontrou **um item de primeiro nível** sob a pasta específica de instalação do WinMerge, sem provar exclusividade ou elegibilidade para exclusão. A captura mostrou uma janela nativa de confirmação de término do WinMerge **simultânea** ao relatório do GF: não confundir ausência no Registro com conclusão de todas as janelas/processos do fornecedor.
3. A consulta ampla seguinte encontrou 12 grupos de vestígios para revisão, com 11 perfis de fabricante protegidos e uma instalação a conferir. As famílias **Aquiris, BANDAI NAMCO Entertainment, Colossal Order, Crystal Dynamics, Eidos Montreal, Guerrilla Games, nWay Inc, Opera Software, Red Dot Games, Sucker Punch Productions e Western Digital** não podem ser apagadas só porque suas chaves/pastas ainda existem. **Netease** e **Driver Booster** continuam a exigir confirmação de instalações e componentes dependentes, sem exclusão automática.
4. **foobar2000** e **VLC** aparecem instalados, mas ainda **não** há evidências de que tenham sido removidos ou que seus resíduos tenham sido comparados. A linha do foobar2000 é consultiva (guia de atualização), não autorização para instalar pacote genérico.

## Problema identificado e correções da v5.13.11.14 TESTE

A auditoria pós-desinstalação do GF já localizava a pasta de instalação e caminhos conservadores sob AppData, mas pesquisava somente `HKCU\Software\{nome_do_programa}`. Isso deixava de fora a chave **documentada do WinMerge**:

`HKEY_CURRENT_USER\Software\Thingamahoochie\WinMerge`

Referências oficiais e relatos: https://manual.winmerge.org/en/Command_line.html, https://github.com/WinMerge/winmerge/issues/876 e https://github.com/WinMerge/winmerge/discussions/2197.

- Auditoria R/O de **somente** `SOFTWARE\Thingamahoochie\WinMerge` em HKCU e, se acessíveis, nas visualizações HKLM 32/64. **Nenhum código de exclusão nem nova receita de limpeza**.
- **Inspeção histórica sob demanda**: procurar a chave exata e os diretórios específicos `Program Files\WinMerge` (x64/x86) mesmo que a remoção tenha ocorrido numa execução anterior do GF. Se uma variante de WinMerge constar instalada, a revisão histórica específica é suprimida. Ausência no ARP **não é confirmação de desinstalação completa**.
- A lista de aplicativos é atualizada antes de mostrar o relatório pós-desinstalação, evitando exibir o programa como instalado no fundo. A mensagem explica que o fabricante pode manter uma janela aberta e que registros/pastas remanescentes são **candidatos a revisão**, não lixo confirmado.
- Todos os achados são de somente leitura, inclusive configurações de usuário e integrações com Explorador. A varredura geral continua proibida de apagar perfis de jogos, drivers ou chaves compartilhadas.

## Próximos testes em PC real

- [ ] Fechar a janela final do desinstalador do WinMerge, se ainda aparecer.
- [ ] Reabrir a candidata mais recente em pasta separada, sem misturar builds e mantendo AVG ativo.
- [ ] Usar **Limpeza e desempenho → Resíduos de aplicativos** para conferir vestígios históricos **somente leitura**, inclusive a chave específica WinMerge caso ela ainda exista. Não pressionar botões de excluir/limpeza avançada para esses achados.
- [ ] Registrar se o item da pasta WinMerge desapareceu após fechar a janela do fornecedor. Se ainda existir, inspecionar **somente nomes/tipos** antes de inferir finalidade; não enviar logs que contenham caminhos de outros usuários.
- [ ] Em teste separado, comparar o Registro específico do WinMerge antes/depois da instalação, com exportação segura apenas se for realmente necessária para comparação.
- [ ] Repetir em foobar2000 e VLC com dados **fictícios**, sabendo que playlists, perfis e histórico são dados a preservar e não devem ser eliminados automaticamente.
- [ ] Testar atualizações individuais e outras desinstalações somente após backup e ponto de restauração viável; não usar limpeza profunda, remoção de drivers ou alterações no Registro como parte desta fase.

**A detecção é parcial por projeto:** a busca limitada evita seguir junções, diretórios do sistema e dados do usuário. Não afirmar que ausência de achado é ausência de resíduo.
