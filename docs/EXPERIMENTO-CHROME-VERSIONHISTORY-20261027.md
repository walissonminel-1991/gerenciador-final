# Experimento isolado — Google Chrome VersionHistory, somente consulta

Data: 27/10/2026. Base: branch `work18-ampliacao-atualizacoes-seguras`, HEAD `2bc9f4228fd448445fbf181652e5af93005def20` (.65 TESTE). **Não mesclar, publicar EXE ou considerar homologado antes da CI e dos testes físicos autorizados.**

## Objetivo

Ampliar fontes independentes da WinGet COM por meio da API HTTPS pública e oficial [Google Chrome VersionHistory](https://developer.chrome.com/docs/web-platform/versionhistory/guide), sem CLI, COM, instalação, download de payload, scripts, alteração de registro, cookies ou telemetria.

### Critérios de aplicação (falha segura)

- Só `Google Chrome` com editor reconhecido (`Google LLC`, `Google Inc.`, `Google`) e canal `stable` ou não identificado. `Beta`, `Dev`, `Canary`, imitadores e fornecedores desconhecidos não herdam a identidade.
- `x64` consulta exclusivamente `win64/stable`; `x86` consulta exclusivamente `win/stable`. Com arquitetura local desconhecida, `arm64` ou conflituosa, **não consulta esta fonte**. Essa limitação deliberada evita sugerir uma versão de outra plataforma.
- URLs literais com HTTPS e origem Google exata, `order_by=version desc`, `pageSize=1`; redirecionamentos não são seguidos. Sem variável de caminho derivada de aplicativos.
- Corpo limitado a 128 KiB e timeout já impostos pelo `OfficialReleaseSources`; valida resposta com **exatamente uma versão de quatro componentes**, identificador `chrome/platforms/<plataforma>/channels/stable/versions/<versão>` correspondente. Um formato inesperado resulta em **fonte indisponível**, sem candidato.
- Cache em memória (TTL de 10 minutos), consulta concorrente deduplicada e isolamento de falhas da base existente.
- Resultado é **somente candidato consultivo**; não cria autorização de atualização, receita executável, limpeza/desinstalação, exceção à assinatura ou aumento fictício do roadmap.

## Testes acrescentados

`TestesMultifontes/ChromeVersionHistoryTests.cs` cobre: identidade/editor, canal, plataforma, versão distinta por arquitetura, cache/forceRefresh, URL alterada/falsa/HTTP, resposta vazia, múltipla, Beta, divergência, formato inválido, resposta HTTP 429 parcial e correspondência conservadora. Apenas handlers sintéticos, **sem rede real**.

## Revisão pré-BETA — Etapa 1 (27/10/2026)

A PR #63 continua Draft e intacta no HEAD
`04f59750739c6bdbaeb0bacd86ed2b734fba1271`. Sua
[CI 40507858102](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40507858102)
foi confirmada: seis jobs obrigatórios aprovados, dois condicionais skipped.
Os cinco arquivos revistos foram incorporados como alterações de trabalho na
branch `work-pre-beta-etapa0-20261027` / PR #64. Não houve merge de PR,
alteração da branch original do experimento ou da BETA privada.

### Defeitos reproduzidos e corrigidos

O modo `--pre-beta-fontes` executa 37 verificações sem rede. Antes das
correções, sete falharam: quatro respostas com propriedades JSON duplicadas
(inclusive chave escapada), duas versões inválidas (dígitos não ASCII e LF
final) e a propagação do JSON duplicado como resultado válido na fusão de
fontes. O parser agora recusa propriedades repetidas no objeto raiz e no item,
e exige quatro componentes numéricos ASCII, com início/fim absolutos.

Após a correção: **37/37** na revisão focal e **196/196** na suíte multifontes
completa, executadas em Linux com o SDK fixado **10.0.402**. O SDK foi obtido
do endereço oficial Microsoft e seu SHA-512 conferido pelos metadados oficiais.
O job de regressões Windows da
[CI 40514120889](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40514120889)
também aprovou **196/196 multifontes**, 368/368 atualizadores e 707/707
resíduos/desinstalação no SHA `8e195bfa3dfaab6767d877e78a2ad8d2b538f49c`.
**A CI integral falhou:** tentativa 1 recusou redirect HTTP do VLC; a única
repetição recebeu HTTP 403 de outro espelho HTTPS. O empacotamento x64 ficou
skipped pelo gate. [Evidência de CI](evidencias/pre-beta-etapa1-ci-40514120889.json).
Não há aprovação de candidata; a Etapa 1 permanece bloqueada no checkpoint.

O conjunto cobre identidade, canal, arquitetura, resposta paginada real,
host/credenciais/porta/query/fragmento, cache, uma requisição compartilhada
entre consultas concorrentes, cancelamento explícito e preservação dos
resultados Mozilla/GitHub quando Chrome falha por 429, 500, 302, JSON inválido,
duplicidade, resposta excessiva ou timeout. Nenhuma falha é convertida em
receita de atualização e nenhum redirecionamento é seguido.

### Serviço oficial e ganho mensurável

As duas URLs literais responderam **HTTP 200**, sem redirecionamentos, em
27/10/2026 ([horário, tamanho e hashes](evidencias/pre-beta-etapa1-api.json)). A resposta incluiu exatamente um item em `versions` e um
`nextPageToken`; o parser aceita a primeira página e não segue esse token.
Na amostra observada, `win64` e `win` informaram **157.0.8103.15**. São valores
consultados naquele horário, não versões fixadas no código. O modo explícito
`--chrome-live` também confirmou ambas as respostas pelo transporte/parser
reais do C#, em Linux; ele não roda no caminho comum de testes/CI.

A [referência oficial](https://developer.chrome.com/docs/web-platform/versionhistory/reference)
confirma plataforma/canal e ordenação numérica `order_by=version desc`.
O endpoint `versions` informa histórico de versões; disponibilidade de rollout
e compatibilidade de instalador não são comprovadas por esse resultado.

O mesmo probe, compilado contra a base imutável .65 e a revisão, usa sete
entradas sintéticas: Chrome x64, Chrome x86, Firefox e quatro controles
inelegíveis. Resultado: **1 → 3 descobertas/candidatos consultivos**, com
**0 → 2** candidatos Chrome e **0 → 0** falsos candidatos nos controles.
São duas arquiteturas de **uma família**, não dois produtos reais novos.
Reprodução: `python scripts/measure_pre_beta_sources.py`.
[Matriz com hashes dos snapshots](evidencias/pre-beta-etapa1-fontes-comparacao.json).

### Limitação de integração identificada

`Program.cs`, em `LoadUpdatesAsync`, preenche arquitetura a partir dos sufixos
do nome. A identidade Chrome aceita somente o nome exato `Google Chrome`,
que não contém esses sufixos. Portanto, a tela atual entrega `unknown` para
esse caso e **não consulta a nova fonte**. Não inferir x64 pelo Windows nem
admitir x86/x64 indistintamente para contornar isso.

O componente funciona quando recebe arquitetura conhecida; falta fornecer
evidência local de arquitetura ao adaptador da tela. Essa ligação fica
registrada para a Etapa 3 (cobertura/identidade), sem ampliar esta sessão para
o pipeline de execução. Não houve medição de ganho físico nos PCs e não se
afirma que o Chrome esteja instalado neles.

## Limites e próximos gates

1. Regressões Windows da revisão aprovadas, mas CI integral bloqueada pela integridade externa VLC. Retomar somente esse bloqueio com nova evidência, sem afrouxar HTTPS/hash/assinatura; não gerar candidata aprovada. A CI anterior da proposta #63 continua aprovada.
2. Teste ao vivo do serviço Google realizado em Linux por GETs fixos e pelo parser C#; não é homologação da aplicação em Windows nem de instalador.
3. Medir acréscimo real de cobertura nos inventários antes de prometer novos candidatos. Logs anteriores são anônimos e não permitem afirmar que o Chrome está instalado nos PCs.
4. Validar interação física e AVG somente se o código for aprovado para uma futura build TESTE; sem tocar nos computadores agora.

WinGet COM segue opcional e consultivo; o estudo não tenta repará-lo. Chocolatey comunitário não foi integrado porque o próprio provedor não suporta clientes de consulta OData próprios. Scoop já existe para leitura dos metadados locais de instalações gerenciadas, sem necessidade de novos scripts CLI.

Checkpoint oficial: **27/42** até revisão das evidências. Worker `gerenciador-final-beta-private`, D1 `gf-beta-private`, flags, DNS e EU.org permanecem fora de escopo. Main e as PRs BETA permanecem inalteradas.
