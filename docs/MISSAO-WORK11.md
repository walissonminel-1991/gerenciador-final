# MISSÃO WORK11 — progresso individual das atualizações

Data: 23/10/2026. Candidata: **v5.13.11.53 TESTE**. Fonte: `78424b8e6bc64ba6a9b4f546e6542ead608c7362`.
Base examinada: `6511ff12` (documental WORK10), código .52 `c200ab25`, CI 40021841254 aprovada. PR #40 permanece Draft; main não alterada.

## Problema e implementação

O caso físico informado da .52 atualizou 7-Zip 26.04 → 26.05 em aproximadamente
1,4 s. O motor confirmou a versão, mas só enviava mensagens ao rodapé; os
cartões não tinham estado próprio. LoadInstalledAsync reconstruía os objetos,
e LoadUpdatesAsync substituía mensagens finais por mensagens de consulta.

- `UpdateOperationProgress.cs`: fotografia de operação separada do inventário,
  identidade por produto, hive/visão/registro, editor, pasta e MSI. Nomes com
  versão, como 7-Zip, preservam a associação. Sem busca aproximada por nome ou
  remapeamento automático para outra instalação. Cache fraco por objeto, limite
  de 256 operações; ativas/pendentes não são expulsas. Os resultados duram a
  sessão, salvo expulsão de resultados antigos ao atingir o limite.
- `VerifiedUpdaters.cs` e `WindowsTerminalUpdater.cs`: download compartilhado
  mede bytes e Content-Length; sem tamanho, indicador indeterminado. Detecta
  EOF incompleto, excesso de tamanho, HTTP e cancelamento. Os domínios,
  redirecionamentos autorizados, SHA-256, assinaturas, recibos e locks existentes
  continuam sendo exigidos. Download completo não confirma instalação.
- `DarkControls.cs` e `Program.cs`: barra compacta desenhada no cartão. Em largura
  suficiente, ocupa a lacuna antes dos botões; em largura pequena, usa a segunda
  linha, com mensagem completa na dica. Sem aumentar a altura de 67 pixels.
  Azul ativo, verde confirmado, vermelho falha, amarelo pendente/bloqueado/UAC e
  cinza cancelado. Apenas download mede porcentagem; instalação fica animada.
- Um timer de 100 ms por lista, ligado somente enquanto houver operação ativa
  visível. Download notifica no máximo a cada 120 ms, além de início/fim.
  Repaint limitado às linhas necessárias; callbacks de download modificam
  apenas estado sincronizado, sem tocar controles WinForms.
- `UpdateLaunchGate.cs`: separa recusa UAC de cancelamento de download; mantém
  3010 e incerteza após lançamento. Erro nativo MSIX comprovado conserva HRESULT
  e fica vermelho; cancelamento da espera não afirma que o Windows parou.
- A reserva de operação ocorre antes da primeira espera assíncrona. Bloqueia
  cliques duplicados e repetição sobre resultado pendente. O inventário é
  renovado também após falha/resultado incerto quando houve início efetivo do
  instalador. Falha posterior de consulta não apaga a versão já confirmada.
- Diagnóstico existente: op GUID, hash da identidade, etapa, bytes/tamanho,
  porcentagem, download concluído, exceção por tipo, retorno, reinício e duração.
  Sem caminhos, nomes livres, credenciais ou saída de instaladores. Falha de
  gravação é indicada sem falsificar resultado de uma instalação já executada.

## Defeitos adicionais encontrados

1. Preflight do 7-Zip/Notepad++/Sumatra aguardava inventário antes de marcar o
   GF como ocupado: duas chamadas podiam passar pelo primeiro bloqueio. A
   verificação foi movida para dentro do executor que assume a operação antes
   de aguardar; a identidade também tem reserva sincronizada.
2. Download encerrado antes do Content-Length dependia de falha do stream/hash
   para ser rejeitado. A transferência agora exige o tamanho final exato quando
   informado e testa EOF antecipado/excesso.
3. Recusa UAC era misturada ao cancelamento genérico. O resultado agora a
   identifica separadamente, sem alterar a elevação nem o manifesto asInvoker.
4. Resultado MSIX de erro não tinha tipo técnico que distinguisse falha nativa
   comprovada de espera indeterminada. O HRESULT comprovado agora é preservado.
5. Status tinha uma janela entre InvokeRequired e BeginInvoke no descarte.
   O encaminhamento agora protege handle, Disposing e InvalidOperationException.

## Validação

[CI 40028404988](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40028404988) **success**: regressões Windows, política e
compilação/publicação. Checkout de integração `c824894c` sobre main `410d65e2`;
a fonte da candidata é `78424b8e6bc64ba6a9b4f546e6542ead608c7362`.

| Conjunto | Resultado |
|---|---|
| Atualizadores: autorização, identidade e integridade | **186/186**, incluindo **38 controles novos WORK11** |
| Mutações Windows | **17/17 rejeitadas**, três novas de progresso/resultado |
| Resíduos/desinstalação WORK10 | **576/576**, sem modificar seu executor |
| Multifontes / catálogo | **159/159** / **17/17** |
| MSIX / encerramento | **47/47** / **30/30** |
| UI nova | **36 combinações**, oito estados; reconstrução, filtro, navegação e descarte |
| UI anterior preservada | **132** combinações resolução/aba; **22** cenários; **44** combinações WORK10 |
| Política | **29 Python + 10 Node**, aprovados |
| Compilação com analisadores | **zero erros e zero avisos** |

As três mutações novas aceitaram artificialmente download truncado, repetição
de pendente e sucesso em resultado incerto; os testes correspondentes falharam
pelo motivo esperado. Não se conta erro de compilação como mutação detectada.
No Linux, os controles disponíveis deram **10/10**; as sete mutações nativas
adicionais foram executadas no Windows.

[Capturas do runner](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40028404988/artifacts/12533617807): 66 imagens no total,
dez novas dos estados WORK11. Revisão visual efetuada nas capturas de 1280×720,
1440×900 e 2560×1440, incluindo a variante bitmap 200%: barra contida, cores
legíveis, porcentagem e símbolo de confirmação sem sobreposição. As demais
combinações receberam desenho/validação de geometria automatizados.
A medição no runner foi **25 ms / 608 bytes** para 100 mil consultas de estado
em cache, separada de inventário, rede e gravação de log.

Os testes novos usam respostas HTTP/streams controlados, gates e processo
filho descartável. Não atualizam, instalam nem desinstalam aplicativos reais
nos PCs pessoais. A UI usa formulário real sem serviços, inventário sintético,
reconstrução, pesquisa, navegação, oito estados e desenho em diferentes DPI.
As escalas de bitmap/geométricas não equivalem a uma troca física de monitor
PerMonitorV2. UAC recusado é simulado; não demonstra o clique humano no desktop.

O ensaio local Linux do conjunto antigo completo de atualizadores para na
fixture Windows do 7-Zip (pasta ProgramFiles inexistente); não foi contabilizado
como aprovação. A suíte --work11 é portável e passou; a regressão completa
precisa do runner Windows. Fontes/downloads externos opcionais permanecem
fora da regressão determinística; nenhum skipped conta como aprovado.

## Desempenho e pacote

[Pacote portátil .53 TESTE x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40028404988/artifacts/12533044951), runtime .NET/WindowsDesktop
**10.0.13 incluído**, sem SDK no PC do usuário e sem extração single-file.

- Commit fonte: `78424b8e6bc64ba6a9b4f546e6542ead608c7362`; build de integração: `c824894c29285549a03b1d9e9bc6b9b0b24edd04`.
- ZIP: **58.992.475 bytes**. Extraído: **151.369.110 bytes**.
- **281 arquivos / 280 hashes** conferidos no runner e novamente fora dele.
- EXE e DLL: **5.13.11.53**; executável **AMD64**, manifesto **asInvoker**.
- SHA-256 ZIP: `c9a98052ce145e7c70db3a19d70cd4ac602e6f48b1e39b11614afb8ae15168f3` — igual ao digest do upload GitHub.
- Assinatura Authenticode: ausente. AVG/SmartScreen reais não homologados.
- Retenção do artefato: **30/10/2026, 18:44:53 (Fortaleza)**.

Em relação ao pacote .52 aprovado, o ZIP aumentou **9.623 bytes** e o conteúdo
extraído **24.576 bytes**, conservando 281 arquivos e o mesmo runtime.
Isso mede distribuição; não comprova diferença de desempenho em uso.

Medição local auxiliar: 100 mil consultas a uma identidade em cache levaram
32–44 ms, com 608 bytes alocados no trecho medido. É uma microamostra Linux,
sem equivalência com abertura, rede, CPU ou RAM dos PCs. Não há comparação A/B
válida com a .52; não se afirma ganho de desempenho. A implementação usa um
buffer de 128 KiB por transferência, um timer por lista e armazenamento limitado.

## Aceitação e pendências

Implementação: progresso por instalação, medidas reais, resultados distintos,
reconstrução sem perder estado, bloqueio de repetição e diagnóstico integrados.
Validação automatizada: conforme CI e evidências acima. Homologação física da
.53: **pendente** em ambos os PCs; inclui leitura do estado, escala real,
instaladores silenciosos, resultado de versão e AVG/SmartScreen.

A WORK10 não foi redesenhada: desinstalação, uma decisão SIM/NÃO, limpeza
permanente elegível, worker UAC e proteções pessoais continuam no mesmo fluxo.
Drivers, energia, reparos e Windows Update não foram modificados. Roadmap
permanece **27/42**, com U8/R7/R4 físicos abertos. Sem merge ou release estável.

O próximo teste é o [roteiro consolidado](TESTE-CONSOLIDADO.md): Notepad++
8.9.1 → 8.9.8.1, depois SumatraPDF 3.5 → 3.6.1, já preparados no PC FORTE.
Não reinstalar versões antigas e não repetir a atualização do 7-Zip.
