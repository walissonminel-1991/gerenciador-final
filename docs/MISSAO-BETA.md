# MISSÃO BETA — participação e diagnóstico remoto seguro

Status: **PR #61 Draft. CANDIDATA .66 EM PREPARAÇÃO, NÃO DISTRIBUIR.**
Base: v5.13.11.65 TESTE, WORK18 PR #60, HEAD `2bc9f4228fd448445fbf181652e5af93005def20`.
Branch: `beta-piloto-consentimento-envio-20261026`.
A última candidata tecnicamente aprovada continua a .65.

## Requisitos funcionais do piloto

- Remover o seletor de perfis **PC FORTE/PC FRACO** do sistema na edição BETA.
- Remover o botão **Configurar privacidade/Ativar coleta** do cabeçalho. Não ativar coleta por padrão: apresentar autorização remota separada na primeira abertura; sem botão de opt-in persistente.
- Preservar acesso de **revogação e explicação** na bandeja (Privacidade e diagnóstico) e direito de recusar sem perda de funcionalidades.
- Só depois do aceite enviar um resumo no fechamento normal, HTTPS, 202 explícito de confirmação, sem redirecionamentos, timeout curto; erro deixa no máximo uma pendência, com TTL local de sete dias.
- Nunca transmitir log bruto nem inventário: somente schema 1, versão, tipo de encerramento e contagem em 14 categorias técnicas predefinidas, máximo 4096 bytes. Sem identificador persistente do usuário/dispositivo.
- Envio direto do aplicativo para **um receptor privado HTTPS**, nunca para GitHub REST com token dentro do EXE. O backend fará agregação por dia/versão/código em banco D1 privado e apenas sumários revisados serão gravados em **repositório GitHub PRIVADO**, não Issues públicas do projeto.

## Bloqueios obrigatórios antes da distribuição

1. Aprovar controlador/contato LGPD, aviso legível, finalidade, base legal, revogação, transferências internacionais e retenção. Confirmar consentimento em versão antiga LocalOnly **não** autoriza RemoteBeta.
2. Implantar receptor privado com autenticação/controle de abuso, validação server-side estrita de payload, WAF/rate limiter, limites e TTL 7 dias. Verificar não armazenamento de IP, corpo integral, usuário, caminhos ou listas de aplicativos em logs.
3. Configurar credencial GitHub App de privilégio mínimo **somente no servidor**, restringir ao repositório **privado**, agregação diária/deduplicação, sem PRs automáticas, Issues públicas, anexos ou conteúdos livres de usuários.
4. Fornecer URL pública do receptor no build aprovado como `-p:GfBetaEndpoint=https://<receptor-validado>/ingest`; sem esta propriedade o cliente não envia e o botão de aceitação fica desabilitado. URL não é segredo, token nunca é distribuído.
5. Testar recusa, consentimento anterior local, novo consentimento remoto, revogação, fecho na bandeja, erro 403/429/500, timeout, sem rede, POST 202, reporte adulterado, retry limitado, concorrência, ausência de respostas e desligamento sem travar a UI. **Não afirmar que já houve testes de envio real**.
6. Executar CI Windows completa, negativa por mutação, política Python/Node, build self-contained x64 e atestado do ZIP selecionado. Testar AVG ativo, Startup/Bandeja e DPI antes de convidar terceiros.
7. Pilotar primeiro com **1–2 voluntários** em Windows descartável, expandir somente após avaliar alertas e taxa de sucesso, e publicar página de privacidade e opt-out. Não publicar BETA antes disso.

## Restrições

Não subir material bruto ao GitHub público, não incluir PAT/token no aplicativo, não exigir GitHub de voluntários, não esconder o envio, não rastrear instalações individualmente, não permitir telemetria remota por LocalOnly anterior, não desativar AVG, não contar CI como homologação física. `main`, PR #60 e checkpoints do roadmap 27/42 intactos.
