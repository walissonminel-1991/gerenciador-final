# Matriz de paridade e homologação — UI V2.6.9

Referência: `prototipos/ui-v269/GF_Prototipo_Interativo_V2_6_9_2026-11-23.html`.  
Status da matriz: **pendente para WinForms real**. A auditoria do HTML não substitui os testes de integração do EXE.

## Critérios transversais obrigatórios

| ID | Cenário | Resultado esperado | Evidência a coletar |
|---|---|---|---|
| UX-01 | Alternar Azul GF, Dark e Claro | Paletas independentes, sem herdar cores ou filtros do tema anterior | Captura pareada, comparação de cor |
| UX-02 | Texto no Dark | Cinza claro suave, legível, sem branco excessivamente luminoso | Revisão visual humana |
| UX-03 | Ícones nos 3 temas | Ícones nítidos, coloridos e distinguíveis | Capturas por aba e tema |
| UX-04 | Atualizar elegível | Ícone **verde**, botão habilitado | Fixture elegível + verificação real de habilitação |
| UX-05 | Atualizar inelegível | Ícone neutro, botão desabilitado | Fixture inelegível + nenhuma chamada ao motor |
| UX-06 | Remover elegível | Lixeira **vermelha viva**, botão habilitado | Fixture elegível + confirmação lateral |
| UX-07 | Remover inelegível | Lixeira **cinza/neutra**, botão desabilitado | Fixture protegida + nenhuma chamada ao motor |
| UX-08 | Tabelas | Cabeçalhos e células alinhados; não perder 'Versão atual' nem deslocar 'Ações' | Capturas e teste de geometria |
| UX-09 | Ícones de ação | Tamanho compacto consistente; sem mudanças de largura de linhas | Geometria em WinForms |
| UX-10 | Esc / Enter / setas / Tab | Foco e comandos sem ativar operação por engano | Testes de teclado |
| UX-11 | Painel direito | Detalhes, confirmação, progresso, cancelamento, erro, conclusão e histórico persistentes | Testes de estado e captura |
| UX-12 | Alternância de abas durante operação | Sem perda de estado/resultado ou execução duplicada | Teste de concorrência |
| UX-13 | Animações ativadas/desativadas | Todas as abas respeitam a preferência e 'movimento reduzido' | Inspeção de transições |
| UX-14 | Tooltips | Nenhuma dica flutuante ao passar o mouse | Inspeção de todas as ações |
| UX-15 | Escala de interface 90–110%, passo 1 | Alteração real de geometria e espaçamento | Teste do controle; homologação final de DPI adiada |
| UX-16 | Escala do texto 90–115%, passo 1 | Tipografia cresce independentemente da geometria | Teste do controle; homologação final de DPI adiada |
| UX-17 | Reabertura do GF | Preferências locais são reaplicadas conforme política de persistência | Teste WinForms no Windows |
| UX-18 | Operações reais | Sem instalar, remover, reparar ou limpar por mera consulta | Testes negativos |
| UX-19 | Segurança | Autorização, revalidação, assinaturas, consentimento e logs do backend preservados | Regressões do domínio e revisão de segurança |

## Cenários por aba

| Aba | Paridade visual | Paridade funcional — sem simulações | Proteção |
|---|---|---|---|
| **Início** | Cartões CPU/GPU/RAM/placa-mãe/armazenamento, imagens e sensores junto ao equipamento | Inventário e leituras nativas disponíveis, sem valores fabricados | Nenhum comando de manutenção na página |
| **Aplicativos** | Lista compacta, busca, ordenação, linhas selecionadas e ações laterais verde/neutro/vermelho | Inventário real + consulta de ofertas + desinstalação autorizada via motor existente | Apps do sistema/protegidos inelegíveis |
| **Drivers** | Mesma gramática de linha e ação de Aplicativos, com fabricante, versão atual/oferta e painel | Identidade PnP, inventário, WUA/ofertas comprovadas, consentimento, histórico e operação elegível | G2/Display e demais bloqueios existentes |
| **Limpeza e Desempenho** | Cartões e prévia, confirmação/resultado no painel | Consultas, prévias e limpeza controlada já implementadas | Nada automático; sem sucesso presumido |
| **Reparo e Rede** | Lista de diagnósticos/ações e evidência no painel | Diagnósticos nativos, execução supervisionada, verificação após reparo | Elevação somente quando necessária e consentida |
| **Sistema** | Windows Update, planos de energia e RAM numa aba | Consulta e operações existentes; resultado por atualização/ação | Revalidação, reinício não automático |
| **Inicialização** | Lista de itens, indicadores discretos, revisão lateral | Gestão de inicialização existente respeitando escopo do usuário e itens protegidos | Não modificar serviços protegidos |
| **Configurações** | Geral/Aparência; 3 temas, duas barras e animações | Persistir preferências sem alteração do Windows fora do escopo; consentimento real intacto | Sem telemetria opt-in presumida |

## Sequência recomendada para evidência de cada etapa

1. Registrar branch, parent e **HEAD remoto exato** antes de editar.
2. Confrontar diff com os caminhos reservados das outras PRs; restringir somente ao escopo autorizado.
3. Escrever/atualizar testes sintéticos que exercitam o estado de cada botão, o painel, a navegação e o teclado.
4. Executar CI Windows e integridade; inspecionar todos os jobs no SHA **posterior ao commit**.
5. Capturar a mesma tela nos três temas sem mudar o inventário; comparar colunas, cabeçalhos, ícones e legibilidade.
6. Registrar pendências com os cenários que falharam, sem alterar testes para forçar aprovação.
7. Fazer homologação Windows em máquina autorizada, sempre separando consulta de manutenção real.
8. Apenas após todas as abas estarem aprovadas: dimensionamento, PerMonitorV2, PC FORTE (2560×1440), PC FRACO (1440×900), escala 90–110% e texto 90–115%; permitir retorno de ajustes se necessário.

## Checklist de regressões históricas específicas

- [ ] Não repetir **V2.6.8**: nunca retirar a célula de 'Versão atual' em Drivers.
- [ ] Não repetir **V2.6.8**: nunca mudar de forma não revisada largura ou posição dos ícones de ação.
- [ ] Não repetir **V2.6.6**: verificar cores computadas/efetivamente renderizadas, não apenas alterações textuais de CSS.
- [ ] Não repetir ícones indevidamente apagados por opacidade global/estado desabilitado.
- [ ] Não usar vermelho em lixeira bloqueada ou verde antes de haver elegibilidade real.
- [ ] Não transformar a interface em painel azul no tema Dark: Dark é **preto/grafite**.
- [ ] Não transformar sensores não expostos em valores inventados.
- [ ] Não trazer janelas de confirmação/progresso flutuantes de volta, exceto controles nativos obrigatórios do Windows.

## Situação inicial

- [x] Protótipo V2.6.9 congelado como referência no GitHub.
- [x] Contrato de portabilidade para WinForms documentado.
- [x] Matriz de paridade e testes preparada.
- [ ] Código nativo de tema/shell implementado.
- [ ] Oito abas migradas ao novo shell.
- [ ] Motores funcionais vinculados e revalidados.
- [ ] CI de integração WinForms aprovada no SHA exato.
- [ ] Homologação física (incluindo DPI final) realizada.

**Nota:** testes da demonstração HTML não contam como aprovação de builds Windows nativos.
