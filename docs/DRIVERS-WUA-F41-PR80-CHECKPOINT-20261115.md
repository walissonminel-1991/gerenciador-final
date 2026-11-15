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

Vínculos de ofertas com instâncias PnP preservam UpdateID e revisão e usam HMAC com chave aleatória descartada ao fim da consulta para a identidade pública da instância. O mesmo token tem sentido dentro de seu relatório. Titularidade da oferta em outro job precisa ser demonstrada de novo sobre o inventário desse job. IDs brutos, títulos, caminhos e nomes do dispositivo ficam fora da evidência.

A consulta usa o inventário e o cliente WUA de leitura existentes. Candidatos de classe permitida passam por `TryPrepare`, validação de entrada única e `Revalidate`. A revalidação lê propriedades de disponibilidade do instalador WUA em memória e não chama `Download`, `Install`, `AcceptEula` ou reinício.

## Instalação controlada

Nenhuma instalação real autorizada nesta rodada. Um job hospedado é uma VM nova, e o runner não fornece o snapshot restaurável independente exigido pelo executor F4. A decisão sobre preparar um workflow separado depende de uma oferta elegível observada. Uma eventual execução exigiria aprovação específica, revalidação da identidade naquele job e um ambiente que cumpra o contrato F4.
[Referência dos runners hospedados](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

Roadmap 27/42. G1–G5 abertos. Escopo de escrita restrito à branch da PR #80.
