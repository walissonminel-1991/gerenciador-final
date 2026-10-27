# Integração final — matriz de continuidade

27/10/2026, America/Fortaleza. Branch `work-integracao-final-20261027`,
[PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).
Commit da análise: `f000ca3227227f11d8412c9a3f87335ead80bd9d`;
fechamento da Etapa 1: `275cfc396d22c8aff933a3879f0702d20596a2bc`.
Etapa 2: commit fonte `06d7dce0bb47944f1efd18a10785fa77f5f0c8f7`;
fechamento posterior somente Markdown/JSON, HEAD final a conferir na PR.
**Etapa 3 EM EXECUÇÃO — CI da própria árvore a registrar; nenhuma homologação nova.**
[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) ·
[Reconciliação](WORK-INTEGRACAO-ETAPA1-RECONCILIACAO.md) ·
[Integração de código](WORK-INTEGRACAO-ETAPA2-CODIGO.md) ·
[Recibo Etapa 1](evidencias/integracao-etapa1-reconciliacao.json) ·
[Recibo Etapa 2](evidencias/integracao-etapa2-codigo.json) ·
[Validação Etapa 3](WORK-INTEGRACAO-ETAPA3-VALIDACAO.md) ·
[Recibo Etapa 3](evidencias/integracao-etapa3-ci.json) ·
[Adendo 5–9](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## Fontes e sobreposição

| PR | SHA fixado | Delta contra #60 | Tratamento |
| --- | --- | --- | --- |
| #60 | `2bc9f4228fd448445fbf181652e5af93005def20` | Base | Ancestral comum e base da PR nova |
| #64 | `357877e9acd628f84e5d9944e02e471fe319d1bc` | 64 arquivos | Origem da composição; exclusivos preservados, compartilhados conciliados |
| #62 | `e530d724b7322963c8795b005f13ae6948a73e43` | 62 arquivos | Delta incorporado: 57 exclusivos idênticos, um workflow adaptado e quatro compartilhados conciliados |
| #61 | `f64600748b45b0ddc7e39c530ac56809c26f7ea8` | 21 arquivos | Já ancestral da #62; não importar separadamente |
| #63 | `04f59750739c6bdbaeb0bacd86ed2b734fba1271` | 5 arquivos | Conteúdo incorporado/revisado na #64; não reimportar |

Interseção #64/#62: `Program.cs`, `TestesLayout/Program.cs`,
`docs/PROGRESSO.json`, `docs/ROADMAP.md`. Exclusivos: 60 e 58; união: 122.
As fontes possuem aprovações individuais com os limites do relatório.

## Preservação e validação futura

A Etapa 1 mapeou as decisões; a Etapa 2 aplicou a composição. Preservação em
fonte e verificações estruturais não comprovam compilação, regressão ou
homologação. Nenhuma suíte de produto ou CI foi executada nesta sessão.

| ID | Área | Origem / arquivos | Decisão de reconciliação | Etapa 2 | Validação necessária após integração |
| --- | --- | --- | --- | --- | --- |
| M01 | Base e inventário Win32/MSIX/ecossistemas | #60/#64; inventário, reconciliador e leitores | Manter funções existentes e estados parciais | Preservado em fonte; CI pendente | Regressões de inventário/MSIX/identidade; cobertura física I7 permanece aberta |
| M02 | Arquitetura e consultas | #64; `InstalledUpdateIdentity.cs`, `Program.cs` | Manter evidência local e unknown quando inconclusivo | Preservado em fonte; CI pendente | Atualizadores, consultas da tela, arquitetura/canal/fabricante/escopo |
| M03 | Chrome Stable consultivo | #63 revisada na #64; fonte, catálogo e testes | Conservar a revisão #64; um dos cinco blobs é idêntico e quatro revisados | Preservado em fonte; CI pendente | Suítes multifontes/controles já existentes; não cria receita executável |
| M04 | Cache, falha parcial e cancelamento | #64; `UpdateResultHistory.cs`, tela | Conservar candidatos para revisão sem renovar autoridade | Preservado em fonte; CI pendente | Falha parcial, timeout, cancelamento e mudança de instalação |
| M05 | Drivers somente leitura | #64; inventário, lookup e histórico | Preservar conflitos/evidência parcial; gestão desativada | Preservado em fonte; CI pendente | Fixtures e runners Windows descartáveis; G1–G5 não encerrados |
| M06 | Desinstalação e resíduos | #64; executor, lote, resumo e políticas | Preservar Pending, consentimento, propriedade e proteção de dados | Preservado em fonte; CI pendente | Suítes de resíduos/desinstalação, concorrência, preservação e mutações |
| M07 | Exclusão permanente | #64; proteção/catálogos/leitores | Manter VLC somente no inventário, sem ações | Preservado em fonte; CI pendente | Conservar escopo existente; nenhum teste/consulta/verificação específica novo |
| M08 | Consentimento e privacidade | #62 inclui #61; política, diálogo, recorder | Conservar aviso/versionamento/revogação e bloqueio de receptor | Preservado em fonte; CI pendente | Testes sintéticos de consentimento, arquivos, locks e revogação; sem envio real |
| M09 | Fechamento e bandeja | #62 + #64; `Program.cs`, coordenador e `WindowChrome.cs` | União das guardas, modos e estados; Shutdown sem rede | Preservado em fonte; CI pendente | Encerramento, cancelamento, handles, falha e bandeja; teste de boot só com autorização específica |
| M10 | Transporte/retenção privados | #62; cliente e protótipos | Preservar código e configuração bloqueada, sem ativação operacional | Preservado em fonte; CI pendente | Transporte/receptor/retenção em cenários sintéticos; sem Worker/D1/GitHub privado reais |
| M11 | UI e DPI | #62/#64; despachante e cenários de layout | Manter `--work18`, `--etapa3`, `--beta-close`, aviso, testes e remoção dos perfis de PC | Preservado em fonte; CI pendente | Layout/foco/rolagem e escalas; DPI físico ainda pendente |
| M12 | Projeto, manifesto, licenças e pacote | #62; projeto .66, manifesto e verificadores | Manter .66, dependências, exclusões e contrato fechado | Preservado em fonte; CI pendente | Compilação Windows x64 interna e verificadores aplicáveis; sem pacote distribuído nesta etapa |
| M13 | CI e integridade externa | #62 reorganiza workflows; #64 conserva exclusão | Gates preservados; --official-live obrigatório 2022/2025 restaurado; upload BETA bloqueado | Preservado em fonte; CI pendente | CI do SHA integrado; fontes excluídas não reabertas; CI dos pais não transferida |
| M14 | Histórico e roadmap | JSONs #60/#62/#64 | Sete chaves pré-BETA + `candidate`/`beta`, preservação por ID e registros históricos | Conciliado; 42 estados iguais, 27/42 | 27/42 invariantes; identidade/CI nova próprias; não copiar aprovação histórica |
| M15 | Futuro perfil BETA LOCAL | Adendo do mantenedor | Coleta/transmissão remotas desativadas, consultas explícitas e independência da infraestrutura | Requisito registrado | Somente preparar após Etapas 5–7; cumprir contrato de distribuição; físico na Etapa 9 |

## Controle dos arquivos compartilhados

| Arquivo | Risco | Resultado da Etapa 2, a validar na Etapa 3 |
| --- | --- | --- |
| `Program.cs` | RI1, RI4 | Conservar propriedades/identidade/resumo #64 junto ao ciclo de vida/privacidade #62; sem substituição integral |
| `TestesLayout/Program.cs` | RI1, RI5 | Conservar despachantes e asserções dos dois lados |
| `docs/PROGRESSO.json` | RI2, RI6 | Interseção de chaves raiz alteradas vazia; sete da #64, duas da #62; 42 IDs/done iguais; sem herdar CI como aprovação nova |
| `docs/ROADMAP.md` | RI2, RI7 | Histórico datado das duas fontes, 27/42 e todas as pendências preservadas |

## Etapas e pontos de parada

| Etapa | Trabalho | Estado ao encerrar esta sessão | Próxima condição |
| --- | --- | --- | --- |
| 1 | Reconciliação | Concluída na sessão anterior; evidência preservada | Fontes/decisões respeitadas na Etapa 2 |
| 2 | Integração do código | Concluída em fonte; sem validação executável | Salvar/verificar SHA; parar antes da Etapa 3 |
| 3 | Regressões/CI da integração | Em execução nesta sessão | Concluir somente com CI aplicável aprovada no SHA próprio |
| 4 | Avaliação da candidata | Não iniciada | CI aplicável aprovada; relatar limites sem liberar BETA |
| 5 | Organização segura do GitHub | Aguardando 1–4 | Uma sessão; nenhuma exclusão/fechamento sem autorização específica |
| 6 | Auditoria geral | Não iniciada | Matriz de defeitos/evidências; não confundir riscos desta análise com defeitos reproduzidos |
| 7 | Correções e regressões | Não iniciada | Críticos/altos resolvidos para o cenário, CI por SHA |
| 8 | Preparação BETA LOCAL | Não iniciada | Pacote privado/verificável permitido pelo contrato; nenhum avanço automático à Etapa 9 |
| 9 | Homologação PC FORTE | Não executada; manual | Resultados do mantenedor, AVG ativo, sem presumir acesso ao PC |

## Conferência estrutural da composição

58 exclusivos incorporados: 57 blobs exatos #62 e uma adaptação delimitada
no workflow para conservar a consulta oficial obrigatória. Quatro arquivos
compartilhados conciliados; 70 cópias de fonte por SHA, 105 referências de
projetos resolvidas e 92 fontes C# de testes excluídas do produto. Os comandos
anteriores de CI foram preservados: 42 idênticos e um com flags adicionais
na consulta opcional. JSON/histórico e 27/42 preservados. Essas são conferências
estáticas, sem testes de produto, CI ou build. Detalhes e limites no relatório.

Árvore do commit fonte conferida no GitHub: 433 blobs, 367 herdados fora
do escopo intactos, 66 caminhos esperados e sete refs originais preservadas.
Nenhum run próprio. Fechamento posterior somente documental; regressões/CI
continuam reservadas à próxima sessão.

## Bloqueios preservados

Roadmap **27/42**, com 15 IDs abertos: U8, I7, R4, R6, R7, P5, P6, V5,
D2, D4, G1, G2, G3, G4 e G5. Evidência física anterior não identifica o
SHA/hash da futura composição. AVG sem homologação do pacote novo.
Configuração/aviso/aprovações e homologação conectada continuam pendentes;
nenhum estado remoto foi auditado. Falha externa antiga da #62 preservada,
sem reinvestigação ou reexecução do alvo excluído.

WinGet COM permanece opcional, não bloqueante e consultivo. Gestão de drivers
indisponível. Flags `PILOT_ENABLED=no` e `GITHUB_EXPORT_ENABLED=no` preservadas.
Sem merge, release, deploy, distribuição, novo executável, transmissão ou
operação nos PCs. A BETA local futura não exige completar 42/42 e não libera
automaticamente a BETA conectada/pública.
