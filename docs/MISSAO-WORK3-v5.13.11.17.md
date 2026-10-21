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
