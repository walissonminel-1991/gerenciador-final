# Ferramentas de engenharia do GF

Este diretório contém ferramentas de desenvolvimento, teste e diagnóstico. Nada aqui é funcionalidade do Gerenciador Final e nada deve ser incluído no EXE ou no ZIP portátil.

## Ambiente

- Windows x64 para o probe WinForms e os scripts PowerShell.
- PowerShell 7 ou posterior.
- .NET SDK 10.0.402, conforme global.json.
- Python 3.11 ou posterior.
- Git.

A restauração é sempre explícita. Nenhum script instala software silenciosamente.

## Ferramentas fixadas

O manifesto local .config/dotnet-tools.json fixa:

- dotnet-trace 10.0.750501;
- dotnet-counters 10.0.750501.

A comparação visual usa um ambiente Python separado:

- opencv-python-headless 5.1.0.95;
- numpy 2.4.5.

OpenCV headless é suficiente porque o comparador não abre janelas. Não instale simultaneamente variantes opencv-python, opencv-contrib-python ou headless no mesmo ambiente.

## Verificação

Depois da restauração explícita das ferramentas e do ambiente Python:

    dotnet tool restore --tool-manifest .config/dotnet-tools.json
    pwsh -File tools/Check-GfEngineeringTools.ps1

O health check apenas verifica versões e importações. Ele não instala, não inicia o GF e não altera o repositório.

## Probe WinForms

O probe é independente do produto:

    dotnet build tools/WinFormsToolProbe/WinFormsToolProbe.csproj -c Release
    dotnet run --project tools/WinFormsToolProbe/WinFormsToolProbe.csproj -c Release

Ele existe para testar ferramentas de diagnóstico, árvore UI Automation e profiling com uma janela determinística. Não representa o comportamento ou a homologação do GF.

## Coleta local de trace e counters

Com o processo autorizado já aberto, localize o PID e forneça metadados do build:

    pwsh -File tools/Collect-GfDiagnostics.ps1 -ProcessId 1234 -ExpectedCommit 0000000000000000000000000000000000000000 -Scenario synthetic-probe -BuildInfoPath C:\caminho\BUILD-INFO.txt -OutputDirectory C:\GF-Diagnostics\caso-01 -DurationSeconds 30

A coleta:

- anexa aos processos GerenciadorFinal ou GfToolProbe;
- exige o SHA esperado no BUILD-INFO ao lado do executável observado e verifica nome, caminho e horário de início do mesmo PID antes de cada coleta;
- exige uma duração entre 5 e 600 segundos e passa essa duração aos dois coletores;
- mantém o runtime do alvo suspenso quando aplicável, sem usar entrada interativa ou enviar Q;
- exige um diretório absoluto no mesmo volume local do repositório, fora da árvore do repositório, e cria uma pasta única por coleta;
- grava .nettrace, CSV e JSON somente nessa pasta externa;
- não inicia, fecha, reinicia, eleva ou interage com o processo-alvo, nem envia arquivos para serviços externos.

Traces e caminhos podem conter informações sensíveis. Mantenha-os locais e faça sanitização manual antes de qualquer publicação.

## Comparação visual

O comparador normaliza entradas de 8 bits (cinza, BGR ou RGBA) para RGBA e exige igualdade exata por padrão:

    python tools/compare_gf_screenshots.py reference.png candidate.png --output report.json

Imagens com profundidade diferente de 8 bits são rejeitadas, assim como um relatório cujo destino seja uma das capturas de entrada.

Código de saída:

- 0: imagens aprovadas;
- 1: diferença visual;
- 2: entrada ou configuração inválida;
- 3: dependência ausente.

Tolerância nunca é implícita. Quando autorizada, informe explicitamente:

    python tools/compare_gf_screenshots.py reference.png candidate.png --threshold 2 --region 10 20 552 186 --output report.json

A região deve estar dentro das duas imagens. O relatório inclui hashes SHA-256, dimensões, região, threshold, quantidade de pixels alterados e maior diferença de canal. Uma comparação visual não prova interação, desempenho, sensores ou homologação física.

## Ferramentas externas

- PerfView: análise local de traces .NET/ETW; não é redistribuído.
- Windows Performance Recorder/Analyzer: análise ETW local; não é dependência do GF.
- Git Bisect: comando já fornecido pelo Git para localizar regressões entre commits.
- UI Automation/Accessibility Insights/Inspect: primeira camada para inspeção de controles reais.
- FlaUI: não faz parte do primeiro incremento. Só será avaliado se UI Automation nativa demonstrar uma lacuna concreta.

Fontes:

- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
- https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-analyzer
- https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-recorder
- https://learn.microsoft.com/en-us/windows/win32/winauto/testing-tools
- https://github.com/LibreHardwareMonitor/LibreHardwareMonitor

## Limites

Estas ferramentas reduzem incerteza; não substituem os gates do AGENTS.md, a matriz de rastreabilidade, o CI existente ou a homologação física pelo WORK. Não adicione dependências ao projeto principal para usar este diretório.
