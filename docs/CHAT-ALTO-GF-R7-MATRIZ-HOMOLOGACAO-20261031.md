# GERENCIADOR FINAL — R7 — checkpoint técnico e matriz de homologação

**Data:** 31/10/2026. **Base:** PR #65 / `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. **Estado:** R7 não homologado, roadmap **27/42**. Auditoria sobre fluxo de desinstalação e resíduos; nenhum teste pessoal, Release, merge ou alteração nas PRs #65/#66/#67 e no autoatualizador.

## Contraexemplo novo: revisão consumida sem reiniciar

`RebootResidualReview.IsLaterBoot(stored,current)` utilizava `Math.Abs(current-stored) > 2min`. Fixture determinística: boot estimado anterior 14:00 (hora 15:00, uptime 60min); cinco minutos depois, no **mesmo boot**, o relógio retrocede dez minutos (hora 14:55, uptime 65min), resultando estimativa 13:50. A implementação antiga responde **true** e pode processar e remover um registro de revisão antes de reinicialização verdadeira. Fix mínimo: exigir `current > stored` e diferença acima de dois minutos; teste negativo próprio no conjunto W10/W13. A revisão permanece **somente leitura**, sem alterar dados do usuário. **Limite:** um adiantamento grande do relógio ainda pode confundir a estimativa; o fix não comprova uma identidade de boot inequívoca nem autoriza recuperação.

## Contratos examinados

- `UninstallBatch.RunAsync`: consentimento anterior à execução; inventário completo, mesmo registro/comando, edição única e proteção de raiz compartilhada. `RegistrationAbsent` requer `exit=0` e observação completa com ausência nas últimas consultas. Sem handle, timeout, MSI 3010/1641, cancelamento de espera ou inventário incerto -> `Pending`, sem repetição/limpeza.
- `RegisteredUninstaller.LaunchAsync`: proteger o processo antes do start, distinguir erro posterior à criação do desinstalador com `UninstallObservationException` e conservar pendência.
- `IntegratedResidualBatch`: limpeza disponível **somente** para remoção confirmada, uma confirmação SIM/NÃO por seleção, omitindo alvos pendentes; zero elegíveis não abre diálogo vazio; diagnóstico por aplicativo.
- `IntegratedResidualCleanup`: raízes e Registro somente com titularidade, fotografia pré-desinstalação, handles/identidade/hash, limite, novos inventários, prevenção de links, perfis e compartilhamento. ACL/erro 5 -> preservação inconclusiva, não ausência; erro 32/33 pode justificar **somente revisão passiva** após reinício.
- `RebootResidualReview`: consentimento específico, estado DPAPI no perfil do usuário, sem apagar conteúdo após o boot; apenas verificar presença/ausência, com relatos `partial`, `indeterminate`, `provider_pending_review`, `application_present` ou `verified_absent` quando cabível.

## Matriz objetiva — fixtures Windows descartáveis

| Caso | Pré-condição simulada | Aprovação exigida | Teste existente |
|---|---|---|---|
| R7-01 | Inventário parcial, registro divergente, edição 7-Zip dupla ou diretório compartilhado | Não executar/desinstalar nem oferecer limpeza | W7, W12, W14 |
| R7-02 | Fornecedor conclui com exit=0 e ausência estável | `RegistrationAbsent`, pode somente iniciar a inspeção residual | W12 UninstallerStates |
| R7-03 | MSI 3010/1641, timeout, sem handle, cancelamento pós-start | `Pending`, proibir repetição e limpeza | W7, W12 |
| R7-04 | Resíduo comprovado + SIM | Excluir só alvo revalidado, confirmar ausência final | W10 Decisions |
| R7-05 | Resíduo comprovado + NÃO, seleção com item pendente | Nenhuma exclusão não autorizada; decisão única; item pendente omitido | W10 Decisions |
| R7-06 | Sem elegíveis, backups/perfis/documentos/arquivos desconhecidos | Não abrir pergunta vazia; preservar e explicar incompletude | W10 SharingAndPrivacy, W12 |
| R7-07 | ACL negada com erro 5 | Registro e ACL preservados, estado inconclusivo; não solicitar reboot por erro 5 | W12, W14 RegistryTests |
| R7-08 | Arquivo ocupado com 32/33 | Revisão pós-boot somente leitura, sem exclusões diferidas | W12, W10 |
| R7-09 | Mesma sessão com recuo de relógio > 2 minutos | `IsLaterBoot=false`; preservar revisão pendente | **Novo R7** em W10 RebootReviewAndTray |
| R7-10 | Boot estimado posterior, além da tolerância de 2 minutos | Revisão permitida, sem promoção de estados incertos | W10 RebootReviewAndTray |
| R7-11 | Link/hardlink, alteração de conteúdo após SIM, arquivo pessoal ou diretório substituído | Preservar e reportar falha sem apagar dados novos | W10 MutationsAndCancellation, W14 |
| R7-12 | Interrupção/diagnóstico indisponível | Não iniciar ações adicionais e não alegar sucesso | W10, W12 |

As fixtures são sintéticas; os controles ACL tocam apenas chaves dos próprios testes, restaurando-as. Não atribuir esses resultados à homologação física.

## Matriz FUTURA — PCs pessoais, SOMENTE após autorização específica

**Pré-condições obrigatórias:** pacote exato identificado por versão e SHA-256; backup pessoal; consentimento informado; programa dispensável que o mantenedor **já deseje remover** (não reinstalar um software só para testar); confirmar que não é antivírus, driver, componente Windows, browser em uso, biblioteca de jogos, perfil pessoal ou pasta compartilhada; AVG ativo. Se não houver candidato, **não executar**.

| Ambiente | Ação permitida futuramente | Evidência de aceitação e parada |
|---|---|---|
| PC FORTE, 2560×1440 | Uma desinstalação supervisionada com prévia de modo, uma confirmação, inventário antes/depois, inspeção automática, SIM/NÃO apenas para resíduos comprovados | Registrar versão/hash, estado inicial/final do registro, estados da UI e contagens, preservação de dados pessoais. Parar imediatamente diante de `Pending`, inventário parcial, erro 5, compartilhamento ou alerta de segurança; não repetir |
| PC FRACO, 1440×900 | Inicialmente **somente leitura de inventário, resumo, bandeja e interface**; desinstalação futura só por necessidade real e nova autorização | Mesmas informações de versão/hash, legibilidade, cancelamento e preservação. Nenhum aplicativo de teste instalado apenas para fabricar demanda |
| Reinício condicional | Apenas se fornecedor informar 3010/1641 ou erro 32/33 ocorrer e usuário autorizar | Verificar somente após reboot, com resultado explícito. Não reiniciar por erro 5, não executar exclusão pós-boot |

**Privacidade:** não registrar/publicar IP, conta, serial, tokens, caminhos privados, nomes de arquivos pessoais ou logs brutos. Usar relatórios minimizados por estado/contagem. **R4, R6 e R7 permanecem abertos**; nenhuma CI substitui o ciclo físico antes/depois.
