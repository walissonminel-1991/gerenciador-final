# Roteiro físico mínimo — candidata WORK11 .53

Este roteiro substitui o lote v5.13.10.18. Os registros antigos do PC FRACO
estão no [arquivo histórico](TESTES-HISTORICOS-PC-FRACO.md). A aprovação em
runner não significa que esta candidata já rodou nos dois PCs.

## Primeira bateria — PC FORTE (aplicativos já preparados)

1. Usar a candidata .53 identificada na PR #40, após aprovação da CI. Conferir
   versão e hashes. Não reinstalar versões antigas nem repetir o 7-Zip: o teste
   .52 já o atualizou de 26.04 para 26.05.
2. Confirmar que Notepad++ 8.9.1 e SumatraPDF 3.5 continuam instalados nas
   arquiteturas e escopos existentes. Os destinos das receitas desta candidata
   são 8.9.8.1 e 3.6.1. Se o inventário não corresponder, registrar e parar esse
   caso, sem alterar a instalação para forçar o teste.
3. Atualizar somente o Notepad++ pelo cartão. Observar estado azul, download
   com porcentagem quando houver tamanho informado, validação, instalação
   indeterminada e conferência. Não deve aparecer porcentagem inventada durante
   a instalação. O UAC legítimo do Windows pode aparecer.
4. Esperar a consulta automática terminar. Exigir verde com confirmação e 100%
   para considerar sucesso; conferir também a versão pelo próprio Notepad++.
   Pesquisar o aplicativo, sair/voltar de abas e renovar a lista: o resultado
   deve permanecer ligado à mesma instalação durante a sessão.
5. Repetir uma única vez para SumatraPDF 3.5 → 3.6.1, conferindo a versão no
   próprio aplicativo. O progresso do Notepad++ não pode migrar para o Sumatra.
6. Se houver vermelho, amarelo, cancelamento ou recusa UAC, registrar o estado e
   o diagnóstico; não repetir automaticamente um resultado pendente. Não
   provocar falhas apagando arquivos, desligando proteções ou adulterando pacotes.
7. Enviar o diagnóstico .53 e capturas do progresso/resultado, indicando versão
   antes/depois e se apareceu instalador interativo. Um download muito rápido
   pode exibir poucas amostras; o resultado final precisa continuar visível.

No **PC FRACO (1440×900)**, começar por consulta e navegação, sem preparar novas
instalações. Conferir largura, DPI habitual, leitura do estado, botões, bandeja,
Inicialização/Windows Update, consumo em repouso e ausência da ferramenta
residual manual. Estados adversos adicionais pertencem ao ambiente descartável.

## Preparação e consultas

1. Usar somente o artefato indicado na PR #40 após CI aprovada. Extrair em pasta
   nova, manter todos os arquivos juntos e conferir versão, BUILD-INFO e hashes.
   Não instalar SDK. Manter AVG ativo; parar se houver bloqueio.
2. PC FORTE: 2560×1440; PC FRACO: 1440×900. Registrar escala DPI, versão do
   Windows, RAM total e GF. Não registrar usuário, caminho pessoal, serial ou IP.
3. Abrir, minimizar/restaurar três vezes e fechar. Navegar rapidamente por
   Aplicativos, Inicialização e Windows Update, inclusive sair e voltar à mesma
   tela durante uma consulta. Nenhum cartão de uma tela deve aparecer na outra;
   botões precisam recuperar o estado após cancelamento e descarte.
4. Consultar inventário e atualizações. Comparar contagens Win32/MSIX, identidade,
   versões e estados parciais, sem executar instaladores. Cancelar uma consulta
   e repetir; resultado atrasado não deve substituir o atual. Falha COM ou fonte
   indisponível precisa aparecer como parcial, sem inferir desinstalação.
5. Conferir que não existem botões de investigação/limpeza residual independente.
   A limpeza avançada do Windows, temporários, caches, Lixeira e discos continuam
   disponíveis. A confirmação de resíduos só pertence à desinstalação concluída.
6. Redimensionar em 100% e escala habitual, verificar foco, botões, rolagem,
   sobreposição e tema. Consultar drivers somente leitura e cancelar perto do
   fim; não instalar, reparar, remover ou reverter drivers.
7. Na mesma condição de energia e carga, medir três aberturas e três consultas
   de inventário/atualizações de cada versão comparada, separando cache
   frio/quente e fontes de rede. Medir também RAM, CPU e disco em repouso,
   minimizado e após cancelar. Não atribuir diferença de rede ao código do GF.

## Homologação funcional posterior, em ambiente descartável

U8: atualizar uma instalação identificada de Notepad++ e SumatraPDF, preservando
arquitetura/escopo; verificar assinatura, hash, versão do binário e Registro
após concluir. Repetir com processo ativo e cancelamento. Não instalar versões
antigas nos PCs pessoais para criar artificialmente esse cenário.

R7: desinstalar somente um aplicativo de teste autorizado, observar execução,
retorno, reinício/pending e duas consultas posteriores; testar coexistência
EXE/MSI e cancelamento sem apagar a instalação vizinha.

R4: em VM descartável, desinstalar dois aplicativos de teste pelo GF. Conferir
uma única confirmação SIM/NÃO, com arquivos, pastas, chaves e preservações por
aplicativo. Primeiro escolher NÃO e conferir que os resíduos continuam presentes;
não deve haver pergunta repetida. Reinstalar os aplicativos de teste na VM,
desinstalá-los novamente e escolher SIM; conferir exclusão permanente e ausência
posterior dos alvos elegíveis, inclusive após reiniciar a VM. Não usar um botão
manual de busca: esse acesso foi removido.

Incluir um resíduo HKLM32 sintético, sem iniciar o GF como administrador. SIM
deve chamar o UAC legítimo dentro da operação quando necessário; testar recusa
(preservação e erro 1223 no diagnóstico) e autorização com a mesma conta (remoção
e conferência). Não alterar ACLs nem assumir propriedade. Credenciais de outra
conta são recusadas e registradas. Uma remoção pendente/reinício/falha não pode
oferecer limpeza daquele aplicativo, mesmo em lote.

Conferir diagnósticos por aplicativo para alteração de conteúdo, reinstalação,
link, arquivo em uso, cancelamento, permissão e resultados parciais. Arquivos
pessoais, backups e locais compartilhados permanecem preservados. Na bandeja,
a pergunta aguarda a restauração; cancelar não pode reabri-la depois.

Não repetir limpezas em massa sugeridas por ferramentas externas. A missão Work
não executa essas operações nos computadores pessoais. U8/R7/R4 só avançam com
evidências reais suficientes, não com o número de testes sintéticos.
