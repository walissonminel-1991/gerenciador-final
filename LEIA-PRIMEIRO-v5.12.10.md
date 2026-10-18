# Gerenciador Final v5.12.10 TESTE — expansão conservadora de cobertura

Versão experimental baseada na v5.12.9, mantendo o visual e o mecanismo de atualizações seguras anteriores.

## Fontes em conjunto (HTTPS GET, somente leitura)
- RuckZuck: catálogo com cache temporário exclusivamente em memória.
- GitHub Releases oficial: OBS Studio, Notepad++, PowerToys, PowerShell 7, **7-Zip**, **GitHub Desktop** e **SumatraPDF**, apenas quando fabricante e nome instalados são reconhecidos.
- Mozilla Product Details: Firefox estável e **Thunderbird Stable com canal explicitamente identificado**.
- Windows Update: continua separado, consultado na inicialização.

Nenhuma lista de aplicativos é transmitida aos provedores; cada requisição solicita apenas uma versão pública em um endereço fixo do próprio projeto. Não usa WinGet, COM, PowerShell, executáveis auxiliares nem scripts baixados.

## Prevenção de correspondências erradas
- 7-Zip exige fabricante Igor Pavlov e nome com versão e arquitetura identificáveis.
- GitHub Desktop exige nome exato e fabricante GitHub.
- SumatraPDF exige fornecedor conhecido; tags oficiais com sufixo `rel` são reconhecidas sem aceitar betas.
- Thunderbird: nomes genéricos não indicam se o canal é ESR. Portanto o motor **não sugere atualizações** para entradas ambíguas e só trata como Stable se o nome declarar `(Release)` ou `(Stable)`. Atualizações ESR serão incluídas apenas após validar um método próprio de identificação do canal.
- A versão oficial nunca é usada como comando de instalação. Ações reais continuam limitadas às receitas verificadas de Visual C++ 2013 e OBS.
- Se um provedor falhar, os demais permanecem disponíveis; uma resposta inválida não entra no cache.
- Resultados repetidos são consolidados, e uma versão já instalada não é reapresentada como pendente.
- O catálogo do GitHub faz até duas consultas simultâneas, apenas para projetos identificados; o cache dura dez minutos. A verificação manual atualiza as fontes.

## Diagnóstico local
O evento `MULTIFONTES.CONSULTA_OK` inclui agora `reconhecidos`, `fontes_consultadas`, `fontes_ok` e `fontes_oficiais`, além dos tempos. São contagens agregadas, não nomes de aplicativos ou dados pessoais.

## Testes e limitações
- CI Windows x64 executa comparação histórica, cache/cancelamento/concorrrência, segurança de receitas assinadas e novos casos de etiquetas oficiais, versões, fornecedores e ESR.
- O teste ao vivo faz apenas HTTPS GET de informações públicas, sem executar instaladores.
- A cobertura depende de quais aplicativos o computador tem instalados. Ter mais fontes não garante aumentar o número de atualizações encontradas.
- Compilação e testes não garantem que o AVG ou SmartScreen aceitarão o executável portátil não assinado no computador de destino. Não desative proteções para testar.

## Primeiro teste no computador
1. Extraia o artefato Windows x64 em uma pasta nova.
2. Abra com o AVG ativo e aguarde a busca automática.
3. Anote a quantidade de candidatos e a fonte indicada para cada um.
4. Faça uma única busca manual e confira os tempos.
5. Não instale ou desinstale nada neste primeiro ciclo.
6. Caso ocorra falha, envie o diagnóstico `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.10.log`.

Branch: `teste-cobertura-v5.12.10`.
