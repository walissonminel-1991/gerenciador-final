# MISSÃO WORK 5 — investigação e barreiras WinGet

## Baseline verificado em 21/10/2026

HEAD remoto `bbdc02f2238f5cad8d5b986c387acab55d609727`, versão 5.13.11.32 TESTE;
PR #40 aberto e Draft; main `9ac3dc167b9e67ac50628749d5ad33c563c09376`.
Actions desse HEAD: regressões 39684776766, build 39684776783, política 39684776790,
todas success. Backup lógico: o HEAD anterior continua acessível; commits desta missão
têm esse histórico como ancestral. Sem merge, release ou alteração destrutiva.

Log privado da .32, sem publicação do arquivo: 174 apps, 27 MSIX conforme teste
presencial documentado; 3 candidatas, 8 fontes fixas consultadas e 8 respondendo,
631 entradas RuckZuck, 4 incertas. Consultas 3643 / 985 / 350 / 1353 ms.
Não inferir cache quente apenas pelo tempo. O diagnóstico agregado não identifica
nomes/editor/arquitetura/escopo das seis diferenças e não comprova o estágio exato
do GameInput. Não publicar inventário, caminhos ou imagens do usuário.

Baseline local: Linux, sem AVG/Windows/COM. A suíte multifontes anterior passou
69/69. Não é medição de startup Windows. Processos auxiliares, escritas, temporários,
CPU/RAM, flashes e AVG/Defender da .32: **não medidos nesta missão**. Offline real
nos PCs: pendente; falhas HTTPS/cancelamento serão exercitados por fixtures.

## Etapa 0 — tabela de regressões e prevenção

| Observação/evidência | Mecanismo/certeza | Risco de retorno | Prevenção e mitigação |
| --- | --- | --- | --- |
| v5.12.3 (`a49e401`) consultava `list --upgrade-available` em LoadUpdatesAsync; chamada automática na abertura | Código comprovado; aceitação de contratos e parsing textual | Reativar método antigo ou fallback CLI | Nenhum executor COM/CLI distribuído; contratos/mocks isolados; testes de desligado e chamadas normais |
| PowerShell/ExecutionPolicy falharam durante desenvolvimento anterior | Relato histórico; commits disponíveis começam em .12.3 e não contêm a cadeia anterior | Reintroduzir script ou reparo automático | Não criar scripts de consulta nem mudar políticas; varredura do código novo |
| Janelas extras/console | Relato; v.12.3 já usava CreateNoWindow, que não prova compatibilidade AVG | Subprocesso ou servidor COM | Não ativar COM nem iniciar CLI; inspeção de factories e suíte |
| Processos ocultos e logs/temporários | A API COM usa servidor fora do processo; WinGet grava logs segundo documentação | Confundir COM com execução sem efeitos locais | Integração real bloqueada; nada de fallback transparente |
| SDK/runtime ausente e instalação anterior de SDK | Relato; script de criação de instalador atual contém oferta opcional no PC de desenvolvimento | Confundir criação do pacote com execução portátil | Pacote self-contained; remover bootstrap WinGet do script de criação; não instalar SDK no destino |
| Download de EXE | Receitas existentes explícitas; não é consulta de versão | Transformar catálogo remoto em autorização de execução | Preservar hashes/assinatura/receita local; divergência só consultiva |
| UAC no início | Não demonstrado no HEAD; manifesto asInvoker comprovado | Autoelevar por comando não encontrado | Sem auxiliar novo, runas ou reparo; consulta de contexto não eleva |
| Startup lento/consumo | Sem comparação física nesta missão | Ativar catálogos/subprocessos continuamente | WinGet off, TTL 10 min e concorrência limitada; consulta normal assíncrona |
| App piscava/fechava; IDP.Generic na bandeja | Relatos e docs/AVG-DESENVOLVIMENTO-SEGURO.md; causa causal não comprovada | Repetição automática após falha | Não reiniciar; falha desliga adaptador e preserva motores HTTPS |
| Falso positivo atribuído à assinatura | Hipótese; sem confirmação AVG de causa | Promessa indevida de correção | Hash/CI não aprovam antivírus; AVG real permanece pendente |
| Coleta incompleta/MSIX | Falhas documentadas; proteção de snapshot existente | Misturar máquinas/sessões, tratar ausência como desinstalação | Snapshot por referência, sem persistir achados; conservar somente inventário |

Histórico examinado: `git log --all` e buscas -S por ExecutionPolicy/GetWingetExecutable;
`a49e401`, `278a554` (RuckZuck), `3f77244`, `d118f11`, `e5080d3`, `ea8e5f0`,
docs de AVG e testes do PC FRACO/FORTE; metadados/Actions do PR #40.
A ausência de scripts pré-repositório impede afirmar que toda a história local foi reconstruída.

## Decisão ADR — sem executor WinGet nesta candidata

`Microsoft.Management.Deployment` é a API do Windows Package Manager;
`Windows.Management.Deployment` é inventário/deployment MSIX e não a substitui.
O IDL oferece catálogo composto/local, pesquisa e operações assíncronas estruturadas;
a criação/ativação, contratos por versão e cancelamento dependem da implementação instalada.
COM não significa execução dentro do GF: o manifesto e WinMain do upstream registram
WindowsPackageManagerServer.exe. A spec descreve CoCreateInstance em servidor externo.
Ainda faltam prova .NET 10 self-contained/unpackaged, App Installer por usuário,
compatibilidade das versões, processo/IO real e comportamento com AVG.

Escolha: contrato consultivo isolado, sessão efêmera e backend de produção ausente;
fixtures não executam WinGet. Nenhuma CLI como fallback, interop NuGet nova, identidade
MSIX artificial, SDK/runtime adicional, autoelevação ou reparo. O opt-in em teste não
supera ausência de backend validado. O programa continua com motores HTTPS existentes.
Consulta administrativa rejeitada por falta de necessidade comprovada; nenhum novo IPC.

Parada futura: alerta AVG, console/flash/UAC inesperado, dependência, escrita inesperada,
assinatura/compatibilidade inválida, falha, timeout ou custo material: cancelar,
desabilitar a sessão e manter HTTPS. Sem exceção, tentativa invasiva ou reinício.
Uma chamada COM não cooperativa não pode ser encerrada com segurança apenas por
CancellationToken; esse problema também bloqueia o executor real.

Fontes primárias consultadas (links de pesquisa; nenhum código externo incorporado):

- https://github.com/microsoft/winget-cli/blob/master/src/Microsoft.Management.Deployment/PackageManager.idl
- https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23888%20-%20Com%20Api.md
- https://github.com/microsoft/winget-cli/blob/master/src/WinGetServer/WinMain.cpp
- https://github.com/microsoft/winget-cli/blob/master/src/AppInstallerCLIPackage/Package.appxmanifest
- https://learn.microsoft.com/windows/package-manager/winget/
- https://learn.microsoft.com/windows/package-manager/winget/troubleshooting
- https://github.com/microsoft/winget-cli/blob/master/doc/troubleshooting/README.md

As instruções de reparo dessas páginas não são executadas. Alias/PATH/registro por usuário
são hipóteses documentadas para terminal normal versus elevado, não prova de precisar de admin.

## Execução, testes e entrega

Implementação .33: `WingetConsultativeProvider.cs` (contrato sem backend real),
`WingetContextInspection.cs` (presença no contexto atual), `OfficialReleaseSources.cs`
(fontes, procedência, TTL e reconciliação), modelos, inventário para contexto,
tooltips, resolvers, diagnóstico opt-in e testes. Sem nova dependência NuGet.
Só fatos públicos/nomes de produtos e editoras foram usados para mapear fontes;
nenhum código GPL ou de outro projeto foi copiado. Script de desenvolvimento não
oferece mais bootstrap de SDK/Inno por WinGet; o runtime do portátil não mudou.

### Matriz antes/depois — sem instalar

| Produto | .32 no PC FORTE | .33: cobertura implementada / limite |
| --- | --- | --- |
| 7-Zip | GF e WinGet 26.05 | GitHub oficial preservado; evidências consolidadas; um registro por instalação |
| Outlook for Windows | GF e WinGet 1.2026.812.100 | Índice HTTPS/identidade MSIX preservados; Store como rota oficial |
| Proton Pass | GF 1.43.0 / WinGet 1.42.0 | Feed Windows priorizado: 1.42.0 estável; changelog 1.43.0 e índice 1.42.0 preservados; sem instalador direto |
| EA app | Somente WinGet | Faltava receita fixa: agora índice ElectronicArts.EADesktop por HTTPS; somente consulta/site |
| FanControl | Somente WinGet | Faltava receita fixa e comparação inteira: GitHub Rem0o/FanControl.Releases e V284; sem instalar dependências |
| Jellyfin Server | Somente WinGet | Faltava receita fixa: GitHub jellyfin/jellyfin; mudança de principal exige revisão; não confirma instalador Windows |
| K-Lite Mega Codec Pack | Somente WinGet | Faltava receita fixa: índice CodecGuide.K-LiteCodecPack.Mega; rejeita Standard/Full e fabricante divergente |
| Microsoft GameInput | Somente WinGet | Fonte consultiva já existia; causa real exata permanece pendente. Códigos por etapa acrescentados; receita oficial restrita não ampliada |
| Plex Media Server | Somente WinGet | Identidade existia sem fonte: índice Plex.PlexMediaServer por HTTPS, predicado restrito ao Server; não confunde player |

A coluna .32 vem da comparação relatada/documentada, não de inventário bruto reproduzido.
Ausência de fonte fixa é **comprovada no código** para cinco produtos, mas não prova sozinha
em que etapa cada registro real falhou no RuckZuck. Os campos privados não estão no log.
Assim, para esses registros reais, vínculo de fabricante/versão/arquitetura/canal/escopo e
etapa exata permanecem pendentes; fixtures demonstram os caminhos, não substituem evidência.
Não se promete nove candidatas no PC. Versões publicadas hoje podem mudar.

Proton: investigação adicional localizou a documentação primária do autoatualizador e
consultou somente o JSON público oficial:
`https://proton.me/download/PassDesktop/win32/x64/version.json`.
Em 21/10/2026 esse feed oferece **1.42.0, Stable, rollout 1**, com referência ao MSIX
x64; changelog anuncia 1.43.0. O índice comunitário também aponta 1.42.0. Logo, não se
pode atribuir a diferença apenas a atraso do WinGet: anúncio no changelog não comprova
distribuição Windows. A implementação passou a priorizar o feed Windows e manter as
três evidências. Beta, rollout parcial, URL fora do caminho/host/versionamento esperado,
hash de metadados inválido ou esquema ambíguo são rejeitados. Só o JSON fixo foi
consultado; o GF não baixa/segue a URL de pacote nem usa seus argumentos.
O checksum do feed é informação, não validação de bytes/assinatura do instalador.
Instalador 1.43.0 Windows e assinatura do binário 1.42.0 não foram verificados;
Proton nunca foi usado como cobaia de instalação.
Fontes: commit `9ebebbc88e8b3b9652d4b5bbfebfd8e2d3538966`,
`https://github.com/ProtonMail/WebClients/blob/main/applications/pass-desktop/CHANGELOG.md`,
`https://github.com/ProtonMail/WebClients/blob/main/applications/pass-desktop/update/README.md`,
`https://github.com/microsoft/winget-pkgs/tree/master/manifests/p/Proton/ProtonPass`.

### Cache, identidade e estado de instalação

TTL monotônico explícito de 10 min. Cache contém somente versões públicas, separado
por fonte/transportes e relógio; não armazena inventário, usuários ou achados. Instância de
produção compartilha apenas transportes públicos fixos. Ao mudar inventário, associações
são reconstruídas usando os objetos do snapshot novo; resultados de outro snapshot
são recusados. A versão pública não precisa ser apagada porque o inventário mudou.
Sem cache persistente nem associação entre processos/máquinas. Data da observação é
preservada no cache. Fabricante/identidade/canal e dimensões conhecidas incompatíveis
bloqueiam reconciliação. Dimensões desconhecidas são declaradas, nunca prova de elegibilidade.
Feed estável específico Windows prevalece sobre anúncio e catálogo comunitário; divergências
continuam visíveis. Nenhuma observação consultiva gera receita nova.

`Instalador verificado` continua sendo responsabilidade separada dos resolvers locais,
SHA-256/WinTrust, contexto e confirmação humana. Divergências/revisão impedem
EXE/MSIX direto mesmo que exista receita; Store/site/orientação continuam possíveis.
Não houve autorização de instalação, desinstalação ou manutenção de drivers nesta missão.

### Códigos agregados do perfil temporário

COBERTURA_ATUALIZACAO fonte: 1 EA, 2 FanControl, 3 Jellyfin Server, 4 K-Lite Mega,
5 GameInput, 6 Plex, 7 Proton changelog, 8 Proton índice comunitário, 9 Proton Windows.

Etapa: 0 nenhuma identidade canônica reconhecida (não prova app ausente); 1 sem fonte fixa;
2 somente inventário/inelegível; 3 identidade/canal não corresponde; 4 fonte não consultada;
5 falha de fonte; 6 ausência de achado nessa fonte; 7 conflito entre fontes;
8 revisão de contexto/migração; 9 candidata; 10 sem versão superior;
11 versão não comparável; 12 outra regra conservadora.

Código 3 é verificável diretamente por testes; para identidade não reconhecida no PC,
usa-se 0, sem publicar nomes/editor. O diagnóstico respeita o teto total de 160 tentativas
preexistente e só contém códigos/contagens. Se atingir teto, não presumir completude.
WINGET_CONTEXTO contém somente cinco booleanos; alias presente não comprova executável
genuíno ou API suportada. Não executa o alias nem pesquisa executáveis arbitrários no PATH.

### Verificação antes da CI

Linux: multifontes 127/127, motor 18/18, política 15/15; cross-build do aplicativo e
suite de instaladores aprovado, zero avisos/erros. Nenhum teste Windows/AVG foi simulado
como execução real. Primeiro checkpoint `8ed6c95` passou as três Actions (39689834609, 39689834693, 39689834600). A descoberta do feed Windows gerou outro commit; CI final e artefato devem ser do código posterior, nunca desse pacote intermediário.
Fixtures: nove produtos/três fontes Proton, falsos fabricantes/edições, versões inteiras,
contextos isolados x86/x64/ARM64/usuário/máquina/portátil/MSIX/canal, cache/TTL/data,
falhas de fontes independentes, offline, progresso, snapshot estranho, desligado em todos
os gatilhos, opt-in restrito, timeout não cooperativo, cancelamento e parada da sessão.
Mocks de ausência/COM/alias/política são estados simulados, não disponibilidade real no runner.
Tempos de fixtures são custo do teste; não se extrapolam para os PCs.

Ainda não há medição real da .33 nos PCs, aprovação AVG, COM/CLI real, validação visual
física de todos os DPI ou explicação individual definitiva dos seis registros privados.
[Próxima bateria, PC FORTE depois PC FRACO](TESTES.md#próxima-bateria-33--somente-consultas).
Este checkpoint fecha a investigação disponível, não o portão presencial.
Roadmap permanece **26/42 (61,9%)**; R4 e G1–G5 bloqueados.
