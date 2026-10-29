# Integração final — Etapa 6: auditoria geral

27/10/2026, America/Sao_Paulo. Branch `work-integracao-final-20261027`,
[PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).

**AUDITORIA CONCLUÍDA no escopo e limites abaixo. Dois ALTOS do cenário
BETA LOCAL continuam abertos; Etapa 7 não iniciada.** Nenhuma correção de
produto, executável BETA local, operação em PC ou ativação conectada.

[Matriz de defeitos](WORK-INTEGRACAO-DEFEITOS.md) ·
[Recibo por SHA](evidencias/integracao-etapa6-auditoria.json) ·
[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz de continuidade](WORK-INTEGRACAO-MATRIZ.md) ·
[Adendo](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## 1. Identidade e método

HEAD de entrada **`db67cde412db306c316e79c0761e8dafef4b3b34`**, árvore
`7dfb4d0f957691792a93ada5eabda321f210f92c`; PR aberta/Draft, base #60
`2bc9f4228fd448445fbf181652e5af93005def20`. Checkpoint, matriz, avaliação 4,
organização 5 e adendo relidos. D01/D02 continuam propostas sem autorização
para execução; não foram realizadas ou reinvestigadas como limpeza.

Os **439 blobs** foram materializados e conferidos por SHA Git antes dos
testes, incluindo os dois assets; reconferidos sem alteração depois dos
testes. A fonte funcional difere do SHA validado `6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8`
apenas em **14 Markdown/JSON** de continuidade/organização. Código, testes,
workflows, versão, aviso e políticas permanecem iguais aos da CI da composição.
Nenhum AGENTS.md existe nessa árvore.

Método: inspeção de fluxos/contratos e fronteiras de autorização, testes
existentes com fixtures adversariais, guardrail estático e análise de sintaxe/
formatos. O ambiente local é Linux, Python 3.12.14 e Node 24.22.1, **sem SDK
.NET, PowerShell ou runtime Windows disponíveis**. As execuções C#/WinForms/
Win32 e os analisadores de compilação são evidências já aprovadas na CI do
SHA exato da composição; não foram reexecutados localmente. Nenhum PC pessoal
foi usado como laboratório ou solicitado para preencher essa limitação.

Não foram criados testes novos ou executados instaladores reais. Os cenários
adversariais existentes cobrem travessia/corrupção de pacote, ambiguidade,
revogação, concorrência, timeout, falhas de disco/SQLite e recuperação com
transportes fictícios. A matriz distingue constatação estática, teste local
executado, prova Windows existente e lacuna física. As novas exigências de
perfil local precisam de testes comportamentais próprios na Etapa 7.

## 2. Execuções desta auditoria e CI reconferida

| Evidência | Resultado / alcance |
| --- | --- |
| Python `unittest discover`, test_p*.py | **47/47**, exit 0; pacote/metadata e guardrails, com 278 arquivos-fonte na varredura estática |
| Node, cinco arquivos BETA + dois receptores anteriores | **136/136**, exit 0, sem skip/falha/cancelamento; SQLite em memória e APIs/transportes fictícios |
| Analisadores de formatos | 9 Python AST, 19 Node --check, 19 XML/projetos/props/manifesto, 5 YAML, 4 TOML de exemplo e 34 JSON; nenhuma falha de análise |
| Principal/Windows | [40604593820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593820), completed/success, tentativa 1, fonte 6b5b0e52 |
| Integridade externa | [40604593709](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593709), completed/success, tentativa 1, mesma fonte |

Jobs reconferidos: sete obrigatórios success e dois condicionais skipped,
conforme desenho. A CI compila com analisadores e exercita a árvore integrada;
não foi transferida aprovação das PRs de origem. São os runs da Etapa 3;
nenhum run/build novo foi solicitado nesta auditoria documental. O merge de
teste registrado `3bb5e7b44ccc7c362e32678afa9b99791d460211` tem a mesma árvore
`e5646e6d419b47aa80e337359b1a49c9262ce6f7` da fonte validada.

As 136 verificações Node correspondem aos 126 testes BETA e dez anteriores,
agora executados em conjunto; não são ganho novo de cobertura nem pessoas/
programas homologados. Não somar subconjuntos/repetições/runners. Logs locais
contêm nomes de testes/contagens, e o recibo conserva comandos, resultados e
SHA-256 dos logs; nenhum diagnóstico privado de PC é publicado.

## 3. Cobertura dos 14 eixos

| Eixo | Inspeção/cenários/evidência | Resultado e limite |
| --- | --- | --- |
| A01 Inicialização/encerramento/bandeja | Program, BetaCloseCoordinator, BetaCloseTests; CI: 30 encerramento, 15 janela e 12 ciclos de bandeja | GF6-001/GF6-002; liberação de janela/Shutdown coberta em fixture. Sem boot/login físico |
| A02 Inventário Win32/MSIX/gerenciados | InstalledUpdateIdentity, InventoryReconciler, LocalMsixSnapshot, fronteiras de ecossistemas; CI: 47 MSIX e 54 ecossistemas | Parcial não confirma ausência/remoção; WinGet COM não reinvestigado; I7 físico aberto |
| A03 Atualizações/fontes | LoadUpdatesAsync, OfficialReleaseSources, AutomaticDiscoveryPipeline/UpdateResultHistory; CI: 410 atualizadores e 196 multifontes | Consultas/receitas separadas; GF6-001 e GF6-004. Nenhuma fonte ao vivo reconsultada |
| A04 Rede parcial/timeout/cache | TTL monotônico, limites de corpo, CancellationToken global 65 s da tela, cancelamento/estado stale; fixtures de lifetime e Node de stream lento | Não se demonstrou novo erro de timeout/cache. HttpClient.Timeout não foi tomado isoladamente como prazo de toda a consulta; fluxo externo tem token próprio |
| A05 Identidade/versão/editor/canal/arquitetura | InstalledUpdateIdentity, Matching, AutomaticDiscoveryPipeline, contratos e bloqueios de receita; CI de identidades/multifontes | Unknown/conflito continua consultivo. GF6-004 afeta metadado HTTP, não autorização de produto |
| A06 Drivers/IDs/INF/assinatura/histórico | DriverOfficialLookup/QueryCoordinator/ReadOnlyDialog e verificadores; CI 173 Windows 2022 / 174 Windows 2025 | WUA 2022 inconclusiva, 2025 sem ofertas; tarefas nativas pendentes impedem nova consulta da mesma classe. Nenhuma prova positiva G1/G2 ou operação de driver |
| A07 Atualizar/desinstalar/resíduos | UpdateLaunchGate, VerifiedUpdaters, UninstallBatch, RegisteredUninstaller, política/guardas de resíduos; CI 739 verificações e 36 mutações | Pending/repetição/pós-confirmação/proteções preservados; nenhum ciclo real executado |
| A08 Dados pessoais/compartilhados | ResidualPersonalDataBoundary, RegistryGuard, compartilhamento de raiz e fixtures Work14RegistryTests | Proteções/negações revisadas; Issue #48 tecnicamente mitigada em fonte, caso físico não encerrado |
| A09 Interface/foco/rolagem/DPI | Program, layout/driver dialog, BoundedLogText e modos de teste; CI 132 combinações + oito WORK18 | GF6-003; tema/foco/DPI/fluidez físicos não homologados; sem novas capturas físicas |
| A10 CPU/RAM/desempenho | ProcessSamplingPolicy 2/12/20 s, log limitado 65.536 caracteres, métricas locais e limites de consulta | Sem medição nova de CPU/RAM do GF; P5/P6 permanecem abertos. Tempos dos testes Linux não representam o aplicativo |
| A11 Exceções/cancelamentos/pendências | Fechamento, snapshots parciais, gate de lançamento, cancelamento e leases; fixtures Python/Node e C# da CI | Falhas continuam conservadoras; GF6-002 abrange saída/falha. Hipóteses sem reprodução não viraram defeitos |
| A12 Consentimento/privacidade/conectados | Gravador, política/endpoint/uploader, handlers de falha/saída; 136 Node locais e 127 privacidade/transporte na CI | GF6-002. Transporte real bloqueado; estados de fixtures não ativam Worker/D1 ou flags versionadas |
| A13 Pacote x64/integridade/dependências | 47 Python com fixtures de pacote, contratos de metadata e manifests; CI AMD64/self-contained/286 hashes | Pacote real não regenerado/baixado; somente manifestos foram conferidos fora do runner na Etapa 3. Perfil/rotulagem/entrega local ainda pendentes |
| A14 Segurança/privilégios/instaladores | 278 fontes no guardrail, pinning de arquivo, hash/assinatura, parsing sem hosts/shell de comandos, consentimento e confirmação | Sem nova quebra demonstrada das guardas existentes; isso não é homologação AVG/Authenticode ou ausência universal de defeitos |

Nenhum teste específico, consulta, botão ou verificação para o alvo
permanentemente excluído foi acrescentado/executado. Nenhuma investigação
WinGet COM foi reaberta; os guardrails gerais existentes continuam preservados.

## 4. Decisão e matriz de defeitos

[Matriz detalhada](WORK-INTEGRACAO-DEFEITOS.md): quatro IDs estáveis, com
severidade, localização por SHA/linha, pré-condições/reprodução, esperado,
observado, impacto, evidência/limite e recomendação.

| Classificação | Quantidade demonstrada |
| --- | --- |
| CRÍTICO | 0 |
| ALTO | 2, GF6-001/GF6-002, conformidade do cenário BETA LOCAL |
| MÉDIO | 0 |
| BAIXO | 2, GF6-003/GF6-004 |
| EXTERNO | 0; WUA/AVG não receberam causa externa presumida |

**A BETA LOCAL não está pronta.** Os ALTOS exigem tratamento antes do cenário
físico planejado e comprovação do perfil restrito antes de preparar seu pacote.
O aviso CS0649 e os User-Agents são não bloqueantes. Nenhuma falha nova das
suítes executadas foi reproduzida; as lacunas locais já indicadas na avaliação
4 agora têm rastreio e enquadramento no adendo. Não criar contagem artificial
de bugs, transferir a aprovação interna para o novo perfil ou contornar gates.

## 5. Issue #48 e incertezas preservadas

A Issue #48 foi relida no GitHub e continua aberta. O caso antigo .59 tratava
leitura HKLM32/64 com erro 5. Na fonte atual, `IntegratedResidualPolicy.Build`
limita o catálogo per-user ao HKCU/Registry64; `ResidualRegistryGuard` usa
direitos de leitura `0x00009` e handle predefinido para evitar pedido incidental
de escrita da raiz. Os testes WORK14 têm token restrito e falhas por etapa.

Isso sustenta a mitigação técnica incorporada e a distinção de escopo; não
comprova titularidade de mais resíduos, cobertura física, causa de todo erro
5 ou fechamento de R4/R7. Não pedir desinstalação nova, alteração de ACL,
elevação automática, limpeza genérica ou repetição da bateria física.

WUA, AVG, DPI e CPU/RAM físicos permanecem com seus limites anteriores.
Ausência de oferta/timeouts não autorizam drivers. O contrato BETA conserva
manifestos, não um executável pronto para entrega. Worker/D1/DNS/EU.org e
homologação conectada privada continuam fora da operação desta sessão.

## 6. Preservação, arquivos e próximo passo

Somente dez Markdown/JSON: checkpoint, matriz de continuidade, matriz de
defeitos, este relatório, recibo JSON, PROGRESSO, ROADMAP, README, índice de
organização e nota em TESTES. Nenhum código de produto, teste, workflow,
versão, aviso, aprovação ou configuração é alterado. Zero testes novos,
correções, builds/pacotes BETA, merges, releases, deploys ou distribuição.

Conferência documental aprovada: 94 Markdown/354 destinos relativos de arquivo,
14 eixos e quatro achados coerentes; 432 blobs herdados fora do escopo intactos.
Os mesmos 47 testes Python passaram também após as mudanças documentais,
sem somar a repetição à cobertura. A árvore será conferida no GitHub ao salvar.

PROGRESSO muda apenas `integration_final`; candidate/beta, históricos e os
42 IDs/done permanecem iguais, **27/42**. Relatórios/recibos das Etapas 1–5
preservados. Main, PR #40 e todas as outras branches intactas; PR #65 Draft.
VLC somente inventário, COM opcional/não bloqueante/consultivo, gestão de
drivers desativada, AVG/proteções e flags `PILOT_ENABLED=no` e
`GITHUB_EXPORT_ENABLED=no` mantidos. Sem coleta/transmissão real ou operação
no receptor e nos PCs. Fixtures de pacote não são builds executáveis do GF.

**Próxima sessão: somente Etapa 7 — correções e regressões**, começando pelos
dois ALTOS. Reproduzir com testes em ambiente isolado, corrigir a causa e
validar antes/depois/áreas afetadas; CI Windows aplicável pelo SHA exato.
Se faltar correção segura ou evidência, registrar o bloqueio. Não executar
D01/D02, preparar o pacote da Etapa 8 ou iniciar testes físicos da 9.
**Parar antes da Etapa 8. Etapa 7 não iniciada nesta sessão.**
