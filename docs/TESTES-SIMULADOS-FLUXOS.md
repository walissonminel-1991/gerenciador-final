# GERENCIADOR FINAL — simulações integradas de atualização, desinstalação e resíduos

> **Atualização pós-WORK 6:** a bateria aqui descrita é o baseline inicial da .34. O Work adicionou testes de orquestração, cópias descartáveis, prévia nativa e 10/10 mutações rejeitadas na candidata .35. Veja [o fechamento técnico da WORK 6](MISSAO-WORK6-RESULTADO.md). Os gates originais U8/R7/R4 não foram alterados.

> Preparação em 21/10/2026, branch experimental do PR #40. Implementação de
> testes **não altera a versão v5.13.11.34 TESTE distribuída**.
> Evidência automatizada e validação de ação efetiva são coisas distintas.

## Regra de produto

- Nenhum botão para executar como administrador. O usuário inicia o aplicativo
  como preferir; não há elevação automática, mudança de manifesto ou WinGet COM/CLI.
- A versão de teste em uso continua funcionando sem exigir SDK no computador do usuário.
- As novas matrizes são **cenários em memória**, sem download ou lançamento de
  instaladores/desinstaladores, sem exclusão de arquivos e sem varredura de perfis.
- As suítes já existentes podem criar e remover **fixtures sintéticas isoladas**
  (diretório temporário e chaves temporárias em HKCU) nos runners de testes.
  Não significam limpeza de resíduos reais. Não instalar SDK nos PCs pessoais só
  para executar essa suíte.
- Não gravar logs privados, paths, programas ou capturas dos computadores no GitHub.

## Ordem e critérios de simulação

### 1. Atualizações — validação de contrato de U8, sem instalar

Código: `TestesAtualizador/SimulatedUpdateScenarioTests.cs`.

- Usar apenas OBS Studio fictício 31.1.2→32.2.2 e identidade de inventário
  inventada. Conferir receita local aprovada sem acessar a rede.
- Confirmar resultado hipotético apenas com **mesmo identificador, fabricante
  e versão final**, a partir do snapshot posterior simulado.
- Recusar: inventário vazio, versão antiga, outro usuário/escopo, fabricante
  diferente, conflito de fontes, URL/argumentos alterados.
- Nenhuma chamada a `DownloadAndVerifyAsync` ou execução de programa novo
  na bateria específica. Testes de integridade existentes continuam separados.

**Para fechar U8:** ainda será necessário pelo menos um teste real,
individual, voluntário e reversível quando viável, comprovando versão e
integridade após a execução; fixture não comprova sucesso do instalador.

### 2. Desinstalações — validação de contrato de R7, sem desinstalar

Código: `TestesResiduos/SimulatedUninstallScenarioTests.cs`.

- Usar aplicativos fictícios e um comando MSI de fixture que **nunca é
  executado**.
- Preservar GameInput protegido; esconder comando de desinstalação ausente.
- Cancelamento UAC, erro de acesso e solicitação de elevação **não reexecutam**
  um desinstalador nem autorizam varredura destrutiva.
- Aplicativo ainda instalado, segunda instalação ou escopo alternativo
  bloqueiam a interpretação de remoção concluída.
- Ausência hipotética em inventário inventado habilita **somente investigação**.

**Para fechar R7:** teste real controlado de desinstalação de aplicativo
descartável, backup e comprovação de que dados do usuário e outras
instalações foram preservados. Um snapshot fictício não substitui isso.

### 3. Resíduos — política de R4, sem exclusão

Código: `TestesResiduos/SimulatedResidualScenarioTests.cs`.

- Simular 11 falhas independentes: inventário parcial, instalador/auxiliares
  ativos, arquivo em uso, propriedade não exclusiva, política não explícita,
  ausência de consentimento, snapshot alterado, dados pessoais/compartilhados,
  serviços/integrações não verificados e reinstalação.
- Mesmo quando todas as condições de uma **simulação** são atendidas,
  `ResidualDecision.CanExecute` continua `false`.
- Validar que os pontos de entrada diretos de exclusão continuam com zero
  arquivos e pastas removidos, mesmo recebendo allowlist simulada.

**Para fechar R4:** não basta testar em computador real sem excluir.
Exige prova de exclusividade, revalidação do mesmo conteúdo e identidade,
consentimento exato, testes de segurança e uma operação real separadamente
autorizada em ambiente descartável. Até lá a exclusão fica desativada.

## Execução e entrega

CI oficial Windows (`.github/workflows/validar-candidata-windows.yml`):
1. `dotnet run --project TestesAtualizador/TestesAtualizador.csproj -c Release`
2. `dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release`

Esses comandos são para CI/desenvolvimento com SDK preinstalado; **não**
são etapas exigidas ao usuário final. As classes de testes são compiladas
automaticamente pelos projetos existentes. Na suíte de resíduos, a chamada
ocorre explicitamente na ordem desinstalação→resíduos.

### Evidências de checkpoint

Enquanto não ocorrer ação real confirmada:
- U8 — pendente (instalação e versão final no computador).
- R7 — pendente (desinstalação real e preservação de dados).
- R4 — suspenso (exclusão real requer evidência e consentimento).
- Roadmap global: **27/42 (64,3%)**. Não declarar U8/R7/R4
  concluídos porque passaram simulações.

PR #40 deve continuar Draft e `main` inalterada. Não disparar
instaladores oficiais ou testes destrutivos em Actions de PR.
