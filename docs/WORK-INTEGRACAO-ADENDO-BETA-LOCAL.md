# Adendo do mantenedor — organização, correções e BETA LOCAL

27/10/2026, America/Sao_Paulo. Diretrizes recebidas nesta missão.
Este adendo estende a sequência após a integração 1–4; não autoriza pular
essas etapas. A execução continua restrita a **uma etapa por sessão**,
com atualização do [checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) e da
[matriz](WORK-INTEGRACAO-MATRIZ.md), salvamento no GitHub e parada.

Objetivo futuro: candidata **BETA LOCAL TESTE — NÃO HOMOLOGADA**, portátil
Windows x64, inicialmente para o PC FORTE do mantenedor. Organizar, auditar,
corrigir e validar antes de preparar o executável destinado ao teste.

## Restrições permanentes

- Main e branches originais preservadas; sem merge, reescrita de histórico,
  release, deploy ou distribuição automática.
- VLC permanentemente somente inventário: sem ações, botões, testes
  específicos, consultas ou verificações externas.
- WinGet COM opcional, não bloqueante, somente consultivo; não reinvestigar.
- Instalação, remoção e reversão de drivers desativadas.
- AVG e todas as proteções ativas; não declarar assinatura/homologação sem prova.
- Nenhuma operação destrutiva ou depuração nos PCs pessoais.
- BETA conectada bloqueada; sem ativar Worker/D1/envio/exposição pública;
  `PILOT_ENABLED=no` e `GITHUB_EXPORT_ENABLED=no` preservados.
- Roadmap 27/42 sem promoção por contagem de testes. A BETA local pode ser
  testada antes de 42/42 se pendências estiverem documentadas/desativadas e
  não houver risco indevido no cenário planejado.

## Etapa 5 — organização e limpeza segura do GitHub

Auditar branches, PRs abertas, Issues, workflows, documentação, artefatos e
arquivos de teste. Identificar linhas antigas/experimentais/substituídas ou
incorporadas, PRs redundantes e evidências históricas a preservar. Organizar
índices/links/continuidade e corrigir referências obsoletas, duplicadas ou
inconsistentes; conferir a necessidade dos arquivos da árvore integrada.

Executar somente correções documentais/organizacionais não destrutivas.
Separar no relatório o que foi feito com segurança e as propostas que
dependem de autorização específica. Não excluir branches, commits, tags,
Releases ou históricos; não fechar PRs/Issues por parecerem antigos;
não apagar evidência histórica, remover testes de segurança ou alterar main.
**Salvar relatório/checkpoint e parar.**

## Etapa 6 — auditoria geral e busca de erros

Inspecionar a candidata integrada nas seguintes áreas:

1. Inicialização, encerramento e bandeja.
2. Inventário Win32, MSIX e ecossistemas gerenciados.
3. Descoberta de atualizações e comportamento das fontes.
4. Rede parcial, timeout e cache.
5. Identidade, versão, fabricante, canal e arquitetura.
6. Drivers, hardware IDs, INF, assinatura e histórico.
7. Atualização, desinstalação e resíduos.
8. Preservação de dados pessoais/compartilhados.
9. Interface, foco, rolagem, DPI e resoluções.
10. CPU, RAM e desempenho.
11. Exceções, cancelamentos e operações pendentes.
12. Consentimento, privacidade, diagnósticos e recursos conectados.
13. Empacotamento x64, integridade e dependências.
14. Segurança, privilégios e execução de instaladores.

Usar testes existentes, análise estática e cenários sintéticos adversariais.
Criar testes apenas para lacunas demonstradas. Sem operações destrutivas,
PCs pessoais como laboratório ou reinvestigação de fontes excluídas/COM.
Produzir matriz de defeitos com localização, reprodução, impacto, evidência
e recomendação, classificando CRÍTICO, ALTO, MÉDIO, BAIXO ou EXTERNO.
CRÍTICO impede preparar a BETA local; ALTO deve ser corrigido antes do teste
físico; EXTERNO exige evidência de causa fora do GF. **Salvar e parar.**

## Etapa 7 — correções e regressões

Priorizar críticos/altos. Reproduzir com teste, corrigir a causa sem contornar
proteções e demonstrar antes/depois. Rodar testes da área e regressões
afetadas, procurar erros introduzidos, executar CI Windows aplicável e
confirmar pelo SHA exato. Atualizar cobertura/documentação. Não remover
testes para aprovar. Se correção segura não for possível, registrar bloqueio;
não declarar BETA pronta com defeito crítico/alto relevante ao cenário.
**Salvar commits/evidências/checkpoint e parar.**

## Etapa 8 — preparação da BETA LOCAL TESTE

Somente após as validações anteriores e CI Windows aprovada no SHA exato:

1. Usar a candidata integrada/corrigida e preparar pacote portátil x64 com
   dependências, versão e identificador rastreáveis.
2. Identificar **BETA LOCAL TESTE — NÃO HOMOLOGADA**, gerar SHA-256 dos
   arquivos e manifesto de integridade, verificar licenças/recursos/segredos.
3. Não acrescentar instalador automático, serviço persistente ou elevação
   obrigatória sem necessidade comprovada.
4. Manter instalação/remoção/reversão de drivers indisponíveis e **coleta e
   transmissão remotas desativadas**, inclusive em erro ou encerramento.
5. Não incluir tokens, credenciais ou endpoints privados ativos. Permitir
   apenas consultas HTTPS oficiais estritamente consultivas, explicitamente
   iniciadas pelo usuário. Não depender de Worker, D1, DNS ou EU.org.
6. Respeitar o contrato de distribuição. Se impedir entregar executável,
   registrar a causa e propor entrega privada autorizada/verificável, sem
   contornar gates. Não criar Release pública ou publicar ZIP indiscriminadamente.

Entrega: identificador da candidata, SHA fonte, resultado da CI, SHA-256 do
pacote, funções disponíveis/desativadas, limites/problemas, roteiro mínimo
PC FORTE e retorno seguro à versão anterior. AVG permanece ativo; nenhuma
assinatura ou homologação é presumida. A BETA local não homologa a conectada.
**Parar após preparar. Não iniciar a Etapa 9 automaticamente.**

## Etapa 9 — homologação física manual no PC FORTE

O mantenedor executa os testes e envia resultados/capturas/diagnósticos.
O Work não presume acesso ou controle do PC. Roteiro priorizado deve
reaproveitar evidências físicas anteriores e evitar repetição desnecessária.

Verificar quando aplicável: integridade/versão; AVG ativo; abertura/fechamento;
CPU/RAM; interface/responsividade; inventário; consultas de atualizações;
drivers somente leitura; prévia e proteções de desinstalação/resíduos;
histórico/relatórios/diagnóstico; estabilidade. Bandeja e inicialização com
Windows exigem autorização específica quando aplicável.

Não solicitar atualizações/desinstalações reais apenas para gerar prova.
Não instalar/remover/reverter drivers. Classificar cada teste APROVADO,
REPROVADO, INCONCLUSIVO ou NÃO EXECUTADO. Dados privados não são publicados
integralmente no repositório. Novo defeito gera reprodução e correção
específica, sem reiniciar toda a missão. Sucesso físico não libera a BETA
conectada ou pública automaticamente.

## Encerramento de cada sessão

Registrar alterações/evidências, commits/CI, problemas conhecidos e próxima
etapa no checkpoint/matriz; salvar e encerrar. Interrupção exige ponto exato
de retomada e preservação dos commits válidos. Não avançar automaticamente.
A missão ampliada termina somente com candidata local validada tecnicamente
e análise dos testes físicos autorizados enviados pelo mantenedor.
