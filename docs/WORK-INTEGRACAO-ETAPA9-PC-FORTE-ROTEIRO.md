# BETA LOCAL — guia portátil e roteiro mínimo futuro do PC FORTE

> Estratégia vigente: [piloto BETA único](WORK-INTEGRACAO-PILOTO-BETA-UNICO.md). Este roteiro de leitura/segurança serve à primeira avaliação do mantenedor, sem transmissão, quando houver pacote real validado e ensaio autorizado. Identificação local atual é histórica/configuração inicial da mesma BETA; não exige produto separado ou VM pessoal. Nenhum teste executado nesta revisão; estados abaixo preservados. Convites posteriores dependem da homologação inicial E validação conectada.

Preparado na Etapa 8, 28/10/2026. **Etapa 9 NÃO INICIADA.**
Não executar enquanto a Etapa 8 estiver bloqueada:
[estado/contrato de entrega](WORK-INTEGRACAO-ETAPA8-PREPARACAO.md).
Não há pacote executável, hash de ZIP executável ou link privado disponível.
Todas as linhas do roteiro começam como **NÃO EXECUTADO**.

## Identidade que a entrega futura deverá fornecer

Gerenciador Final v5.13.11.66 — **BETA LOCAL TESTE — NÃO HOMOLOGADA**.
Fonte candidata: 6df95fdd776644cd4b248b5cea61108974ca9ce9,
branch work-integracao-final-20261027, PR #65 Draft.
Exigir identificador único da build, fonte/build/CI, hash do ZIP, manifesto,
link privado nominal e limitações da entrega efetiva. Se a fonte mudar,
conferir sua validação específica antes de usar este roteiro.

O arquivo BETA-manifesto-6df95fdd da CI contém somente três documentos.
Seu hash não é hash de pacote executável. Não tentar abrir o GF a partir dele.

## Extração e abertura, somente depois da liberação local

1. Obter o ZIP pelo canal privado aprovado. Manter AVG e proteções ativos.
   Conferir o SHA-256 completo com a referência confiável da entrega:

       Get-FileHash -LiteralPath "CAMINHO_DO_ZIP_PRIVADO_APROVADO" -Algorithm SHA256

2. Em caso de divergência, arquivo inesperado, origem não confirmada ou bloqueio
   de proteção, parar. Não restaurar quarentena, criar exceção, desativar AVG,
   remover bloqueios ou repetir tentativas para fabricar aprovação.
3. Extrair em pasta nova e vazia com escrita normal do usuário, identificada
   pela versão/build. Não extrair sobre a versão anterior, pastas instaladas,
   AppData compartilhado ou outra saída de build. Conservar o ZIP recebido.
4. Manter a pasta inteira: EXE, DLLs, runtime, recursos, avisos e manifesto.
   Não copiar somente o EXE nem misturar DLLs de outras versões. O usuário
   final não precisa instalar SDK, runtime separado, serviço ou instalador.
   A conferência integral do manifesto pelo verificador existente deve constar
   da entrega; pode ser realizada no ambiente de compilação com Python,
   sem instalar ferramentas no PC FORTE apenas para isso.
5. Conferir BUILD-INFO.txt/BETA-BUILD.json e a versão do arquivo, sem abrir:

       (Get-Item -LiteralPath "PASTA_EXTRAIDA\GerenciadorFinal.exe").VersionInfo.FileVersion

   A versão deve ser 5.13.11.66; a fonte, perfil local, identificador e hashes
   devem corresponder à entrega. Código/versão iguais não provam que dois
   pacotes têm os mesmos bytes. Transport_configured deve estar false e
   endpoint vazio. Hash/metadata não equivalem a assinatura ou aprovação AVG.
6. Somente depois dos gates, abrir normalmente, sem “Executar como administrador”.
   Confirmar o título BETA LOCAL TESTE — NÃO HOMOLOGADA. Não habilitar início
   com Windows, reparos ou operações de manutenção para verificar a abertura.
   Qualquer elevação inesperada neste cenário de leitura deve ser cancelada
   e relatada, sem alterar permissões do Windows.

## O que está disponível e o que fica fora deste ensaio

Inventário Win32/MSIX/ecossistemas, interface, consultas permitidas por ação
explícita, investigação de drivers somente leitura, histórico e relatórios
técnicos locais podem ser observados. A simulação de diagnóstico usa dados
inventados. Falha parcial de fonte deve ser registrada como parcial.

Coleta colaborativa, nova pendência para envio e transmissão remota ficam
desativadas na edição local mesmo com escolha herdada, falha ou encerramento.
Instalação, remoção e reversão de drivers seguem indisponíveis. WinGet COM
permanece opcional e consultivo; não reinvestigar sua disponibilidade.
VLC permanece somente inventário, sem ações, verificações ou testes específicos.

Os outros fluxos mutáveis existentes no GF não são autorizados por este
roteiro: não atualizar/desinstalar aplicativos, apagar resíduos, limpar dados,
reparar/redefinir rede/Windows ou mudar energia/permissões apenas para evidência.
Nada depende de Worker, D1, DNS privado ou EU.org para funcionar localmente.

## Reaproveitar as evidências físicas existentes

[E1–E5 consolidadas](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md) e
[plano da avaliação 4](WORK-INTEGRACAO-ETAPA4-AVALIACAO.md#7-testes-físicos-mínimos-futuros--pc-forte)
continuam referências. FORTE/.64: 169 entradas, incluindo 27 MSIX;
FRACO/.65: 74, incluindo 19 MSIX. Essas contagens não são metas da .66:
programas, escopo e tempo podem diferir. E3/E5 sobrepõem a mesma sessão.

Já existem encerramentos normais e evidência de bandeja/estabilidade. Não repetir
T01–T10/T12 integralmente. Os registros anteriores não incluem SHA/hash da
futura .66; completar apenas a ligação ao novo artefato e as lacunas relevantes.
FORTE teve 2560×1440 a 125%; confirmar o ambiente real atual sem exigir mudança.
Amostras de pico anteriores não substituem repouso comparável 30/60/120 s.
T11/boot permanece pendente e exige autorização separada; não reiniciar agora.

## Ordem mínima da futura Etapa 9

| ID / prioridade | Verificação e evidência mínima | Critério/limite | Estado inicial |
| --- | --- | --- | --- |
| F9-00 / bloqueante | Canal privado, versão, build, SHA-256 do ZIP e referência dos arquivos | Sem pacote/hash válidos, parar todos os demais | NÃO EXECUTADO |
| F9-01 / bloqueante | AVG ativo, resultado disponível para estes bytes; guardar código/data da eventual detecção | Sem assinatura/homologação presumida; bloqueio suspende ensaio, sem contorno | NÃO EXECUTADO |
| F9-02 / alta | Uma abertura normal, título local, inventário inicial, depois encerramento normal e uma reabertura | Responsividade e saída normal; nenhuma consulta de versões iniciada sem ação; não provocar falha/forçar término | NÃO EXECUTADO |
| F9-03 / alta | Sem iniciar consultas, registrar CPU/RAM do processo em 30/60/120 s e método/unidades | Mesma métrica/coluna e condições; não comparar pico antigo com repouso novo nem limpar RAM para fabricar resultado | NÃO EXECUTADO |
| F9-04 / alta | Navegação, foco, rolagem e leitura no DPI/resolução atuais; capturas recortadas se necessário | Sem controles inacessíveis/cortes; não refazer a matriz sintética inteira de resoluções | NÃO EXECUTADO |
| F9-05 / alta | Totais e estado completo/parcial de Win32/MSIX; conferir poucas divergências de mesmo escopo com referência local | Sem publicar lista integral, nomes privados ou tratar diferença de contagem como defeito automático | NÃO EXECUTADO |
| F9-06 / média | Uma consulta oficial permitida explicitamente solicitada pela UI e cancelamento se necessário, somente se aplicável | Consulta não autoriza Atualizar/Instalar por item/lote; parcial/timeout não apaga candidatos confiáveis nem autoriza operação | NÃO EXECUTADO |
| F9-07 / média | Leitura de rede/vídeo/storage/um INF compartilhado, apenas se já presentes | Conferir associação/histórico e limites; não instalar dispositivo, buscar alvo excluído ou realizar gestão de drivers | NÃO EXECUTADO |
| F9-08 / média | Observar prévia/resíduos apenas se houver caso passivo já disponível e seguro | Se exigir desinstalar/atualizar/apagar, manter NÃO EXECUTADO; as 739 regressões não homologam R4/R7 físicos | NÃO EXECUTADO |
| F9-09 / média | Histórico/relatório local, eventual exportação explícita em arquivo novo; confirmar mensagem de diagnóstico remoto desativado | Não alterar consentimento antigo/arquivos para reproduzir fixture; pendência antiga intacta não prova coleta nova | NÃO EXECUTADO |
| F9-10 / separada | Bandeja e inicialização com Windows/T11, se o mantenedor autorizar especificamente | Sem autorização: NÃO EXECUTADO; não alterar autorun ou reiniciar para gerar evidência | NÃO EXECUTADO |
| F9-11 / encerramento | Uso natural curto nos itens de leitura escolhidos, término normal, eventos inesperados | Sem crash induzido, teste destrutivo ou nova bateria completa; registrar estabilidade e limitações | NÃO EXECUTADO |

Quando falta contexto para julgar, usar **INCONCLUSIVO**, sem converter ausência
de evidência em aprovação. **REPROVADO** exige comportamento observado contrário
ao critério; **APROVADO** vale apenas para o que foi executado e documentado.
Itens opcionais indisponíveis ficam NÃO EXECUTADO com motivo.
O Work não deve presumir execução, acesso, captura ou controle do PC.

## Relatar falhas com privacidade

Enviar pelo canal privado escolhido: ID do teste, APROVADO/REPROVADO/
INCONCLUSIVO/NÃO EXECUTADO, versão/build/hash do ZIP, Windows/resolução/DPI,
passos, esperado/observado, horário e frequência. Para CPU/RAM, informar
coluna/unidade/condições e três amostras. Para AVG, nome da detecção e hash
do arquivo afetado, sem desativar proteção ou reenviar o binário apenas para prova.

Se houver falha natural, conservar o diagnóstico existente e recortar somente
o trecho necessário. Caminhos, usuário, máquina, inventário, IDs de hardware,
nomes identificáveis e logs completos permanecem privados. Revisar capturas
antes de compartilhar; não anexar material bruto a Issue/PR pública.
Registrar reprodução e abrir correção específica quando demonstrada; não
reiniciar todas as etapas ou fechar Issue #48 por uma execução bem-sucedida.

## Retorno seguro à versão anterior

Fechar a BETA normalmente e confirmar que sua janela/processo terminou antes
de abrir outra versão. Manter a pasta/ZIP anteriores intactos desde a extração.
Retomar apenas uma cópia anterior já conhecida, com origem/hash conferidos e
proteções/diagnóstico remoto na configuração segura conhecida. Se sua origem,
hash ou política remota não puderem ser confirmados, interromper e revisar;
não baixar ou abrir build desconhecida para “voltar”.

Portabilidade não elimina os registros locais: DiagnosticTrace pode usar
AppData/Diagnosticos ou TEMP, e o histórico fica em AppData/Historico.
Não apagar, mover ou restaurar todo o diretório GerenciadorFinal de AppData:
ele pode conter preferências, histórico e dados compartilhados por versões.
Não editar consentimento, Registro, serviços ou permissões para o retorno.
Conservar evidência da sessão .66 separadamente; remover sua pasta é opcional
e posterior, com o programa fechado e após preservar os dados necessários.

Trocar de versão não desfaz alterações de aplicativos/sistema realizadas por
outras ações. Este roteiro não autoriza tais ações nem cria uma promessa de
reversão. Sucesso local não libera BETA conectada ou pública.

## Comando futuro, condicionado à entrega

Somente após encerrar o bloqueio da Etapa 8 e receber resultados manuais:

> Continue pela Etapa 9 na branch work-integracao-final-20261027, PR #65 Draft. Leia checkpoint, matriz, relatório/recibo da Etapa 8 e roteiro PC FORTE. Confirme primeiro pacote privado efetivamente entregue, versão, fonte, build, SHA-256 e autorização manual. Se faltar algum requisito, mantenha Etapa 9 não iniciada e registre a pendência. Analise somente os resultados/capturas/diagnósticos que eu fornecer, sem presumir acesso ao PC. Classifique cada teste como APROVADO, REPROVADO, INCONCLUSIVO ou NÃO EXECUTADO, preserve dados privados e todas as restrições, registre defeitos específicos sem reiniciar a missão e não libere BETA conectada/pública.


## Resultado posterior — Etapa 9 parcialmente concluída em 28/10/2026

O roteiro acima é o estado inicial/histórico. O mantenedor informou a realização dos testes possíveis, sem bugs percebidos, com impressão de rapidez e leveza. Registre **PARCIALMENTE CONCLUÍDA**, preservando testes não integralmente evidenciados; não refazer bateria completa sem alteração específica ou falha concreta. Consulte [resultados detalhados](WORK-INTEGRACAO-ETAPA9-PC-FORTE-RESULTADOS.md). A entrega privada .66 já havia sido concluída; restrições sobre publicação conectada/pública permanecem.
