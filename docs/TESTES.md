# Roteiro de testes — Gerenciador Final (candidata atual v5.13.11.26 TESTE)

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
