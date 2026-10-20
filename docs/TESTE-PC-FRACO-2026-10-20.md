# Bateria PC FRACO — evidências de 20/10/2026

Status: **em investigação; não aprovada**. Este relatório contém apenas contagens técnicas e ações relatadas, sem perfis de usuário, dados identificáveis, nomes de dispositivos ou caminhos absolutos.

## Segurança — P6 (bloqueio AVG)
- Durante a execução de v5.13.10.20, após minimização para bandeja e tentativa de restauração por duplo clique, o AVG exibiu a detecção comportamental `IDP.Generic`, realizou quarentena e interrompeu o uso normal.
- O log v5.13.10.20 termina após `DIAGNOSTICO_COMPLETO.FIM`, sem `APP.ENCERRAMENTO_NORMAL`. Isso é compatível com interrupção, mas não revela a causa da classificação.
- Uma coincidência temporal com a restauração da bandeja não comprova causalidade.
- Não restaurar/autorizar exceções nem repetir execução no PC até avaliação de integridade e resposta do antivírus. Preferir submissão para análise oficial pelo AVG.
- v5.13.10.21 adiciona apenas diagnóstico de transição da janela; não contorna o antivírus. **O bloqueio AVG não está resolvido.**

## Inventário — I7
| Medida | v5.13.10.19 | v5.13.10.20 |
|---|---:|---:|
| Inventário total após desinstalação | 74 | 55 |
| Apps MSIX agregados | 19 | 0 |
| Status leitura MSIX | ok | COMException |
| Erros de leitura Win32 | 0 | 0 |

- A falha de enumeração MSIX foi detectada na segunda versão, não se sabe ainda se temporária ou relacionada ao ambiente.
- v5.13.10.21 passa a gravar o HRESULT sem mensagens sensíveis e contabiliza a falha. Não presume inventário completo quando MSIX indisponível.
- O Driver Booster deixou de aparecer na listagem de apps após nova atualização da lista (evidência das capturas).

## Resíduos — R6/R7
- A primeira revisão pós-desinstalação observou um local e ainda viu registro relacionado; a confirmação do desinstalador pode ter ocorrido antes da remoção final.
- v5.13.10.20 reconheceu **3 locais específicos** de Driver Booster (duas pastas de dados e uma chave de Registro), **somente leitura**. Não comprova autorização para excluir.
- Outros grupos históricos examinados: GOG.com, IObit, McAfee, Microvirt — preservar enquanto a autoria/exclusividade estiver incerta.
- A análise geral da v5.13.10.20 foi **parcial** porque o inventário MSIX falhou. Ausência no inventário não prova remoção.

## Drivers — D4
- Consultas v5.13.10.19 e v5.13.10.20: 107 associações, 49 INFs, três sem INF válido; sem novos/alterados/não observados desde a linha de base.
- O arquivo local `drivers-v1.json` contém esquema 1, 107 entradas resumidas e nenhum evento de mudança. Evitar compartilhar as identificações hash do histórico.
- É necessário comparar exemplos diretamente ao Gerenciador de Dispositivos; não inferir limpeza de pacotes.

## Interface — V5
- O título da janela continuava mostrando v5.13.10.18 nas compilações posteriores por ser texto estático. Corrigido em código na v5.13.10.21.
- A rolagem clara da aba DRIVERS foi corrigida no lote v5.13.10.19; as barras de outras janelas deverão ser conferidas quando a execução voltar a ser segura.
- Nenhum teste real de desempenho ou restauração pode ser aprovado enquanto persistir a detecção.

## Ações seguintes
1. Preservar quarentena, logs e referência ao alerta; enviar para análise oficial AVG.
2. Inspecionar integridade/procedência do pacote e resultados de ferramentas de segurança, sem recorrer a exclusões.
3. Após liberação/revisão, validar v5.13.10.21 em ambiente controlado e comparar HRESULT MSIX.
4. Confirmar inventário, preservação de resíduos e histórico de drivers antes de retomar operações modificadoras.
