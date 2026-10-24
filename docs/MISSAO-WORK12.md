# MISSÃO WORK12 — desinstalação e resíduos

Data: 24/10/2026. Branch `piloto-diagnostico-github-v5.13.11.2`; PR #40 permanece Draft. `main` preservada em `410d65e23546976db456337c71e4705f10217d36`.

## Base examinada

HEAD inicial `d678eb74b55ba986ab0019cbaf43901d11bfd50d`, candidata .56; CI anterior `40037717678` aprovada. Examinados os commits .55/.56, o PR, as missões WORK10/WORK11 e os dois diagnósticos originais disponibilizados pelo usuário. Logs brutos e dados do PC não foram copiados para o repositório público.

A .55 registrou operações `Pending`. Na .56, duas operações terminaram em `RegistrationAbsent`; o inventário caiu de 170 para 169 e depois 168. Ambas registraram `registry_unreadable`/erro 5 e, apesar disso, `capture=complete` seguido de `no_eligible_residue`. Esses eventos comprovam remoção dos registros, mas não comprovam ausência de resíduos em todos os locais.

## Causas comprovadas e correções

| Causa | Alteração |
|---|---|
| `Capture` devolvia `Complete=true` mesmo mantendo problemas de leitura; `Batch` descartava planos sem elegíveis e a UI terminava apenas com “Desinstalação concluída”. | Separação entre plano seguro executável e cobertura conclusiva; inspeções sem elegíveis também permanecem no resultado. Ausência no escopo, removíveis, preservados e inconclusivos são diferentes. |
| `Prepare` guardava somente chaves já existentes; uma chave de política criada pelo próprio fornecedor durante a remoção desaparecia da cobertura. | Reconsulta das chaves de políticas conhecidas ao final. Chave genérica continua exigindo comprovação anterior e posterior. |
| Pasta de instalação dedicada exigia `InstallLocation` preenchido. | Fallback estrito para a pasta do EXE registrado, com nome de produto corroborado e base de instalação delimitada. Pastas compartilhadas continuam bloqueadas. |
| `KEY_READ` exigia também `KEY_NOTIFY`/`READ_CONTROL`, desnecessários à inspeção. | Consulta usa `KEY_QUERY_VALUE | KEY_ENUMERATE_SUB_KEYS`. A regressão reproduz erro 5 com `Notify` negado e leitura de valores permitida. Isso não demonstra qual ACL específica causou os erros no PC FORTE. |
| Erros do Registro não discriminavam hive/visão; uma falha podia descartar também resultados acessíveis de outra parte do plano. | Etapas técnicas por hive/visão, verificação parcial explícita, preservação do alvo ilegível e limpeza somente dos outros alvos comprovados e consentidos. Nenhuma alteração de ACL. |
| O desenho dos cartões e o timer conheciam somente atualizações. | Estado independente de desinstalação, mostrado desde a preparação, animação indeterminada, trabalho do fornecedor, conferência, decisão e limpeza. Seleção, metadados e execução do fornecedor ficam em segundo plano; callbacks visuais são encaminhados à thread da interface. Sem porcentagens inventadas. |
| O resultado existia apenas na linha de status/log. | Resumo final escuro e rolável por aplicativo, inclusive ausência, preservações, erro 5, cancelamento, falha e conclusão pendente. Aguarda reabertura quando o GF está na bandeja. |
| Quiet validado na seleção não era novamente limitado ao mesmo produto em toda autorização. | Revalidação também na fronteira de execução. Argumentos registrados preservados exatamente; quiet não pode trocar identificadores de produto no mesmo EXE nem mascarar um desinstalador compartilhado por dois registros. |
| Cancelar a espera de um processo real era convertido em timeout. | Distingue cancelamento de prazo expirado; ambos deixam a conclusão do fornecedor pendente, sem matar nem relançar o processo. |

## Arquivos modificados

- `.github/workflows/validar-candidata-windows.yml`
- `CHANGELOG.md`
- `DarkControls.cs`
- `GerenciadorAplicativos.csproj`
- `IntegratedResidualBatch.cs`
- `IntegratedResidualCleanup.cs`
- `IntegratedResidualPolicy.cs`
- `Program.cs`
- `README.md`
- `RebootResidualReview.cs`
- `RegisteredUninstaller.cs`
- `ResidualRegistryGuard.cs`
- `SilentUninstallPolicy.cs`
- `TestesLayout/Program.cs`
- `TestesLayout/Work12UninstallLayoutTests.cs`
- `TestesResiduos/Program.cs`
- `TestesResiduos/SilentFixture/Program.cs`
- `TestesResiduos/SilentFixture/SilentFixture.csproj`
- `TestesResiduos/TestesResiduos.csproj`
- `TestesResiduos/Work12RegressionTests.cs`
- `UninstallBatch.cs`
- `UninstallCleanupDialog.cs`
- `UninstallCleanupUi.cs`
- `UninstallOperationProgress.cs`
- `UninstallProgressUi.cs`
- `app.manifest`
- `docs/MISSAO-WORK12.md`
- `docs/ROADMAP.md`
- `docs/TESTE-CONSOLIDADO.md`
- `scripts/test_work6_mutations.py`

## Fluxo final

1. Progresso aparece antes do preflight. A captura anterior preserva identidade do produto, pastas e locais de Registro autorizáveis.
2. Prévia informa se há modo silencioso ou necessidade de interação e mantém os avisos existentes de assinatura/UAC.
3. O fornecedor é iniciado uma única vez. A janela de observação do inventário permanece completa; retorno zero sozinho não comprova remoção.
4. Somente `RegistrationAbsent` autoriza investigar a limpeza; pendência não autoriza exclusão.
5. Resíduos comprovados geram uma confirmação SIM/NÃO por seleção. O resumo separa os aplicativos. Sem elegíveis, não há uma confirmação vazia.
6. SIM exclui permanentemente somente a seleção revalidada. NÃO preserva a seleção inteira. Conteúdo/identidades, inventário, processos, links, limites e fronteiras pessoais continuam conferidos.
7. O resumo final explica removidos, preservados e verificação parcial. Não afirma apagar todos os vestígios do computador.

## Cobertura sem receita

Pastas dedicadas em Program Files, Program Files (x86), LocalAppData e LocalAppData/Programs podem ser correlacionadas ao EXE registrado e ao nome exato do produto. A raiz nunca é derivada apenas de um nome semelhante ou de uma busca ampla no disco.

Uma chave genérica direta `Software/Produto` exige valor de caminho de instalação/executável corroborante antes e depois, instalação dedicada previamente identificada e inventário sem compartilhamento. Não adota a chave apenas pelo nome. Registros com caminhos divergentes, pastas substituídas ou instalações compartilhadas são preservados. Pastas criadas depois, sem identidade anterior, são informadas e preservadas.

Formatos pessoais/desconhecidos, backups, jogos, vínculos, documentos e arquivos de credenciais identificados continuam fora da exclusão. A cobertura não inclui todo formato de banco pessoal nem toda aplicação existente.

## Modo silencioso

- `QuietUninstallString` verificável continua preferencial e mantém seus argumentos.
- MSI utiliza ProductCode exato, `/qn /norestart` e o serviço legítimo do Windows.
- Notepad++ oficial: cobertura x64 e x86, inclusive cadastro sem `InstallLocation`, com as condições estritas do NSIS existente.
- SumatraPDF **3.6.1**: `-uninstall -silent`, com EXE/versão/pasta corroborados. O fonte oficial `3.6.1rel/src/Uninstaller.cpp` repassa `-silent` ao auxiliar e usa o fluxo sem janela; versões não conferidas não recebem essa receita por suposição.
- Inno Setup: nome, metadado `Setup/Uninstall` e cabeçalho oficial do log adjacente; somente então `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. Nome `unins000.exe` isolado ou log inválido não bastam.
- Unlocker sem quiet ou motor comprovado continua interativo e a prévia explica isso. Não foi acrescentado `/S` por tentativa.

Fontes primárias conferidas: [parâmetros Inno](https://jrsoftware.org/ishelp/topic_uninstcmdline.htm), [estrutura oficial do log Inno](https://github.com/jrsoftware/issrc/blob/main/Projects/Src/Shared.Struct.pas), [fonte Sumatra 3.6.1](https://github.com/sumatrapdfreader/sumatrapdf/blob/3.6.1rel/src/Uninstaller.cpp) e [documentação Sumatra](https://www.sumatrapdfreader.org/docs/Installer-cmd-line-arguments). Não houve incorporação desses fontes ao produto.

## Reinício e bandeja

Mantido opt-in por HKCU Run com caminho absoluto entre aspas e `--tray`, inclusive portátil. A pasta do portátil precisa permanecer no mesmo local. Sem elevação para ativar essa opção.

Reinício só é oferecido por retorno documentado 3010/1641 ou falha efetiva de compartilhamento 32/33. Erro 5 isolado não justifica reiniciar. Bloqueio detectado durante a captura também permite apenas uma revisão passiva posterior dos locais previamente identificados. Nada agenda exclusão automática.

Estado mínimo continua protegido por DPAPI da mesma conta, recuperado após outro boot e auditado. A consolidação de pendências conserva os alvos de revisões anteriores em vez de descartar os mais novos para o mesmo app. Ao reabrir o GF, o resultado também fica visível no resumo. UAC com outra conta permanece recusado pelo protocolo existente.

## Evidências e execução

- `37b6249`: testes anteriores à correção, [CI 40091271025](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40091271025). Três falhas explícitas reproduzidas; o teste de cobertura seguinte foi interrompido por uma falha de teardown da fixture, corrigida em seguida. Esse último cenário não é contabilizado como uma quarta falha de produção comprovada nessa rodada.
- `db298f0`: [CI 40092030719](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40092030719) integralmente aprovada, incluindo regressões anteriores e os quatro controles básicos WORK12. Candidata ainda .56 nesse checkpoint.
- `e7a33f8`: amplia as provas Windows, processos inertes, motores, interface e quatro mutações novas; prepara versão .57. A CI `40092722102` confirmou os casos de ciclo e segurança, mas falhou no positivo Inno: a fixture tinha `AssemblyTitle` diferente de `Setup/Uninstall`. O recurso foi corrigido na própria fixture, sem afrouxar o detector. A seção WORK12 da [PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40) registra o resultado da repetição integral, SHA da fonte, execução final e pacote autorizado após aprovação.
- Compilações locais cruzadas de produto, resíduos e layout com SDK 10.0.402: zero erros/avisos. Não são execução WinForms no Linux.
- Política local: 29 testes Python e 10 Node aprovados.

Os cenários novos utilizam GUIDs, chaves isoladas, arquivos sintéticos e um PE inerte. Incluem SIM/NÃO efetivos, ausência, preservação pessoal, ACL negada, arquivo ocupado, pasta nova sem prova anterior, compartilhamento, comando quiet divergente, log Inno adulterado, timeout/cancelamento reais de processo e operações pendentes. A própria fixture não desinstala software.

As mutações trabalham em cópias descartáveis. Não substituem falhas por sucesso nem aceitam erro de compilação como mutação detectada. As capturas e a geometria de UI cobrem estados de desinstalação, variantes compactas, busca, reconstrução, resumo e restauração da bandeja.

## Limitações

- Não existe remoção silenciosa universal; o fornecedor ainda pode exigir UAC ou interação.
- `RegistrationAbsent` é a conferência conservadora implementada, não prova de apagamento de todos os recursos do app. Processos auxiliares podem continuar; resultado indeterminado impede repetição durante a sessão.
- A limpeza cobre alvos exclusivos identificados nesta desinstalação. Não procura retroativamente todos os resíduos de remoções antigas.
- Nenhuma permissão é alterada para vencer erro 5. Preservações e falhas aparecem no resultado.
- Boot físico, UAC interativo, AVG/SmartScreen, assinatura e DPI real dos dois PCs não foram homologados pela CI. O pacote permanece TESTE e sem assinatura.
- U8/R7/R4 físicos e roadmap 27/42 permanecem abertos; não foram encerrados por simulação.

## Validação física mínima

PC FORTE: quando houver um aplicativo dispensável já instalado, fazer **uma** remoção pelo GF; observar a barra desde o clique e o texto de trabalho/conferência. Se houver elegíveis, usar SIM e ler removidos/preservados no resumo. Se não houver, conferir ausência no escopo ou limitação explícita. Não reinstalar Notepad++/Sumatra nem repetir remoções antigas apenas para reproduzir os defeitos já cobertos automaticamente. Não há expectativa de pergunta de reinício sem causa real.

PC FRACO, 1440×900: conferir o espaço do cartão, texto, botão Cancelar e resumo; ativar/desativar início com Windows, abrir na bandeja e restaurar. Essa rodada pode permanecer somente leitura; não exige nova desinstalação. Inicialização real após login pode ser validada no próximo uso normal do PC.
