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

Resultados de implementação/CI, matriz dos nove produtos e roteiro: completar nos commits
seguintes. Este checkpoint fecha a investigação disponível, não o portão presencial.
Roadmap permanece **26/42 (61,9%)**; R4 e G1–G5 bloqueados.
