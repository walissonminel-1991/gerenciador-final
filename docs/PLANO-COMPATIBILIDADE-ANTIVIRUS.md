# Plano de compatibilidade com antivírus — PC FRACO

Rodada: v5.13.10.22, 2026-10-20.

## Princípio

O alerta `IDP.Generic` (AVG Módulo Comportamento) após tentativa de restauração da bandeja **não prova** por si só a causa da detecção nem que se trata de falso positivo. Não desativar o AVG, não criar exceções automáticas, não ofuscar nem tentar ocultar funções do programa. Preservar o executável em quarentena e submeter a amostra ao AVG para análise.

## Alternativas legítimas à enumeração frequente de processos

1. **Modo de coleta global (implementado em v5.13.10.22)**: fora da aba Processos, inclusive na bandeja, continuar a coletar CPU e RAM globais pelas APIs do Windows e tráfego total pelos contadores da interface de rede. Não percorrer todos os processos nem abrir seus handles.
2. **Modo detalhado sob demanda (implementado em v5.13.10.22)**: quando a aba Processos está visível, preservar a coleta de PIDs, CPU/RAM e E/S por processo e a ordenação. Ao ocultar a janela, manter a última tabela em cache; amostras globais não substituem a tabela por números inventados. Ao voltar à aba, renovar a medição.
3. **Sem utilitários auxiliares para essa troca (implementado)**: não lançar scripts, comandos externos ou processos adicionais. As rotinas de manutenção continuam independentes e sujeitas à confirmação.
4. **Próximo passo**: comparar custos em Windows real e observar novos eventos antes de alterar a política. Não concluir que a redução de frequência por si só corrige uma detecção comportamental.

## Processo de segurança e distribuição

1. Compilar código público com workflow GitHub Actions, manifestos/versionamento coerentes e hashes SHA-256. Conferir que os artefatos são originados do código correto, sem enviar executáveis obtidos de terceiros.
2. Investigar o alerta com o fornecedor (AVG Threat Labs: Menu > Quarentena > Mais opções > Enviar para análise). Não tirar o arquivo da quarentena para os testes antes da avaliação.
3. Manter alterações sensíveis isoladas em branches/PR de teste. Pedir revisão de código para operações com privilégios elevados, encerramento de outros processos e manipulação de Registro.
4. Avançar na assinatura de código por processo documentado (SignPath Foundation para OSS, se aprovado) para melhorar origem/procedência. Assinatura **não garante** ausência de malware nem impede todos os alertas.
5. Não coletar nomes de programas/processos em logs de diagnóstico compartilhados. Registrar apenas eventos, tipos de erro e contagens. Remover dados pessoais de capturas antes da divulgação.

## Gate de validação

- [ ] CI Windows: build e regressões do monitoramento aprovados.
- [ ] CI: testes MSIX e resíduos aprovados.
- [ ] Análise de procedência e resultado da submissão ao AVG avaliados.
- [ ] Nenhuma exclusão de antivírus necessária para executar as funcionalidades.
- [ ] Abrir/minimizar/restaurar com AVG ativo sem detecção, em ambiente liberado para testes.
- [ ] Comparar CPU, RAM, E/S e fluidez no PC FRACO, inclusive em bandeja.
- [ ] Confirmar que a consulta MSIX não produz inventário silenciosamente incompleto.

A aprovação do CI não substitui as etapas específicas do AVG ou do hardware do usuário.
