# Roteiro de testes — Gerenciador Final (candidata v5.13.11.27 TESTE com CI aprovada; referência presencial .26)

**21/10/2026: incorporação conservadora na .26; [relatório e roteiro consultivo](OPERACAO-FAXINA-FINAL.md). Último teste real do PC fraco: .24. A MSIX permanece parcialmente indisponível após retry na .24; proteger os resultados e não concluir que 19 apps foram desinstalados.** Primeiro fazer somente a bateria de interface e coleta local, depois consultas, e somente após backup as operações individuais de atualização, desinstalação e reparo. **Envio real continua indisponível nesta candidata.** A ideia de autenticação GitHub com conta existente foi estruturada em [piloto restrito](PILOTO-AUTENTICACAO-GITHUB.md), dependente de uma GitHub App e repositório privado exclusivos.

Este roteiro serve a mantenedores e revisores. **Não é um relatório de testes
concluídos nem uma garantia de segurança.** Registre ambiente, compilação,
versão, resultados e erros encontrados ao executar cada verificação.

## Preparar ambiente

1. Use o computador Windows x64 disponível, mantendo o antivírus ativo; faça backup antes de operações administrativas.
2. Para testar o binário já compilado, utilize o artefato específico da build Windows aprovada na PR #40; não é necessário instalar SDK neste PC apenas para abri-lo.
3. Confira a versão e os hashes informados na build, não misture binários de candidatos diferentes. Não ignore alertas do AVG nem contorne o SmartScreen.
4. Abra o aplicativo e observe abas, tema, ícone GF, restauração da bandeja e disponibilidade dos botões. Teste a janela de privacidade: recusar, simular **sem enviar**, autorizar somente coleta local e revogar. Verifique que isso não tenta conexão de envio.

## Testes consultivos — sem alterar o Windows

- **Aplicativos:** listar instalações, pesquisar e consultar atualizações via catálogos HTTPS e fontes oficiais.
  Não clicar em atualização/desinstalação em massa neste teste.
- **Limpeza e desempenho:** conferir prévia de limpeza, espaço de cada disco,
  botões `Atualizar discos` (texto completo) e `Analisar limpeza`.
- **Reparo e rede:** ao iniciar, `Reparar rede` deve estar desabilitado; clicar
  `Analisar rede` e conferir adaptador, gateway, DNS e TCP/443 no painel/log.
  Com testes saudáveis, o reparo deve continuar desabilitado.
- **Planos de energia:** verificar plano ativo sem trocá-lo.
- **Inicialização e sistema:** consultar dados, sem alterar configurações.

## Bateria prioritária — PC FORTE (v5.13.11.26 TESTE)

Objetivo: verificar a coleta e **refinar a interpretação dos resíduos antes** de
avançar a gerenciamento de drivers. O nome PC FORTE identifica o computador
principal de testes; PC FRACO designa o equipamento de menor capacidade.
Esta bateria não substitui a exigência de validação nos **dois** computadores
para I7, R7, U8, P5, V5 e D4 do roadmap.

**Preparo e rastreabilidade** — usar somente o ZIP x64 da .26 vinculado ao
código `8578fa3` (merge sintético `165069f3`), obtido na
[build Windows aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39642731837).
Conferir `BUILD-INFO.txt`, `SHA256SUMS` e hash do ZIP
`8a30abca5cefcee7d5168c5741ac71975abc294c0517ea99d133c71bf91d66e2`.
Manter AVG/Defender/SmartScreen ativos; não criar exceção, desativar serviço
ou baixar build de procedência diferente. Anotar Windows, arquitetura, escala DPI,
resolução, hora, tempo de abertura, consumo aproximado CPU/RAM e
`v5.13.11.26 TESTE`; não publicar identificadores do PC.

### Etapa 1 — leitura de programas e resíduos (sem exclusão)

- [ ] Abrir, fechar e reabrir; navegar nas abas e conferir contagens do inventário
  antes/depois de atualizar. Registrar fontes indisponíveis e status de **parcial**
  sem interpretar ausência MSIX como desinstalação.
- [ ] Na investigação existente, conferir grupos e caminhos vinculados a softwares
  **ainda instalados**, fabricantes compartilhados e vestígios históricos;
  separar **preservar**, **suspeita sem confirmação** e **vínculo confirmado**.
- [ ] Inspecionar exemplos de perfis, cookies, credenciais, favoritos, saves,
  backups, playlists, caches e fontes de instalação **apenas quando aparecerem na
  consulta atual**. Conferir explicação no detalhe e que conteúdo não é aberto.
- [ ] Testar consulta parcial e cancelamento pela interface, sem forçar falhas
  destrutivas. Resultado parcial deve preservar dados, não ampliar remoção.
- [ ] Verificar que R4 continua bloqueado: nenhum botão de investigação deve
  apagar pastas, Registro, perfil, Driver Store, WinSxS ou cache do Windows.
- [ ] Comparar visualmente candidatos com um desinstalador conhecido, **sem
  executar limpeza externa**, classificando falsos positivos e falsos negativos.
  Não assumir que cache ou pasta órfã é dispensável.

### Etapa 2 — cobertura de atualizações e desinstalação comum

- [ ] Guardar fotografia do inventário e candidatos (nome, editor, versão,
  origem e motivo de indeterminação; ocultar dados sensíveis).
- [ ] Comparar cobertura com WinGet **no mesmo PC e período**, apenas como
  referência independente; distinguir diferenças de identidade, arquitetura,
  versões e fontes, sem forçar equivalência.
- [ ] Só após backup e resultado satisfatório da etapa 1, escolher **um programa
  não essencial**, com desinstalador oficial e sem dados pessoais importantes,
  para atualização individual supervisionada. Conferir versão e identidade
  após a operação e que a lista se atualiza. Sem `Atualizar todos`.
- [ ] Em rodada separada, com backup e programa de teste não essencial,
  verificar desinstalação pelo mecanismo oficial. Reconsultar inventário
  e resíduos, **sem excluir** achados pós-desinstalação; conferir preservação
  de dados compartilhados.

### Etapa 3 — drivers: observação, não manutenção

- [ ] Executar `Consultar drivers`, anotar quantidade, parcialidade, INFs
  ausentes/inválidos, dispositivos presentes/ausentes e versões sem formato
  reconhecível. Repetir na mesma sessão para identificar instabilidade.
- [ ] Conferir manualmente alguns itens no Gerenciador de Dispositivos:
  mesmo ID PnP, dispositivo, fornecedor, versão informada pelo Windows e INF
  quando disponível. Mesmo INF em dois dispositivos **não** significa duplicata.
- [ ] Registrar divergências de WMI, itens sem INF e assinaturas indeterminadas
  como **pendentes de interpretação**; não marcar driver como obsoleto ou
  compatível somente por comparação textual.
- [ ] Não instalar, atualizar, desinstalar, remover do Driver Store nem
  reverter drivers nesta candidata. G1–G5 seguem não implementados.
  Testes de operação sobre drivers exigirão outra versão, pré-validação
  de pacote/assinatura/compatibilidade, plano de recuperação e ambiente isolado.

### Encerramento e decisão

Classificar cada resultado como **aprovado, reprovado, inconclusivo ou não
executado**. Guardar versão/hash, passos e resultados sanitizados; se houver
travamento, erro do AVG, `0x80040154`, divergência de inventário, corrupção
de dados ou ameaça à inicialização, parar as ações de escrita e continuar
somente a coleta segura. Não elevar os **26/42** apenas por instalar uma build
ou por ter testes sintéticos aprovados: cada checkpoint depende da evidência
descrita no roadmap. Depois repetir os cenários aplicáveis no PC FRACO.

## Evidência sanitizada do PC FORTE — 21/10/2026, .26

O diagnóstico local e cinco imagens foram analisados sem publicar dados
privados ou IDs de dispositivos. Startup etapa 329,74 ms, inventário inicial
666,33 ms e repetido 467,82/455,90 ms; pico de memória do processo ~151 MB.
Não equivale a benchmark de uso prolongado ou desempenho do PC FRACO.

- **Inventário:** 174 entradas e contagem estável, 140 desinstaláveis, 34
  somente inventário; 27 MSIX registrados com status ok, 159 pacotes
  enumerados. Jogo Steam 42 entradas observadas, cinco adicionadas.
- **Atualizações:** 2 candidatos (7-Zip e Outlook/Store); fontes consultadas
  8, sucesso em 7, catálogo RuckZuck 631. Há 107 sem correspondência,
  31 só inventário, três correspondências ambíguas e um timeout de uma
  fonte oficial. Windows Update retornou zero na sessão. Nenhuma escrita.
- **Drivers:** 168 associações em consulta não parcial, 60 INFs, 19
  compartilhados, três sem INF válido. Mudanças do histórico próprio:
  quatro novos, 18 não observados, dois alterados — **não são provas de
  remoção ou atualização**. Fotos mostram nomes/provedores não informados
  e versões WMI incomuns, requerendo conferência por dispositivo/ID.
- **Resíduos:** 12 origens, 3.925 entradas, 43 candidatos, 15 instalações
  a conferir, 11 perfis/fabricantes e 42 jogos ativos. Parcial por limite,
  falha de leitura, profundidade e link; UI unificada mostra 44 grupos e
  161 locais. Fabricantes/jogos foram tratados para preservar; não há
  comprovação de exclusividade nem autorização de exclusão.
- **Rede/encerramento:** HTTPS 443 concluído, DNS com falha isolada;
  diagnóstico inconclusivo e sem reparo automático. Encerramento normal;
  sem alerta AVG visível nesta sessão, sem comprovar compatibilidade universal.

**Decisão:** manter 26/42 e R4 bloqueado. A próxima candidata .27 usa
`Description` do mesmo registro PnP quando falta `Name` e permite busca/tooltip
local pelo ID, sem exportar IDs em logs. As três Actions da .27 foram aprovadas: [regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39648484834),
[build x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39648484877) e
[políticas](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39648485243).
A regressão confirmou 351/351 resíduos, 30/30 MSIX e o caso novo do fallback
PnP no conjunto de drivers. Pacote .27 de teste: [artefato temporário](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39648484877/artifacts/12368243340)
(válido até 28/10/2026 segundo GitHub), fonte `77d9522`, merge sintético
`3e66b334`. Conferir BUILD-INFO e SHA256SUMS; arquivo sem assinatura.
 Ainda pendentes testes de atualização/desinstalação
supervisionadas e confirmação de detalhes dos resíduos em cada caso.

## Testes administrativos — apenas em ambiente controlado

Após backup e autorização explícita, avaliar individualmente funções de limpeza,
atualização supervisionada, reparo DISM/SFC, alterações de energia, restauração e rede. Confirmar
UAC, mensagens, códigos de saída e se operações interrompidas relatam falha.
Não provocar defeitos de rede ou remover programas apenas para acionar botões.
`Redefinir rede` é uma operação avançada separada de `Reparar rede`.

## Instalador e segurança

- Inspecionar o projeto e o binário efetivamente compilado, incluindo dependências.
- Testar instalação/desinstalação e o atalho GF em máquina isolada.
- Confirmar que não são criadas exclusões no antivírus ou regras de firewall.
- Investigar alertas antivírus sobre o artefato testado antes da distribuição;
  não instruir ninguém a contornar bloqueios.
- Para versões assinadas, verificar assinatura, carimbo de data/hora e SHA-256.

Para releases, arquivar os resultados reais dos testes no repositório (Issues ou
relatório vinculado), com sistema operacional e hash do binário testado.
