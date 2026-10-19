# Política de segurança

## Estado

O projeto está em preparação para a primeira publicação aberta. **Nenhum binário está atualmente certificado como seguro ou assinado pela SignPath Foundation.**

## Como reportar vulnerabilidades

Após criar o repositório público, o mantenedor deve habilitar **Private vulnerability reporting** em Settings → Security → Private vulnerability reporting. Use então a opção **Report a vulnerability** disponível na aba Security do repositório. Se ela ainda não estiver configurada, **não publique um exploit nem credenciais em uma Issue pública**; aguarde a habilitação do canal privado.

Ao reportar, inclua a versão, configuração Windows, comportamento esperado, passos seguros para reprodução e o impacto observado. Não compartilhe dados pessoais ou credenciais.

## Distribuição e antivírus

Um instalador da versão 5.12.3 recebeu anteriormente **3/52 alertas de fornecedores no VirusTotal**. Houve inspeção estática limitada e testes em máquina isolada sem anormalidades relatadas, mas isso **não elimina os alertas nem comprova a inexistência de malware**. Antes de uma Release pública, concluir verificação das dependências e do build final; contatar fornecedores se a evidência indicar falso positivo. Não recomende desativar proteções do sistema.

## Builds e proveniência

GitHub Actions compila o projeto em runner Windows hospedado pelo GitHub. O artefato é de **teste, não assinado**. A política de assinatura pública, se aprovada, deve exigir origem verificável, MFA e autorização manual antes de distribuir binários assinados. Não versionamos chave privada ou segredo de assinatura.

## Operações intencionalmente não automatizadas

Para reduzir o risco de dano, o projeto não se propõe a desligar serviços de
segurança, desativar integridade do Windows, apagar registros de diagnóstico
ou aplicar em massa ajustes de Registro e inicialização para “otimização”.
As ações administrativas disponíveis são específicas, visíveis e confirmadas.

## Integração nativa de proteção (v5.13.10.15 TESTE)

A consulta no Diagnóstico completo usa `WscGetSecurityProviderHealth` da biblioteca oficial `wscapi.dll`. Apresenta somente o estado agregado de proteção antivírus e firewall que o próprio Windows informa. Não identifica especificamente AVG, não executa varredura, não cria mecanismos de antivírus, não altera proteções nem substitui solução existente. `S_FALSE`, erros e estados não monitorados são classificados como indeterminados, nunca como evidência de infecção.

O AVG CyberCapture bloqueou uma compilação portátil anterior e a encaminhou para análise. Enquanto não houver parecer, **não tentar contornar o bloqueio, desativar o AVG, adicionar exceções globais ou distribuir a versão como aprovada**. Assinatura, dependências, proveniência e relatórios de falso positivo exigem verificação independente.
