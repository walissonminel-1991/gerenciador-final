# Pesquisa avançada e otimização de RAM — v5.13.10.24

Data: 20/10/2026. Escopo: Windows 10/11 x64, portátil, sem serviços extras, sem administrador obrigatório, com AVG ativo.

## Fontes examinadas (projetos externos)

| Referência | Licença constatada | Técnica ou padrão encontrado | Decisão |
|---|---|---|---|
| [Mem Reduct](https://github.com/henrypp/memreduct) | GPL-3.0 | monitoramento de memória, ações de limpeza e interface portátil; usa API interna não documentada para working sets e listas de páginas; requer admin | Apenas referência conceitual; nenhuma linha copiada e nenhum uso de interfaces internas |
| [Windows Memory Cleaner](https://github.com/IgorMundstein/WinMemoryCleaner) | GPL-3.0 | execução periódica ou por limite de RAM disponível, histórico e controles | Modelo funcional de gatilhos; política implementada do zero sem copiar código |
| [RAM Cleaner Flusher Pro](https://github.com/alisakkaf/RAM-Cleaner-Flusher-Pro) | README declara MIT; não auditamos integralmente dependências | exemplos nativos de limpeza global | Não copiar trechos ou scripts nem incluir dependências externas |

Licenças foram verificadas nos arquivos LICENSE dos dois projetos GPL. Direitos de terceiros e obrigações de redistribuição precisam ser respeitados; não se fez fusão literal de código GPL com o projeto MIT.

## Documentação oficial (fundamentação)

- [GlobalMemoryStatusEx/MEMORYSTATUSEX](https://learn.microsoft.com/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex): memória disponível inclui páginas standby, livres e zeradas, reutilizáveis imediatamente. Não é correto denominá-las lixo.
- [CreateMemoryResourceNotification](https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-creatememoryresourcenotification) e [QueryMemoryResourceNotification](https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-querymemoryresourcenotification): sinalização oficial, não bloqueante, de baixa memória no Windows.
- [SetProcessWorkingSetSize](https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-setprocessworkingsetsize): manipular working sets afeta paginação e desempenho; não adotado.
- [GCMemoryInfo](https://learn.microsoft.com/dotnet/api/system.gcmemoryinfo.highmemoryloadthresholdbytes): o GC já considera situações de pressão; não forçar coletas periódicas arbitrárias.

## Implementação independente desta rodada

- `WindowsLowMemorySignal`: objeto nativo de notificação de baixa memória, encerrado em Dispose; não requer serviço, thread extra nem elevação.
- `SafeMemoryOptimizationPolicy`: automática desativada até ação explícita do usuário; exige **3 amostras de pressão** (Windows sinalizou baixa memória ou <= 15% disponível) e **cooldown de 15 minutos**.
- Ações automáticas/manuais limitadas a buffers **do próprio aplicativo**: texto antigo do painel de registro (somente se exceder 50.000 caracteres, preservando 16.000 recentes) e redução do histórico gráfico a 30 amostras. Não modifica processos, arquivos pessoais, cache de standby, registro ou serviços do Windows.
- Consulta global RAM continua na bandeja, sem criação de threads ou enumeração de processos a cada leitura.
- O contador exibe **bytes estimados de buffers de texto descartados**, não memória física de RAM recuperada; os itens do histórico reduzidos não entram na soma, pois não há medida direta.
- Não usar `NtSetSystemInformation`, `EmptyWorkingSet`, `SetProcessWorkingSetSize`, `GC.Collect` cíclico, PowerShell, WinGet, drivers ou elevação para limpeza.
- Botão `Tarefas do Windows` preservado.

## Limites e aprovação

Essa técnica reduz consumo potencial do **próprio programa**, não promete aceleração nem libera magicamente RAM alheia. Em uma sessão normal, é esperado **0 KB** de cache elegível. Um número positivo só indica descarte de texto interno, não queda equivalente no uso físico do Windows.

- [ ] Build Windows aprovado.
- [ ] Regressões de layout, política, cooldown, cache, inventário MSIX e resíduos aprovadas.
- [ ] Revisão humana de segurança e avaliação do AVG `IDP.Generic` concluídas antes de rodar no PC FRACO.
- [ ] Testes em hardware com AVG ativo: bandeja, gráficos, uso de CPU/RAM, evolução do indicador e resposta sob memória baixa.
- [ ] Considerar novas técnicas somente após medir melhoria perceptível sem paginação adicional e sem violar licenciamento.
