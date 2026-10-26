# Regressões do GERENCIADOR FINAL

WORK18 (.65): executar também `dotnet run --project TestesAtualizador -c Release -- --work18`,
`dotnet run --project TestesLayout -c Release -- --work18` e `python scripts/measure_work18.py`.
As 28 fixtures comparam o mesmo cenário contra a integração .64 imutável; cinco mutações adicionais
verificam os novos contratos. [Matriz, evidências e pendências](MISSAO-WORK18.md).
Nenhum teste instala programas nos PCs FORTE/FRACO; elegibilidade anterior ao payload não é autorização final.

Este é o procedimento técnico da candidata integrada .64, com [CI Windows aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40241712164)
e ZIP verificado externamente; evidências em [MISSAO-INTEGRACAO](MISSAO-INTEGRACAO.md).
A aprovação é técnica e não substitui homologação AVG ou testes nos PCs. CI e dados sintéticos não
substituem homologação nos PCs. O [roteiro físico mínimo](TESTE-CONSOLIDADO.md)
é separado dos testes automatizados.

## Ambiente e execução

Windows x64 com SDK indicado em `global.json`. O usuário final não precisa de
SDK. Os projetos de regressão são executáveis de console: `dotnet test` sozinho
não executa estas asserções. Na raiz do checkout:

```powershell
dotnet build GerenciadorAplicativos.csproj -c Release -p:RunAnalyzers=true
python -B -m unittest discover -s scripts -p 'test_p*.py' -v
```

Executar `dotnet run --project PASTA/PASTA.csproj -c Release` para cada projeto
obrigatório abaixo. A CI mantém estes comandos explícitos para que o guardrail
confira se algum projeto ficou sem caminho de execução.

| Projeto | Contratos verificados |
| --- | --- |
| TestesSeguranca | WSC, arquivos temporários sintéticos, DLLs nativas e fronteiras de execução |
| TestesMotor | Versões, identidade, ambiguidades e matching |
| TestesCatalogo | RuckZuck simulado, cache e descarte |
| TestesMultifontes | Fontes oficiais simuladas, conflitos, duplicidade e histórico |
| TestesAtualizador | Receitas, hashes, assinaturas, MSI, processos, cancelamento e pós-confirmação |
| TestesDrivers | Inventário/histórico consultivo; nenhum reparo de driver |
| TestesResiduos | Desinstalação simulada e alvos descartáveis de arquivos/Registro, HKCU/HKLM e visões 32/64, revalidação e preservação |
| TestesLayout | WinForms, navegação tardia, inventário, RAM, DPI, resoluções e capturas sintéticas |
| TestesMsix | Falhas COM, retry limitado, fotografia parcial e cancelamento |
| TestesTelemetria | Consentimento, expiração, esquema e privacidade local |
| TestesFechamento | Bloqueios, encerramento seguro, concorrência e cancelamento |

Os 42 controles WORK9 e as prévias WORK6 já integram as suítes completas; os
modos `--work9` e `--work6-preview` permanecem disponíveis para reproduções
focadas. Não somar essas execuções novamente ao total de cobertura.

```powershell
python scripts/test_work6_mutations.py
node --test prototipos/telemetria-worker/test.mjs prototipos/telemetria-worker/staging-receiver.test.mjs
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release --no-build --no-restore -- --benchmark
```

Mutações ocorrem em cópias descartáveis; erro de compilação não conta como
mutação rejeitada. O benchmark usa dados sintéticos e não mede velocidade de
disco nem RAM residente. [Método comparativo WORK9](MISSAO-WORK9-PARTE1.md).

## Pacote e integrações externas

Somente depois das regressões e políticas: publicar win-x64 self-contained,
validar o manifesto pelo `TestesManifesto/Testar-Ativacao.ps1`, gerar
BUILD-INFO/SHA256SUMS e executar `scripts/verificar_pacote.py` com versão e
SHAs esperados. A conferência independente exige cobertura exata de hashes,
arquitetura AMD64, runtime incorporado, origem e avisos de licença. Python é
uma ferramenta de auditoria/CI, não dependência do aplicativo distribuído.

Nas PRs, catálogo Microsoft autenticado e downloads oficiais com hash, identidade,
assinatura e motor são **obrigatórios**, sem executar instaladores. Os dois jobs
`drivers-leitura` consultam SetupAPI/WUA/INFs dos runners Windows 2022 e 2025,
sem instalar/remover drivers. `TestesRede` e a consulta ampliada `--live` continuam
opt-in por `workflow_dispatch`. Um job skipped não comprova sua execução.

Modos explícitos adicionais, sem somar asserções duplicadas ao total:

```powershell
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --lote4
./scripts/Testar-Regressao-Relatorio.ps1
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --drivers-readonly
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --lote3
dotnet run --project TestesAtualizador/TestesAtualizador.csproj -c Release -- --lote3
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release -- --lote3
```

O controle negativo Lote 4 usa cópia descartável com `Program.cs` anterior.
Só passa se compilar, abrir o diálogo e recusar especificamente a rolagem
nativa; depois restaura o diálogo atual e exige recusa da sobreposição usando
DarkControls anterior. Falha genérica, timeout ou compilação quebrada não contam. O teste
positivo exige a marca final de execução, texto completo, ausência de
identificador privado, rolagem escura e fechamento. Não usa o seletor Salvar.
Não executar testes sintéticos destrutivos nos PCs pessoais.

## Resultados e limites

Resultados de cada revisão, falhas anteriores e evidências ficam nos relatórios
[WORK9 parte 1](MISSAO-WORK9-PARTE1.md) e [parte 2](MISSAO-WORK9-PARTE2.md).
A regressão principal usa Windows Server 2022, e os jobs de drivers também
Windows Server 2025: não comprova execução física no PC FORTE/FRACO,
compatibilidade universal do Windows 10/11 nem aprovação pelo AVG. O tema do
conteúdo é escuro; decoração nativa e todos os diálogos ainda exigem revisão
visual em Windows de uso real. U8/R7/R4 seguem pendentes no roadmap.

<details>
<summary>Arquivo de roteiros e resultados anteriores — não vigente</summary>

## Política de liberdade na desinstalação (22/10/2026)

O usuário decide desinstalar aplicativos Win32 comuns, independentemente do perfil de testes. Ausência comprovada de assinatura Authenticode **não bloqueia** o desinstalador registrado; a interface apresenta advertência explícita e solicita consentimento. Cadeia inválida, assinatura alterada, editor divergente, arquivo não-PE, host de sistema disfarçado, caminho remoto/relativo/reparse e comando ambíguo continuam impedidos de execução **por este fluxo** (não significam que o usuário está proibido de remover o aplicativo por outra rota oficial). A lista de proteção de componentes do Windows e as limitações MSIX são mantidas para revisão separada. Não automatizar exclusão de resíduos pessoais.

## Implementacao inspirada em monitoramento de desinstaladores (22/10/2026)

Manual oficial IObit descreve *Install Monitor* que registra criacoes de arquivos e Registro durante instalacao, e varredura de sobras feita apos uma desinstalacao, inclusive de outro desinstalador. Como GF e portatil, sem servico persistente, nao tem log de instalacoes anteriores. Implementada uma correlacao historica limitada a Notepad++ e foobar2000, usando familias de perfis documentadas (inclusive AppData/foobar2000-v2) e indices de referencias Windows pelos nomes dos executaveis; nunca le dados nem apaga chaves compartilhadas. A cobertura e inferior ao monitor que estava rodando antes da instalacao.

## Evidências PC FORTE .38 — 22/10/2026, U8/R7/R4 não encerrados

- Inventário completo 174 aplicativos/27 MSIX; 8 possíveis atualizações, 4 incertas, 14/15 fontes disponíveis; uma fonte externa com timeout. Não houve instalação supervisionada de atualização; **U8 pendente**.
- Desinstalação real observada: `StillPresent codigo=0` às 14:51:46 e inventário 174 logo depois; diagnóstico posterior mostrou 173, sem identidade no log. Não se deve inferir que a redução foi obrigatoriamente causada pela ação. Ajustado estado para **Pending** após consultas rápidas quando o fornecedor termina com código zero mas o registro continua. Não repetir silenciosamente nem declarar sucesso. **R7 pendente**.
- Duas preparações bloqueadas por `CryptographicException` (assinatura/identidade do desinstalador), sem execução: **não desabilitar Authenticode** para contornar.
- IObit mostrou 6 itens Registro + 11 itens arquivos atribuídos a Notepad++ (somente imagem, não confirmados por auditoria do GF). Novo histórico consultivo identifica pastas exatas `AppData\\Roaming\\Notepad++`, `AppData\\Local\\Notepad++`, `ProgramData\\Notepad++` e chaves específicas `SOFTWARE\\Notepad++`; preserva qualquer dado e ignora chaves compartilhadas UFH do Windows. `R4` continua suspenso sem exclusão.
- Resíduos gerais 45 grupos/164 locais, 864 grupos examinados, 44 candidatos amplos, 608 com evidência insuficiente, 212 protegidos. Análise parcial por limites de entradas/profundidade, links redirecionados e leituras AppData/ProgramData.
- Recuperação de informações dos componentes e logs de usuário são privados. Não registrar nomes de apps ou caminhos nos diagnósticos automatizados.
- Próximo ciclo significativo: verificar identidade pós-desinstalação em **duas consultas completas** e se ainda restam configurações do aplicativo; repetir U8 com app descartável atualizado pelo próprio GF sob autorização. R4 exige executor seguro em ambiente isolado e aprovação própria; não usar IObit como prova de exclusividade.

## Lote funcional em desenvolvimento — sem nova build para teste físico (22/10/2026)

* **Windows**: dividir "Verificar Windows" (DISM ScanHealth + SFC verifyonly) e "Reparar Windows" (RestoreHealth + scannow). Reparação só habilita quando o texto explicitamente reconhecer corrupção nesta sessão; resultados inconclusivos não habilitam.
* **Restauração**: "Criar ponto" permanece; "Escolher ponto de restauração" abre exclusivamente rstrui.exe oficial após confirmação, sem escolher/aplicar ponto automaticamente.
* **Responsividade**: permitir pesquisa local e navegação com DISM/SFC ativo; ações conflitantes seguem protegidas por _busy. Progresso deriva dos percentuais reais das etapas; não fingir progresso quando a ferramenta não fornece valores.
* **Discos**: otimização /O de uma unidade selecionada em cada card, revalidada no worker elevado. Opção global eliminada, letras arbitrárias/injeção rejeitadas.
* **MSIX**: um retry limitado após pausa curta só para HRESULTs transitórios; manter a fotografia anterior como "presença não confirmada", sem habilitar receitas. Incluir regressão completa→falha 0x800703FA→recuperação completa.
* **Atualização, desinstalação e resíduos**: regressões existentes devem rodar completas, sem operações reais sobre usuário. Não mudar bloqueio da exclusão de resíduos.
* **Critério de saída**: build, regressões e política CI verdes + auditoria do diff; só então consolidar lote e solicitar bateria única no PC FRACO. Nada de microtestes e nada de aumentar 27/42 antes da validação.

# Roteiro de testes — v5.13.11.38 TESTE

## Contrato responsivo — aba Inicialização
- A largura de cada card depende da largura real do painel e DPI, nunca exclusivamente de 1440×900. Grade usa 1, 2, 3 ou mais colunas segundo espaço; não cortar ações nem provocar scroll horizontal.
- Validar início de 720p (1280×720), 1366×768, 1440×900, 1600×900, 1920×1080, 2560×1440, ultrawide e 4K; testar escala 100%, 125%, 150%, 200% em CI.
- O painel deve redistribuir automaticamente ao redimensionar a janela ou alterar o DPI/monitor; barras verticais são permitidas quando a lista de entradas excede a altura. Preservar nomes completos nas dicas e descrições acessíveis.
- No PC FRACO: tirar captura da Inicialização com 18 itens, confirmar uso equilibrado da largura e funcionamento da rolagem, sem modificar entradas.
- O log da .37 confirma botao Drivers: cancelamento solicitado antes de cancelamento efetivo e consulta posterior 117 associações. Resíduos 12 grupos/41 locais, status parcial. MSIX continua com falha 0x800703FA.
- A .37 também executou limpadores de temporários, cache Windows Update, limpeza de disco e shader cache. **Essas operações reais não são necessárias na próxima bateria**, limitar à consulta.
- Política segura inalterada: nenhum reparo de drivers, desinstalação ou exclusão de resíduos em teste.



## PC FRACO: critérios antes de avançar
- Todos: lista completa, possíveis atualizações primeiro e demais em ordem alfabética; entradas somente leitura sem ações perigosas. Sem botões Possíveis/Inventário; manter Marcados.
- Atualizar e verificar: ao terminar ou cancelar, não selecionar somente os candidatos. Busca textual permanece valendo.
- Drivers: repetir consulta e cancelar próximo da conclusão; o botão não deve ficar travado em Cancelando, nem desativado permanentemente. Leitura WMI nativa ainda viva bloqueia apenas outra consulta.
- Inicialização e sistema > Inicialização: preencher horizontalmente as três colunas sem excesso de faixa vazia à direita. Verificar DPI alto e resoluções menores; nenhuma edição real.
- Resíduos: baseline 12 grupos e 41 locais com análise parcial; exclusão não autorizada.
- MSIX: 0x800703FA persistente na .36; não declarar leitura completa. Nenhuma operação real de atualização, desinstalação ou driver.

Roadmap permanece 27/42 até validação física e evidências.


## Bateria prioritária no PC FRACO, 1440×900
1. Extrair a nova candidata em pasta separada e confirmar versionamento, integridade, AVG e ausência de alertas; não criar exceções no antivírus.
2. Aplicativos: pressionar **Atualizar e verificar**, observar inventário Win32/MSIX antes de consultas HTTPS e confirmação dos candidatos; cancelar consulta online e garantir que não publica resultado atrasado.
3. MSIX: comparar com baseline completo 74 apps/19 MSIX. A .35 falhou com 0x80040154 e 0x800703FA (55 apps). A .36 repete uma única vez também 0x800703FA, mas não repara a origem Windows; falha persistente deve marcar PARCIAL e preservar restrições.
4. Drivers: consultar normalmente e realizar nova consulta, pressionando **Cancelar** durante leitura. A lista anterior deve permanecer; a resposta nativa tardia não pode substituí-la, nem permitir varreduras concorrentes.
5. Limpeza e desempenho: com Windows em 1440×900 e 100%, verificar quatro cartões por linha (duas fileiras) e dados de disco sem rolagem desnecessária. DPI alto e telas pequenas podem exigir rolagem, nunca corte de descrições.
6. Resíduos: somente prévia, comparar 12 grupos/41 locais e transparência dos motivos de análise parcial; nenhuma remoção.
7. Registrar duração, memória, fechamento e diagnóstico local. Não instalar, desinstalar, limpar cache, atualizar Windows ou manipular drivers nesta bateria.

Critérios: sem mutação do Windows, consultas canceladas não modificam listas nem históricos e fontes parciais não são declaradas completas. **Roadmap permanece 27/42**; U8, R7, R4 e G1–G5 pendentes.



## Próxima bateria: primeiro contato .35 no PC FORTE

As simulações WORK 6 e as três Actions passaram, mas a .35 **não** foi executada ainda no PC do usuário. Em primeira sessão, manter AVG ativo, selecionar perfil PC FORTE, verificar inventário, consultar atualizações e navegar pelas telas; não instalar, desinstalar ou excluir resíduos. Preservar diagnóstico local e encaminhar privadamente se houver diferença importante, UAC inesperado ou bloqueio AVG. O teste de instalação real (U8) e de desinstalação (R7) só deverá ocorrer separadamente, em ambiente descartável/autorizado, e R4 permanece proibido nesta candidata.

[Resultado da MISSÃO WORK 6, evidências e critérios](MISSAO-WORK6-RESULTADO.md).

## Avanço validado — U6 encerrado, 27/42 (64,3%)

A comparação real do PC FORTE mostrou nove entradas em `winget upgrade`
e oito no Gerenciador Final .34, das quais duas exigiam revisão.
**Reconciliação completa dos nove produtos**, sem afirmar que sejam
instaláveis: seis candidatos consultivos sem conflito documentado
(7-Zip, EA app, FanControl, Jellyfin Server, K-Lite Mega e Outlook),
dois casos com fontes/versionamento em revisão (Plex Media Server e
Proton Pass) e um item propositalmente somente inventário
(Microsoft GameInput, componente protegido do Windows).

Fontes oficiais e catálogos HTTP(S), sem executar WinGet COM/CLI;
identidades do servidor Jellyfin e Plex protegidas contra confusão
com players. Fixture automatizada de nove produtos e cenário de
GameInput protegido na suíte multifontes, CI Windows aprovada:
[regressões](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959079),
[build](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959083),
[política](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39698959189).
Código de teste aprovado: `5067a419`. Essa aprovação encerra
**U6 (explicar divergências de cobertura)**, e não U8 (instalação
real), G1–G5 ou validação antivírus. Sem nova build/versão,
sem novas dependências ou botões.

**Diretriz permanente de produto:** o usuário decide se inicia
o executável como administrador. Não criar botão administrativo,
não forçar elevação nem alterar o manifesto padrão. Uma possível
recomendação de uso elevado só será avaliada no final do projeto,
se um benefício material for comprovado. A comparação A/B feita
não aumentou a cobertura de aplicativos nem de atualizações.


## Resultado da comparação normal × administrador no PC FORTE (.34)

Teste real com o mesmo pacote **v5.13.11.34 TESTE**, em sessões
separadas. Diagnóstico da segunda sessão confirmou
`TESTES.WINGET_CONTEXTO ... elevado=1` (normal anterior:
`elevado=0`). O provedor WinGet COM/CLI permaneceu **desativado**;
`path=0` no contexto normal e `path=1` no elevado é diferença do
ambiente PATH, não prova de WinGet habilitado ou da necessidade de UAC.

**Comparação com fontes completas:**
- Inventário: **174 vs 174**, com 292 registros examinados, 140
  desinstaláveis, 27 MSIX, zero erros e zero itens MSIX retidos.
- Atualizações: **8 vs 8** candidatas, das quais 2 em revisão,
  **15/15 fontes OK** nas primeiras consultas. Nenhuma instalada.
- Drivers: 178 associações WMI, 63 INFs, 176 PnP e três associações
  sem INF em ambos; nenhuma operação de driver.
- Resíduos gerais: consulta normal examinou **3.925** entradas,
  44 candidatos, duas falhas parciais de leitura em AppData/ProgramData,
  867 grupos avaliados (212 protegidos, 611 sem evidência).
  Consulta elevada examinou **3.929** entradas, 44 candidatos,
  **zero falhas de leitura**, 871 grupos avaliados (212 protegidos,
  615 sem evidência). Contagem final de 45 grupos/164 locais mantida.
- Consulta elevada ainda **parcial** devido a EntryLimit,
  DepthLimit e LinkSkipped; limites de segurança intactos.
- Encerramento normal; nenhum alerta AVG documentado nas capturas
  enviadas. Sem excluir resíduos nem operar drivers.

**Interpretação restrita:** a elevação coincidiu com melhoria de
acessibilidade na leitura de diretórios, mas uma rodada A/B não prova
que as duas falhas anteriores decorreram apenas de ACL/UAC; arquivos
em uso e alterações transitórias são explicações alternativas.
**Elevação não ampliou aplicativos nem atualizações** no PC FORTE
e não inspecionou automaticamente perfis de outros usuários.

Próximo passo: manter execução padrão não elevada, decidir sobre
futura opção consultiva `Diagnóstico aprofundado` com UAC
voluntário e privilégios mínimos, preservando o processo principal
sem elevação. Se útil, repetir A/B com fontes OK e mesma conta.
Não implementar limpeza/exclusão elevada nem enfraquecer proteção
de resíduos para incrementar estatísticas. Roadmap segue **26/42**.
Não publicar logs privados/caminhos de usuário no repositório.

## Validação física .34 no PC FORTE e teste A/B de privilégios (21/10/2026)

Capturas e diagnóstico privado `.34` confirmaram **174 aplicativos,
27 MSIX, 140 com desinstalador, 34 sem**, zero erros de inventário;
consulta inicial **8 candidatos (2 em revisão)** com **15/15 fontes
HTTPS OK**. Jellyfin Server versionado reconhecido e Media Player
separado, Plex Media Server versionado reconhecido e Plex player
separado. O Plex Server permaneceu em revisão de fontes; instalação
direta não foi autorizada. Nenhuma instalação efetuada.

**Oscilação de rede/consulta:** nova atualização mostrou 6 candidatos
com 8/15 fontes OK; depois 2 candidatos com 4/15 fontes OK e 11
indisponíveis, sem mudança no inventário (174). Erros de rede
`HttpRequestException` apresentados em diversas fontes. A contagem
menor em consulta parcial **não representa desaparecimento de
atualizações nem prova de bloqueio AVG**. Não concluir comparação
de privilégios utilizando resultados de fontes degradadas.

Contexto consultivo da sessão: `TESTES.WINGET_CONTEXTO pacote=1
alias=1 path=0 elevado=0 parcial=0`; executor WinGet COM/CLI
permanece desligado. Drivers 178 associações/63 INFs; resíduos 45
grupos/164 locais, parciais, sem exclusão. Histórico PnP sem
novos/ausentes/alterados. Encerramento normal. Nenhuma
validação específica do PC FRACO nesta sessão.

**Experimento A/B administrativo solicitado pelo usuário:**
não criar outra build nem modificar manifestação ou privilégio padrão.
Usar **exatamente a mesma pasta .34** e rodar uma sessão normal,
fechar completamente, e uma sessão `Executar como administrador`
somente com aceite UAC manual. Manter AVG ativo, não abrir shell
nem executar WinGet, não instalar/atualizar/desinstalar, não limpar,
não inspecionar conteúdo privado ou gerenciar drivers. Em ambas:
ativar `Teste PC FORTE`, `Atualizar lista`,
`Verificar atualizações`, conferir conclusão com fontes OK, encerrar
normalmente e exportar os dois logs **privadamente**. Não anexar
logs/caminhos privados ao repositório. Em sessão elevada esperar
`elevado=1`; no perfil normal `elevado=0`.

Comparar: `quantidade`, `examinados`, `erros`, `MSIX
aplicativos/falhas`, `desinstalaveis`, `candidatos`,
`fontes_ok/consultadas`, `incertos`, `parcial`, tempo e
presença do pacote/alias. A elevação não acessa automaticamente
todos os usuários, pois ARP lê HKLM + HKCU e MSIX é do usuário
ativo; se UAC exigir credenciais de **outro usuário**, HKCU/MSIX
podem mudar e o teste não é diretamente comparável.
Sem alterações no código, WinGet continua desligado.
Checkpoint U6 e demais etapas do roadmap permanecem abertos.
Não ativar execução administrativa como padrão nem reduzir
validação de identidade.

## Bateria de correção Jellyfin/Plex no PC FORTE (.34, CI aprovada)

Build oficial de testes [v5.13.11.34 (artefato GitHub Actions)](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660841/artifacts/12390097297).
Código aprovado: `9c411fba3177c111888d5fc7a62b4bd0b3211511`.
[Regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660843)
(**130/130 motores multifontes**) e
[segurança/privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660928)
aprovadas. Uma revisão inicial da .34 falhou o teste canônico de Plex
e não deve ser testada; o pacote acima vem da revisão corrigida.
Confira BUILD-INFO e SHA256SUMS no artefato portátil, sem mesclar pastas.
Retenção GitHub indicada até 28/10/2026. Executável sem assinatura,
sem aprovação real AVG e sem elevação/WinGet real.

Capturas da .33 mostraram Jellyfin Media Player separado do Jellyfin
Server 12.1.0, Plex player 1.115.0 separado do Plex Media Server
1.43.4.10903 (x64), GameInput fora de Todos (somente inventário) e
divergências Proton 1.43.0 changelog/1.42.0 Windows. A .33 registrou
cancelamento de consulta em cerca de 0,6 s e encerramento normal.

Na .34, verificar **somente leitura**: Jellyfin Server reconhecido
com versão no nome, Plex Media Server reconhecido com sufixo (x64),
sem sugerir atualização a Jellyfin Media Player nem ao Plex player.
Versão mais nova não é prova de compatibilidade de instalador.
A UI deve mostrar número de candidatas e quantas exigem revisão.
Não alterar GameInput, Proton, MSIX, WinGet e políticas.
Preservar AVG e comparar com .33 após passar a CI, se for seguro.
PC FRACO adiado pelo usuário; não assumir testes nesse ambiente.

## Histórico das sessões anteriores

## Primeira bateria real .33 — PC FORTE (21/10/2026)

Evidências enviadas em sessão privada: seis capturas e diagnóstico local da .33;
**não** anexar arquivo pessoal bruto, nomes de pastas de usuário nem paths do teste
ao GitHub. Diagnóstico apresenta encerramento normal, sem erro fatal observado,
sem uso de executor WinGet COM/CLI ou instalação, sem alteração de drivers
e sem exclusão de resíduos. AVG não exibiu alerta nas capturas; aprovação
antivírus universal **não** comprovada.

**Estabilidade:** 174 aplicativos (140 com desinstalador/34 sem), 27 MSIX,
inventário completo; WinGet contexto `pacote=1 alias=1 path=0 elevado=0 parcial=0`.
Alias/PATH não comprovam capacidade da API nem exigência de administrador.
178 associações WMI/63 INFs, 3 associações sem INF válido, 176 PnP,
nenhuma diferença entre leituras. Resíduos 45 grupos/164 locais,
44 candidatos gerais, 212 grupos protegidos, 611 sem evidência,
3.925 entradas na descoberta geral, cobertura parcial por limite de entradas,
profundidade, links e leitura em AppData/ProgramData. Nenhuma remoção.

**Atualizações:** .32 tinha 3 candidatas; a .33 observou **6** em 174
instalações; 13/13 fontes HTTPS reconhecidas e OK, 631 entradas no RuckZuck,
103 sem correspondência de catálogo, 31 inventário apenas e 4 incertos.
Tempo da etapa de busca medido: 5.726s na abertura, 1.775s após
renovação (cache/fatores de rede não isolados). Oito fontes do projeto
antes, treze agora; não existe executor WinGet real.
Um resultado com **divergência entre fontes**; `IsUpdate` ainda conta
`UpdateRequiresReview`, logo a UI inclui o caso conflitante nas seis
candidatas; `VerifiedUpdaters.Resolve` bloqueia receita direta
e `GetPrimaryAction` apresenta somente Store/site/orientação.

**Códigos consultivos novos (somente números, definidos no código):**
- `fonte=1 etapa=9`, `fonte=2 etapa=9`, `fonte=4 etapa=9`:
  novas candidatas reconhecidas por fontes HTTPS, conservando ações
  de site/orientação e sem autorizar instalador genérico.
- `fonte=3 etapa=0`: identidade canônica não reconhecida no
  snapshot; **não comprova ausência do produto**.
- `fonte=5 etapa=2`: entrada reconhecida mas inelegível para
  atualização por proteção de inventário; não liberar automaticamente.
- `fonte=6 etapa=3`: condição de nome/editor/canal/identidade
  não satisfeita; causa específica ainda não comprovada sem
  verificação local das propriedades originais.
- `fontes=7/8/9 etapa=7`: conflito entre proveniências do mesmo
  produto; manter versão de feed Windows e outras observações
  discriminadas, sem instalação direta.

**Pendências objetivas:** desambiguar visualmente
`candidatas totais` de `candidatas que exigem revisão`; apresentar
na própria UI o motivo seguro de inelegibilidade sem divulgar dados
privados; conferir identidade exata do candidato fonte 3 e a
condição estrita fonte 6, sem enfraquecer proteções ou instalar
WinGet. Ainda não há teste físico da .33 no PC FRACO,
medição completa de AVG/CPU durante várias sessões nem
instalação verificada; U6 e demais checkpoints de validação
continuam abertos, roadmap **26/42 (61,9%)**.


## Próxima bateria .33 — somente consultas

1. PC FORTE: extrair pacote .33 validado em pasta nova e conferir BUILD-INFO/SHA256SUMS.
   Manter AVG ativo. Abrir normalmente, registrar startup/RAM/CPU/processos/arquivos novos.
   A versão não inclui executor WinGet e não pede UAC para pesquisar.
2. Escolher Teste PC FORTE em Limpeza e desempenho. Inspeção de contexto apenas de leitura:
   App Installer/alias/PATH do usuário atual; não executa WinGet nem instala pré-requisitos.
3. Verificar atualizações; observar barra por fontes concluídas, Cancelar consulta,
   falha isolada e estado parcial. Repetir; mudar lista; confirmar que não há flash ou reinício.
4. Apontar para cada candidata: conferir as versões/origens na dica. Proton prioriza feed Windows e pode mostrar
   divergência com changelog; FanControl aceita versão inteira; Jellyfin de versão principal diferente
   requer revisão. Novos produtos oferecem consulta/site/orientação, nunca instalador genérico.
5. Registrar somente contagens e códigos COBERTURA_ATUALIZACAO, especialmente fonte 5
   (GameInput); não compartilhar inventário bruto. Etapa 0 significa identidade não reconhecida,
   não ausência comprovada de instalação. Legenda completa em [MISSÃO WORK 5](MISSAO-WORK5.md).
6. Sem rede: inventário utilizável, fontes indisponíveis explícitas e nenhuma instalação.
   Fechar/reabrir, minimizar/restaurar e comparar fontes/RAM/CPU com baseline .32.
7. Se houver alerta, janela inesperada, UAC, dependência ou fechamento: parar, guardar evento
   e hash. Não criar exceções nem tentar reparar WinGet. A consulta real COM/CLI está bloqueada.
8. Somente depois, repetir consultas no PC FRACO, 1440×900; incluir inicialização/MSIX,
   DPI real, cancelamento e bandeja. Comparar com .24 sem presumir que ausências são remoções.

Não atualizar/desinstalar aplicativos, não limpar resíduos/Lixeira nem alterar drivers nesta bateria.
UI automatizada usa o runner; não substitui monitores/DPI/AVG reais. Zero checkpoints novos.

## Comparação presencial com WinGet — PC FORTE, 21/10/2026

Consulta **somente leitura** `winget upgrade` em PowerShell elevado mostrou
**9 atualizações candidatas**, enquanto o GF .32 mostrou **3** na mesma
máquina. Duas versões-alvo coincidiram; a terceira divergiu:
GF detectou uma revisão recém-anunciada pelo fornecedor, enquanto o
manifesto comunitário WinGet ainda mostrava a revisão anterior.
O changelog oficial recebeu a revisão mais nova em 21/10/2026 às
16h UTC, conforme commit
[`9ebebbc`](https://github.com/ProtonMail/WebClients/commit/9ebebbc88e8b3b9652d4b5bbfebfd8e2d3538966).
Isso valida a detecção por fonte oficial, **não** a disponibilidade
comprovada do instalador Windows nem a segurança de instalação.

Das **6 candidatas adicionais** do WinGet, cinco produtos não têm
receita correspondente nas fontes oficiais fixas atuais do GF;
um produto de sistema tem receita consultiva, mas a razão exata de
não se tornar candidato deve ser apurada com vínculo de
nome/editor/versão/eligibilidade e proteção de mudança de versão
principal. Não atribuir causa exata com base só em totais agregados.
RuckZuck e fontes fixas têm escopo menor e só aceitam correspondências
conservadoras de identidade. Sem execução de atualização, download,
alteração do Windows ou ampliação automática de privilégios.

A execução de `winget` no PowerShell não elevado falhou por comando não
encontrado, mas funcionou no terminal elevado. Possível divergência
de App Execution Alias/PATH/registro do App Installer por contexto; **não
é prova de que o WinGet exige administrador**. Não reinstalar
pré-requisitos apenas por este diagnóstico.

A Lixeira nativa continuava mostrando dois itens da pasta/arquivo
de teste da versão .32 após o retorno
`0x8000FFFF` observado em `SHEmptyRecycleBin`. Isto comprova que
a Lixeira **não ficou vazia**; não comprova se houve exclusão parcial
anterior. Não repetir esvaziamento, não apagar `$Recycle.Bin`
manualmente e não inspecionar conteúdo privado.

**U6:** comparação real foi obtida e os limites arquiteturais
ficaram explícitos. Permanecem pendentes a classificação individual
das seis diferenças e a verificação de origem/canal de instaladores.
Checkpoint continua aberto; roadmap **26/42**. O próximo passo é
melhorar a classificação consultiva e seus testes sem habilitar
instalações automáticas.


## Validação presencial .32 — PC FORTE, sessão 21/10/2026

**Aprovado nesta sessão:** 174 aplicativos/27 MSIX sem falhas, 140 entradas
com ação, três candidatas de atualização, oito fontes sem falha,
178 associações de drivers, 63 INFs mapeados, nenhuma alteração no
histórico de drivers (nas leituras sucessivas), 176 dispositivos PnP
consultados, encerramento normal e sem eventos de exclusão de drivers.

**Resíduos:** 45 grupos/164 locais e 44 candidatos gerais após 867 grupos
examinados. 212 protegidos, 611 sem evidência suficiente, 3.925 entradas
nas origens gerais, cobertura parcial de AppData e ProgramData e limites
de profundidade, entradas e redirecionamentos. A inspeção de dados
da Aquiris foi preservada; nenhuma exclusão de resíduos realizada.

**Auditoria de limpezas comprovada no PC FORTE:**
- `LIMPEZA.INICIO tipo=temporarios_usuario` e `LIMPEZA.RESUMO`
  com zero arquivos/pastas removidos, 30 itens preservados, zero links e falhas.
- `LIMPEZA.PROCESSO_RESULTADO tipo=temporarios_windows codigo=0`
  — resultado do worker sem contagem individual de arquivos.
- `LIMPEZA.PROCESSO_RESULTADO tipo=limpeza_disco_windows codigo=0`
  — não implica bytes recuperados.
- `RAM.CACHE_INTERNO modo=manual bytes_descartados=0`.
- Os eventos estruturados não contêm caminhos nem nomes de arquivos.

**Ocorrência aberta, não resolvida:** a interface mostrou
`O Windows retornou código -2147418113` após a chamada para esvaziar
a Lixeira, equivalente ao HRESULT `0x8000FFFF` (`E_UNEXPECTED`)
de `SHEmptyRecycleBin` no código atual. É operação separada
de `cleanmgr`. Não foi registrada no arquivo local de diagnóstico
da .32; resultado final da Lixeira não comprovado. **Não repetir
a exclusão nem apagar a pasta do sistema da Lixeira para investigar.**
Inspeção manual da Lixeira do Windows sem exclusão, seguida de
diagnóstico específico de estado e erro em versão futura.
Fonte oficial: documentação Microsoft de SHEmptyRecycleBinW/HRESULT.

**Continuidade:** confrontar 7-Zip/Outlook/Proton Pass com fontes oficiais
e WinGet (somente listagem), validar os três dispositivos sem nome
sem instalar drivers e preparar programa de teste não essencial para
atualização supervisionada, com backup e critério de confirmação.
Nenhum checkpoint adicional foi fechado: **26/42**.

## Build da .32 e CI

Artefato portátil não assinado [v5.13.11.32 TESTE](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829661/artifacts/12384491029),
código `ea8530d`. Confira BUILD-INFO e SHA256SUMS; retenção informada
até 28/10/2026. Aprovados: [compilação](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829661),
[regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829530)
e [política de privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829538).
A primeira tentativa de compilação falhou por colisão de nomes locais,
corrigida na revisão aprovada. CI não substitui execução nos PCs reais.

## Resultados da segunda bateria PC FORTE (.31) e objetivo da .32

A .31 confirmou 174 programas, 27 MSIX, 3 possíveis atualizações,
178 associações WMI, 63/63 INFs consultados no Driver Store,
45 grupos e 164 locais de resíduos **parciais**. A UI informou
47 arquivos/pastas removidos dos temporários, 17 preservados e
zero falhas, e retornou código 0 de Limpeza de Disco; esses eventos
não constaram no diagnóstico persistente. A .32 acrescenta trilha
local agregada e estrita, sem mudar os critérios de limpeza.

Não repetir limpeza só para testar logging. A validação de formato fica
na CI e pode ser verificada quando houver outra limpeza genuinamente
necessária. Nunca enviar dumps da Steam, paths sensíveis ou arquivos
pessoais; Steam/dumps permanece somente leitura e protegido.
O filtro Todos do GF exibe aplicativos com ação; Inventário mostra
também entradas sem ação. Visualizar no Driver tab a distinção entre
presença indeterminada e assinatura informada pelo Windows.

## Histórico da bateria .31


21/10/2026 · PR #40 Draft · Roadmap **26/42 (61,9%)**.
Código `de6ee62` aprovado nas três Actions. [Pacote escolhido e hashes](OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido). Este arquivo é
um procedimento pendente de campo, não um relatório de testes presenciais.
Resultados .30 do PC FORTE e matriz de achados: [revisão consolidada](OPERACAO-FAXINA-FINAL.md).
Testes anteriores permanecem no CHANGELOG, documentos históricos e histórico Git.

## Preparar os dois computadores

PC FORTE: 2560×1440, DPI real 120/125%. PC FRACO: 1440×900, escala a confirmar.
Começar no PC FORTE, refinar resíduos e repetir a bateria consultiva no PC FRACO.

1. Baixar o ZIP x64 da execução aprovada indicada no PR. Extrair a pasta inteira;
   runtime incluído, sem instalar SDK ou misturar arquivos de outras candidatas.
2. Conferir `BUILD-INFO.txt` e `SHA256SUMS.txt`, versão .31, SHA fonte e merge
   sintético. Registrar qual artefato foi usado. Hash não é assinatura digital.
3. Manter AVG/Defender/SmartScreen ativos. Se bloquearem, parar e registrar
   horário, etapa, código/nome da detecção e hash. Nenhuma exceção ou desativação.
4. Não executar otimização, limpeza, reparo, instalação, desinstalação ou ações em
   lote nesta primeira bateria. Nenhuma operação de driver faz parte desta build.
5. Ativar manualmente `Teste PC FORTE` ou `Teste PC FRACO` em Limpeza e desempenho,
   **antes de repetir** consultas. Padrão desligado, seleção só nesta sessão.

## Bateria A — somente leitura, nos dois PCs

| Ordem / checkpoints | Ação objetiva | Evidência mínima e critério |
| --- | --- | --- |
| 1 · I7 | Atualizar lista duas vezes na mesma sessão; pesquisar MSIX no filtro Inventário | `REGISTRO_PROGRAMAS.LIDO`, `MSIX.LEITURA`, `JOGOS.LEITURA`, `TESTES.INVENTARIO` e `TESTES.MSIX`; total, observados/retidos, falhas, fonte completa/parcial. PC FORTE .30 tinha 174/27 MSIX; variação legítima deve ser explicada, não forçada |
| 2 · I7/MSIX | Se 0x80040154 ocorrer naturalmente, atualizar lista uma vez; não reparar Windows | Falha permanece PARCIAL; após leitura completa anterior nesta sessão, entradas antigas ficam retidas/sem receita e com presença não confirmada. Sem base nesta sessão não haverá retenção inventada de 19 apps da execução passada. Recuperação completa remove a marca antiga e substitui a fotografia |
| 3 · U6 | Verificar atualizações; repetir; cancelar uma consulta e repetir | `TESTES.ATUALIZACOES`, `MULTIFONTES.COBERTURA`, tempos, contagem de fontes ok e rótulos cache/HTTPS no painel. Nenhuma instalação. A soma das categorias deve igualar o total avaliado; candidatos sem receita mantêm site/Store como alternativa |
| 4 · U6 | Se WinGet já existe, no mesmo PC/horário consultar `winget upgrade` sem outros argumentos; comparar com nova consulta GF | Somente listagem, sem `--all`, sem ID de pacote, sem instalar/reparar WinGet. Planilha manual privada por caso A/B: versões instalada/GF/WinGet, canal, arquitetura, escopo e editor conferido localmente. Explicar identidade ausente, catálogo sem entrada, versão incerta, preview/ESR, registro antigo e principal divergente; não tratar mais candidatos como mais segurança |
| 5 · R6 | Investigar resíduos uma vez e consultar Resumo | `RESIDUOS_GERAIS.CONSULTA_FIM`, `TESTES.RESIDUOS`, `TESTES.LIMITES`, `TESTES.FONTES`, `TESTES.ORIGEM`. Separar grupos gerais classificados, preservados, insuficientes e candidatos; grupos/locais reunidos são outro agregado. Flags/ACL/links devem permanecer PARCIAL |
| 6 · R6 | Conferir fabricante de jogo, save/perfil, cache, software portátil e exemplo homônimo, se aparecerem; usar Inspecionar local voluntariamente | Capturas recortadas da decisão, filtros/origens, paginação, motivo e foco; mascarar caminhos/IDs. Não abrir conteúdo nem apagar. Finalidade pelo nome não prova exclusividade; pasta sem ARP pode ser instalação ativa. Cancelar inspeção preserva fotografia anterior |
| 7 · D2/D4 | Consultar drivers duas vezes sem conectar/desconectar dispositivos | `TESTES.DRIVERS`, `DRIVERS.VINCULOS`, `TESTES.PACOTES`, `DRIVERS.HISTORICO`: WMI bruto/associações/IDs, PnP consultados, INFs/compartilhados, mapeados/indisponíveis/inconsistentes/não consultados. Nada é removível por estar ausente ou não mapeado |
| 8 · D4 | Conferir localmente rede, vídeo, storage e um INF compartilhado com o Windows, se disponíveis | Nome/fornecedor/versão/presença/INF e caminho Store em tooltip, apenas no PC. Registrar resultado agregado e divergências por caso, sem exportar IDs. `IsSigned` WMI não é validação criptográfica. Não tentar explicar 168→177 sem histórico real correlacionado |
| 9 · V5 | Abrir modal de resíduos com lista extensa e detalhes longos; buscar, filtrar origem/classificação, paginar e cancelar; Tab/Shift+Tab/Esc/fechar; testar bandeja | Capturas gerais recortadas nos DPI reais; controles visíveis, janela no monitor correto, rolagem dark, foco recuperado e fechamento sem travar. Modal diante de botões não significa controles cortados. `Otimizar` deve continuar em uma linha |
| 10 · P5/P6 | Três sessões da mesma build, condições comparáveis; 2 min em repouso e bandeja/restauração | `DESEMPENHO.LOCAL`: Startup, Inventory, Updates, ResidualDiscovery/Classification/Inspection; anotar tempo até UI, até inventário e até consultas concluídas separadamente; CPU/RAM em 30/60/120 s. Não atribuir rede ao desenho da UI nem comparar MB decimal com MiB |
| 11 · Privacidade | Desativar perfil e repetir uma consulta; alternar perfil não reinicia teto | Sem novas linhas de observação `TESTES.*`; linha MODO de desligamento somente se ainda houver orçamento. Diagnóstico básico existente permanece. Máximo total de 160 tentativas, incluindo modo/falhas; ausência de linha após teto não significa zero resultado. Nenhum envio/arquivo novo |
| 12 · I7/R6/P6 | Diagnóstico completo; fechar normalmente e reabrir | `TESTES.MANUTENCAO` e `APP.ENCERRAMENTO_NORMAL`. Conferir PARCIAL persistente por fonte; não transformar histórico PnP em lixo. Desaparecer um alerta numa sessão não conclui compatibilidade AVG |

As consultas não devem provocar novas instalações nem mudar permissões. Falha
ou cancelamento preserva dados e resultados anteriores; não insistir em loops.
Não provocar erro COM, remover componentes do Windows, criar ACL negada no perfil
real nem instalar antivírus para simular um caso.

### Campos novos para comparar a .31

`TESTES.MSIX`: `observados`, `retidos`, `falhas`, `parcial`.
`TESTES.PACOTES`: `pacotes`, `mapeados`, `indisponiveis`, `inconsistentes`,
`nao_consultados`, `pnp`, `wmi_brutos`, `parcial`. Vínculos confirmados não validam
história, compatibilidade, hash, catálogo ou assinatura de drivers.

`TESTES.ORIGEM`: código fixo, entradas e flags de limite de entradas,
profundidade, leitura, links, tempo, cancelamento e indisponibilidade. As contagens
representam observações debitadas no orçamento da descoberta, não arquivos únicos
ou espaço recuperável. Um mesmo local pode ser observado por fontes distintas.

| Código | Origem fixa |
| --- | --- |
| 1 | AppData do usuário atual |
| 2 | ProgramData |
| 3 | Program Files |
| 4 | Atalhos do menu Iniciar |
| 5 | Processos em execução |
| 6 | App Paths |
| 7 | Inicialização/serviços |
| 8 / 9 | Registro HKCU, visões 64 / 32 bits |
| 10 / 11 | Registro HKLM, visões 64 / 32 bits |
| 0 | Categoria não identificada; sem caminho no log |

## Entrega privada das evidências de campo

- Um `GerenciadorFinal-Diagnostico-v5.13.11.31.log` por PC/sessão relevante,
  identificado manualmente no texto da conversa. Não publicar log bruto no GitHub.
- Resolução, escala, Windows/arquitetura, versão/hash do artefato e ordem executada.
  O programa não infere PC FORTE/FRACO automaticamente.
- Capturas recortadas: inventário completo/parcial; resumo de resíduos e um
  exemplo preservado; tabela/estado de drivers; modal visível no DPI real.
  Mascarar usuário, caminhos, IDs, contas e itens pessoais antes de compartilhar.
- Para AVG: texto/código da detecção, instante e hash; somente captura recortada
  do aviso se necessário. Não enviar binário ativo para criar exceção.
- Comparação WinGet por casos equivalentes, no mesmo PC e em horário próximo;
  se não estiver disponível, marcar pendente sem instalar pré-requisitos.

I7, R7, U8, P5, V5 e D4 exigem os dois computadores. R6 exige resolver casos
históricos concretos; U6 exige explicar divergências reais de fontes. D2 ainda
exige histórico confiável do Windows e cobertura além das associações WMI.

## Bateria B — aplicativos, separada e supervisionada

Somente depois de A e do refinamento de resíduos: escolher **um** aplicativo não
essencial sem dados sensíveis, salvar backup e definir uma rota de recuperação
pelo instalador oficial verificado. Ponto de restauração não substitui backup.

1. Registrar versão/identidade/escopo/arquitetura e obter consentimento individual.
2. Testar uma atualização com receita validada; conferir hash/assinatura,
   código de saída, versão final e atualização automática da lista. Se depender
   de site/Store, usar a alternativa e validar a nova leitura; nenhum sucesso presumido.
3. Em etapa separada, testar o desinstalador oficial, observando as escolhas de
   preservação do próprio instalador. Reconsultar inventário e resíduos; não usar
   limpeza heurística externa nem liberar o botão de exclusão do GF.
4. Se houver falha, parar essa operação, registrar resultado e seguir a rota de
   recuperação prevista. Não testar lotes nem aplicativos essenciais do Windows.

Repetir somente os cenários autorizados no PC FRACO após a validação no PC FORTE.
Não houve execução remota destas operações nesta missão.

## Ambiente descartável e manutenção do Windows

Drivers G1–G5 continuam bloqueados. Instalação, remoção, rollback, export/backup e
recuperação de falha de drivers só poderão ser ensaiados em VM/sistema descartável
após desenho/validação de origem, compatibilidade e assinatura. Não usar
`pnputil /delete-driver` ou equivalente nos PCs pessoais. Saves, credenciais,
WinSxS, Driver Store, pacotes compartilhados e pontos de restauração são preservados.

Testes de instalador GF, atalhos e operações administrativas existentes (rede,
DISM/SFC, limpeza, energia/restauração) são outra bateria em máquina isolada,
após backup e consentimento específico. Não criar defeitos para acionar botões.
Assinatura Authenticode, atestação de procedência e SHA-256 são evidências distintas;
a candidata desta missão permanece experimental e sem assinatura.


</details>
