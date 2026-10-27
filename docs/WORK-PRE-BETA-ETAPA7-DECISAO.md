# Pré-BETA — Etapa 7: integração e decisão técnica

27/10/2026 (America/Sao_Paulo). Branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Entrada conferida: `d01e37708bc06141e6dc7ffdee4ca346caddec4c`.
**Etapa 7 concluída como avaliação. Decisão: BETA BLOQUEADA para a candidata
conectada que reúna as duas frentes neste estado. Missão 0–7 encerrada.**

## Fundamento da decisão

O código pré-BETA .65 tem CI aprovada. A fonte BETA .66 tem outra CI principal
aprovada, mas permanece configurada para bloquear o envio: endpoint e hash
do aviso revisado estão vazios; aprovações e referências de homologação seguem
pendentes no arquivo versionado. Não há fonte combinada compilada/testada.
As evidências físicas .64/.65 não identificam SHA/hash nem homologam a build
conectada. O conjunto não fornece as provas necessárias para declarar aptidão
ao piloto conectado agora.

Esta é uma decisão sobre **fontes e evidências disponíveis**, não uma auditoria
do estado remoto. Worker/D1/DNS/flags e Secrets não foram consultados ou
alterados. Um campo pendente em Git não prova que um serviço remoto falhou.
Não houve substituição do piloto conectado por distribuição offline.

Os 15 checkpoints abertos não são tratados automaticamente como 15 bloqueios
de lançamento. Gestão de drivers pode permanecer fora do escopo funcional,
desativada, sem fechar G3–G5. Os impedimentos concretos da candidata conectada
estão na matriz de liberação abaixo; o roadmap permanece **27/42**.

Evidências locais: [recibo CI da Etapa 4](evidencias/pre-beta-etapa4-ci-40565220517.json),
[limites de drivers](WORK-PRE-BETA-ETAPA5-GESTAO-DRIVERS.md),
[consolidação física](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md) e
[recibo desta avaliação](evidencias/pre-beta-etapa7-decisao.json).

Fontes BETA consultadas no SHA e530d72:
[aprovações](https://github.com/walissonminel-1991/gerenciador-final/blob/e530d724b7322963c8795b005f13ae6948a73e43/docs/BETA-APROVACOES.json),
[política de build](https://github.com/walissonminel-1991/gerenciador-final/blob/e530d724b7322963c8795b005f13ae6948a73e43/BetaTelemetryPolicy.props),
[workflow](https://github.com/walissonminel-1991/gerenciador-final/blob/e530d724b7322963c8795b005f13ae6948a73e43/.github/workflows/build-windows.yml) e
[roteiro conectado](https://github.com/walissonminel-1991/gerenciador-final/blob/e530d724b7322963c8795b005f13ae6948a73e43/docs/BETA-66-HOMOLOGACAO-CONECTADA.md).
Esses arquivos não foram alterados.

## Referências imutáveis conferidas

| Referência | SHA conferido | Estado / CI consultada |
| --- | --- | --- |
| Main | `410d65e23546976db456337c71e4705f10217d36` | Preservada, sem execução solicitada |
| PR #40 | `ef13aef49083f357f6a14f4de46f8bc976dbd830` | Aberta/Draft, preservada |
| PR #59 | `28beb9607e85d208af4c0864d768a6db7fb5296a` | Aberta/Draft, preservada |
| PR #60 / base comum WORK18 | `2bc9f4228fd448445fbf181652e5af93005def20` | Aberta/Draft; 40253446761 success |
| PR #61 / BETA original | `f64600748b45b0ddc7e39c530ac56809c26f7ea8` | Aberta/Draft; 40324802334 success |
| PR #62 / BETA endurecida | `e530d724b7322963c8795b005f13ae6948a73e43` | Aberta/Draft; principal 40409850849 success |
| PR #63 / experimento | `04f59750739c6bdbaeb0bacd86ed2b734fba1271` | Aberta/Draft; 40507858102 success; revisão não repetida |
| PR #64 / entrada desta etapa | `d01e37708bc06141e6dc7ffdee4ca346caddec4c` | Aberta/Draft; recibo documental da Etapa 6 |
| Último código pré-BETA validado | `386e7cd5263266a0478e60139e59b036f1d69e31` | CI 40565220517 success, tentativa 1 |

Na #62, 40409850628 conserva a falha externa histórica. Somente seu estado
foi conferido, sem novo acesso ao alvo, investigação ou reexecução. A exclusão
permanente removeu esse alvo dos requisitos; essa falha não é reaberta como
impedimento externo da missão. Demais receitas continuam sujeitas aos gates
de integridade, identidade, hash e assinatura, sem relaxamento.

## Compatibilidade: ensaio somente de conferência

Contra a mesma base WORK18, a entrada pré-BETA altera **62 arquivos**:
21 de produto/catálogos, 17 de testes, 23 documentais e um script de medição.
A BETA endurecida altera outros 62. A interseção contém **quatro arquivos**,
não 62 conflitos. A comparação utiliza os SHAs da tabela, sem referências
móveis ou alteração de branch BETA.

Foi executado `git apply --cached --3way --check` sobre índice temporário
inicializado com a árvore da #62, usando o delta completo base→entrada #64.
O diretório de trabalho, índice real, HEAD e referências permaneceram intactos;
o índice temporário foi descartado. Nenhum código combinado foi executado.

| Arquivo compartilhado | Resultado textual | Reconciliação / limite de comportamento |
| --- | --- | --- |
| `Program.cs` | Aplicação de conferência limpa | Conservar identidade consultiva, estados pendentes e resumo da pré-BETA junto ao fechamento/consentimento da BETA. Ausência de conflito textual não prova compilação ou interação correta |
| `TestesLayout/Program.cs` | Limpa | Preservar os modos da Etapa 3 e os testes de fechamento/privacidade da BETA; compilar/testar juntos somente após existir fonte integrada rastreada |
| `docs/ROADMAP.md` | Limpa | Conservar 27/42, homologações parciais e separação entre candidata .66 e artefato .65 anterior |
| `docs/PROGRESSO.json` | **Com conflito** | Conservar candidata/bloco BETA da #62 e seis chaves físicas/seções da pré-BETA; não escolher arquivo inteiro de um lado |

O comando terminou com código **0**, mas escreveu explicitamente
`with conflicts` para PROGRESSO. Essa saída não foi interpretada como
aprovação integral. Há um conflito textual e **nenhum conflito de chaves raiz
alteradas em comum**: a BETA muda `candidate`/`beta`; a pré-BETA muda
`sections`, últimos testes, histórico e resumo físico. Isso torna possível
propor uma reconciliação de dados, sem certificar toda a integração.

Uma projeção **somente em memória** partiu do JSON BETA e incorporou as seis
chaves alteradas da pré-BETA. Foram preservados candidata/bloco BETA, últimos
testes/histórico físico e todos os 42 estados, com 27 concluídos. SHA-256 da
serialização UTF-8, chaves ordenadas e separadores compactos:
`c14b3c059545820e2cfa040fa1d0f2b2650b7daaf9831c7a4eac43c24bf002ad`.
Não foi gravada ou aplicada como PROGRESSO real. A reprodução usa os três
SHAs imutáveis acima, anteriores ao recibo documental desta etapa.

`InstallerFileGuard.cs`, projeto/manifesto, workflows e cliente de telemetria
mudam apenas na frente BETA. A adição do lock de coordenação não sobrepõe um
delta de arquivo da pré-BETA; ainda precisa da regressão da composição futura.
Não importar a BETA por cima da pré-BETA inteira nem usar CI de uma árvore
como prova da outra.

## Cobertura antes/depois e estado das funções

Os resultados abaixo são **evidências anteriores por SHA**, não testes
reexecutados nesta etapa. Não somar suítes, subconjuntos ou arquiteturas como
aplicativos reais adicionais.

| Área | Antes / depois comprovados nas etapas anteriores | Estado para esta decisão |
| --- | --- | --- |
| Consultas multifontes | 159→196 verificações; integração de tela 0→2 candidatos consultivos em quatro entradas, zero receitas/rede real na fixture | Implementada com limites; não equivale a dois novos aplicativos homologados ou receita executável |
| Atualizações | 368→410 verificações; 41 casos comparáveis 36 aprovados/5 falhas→41 aprovados, mais um controle de memória | Correções aprovadas na .65; execução real restante U8 aberta |
| Desinstalação/resíduos | 705 após adequação do escopo→739 verificações; 29 comparáveis 11 aprovados/18 falhas→29 aprovados e cinco nativos | Pendência segura, consentimento e preservação aprovados; R4/R6/R7 físicos abertos |
| Drivers consultivos | 26 casos comparáveis 6 aprovados/20 falhas→26 aprovados; 174 verificações por runner Windows na Etapa 2 | Consulta limitada; D2/D4/G1/G2 abertos; sem escrita |
| Gestão de drivers | Estudo da Etapa 5, sem executor | **Desativada**; G3/G4/G5 não homologados |
| Inventário/interface/desempenho | E1–E5 consolidadas, 169/74 incluindo 27/19 MSIX, encerramentos normais | Evidência física parcial, sem SHA/hash; I7/P5/P6/V5 e T11 remanescente conforme Etapa 6 |
| Cliente BETA/consentimento | CI própria aprovada, incluindo fechamento/privacidade sintéticos | Preparado em outra fonte, envio bloqueado e homologação conectada ausente no registro |
| Empacotamento | .65 aprovada na CI; .66 comum conserva manifestos/hashes, sem publicar EXE/ZIP BETA | Build interna bloqueada não é candidata conectada homologada ou distribuição aprovada |

O total histórico de resíduos era 707 antes da exclusão e 705 após a remoção
dos dois casos antigos; os 34 novos da Etapa 4 explicam 739. Isso é adequação
de escopo e cobertura de testes, não ganho físico de remoção.

## Matriz final de critérios de liberação

| Gate | Evidência atual | Estado / condição objetiva para avançar |
| --- | --- | --- |
| L1 — Fonte integrada rastreada | Conferência textual, conflito PROGRESSO e projeção de dados revisável; nenhuma composição aplicada | **Pendente:** reconciliar em fonte isolada, preservar ambas as frentes e registrar SHA exato |
| L2 — CI dessa fonte | .65 40565220517 e .66 40409850849 aprovadas separadamente | **Pendente:** CI aplicável à futura árvore integrada; aprovações dos pais não se transferem |
| L3 — Configuração/aviso revisados | Na #62: endpoint/hash/contato vazios; privacy_approved=false; política de build sem endpoint autorizado | **Bloqueado em fonte:** configuração e aviso aprovados/versionados antes de build conectada; nenhum valor preenchido nesta etapa |
| L4 — Homologação conectada e retenção | connected_homologation_approved=false; referências Cloudflare/exclusão GitHub vazias | **Não comprovada:** evidência em ambiente descartável especificamente autorizado, conforme roteiro BETA existente; remoto não inspecionado |
| L5 — Windows/AVG do pacote exato | Referência Windows/AVG vazia; E1/E2 antigas e incidente sem causa/resolução comprovadas | **Pendente:** artefato exato e provas aplicáveis; não provocar alerta nem contornar proteção |
| L6 — Integridade das demais receitas | Job de integridade pré-BETA aprovado no código 386e7cd | **Aprovada nesse escopo/SHA:** validar mudanças futuras aplicáveis sem reabrir o alvo permanentemente excluído |
| L7 — Escopo funcional e homologações | 27/42; drivers desativados, limites de apps/limpeza explícitos | **Parcial:** manter recursos não homologados limitados/desativados; não exigir 42/42 por regra inventada nem promover pendências por CI |
| L8 — Entrega ao primeiro grupo | distribution_approved=false no registro BETA; esta sessão proíbe merge/release/deploy | **Não concedida:** autorização de distribuição é posterior e distinta da avaliação técnica |

L1–L5 impedem recomendar a candidata conectada atual. L8 também permanece
fechado, sem pedido de aprovação nesta sessão. L6 não autoriza executar todo
instalador; L7 não homologa recursos que permanecem limitados. O piloto é
reavaliável quando houver nova evidência concreta, não por repetição da
mesma CI ou preenchimento administrativo artificial.

## CI aplicável, validação desta etapa e isolamento

CI pré-BETA [40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517),
fonte 386e7cd, tentativa 1: seis jobs obrigatórios success, dois condicionais
skipped. São 739 resíduos/desinstalação, 410 atualizadores, 196 multifontes,
36 mutações rejeitadas, 47 MSIX, 30 encerramento e 41 Python/políticas, além
de drivers/UI/build x64. Entre esse código e a entrada d01e377 mudam somente
12 documentos; não há motivo de regressão executável para repetir as suítes.

CI principal BETA [40409850849](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40409850849),
fonte e530d72, tentativa 1: seis jobs success, um condicional skipped.
Consultados jobs/logs anteriores e condições do workflow: compilação comum
`BETA-INTERNA-BLOQUEADA`, upload somente de BETA-BUILD/BUILD-INFO/hashes;
o upload da distribuição exige beta=false. Nenhuma CI BETA antiga foi
reexecutada. A composição hipotética não recebeu build, testes ou CI.

Isolamento verificado nas definições de ambos os projetos: os 16 arquivos C#
de testes alterados na frente pré-BETA e os cinco da frente BETA estão cobertos
pelos Compile Remove do produto. O projeto do produto não define GF_PROGRESSIVE;
a BETA acrescenta somente GF_BETA às constantes herdadas. É conferência da
configuração de fonte, não inspeção de um novo binário combinado. Nenhum
protótipo remoto ou script de implantação foi executado.

Nesta etapa: conferência de referências/CI, um ensaio textual de aplicação,
projeção de dados em memória e validação documental/JSON/metadados. Nenhuma
suíte de produto foi repetida. O recibo muda somente seis Markdown/JSON,
com `[skip ci]`; não cria nova build nem altera candidata/artefato, versão,
históricos físicos ou qualquer valor done. PROGRESSO/ROADMAP recebem apenas
o estado final da avaliação e seus vínculos de evidência.

Exclusão permanente do VLC preservada, sem teste, consulta, verificação ou
botão novo. Sem operação nos PCs, instalação/remoção/reversão de drivers,
elevação/ACL, reinício, investigação WinGet ou transmissão de diagnóstico.
Main, PR #40, branches BETA e infraestrutura privada preservadas. Nenhum
merge, rebase, release, deploy ou distribuição foi feito.

## Encerramento e continuação possível

**Missão 0–7 encerrada; BETA BLOQUEADA para o piloto conectado neste estado.**
O [roteiro focal da Etapa 6](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md) continua
válido para as provas físicas realmente ausentes, sem bateria completa.
Não existe Etapa 8 automática.

Próximo trabalho possível, somente com novo escopo: formar fonte integrada
isolada com envio desativado, aplicar a reconciliação documental proposta e
validar essa árvore na CI apropriada, preservando a exclusão permanente.
Homologação conectada/configuração/infraestrutura/entrega são blocos posteriores
e distintos; nenhuma dessas ações está iniciada ou autorizada por este recibo.
