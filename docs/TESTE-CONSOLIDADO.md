# Roteiro físico mínimo — candidata WORK9 .49

Este roteiro substitui o lote v5.13.10.18. Os registros antigos do PC FRACO
estão no [arquivo histórico](TESTES-HISTORICOS-PC-FRACO.md). A aprovação em
runner não significa que esta candidata já rodou nos dois PCs.

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
5. Consultar resíduos gerais e os pilotos específicos de Notepad++/SumatraPDF,
   sem excluir. A ausência específica não comprova ausência geral. Registrar
   contagens, motivos de parcialidade, classificação e legibilidade da prévia.
6. Redimensionar em 100% e escala habitual, verificar foco, botões, rolagem,
   sobreposição e tema. Consultar drivers somente leitura e cancelar perto do
   fim; não instalar, reparar, remover ou reverter drivers.
7. Na mesma condição de energia e carga, medir três aberturas e três consultas
   de inventário/atualizações/resíduos de cada versão comparada, separando cache
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

R4: em VM descartável, revisar a prévia inteira, confirmar apenas alvos próprios,
revalidar ausência da família, excluir, conferir arquivos/pastas/chaves HKCU/HKLM
32/64 e reiniciar para nova consulta. O erro `registro_ArgumentException` da .48
tem de permanecer ausente. Qualquer alvo alterado, reinstalação, link, arquivo
em uso ou falha de leitura deve ser preservado/explicado. Configurações, favoritos
e histórico exigem decisão separada; locais compartilhados permanecem protegidos.

Não repetir limpezas em massa sugeridas por ferramentas externas. A missão Work
não executa essas operações nos computadores pessoais. U8/R7/R4 só avançam com
evidências reais suficientes, não com o número de testes sintéticos.
