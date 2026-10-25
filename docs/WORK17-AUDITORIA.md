# WORK17 — auditoria e linha de base

Base imutável: PR #52 Draft, `680ba3134b5191628ebc9ed83f163fa33b85dda4`, consultada em 25/10/2026, aberta, não integrada. A CI documental 40185290662 e a CI do código WORK16 40184303904 estão aprovadas. Branch WORK17 empilhada sobre WORK16; main preservada.

Gargalos observados antes da implementação:

1. `AutomaticUpdateDiscovery.QueryAsync` captura exceções ao redor do lote inteiro. Falha no aplicativo/manifesto N retorna listas vazias, descartando N−1 resultados. O limite de 512 também lança exceção global. Há isolamento entre provedores, mas falta dentro deste provedor.
2. Identificadores observados e ProductCode exato no índice assinado alcançam somente consulta quando o nome ARP difere. O PE e o cache MSI podem comprovar a identidade canônica, porém não existe rota para combinar essas provas.
3. `MsiGetProductInfo(LocalPackage)` não especifica contexto. A nova rota precisa consultar a instância por máquina ou pelo usuário atual, rejeitando publicidade sem instalação, ambiguidade e divergência de cache.
4. UpgradeCode igual não demonstra substituição: major upgrade requer tabela Upgrade e ações correspondentes. A nova rota não poderá autorizar um novo ProductCode sem verificar esse contrato.
5. O benchmark anterior tinha seis seleções de manifesto, não seis operações comprovadas. É necessário medir instalações com PE, contexto e revalidação, incluindo controles adversariais.

Linha de base reproduzível: `python scripts/measure_work17.py` compila o mesmo probe contra esta base e o commit candidato. São 22 cenários com arquivos PE inertes; cache MSI e assinante simulados explicitamente em ambas as cópias quando suportados. Os resultados deverão ser registrados após a CI Windows; nenhuma homologação física é presumida.

As limitações WORK16 do COM WinGet em usuário comum, execução Scoop/Chocolatey, Burn genérico e remoção MSIX permanecem independentes. Não serão substituídas por contagens de fixtures.
