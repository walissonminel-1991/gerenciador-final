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

## Limites e próximos gates

1. Compilação e suites Windows/CI com suas demais regressões (ainda pendentes até o resultado da PR).
2. Teste ao vivo do serviço Google em ambiente isolado; a API/documentação pública está confirmada, **mas o endpoint não foi validado neste experimento**.
3. Medir acréscimo real de cobertura nos inventários antes de prometer novos candidatos. Logs anteriores são anônimos e não permitem afirmar que o Chrome está instalado nos PCs.
4. Validar interação física e AVG somente se o código for aprovado para uma futura build TESTE; sem tocar nos computadores agora.

WinGet COM segue opcional e consultivo; o estudo não tenta repará-lo. Chocolatey comunitário não foi integrado porque o próprio provedor não suporta clientes de consulta OData próprios. Scoop já existe para leitura dos metadados locais de instalações gerenciadas, sem necessidade de novos scripts CLI.

Checkpoint oficial: **27/42** até revisão das evidências. Worker `gerenciador-final-beta-private`, D1 `gf-beta-private`, flags, DNS e EU.org permanecem fora de escopo. Main e as PRs BETA permanecem inalteradas.
