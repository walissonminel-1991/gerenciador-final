# Roteiro de testes — v5.13.11.32 TESTE

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
