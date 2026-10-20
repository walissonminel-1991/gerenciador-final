# Triagem GitHub — validação estrita de Issues públicas

**Estado:** implementado na branch de teste da PR #40. A coleta continua **somente local**; o aplicativo abre um formulário GitHub com **publicação manual**. Não há envio em segundo plano, token no EXE, agente IA executando mudanças ou publicação automática.

## Funcionamento

1. O usuário consente com a coleta local e, separadamente, decide abrir o formulário do GitHub no navegador. A Issue é pública e associada à conta que a publicar.
2. Quando uma Issue nova possui prefixo `[GF Diagnostico]`, um workflow com **permissão somente de leitura** valida título e corpo do formato gerado pelo GF. Demais Issues não recebem processamento. Não usa `pull_request_target` nem recebe código da Issue.
3. O parser local `scripts/triagem_diagnostico.py` recusa conteúdo fora do formato canônico, versões inconsistentes, categorias fora da allowlist, repetições, contagens 0 ou acima de 100, linhas extras e observações livres. Recusa sem imprimir conteúdo original, anexos, dados de autor, tokens ou comandos.
4. Se o corpo for aceito, escreve no resumo do job **somente a versão, o tipo de encerramento e os totais por categoria autorizada**. Não abre outra Issue, não comenta, não etiqueta, não clona conteúdo recebido e não modifica repositório.
5. A triagem não assegura que o usuário realmente executou o GF: como as Issues são públicas, os agregados devem ser tratados como autorrelatos, não dados autenticados.

## Testes e limites

A suíte `scripts/test_triagem_diagnostico.py` cobre a mensagem gerada, CRLF, categorias repetidas, contagens inválidas, versão divergente, tentativas de comando e vazamento de texto. Executa em PR, push na main e disparo manual; CI de Issues executa apenas o parser.

Ainda restam: autenticação/verificação de origem, deduplicação com tratamento de privacidade, limitação de abuso, encaminhamento a IA com isolamento e revisões, retenção/exclusão dos conteúdos no GitHub, validação nos PCs e AVG. O processamento de Issues não promete apagar dados de Issues públicas. Não habilitar automaticamente agentes que modifiquem código com base em texto de terceiros.

O workflow desta etapa passa a existir na branch padrão apenas **após** merge da PR #40.
