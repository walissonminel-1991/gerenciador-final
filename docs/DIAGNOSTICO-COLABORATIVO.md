# GF Diagnóstico Colaborativo — planejamento e primeira implementação

Versão experimental v5.13.11.1. **Nenhum relatório é transmitido ao GitHub ou a outro serviço nesta versão.** O componente de recepção em `prototipos/telemetria-worker` não está implantado e não contém credenciais.

## Arquitetura

1. O GF mostra na primeira abertura um aviso claro de participação opcional, recusável, sem caixa selecionada por padrão. Sem concordância, nada é agregado para telemetria. O diagnóstico técnico local tradicional continua independente.
2. Com permissão **somente local**, registra apenas contagens de códigos pré-aprovados de diagnósticos; nunca linhas integrais dos logs. O resumo mostra o identificador da versão do GF e o tipo de encerramento. Não se transmite nome da máquina, usuário, lista de programas, serial, caminhos, comandos, IP, logs, stack traces ou ID estável do cliente.
3. No encerramento normal, o arquivo `%LOCALAPPDATA%\GerenciadorFinal\Privacidade\diagnostico-pendente-v1.json` é sobrescrito atomicamente (somente **um** pendente; não cresce indefinidamente). Nenhuma solicitação de rede aguarda o fechamento; o funcionamento do programa não depende disso.
4. Expiração local após **7 dias** e exclusão imediata do pendente quando o usuário desativa a opção. A preferência mínima é gravada no arquivo `consentimento-v1.json` para não repetir a pergunta a cada inicialização; não há identificadores ou cadastro. Pode ser alterada pela bandeja em **Privacidade e diagnóstico**.
5. **Fase posterior (não ativada):** uma nova opção separada autorizará especificamente transmissão automática de resumo agregado. Não presumir que consentimento para registro local permite envio remoto.
6. Futuro backend: Cloudflare Worker HTTPS + banco D1 privado, payload até 4 KB com esquema estrito, recusa de campos extras, rate limiting/WAF e cron diário para apagar registros com 7 dias. O código-fonte do servidor poderá ficar no GitHub; dados brutos não devem ir para repositório público.
7. Após a validação, é possível gerar no GitHub apenas **Issues agregadas de bugs**, revisadas para privacidade, sem registrar um arquivo por instalação. Não guardar token GitHub no aplicativo; se for necessária automação GitHub, o token será segredo do serviço do servidor com privilégios mínimos. GitHub Issues criadas não são eliminadas pelo TTL do banco automaticamente; precisam de política própria.

## Privacidade e conformidade

- Consentimento voluntário, informado, específico e revogável. Nenhum prejuízo funcional se recusar.
- O protocolo não inclui identificador estável para reduzir rastreamento. Isso **não garante anonimato completo**: padrões de contagem podem ser singulares, e o provedor HTTPS pode processar IP e metadados necessários à rede para entrega/antifraude.
- Configurar política transparente sobre entidade responsável, finalidade, período de retenção, infraestrutura, contato, países de tratamento e eventuais operadores antes de ativar a rede; avaliar LGPD, finalidade e bases legais.
- Sem identificador do cliente não será possível localizar com precisão um evento já transmitido para exclusão individual. A revogação interrompe o envio futuro e apaga o pendente; histórico remoto expira em até 7 dias. Informar esse limite antes do consentimento remoto.
- O formulário deve descrever separadamente dados técnicos, URL do destinatário e revogação e não pode usar opções pré-marcadas. Não iniciar uploads em `FormClosing` com `Task.Wait` ou bloquear a UI em falha de rede.
- No futuro, envio ao encerrar pode ter janela curta e sem bloqueio; se não concluir, nova tentativa no próximo início autorizado com TTL e tentativas limitadas, somente se o usuário aceitou expressamente **transmissão**.
- Não armazenar credencial de GitHub, tokens de criação de Issues, fingerprints da máquina nem receber logs brutos. Não usar a pasta pública de código como destino de dados de usuários.
- As regras do backend precisam impor rate limiting e proteção contra spam, pois um endpoint de envio não autenticado poderia ser abusado. **Não publicar o Worker antes dessas proteções e da avaliação de privacidade.**

## Campos do arquivo local

```json
{
  "schema": 1,
  "version": "v5.13.11.1",
  "exit": "normal",
  "counts": {
    "MSIX.LEITURA": 1,
    "REGISTRO_PROGRAMAS.FALHA": 2
  }
}
```

Exemplo fictício, não relatório real. O máximo é 100 eventos por categoria; existem 14 categorias autorizadas e cada registro não inclui parâmetros. O arquivo pendente é rotativo e mantido no máximo 7 dias. Não há envio nesta etapa.

## Próximos portões de aprovação

- [ ] CI Windows: TestesTelemetria com consentimento, whitelist, JSON, exclusão e TTL.
- [ ] Teste visual da janela escura em vários DPIs e funcionamento da opção na bandeja.
- [ ] Hospedagem independente configurada e política de privacidade revisada; comprovar segurança, retenção, limites de frequência e monitoramento de abuso.
- [ ] Novo consentimento específico para **envio automático ao encerrar** e teste de conexão com servidor controlado antes de habilitar publicamente.
- [ ] Ensaios com fechamento normal, crash, ausência de rede, dupla execução e revogação; sem atrasar o encerramento.
- [ ] Revisão do incidente AVG e testes reais antes de recomendar binários.
