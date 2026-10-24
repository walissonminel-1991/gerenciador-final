# Benchmarks reproduzíveis — Work 3

Ambiente local Linux x64, SDK 10.0.402, runtime 10.0.13. Processos separados, execução sequencial sem compilar durante a medição. Mesma fixture/rotina, aquecimento inicial e três iterações por caso; tabela usa medianas. Base exata: `82259f4301758eee638422fd425f8fb41371086b`; implementação progressiva do marco M3. Não representa o Windows nem o PC FRACO e não comprova ganho percentual.

| Apps no inventário | Evidências recebidas | .16 ms | M3 ms | .16 MiB alocados | M3 MiB alocados | Grupos retornados .16 / M3 |
|---:|---:|---:|---:|---:|---:|---:|
| 500 | 5000 | 45.31 | 46.72 | 18.91 | 21.76 | 100 / 2500 |
| 1000 | 10000 | 79.73 | 113.24 | 37.79 | 43.51 | 100 / 5000 |
| 1000 | 20000 | 187.76 | 135.04 | 75.19 | 86.06 | 100 / 10000 |
| 1000 | 50000 | 147.12 | 130.50 | 109.92 | 124.11 | 100 / 15000 |

A saída maior tem custo de retenção/alocação: agora todos os grupos classificados ficam disponíveis, sem corte em 100. Não comparar apenas tempo ignorando a cobertura. Em 50.000 evidências, **ambas** examinam no máximo 30.000 e informam as 20.000 restantes como não avaliadas. Paginação não inventa esses resultados. O caso de 20.000 evidências representa 10.000 candidatos fictícios, além dos casos com 500/1.000 aplicativos.

Dados completos: [base](benchmarks/WORK3-base-v5.13.11.16.jsonl) e [progressivo](benchmarks/WORK3-progressivo.jsonl), incluindo CPU em ms, heap antes/depois, RAM antes/depois, pico do processo e alocações. Pico é acumulado desde o início do processo, não um pico isolado da consulta. Heap após GC ainda mantém o resultado vivo. Alocação total inclui objetos transitórios e não significa RAM retida. O benchmark de navegação separado existe apenas na versão nova; confirma um filtro por sequência de páginas, não uma comparação inexistente com a .16.

## Reproduzir

Na revisão nova: `dotnet run --project TestesResiduos -c Release -- --benchmark`. Para comparação, criar checkout separado da base .16, copiar **somente** `TestesResiduos/ResidualBenchmark.cs` e inserir `if (args.Contains("--benchmark")) { ResidualBenchmark.Run(); return; }` antes dos testes no Program.cs desse projeto. A base não define GF_PROGRESSIVE e executa a mesma parte de classificação, sem referências a tipos novos. Compilar ambos antes; executar novamente com `--no-build` de forma sequencial. Não alterar asserções ou fontes da base.

## Métricas locais no aplicativo

Linhas `DESEMPENHO.LOCAL` usam somente enum de etapa e números: Startup, Inventory, Updates, ResidualDiscovery, ResidualClassification, ResidualFilter e ResidualInspection. Sem nomes de programas, caminhos, conteúdo, identificadores de máquina ou envio. Startup mede início interno até primeiro Shown, antes do consentimento; não mede tempo do Windows anterior ao processo. Discovery é a consulta total, incluindo classificação; a linha Classification mede separadamente a parte de correlação. RAM/CPU/alocação são do processo e podem incluir tarefas concorrentes; campos indisponíveis não são tratados como zero. Sem GC forçada na aplicação, thread por candidato ou cache persistente de perfis.
