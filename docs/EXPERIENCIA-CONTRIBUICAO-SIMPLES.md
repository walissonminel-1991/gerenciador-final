# Experiência de contribuição — do teste local ao envio futuro

## Meta do produto

Pessoas com pouca experiência em informática devem conseguir ajudar a melhorar o GERENCIADOR FINAL sem procurar logs, escolher arquivos, copiar mensagens técnicas, criar Issues ou utilizar o GitHub. Recusar a participação nunca restringe funções do programa. GitHub continua como **ferramenta interna da manutenção**, não como requisito da interface do usuário final.

## Implementado: v5.13.11.8 TESTE

- A janela de privacidade explica os dados em linguagem simples, com estado explícito **COLETA DESATIVADA** ou **COLETA LOCAL ATIVA — NENHUM ENVIO**.
- O botão **Simular sem enviar** funciona com ou sem consentimento: monta contagens completamente fictícias e verifica o mesmo esquema restrito do diagnóstico (`schema,version,exit,counts`).
- A simulação exibe exemplos em português e avisa que nenhum dado real do computador, arquivo, credencial ou histórico foi utilizado. **Não acessa rede, não salva arquivos e não altera consentimento**.
- O consentimento local continua opcional, pode ser recusado ou revogado e não é automaticamente convertido em consentimento remoto.
- Não existe envio real, conta GitHub solicitada, instalação de drivers ou manutenção por IA associada ao botão.

## Objetivo da experiência final — ainda NÃO implantado

Fluxo esperado:

1. **Convite simples:** "Quer ajudar a melhorar o Gerenciador Final? Enviaremos apenas contagens técnicas anônimas, sem arquivos pessoais." Opções igualmente claras: "Participar" e "Agora não".
2. **Explicação verificável:** mostrar quais categorias são necessárias, por quê, destino do envio, retenção e que o serviço de rede pode conhecer metadados de conexão. Não afirmar anonimato absoluto.
3. **Consentimento específico de envio:** separado do consentimento de gravação local; desativado por padrão. Sem anexos, texto livre, log completo ou seleção de arquivos.
4. **Envio com mínimo esforço:** somente pacote gerado internamente, validado antes de transmitir, para um receptor HTTPS autenticado e com limite de requisições. Não exigir conta GitHub do usuário comum. Mostrar confirmação verdadeira do servidor e opção de desativar.
5. **Manutenção desacoplada:** receptor agrega contagens técnicas em área privada, exclui dados temporários conforme política, publica somente problemas resumidos e verificados para manutenção no GitHub. Texto externo nunca vira comando, PR ou release automaticamente. Agentes trabalham em branches e requerem CI, revisão humana e testes em PCs antes de publicar.

Um serviço receptor será necessário para receber contribuições sem exigir conta GitHub do usuário. **Cloudflare não é uma obrigação**, mas o GitHub sozinho não deve ser usado como caixa de entrada pública sem controle contra abuso. Não embutir token ou segredo no EXE.

## Critérios para permitir teste real de envio

- [x] Simulação local sem rede, sem arquivos e sem consentimento.
- [x] Coleta local com opção de recusar e revogar.
- [x] Política rígida de categorias e validação de campos.
- [x] O executável não oferece anexos, Issues manuais ou upload genérico.
- [ ] Definir receptor seguro sem cadastro do participante, autenticação antiabuso e segregação das credenciais.
- [ ] Aprovar política LGPD, aviso de privacidade, retenção, exclusão e consentimento específico de envio.
- [ ] Testar com destino simulado: aceita campos permitidos; rejeita uploads, texto, tamanho excedido, duplicações e respostas adversas.
- [ ] Exigir proteções de branch e revisão humana de builds.
- [ ] Validar experiência real, layout 1280×720 a 8K, conexões offline, antivírus AVG, UAC e máquinas de teste.
- [ ] Revisar segurança e autorizar implantação gradual antes de distribuição.

**Não ativar transmissão com base apenas nos testes locais ou na simulação.** A simulação só confirma que o usuário pode experimentar a interface sem enviar nada.
