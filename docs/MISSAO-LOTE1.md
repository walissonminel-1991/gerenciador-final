# Lote 1 — confiabilidade e clareza (candidata técnica)

**Base examinada antes de mudanças:** `main` `410d65e23546976db456337c71e4705f10217d36`;
PR #40 em Draft: `ef13aef49083f357f6a14f4de46f8bc976dbd830`.
Este lote usa **branch separada a partir do HEAD da PR #40**. Não altera `main`,
o estado Draft da PR #40, permissões, instaladores, WinGet ou regras de exclusão.

## Implementado na branch de trabalho

- **Histórico local:** para cada sessão de teste, um arquivo novo `GF-Historico-<GUID>.log`
  em `%LOCALAPPDATA%\GerenciadorFinal\Historico`. Apenas códigos fechados,
  horários e contagens. O arquivo nunca recebe nome de aplicativo, caminho,
  editor, linha de comando, token ou identificador do Registro.
  Criação exclusiva e pasta ancorada por handle; se não gravável, a UI avisa
  e mantém somente os últimos eventos da sessão em memória. Sem upload, envio ou serviço.
  Limites: 128 eventos na prévia e 256 eventos escritos por sessão.
- **Inventários:** compara somente fotografias marcadas completas, com
  identificadores não vazios e únicos. Leitura parcial, vazia ou ambígua
  **não substitui a referência anterior**. Alterações de versão/editor/nome
  são relatadas como metadados modificados, e ausência de Registro não
  equivale a desinstalação comprovada. A identidade bruta de comparação
  não é escrita no histórico.
- **Motivos de bloqueio:** relatório distingue componente protegido,
  somente inventário, divergência que pede revisão, falta de receita
  autorizada e desinstalador não validado. Mensagens explicativas
  não concedem permissão para executar.
- **Relatório:** botão **Histórico / relatório** junto ao REGISTRO;
  diálogo somente leitura com resumo do inventário, contagens por
  restrição e eventos. **Salvar TXT** exige seleção explícita de destino.
  Não inclui nomes nem caminhos dos aplicativos. Atribuição individual
  e diagnóstico avançado continuam sujeitos às proteções existentes.
- **Resultados:** apenas estágios finais de atualização e desinstalação
  entram no histórico. Exceções, `Reason` e `Details` não são persistidos.
  Uma falha no histórico não apaga confirmação, bloqueio, pendência ou
  proteção de repetição; a função tem natureza somente consultiva.

## Validações propostas e limites

Fixtures em `TestesLayout/Work14ReliabilityTests.cs` cobrem:
primeira fotografia, leitura parcial, registro novo, alterado e ausente,
duplicidade, identidade em branco, snapshot vazio, privacidade de strings
adversariais, enum de bloqueio, limite de 128 e falha ao criar arquivo
sem sobrescrita. Tudo dentro de `%TEMP%` do runner Windows,
sem tocar usuários ou desinstalar programas.

A workflow existente `validar-candidata-windows.yml` já executa
`TestesLayout`, além das baterias anteriores; o fluxo `build-windows.yml`
publica somente pacote TESTE, não assinado e sem release. Evidências de
execução precisam ser acrescentadas **após** as Actions concluírem:
links, commit de fonte, resultados, falhas, skips e correções.

**Homologação física é etapa diferente da implementação.** Os critérios
de I7/R4/R7/R6/U8/P5/P6/V5/D2/D4 e G1–G5 não mudaram.
Roadmap permanece **27/42**, enquanto os requisitos de campo não forem
comprovados. Não executar em PC FORTE ou PC FRACO nesta missão.
A integridade/assinatura de binários externos não deve ser afrouxada
para aprovar CI ou fixture. Sem merge nem release estável.
