# Design: kit de ferramentas profissionais para engenharia do GF

- Data: 2026-11-27
- Estado: proposta aprovada em princípio pelo proprietário; implementação ainda não iniciada
- Base consultada: PR #113, HEAD 2a142bd8c72752aa9ebb413b528243619b33dd43
- Escopo: ferramentas de desenvolvimento, testes e diagnóstico; não é funcionalidade do Gerenciador Final

## Intenção

Adicionar uma camada profissional, gratuita e verificável para reduzir o tempo entre uma falha do GF e sua causa demonstrada. A camada deve apoiar automação de WinForms, profiling, análise estática, comparação visual, diagnóstico de recursos e localização de regressões, sem transformar qualquer ferramenta em parte do EXE portátil.

## Objetivos

- Fixar, quando apropriado, versões de ferramentas de desenvolvimento sem modificar o runtime distribuído.
- Produzir traces, contadores e comparações locais que possam ser relacionados ao SHA e ao ambiente.
- Exercitar a janela WinForms real por UI Automation quando a infraestrutura nativa não for suficiente.
- Detectar problemas de C# por analyzers do SDK sem copiar código de terceiros.
- Comparar imagens e regiões com Python/OpenCV em um processo separado.
- Documentar PerfView, Windows Performance Analyzer e Git Bisect como instrumentos de investigação.
- Preservar orçamento zero, privacidade, licenças, áreas protegidas, workflows e o único produto integrado.

## Fora do escopo

- Nenhuma funcionalidade nova para usuários.
- Nenhum profiling, OpenCV, FlaUI, PerfView ou WPA no EXE, ZIP ou runtime do GF.
- Nenhum serviço, host, Azure, telemetria adicional ou automação agendada.
- Nenhuma mudança em main, G5, GF_BETA, Secrets, workflows ou PRs de origem.
- Nenhuma troca de framework, reconstrução, microserviço ou reestruturação geral.
- Nenhuma promoção de screenshot, trace, CI ou teste sintético para homologação física.
- Nenhuma instalação automática de software externo em máquinas de usuário.

## Contexto atual

GerenciadorAplicativos.csproj é um WinForms net10.0-windows com SDK fixado em global.json para .NET 10.0.402. O projeto principal já possui dependências de produção e publica um executável portátil. TestesLayout é um executável separado que referencia o produto e é o único alvo inicial possível para automação de interface. Os scripts privados já exigem git, dotnet e python. Não existe atualmente manifesto de ferramentas nem configuração central de analyzers.

## Abordagens consideradas

### A — somente ferramentas instaladas localmente

Usar Visual Studio Profiler, WPR/WPA, PerfView, UIA Verify/Accessibility Insights, dotnet-trace, dotnet-counters, Git Bisect e Python/OpenCV sem registrar wrappers no repositório.

Vantagem: menor alteração e nenhum arquivo novo.

Desvantagem: baixa reprodutibilidade; cada operador pode usar versões, argumentos e locais diferentes; evidências podem não ser comparáveis.

### B — kit de engenharia versionado, sem dependência de produto (escolhida)

Adicionar manifesto local de ferramentas, scripts dev-only, configuração de analyzers, utilitário de comparação visual e documentação. Ferramentas ficam em projetos de desenvolvimento/diagnóstico e nunca são empacotadas no produto. FlaUI fica condicional: só será integrada ao TestesLayout após uma verificação UIA nativa demonstrar uma lacuna concreta.

Vantagem: reprodutibilidade, rastreabilidade e baixo risco para o produto.

Desvantagem: acrescenta arquivos e exige manutenção mínima de versões e licenças.

### C — integração imediata de todas as bibliotecas

Adicionar FlaUI, OpenCV, benchmarks, geração de propriedades e mutation testing aos projetos e gates existentes.

Vantagem: maior automação potencial.

Desvantagem: aumenta dependências, tempo de CI, risco de conflito com a PR Draft, exposição de falhas de ambiente e manutenção; contraria a prioridade de recuperar a base antes de ampliar o sistema de testes.

A abordagem B foi escolhida. C permanece uma decisão posterior, por ferramenta, somente se B provar uma lacuna.

## Desenho aprovado

### 1. Manifesto e ferramentas locais

Criar um manifesto de ferramentas .NET dev-only para dotnet-trace e dotnet-counters, com versões fixadas depois da verificação de compatibilidade com o SDK 10.0.402. O manifesto serve para restauração explícita do ambiente de engenharia e não é referenciado pelo projeto de produção.

Git Bisect não será adicionado: já é parte do Git. PerfView e WPA não serão redistribuídos; serão documentados como ferramentas externas do ambiente Windows. WPR/WPA e SDK tools serão usados somente em máquinas de engenharia autorizadas.

### 2. Scripts de diagnóstico

Criar scripts em tools para:

- verificar a presença e a versão das ferramentas, sem instalar software silenciosamente;
- coletar trace/counters de um processo GF iniciado explicitamente pelo operador;
- exigir SHA, cenário, ambiente e diretório de saída fora da árvore do produto;
- recusar caminhos de saída dentro do ZIP/runtime;
- produzir arquivos locais sanitizáveis, sem upload;
- retornar falha clara quando a ferramenta estiver ausente ou a aplicação não for a esperada.

Nenhum script executará limpeza, atualização, reparo, elevação, instalação de driver ou manutenção real do Windows.

### 3. Analyzers

Adicionar configuração mínima e não destrutiva para Roslyn Analyzers já fornecidos pelo SDK. O primeiro ciclo deve inventariar diagnósticos e separar dívida preexistente de diagnósticos introduzidos pela candidata. Regras não serão convertidas em erro global nem usadas para bloquear a recuperação sem baseline e autorização.

A verificação será executável localmente e seus resultados serão associados ao SHA. A configuração não copiará analyzers de terceiros.

### 4. Comparação visual

Adicionar uma ferramenta Python/OpenCV separada para comparar PNGs e regiões:

- modo exato para igualdade de bytes/RGB quando exigido;
- modo de diferença visual com máscara/região e contagem de pixels;
- relatório que identifica arquivo, dimensão, região, threshold e SHA;
- rejeição de imagens ausentes, dimensões divergentes ou parâmetros implícitos;
- nenhuma conclusão de comportamento físico a partir da comparação isolada.

A dependência Python ficará separada do projeto .NET e não será transportada no produto. Licença e versão serão registradas antes da adoção.

### 5. UI Automation e FlaUI

O primeiro passo será usar a árvore UIA nativa e ferramentas de inspeção locais para confirmar nomes, papéis, estados, foco, padrões e unicidade.

FlaUI só será adicionado como dependência do TestesLayout, nunca do GerenciadorAplicativos, se o teste nativo não conseguir executar um cenário real necessário. A decisão exigirá:

- cenário mínimo reproduzível;
- controle positivo e negativo;
- licença e versão verificadas;
- comprovação de que não duplica ou enfraquece guards existentes;
- impacto em compilação, runtime de teste e privacidade documentado.

### 6. Evidência e privacidade

Cada saída deve registrar:

- SHA exato do código;
- versão das ferramentas;
- Windows, DPI, resolução e modo de execução quando relevantes;
- cenário e argumentos;
- tipo de evidência: sintética, componente real, desktop/UIA ou física;
- falha ou resultado sem inferências não demonstradas.

Traces, imagens e logs permanecem locais por padrão. Nenhum arquivo privado será enviado a plugin, serviço hospedado ou IA externa.

## Critérios de sucesso

- As ferramentas dev-only podem ser verificadas sem contaminar o artefato portátil.
- A coleta de trace/counters falha de modo seguro quando faltam ferramenta, processo, SHA ou cenário.
- O comparador rejeita imagens/parâmetros inválidos e produz contagens reproduzíveis.
- Analyzers são executados com baseline explícita e sem transformar dívida existente em regressão nova.
- A automação UIA, se usada, diferencia falha do harness de falha do produto.
- Nenhum workflow protegido, área protegida, função do usuário, serviço ou gasto é criado.
- Cada ferramenta responde a uma pergunta concreta; ferramentas sem efeito decisório serão removidas ou adiadas.

## Validação prevista

1. Verificação estática dos arquivos e licenças.
2. Testes locais dos scripts com entradas válidas, ausentes, divergentes e controle negativo.
3. Execução do comparador com imagens artificiais e imagens reais autorizadas, sem atribuir homologação física.
4. Execução de analyzers no mesmo SHA e registro do baseline.
5. Profiling local em cenário dirigido, quando houver Windows autorizado.
6. UIA real e, somente se necessário, spike isolado do FlaUI no TestesLayout.
7. CI existente apenas após o plano de implementação definir quais gates são afetados; este design não autoriza mudar workflows.

## Riscos e contenções

- Dependências de teste podem aumentar o custo do CI: manter FlaUI condicional e fora do produto.
- OpenCV pode criar divergência de ambiente: fixar versão, parâmetros e relatório; não usar como única prova.
- Traces podem conter informação sensível: manter local e sanitizar antes de qualquer registro.
- Analyzers podem produzir ruído: baseline, escopo incremental e sem falha global inicial.
- UI automation pode falhar por foco/desktop: registrar o primeiro predicado e preservar guards.
- Ferramentas podem virar burocracia: remover qualquer ferramenta que não mude uma decisão de engenharia.

## Próxima etapa

Após aprovação formal deste design, preparar um plano de implementação em incrementos pequenos, começando por manifesto, verificação de ferramentas, scripts de trace/counters, comparação visual e documentação. A integração do FlaUI será uma decisão condicional posterior, não parte automática do primeiro incremento.