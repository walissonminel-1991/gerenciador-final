# Aviso de privacidade — GF v5.13.11.66 BETA

Versão do aviso: **gf-beta-20261026-r3**. Preparado em 26/10/2026 para aprovação humana. **Ainda não autoriza participação ou distribuição.** O texto apresentado no aplicativo está em `TelemetryConsentPolicy.RemoteNotice`; seu SHA-256 é calculado sobre os bytes UTF-8 desse texto, sem normalizar espaços. O hash deste Markdown é outro e não autoriza o aplicativo.

## Responsável e contato

Responsável pelo tratamento proposto: **Walisson Minel**, a confirmar pelo proprietário antes de disponibilizar a BETA. **Canal privado de contato: PENDENTE DE DEFINIÇÃO E APROVAÇÃO.** O mesmo canal deve ser informado aqui, na janela de consentimento e nas instruções aos avaliadores. Não publicar CPF, endereço residencial ou credenciais como parte desta identificação.

Você poderá usar esse canal para perguntar sobre o tratamento, solicitar confirmação, acesso, correção, informações sobre compartilhamento, eliminação quando cabível, oposição ou revogação e demais direitos aplicáveis. Não envie senhas, inventário ou logs brutos para exercer seus direitos. O responsável deverá verificar cada pedido, explicar as limitações técnicas e atender segundo os requisitos legais aplicáveis. Permanece disponível o direito de petição à ANPD.

## Sua escolha

Participar é opcional. O botão **Aceito envio automático** pede consentimento específico para transmissão remota; nenhuma opção vem marcada. Recusar mantém as funções do GF disponíveis, inclusive o uso local sem internet. Recursos que consultam serviços de atualização continuam dependendo de conexão.

Você não precisa de conta GitHub, login, navegador ou formulário para essa coleta. Quando autorizado, o programa tenta enviar o resumo ao sair, com prazo de aproximadamente 1,7 segundo e uma tentativa por fechamento. Minimizar e restaurar pela bandeja não são encerramento e não provocam envio.

Para revogar, abra **Bandeja > Privacidade e diagnóstico > Não participar / Desativar**. Isso interrompe novas contagens desse recurso, cancela a tentativa de envio e tenta excluir a pendência. Se o disco impedir salvar a escolha ou excluir o arquivo, o programa informa a falha; a recusa permanece efetiva na instância atual e precisa ser revista ao reabrir. Uma requisição já recebida pelo servidor não pode ser retirada pelo computador.

## Dados e finalidade

A finalidade proposta é identificar frequências de falhas técnicas para melhorar estabilidade e compatibilidade. O payload automático tem no máximo 4096 bytes e contém somente `schema`, versão do GF, encerramento normal ou incompleto e contagens entre 1 e 100 de até 14 códigos existentes:

| Grupo | Códigos permitidos |
|---|---|
| Aplicação/interface | `APP.FALHA_INTERFACE`, `APP.FALHA_NAO_TRATADA` |
| Leitura de aplicativos | `REGISTRO_PROGRAMAS.FALHA`, `MSIX.LEITURA` |
| Consultas de atualização | `MULTIFONTES.CONSULTA_FALHA`, `MULTIFONTES.CONSULTA_CANCELADA` |
| Instalação/desinstalação | `INSTALACAO_OFICIAL.FALHA`, `DESINSTALACAO.FALHA`, `TERMINAL_GITHUB.FALHA` |
| Bandeja | `JANELA.BANDEJA_RESTAURAR.INICIO`, `JANELA.BANDEJA_RESTAURAR.FIM`, `JANELA.BANDEJA_OCULTAR.INICIO`, `JANELA.BANDEJA_OCULTAR.FIM` |
| Diagnóstico | `DIAGNOSTICO_COMPLETO.FIM` |

`MSIX.LEITURA` conta ocorrências de leitura; não é, isoladamente, prova de falha. As contagens não informam quais aplicativos estavam instalados ou falharam. Não há inventário, logs brutos, textos livres, nomes de programas, arquivos, caminhos, credenciais, identificadores de hardware, ID de sessão ou identificador permanente de participante. Nenhum novo campo/categoria foi acrescentado nesta rodada. O valor preexistente `incompleto` passa a ser aceito também pelo receptor; o aviso r3 descreve isso e invalida consentimentos de avisos anteriores.

Falhas fatais e desligamentos tentam preservar apenas esse resumo local para uma sessão posterior autorizada. Queda de energia, encerramento forçado e falhas antes do consentimento ou antes de gravar podem não produzir dados. Uma resposta perdida pode causar reenvio posterior. Portanto, **relatórios, eventos e participantes são medidas diferentes**; não deduzimos o número de pessoas ou computadores.

## Destinatários, acesso e retenção

| Local/destinatário | Tratamento proposto | Prazo e limite |
|---|---|---|
| Computador | Uma pendência rotativa e registro mínimo de escolha/versão/hash do aviso | Pendência válida até 7 dias após a gravação, substituível por resumo novo; expirados não são enviados. Limpeza ocorre quando o GF volta a executar. A preferência permanece até mudar/revogar. |
| Cloudflare Worker/D1 | Recebe HTTPS e soma contagens por dia UTC, versão e categoria | Agregados vencem em `(dia UTC do recebimento + 7) × 86400`; manutenção tenta apagar os vencidos. IP não é coluna do D1. |
| GitHub privado | Recebe síntese diária por versão/categoria, com omissão abaixo de 5 e arredondamento para múltiplos de 5 | Mesmo vencimento do agregado, sem renovar na exportação. Exclusão definitiva de Issues na manutenção seguinte, com confirmação positiva. |
| Controle de exclusão | IDs das operações/Issues e estados, sem vínculo com participante | Conservados enquanto houver dívida de exclusão, inclusive após vencimento, até confirmação ou revisão documentada. |

Cloudflare processa IP e metadados de rede para entregar a conexão e proteger o serviço. O GF não grava IP no D1 nem o envia no payload ao GitHub. Configurações de segurança, eventos de WAF, logs de plataforma, backups e outras retenções dos provedores precisam ser avaliadas separadamente; apagar uma linha ou Issue não comprova apagamento de todas essas cópias. O Time Travel do D1 é automático e a documentação indica janelas de 7 dias no Free e 30 no Paid; confirmar o plano efetivo antes de publicar este aviso. Para GitHub, não há prazo contratual de expurgo de todas as cópias confirmado nesta missão.

Somente o responsável e administradores expressamente autorizados devem acessar D1 e o repositório privado `walissonminel-1991/gerenciador-final-beta-agregados`. O público não dispõe de rota para consultar o banco ou ler os agregados. Contas administrativas dos provedores e seus subprocessadores seguem também os contratos aplicáveis. Restringir notificações e proibir cópias informais de conteúdo; não reutilizar os agregados em ferramentas de IA ou outros serviços sem revisão específica.

Falhas de rede, credencial, cron, quotas ou comprovação de identidade podem atrasar a exclusão. O vencimento de sete dias é a política operacional do GF, **não uma garantia de desaparecimento de todas as cópias no sétimo dia**. Os atrasos são monitorados e exigem resposta do responsável. Sem IDs individuais não conseguimos localizar uma contribuição dentro de uma soma; a revogação não remove seletivamente contagens já agregadas, que continuam sob a retenção informada. Não coletaremos identificadores adicionais apenas para tentar reconstruí-las.

## Transferências e avaliação humana pendente

Cloudflare e GitHub podem processar dados fora do Brasil. Antes do piloto, o responsável deve registrar entidades contratadas, regiões/configurações efetivas, subprocessadores, retenções de segurança/backups e mecanismo aplicável às transferências. O aceite da coleta não substitui essa avaliação. A base proposta para a contribuição voluntária é consentimento específico; a suficiência desse consentimento e dos contratos/mecanismos depende de revisão humana. Testes automatizados não declaram conformidade jurídica definitiva.

Contagens de grupos pequenos podem permitir inferências. Supressão/arredondamento não equivalem a anonimato garantido nem a cinco participantes. Não usar os resultados para perfilar, identificar ou contar pessoas. Sugestões qualitativas serão recebidas separadamente em canal privado aprovado; não fazem parte do payload automático e terão aviso/retenção próprios.

Os históricos e registros técnicos **locais** preexistentes do GF estão descritos em `PRIVACY.md`. Eles não são anexados a esse envio. A revogação deste recurso não modifica configurações do Windows ou serviços de atualização.

## Referências para a aprovação

- [LGPD, texto compilado](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm), especialmente direitos, consentimento e transferências.
- [ANPD — Resolução 19/2024](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-19-de-23-de-agosto-de-2024).
- [Cloudflare DPA](https://www.cloudflare.com/cloudflare-customer-dpa/) e [D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/).
- [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

Antes de aprovar: preencher contato, confirmar responsável, revisar fornecedores/transferências/retenção e inserir o texto final no aplicativo; recalcular hash e registrar aprovações em `docs/BETA-APROVACOES.json` e `BetaTelemetryPolicy.props`. Mudança do texto exige novo hash e novo consentimento. Os campos pendentes bloqueiam a candidata conectada.
