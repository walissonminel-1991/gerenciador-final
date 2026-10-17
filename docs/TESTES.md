# Roteiro de testes — Gerenciador Final 5.12.3

Este roteiro serve a mantenedores e revisores. **Não é um relatório de testes
concluídos nem uma garantia de segurança.** Registre ambiente, compilação,
versão, resultados e erros encontrados ao executar cada verificação.

## Preparar ambiente

1. Use uma máquina Windows 10/11 x64 de testes, com SDK .NET 10 para compilação.
2. Extraia o código em uma pasta nova, sem misturar versões anteriores.
3. Execute `Compilar-OSS-Windows-x64.bat` e verifique a criação de
   `Distribuicao\GerenciadorFinal.exe`.
4. Abra o aplicativo e observe se as abas, o tema e o ícone GF aparecem corretamente.

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
