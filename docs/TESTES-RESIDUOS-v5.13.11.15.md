# Roteiro supervisionado — resíduos v5.13.11.15 TESTE

Usar apenas a candidata com os três checks aprovados no mesmo código e conferir `BUILD-INFO.txt`/`SHA256SUMS.txt` do pacote. Esta build não elimina resíduos; não oferece opção para forçar exclusão. Backup continua necessário para testar o desinstalador oficial, cuja política de dados pertence ao fabricante.

## Bateria de leitura

1. Abrir no PC de histórico longo e anotar tempo até a interface ficar disponível, CPU/RAM e eventual alerta AVG. Não desativar proteção.
2. Em Resíduos de aplicativos, conferir pastas de fabricantes conhecidos e de um fabricante novo. Conferir que cada local tem decisão e motivo próprios. Comparar cobertura com .14; maior quantidade não é prova de lixo.
3. Conferir Aquiris/Bandai/Colossal/Crystal/Eidos/Guerrilla/nWay/Opera/Red Dot/Sucker Punch/Western Digital: preservar perfis e saves. Netease e Driver Booster permanecem sem exclusão.
4. Conferir VLC/foobar2000 ainda instalados: perfis não devem ser tratados como dados descartáveis. Na revisão avançada, qualquer perfil encontrado continua somente leitura. Preservar playlists, preferências, biblioteca de mídia e backups.
5. Tentar fechar durante a busca geral. O pedido cancela cooperativamente e orienta tentar fechar de novo; chamadas nativas de Registro/sistema podem demorar até retornar. Consulta limitada deve informar estado parcial.
6. Comparar os mesmos locais no segundo PC e em resolução menor, inclusive textos por item e botões.

## Desinstalação de aplicativo descartável — somente com autorização e backup

1. Escolher um aplicativo instalado exclusivamente para teste e registrar suas preferências fictícias antes do teste. Não usar programa com trabalho real.
2. Usar o desinstalador oficial e observar separadamente o fechamento do lançador, janelas/auxiliares e desaparecimento do registro. O desaparecimento no ARP não equivale ao término completo.
3. Na revisão, conferir locais e rótulos. Quando disponível, **Conferir novamente** deve repetir consulta sem excluir. Não existe botão de apagar resíduos.
4. Para validar mudança de conteúdo, usar somente fixture descartável. Após a prévia, mudar um arquivo fictício; a nova conferência deve preservar e exigir nova análise. Não editar executáveis reais para esse teste.
5. Conferir que reabrir o GF mantém perfis/arquivos preservados. Uma reinstalação/edição paralela deve impedir confirmar ausência pelo nome antigo quando houver MSI/identidade/caminho compartilhado.

## Pendências que esta bateria não libera

Exclusão real, propriedade exclusiva de arquivos, rastreamento completo de auxiliares/MSI/serviços, recuperação de dados, junctions nativas, ACLs complexas e concorrência adversarial. Um ponto de restauração não garante recuperação de dados pessoais. Não há envio remoto, manutenção automática por IA ou gestão destrutiva de drivers nesta candidata.

## Retomada técnica

Partir da branch `piloto-diagnostico-github-v5.13.11.2`, PR #40 Draft. Ler `MISSAO-RESIDUOS-v5.13.11.15.md` e o resultado final do CI. Próximo trabalho de código: coletar as divergências reais de descoberta/classificação antes de ampliar tarefas agendadas/integrações COM; manter os quatro pontos de entrada de exclusão bloqueados até desenhar autorização por alvos/conteúdo e executor seguro. Não reutilizar pontuação ou ausência no inventário como permissão.
