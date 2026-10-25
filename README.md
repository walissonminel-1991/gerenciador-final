# Gerenciador Final · GF

**Ferramenta gratuita e de código aberto para Windows 10/11 x64**, destinada a tornar tarefas de manutenção de aplicativos e do sistema mais compreensíveis para pessoas sem experiência técnica.

![Prévia da identidade visual GF](Assets/GerenciadorFinal-preview.png)

**Candidata TESTE: v5.13.11.64 — integração em validação, SEM aprovação técnica consolidada.** [CI aprovada da candidata anterior .63](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234268549) · [PR de integração #59](https://github.com/walissonminel-1991/gerenciador-final/pull/59). Integra os lotes 1–4 e o desenvolvimento WORK14–WORK17 em uma branch isolada, com leitores WinGet COM consultivos e políticas de execução ainda restritivas. Os resultados e hashes da versão anterior não validam o novo executável. Consulte o [relatório de integração](docs/MISSAO-INTEGRACAO.md). Última candidata tecnicamente aprovada: .63. **Não há release estável, merge na main, aprovação AVG nem homologação física. Roadmap 27/42.**

**Estado atual:** [roadmap 27/42](docs/ROADMAP.md) · [organização](docs/ORGANIZACAO-REPOSITORIO.md) · [roteiro físico vigente](docs/TESTE-CONSOLIDADO.md) · [PR #40 (Draft)](https://github.com/walissonminel-1991/gerenciador-final/pull/40). A limpeza residual agora pertence à desinstalação: conferência automática, uma decisão SIM/NÃO por seleção e exclusão permanente dos alvos exclusivos revalidados ao escolher **SIM**. **NÃO** mantém os resíduos; dados pessoais, compartilhados ou desconhecidos são preservados. A cobertura é progressiva. U8, R7 e R4 físicos continuam abertos; inventário parcial não comprova remoção.

## Recursos

- Inventário independente do catálogo de atualizações (Win32 e MSIX/Store), busca de versões por RuckZuck e fontes oficiais HTTPS com identidade de produto/editor, ações de atualização verificadas e desinstalação com confirmação.
- Limpeza seletiva de arquivos temporários, Lixeira e caches, com informações de unidades e otimização pelo Windows.
- Diagnóstico de rede, reparos específicos mediante consentimento, redefinição avançada e reparo do Windows por DISM/SFC.
- Planos de energia e hibernação com opções explícitas.
- Gráfico de RAM com acompanhamento na bandeja, descarte de caches internos e ajuste seletivo do working set de um aplicativo em segundo plano autorizado pelo usuário (não equivale a liberar RAM permanentemente), inicialização, Windows Update e informações do computador.
- Aba DRIVERS somente leitura: IDs de hardware, histórico Windows, associações de INFs publicados, ofertas oficiais do Windows Update e verificação nativa do INF selecionado. Não instala, remove nem reverte drivers; a assinatura do INF não certifica o pacote inteiro nem sua compatibilidade.
- Desinstalação com investigação residual automática e confirmação única SIM/NÃO, incluindo lotes. Exclusão por identidade/conteúdo, conferência final e UAC integrado quando necessário; locais pessoais, compartilhados, alterados e não verificáveis são preservados. Sem ferramenta residual manual independente.
- Consultas multifontes com versões divergentes preservadas, origem na dica da linha e bloqueio do instalador direto em conflitos; cinco novos produtos consultivos e comparação Proton com feed estável Windows priorizado. A consulta WinGet COM é somente leitura e condicionada à disponibilidade do Windows; o CLI não é executado e descoberta consultiva não autoriza instalação.
- Atualizações com progresso real de download por instalação, etapas sem porcentagem inventada, resultado verde/vermelho/pendente/cancelado e confirmação persistente durante a sessão, mesmo após renovar a lista.
- Histórico / relatório junto ao REGISTRO: contagens e códigos sem nomes/caminhos, comparação somente entre inventários completos e inequívocos, exportação TXT por escolha explícita.
- Interface Windows Forms em português, tema escuro e ícone **GF** azul.
- Diagnóstico colaborativo: coleta **local, voluntária e revogável**, com prévia em linguagem simples e botão **Simular sem enviar** usando dados fictícios (v5.13.11.8). Não existe envio real, anexo, formulário de Issue nem conta GitHub exigida do usuário. A arquitetura de compartilhamento futuro ainda depende de segurança e consentimento específico; veja [experiência de contribuição](docs/EXPERIENCIA-CONTRIBUICAO-SIMPLES.md) e [roteiro de autenticação GitHub](docs/PILOTO-AUTENTICACAO-GITHUB.md).

**Atenção:** uma ferramenta de manutenção não substitui backups, proteção antivírus ou suporte especializado. Operações administrativas podem causar alterações de rede, reinicializações ou perda de dados não salvos. Leia cada confirmação. Não há ganho de desempenho garantido.

## Para usuários finais

Após termos uma versão revisada e liberada, os downloads oficiais serão publicados em **Releases** neste repositório. Não execute instaladores encontrados em outros sites. Consulte o arquivo `SECURITY.md` e verifique hashes SHA-256 divulgados junto ao download.

**Situação da assinatura:** o projeto **não está assinado pela SignPath Foundation**. A inscrição será considerada depois da publicação e revisão. Versões sem assinatura podem apresentar avisos do Windows ou de antivírus. Não desative proteções nem configure exclusões para instalar o programa.

O portátil inclui uma aplicação .NET **self-contained**, portanto o usuário final **não precisa instalar o SDK .NET**. As consultas de aplicativos usam RuckZuck e fontes oficiais HTTPS; não exigem a CLI WinGet. MSIX/Store e Windows Update dependem das APIs e serviços disponíveis no Windows.

## Código-fonte e compilação

Requisitos **somente para desenvolver/compilar**: Windows x64 e SDK do .NET indicado em [global.json](global.json) (10.0.402 exato, sem resolução para outro SDK). Para gerar o pacote portátil local:

```bat
Compilar-OSS-Windows-x64.bat
```

Ou diretamente no terminal:

```powershell
dotnet publish GerenciadorAplicativos.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o Distribuicao
```

O resultado estará em `Distribuicao\GerenciadorFinal.exe`. **Mantenha os arquivos adjacentes ao EXE**; ele depende da DLL e demais componentes publicados. Para uso portátil, consulte [o guia resumido](docs/USO-PORTATIL.md); para validação, siga [o roteiro de testes](docs/TESTES.md). Para um instalador de teste consulte `docs/PUBLICACAO.md`. Os fluxos antigos `Criar-Instalador-Windows-x64.bat`, `Assinar-GF.cmd` e `GerenciadorFinal-Instalador.iss` continuam exigindo certificado Authenticode próprio; **não fazem parte da assinatura gratuita da SignPath**.

Os três workflows atuais estão em `.github/workflows/` (compilação, regressões e política de segurança); configurações antigas foram retiradas da árvore ativa e permanecem recuperáveis no histórico Git. O GitHub Actions compila com um runner Windows hospedado pelo GitHub e conserva **artefatos de teste não assinados** com nome versionado, `BUILD-INFO.txt` e `SHA256SUMS.txt`. O workflow de validação candidata executa regressões de inventário, fontes, instaladores, drivers, resíduos, RAM e segurança. Nenhum desses testes valida o alerta do AVG ou equivale a uma Release pública. Consulte `.github/workflows/build-windows.yml`, `.github/workflows/validar-candidata-windows.yml` e [o roadmap](docs/ROADMAP.md).

**Estado de segurança:** há investigação pendente sobre alerta comportamental `IDP.Generic` no AVG. A amostra foi encaminhada pelo mantenedor. Não desative proteções nem tente executar um arquivo em quarentena. A assinatura SignPath segue pendente.

## Segurança e privacidade

- Operações que alteram configurações solicitam confirmação e, quando necessário, permissão UAC.
- O instalador não adiciona exceções de antivírus nem regras de firewall.
- A análise de rede faz DNS e teste de conectividade com `www.microsoft.com`, além de testes locais; as consultas de catálogos HTTPS e Windows Update interagem com seus provedores, inclusive nas verificações automáticas da inicialização.
- Consulte [PRIVACY.md](PRIVACY.md) e [SECURITY.md](SECURITY.md) para detalhes e divulgação responsável.

## Code signing policy

**Status: solicitação futura, ainda não aprovado.** As assinaturas públicas planejadas serão feitas apenas de artefatos gerados por workflow verificável a partir deste repositório, após aprovação manual. Todas as versões devem manter metadados coerentes de produto e versão. A equipe do projeto terá papéis de autor/revisor/aprovador documentados antes da inscrição. O arquivo [docs/SIGNPATH.md](docs/SIGNPATH.md) explica o procedimento e suas restrições.

Se o projeto for aceito, a política será atualizada para registrar o provedor e o certificado autorizados, com o crédito exigido pela SignPath Foundation. **Nenhuma assinatura ou aprovação é afirmada aqui.**

## Como contribuir

Leia [CONTRIBUTING.md](CONTRIBUTING.md) antes de abrir uma contribuição. Relatos de vulnerabilidades devem seguir [SECURITY.md](SECURITY.md). O projeto está sob a [licença MIT](LICENSE), com os [avisos de terceiros](docs/THIRD-PARTY-NOTICES.md) preservados na distribuição.

## Sobre o nome e o ícone

**Gerenciador Final** e **GF** identificam este projeto. A licença de código e dos recursos gráficos incluídos permite reutilização conforme `LICENSE`, ressalvados direitos de terceiros. Não sugere aprovação ou associação à Microsoft ou aos fornecedores de antivírus.

