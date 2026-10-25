# WORK16 — linha de base e gargalos

Inspeção em 25/10/2026. Base imutável: `f179dbabe78d0d42c4112a283040c5c781f109f0` (PR #51).

| PR | Estado observado | HEAD | CI |
|---|---|---|---|
| #40 | aberta, Draft, não integrada | ef13aef4 | 40142017415 aprovada |
| #47 | aberta, Draft, não integrada | c27231cb | 40148654831 aprovada |
| #49 | aberta, Draft, não integrada | 954949ee | 40151238332 aprovada |
| #50 | aberta, Draft, não integrada | a24753af | 40156822827 aprovada |
| #51 | aberta, Draft, não integrada | f179dbab | 40179729611 aprovada |

A cadeia #51 → #50 → #49 → #47 → #40 preserva os consertos de estados indeterminados, direitos mínimos HKLM e isolamento de regras per-user. A issue #48 continua aberta para reavaliação física; CI não comprova resolução no PC FORTE. Nova branch WORK16 sobre #51, sem merge ou alteração da main.

## Gargalos confirmados no código antes da implementação

- `WingetConsultativeProvider` não possui backend de produção; somente fixtures conseguem consultar. Presença de alias/MSIX não demonstra COM funcional.
- `PackageCatalogIndex` combina nome/editor e, consultivamente, ProductCode. Não consulta PFN nem os identificadores associados pela API oficial. GUID do MSI estável entre duas versões não é uma premissa válida.
- `AutomaticUpdateDiscovery` preserva permissões efêmeras, hash e assinatura, mas cada fase está acoplada a uma fonte. Um erro de uma entrada pode descartar todo o resultado desse provedor.
- Inventário combina ARP, MSIX e bibliotecas de jogos. Não lê instalações Scoop/Chocolatey sem ARP. Nome de pacote comunitário não é nome do produto nem prova de instalação.
- MSI/WiX, NSIS e Inno possuem contratos efetivos e revalidação. Burn, Squirrel e gerenciadores externos não podem herdar esses contratos pelo reconhecimento do formato.
- Capacidades já distinguem consulta e execução, mas identidades reconhecidas são contadas apenas no catálogo estático; faltam contadores por origem e gerenciamento externo.
- `QuietUninstallString`/ARP já permitem remoção supervisionada de produtos desconhecidos. Não refazer esse suporte nem acrescentar switches a qualquer EXE.

## Medição

Manter denominadores fixos e executar o mesmo probe contra esta base e a candidata. Fixtures, metadados públicos inspecionados e homologação física devem ocupar conjuntos separados. Medir decisões dos motores, sem somar quantidade de testes como aplicativos compatíveis. Novos provedores consultivos não contam como operações autorizadas. A comparação WORK14 existente permanece preservada.

## Pesquisa primária

- Microsoft COM IDL: https://github.com/microsoft/winget-cli/blob/master/src/Microsoft.Management.Deployment/PackageManager.idl
- Interop oficial .NET: https://www.nuget.org/packages/Microsoft.WindowsPackageManager.ComInterop/1.29.380
- Módulo oficial: https://github.com/microsoft/winget-cli/blob/master/src/PowerShell/Help/Microsoft.WinGet.Client/Get-WinGetPackage.md
- Scoop, metadados e migração dos nomes: https://github.com/ScoopInstaller/Scoop/blob/master/lib/install.ps1 e https://github.com/ScoopInstaller/Scoop/releases
- Chocolatey (upgrade pode instalar pacote ausente): https://docs.chocolatey.org/en-us/choco/commands/upgrade/
- NSIS: https://nsis.sourceforge.io/Docs/Chapter3.html
- Inno: https://jrsoftware.org/ishelp/topic_setupcmdline.htm
- Burn: https://docs.firegiant.com/wix/tools/burn/ e https://docs.firegiant.com/wix/tools/signing/
- Squirrel: https://github.com/Squirrel/Squirrel.Windows/blob/develop/src/Update/Program.cs

`Microsoft.WinGet.Client` exige host PowerShell/módulo e pode provisionar dependências se usado para reparo. Preferência pelo interop COM tipado, sem reparo, instalação de WinGet, consentimento de fontes automático ou parsing de texto CLI. Scoop e Chocolatey são inicialmente leitores de dados locais; seus scripts não recebem autoridade de execução.
