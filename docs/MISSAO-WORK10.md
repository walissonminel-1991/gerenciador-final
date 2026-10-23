# WORK10 — limpeza integrada à desinstalação

Checkpoint de implementação, 23/10/2026. Base: `e7b342934eb2c81add28b6f011c81cc5f4a64e99` (.51), CI anterior 40012294838 aprovada. PR #40 permanece Draft; main não é alterada.

## Comportamento implementado

A identidade e os locais exclusivos são registrados antes do desinstalador. Somente o resultado `RegistrationAbsent`, após saída zero e ausência estável, participa da investigação. Falhas, códigos não padronizados, reinício e cancelamentos não concedem limpeza. A seleção inteira recebe uma única confirmação SIM/NÃO, com aplicativos, contagens, alvos e preservações. Sem alvos elegíveis, não há pergunta. NÃO não executa exclusão, elevação ou reagendamento.

SIM usa exclusão permanente por handle para arquivos e pastas vazias; compara identidades dos arquivos e de toda a cadeia de diretórios e SHA-256 do conteúdo. O Registro usa as visões explícitas, fingerprints enquadrados e uma transação: alterações concorrentes abortam a exclusão, sem fallback não transacional. Nenhuma operação usa Lixeira, mudança de ACL ou tomada de propriedade. Quatro notificações do Registro invalidam o inventário durante a execução; a consulta completa ocorre no início/fim e quando o Windows sinaliza alterações.

O mecanismo UAC existente (`RunElevatedAsync` / `WorkerHost`) ganhou uma tarefa fixa e um pipe duplex. Planos somente em memória, frames limitados e revalidação independente no worker. Somente alvos já confirmados e recusados por permissão podem ser reenviados, em uma única elevação por lote. PID do cliente, token da sessão, executável e conta do servidor são conferidos. Recusar UAC preserva os alvos. Não há instrução de reabrir o GF como administrador nesse fluxo.

A interface antiga de investigação, pesquisa, filtros e limpeza independente foi retirada. Limpeza avançada passa a identificar a ferramenta nativa de limpeza do Windows que já existia; temporários, caches, Lixeira e discos permanecem. O cancelamento pertence à operação de desinstalação/limpeza. Na bandeja, o fluxo aguarda restauração antes da confirmação; cancelamentos descartam a pergunta.

## Cobertura e limites explícitos

- Receitas verificadas de perfis/Registro: Notepad++, SumatraPDF, 7-Zip, OBS, VLC, Audacity e foobar2000. Identidade inclui editor; uma correspondência de nome isolada não concede uma receita.
- Para outros aplicativos, há uma regra geral de pasta de instalação dedicada, em bases conhecidas, corroborada pelo comando registrado e pelo produto. Não há varredura do disco, escolha arbitrária de chaves nem geração de pastas de perfil por semelhança de nomes.
- Pastas pessoais, backups, projetos, gravações, arquivos pessoais e formatos desconhecidos são preservados. Conteúdo novo impede apagar diretórios; a exclusão de pastas nunca é recursiva.
- Locais personalizados fora das bases aceitas e integrações compartilhadas ainda podem ficar preservados. Cobertura progressiva não equivale a remover todos os vestígios de todo software.
- Elevação com credenciais de outra conta é recusada: inventário/perfil da sessão original não pode ser presumido no outro usuário. O motivo entra no diagnóstico. Execução normal usa `asInvoker` e continua portátil self-contained.
- CI pode executar o worker administrativo porque o runner já possui direitos. Isso não demonstra o clique humano no UAC nem substitui homologação no PC FORTE/PC FRACO, AVG ou SmartScreen.

## Validação neste checkpoint

Compilação local cruzada da aplicação, TestesLayout e TestesResiduos: zero avisos/erros. Política Python: 18 testes aprovados. Windows ainda precisa executar a nova bateria antes da candidata ser entregue.

Novos testes incluem: arquivos realmente removidos, NÃO com conteúdo preservado, nenhum alvo, lote com pendência, HKCU/HKLM 32/64, isolamento da visão HKLM32 equivalente ao Notepad++, alteração/conflito no Registro, reinstalação durante exclusão, cancelamento após um arquivo, bloqueio real de compartilhamento/permissão, conferência final, privacidade, IPC limitado e processo desinstalador PE real com entrada ARP descartável. Interface: 44 combinações de resolução/escala, confirmação única, botões antigos ausentes, bandeja e cancelamento.

Os casos de autorização/recusa do UAC são controlados na fronteira de autorização; o transporte e a exclusão do worker são exercitados em processo separado no Windows. Nenhuma instalação, desinstalação ou exclusão ocorre nos PCs pessoais.

Não concluir U8/R7/R4 nem marcar a missão integralmente concluída antes das evidências Windows e dos limites acima.
