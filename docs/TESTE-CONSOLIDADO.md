# Roteiro físico mínimo — WORK12 .57 TESTE

Usar somente o pacote indicado na [PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40) após aprovação da CI. A PR identifica a fonte, o artefato e os hashes. [Correções, testes automatizados e limites](MISSAO-WORK12.md). Roteiros antigos estão nos relatórios históricos; este documento substitui a bateria .53 com versões antigas de aplicativos.

## Preparação

Extrair o portátil em pasta própria, manter os arquivos juntos e conferir versão .57, BUILD-INFO e SHA256SUMS. Não instalar SDK. Manter AVG ativo; registrar eventual bloqueio. O programa continua TESTE e sem assinatura.

Não reinstalar Notepad++/SumatraPDF nem repetir as limpezas antigas só para reproduzir os defeitos das versões .55/.56. Os casos de ausência, erro 5, preservação, SIM/NÃO, arquivo ocupado e cancelamento pertencem à bateria automatizada com alvos sintéticos.

## PC FORTE — uma operação quando houver aplicativo dispensável

1. Abrir Aplicativos. Se houver um programa comum que o usuário já queira remover, solicitar **uma única desinstalação**. Se não houver, encerrar esta etapa sem instalar programas apenas para criar trabalho.
2. Conferir o andamento desde a preparação: modo silencioso/interativo explicado na prévia, “Desinstalador trabalhando...” e “Conferindo remoção...”. Operações sem medida real devem usar animação, sem porcentagem inventada. A interface e Cancelar permanecem responsivos; cancelar a espera não promete encerrar o fornecedor.
3. Somente após remoção confirmada, observar a verificação automática. Havendo resíduos comprovados, a pergunta SIM/NÃO deve identificar os alvos. SIM autoriza a exclusão permanente revalidada; NÃO mantém os resíduos. Em seleção múltipla há uma decisão para a seleção e resultado separado por aplicativo.
4. Ler o resumo final: removidos, preservados e seus motivos; ausência apenas nos locais verificados; erro de acesso ou cobertura limitada como parcial/inconclusiva. Não deve existir apenas “concluída” escondendo falhas. Ausência de elegíveis dispensa uma pergunta vazia de limpeza.
5. Não esperar pergunta de reinício quando nenhum fornecedor ou arquivo bloqueado justificar isso. Se houver causa real, o GF explica e pede consentimento; a revisão pós-boot é somente leitura. Não há exclusão automática adicional.

Modo interativo pode abrir a janela do próprio fabricante. UAC legítimo também pode aparecer. Unlocker sem quiet/motor comprovado não recebe parâmetros improvisados. UAC com outra conta continua recusado no worker de limpeza; permissões não são alteradas para contornar uma negativa.

## PC FRACO — leitura, layout e bandeja

Em 1440×900 e no DPI habitual, conferir Aplicativos, Inicialização e Windows Update; foco, botões, espaço do cartão e ausência de sobreposição. Não exige nova desinstalação.

Ativar/desativar “Iniciar com o Windows” e, se desejado, manter ativado até o próximo login normal. O portátil precisa permanecer na mesma pasta. Ao iniciar com Windows, deve ficar na bandeja; Abrir restaura a janela, Sair encerra. Uma decisão ou um resumo pendente deve aparecer ao reabrir, sem janela inesperada enquanto oculto.

No PC FORTE, a referência é 2560×1440; no FRACO, 1440×900. Geometria, estados e resoluções/DPI simulados já passam pela CI, mas isso não homologa DPI físico nem antivírus dos PCs.

## Se houver falha

Usar o diagnóstico do próprio GF e indicar versão, PC FORTE/FRACO, modo silencioso/interativo, etapa e mensagem final. Não repetir automaticamente uma operação pendente, apagar resíduos por fora, desligar proteções ou alterar ACLs. Não publicar logs brutos com nomes, caminhos pessoais, serial ou IP na PR pública.

Os testes de reinstalação durante a limpeza, links, compartilhamento, ACL negada, UAC simulado, atraso do fornecedor, retorno indeterminado e cancelamento são executados em ambiente descartável. Não precisam ser provocados repetidamente nos computadores pessoais. Boot físico e UAC interativo real ainda dependem de homologação posterior. U8/R7/R4 físicos e roadmap 27/42 permanecem abertos.
