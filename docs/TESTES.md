# Roteiro de testes — Gerenciador Final v5.13.11.8 TESTE

**20/10/2026: neste ciclo, usar o outro PC; PC FRACO não será utilizado hoje.** Primeiro fazer somente a bateria de interface e coleta local, depois consultas, e somente após backup as operações individuais de atualização, desinstalação e reparo. **Envio real ainda não é oferecido na v5.13.11.8.** A ideia de conta GitHub secundária foi estruturada em [piloto restrito](PILOTO-CONTA-GITHUB-SECUNDARIA.md), dependente de uma GitHub App e repositório privado exclusivos.

Este roteiro serve a mantenedores e revisores. **Não é um relatório de testes
concluídos nem uma garantia de segurança.** Registre ambiente, compilação,
versão, resultados e erros encontrados ao executar cada verificação.

## Preparar ambiente

1. Use o computador Windows x64 disponível, mantendo o antivírus ativo; faça backup antes de operações administrativas.
2. Para testar o binário já compilado, utilize o artefato específico da build Windows aprovada na PR #40; não é necessário instalar SDK neste PC apenas para abri-lo.
3. Confira a versão e os hashes informados na build, não misture binários de candidatos diferentes. Não ignore alertas do AVG nem contorne o SmartScreen.
4. Abra o aplicativo e observe abas, tema, ícone GF, restauração da bandeja e disponibilidade dos botões. Teste a janela de privacidade: recusar, simular **sem enviar**, autorizar somente coleta local e revogar. Verifique que isso não tenta conexão de envio.

## Testes consultivos — sem alterar o Windows

- **Aplicativos:** listar instalações, pesquisar e consultar atualizações WinGet.
  Não clicar em atualização/desinstalação em massa neste teste.
- **Limpeza e desempenho:** conferir prévia de limpeza, espaço de cada disco,
  botões `Atualizar discos` (texto completo) e `Analisar limpeza`.
- **Reparo e rede:** ao iniciar, `Reparar rede` deve estar desabilitado; clicar
  `Analisar rede` e conferir adaptador, gateway, DNS e TCP/443 no painel/log.
  Com testes saudáveis, o reparo deve continuar desabilitado.
- **Planos de energia:** verificar plano ativo sem trocá-lo.
- **Inicialização e sistema:** consultar dados, sem alterar configurações.

## Testes administrativos — apenas em ambiente controlado

Após backup e autorização explícita, avaliar individualmente funções de limpeza,
WinGet, reparo DISM/SFC, alterações de energia, restauração e rede. Confirmar
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
