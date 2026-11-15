# GERENCIADOR FINAL — Drivers F4.1 — Consulta WUA no GitHub Actions

Base F4 `94c8da88ee95316af4c5f9801bdd1f64d3b19886`, PR #80 Draft.
[CI Windows F4](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43664527373)
e [CI integridade F4](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43664527413).

## Disparo

Na página [Compilar Windows (sem assinatura)](https://github.com/walissonminel-1991/gerenciador-final/actions/workflows/build-windows.yml),
escolher **Run workflow** e a branch `experimento-atualizador-drivers-wua-20261114`.
O workflow já registrado na branch padrão chama a consulta da própria branch.
A consulta roda em `windows-2022` e `windows-2025`, com os argumentos de instalação F4 fora desse fluxo.
Cada artefato registra SHA exato, run, tentativa, runner, build e arquitetura Windows, contagens PnP e WUA e veredito.
[Regra de disparo do GitHub](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow).

## Leitura da evidência

- `NO_OFFERS` indica pesquisa online completa do Windows Update com zero ofertas de driver.
- `NO_ELIGIBLE_OFFERS` indica pesquisa completa com ofertas, nenhuma aprovada pelos gates F4.
- `ELIGIBLE_OFFER` indica oferta que passou pelos gates F4 antes de download. Compatibilidade do pacote, instalação e versão numérica remota seguem sem prova.
- `QUERY_FAILED` indica falha ou inconclusão de PnP, busca WUA ou revalidação. A etapa falha e preserva o relatório quando disponível.

Vínculos de ofertas com instâncias PnP preservam UpdateID e revisão e usam HMAC com chave aleatória descartada ao fim da consulta para a identidade pública da instância. O mesmo token tem sentido dentro de seu relatório. A correspondência da oferta com o dispositivo deve ser verificada de novo no inventário de cada job. IDs brutos, títulos, caminhos e nomes do dispositivo ficam fora da evidência.

A consulta usa o inventário e o cliente WUA de leitura existentes. Candidatos de classe permitida passam por `TryPrepare`, validação de entrada única e `Revalidate`. A revalidação lê propriedades de disponibilidade do instalador WUA em memória e não chama `Download`, `Install`, `AcceptEula` ou reinício.

## Resultado observado

Consulta manual [43672915368](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672915368), tentativa 1, concluída em sucesso.
SHA de código e consulta `9137b8e0673bdb46c0bea5a9f86b723f81fe543f`.

| Runner | Build Windows x64 | Entidades PnP | Linhas de drivers assinados | Itens presentes | Ofertas WUA | Vínculos | Elegíveis | Veredito |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Windows Server 2022 | 10.0.20348.0 | 59 | 61 | 59 | 0 | 0 | 0 | `NO_OFFERS` |
| Windows Server 2025 | 10.0.26100.0 | 66 | 67 | 66 | 0 | 0 | 0 | `NO_OFFERS` |

Os dois jobs terminaram em sucesso. Os dois inventários e as duas buscas online no Windows Update terminaram completos, sem HRESULT de falha. Os ZIPs contêm apenas `wua-f41.json`. O conteúdo conferido coincide com a evidência impressa nos jobs. SHA-256 dos ZIPs conferidos:

- [Job Windows 2022](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672915368/job/132686699222), [ZIP 14144752618](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672915368/artifacts/14144752618), `862beaec633853a07f78311dcfcfb75ced9c91c0cc1c46544a98c4578935733b`.
- [Job Windows 2025](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672915368/job/132686698946), [ZIP 14144393760](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672915368/artifacts/14144393760), `06cb29328079cc1bf67036357fd40638c70dcd33de3613d442544bf212f54f12`.

Os resultados valem para as duas imagens e dispositivos observados. Com zero ofertas, a revalidação de uma oferta real ficou sem exercício e não foram gerados pares UpdateID/revisão.

## CIs do código consultado

- [Windows 43672870413](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672870413). `completed/SUCCESS`. Drivers em Windows 2022/2025, 24/24 G1.9 em cada runner, regressões F1–F4, WinForms, 36/36 mutações negativas, política de segurança e privacidade, consentimento e compilação Windows x64 aprovados.
- [Integridade externa 43672870378](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43672870378). `completed/SUCCESS`. Verificação dos instaladores oficiais concluída.

Os dois runs de CI têm o SHA técnico como head. O merge sintético da PR no momento desses runs teve a mesma árvore de arquivos do SHA técnico, `3204ce0058c8abf243c0ef175fd09941bf9c14eb`.

## Instalação controlada

NO-GO para a primeira atualização real nesses runners. A busca retornou zero ofertas nos dois ambientes. Uma nova VM por job também deixa sem prova o snapshot restaurável independente exigido pelo executor F4. A etapa condicional de preparar o workflow de instalação ficou sem candidato. A consulta não fez download de driver, instalação, aceitação de EULA, mudança de política ou reinício.
[Referência dos runners hospedados](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

Roadmap 27/42. G1–G5 abertos. Escopo de escrita restrito à branch da PR #80.
