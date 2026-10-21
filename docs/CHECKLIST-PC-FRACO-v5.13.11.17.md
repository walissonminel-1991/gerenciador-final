# Checklist para preencher — PC FRACO / v5.13.11.17 TESTE

Data/hora local: __________ · Windows (versão): __________ · Resolução: **1440×900** (confirmar: ______) · Escala do Windows: ____% · RAM total: ____ GB · Antivírus e versão: __________

Não preencher nome do computador, usuário, números de série, IP, senhas ou caminhos pessoais. Resultados abaixo são locais. As colunas ficam em branco até a medição real.

## Preparação

- [ ] Artefato final indicado na PR #40; versão e SHA-256 conferidos.
- [ ] .16 e .17 extraídas em pastas diferentes; uma versão aberta por vez.
- [ ] Manter EXE e arquivos adjacentes. Não instalar SDK no computador de testes.
- [ ] Antivírus/SmartScreen/UAC ativos; não executar pacote em quarentena nem criar exceção para forçar teste.
- [ ] Anotar estado comparável de outros programas e carga do Windows; manter uso normal registrado.
- [ ] Não usar botões de desinstalar, reparar, limpar temporários, instalar atualizações ou alterar drivers nesta bateria.
- [ ] Usar somente consultas, filtros e inspeções de resíduos; nenhuma alteração do Registro.

## Comparação de duração e recursos

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

## Navegação e clareza

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

## Preservação e ciclo da janela

- [ ] foobar2000: playlists/configurações/componentes preservados, se presentes.
- [ ] VLC: preferências/histórico/cache tratados como dados a conferir, sem remoção.
- [ ] Fabricantes/jogos e pastas compartilhadas preservados.
- [ ] Netease/Driver Booster/instalações portáteis exigem conferência.
- [ ] Minimizar para bandeja e restaurar durante consultas: sem duplicar janelas ou iniciar outra busca automaticamente.
- [ ] Fechar a revisão, reabrir a consulta e fechar/reabrir o GF: sem callback antigo ou dados apresentados como novos.
- [ ] Nenhum alerta de antivírus observado / alerta observado (descrever): __________

## Registrar divergências

| Cenário | Esperado | Observado | Repetiu? | Mensagem/contagem agregada |
|---|---|---|---|---|
| __________ | __________ | __________ | sim / não | __________ |
| __________ | __________ | __________ | sim / não | __________ |

Antes de compartilhar capturas, ocultar caminhos/nomes privados. Não alterar ACLs, criar junctions ou fabricar resíduos em pastas reais. Esta bateria não autoriza exclusão, desinstalação ou mudanças de drivers.
