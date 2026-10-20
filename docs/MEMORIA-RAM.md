# Memória RAM — painel da v5.13.10.23

## O que foi implementado
- Substituição da área **Processos** por **Memória RAM**, mantendo o atalho para o Gerenciador de Tarefas do Windows.
- Histórico visual de até 90 leituras da memória física do Windows, sem gravar nomes de programas ou PIDs.
- **RAM em uso** = memória física total menos memória física imediatamente disponível.
- **Disponível** = `MEMORYSTATUSEX.ullAvailPhys`, que inclui as listas standby, livre e zerada, prontas para reutilização automática pelo Windows.
- **Liberada pelo Gerenciador Final desde a abertura**: inicialmente 0 MB, sem estimar ganhos a partir da oscilação da memória disponível.
- Leitura a cada 2 s com a aba Memória RAM visível, 12 s em outras áreas e 20 s na bandeja. A série continua sendo coletada em segundo plano.
- O projeto não compila a antiga grade de CPU/RAM por processo nem seu coletor que abria handles de processos.

## O que **não** foi implementado
Não há limpeza automática de RAM do sistema nesta versão. Liberar o cache standby à força, tentar reduzir working sets de terceiros ou forçar coleta de lixo em intervalos fixos não apresenta garantia de melhora e pode piorar a fluidez; poderia também introduzir novos riscos de compatibilidade.

O indicador **disponível** não deve ser rotulado como "lixo" ou "memória que o Gerenciador pode limpar": o Windows já pode reutilizá-la conforme a demanda. O campo **liberada pelo GF** não recebe números estimados ou inventados.

## Fase futura: otimização segura
1. Medir se o próprio Gerenciador mantém caches desnecessários sob pressão real de memória.
2. Liberar somente objetos temporários que o próprio programa controla, de forma incremental, nunca dados do usuário, perfis ou working sets alheios.
3. Medir antes/depois: heap gerenciado, working set, memória disponível no sistema, e impacto em CPU/tempo de resposta. Registrar apenas a métrica comprovada de cada operação; não somar variações causadas pelo Windows como se fossem limpeza.
4. Oferecer ativação explícita para eventual otimização periódica e permitir desligar. Não executar ações agressivas automaticamente.
5. Validar em ambos os PCs, com AVG habilitado e após análise do incidente de segurança.

Fonte técnica: Microsoft Learn, MEMORYSTATUSEX (ullAvailPhys é standby + livre + zerada).
