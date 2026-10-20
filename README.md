# Gerenciador Final · GF

**Ferramenta gratuita e de código aberto para Windows 10/11 x64**, destinada a tornar tarefas de manutenção de aplicativos e do sistema mais compreensíveis para pessoas sem experiência técnica.

![Prévia da identidade visual GF](Assets/GerenciadorFinal-preview.png)

**Versão do código em desenvolvimento:** v5.13.11.4 TESTE · **[Histórico de versões](CHANGELOG.md)** · **Estado:** testes automatizados; não é uma versão estável nem aprovada pelo AVG.

## Recursos

- Inventário independente do catálogo de atualizações (Win32 e MSIX/Store), busca de versões por RuckZuck e fontes oficiais HTTPS com identidade de produto/editor, ações de atualização verificadas e desinstalação com confirmação.
- Limpeza seletiva de arquivos temporários, Lixeira e caches, com informações de unidades e otimização pelo Windows.
- Diagnóstico de rede, reparos específicos mediante consentimento, redefinição avançada e reparo do Windows por DISM/SFC.
- Planos de energia e hibernação com opções explícitas.
- Gráfico de RAM com acompanhamento na bandeja, limpeza opcional apenas de caches internos do GF (não de memória de outros aplicativos), inicialização, Windows Update e informações do computador.
- Aba DRIVERS de consulta somente leitura com histórico local de dispositivos/INF; não instala, remove nem reverte drivers.
- Auditoria histórica de possíveis resíduos somente leitura, com proteção de instalações, dados pessoais e fabricantes compartilhados.
- Interface Windows Forms em português, tema escuro e ícone **GF** azul.
- Diagnóstico colaborativo: coleta local voluntária e prévia. Na v5.13.11.4 é possível **revisar um resumo e abrir uma Issue pública no GitHub**, mediante duas confirmações do usuário. **Nenhum envio automático para servidores está ativo.** Consulte [o piloto de colaboradores](docs/PILOTO-DIAGNOSTICO-GITHUB.md).

**Atenção:** uma ferramenta de manutenção não substitui backups, proteção antivírus ou suporte especializado. Operações administrativas podem causar alterações de rede, reinicializações ou perda de dados não salvos. Leia cada confirmação. Não há ganho de desempenho garantido.

## Para usuários finais

Após termos uma versão revisada e liberada, os downloads oficiais serão publicados em **Releases** neste repositório. Não execute instaladores encontrados em outros sites. Consulte o arquivo `SECURITY.md` e verifique hashes SHA-256 divulgados junto ao download.

**Situação da assinatura:** o projeto **não está assinado pela SignPath Foundation**. A inscrição será considerada depois da publicação e revisão. Versões sem assinatura podem apresentar avisos do Windows ou de antivírus. Não desative proteções nem configure exclusões para instalar o programa.

O instalador do Windows usa **Inno Setup** e instala uma aplicação .NET **self-contained**, portanto o usuário final **não precisa instalar o SDK .NET**. Algumas funções dependem de recursos do próprio Windows, como WinGet/App Installer.

## Código-fonte e compilação

Requisitos **somente para desenvolver/compilar**: Windows x64 e SDK do .NET 10. Para gerar o pacote portátil local:

```bat
Compilar-OSS-Windows-x64.bat
```

Ou diretamente no terminal:

```powershell
dotnet publish GerenciadorAplicativos.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o Distribuicao
```

O resultado estará em `Distribuicao\GerenciadorFinal.exe`. **Mantenha os arquivos adjacentes ao EXE**; ele depende da DLL e demais componentes publicados. Para uso portátil, consulte [o guia resumido](docs/USO-PORTATIL.md); para validação, siga [o roteiro de testes](docs/TESTES.md). Para um instalador de teste consulte `docs/PUBLICACAO.md`. Os fluxos antigos `Criar-Instalador-Windows-x64.bat`, `Assinar-GF.cmd` e `GerenciadorFinal-Instalador.iss` continuam exigindo certificado Authenticode próprio; **não fazem parte da assinatura gratuita da SignPath**.

O GitHub Actions compila com um runner Windows hospedado pelo GitHub e conserva **artefatos de teste não assinados** com nome versionado, `BUILD-INFO.txt` e `SHA256SUMS.txt`. O workflow de validação candidata executa regressões de inventário, fontes, instaladores, drivers, resíduos, RAM e segurança. Nenhum desses testes valida o alerta do AVG ou equivale a uma Release pública. Consulte `.github/workflows/build-windows.yml`, `.github/workflows/validar-candidata-windows.yml` e [o roadmap](docs/ROADMAP.md).

**Estado de segurança:** há investigação pendente sobre alerta comportamental `IDP.Generic` no AVG. A amostra foi encaminhada pelo mantenedor. Não desative proteções nem tente executar um arquivo em quarentena. A assinatura SignPath segue pendente.

## Segurança e privacidade

- Operações que alteram configurações solicitam confirmação e, quando necessário, permissão UAC.
- O instalador não adiciona exceções de antivírus nem regras de firewall.
- A análise de rede faz DNS e teste de conectividade com `www.microsoft.com`, além de testes locais; recursos WinGet e Windows Update interagem com provedores externos quando acionados.
- Consulte [PRIVACY.md](PRIVACY.md) e [SECURITY.md](SECURITY.md) para detalhes e divulgação responsável.

## Code signing policy

**Status: solicitação futura, ainda não aprovado.** As assinaturas públicas planejadas serão feitas apenas de artefatos gerados por workflow verificável a partir deste repositório, após aprovação manual. Todas as versões devem manter metadados coerentes de produto e versão. A equipe do projeto terá papéis de autor/revisor/aprovador documentados antes da inscrição. O arquivo [docs/SIGNPATH.md](docs/SIGNPATH.md) explica o procedimento e suas restrições.

Se o projeto for aceito, a política será atualizada para registrar o provedor e o certificado autorizados, com o crédito exigido pela SignPath Foundation. **Nenhuma assinatura ou aprovação é afirmada aqui.**

## Como contribuir

Leia [CONTRIBUTING.md](CONTRIBUTING.md) antes de abrir uma contribuição. Relatos de vulnerabilidades devem seguir [SECURITY.md](SECURITY.md). O projeto está sob a [licença MIT](LICENSE), sujeita à revisão final pelo titular do código e dos recursos gráficos antes de sua primeira publicação.

## Sobre o nome e o ícone

**Gerenciador Final** e **GF** identificam este projeto. A licença de código e dos recursos gráficos incluídos permite reutilização conforme `LICENSE`, ressalvados direitos de terceiros. Não sugere aprovação ou associação à Microsoft ou aos fornecedores de antivírus.
