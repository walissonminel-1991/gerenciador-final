# Pré-BETA — Etapa 3: atualizações, identidade e estados seguros

27/10/2026, branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Base `b74eb879b74360ed78e57b105463fb81fa5ffe83`; CI da Etapa 2 40525913386 conferida.
**Concluída tecnicamente; homologação U8 pendente. Etapa 4 não iniciada.**

## Resultado funcional

InstalledUpdateIdentity.CaptureArchitecture lê apenas o chrome.exe Stable
explicitamente registrado em DisplayIcon nas raízes padrão do mesmo escopo:
ProgramFiles/ProgramFilesX86 (máquina) ou LocalAppData (usuário).
InstallLocation, quando informado, deve coincidir. Não enumera diretórios,
executa binários ou usa setup.exe como substituto.

InstallerFileGuard mantém arquivo e ancestrais protegidos contra reparse/troca
durante a leitura. O PE deve ser executável nativo, não DLL, com Machine/Magic
coerentes; produto, empresa e versão devem corresponder ao Registro. Isso é
**evidência estrutural consultiva, não autenticação do editor**. Ausência,
acesso negado ou divergência conserva unknown e a entrada do inventário.
Bitness não vem do SO/RegistryView. ARM64 permanece ARM64, sem fonte x64
substituta. Não se promete arquitetura PE para todos os aplicativos.

Create é o adaptador efetivamente usado por LoadUpdatesAsync. QueryArchitecture
não cria receita, comando ou permissão. A execução mantém os requisitos de
HTTPS, SHA-256, Authenticode/editor, motor, contexto local/MSI e confirmação.

UpdateResultHistory conserva a primeira consulta parcial; cancelamento
posterior mantém candidato em revisão/stale e bloqueia ação direta. Mudança
de instalação invalida o cache; consulta completa substitui a evidência.
Falhas não renovam 30 minutos; máximo de 16 observações por aplicativo.
O resumo da tela conta também falhas da consulta automática.

## Controles revistos e preservados

| Área | Código existente reaproveitado | Limite |
| --- | --- | --- |
| Identidade/contexto | ApplicationIdentityCatalog, InstalledApp, MultiSourceMatcher | Nome parecido, SO e pasta não provam arquitetura; fonte consultiva não autoriza instalar |
| Catálogo/manifesto | PackageCatalogIndex, AutomaticDiscoveryPipeline, AutomaticPackageManifest | Índice autenticado, manifesto/hash, contexto local e contrato continuam obrigatórios |
| Payload | VerifiedUpdaters, InstallerFileGuard, TrustedFileSignature, MsiPackageEvidence | Hash, assinatura/editor, formato/motor, família/versão/arquitetura e MSI preservados |
| Dados/configurações | Contrato da instalação, preflight e confirmação escolhida | Sem limpeza, desinstalação ou fechamento forçado automático novos |
| Estados/recuperação | UpdateLaunchGate, UpdateOperationProgress, PendingDeployment, UpdateInstallationContract | Retorno zero não basta; uma tentativa por autorização; cancelar espera não encerra nem repete instalador |

## Testes e recuperação do ambiente

Projeção original de Program.cs extraída sem alterar a lógica: **36/41
aprovadas e cinco falhas** antes; **41/41** depois. Três falhas na passagem de
arquitetura e duas na retenção após primeira consulta parcial. Mais um controle
de memória com 100 rodadas: **42/42 focais atuais**. Oito fluxos inertes cobrem
bloqueio, pré-verificação recusada, UAC, falha, confirmação, reinício,
cancelamento após início e timeout. Não são instalações reais.

SDK 10.0.402: 196/196 multifontes e builds Release do produto e TestesLayout
com analisadores, sem avisos/erros, antes da interrupção. O ambiente local
reiniciou; o código foi recuperado sobre o mesmo SHA-base usando o registro
desta conversa. A CI 40559631389 aprovou a cópia recuperada no SHA 5a7c048. Nenhum resultado da CI é
atribuído a um SHA diferente. Linux não executou WinForms.

A fixture Windows encadeia adaptador de produção, HTTP fictício e aplicação
dos candidatos na tela real; nenhum endpoint externo ou instalador.

```sh
dotnet run --project TestesAtualizador/TestesAtualizador.csproj -c Release -p:EnableWindowsTargeting=true -- --etapa3
dotnet run --project TestesMultifontes/TestesMultifontes.csproj -c Release
dotnet build TestesLayout/TestesLayout.csproj -c Release -p:EnableWindowsTargeting=true -p:RunAnalyzers=true
# Somente Windows descartável:
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --etapa3
```

[Evidência](evidencias/pre-beta-etapa3-atualizacoes.json).
Referências primárias: [PE](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format)
e [FileVersionInfo](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo?view=net-10.0).
Recursos de versão não substituem Authenticode, exigido no fluxo de payload.

## Roteiro de homologação futura — não executado

1. Escolher aplicativo não essencial com receita elegível. Registrar SHA/CI,
   instalação, versão, escopo, arquitetura e fabricante; aproveitar E1/E2.
   Se inelegível, registrar o motivo, sem reparar WinGet ou trocar permissões.
2. Conferir backup/configurações e consentimento específico. Salvar trabalho
   antes do preflight; nenhum fechamento forçado implícito.
3. Usar o fluxo normal, conferindo HTTPS/hash/assinatura/editor e identidade
   novamente. Uma ação explícita, uma tentativa autorizada.
4. Observar progresso, UAC e término. Cancelar/falhar não pode repetir a ação;
   manter pendência se o instalador pode continuar.
5. Confirmar a mesma instalação e versão no Registro/binário, arquitetura,
   escopo, fabricante e dados/configurações preservados. Retorno zero não
   basta. Reinício requer autorização própria futura e posterior conferência.
6. Publicar apenas códigos/contagens; provas locais detalhadas ficam privadas.
   Fechar U8 somente com critérios reais atendidos; sem pedir bateria completa
   repetida por falta de consolidação.

## Limites

E1/E2 mantêm 10/8 candidatos e zero atualizações elegíveis nas sessões .64/.65;
sem nova operação/medição nos PCs. Relato antigo de atualização 7-Zip .52
preservado sem promovê-lo a prova desta candidata. **U8 aberto; 27/42.**
D2/D4/G1/G2 e outras pendências não mudaram. Fixtures PE não comprovam
homologação física ou rollout Chrome. Nenhuma nova API Chrome ou investigação COM.

VLC permanentemente excluído de testes, verificações, consultas e ações/botões;
nenhum caso novo. Sem ações de resíduos/desinstalação, drivers ou alterações
em main/BETA/Worker/D1/DNS/flags, merge, deploy ou release. Etapa 4 não iniciada.

## Recibo final da CI

Código `5a7c0482a759009678b73c7c0cc75727b3b872f0`, [CI 40559631389](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40559631389),
tentativa 1, **success**: seis jobs obrigatórios aprovados e dois condicionais
skipped. [Evidência por job](evidencias/pre-beta-etapa3-ci-40559631389.json).

- **410/410 atualizadores**, incluindo as **42 verificações novas**.
- **196/196 multifontes**, 705/705 resíduos/desinstalação, 36/36 mutações
  rejeitadas, 47 MSIX, 30 encerramento e 41 Python/políticas.
- Integração com a tela real: **0→2 candidatos consultivos em quatro entradas**,
  dois controles sem candidato, duas consultas HTTP fictícias, **zero rede
  real e zero receitas executáveis**. São duas arquiteturas da mesma família,
  não dois novos aplicativos homologados nem instalações físicas.
- Oito combinações de interface WORK18, drivers 2022/2025 e integridade dos
  demais instaladores aprovados. Pacote x64 self-contained com 284 hashes
  conferidos pela CI; sem homologação AVG ou revalidação independente do ZIP.

Nenhum código mudou após essa CI. O recibo posterior atualiza somente
Markdown/JSON e usa `[skip ci]`; conferir seu SHA na PR #64 ao retomar.
**U8 e os demais checkpoints físicos continuam abertos, 27/42.** Próxima
sessão: somente Etapa 4. Nenhuma ação da Etapa 4 foi executada.
