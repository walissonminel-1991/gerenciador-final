# Actions atuais e históricas

## Validação das versões atuais

PRs destinados à `main` executam três workflows atuais:

- `validar-candidata-windows.yml`: compilação e regressões atuais de inventário, atualizações, drivers, resíduos, layout/RAM, privacidade, segurança e encerramento. Também executa após push na `main`.
- `build-windows.yml`: publicação portátil, verificação de identidade/ativação do manifesto no Windows, SHA-256, artefato e procedência. **Builds de PR não recebem OIDC nem permissão para atestações**: um job separado com permissões elevadas só roda a partir da `main` confiável, após validar o manifesto do artefato gerado pelo próprio workflow.
- `triagem-diagnostico-github.yml`: regressões de validação estrita em PRs/push e triagem somente de Issues voluntárias recém-publicadas com prefixo conhecido, sem permissões de escrita ou interpretação de texto como instruções.

Ambos cancelam execuções anteriores do mesmo workflow/ref quando chega uma revisão mais recente. Falhas reais desses workflows devem ser investigadas antes de considerar uma candidata aprovada.

## Workflows históricos

Em 20/10/2026, 24 arquivos `compilar-gerenciador-v*.yml` ainda incluíam `main` no filtro de PR. Isso disparava verificações de versões anteriores para o código atual, incluindo exigências de `ProcessMonitor.cs`/`ProcessMonitorUi.cs`, substituídos na implementação atual. A execução 39444626604 comprova o caso: falhou em “Validar amostragem conservadora e tabela de processos” com “Monitor ou testes ausentes”.

Foi removido somente o destino `main` dos gatilhos de PR desses 24 workflows. As branches históricas já configuradas, gatilhos de push, execução manual e passos dos jobs foram preservados. A v5.13.3 tinha apenas `main` como destino de PR e passou a executar somente por seu push histórico ou manualmente.

Esses arquivos continuam como referência histórica e podem falhar quando executados manualmente sobre código recente. Para reproduzir uma versão antiga, use sua branch/revisão correspondente. Para validar uma candidata atual, use os dois workflows atuais. Os resultados antigos permanecem no histórico do GitHub; esta mudança não transforma falhas anteriores em sucessos.

Não foram removidas as suítes atuais nem inseridos `continue-on-error` ou comandos que convertam falhas de testes em sucesso. A verificação local comparou semanticamente todos os jobs históricos e os gatilhos preservados, validou os YAMLs e confirmou que somente os dois workflows de compilação e validação da época disparavam para PRs destinados à `main` (a triagem foi adicionada posteriormente).

A alteração não aumenta a versão do aplicativo: trata da configuração de CI e não modifica o executável. Sua aplicação na `main` depende da integração da PR que a contém; branches antigas continuam usando seus próprios arquivos.
