# Missão Work 3 — investigação progressiva e PC FRACO

Base confirmada: PR #40 aberta/Draft, head `82259f4301758eee638422fd425f8fb41371086b`, v5.13.11.16. Três workflows finais aprovados (39509664342, 39509664344, 39509664427); Windows 263/263. Main permanece `9ac3dc167b9e67ac50628749d5ad33c563c09376`, sem proteção; rulesets vazios. Não repetir requisições administrativas que já retornaram 403. Sem alteração administrativa nesta missão.

## M1 — investigação progressiva

Removidos os cortes de apresentação de 100 grupos e sete locais **no classificador**, em vez de paginar uma lista já truncada. O orçamento de descoberta/classificação de 30.000 entradas permanece; o que não foi examinado continua parcial e não é prometido como resultado disponível.

`ResidualResultIndex` mantém fotografia em memória, busca em produtos/fabricantes/locais/justificativas e filtros de classificação antes da paginação. Páginas de 10–100 grupos, locais de um produto em páginas separadas. Informa coleção disponível, grupos filtrados, carregados na página e disponíveis nas demais. Duplicatas reconciliadas possuem contagem independente das evidências não examinadas/fontes indisponíveis.

`ResidualInspectionSession` aceita somente locais da fotografia ou contêineres descobertos nela; não recebe destinos do texto de pesquisa. Enumera metadados de um nível sob demanda, sem conteúdo de arquivos/valores do Registro. Até 512 entradas por padrão (máximo 2.048), cinco segundos cooperativos, dois níveis a partir do alvo inicial e até 4.096 novos contêineres por sessão. Não segue links; no Windows prende ancestrais durante a leitura; uma chamada nativa já em curso não pode ser interrompida. Alvos iniciais permanecem disponíveis, inclusive depois do 4.096º. Falhas, cancelamento e limites não viram ausência. Nenhum método destrutivo.

Testes locais: navegação 500/1.000/5.000/10.000 grupos, busca/filtro global, 1.000 locais por produto, fotografia independente, parcial vazio, cancelamento, duplicatas e inspeção fictícia limitada com playlist/link. Expectativas antigas de truncamento foram substituídas por asserções mais fortes de preservação de **todos** os resultados, sem enfraquecer os limites da descoberta. UI nova será integrada no M4; Windows validará caminhos nativos no CI.

## Restrições

Cinco bloqueios de exclusão mantidos; sem upload, manutenção automática, merge, release estável, mudanças em drivers ou antivírus. Roadmap funcional não recebe checkpoints de hardware por testes sintéticos. Franquia Work não consultável; nenhum saldo estimado.

## M2 — precisão e correlação

M1 aprovado nos três workflows: Windows 39524185096, política 39524185089, pacote 39524185067. Implementada classificação tipada **Instalação a conferir**, sem capacidade de remoção. Caminhos ainda usados por produto renomeado/reinstalado/versão paralela são protegidos por índice de ancestrais/descendentes com fronteira de diretório. Aliases do catálogo de identidades protegem programas sob outro nome, sem alterar receitas executáveis.

Origens das evidências são flags tipadas (perfil, dados comuns, instalação, Registro, atalho, integração), preservadas na união. Fabricantes distintos com nome de produto homônimo mantêm estado desconhecido e preservação compartilhada. Resultado parcial conserva a base de preservação para filtros, além da incerteza; não transforma inventário incompleto em autorização.

Seis regressões novas verificam caminho ainda usado, fronteiras, identidade ambígua, origem/filtro em parcial, alias oficial e fontes combinadas. O teste de parcial encontrou perda da base de preservação durante o desenvolvimento; corrigida antes deste commit. Famílias VLC/foobar2000/WinMerge e os onze fabricantes continuam protegidos. Fontes novas só serão adicionadas se reduzirem erro concreto; consultas globais adicionais não são requisito para completar a paginação.

## M3 — desempenho e instrumentação

M2 aprovado nos três workflows (39524480844, 39524480823, 39524480812). Cache de uma consulta em memória: mudanças de página não repetem correlação, disco ou busca; critério diferente invalida o cache. Fotografia/cache imutáveis publicados atomicamente, cancelamento nunca publica filtro incompleto. Sem cache persistente de caminhos/perfis.

Instrumentação agrega tempo, alocações globais, heap, RAM e CPU do processo, somente no diagnóstico local. Medida de Startup termina antes do consentimento; descoberta inclui classificação e também gera sua medição específica. Fontes novas de informação pessoal não são consultadas. Sem amostragem contínua extra nem GC forçada no aplicativo.

[Benchmarks](BENCHMARKS-WORK3.md): base .16 e M3 executados sequencialmente com mesmo runtime/fixture; 500/1.000 aplicativos, 5.000/10.000 candidatos e 50.000 evidências. Maior cobertura custa alocações adicionais; não declarar melhoria percentual de velocidade nem extrapolar para o PC FRACO. Nos dois motores, 50.000 evidências excedem o teto de 30.000, e as 20.000 não avaliadas ficam explícitas.

## M4 — interface progressiva

M3 aprovado nos três workflows (39525016425, 39525016433, 39525016443). A revisão histórica/avançada agora usa uma única janela organizada com cartões virtualizados, busca e filtros globais de decisão/origem, páginas de grupos e de locais, explicações e inspeção no mesmo painel. Não concatena todos os milhares de achados em um TextBox. O resumo diferencia os grupos disponíveis daqueles apenas em outras páginas; fontes inacessíveis/locais não examinados continuam no resumo de cobertura. Seleção por página preservada dentro do mesmo critério.

UI usa um trabalhador por revisão, exclusão mútua, atraso de 220 ms na digitação e descarte de respostas antigas/canceladas. Nenhuma tarefa por candidato. Fechamento cancela callbacks, e captura de fonte impede acessar índice já descartado. Trocar produto durante inspeção cancela a resposta antiga. Botão Cancelar consulta de resíduos também disponível no registro da janela principal, incluindo as etapas anteriores à descoberta; uma chamada nativa em curso ainda precisa retornar.

Testes WinForms reais adicionados para 800×600, 1280×720, 1366×768, 1440×900, 1600×900, 1920×1080, 2560×1440 e 4K, com mil grupos, páginas de locais, seleção, pesquisa, filtros, controles contidos e desenho apenas de linhas visíveis. Testes de resposta tardia após cancelamento e parcial vazio. Compilação local Windows/layout sem erros; execução visual/nativa será validada no CI deste marco. Segurança visual mantém aviso permanente e nenhum botão destrutivo.

## M5 — segurança, decisão e regressões

CI de M4 aprovou motor/segurança/build, mas encontrou falha no teste visual de continuidade dos locais: ModernButton entrega o evento após animação; a asserção era imediata. Teste corrigido para aguardar a mudança real, mantendo exatamente a exigência de 30 locais e continuidade Local30. Resultado visual continua pendente da rodada seguinte; não declarar M4 plenamente aprovado antes dela.

Na revisão do caminho avançado, confirmada perda de diretórios inacessíveis e falta de orçamento compartilhado. `InspectWithCoverage` agora conserva evidência incerta em revisão, informa falhas/ausência do catálogo e usa 30.000 entradas/10 segundos compartilhados com cancelamento e profundidade limitada. Prende ancestrais por handle durante enumeração Windows; o executor continua removido. Revisão parcial vazia não vira confirmação de ausência.

Decisão em simulação exige explicitamente conferência de serviços/tarefas/integrações e recusa reinstalação. O GF **não** comprova ainda todas essas integrações; portanto o valor padrão falha fechado. Não foi adicionado varrimento global de tarefas/CLSID sem demonstração de precisão/custo. Serviços Run existentes são pistas de preservação; WinMerge mantém inspeção específica da integração. Reversão de arquivos pessoais não é garantida por ponto de restauração: nenhuma exclusão habilitada.

Novos testes: renomeação recusada enquanto handle está preso; revisão avançada cancelada antes da primeira fonte; raiz protegida; contagem de duplicatas na classificação; grupos protegidos/indícios isolados separados de dados não examinados; metadados de desempenho excluídos do payload colaborativo. Inspeção sob demanda desabilita navegação onde profundidade/capacidade já impedem nova leitura. Guardrail estático estendido aos componentes novos de índice, inspeção e UI, impedindo chamadas destrutivas/shell/rede. Sem enfraquecer asserções, realizar desinstalações reais ou tocar em drivers.

## Conferência visual e temporal

Testes de cliques aguardam a entrega efetiva do evento de ModernButton, inclusive Cancelar antes de liberar resposta simulada. Capturas do explorador em oito resoluções são geradas **somente com dados fictícios** e guardadas como artefato de CI; não são capturas do computador do usuário. Isso permite revisão visual além das asserções de contenção, filtro, paginação e virtualização. Nova linha de desempenho também testada fora do esquema colaborativo; nenhuma ampliação de coleta.


## Regressão de fechamento identificada pelo CI

A revisão 74fb2d6 validou navegação em 800×600, mas revelou ObjectDisposedException ao fechar: Form.Close já descarta a janela, e o using chama Dispose novamente. O override tentava cancelar a fonte de lifetime já descartada. Corrigido com liberação idempotente dos recursos; testes continuam fechando e descartando a janela em todas as oito resoluções. Asserções não foram reduzidas. A tentativa de executar a suíte de privacidade Windows no Linux falhou na proteção de caminho Win32 existente; nenhuma proteção foi afrouxada e a verificação nova permanece obrigatória no runner Windows.

Origens das regras históricas, WinMerge e revisão avançada também foram preenchidas, para que o filtro de Registro/perfis funcione nessas fontes, além da descoberta geral. Checklist real e roteiro de histórico extenso preparados; todos os campos de medições pessoais seguem em branco.

## Inspeção da captura sintética

Captura 800×600 do runner conferida visualmente. Ajustados campos de filtro para desenho escuro e rolagem da lista ao restaurar seleção: o cartão selecionado deve ficar visível, em vez de exibir detalhes de um cartão fora do viewport. Subpastas sem navegação autorizada pela profundidade agora explicam por que não foram examinadas. A captura extrema mostra área reduzida de detalhes com rolagem própria; a validação principal continua 1440×900. Nenhuma asserção essencial foi removida.

Teste nativo adicional de inspeção usa somente chave HKCU aleatória própria: enumera nomes de subchaves, não lê os valores durante a operação e comprova valor fictício preservado. A chave é removida exclusivamente no teardown. Nenhum Registro real do usuário é modificado.
