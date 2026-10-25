# Histórico de alterações

## 5.13.11.60 TESTE — WORK14: evolução dos motores (CI aprovada; homologação física pendente)

- Registro: evita a solicitação implícita de escrita na raiz HKLM durante inspeção; preserva erro, etapa, profundidade e cobertura antes/depois. Nenhuma alteração de ACL no produto.
- `registered-install` deixa de inferir HKLM; política de máquina exige revisão própria. Interface distingue verificado, ausente, inacessível, preservado e exclusões propostas.
- Capacidades por aplicativo, motivos e denominadores agregados no diagnóstico. Elegível não equivale a payload validado nem a autorização para excluir.
- Identidade comum de produto preserva `+`, canal e arquitetura; admite decoração ARP conhecida. MSI/WiX sem InstallLocation pode comprovar raiz pelo executável registrado. NSIS reconhece desinstalador em subpasta dedicada. Inno exige também produto e assinaturas, em vez de conceder flags apenas pelo log.
- Corrige classificação IPv4: `169.254/16` permanece bloqueada, sem bloquear o espelho público VLC `169.229`. Assinatura/hash/HTTPS continuam obrigatórios.
- Matriz reproduzível da mesma amostra contra #49; assinatura simulada é declarada e não representa homologação. U8/R7/R4 abertos, 27/42.

## 5.13.11.59 TESTE — descoberta automática (24/10/2026)

- Aprovada na [CI 40138799763](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40138799763), fonte `26bbc17a`; [portátil x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40138799763/artifacts/12578144319). 318 testes de atualizadores (81 da descoberta), 645 resíduos e 27 mutações; 281 hashes internos e SHA do ZIP reconferidos independentemente. Homologação física pendente.
- Descoberta por catálogo Microsoft assinado e manifesto vinculado por SHA-256, sem WinGet COM/CLI, registro de pacotes ou regras por versão no novo mecanismo.
- Autorizações efêmeras por instalação/manifesto; dados, IDs, receitas copiadas e catálogos consultivos não emitem permissão. Assinaturas do aplicativo e instalador precisam corresponder.
- MSI: ProductCode, UpgradeCode, arquitetura, versão e escopo efetivos, com recusa de serviços/drivers. NSIS/Inno: formato real e parâmetros documentados, sem scripts de catálogo e sem encerrar aplicativos automaticamente.
- Fechamento normal exige consentimento e identidade/usuário do processo; alteração de arquitetura, dados compartilhados, duplicações e resultado incerto bloqueiam execução ou repetição.
- Desinstalação NSIS genérica com prova de motor, produto e assinaturas; mantém fallback interativo, SIM/NÃO residual, dados pessoais e recuperação da WORK12.
- YAML limitado e sem aliases; download com HTTPS público, validação de DNS, hash, assinatura e identidade. SQLite do Windows, sem novo banco/serviço instalado; YamlDotNet MIT incluído no aviso de terceiros.
- Testes sintéticos adversariais e validação de catálogo/manifestos/binários oficiais sem executar instaladores. [Relatório](docs/MISSAO-WORK13.md); homologação física pendente.

## 5.13.11.58 TESTE — WORK13 (24/10/2026)

- Candidata aprovada na [CI 40099234162](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162), fonte `691ed096`; [portátil x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162/artifacts/12562141990) conferido por 280 hashes. Homologação física permanece pendente.
- OBS NSIS `/S` comprovado, Visual C++ 2013 x64/x86 com SHA-256 oficiais independentes e Notepad++ x86. Receitas fixas; parâmetros remotos não são executados.
- Contratos EXE preservam produto, pasta, arquitetura, escopo e exclusividade; Registro sem binário novo não confirma sucesso. VC2013 confirma também `msvcr120.dll` e admite migração comprovada de GUID sem perder o resultado.
- Cancelamento imediatamente antes da execução impede o lançamento. Após executar, cancelar interrompe a espera e deixa resultado inconclusivo, sem matar o instalador ou repetir automaticamente.
- Consultas MSIX com prazo e uma chamada em andamento; verificação Authenticode e espera fora da UI. Estrutura e publisher MSIX exatos, além do hash e confiança exigida pelo Windows.
- Corrige páginas oficiais dos servidores Plex/Jellyfin; Acrobat, Proton divergente, pacotes protegidos e WinGet real preservam suas restrições.
- Metadados de candidata e última aprovada separados e verificados automaticamente. U8/R7/R4 sem homologação física; [relatório WORK13](docs/MISSAO-WORK13.md).

## 5.13.11.57 TESTE — WORK12 (24/10/2026)

- Desinstalação com andamento indeterminado desde o clique, etapas reais e resumo final escuro por aplicativo; resultado parcial permanece diferente de sucesso integral.
- Resíduos: erros de acesso não equivalem a ausência; políticas reconsultam chaves criadas pelo desinstalador. Cobertura genérica por comando/pasta dedicados e chaves corroboradas, sem exclusão por mera semelhança.
- Consulta do Registro com direitos mínimos; erros por hive/visão. Nenhuma mudança de ACL. Dados pessoais, compartilhamentos e alvos sem prova anterior preservados.
- QuietUninstallString revalidado; NSIS Notepad++ x86/x64, SumatraPDF 3.6.1 e Inno comprovado. Unlocker sem contrato permanece interativo.
- Cancelamento/timeout de processos reais separados; recuperação de pendências e resumo ao restaurar da bandeja. Reinício somente com motivo técnico; pós-boot permanece somente leitura.
- Regressões Windows e mutações novas em fixtures descartáveis. [Relatório WORK12](docs/MISSAO-WORK12.md); a [PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40) identifica a CI aprovada e o artefato. U8/R7/R4 físicos permanecem pendentes.

## v5.13.11.49 TESTE — WORK9: consolidação, pacote e CI

- Preserva as 13 causas corrigidas e 42 controles adversariais da Parte 1, registrados em [WORK9 parte 1](docs/MISSAO-WORK9-PARTE1.md); U8/R7/R4 continuam sem homologação física.
- Portátil inclui licença GF, privacidade, guia e avisos originais dos runtimes. Conferência independente exige origem, versão, AMD64, self-contained e cobertura exata de hashes antes do upload; onze testes sintéticos do pacote, incluindo links internos dos documentos.
- Retira três leitores de sistema comprovadamente sem uso; preserva implementações atuais e testes. Reduz repetições na CI sem retirar asserções; SDK compartilhado por global.json, permissões e NuGetAudit mantidos.
- Consolida nove relatórios/planos PC FRACO com hashes e referências históricas; corrige README/roteiro/privacidade e preserva os dados do benchmark em JSON compacto.
- Numeração sequencial de candidata, sem avanço do roadmap (27/42), merge, release estável ou operação nos PCs pessoais. O resultado efetivo da CI e o pacote aprovado ficam em [WORK9 parte 2](docs/MISSAO-WORK9-PARTE2.md).

## v5.13.11.48 TESTE — limpeza específica Notepad++/SumatraPDF e atualização silenciosa

- Capturas PC FORTE: Notepad++ apresentou três vestígios históricos; SumatraPDF não apareceu na busca geral parcial, o que NÃO comprova ausência; IObit mostrou 41 registros de desinstalação e 2977 supostos arquivos desnecessários, incluindo instalações ativas e pastas compartilhadas do Windows. Nenhum item do IObit foi automaticamente autorizado.
- Investigação específica independente para Notepad++ e SumatraPDF após inventário íntegro e ausência de toda a família instalada. Verificação de pastas próprias em AppData e Program Files e de chaves específicas em HKCU e HKLM (32/64 bits). Links, hardlinks, caminhos externos, limites excedidos, processo ativo e executável ainda presente bloqueiam limpeza.
- Janela de revisão rolável mostra TODOS os alvos; exclusão requer autorização explícita e revalidação por SHA-256/Registro e inventário. Pastas só são removidas vazias. Rastros compartilhados (MuiCache/HKCR/Packages), perfis de outros usuários e Documentos/Downloads/Imagens/Músicas permanecem fora.
- Atualização silenciosa via instaladores locais verificados e fixados: Notepad++ EXE x64 `/S` (maiúsculo, conforme documentação oficial) e SumatraPDF `-install -silent`, mantendo `-all-users` se aplicável. Evita encerramento forçado e bloqueia edições MSI ou múltiplas instalações. OBS continua interativo.
- Corrige transição de Inicialização para Windows Update: limpa os cartões anteriores imediatamente e impede conclusão tardia da consulta de Inicialização de renderizar na aba Windows Update.
- Testes sintéticos: limpeza segura de diretório descartável, recusa por variação do conteúdo, bloqueio de aplicativo instalado, recusa de parâmetros adulterados e instalações sobrepostas. Nenhuma instalação/desinstalação real pelo CI.
- **Não utilizar no PC FORTE até políticas, testes Windows e pacote x64 estarem aprovados.** Antes da instalação de versões antigas, a limpeza autorizada deve ser verificada após reinicialização.

## v5.13.11.47 TESTE — Conciliação de resultados depois da limpeza residual

- Evidência real PC FORTE 23/10/2026: a DLL e pasta de 7-Zip desapareceram após reinício; limpeza de Registro precisou de duas autorizações, na primeira 1 chave removida e erro `registro_caminho_invalido_win32_87`; na segunda 1 chave removida sem falhas e conferência específica final 0 arquivos, 0 pastas, 0 chaves.
- Corrigida a tela de investigação: após cada limpeza específica, grupo e locais são reconstruídos a partir da nova fotografia. Quando não sobrar nenhum alvo, o grupo de 7-Zip some imediatamente da lista sem necessidade de reiniciar o GF. Limpeza parcial mostra somente os itens remanescentes.
- Provedor único de relatórios específicos evita divergência entre consulta inicial e conferência posterior. Não amplia abrangência de limpeza, não altera permissões, não reinicia nem encerra o Explorer.
- Teste sintético comprova que um grupo com quatro alvos não permanece após a exclusão dos quatro; zero alvo não produz relatório residual.
- Limpeza completa do computador NÃO declarada: integrações compartilhadas e demais rastros sugeridos por terceiros exigem prova de exclusividade. CI deve passar antes de disponibilizar candidato.

## v5.13.11.46 TESTE — identificação exata do bloqueio de exclusão

- PC FORTE: com v5.13.11.45 elevada, a DLL de 101888 bytes e as chaves permaneceram. ACL mostrada pelo usuário: Administradores e SYSTEM com FullControl na pasta e no arquivo, sem atributo somente leitura. Causa real ainda não determinada.
- Instrumentação da limpeza isolada: distinguir falha em verificar caminho, abrir arquivo, calcular/verificar hash, chamar File.Delete e confirmar exclusão. Registrar apenas fase e códigos Win32 restritos, sem incluir nomes, caminhos pessoais ou conteúdos.
- Não houve mudança na autorização de exclusão nem tentativa de contornar bloqueios; a v46 ainda exige inventário íntegro, prévia, confirmação, validação SHA-256, proteção contra reparse/hardlinks e pós-verificação.
- Teste Windows exige identificar a etapa File.Delete e Win32 5 para arquivo sintético marcado como somente leitura. Nenhuma exclusão real no PC FORTE durante CI.

## v5.13.11.45 TESTE — diagnóstico e retomada de limpeza 7-Zip após remoção

- PC FORTE v44: desinstalação silenciosa confirmou saída 0 e remoção do registro, mas limpeza deixou arquivo `7-zip.dll` (101888 bytes), pasta e duas chaves; 0 exclusões, 1 falha e 3 preservações. O log anterior não conservou a classe da falha; suspeita de autorização/arquivo em uso permanece hipótese.
- Limpeza restrita agora registra códigos estáveis de erro por arquivo/pasta/Registro sem coletar caminhos pessoais ou conteúdo. UI mostra categoria e orienta quando permissão de administrador pode ser necessária, sem adicionar botão de privilégio.
- Nova entrada histórica específica 7-Zip 26.05 (x64) mostra resíduos existentes após desinstalação, com opção `Limpar 7-Zip` dentro do investigador. Revalida ausência de qualquer edição do 7-Zip e processos ativos, apresenta prévia completa e exige autorização; permite retomar sem reinstalar o software.
- Corrige o caso em que a investigação geral, apesar de encontrar candidatos, não apresentava nenhum grupo ao filtrar ZIP em varredura parcial; a análise específica é independente da cobertura geral, sem relaxar seus limites.
- Regressão sintética de arquivo somente leitura exige falha explícita; valida preservação e reconciliação. A limpeza não cobre entradas genéricas HKCR/Windows apontadas por ferramentas externas até atribuir propriedade específica.
- Candidata permanece experimental, sem remoções reais pelo CI; só distribuir se políticas, testes Windows e compilação passarem.

## v5.13.11.44 TESTE — piloto de desinstalação completa (NÃO HOMOLOGADA)

- Seleção silenciosa: MSI apenas com ProductCode registrado correspondente, /qn /norestart; 7-Zip EXE x64 apenas quando identidade, diretório, arquivos e comando oficial são confirmados. Outros EXEs continuam com o desinstalador habitual; não há opção genérica insegura de modo silencioso.
- Pós-desinstalação do 7-Zip: captura precisa de arquivos, tamanhos e SHA-256 em Program Files/7-Zip, AppData/Roaming/7-Zip e AppData/Local/7-Zip; também apenas HKCU/HKLM\\SOFTWARE\\7-Zip. Não percorre Downloads, Documentos, Imagens, Músicas, pastas pessoais arbitrárias, Registro interno MSI nem drivers.
- Cada exclusão exige inventário completo, ausência do 7-Zip e de seus processos, prévia integral rolável, confirmação explícita, nova comparação de nomes/conteúdo/Registro. Os arquivos são conferidos antes de excluir; pastas só são eliminadas vazias; chaves exclusivas são verificadas por digest. Falhas de permissão preservam os itens e são contabilizadas, sem elevação automática.
- Diagnósticos novos registram contagens, bytes e categorias de falha, sem registrar nomes/caminhos pessoais. Desinstalações pendentes, canceladas ou não confirmadas não podem iniciar limpeza.
- Regressões com MSI sintético e árvore de arquivos descartável; alteração após a prévia deve impedir toda a limpeza.
- **Bloqueio de release:** manifesto e arquivo do projeto devem ter versões iguais e todas as GitHub Actions devem passar. Nenhum pacote .44 deve ser distribuído até isso acontecer.
- Testes físicos PC FORTE ainda não executados. Limpeza genérica para todos os aplicativos ainda não foi autorizada; expandir por identidades/produtos verificados.

## v5.13.11.43 TESTE — atualização silenciosa 7-Zip, candidata experimental (validação CI/física pendente)

- PC FORTE: após coexistência de 7-Zip EXE 26.04 e MSI 26.05, remover o EXE deixou um MSI registrado sem arquivos executáveis. Novo atualizador exige instalação EXE x64 única, íntegra, na pasta padrão, com identidade e desinstalador esperados; se houver MSI, outra edição ou arquivos ausentes, bloqueia.
- Receita estritamente local para **7-Zip 26.05 x64 EXE**, sem depender de URL/argumentos do catálogo: `https://github.com/ip7z/7zip/releases/download/26.05/7z2605-x64.exe`, SHA-256 `fd99e86f57d8f429ef5ed045bf98315af962e2309b0f1ba87a147a44c71647d0` (corroborado pelo espelho oficial SourceForge do projeto). Argumento de instalação silenciosa `/S` documentado pelo fabricante.
- O EXE 7-Zip é tratado **apenas nessa receita** como artefato de SHA-256 fixado sem exigência de assinatura Authenticode; outros instaladores preservam as verificações existentes de assinatura. Arquivo bloqueado, hash revisitado antes de executar, origem HTTPS restringida e UAC conservado.
- Antes de executar, revalida a instalação/identidade e verifica processos 7-Zip abertos sem encerrá-los automaticamente; após executar, exige mesmo registro, uma única instalação e versão dos arquivos essenciais para confirmar sucesso. Nenhuma exclusão de dados ou migração EXE/MSI.
- Testes sintéticos adversariais de identidade EXE/MSI, arquitetura, edição, pasta, versão, fabricante, hash e argumentos; sem execução de instaladores reais nas GitHub Actions por padrão.
- Apenas o 7-Zip EXE x64 recebeu essa receita. Outros aplicativos só poderão atualizar silenciosamente após validação própria de instalador, modo silencioso, integridade, privilégios e pós-condições.
- Mantém a branch experimental e PR em Draft. Não homologar a build no PC FORTE sem CI aprovada e revisão dos testes.

## v5.13.11.42 TESTE — MISSÃO WORK 7 (CI aprovada; testes físicos pendentes)

- MSI/EXE: seis defeitos adicionais reproduzidos. Cancelamento 1602, ocupação 1618, suspensão 1604 e reinício 1641/3010 interrompem o lote; MSI recebe `/norestart`. EXE com código desconhecido fica pendente. Dois registros com comando EXE idêntico bloqueiam execução; argumentos de produtos distintos continuam permitidos. Reforço aprovado na CI Windows.
- Identidades malformadas: quatro falhas adicionais de UTF-16 reproduzidas e corrigidas; rejeição segura sem interromper as demais fontes.
- 7-Zip: correlação de registros antigos exige escopo, identidade de Registro, mesma pasta local e metadados coerentes; fonte oficial aceita o nome real “x64 edition”. Novas versões oficiais deixam de ser suprimidas por catálogo antigo.
- Windows Update: resultado parcial não vira sucesso vazio; consulta manual é renovada, seleção antiga é invalidada em falha e revisão/licença alterada impede iniciar o lote. Download incompleto não avança para instalação.
- Desinstalação: códigos não zero também consultam estado posterior; ausência transitória não confirma remoção, reinício exigido permanece pendente e lote para enquanto um fornecedor estiver pendente. Desconfiança explícita e erros de assinatura desconhecidos são recusados.
- Resíduos: suspensa exclusão dos perfis foobar2000/OBS/VLC, pois o resumo agregado não comprova integridade dos alvos. Executor sintético não faz parte da aplicação distribuída. R4 permanece aberto.
- RAM: revalidação de PID, início e caminho no mesmo handle usado no ajuste; exclusão mútua das operações. Nenhum working set real é alterado pelos testes.
- CI: pacote portátil depende das regressões Windows e da política de segurança. Teste de reparo passa a selecionar a grade correta sem retirar exigências de cartões/largura.
- CI final do código `616b184f` aprovada: 18/18 controles novos de identidade + 20 existentes; 159/159 multifontes, 127/127 atualizador, 482/482 resíduos/desinstalação, 10/10 mutações, 47/47 MSIX, 30/30 encerramento e 132 combinações de layout. [Pacote `0de83072` conferido](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39868589692/artifacts/12465616744), 273 hashes verificados; sem execução local do binário Windows.
- Roadmap 27/42 (64,3%); U8/R7/R4 e validações físicas permanecem abertos. [Evidências e limitações](docs/MISSAO-WORK7-RESULTADO.md).

## v5.13.11.41 TESTE — ajustes apos testes reais PC FORTE (CI pendente)
- Reparo e rede: manter tres cartoes por linha quando a largura comportar, sem limite de 316 px nem faixas laterais vazias; colunas se adaptam a janelas pequenas. Acoes e salvaguardas permanecem iguais.
- Atualizacoes: o caso real de registros simultaneos do 7-Zip 26.04 (x64) e 26.05 (x64 edition) passa a reconhecer a mesma familia/editor. Registro antigo continua auditavel, sem recomendar atualizar uma versao que ja foi instalada; nenhuma exclusao automatica.
- Regressao: casos negativos para editor divergente, instalacao antiga isolada e catalogo 26.06, mais teste de ocupacao horizontal da grade Reparo e Rede.
- Windows Update: ofertas variam entre consultas; nenhuma alteracao nas autorizacoes. A intermitencia da KB2267602 sera validada separadamente.
- Status: revisao experimental na branch, nao substituir a .40 no PC ate validar CI e pacote.

## v5.13.11.40 TESTE — Correcoes PC FORTE
- Desinstalacao: assinatura Authenticode valida com editor divergente vira aviso confirmado, enquanto adulteracao/revogacao/inconclusivo continuam bloqueados.
- Cartoes Reparar e rede reescritos em portugues simples; comandos nativos continuam com as mesmas salvaguardas.
- Limpeza de perfis passa a ser contextual a produtos suportados e exige ausencia no inventario, confirmacao e revalidacao.
- RAM: diagnostico leve do uso e disponibilidade, inspirado no RAMMap; nenhuma limpeza agressiva de memoria em espera.

# Histórico de versões — Gerenciador Final

## v5.13.11.39 TESTE — PC FORTE, lote integrado

- Executor explícito de limpeza dos perfis pessoais foobar2000/foobar2000-v2 no usuário atual. Antes de permitir exclusão, duas leituras completas do inventário, ausência de foobar.exe, escopo estrito, prévia, confirmação e nova verificação. Bloqueia links simbólicos, junções, perfis profundos e alterações de alvos; não alcança discos de música, Program Files, outros usuários, ProgramData nem chaves compartilhadas do Windows.
- Limpeza contextual nas janelas existentes de resíduos, sem abrir ferramentas externas; chave própria HKCU\\SOFTWARE\\foobar2000 tratada separadamente. Exclui realmente após confirmação, portanto não pode ser desfeita.
- Inventário: fotografa registros completos na sessão e informa quantidade de entradas adicionadas/não observadas na atualização manual. Não instala softwares automaticamente nem mantém monitor residente.
- Verificar Windows continua apenas diagnóstico, liberando Reparar Windows somente quando necessário e mediante confirmação. Testes incluídos para os limites da limpeza. Release somente após CI e sem exceções de antivírus.

## Desinstalação livre de aplicativos Win32 comuns — nova política (a validar)

- O desinstalador EXE registrado **sem assinatura** deixa de ser bloqueado fora do perfil de laboratório. A janela de confirmação informa a ausência de prova de autoria, mas permite continuar por decisão do usuário.
- O executável ainda deve ter origem local registrada, formato PE válido, argumento seguro, identidade de produto revalidada e caminho sem redirecionamentos. Regressões: sem assinatura aceito com aviso; assinatura adulterada, editor divergente e erro de verificação não são confundidos com ausência.
- MSI segue procedimento nativo do Windows. Nada foi desinstalado pela CI; o programa não tenta remover drivers nem serviços protegidos sem rota nativa.
- Bloqueio de um **comando perigoso** não torna seu aplicativo intocável: nesses casos o usuário pode seguir pela desinstalação oficial do fabricante/Windows.

## Excecao opt-in de desinstalador sem assinatura para laboratorio (histórico superado)

- Exclusivamente no perfil `PC_FORTE`, para `Notepad++` e `foobar2000` com nome e fabricante conhecidos, uninstall.exe em pasta exata sob Program Files, arquivo PE e sem reparse point. Apenas quando WinVerifyTrust informa `TRUST_E_NOSIGNATURE`, nao em cadeia invalida, revogacao, editor divergente ou erro qualquer.
- A confirmacao existente explicita a ausência de prova criptografica; unica tentativa, congelamento do EXE ate Process.Start e inventario posterior preservados. Nenhuma excecao para outros aplicativos, fora do perfil de teste ou sem autorizacao.
- Arquivos sem assinatura digital permanecem potencialmente inseguros. Nenhum processo real foi executado pelo CI.

## Lote conservador de correlacao de residuos (a validar)

- Correlacao historica independente para Notepad++ e foobar2000 (inclusive o perfil `foobar2000-v2` documentado pelo fabricante); busca somente em pastas do proprio produto e indices delimitados de referencias do Windows por nome exato do executavel.
- Referencias de chaves compartilhadas **apenas leitura / preservar**, nunca apagar chaves Windows nem carregar valores. App ainda instalado ou inventario parcial bloqueia conclusao de residuo historico.
- Testes negativos de identidade, caminho de rede, arquitetura/versao, referencias e protecao de dados.
- Sem monitor persistente, servico residente ou copia de algoritmo proprietario do IObit.

## Lote de manutenção após .38 — R7 e resíduos históricos (não publicado)

- A saída inicial do desinstalador com código zero e Registro ainda presente após a janela curta agora permanece **pendente**, não falha definitiva nem sucesso. Mantidos cancelamento, dupla leitura completa e uma única tentativa.
- Adicionada observação histórica do Notepad++ após a remoção, somente pastas/chaves específicas e sempre `PreserveData`, sem limpeza e sem varrer chaves compartilhadas do Windows; bloqueado quando ainda instalado ou inventário parcial.
- Novas regressões para remoção tardia, permanência, proteção de outra variante e status parcial. Esta alteração ainda depende da CI.
- U8/R7/R4 permanecem abertos, 27/42 (64,3%).

## Lote de manutenção após .38 — não publicado, CI pendente (22/10/2026)

- DISM/SFC divididos em diagnóstico consultivo e reparo após interpretação conservadora + confirmação. Progresso baseado em percentuais da saída; pesquisa local permanece disponível.
- Escolha de pontos de restauração somente pelo assistente oficial rstrui.exe; nenhuma restauração iniciada automaticamente.
- Otimizar agora atua em uma unidade específica, preserva defrag /O, valida o volume novamente após UAC e remove o botão global.
- MSIX intermitente recebe retry único após pequena pausa no worker de inventário; snapshot parcial não concede direitos de atualização.
- Regressões de segurança, interface, diagnósticos e MSIX ampliadas. Sem nova build pública, sem checkpoints adicionais no roadmap.

## v5.13.11.38 TESTE — layout responsivo universal para Inicialização
- A largura dos cartões agora segue a largura real do viewport, após aplicar scrollbar e padding. Sem reservar duas vezes espaços laterais.
- Número de colunas automático em todos os monitores >=720p e escalas DPI; também mantém o comportamento de degradação para telas antigas menores.
- Redimensionamento reage à mudança de tamanho do conteúdo, inclusive quando a barra vertical aparece/desaparece; proteção contra reentrância.
- Testes sintéticos cobrem todas as 21 resoluções do projeto mais PC FRACO e DPI 100/125/150/200%. Nenhuma ação destrutiva nova; MSIX parcial sem alterações.
- CI e validação física pendentes; roadmap permanece 27/42.

## v5.13.11.37 TESTE — ajustes PC FRACO
- Todos mostra todos os itens de inventário, inclusive protegidos somente leitura; atualizações primeiro.
- Removidos filtros redundantes Possíveis e Inventário; Marcados preservado.
- Atualizar e verificar não reduz a lista apenas aos candidatos no final.
- Drivers: correção de corrida reentrante no botão Cancelar e interface sempre acessível enquanto WMI encerra.
- Inicialização: cartões mais largos, menos espaço desperdiçado à direita.
- MSIX persistente e resíduos parciais continuam protegidos; sem autorizações destrutivas.
- CI e teste físico desta candidata pendentes; roadmap 27/42.

## v5.13.11.36 TESTE — ajustes PC FRACO (CI e validação física pendentes)

Botão **Atualizar e verificar** unifica inventário local e versões HTTPS; a etapa online mantém cancelamento. Consulta de Drivers pode ser cancelada e preserva a lista anterior, sem resultados tardios ou operações de driver. Limpeza e desempenho usa grade de quatro colunas em largura suficiente e painel de discos mais compacto, mantendo rolagem segura em telas pequenas. Erro MSIX `0x800703FA` recebe uma única repetição adicional de leitura, sem fingir cobertura completa. Foram adicionados testes negativos de estado/cancelamento e tratamento MSIX. Sem alteração das proteções de exclusão e sem checkpoints novos (27/42).

## v5.13.11.35 TESTE — MISSÃO WORK 6 (CI aprovada, validação física pendente)

Fluxos de atualizações, desinstalação e prévia de resíduos reforçados: revalidação de autorização, uma tentativa de lançamento, fontes parciais preservadas como revisão, pacote MSIX retido até fim do fornecedor, parser estrito para MSI/EXE, vínculo ao registro de instalação, ausência confirmada por leituras completas, proteção de arquivos e cadeia de diretórios nas prévias. Novo lote de testes de fluxo e 10/10 controles adversariais por mutação, além da suíte regressiva. CI Windows em 39708331526, build em 39708331528 e política em 39708331607 aprovadas. **Não houve instalações, desinstalações ou exclusões reais**, nem validação presencial da .35; U8/R7/R4 continuam abertos. Detalhes em [docs/MISSAO-WORK6-RESULTADO.md](docs/MISSAO-WORK6-RESULTADO.md). Roadmap 27/42.


## 21/10/2026 — U6 concluído, 27/42 (documentação e teste, sem nova build)

Comparação WinGet 9 vs GF .34 8 reconciliada: seis candidatos consultivos, dois com revisão, GameInput protegido. Regressão de nove identidades e falha fechada aprovada nas Actions 39698959079/39698959083/39698959189. Mantido executável .34, sem novo botão administrativo, elevação automática ou WinGet COM/CLI. A futura recomendação de execução administrativa ficará a critério de benefícios comprovados ao fim do desenvolvimento.


## v5.13.11.34 TESTE — correspondência conservadora de servidores (CI aprovada)

Segunda bateria da .33 no PC FORTE confirmou que o inventário real registra
Jellyfin Server com número de versão no nome e Plex Media Server com versão
e arquitetura x64; as receitas anteriores exigiam nomes exatos. Corrigidas
**apenas as identidades consultivas** do Jellyfin Server e Plex Media Server
com regex de versão delimitada, fabricante autorizado e testes negativos
para Jellyfin Media Player, Plex player, preview e nome forjado.
O GameInput continua somente inventário e o Proton continua bloqueado
para instalação direta quando existem fontes divergentes.

A UI esclarece quando candidatos exigem revisão dentro da contagem total.
Não muda motor de instalação, nenhuma dependência nova, nem execução de
WinGet COM/CLI, UAC, limpeza ou manutenção de drivers.
CI Windows aprovada no código final `9c411fb`: compilação x64, regressões com **130/130** testes multifontes e casos negativos de players/preview/fabricantes, e política de segurança/privacidade. A primeira tentativa revelou que faltava o alias canônico versionado do Plex, corrigido e retestado; somente código final aprovado. Nenhum resultado da .34 foi confirmado no PC real. Roadmap 26/42.


## v5.13.11.33 — reconciliação consultiva e barreiras WinGet (TESTE; CI aprovada)

Cinco produtos recebem fontes HTTPS fixas: EA app, FanControl, Jellyfin Server, K-Lite Mega e Plex Media Server, sem novas receitas de instalação. Proton prioriza feed oficial Windows estável/rollout completo e preserva changelog desktop e índice comunitário divergentes. O feed observado ainda oferecia 1.42.0; changelog 1.43.0 não prova distribuição Windows. A fusão preserva evidências, origem, instante e incertezas, separa contexto/identidade e bloqueia instaladores diretos quando requer revisão. FanControl aceita revisões inteiras; migração principal Jellyfin continua consultiva. Barra de consulta conta fontes concluídas, cancelamento preservado, resultado parcial explícito.

Contrato WinGet isolado, sem implementação COM/CLI ou fallback; desligado mesmo com opt-in de produção. Diagnóstico opt-in inspeciona apenas presença de App Installer/alias/PATH do usuário atual, sem executar WinGet, reparar ou elevar. Sem novas dependências/telemetria/IPC. Script de criação de instalador deixa de oferecer instalação de SDK/Inno por WinGet. [Auditoria histórica, testes, matriz dos nove e roteiro](docs/MISSAO-WORK5.md). Roadmap 26/42; PC FORTE, PC FRACO e AVG pendentes.

Código `.33` `7ba87d9`: três Actions aprovadas, 127/127 multifontes, 97/97 instaladores, 360/360 resíduos, 41/41 MSIX, 132 combinações de layout; pacote com 273 hashes conferidos sem execução. [Relatório e links](docs/MISSAO-WORK5.md#ci-final-e-pacote-conferido).

## v5.13.11.32 — auditoria local estruturada de limpeza (TESTE; CI aprovada)

Na segunda bateria presencial da .31, capturas mostraram limpeza real de 47 arquivos/pastas,
17 itens preservados, zero falhas, seguida de cleanmgr do Windows com código 0.
Os eventos só estavam visíveis no painel e não no arquivo de diagnóstico.
A nova versão grava exclusivamente categoria fixa de ação, início, cancelamento,
resumo agregado de temporários e código de saída das limpezas elevadas.
Não grava conteúdo do pipe, nomes de arquivos, caminhos, mensagens de exceção
ou detalhes de usuário; código 0 não significa quantidade de bytes liberados.
O mecanismo de remoção não foi alterado. Testes de campos inválidos incluídos.
CI aprovada em 21/10/2026: compilação Windows x64, regressões (incluindo testes de auditoria), 132 combinações de layout e políticas de segurança. Teste presencial da .32 ainda pendente; roadmap 26/42.


## v5.13.11.31 — MISSÃO WORK 4 (TESTE; CI aprovada)

- MSIX conserva apenas em memória a última leitura completa da sessão quando uma fonte falha; itens retidos têm presença não confirmada e não recebem receitas. Recuperação completa substitui a fotografia; não corrige por suposição a causa de 0x80040154.
- Fusão de atualizações recusa resultados de outra linha/snapshot e revalida somente inventário. Cache de dez minutos usa relógio monotônico, com origem cache/HTTPS observável e regressões de TTL/cancelamento.
- Resíduos registram orçamento e motivos por origem; corrigida contagem além do teto de 128 filhos e de processos já consultados. Ancestrais presos durante enumeração, sem mais raízes, profundidade ou orçamento. Modal contido no monitor do proprietário após escala DPI.
- Consulta de drivers acrescenta vínculo INF publicado ↔ Driver Store por SetupAPI e conferência de volta ao INF exato. Múltiplos dispositivos, presença desconhecida, assinatura WMI, limites e pacotes não consultados são preservados. Não enumera o Store inteiro, não reconstrói histórico Windows e não verifica assinatura criptográfica.
- Diagnóstico opt-in limita todas as tentativas a 160, inclusive troca de perfil e falhas de gravação; sink indisponível não interrompe consultas. Campos novos somente numéricos, sem envio ou persistência adicional.
- Roteiro, matriz P0–P3 e checkpoints consolidados nos documentos existentes. Nenhum checkpoint novo concluído: **26/42**. R4 e G1–G5 bloqueados; main e PR Draft preservados. CI final aprovada: 360/360 resíduos, 41/41 MSIX, 95/95 instaladores, 69/69 multifontes; pacote e 273 hashes conferidos. A integração SetupAPI e um stub antigo reprovaram em rodadas anteriores, foram corrigidos sem remover assertivas. [Evidências e pacote](docs/OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido).


## v5.13.11.30 — comparação de drivers e clareza diagnóstica (TESTE; CI aprovada)

No PC FORTE a .29 mostrou 177 associações de drivers (antes 168), 63 pacotes INF (antes 60) e 9 IDs novos no histórico local, sem entradas ausentes ou alteradas. A segunda leitura foi estável e a consulta PnP passou de 166 para 175; não atribuir a atualização, falha nem remoção sem evidência por ID. O inventário permaneceu em 174 (27 MSIX), com 33 entradas marcadas como inventário somente e 34 sem desinstalação possível. O modo de testes separa essas contagens e discrimina o tipo de fonte com falha de resíduos por rótulos fixos, sem gravar caminhos ou novos identificadores. Não altera varreduras, limites de segurança nem permissões. O botão de otimização recebe rótulo visual compacto mantendo o nome acessível. A investigação de resíduos segue parcial, 867 grupos examinados, 43 candidatos gerais/44 grupos unificados e 161 locais; nenhuma exclusão. Regressões Windows aprovadas (351/351 resíduos, 30/30 MSIX, 132 cenários e verificações das novas métricas), além de build e políticas. Teste presencial da .30 ainda pendente. Checkpoints 26/42.


## v5.13.11.29 — diagnóstico de resíduos e layout para DPI (TESTE)

Bateria presencial da .28 no PC FORTE confirmou 174 aplicativos, 27 MSIX, três possíveis atualizações (7-Zip, Outlook/Store, Proton Pass), 168 associações de drivers e fotografia de drivers estável. Resíduos: 867 grupos avaliados na descoberta geral, 213 preservados por vínculo ativo, 611 sem indícios independentes, 43 candidatos gerais, 44 grupos/161 locais após união com auditorias específicas. Cobertura parcial; nenhuma exclusão.

A .29 deixa explícitos `grupos_avaliados` e `candidatos_gerais`, e renomeia as contagens filtradas no modo voluntário de testes para não confundir os 867 avaliados com os 44 exibidos. No PC FORTE a seleção do modo foi posterior à primeira leitura: `TESTES.INVENTARIO` ausente não significa zero. Linhas de controles de Limpeza e desempenho recebem espaço adicional proporcional ao DPI para reduzir cortes visuais em monitores de 125%+; aprovação presencial ainda pendente. A regressão garante área de conteúdo, não apenas altura nominal. Não foram alterados orçamentos de varredura, remoção de resíduos, ações com drivers, instaladores ou permissões. CI aprovada: 351/351 resíduos, 30/30 MSIX, 132 resoluções/abas e testes de métricas. Roadmap 26/42.


## v5.13.11.28 — coleta comparativa local PC FORTE / PC FRACO (TESTE)

Escolha temporária e explícita em Limpeza e desempenho: Desligado (padrão), PC FORTE ou PC FRACO. Somente após ativar, acrescenta métricas numéricas de inventário, atualizações, drivers, resíduos e diagnóstico completo às consultas que o usuário já solicita. Esquema fechado de categorias e campos; sem IDs de dispositivo, nomes, caminhos, arquivos, dados pessoais, uploads ou novas varreduras. Limite de 160 observações extras por sessão e desativação imediata. Não muda ações de atualização, limpeza, drivers, permissões ou os 26/42 checkpoints. Regressões Windows aprovadas, incluindo 351/351 resíduos, 30/30 MSIX, 132 cenários de layout e testes da coleta opt-in; testes presenciais da .28 ainda pendentes. Limite acumulado por sessão, mesmo após alternar modo, aprovado em teste dedicado. Falhas anteriores de fixture e de layout em 1280×720 foram corrigidas e revalidadas.


## v5.13.11.27 — melhoria conservadora da identificação de drivers (TESTE)

No PC FORTE, a .26 observou inventário estável de 174 entradas, 27 aplicativos MSIX, duas possíveis atualizações, 168 associações de drivers e resíduos parciais em 44 grupos/161 locais. A .27 usa a descrição PnP do MESMO dispositivo quando falta o nome, exibe ID local em tooltip e permite pesquisa por esse ID sem incluí-lo em diagnóstico; versões e assinaturas brutas preservadas. Testes específicos cobrem o fallback e a pesquisa. Nenhum motor de atualização, driver, remoção, rollback, worker, autorização de resíduos ou privilégio foi ampliado. CI Windows da .27 aprovada (351/351 resíduos, 30/30 MSIX, testes de drivers incluindo novo fallback; builds e políticas aprovadas). Teste presencial da .27 realizado no PC FORTE, sem manutenção real de drivers ou resíduos; checkpoints **26/42**.

## v5.13.11.26 — incorporação conservadora (TESTE)

A inspeção sob demanda explica por que preservar favoritos, sessões, credenciais, perfis, playlists, jogos salvos, backups e fontes de instalação. São pistas por nomes, sem ler conteúdo nem comprovar propriedade. Cache não implica abandono ou espaço recuperável. Subpastas genéricas desses dados permanecem associadas ao pai, evitando falsos produtos e fusões entre fabricantes. Recusas de segurança `InvalidDataException` são tratadas como consulta parcial na sessão e interface. Implementação própria após analisar Dism++/WinScript e documentação oficial; nenhum código, catálogo ou workflow externo copiado. Licença MIT, raízes, profundidade, worker, motor de atualizações e exclusão bloqueada preservados. [Procedência, validação e roteiro](docs/OPERACAO-FAXINA-FINAL.md#incorporação-conservadora--v5131126-teste). Roadmap **26/42**; testes nos PCs, MSIX e AVG continuam pendentes.

## v5.13.11.25 — revisão da faxina (TESTE)

Preservação de inventário MSIX observado mesmo em falhas tardias, fase/apartment no diagnóstico e propagação de cobertura parcial ao diagnóstico completo. Nomes de drivers ausentes podem usar o nome PnP do mesmo ID, sem reinterpretar versões. Limpeza temporária e logs passam a conferir a identidade efetiva; consentimento e pendência recusam redirecionamento. Corrigido o contrato GUID/revisão do Windows Update entre consulta, UI e worker, sem instalações no teste. Correções da ABI de restauração, do vínculo MSI/GameInput com o ZIP autenticado e da resolução de desinstaladores do Windows. Certificado extraído do assinante validado pelo WinTrust, sem API obsoleta. Contagens e falhas de fontes de resíduos corrigidas; três arquivos e métodos órfãos retirados. Scripts derivam versão do projeto/PE, CI valida referências e oferece testes externos opcionais. [Relatório único, validações e limites](docs/OPERACAO-FAXINA-FINAL.md). Roadmap mantém **26/42**, PR Draft, resíduos somente leitura, sem release estável. A causa do erro MSIX do PC fraco e a compatibilidade AVG continuam pendentes.

## Manutenção de repositório em 21/10/2026 (sem versão nova de executável)

Removidos da árvore de desenvolvimento workflows versionados antigos e guias de teste v5.12 obsoletos. Mantidos três fluxos CI oficiais, suíte de segurança ajustada à lista explícita, atualização do README e progresso funcional coerente em 26/42. Commits anteriores e resultados históricos permanecem consultáveis.

## v5.13.11.24 — fontes parcialmente lidas e inventário MSIX (TESTE)

Separada falha pontual de leitura de fonte inteiramente indisponível na auditoria de resíduos; painel mais claro e logs com contadores por origem. Testes Windows 327/327 e MSIX 24/24 aprovados; a execução real posterior no PC fraco mostrou erro MSIX 0x80040154 persistente após retry, com inventário parcial de 55 entradas. Nenhuma remoção habilitada.

## v5.13.11.23 — motivos de parcialidade explícitos (TESTE)

Registro sanitizado de falhas e limites na descoberta de resíduos; interface e inventário MSIX preservados. A leitura real MSIX da .23 enumerou 123 pacotes e 19 apps sem retry. Sem exclusão.


## v5.13.11.22 — inventário visível sem ações inseguras (TESTE)

- PC fraco confirmou a leitura MSIX: **123 pacotes**, **19 aplicativos**, **74 entradas** no total, falhas **0**. O retry novo não precisou atuar (`repeticao=False`), então não é possível concluir que ele resolverá uma futura falha persistente.
- Busca por `MSIX` parecia vazia porque a lista exibia somente aplicativos com atualização/desinstalação, e o filtro **Possíveis** estava ativo. Não era ausência de pacotes.
- Novo filtro **Inventário** na aba Aplicativos: mostra também Store/MSIX e jogos já identificados, sem exigir ação autorizada. Busca por origem `MSIX` revela esses aplicativos; dados sem ação têm identificação **Somente leitura** não clicável. A aba padrão **Todos** continua orientada a ações.
- Testes de interface para filtro adicional em resoluções variadas e teste de política confirmando que entradas `InventoryOnly` nunca são desinstaláveis só por aparecerem.
- Terceira bateria agregada: [PC fraco v5.13.11.21](docs/TESTES-HISTORICOS-PC-FRACO.md#registro-5). Nenhum log bruto publicado.
- Permanecem análise parcial de resíduos, nenhuma exclusão autorizada, transmissão de diagnóstico remoto desabilitada e PR Draft. Roadmap **26/42**.


## v5.13.11.21 — proteção do inventário MSIX após terceira bateria no PC fraco (TESTE)

- As capturas reais confirmaram o conserto dos filtros por origem/classificação da v5.13.11.20, sem permitir exclusão de resíduos.
- Diagnosticado `COMException(0x80040154)` ao ativar a API WinRT de inventário MSIX. A leitura caiu de 19 aplicativos MSIX no teste anterior para zero; a lista geral mostrou 55 em vez de 74 entradas. **Não indica desinstalação de aplicativos.**
- MSIX tenta mais uma vez somente nesse HRESULT, sem PowerShell, processos auxiliares, elevação nem leitura de outros perfis. Se a fonte continuar indisponível, o GF anuncia **Inventário PARCIAL** de forma persistente no cabeçalho e no registro; não transforma a falha em consulta completa.
- Casos automáticos para recuperação, falha persistente, acesso negado, texto de interface e propagação da parcialidade para proteção de resíduos.
- [Terceira bateria anonimizada](docs/TESTES-HISTORICOS-PC-FRACO.md#registro-4). Nenhum arquivo de diagnóstico bruto publicado.
- Limpeza de resíduos, instalação/desinstalação e manutenção de drivers não foram liberadas como decorrência desta alteração. Roadmap permanece 26/42.


## v5.13.11.20 — filtros por origem e classificação coerentes (TESTE)

- Segunda bateria real PC fraco 1440×900 identificou que o filtro por origem mostrava todos os locais do grupo, inclusive de outras fontes. Corrigida a interseção entre **origem, classificação e pesquisa** sobre os locais individuais, sem modificar a fotografia original nem permitir exclusões.
- Total de locais passa a indicar apenas locais correspondentes ao filtro; busca por conteúdo de um local não deve corresponder falsamente a outro local do mesmo grupo.
- Listas vazias distinguem "Nenhum candidato revisável" em uma análise parcial de "Nenhum grupo/local corresponde aos filtros", sem sugerir ausência total de resíduos.
- Casos de regressão adicionados ao motor e UI Windows para origem e classificação com dados comuns, perfil do usuário e Registro.
- [Relatório agregado do PC fraco v5.13.11.19](docs/TESTES-HISTORICOS-PC-FRACO.md#registro-3). Excluídos os arquivos brutos e dados pessoais. Exclusão de resíduos e envio remoto continuam desativados.


## v5.13.11.19 — correções após testes reais no PC fraco (TESTE)

- Evidência real em monitor 1440×900 revelou agrupamento incorreto de subpastas genéricas como `Config`, `Logs`, `Updates`, `Scripts` e `Providers`; subpastas homônimas de fabricantes distintos podiam parecer um único aplicativo. A descoberta agora conserva esses locais associados ao nome do contêiner pai, sem os classificar como produtos independentes nem conceder autorização de exclusão.
- Abrange enumeração de subpastas em perfis/instalações e de subchaves de Registro; testes com dois fabricantes fictícios verificam que nenhuma pasta `Config` vira grupo próprio.
- Janela de investigação diferencia lista de grupos vazia de inspeção em nível sem itens; mantém o contexto e a proteção de dados.
- Métricas agregadas e observações estão no [relatório de teste real](docs/TESTES-HISTORICOS-PC-FRACO.md#registro-2), sem anexar diagnósticos brutos ou histórico de IDs de hardware.
- Não foram habilitadas limpeza, atualizações, drivers, remoção de resíduos nem envio de dados. Nova bateria real deverá confirmar agrupamentos e desempenho; progresso funcional continua 26/42.


## v5.13.11.18 — confiabilidade da cobertura e próxima bateria no PC fraco (TESTE)

- Revisão independente da Work 3 detectou inconsistência de cobertura: relatórios de origem marcados como `Incomplete=true`, mas sem `Issues` agregados, podiam aparecer na investigação progressiva como análise normal. A fotografia agora atribui `ReportIncomplete` e informa **relatório de origem incompleto** sem inventar o motivo.
- Se uma origem tiver falha sinalizada apenas em `report.Coverage.Issues`, a consulta agora propaga essa flag para a cobertura geral **sem somar novamente as contagens**. Uma quantidade desconhecida permanece desconhecida.
- Listas de fontes consultadas/indisponíveis são copiadas ao construir a fotografia para que mutações posteriores do produtor não alterem um resultado já apresentado.
- Testes de resíduos cobrem origem parcialmente classificada, informação de falha presente só na linha, contagens não duplicadas, relatório sem cobertura opcional e mutação de listas. Teste WinForms verifica a advertência visível também em uma revisão sem locais.
- Nenhuma alteração no motor de atualização, exclusão de resíduos, desinstalação, drivers ou telemetria. Exclusão real de resíduos continua bloqueada; medições do PC fraco ainda não foram realizadas.
- Esta candidata **não** conclui as 16 etapas funcionais restantes do roadmap; progresso permanece 26/42.


## v5.13.11.17 — investigação progressiva e preparação do PC fraco (TESTE)

- Todos os grupos/locais classificados disponíveis, eliminando os cortes de 100 grupos/sete locais; descoberta continua limitada e explicitamente parcial quando necessário.
- Revisão com cartões virtualizados, busca global, filtros de classificação/origem, páginas de grupos/locais, detalhes e inspeção somente leitura sob demanda.
- Correlação por caminhos usados, aliases e fabricantes ambíguos; instalações a conferir e dados compartilhados permanecem preservados.
- Cache de uma consulta, cancelamento, limites de enumeração/concorrência, liberação de listas ao fechar e resultado pendente na bandeja sem janela inesperada.
- Métricas agregadas locais de duração/CPU/memória/alocações e benchmark reproduzível contra a .16; maior cobertura também tem custo de memória. Sem promessa de ganho no PC real.
- Revisão avançada preserva falhas/limites; decisão exige conferência de integrações e recusa reinstalação. Cinco entradas de exclusão continuam bloqueadas.
- 310/310 verificações de resíduos na consolidação anterior à troca de versão; investigação testada nas oito resoluções de 800×600 a 4K, além da matriz geral de 132 combinações. Falhas de espera de animação e descarte duplo foram corrigidas, sem reduzir asserções.

[Relatório completo](docs/MISSAO-WORK3-v5.13.11.17.md) · [benchmarks e limites](docs/BENCHMARKS-WORK3.md) · [checklist PC fraco](docs/TESTES-HISTORICOS-PC-FRACO.md#registro-7) · [roteiro de histórico extenso](docs/TESTES-RESIDUOS-v5.13.11.17.md). Validações finais, commit de integração, ZIP e SHA-256 registrados na PR #40 após conferência. Pacote de testes sem assinatura; sem merge/release estável, envio remoto ou exclusão de resíduos. Roadmap funcional mantido em 26/42: hardware real e R4 continuam pendentes.

## v5.13.11.16 — integridade e segurança da análise de resíduos (TESTE)

- União deixa de descartar relatórios após os primeiros 500. Classificação contabiliza grupos/locais fora da exibição, cancelamentos, tempo, profundidade, falhas e evidências não avaliadas. Análise parcial permanece visível mesmo sem achados.
- Cobertura acompanha as fontes e se mantém correta ao unir resumos aninhados, sem duplicar contagens. Evidências, tipos e motivos são preservados; aliases da mesma visão do Registro reconciliados sem misturar arquiteturas.
- Preservação geral de perfis, preferências, ProgramData e diretórios compartilhados entre produtos; instalações portáteis, Netease e Driver Booster exigem conferência. Comparação indexada do inventário/processos e cancelamento cooperativo também na classificação.
- Prévia recusa conteúdo misturado, alvos duplicados ou alterados; copia os alvos aceitos e possui timeout na captura e conferência. Hash/identidade não concedem autorização para apagar.
- Cinco pontos internos de exclusão permanecem bloqueados e cobertos por testes, incluindo fixtures Windows de ACL negada, junction, hard links e mudança de arquivo. Não foram alteradas receitas de atualização oficial nem habilitado envio remoto.
- 38 workflows históricos reforçados: Actions em SHA oficial e checkout sem persistência de credenciais. Nomes únicos dos três checks atuais; documentação de proteção administrativa da main ainda pendente do proprietário.

[Relatório, limites e continuidade](docs/MISSAO-WORK2-v5.13.11.16.md) · [Roteiro nos PCs](docs/TESTES-RESIDUOS-v5.13.11.16.md). Resultados finais de CI, origem/commit e integridade do ZIP vinculados na PR #40; pacote experimental sem assinatura. Roadmap funcional permanece 26/42, com exclusão suspensa.

## v5.13.11.15 — descoberta, preservação e prévia de resíduos (TESTE)

- Suspensa exclusão de resíduos de executáveis, perfis e chaves do Registro: reenumeração ampliava o consentimento e não protegia conteúdo/identidade até a remoção. Bloqueio também nos métodos internos, sem opção oculta para reativar nesta build.
- Descoberta limitada a dois níveis para produto sob fabricante, HKCU/HKLM 32/64, pistas de Run/serviços Win32 e perfis históricos de VLC/foobar2000. Sem execução de comandos, leitura de outros perfis, tarefas/drivers ou varredura de discos.
- Decisão por item: revisar, preservar dados, preservar compartilhado ou estado desconhecido. União de evidências sem perder locais/motivos; proteção por MSI, identidade de inventário e caminho ainda utilizado.
- Prévia pós-desinstalação com identidade por handle, recusa de hard links e comparação SHA-256 dos executáveis reconhecidos; botão **Conferir novamente**, sem botão de apagar. Comparação não é prova de exclusividade nem rastreamento completo de subprocessos.
- Testes negativos adicionais, fontes limitadas/canceláveis, classificação com milhares de evidências e regressão da interface. Correção do validador histórico que rejeitava o nome curto VLC.
- Consentimento continua apenas local; sem envio remoto, merge na main, release estável ou nova funcionalidade de drivers. Desinstaladores oficiais continuam exigindo confirmação e têm sua própria política de dados.

Validação e limites: [missão de resíduos](docs/MISSAO-RESIDUOS-v5.13.11.15.md). Consultar os três workflows da revisão exata antes de usar o artefato; testes reais de desempenho, AVG, ACL/junctions e manutenção destrutiva continuam pendentes.


## v5.13.11.14 — resíduos WinMerge após teste real (TESTE)

- **Notepad++ 8.9.8.1:** o teste real confirmou integridade do instalador, pré-verificações, execução e versão instalada.
- **WinMerge x64:** a entrada de desinstalação desapareceu e o inventário caiu de 175 para 174 aplicativos; identificada uma pasta de instalação remanescente, sem remoção automática.
- Auditoria somente leitura agora reconhece `HKCU\Software\Thingamahoochie\WinMerge`, chave documentada pelo fabricante, e as visualizações HKLM da mesma subchave, quando acessíveis.
- **Resíduos de aplicativos** também pode consultar a família WinMerge historicamente, sem exigir reinstalação ou apagar preferências; protege variantes ainda instaladas. Nenhum achado significa autorização de exclusão.
- A lista de programas é atualizada antes do relatório de revisão após desinstalação. A mensagem diferencia **ausência do registro** de **término integral do desinstalador do fabricante**, pois a captura mostrou uma janela nativa ainda aberta.
- Testes verificam a identificação exata do WinMerge, fabricantes homônimos, o bloqueio com instalação ativa e a natureza não destrutiva do relatório histórico.
- As 11 famílias de perfis de fabricante/jogos e os candidatos Driver Booster/Netease continuam protegidos, só para revisão.
- [Relatório da simulação](docs/SIMULACAO-MANUTENCAO-WINMERGE-2026-10-20.md).


## v5.13.11.13 — pequenos ajustes de legibilidade após teste real (TESTE)

- Capturas da v5.13.11.12 confirmaram **três cartões por linha** na subaba Inicialização e o indicador persistente **Coleta local ativa** no cabeçalho, sem solicitar novamente consentimento já concedido.
- O botão da área de memória agora mostra **Abrir tarefas**, com nome acessível e dica completa **Abrir Gerenciador de Tarefas do Windows**, corrigindo corte do texto na janela proporcional.
- O filtro da lista DRIVERS agora mostra **Outros**, com explicação acessível de que inclui associações não presentes ou indeterminadas; nenhuma é automaticamente considerada obsoleta.
- A validação Windows passa a exigir os rótulos curtos e seus textos acessíveis. Sem mudança de execução de atualizações, drivers, limpeza, desinstalação, redes, energia ou telemetria.


## v5.13.11.12 — inicialização compacta e indicador de privacidade (TESTE)

- A aba **Inicialização** (dentro de Inicialização e sistema) distribui cartões em **três colunas quando a largura permite**, duas em janelas intermediárias e uma nas estreitas, sem alterar as grades de Windows Update e outras subabas.
- Cartões de inicialização mais baixos e compactos: títulos e descrições com dicas integrais/acessibilidade; confirmação e ações de ativação/desativação preservadas.
- **Consentimento local não reaparece automaticamente após trocar o ZIP**, porque o aceite/recusa anterior fica persistido fora da pasta do programa, evitando solicitação repetida. A janela principal agora mostra continuamente **Coleta local ativa**, **Coleta desativada** ou **Configurar privacidade**, com botão para revisar a escolha.
- O botão reflete imediatamente aceitação ou revogação; a coleta continua somente local, sem transmissão, arquivo enviado ou novo consentimento remoto.
- Testes de layout verificam transições 1/2/3 colunas, margens mínimas, estado persistente visível e acessibilidade.


## v5.13.11.11 — lista de drivers no mesmo padrão dos aplicativos (TESTE)

- Removida a tabela técnica com colunas da aba DRIVERS; exibida lista de cartões escuros com nome em destaque, informações complementares, ícone, seleção e rolagem escura, seguindo a aba Aplicativos.
- Cada cartão exibe versão, fornecedor, INF associado, assinatura informada pelo Windows e status de presença (presente, não presente ou indeterminado), sem confundir ausência com driver obsoleto.
- Campo de pesquisa por dispositivo, fabricante, versão ou INF; filtros Todos, Presentes e Não confirmados (inclui não presente e indeterminado), sem instalação, atualização ou remoção.
- Renderização virtualizada dos cartões: apenas linhas visíveis são desenhadas, evitando centenas de controles no PC de baixo desempenho.
- Mantidos captura WMI, correlação de pacotes, histórico local e registros de diagnóstico anteriores, com lista integralmente somente leitura.
- Testes Windows validam lista, filtros com 184 entradas fictícias e layout na matriz de resoluções; em 800×600, pesquisa e consulta permanecem visíveis, os filtros passam a um seletor compacto e o rodapé se reduz para preservar um cartão completo, sem perder o estado acessível.


## v5.13.11.10 — tabela de drivers responsiva (TESTE)

- Capturas reais da v5.13.11.8 mostraram a tabela de drivers utilizando somente parte da largura da aba e truncando os dados.
- As seis colunas agora preenchem proporcionalmente a largura disponível, com larguras mínimas para preservar a usabilidade e permitir rolagem horizontal em telas estreitas.
- Regressões de layout conferem a configuração da tabela na matriz de resoluções do projeto.
- Diagnóstico, limpeza, atualização, desinstalação e reparo **não tiveram seus mecanismos de execução alterados** nesta revisão.
- Observações de campo e limitações: [teste real de 20/10/2026](docs/TESTE-REAL-v5.13.11.8-2026-10-20.md).


## v5.13.11.9 — legibilidade do diagnóstico colaborativo (TESTE)

- Corrigidas as quebras de parágrafo do componente `DarkLogView.SetText` em Windows: converte novas linhas para CRLF sem duplicar `\r` e sem alterar o registro ao vivo.
- Janela de privacidade apresenta texto introdutório mais curto, fonte Segoe UI no relatório e explicações com acentos em português.
- Mensagem da simulação agora informa `SIMULAÇÃO CONCLUÍDA — NADA FOI ENVIADO`, evitando confundir simulação com aprovação de consentimento.
- Testes Windows exigem separação visual real dos parágrafos antes e depois do clique, além de isolamento da simulação, sem rede, arquivos ou autorização.
- Nenhum envio de diagnósticos foi ativado; recurso continua apenas local e voluntário.


## v5.13.11.8 — simulação de contribuição sem envio (TESTE)

- Janela de privacidade usa linguagem simples: mostra claramente quando a coleta local está ativa ou desativada, sem obrigar a interpretar JSON.
- Novo botão **Simular sem enviar**, sem exigir consentimento ou conta: cria uma demonstração fictícia estritamente em memória, validada pelo esquema de dados permitido.
- Simulação não lê informações reais do computador, não grava arquivo, não comunica com rede e não altera consentimento. Testes automatizados validam esses limites e a presença do controle na interface.
- A coleta continua apenas local e voluntária; envio remoto, autenticação de participantes, manutenção por IA e publicação automática permanecem desativados.
- Definidos critérios para uma futura contribuição simples, sem upload de arquivos e sem exigir GitHub do usuário final. O receptor de homologação é protegido por segredo exclusivo do servidor, desligado por padrão e testado apenas com tráfego fictício. Nenhuma transmissão foi ativada no GF. Veja docs/EXPERIENCIA-CONTRIBUICAO-SIMPLES.md e docs/INFRAESTRUTURA-ENVIO-SEGURO.md.


## v5.13.11.7 — bloqueio de envio manual e verificações adicionais (TESTE)

- Removida a publicação manual de diagnóstico para Issues públicas; GitHub Actions não aceita mais eventos de Issues como entrada. Coleta permanece estritamente local e consentida.
- Validador fechado de JSON técnico; nenhuma categoria, campo ou anexo arbitrário é admitido; proteção contra contagens ambíguas e timestamps futuros.
- Prototipo de recepção remota desativado por padrão e sem possibilidade de gravação nesta candidata.
- Atualização automática exige receita fixa com SHA-256 e assinatura; Visual C++ 2013 sem hash passa a oferecer apenas página oficial.
- Windows Update valida GUID e revisão selecionada antes da instalação; sem autorização de aceite de EULA ou reinício automático.
- Testes adicionais de privacidade, bloqueios de instalador e políticas de Actions; workflows legados remanescentes fixados por SHA.
- **Ainda é candidata de testes**: não promete ausência absoluta de riscos nem aprovação por AVG. Detalhes em docs/SEGURANCA-v5.13.11.7.md.

## v5.13.11.6 — caminhos protegidos e defesa adicional (TESTE)

- Ancestrais abertos da raiz ao destino, sem seguir reparse points nem compartilhar exclusao/renomeacao; recusa UNC, dispositivos e ADS.
- Arquivo e ancestrais de instaladores permanecem protegidos durante a sessao, mesmo apos retorno do processo inicial. Limite de 32 pacotes; temporarios retidos nao sao apagados durante a sessao.
- Pasta de trabalho do instalador no sistema; arquivos/pastas inesperados junto do pacote bloqueiam lancamento. Isso nao controla DLLs carregadas internamente por instaladores externos.
- Escrita de privacidade e downloads com diretórios presos; auditoria NuGet bloqueante e configuracao Dependabot semanal.
- Testes de renomeacao de ancestral, symlink de pasta, namespaces, retencao e arquivos vizinhos. Ver docs/SEGURANCA-v5.13.11.6.md.

## v5.13.11.5 — protecao de instaladores, pipe e arquivos locais (TESTE)

- Receita integral aprovada e comprovante em memoria vinculam o instalador ao download desta sessao.
- SHA-256 e assinatura sao revalidados antes da execucao; arquivo permanece sem compartilhamento de escrita/exclusao enquanto o processo iniciado e aguardado. Terminal recebe trava equivalente durante deployment.
- P/Invokes do GF usam somente a pasta de sistema para carregar DLLs nativas.
- Pipe administrativo verifica PID informado pelo Windows e limita espera da autenticacao a cinco segundos.
- Privacidade usa temporarios exclusivos com CreateNew; recusa pastas redirecionadas e limita leitura pelo arquivo aberto.
- Testes de adulteracao, receitas forjadas, PID incorreto e temporarios preexistentes. Limites documentados em docs/SEGURANCA-v5.13.11.5.md.


## v5.13.11.4 — auditoria de segurança, diagnóstico e consistência (TESTE)

- Comandos internos Windows e Gerenciador de Tarefas usam caminhos absolutos da pasta de sistema, com lista fechada; evita procurar executáveis homônimos no diretório portátil/PATH.
- Limpeza avançada só pode excluir pastas/chaves simultaneamente válidas na reconsulta e presentes na confirmação original.
- Diagnóstico expirado não pode ser compartilhado quando sua exclusão é bloqueada pelo Windows; sessão vazia preserva pendência anterior.
- Reaceitar coleta já ativa preserva contagens; consentimento inválido/ambíguo e versão com quebra de linha são recusados.
- Roadmap reconciliado: a checklist publicada contém 27/42 checkpoints (64,3%), diferente do resumo histórico 30/45. Evidências de monitoramento atualizadas para o painel RAM.
- Regressões para comandos, escopo autorizado, consentimento, retomada e expiração; relatório em docs/AUDITORIA-v5.13.11.4.md.


## v5.13.11.3 — retomada e revogação do diagnóstico (TESTE)

- Revisão no GitHub usa o resumo da sessão atual ou, sem eventos novos, o relatório local anterior ainda válido, preservando sua versão original.
- Pendências são revalidadas por tamanho, conteúdo permitido e prazo de sete dias também ao abrir a revisão.
- Revogar interrompe a coleta imediatamente nesta sessão mesmo quando o disco impede gravar a preferência; falhas de gravação/exclusão são informadas sem afirmar sucesso.
- Aviso antes de abrir o navegador explica que a URL já transmite o resumo ao GitHub; criar a Issue pública ainda exige confirmação no site.
- Regressões para retomada, prioridade da sessão atual, adulteração, expiração durante a execução e falhas de disco.
- Publicação continua manual no GitHub; testes reais/AVG e automação por IA continuam pendentes.


## v5.13.11.2 TESTE — piloto colaborativo GitHub-only
- Menu da bandeja permite revisar dados técnicos agregados e abrir um formulário de Issue no repositório público, com autenticação pelo próprio navegador do usuário.
- Dupla confirmação humana: aviso de publicidade no GF e envio final no GitHub. Nenhum token, conta vinculada automaticamente, upload ou transmissão invisível é adicionado.
- Filtro estrito de schema, versão, categorias e contagens antes de montar URL; recusa de dados arbitrários/relatórios vazios; testes offline dedicados.
- Mantém a coleta local/consentimento anterior. GitHub App e envio automático ao encerrar ainda não implementados.


## v5.13.11.1 TESTE — privacidade e diagnostico colaborativo local
- Consentimento explícito na primeira abertura, recusável e revogável pelo menu da bandeja; sem caixa pré-selecionada.
- Resumo estrito com versão do GF, encerramento e contagens de 14 categorias técnicas, sem logs brutos, nomes, caminhos, inventário ou IDs persistentes.
- Somente um relatório pendente local, substituído ao encerrar, com expiração de 7 dias e limpeza ao revogar; não utiliza a rede.
- Código de referência para recepção HTTPS/Cloudflare D1 com TTL, ainda **não implantado**. Transmissão automática futura dependerá de novo consentimento e revisão de privacidade.
- Testes próprios de opt-in, recusas, limites, sanitização, rotação e retenção.


## v5.13.11.0 CANDIDATA — novas fontes e consolidação para GitHub
- Quatro novas consultas oficiais somente leitura: ShareX, HandBrake, Rufus e qBittorrent. URLs fixas GitHub Releases; nomes e editores conferidos em catálogo interno antes da chamada. Nenhuma instalação automática habilitada pelas novas fontes.
- Regressões com fontes simuladas: candidatos, versões oficiais, falso editor, nomes parecidos, cache, pré-lançamento, falha de provedor e fusão sem duplicar linhas.
- Bateria abrangente Windows para comparar motores de versões, instaladores, drivers, resíduos, RAM, inventário MSIX e compatibilidade de interface.
- Projeto e manifesto atualizados para v5.13.11.0; README e identificação do pacote atualizados; nome do ZIP com versão e commit, BUILD-INFO e SHA256SUMS verificado em CI.
- Consolidado o desenvolvimento das séries v5.13.10.19 a v5.13.10.24: histórico de drivers, resíduos específicos, tema escuro, painel de RAM, otimização de caches próprios, instrumentação de bandeja/erros MSIX e validação automatizada.
- Estado: CANDIDATA. Validação em PC FRACO e segundo PC, assinaturas digitais e avaliação de alerta AVG ainda pendentes. Sem afirmação de segurança final, estabilidade pública ou cobertura universal.


Este arquivo resume as alterações mais recentes do projeto. As versões anteriores a 5.12 são etapas históricas do desenvolvimento e não representam versões publicadas no GitHub.

## 5.13.10.18 TESTE — consolidacao e inventario consultivo de drivers
- Diagnostico de desinstalacao considera ambas as alternativas registradas; caminho absoluto ausente nao herda confianca do nome msiexec.exe.
- Leitura incompleta do Registro bloqueia os fluxos que precisam confirmar ausencia antes da limpeza; auditoria contextual informa limitacoes do inventario.
- Confirmacao posterior exige a identidade original da instalacao; Sumatra confere tambem arquitetura PE e escopo. Mudanca de identidade permanece indeterminada, sem afirmar sucesso.
- Diagnostico de cobertura registra apenas contagens por motivo, sem nomes de aplicativos; nao presume equivalencia com WinGet.
- DRIVERS recebe tabela consultiva de associacoes dispositivo/INF/versao/fornecedor, presenca e assinatura informada pelo WMI, sob demanda e em segundo plano. Nao enumera todo Driver Store, nao instala, remove ou reverte drivers.
- Consulta de drivers tem limite de espera na interface e impede consultas sobrepostas enquanto WMI termina. Identificadores completos de dispositivos nao sao gravados no log.
- Consulta WMI real validada no runner; enumeracao COM corrigida apos falha reproduzida.
- Aba DRIVERS preserva altura da tabela e permite rolagem em janelas pequenas; captura 800x600 revisada.
- Nove grupos de regressao, 22 cenarios de layout e ativacao do pacote aprovados. Validacao nos computadores do usuario continua pendente.

## 5.13.10.17 TESTE — consolidacao antes dos testes nos computadores
- Verificacao de caminhos de desinstalacao movida para o mesmo trabalho em segundo plano do inventario, antes de entregar o snapshot a interface.
- Resumo acessivel e dica do contador distinguem inventario completo, itens com acao, sem acao, desinstalaveis e atualizacoes, sem duplicar itens com duas acoes.
- Confirmacao posterior de instalacao exige editor reconhecido e recusa entradas somente leitura; nome e versao isolados nao confirmam sucesso.
- Novos casos de regressao para contadores, fabricante divergente, arquitetura Visual C++, receita desconhecida e versao antiga.
- Roadmap de consolidacao com checkpoints verificaveis; validacao nos dois computadores permanece separada.
- Nao comprova resolucao do AVG, ganho de desempenho real ou cobertura universal. Correlacao de arquitetura/escopo na confirmacao posterior de todos os instaladores permanece pendente.

## 5.13.10.16 TESTE — PC FRACO e procedencia verificavel
- A amostragem do monitor usa 2 s somente na aba Processos, 12 s nas demais abas e 20 s ao minimizar para a bandeja; a coleta continua na bandeja.
- A primeira coleta de processos aguarda o intervalo do timer, sem competir imediatamente com o inventario inicial; ao abrir Processos ou restaurar a janela, atualiza sob demanda.
- Consulta de atualizacoes de aplicativos precede a consulta do Windows Update, com pausa curta; ambas continuam somente leitura e sem instalacao automatica.
- O artefato inclui hashes SHA-256 para o executavel e seus arquivos principais; facilita confirmar a integridade, mas nao substitui assinatura digital ou avaliacao do AVG.
- Nenhuma protecao e desativada, nenhuma exclusao antivirus criada e nenhuma deteccao e contornada. Ganhos de desempenho devem ser verificados no PC FRACO.

## 5.13.10.15 TESTE — monitor de processos semelhante ao Gerenciador de Tarefas
- Substitui cartoes de CPU por tabela de processos com CPU instantanea, RAM, PID e entrada/saida em KB/s; colunas ordenaveis e filtro por nome/PID.
- CPU de processos usa diferenca de TotalProcessorTime normalizada pela quantidade de processadores logicos; CPU total usa GetSystemTimes; RAM total usa GlobalMemoryStatusEx; trafego total de rede usa contadores de adaptadores ativos.
- GetProcessIoCounters informa bytes de leitura/escrita acumulados e suas taxas. Esse numero nao e disco fisico: nao inferir consumo de disco nem rede por processo.
- Primeira amostra e acesso negado exibem valor indisponivel em vez de inventar zero. Processos que terminam nao interrompem o monitor.
- Atualiza a cada 2 segundos somente quando a aba Processos esta visivel; em segundo plano e na bandeja passa para 5 segundos; nenhuma operacao pode encerrar processos.
- Tabela WinForms com pintura dupla e atualizacao incremental de celulas (sem apagar todas as linhas quando a ordem nao muda), com filtro preservado e scroll restaurado.
- Botao Tarefas do Windows abre Task Manager nativo para informacoes que nao sao medidas pelo Gerenciador Final.
- Ampliados testes de layout e testes de amostras CPU/E/S, memoria, PID, ordenacao, filtro, refresh sem remontagem e dados sinteticos. Preservadas regras de seguranca e residuos.
- Nenhum codigo ou elemento grafico proprietario do Task Manager foi copiado.

## 5.13.10.12 TESTE — descricoes da ultima aba e Atualizar lista na area correta
- A aba Inicializacao e sistema volta a exibir as quatro descricoes completas, em texto legivel abaixo dos botoes (Processos, Inicializacao, Windows Update e Este computador), com aviso acessivel e tooltip.
- Amplia a altura reservada aos comandos para evitar que suas descricoes fiquem ocultas por falta de espaco.
- Corrige seis verificacoes com indice de aba antigo no painel integrado: depois de incluir DRIVERS, o Sistema passou a ser a sexta aba (indice 5), evitando deixar de exibir resultados atualizados de Processos e Windows Update.
- A opcao Atualizar lista sai da faixa de pesquisa e passa a integrar os cinco comandos de Aplicativos, antes de Verificar atualizacoes.
- Em janelas estreitas, os cinco comandos sao distribuidos em duas linhas proporcionais (tres em cima e dois embaixo), sem abreviar nomes.
- O campo de pesquisa mantem apenas Pesquisar, filtro textual e Limpar; Atualizar lista permanece uma leitura local, enquanto Verificar atualizacoes realiza consulta online.
- Testes automatizados cobrem as descricoes de cada comando nas 22 resolucoes da matriz, visibilidade das legendas, posicao de Atualizar lista, distribuicao responsiva dos cinco comandos e ausencia de indices antigos da ultima aba.
- Nao modifica acoes de drivers, politicas de limpeza, inventario ou o tratamento conservador de residuos de GOG.com, McAfee e Microvirt (somente leitura).
- Versao ainda sujeita a avaliacao presencial no PC FRACO.

## 5.13.10.11 TESTE — sexta aba DRIVERS e navegacao uniforme
- Insere a aba DRIVERS imediatamente depois de Aplicativos; e uma area apenas visual com Inventario, Atualizacao/instalacao e Historico/reversao como funcionalidades futuras.
- Nenhum driver e consultado, instalado, removido ou alterado pela aba provisoria. Nenhuma funcao de manutencao existente foi modificada.
- As seis abas distribuem-se uniformemente em toda a largura disponivel, com margens laterais simetricas de ate 12 pixels e pequenas separacoes iguais entre botoes.
- Titulos completos sao desenhados em uma linha sempre que possivel; em janelas estreitas, usam quebra de linha natural para preservar o texto.
- Ajusta os indices internos de Limpeza, Reparo, Planos de energia e Sistema para evitar carregamento da pagina errada apos inserir DRIVERS.
- A pagina provisoria possui tres cartoes informativos, reorganizados em uma, duas ou tres colunas conforme a largura disponivel. Sem botoes que prometam funcionalidades inexistentes.
- Aumenta a bateria de testes de layout para 22 cenarios x 6 abas = 132 combinacoes, verificando margens simetricas, largura uniforme, hit-test de todas as abas e ausencia de acao na pagina DRIVERS.
- Testa 12 ciclos de bandeja sem recriar janela, 150 linhas de Registro em um lote e sete escalas simuladas de DPI alem dos testes funcionais e de seguranca ja existentes.
- Mantem o limite de ate 60% da area util como teto, podendo ocupar menos quando adequado. A aparencia final ainda requer conferencias no Windows real e no PC FRACO.

## 5.13.10.10 TESTE — densidade visual consistente em 1920x1080 e 2560x1440
- Preserva a janela proporcional e o teto de 60% da area util nas 21 resolucoes.
- Alinha a configuracao DPI do executavel e dos testes no modo PerMonitorV2; trocar de monitor ou escala 100/125/150% deve manter textos com escala nativa do Windows.
- Centraliza as cinco abas com o mesmo espacamento dos dois lados, incluindo a logica de acerto de clique e navegacao por teclado.
- Limpeza distribui oito ferramentas em no maximo quatro colunas, e Reparo distribui cinco ferramentas em no maximo tres colunas; os cartoes aproveitam melhor a largura sem esticar botoes.
- Discos locais podem usar tres colunas quando ha espaco; altura do painel de limpeza acompanha os discos detectados em vez de preencher o resto da janela com uma caixa vazia.
- Diagnostico de rede passa a ter altura baseada no conteudo, deixando a area sem informacoes no fundo neutro.
- Alteracoes de resolucao do Windows recalculam o tamanho proporcional da janela, com 170 ms de consolidacao de eventos para evitar redesenhos repetidos; o redimensionamento manual continua sujeito ao limite.
- Acrescenta testes de distribuicao de cartoes em Full HD e 1440p, preservando rolagem, bandeja e protecao de dados.
- Nenhum mecanismo de busca, desinstalacao, limpeza ou driver foi alterado nesta revisao visual.

## 5.13.10.9 TESTE — proporcao universal de ate 60% da area da tela
- Substitui a janela inicial fixa de 875x745 por dimensoes proporcionais a area util real do monitor, independentemente da resolucao (inclusive 800x600 e 8K).
- A abertura utiliza cerca de 57,4% da area util (70% da largura e 82% da altura); o limite de redimensionamento usa no maximo 59,76% (72% da largura e 83% da altura).
- O conteudo interno agora acompanha todo o espaco da janela, sem o teto de 875x745 escalonado por DPI que mantinha a interface pequena em 2560x1440.
- Mantem os tamanhos de fonte controlados pelo DPI do Windows: a janela cresce sem multiplicar artificialmente textos e botoes.
- Ao trocar de monitor ou DPI, limita novamente o tamanho e reposiciona a janela dentro da area util, sem salvar configuracoes no disco.
- Nao permite maximizar. Continua com minimizar para bandeja, menu Abrir/Sair e operacoes de leitura em andamento.
- Testes de 21 resolucoes + 1440x900 e de monitores 125% verificam limite de area, minimo de aproveitamento de 55% e centragem.
- A interface visual final continua sujeita a capturas e validacao nos computadores de laboratorio e PC FRACO.
- Nenhuma alteracao em limpeza, atualizacao, catalogo, instaladores, drivers ou registros historicos.

## 5.13.10.8 TESTE — pintura leve e Registro estavel para computadores antigos
- Remove WS_EX_COMPOSITED aplicado indiscriminadamente a toda a janela; o estilo pode gerar custo alto de repintura em GPUs integradas e maquinas sem placa dedicada.
- Usa buffering somente na janela principal, mantendo a estrutura nativa e a transicao oculta/visivel pela bandeja.
- Prepara o layout antes de Show() ao restaurar a bandeja, evitando exibir uma arvore suspensa que precisaria ser remontada.
- Na abertura, executa o inventario sem animar o progresso ou alternar estados de dezenas de controles; operacoes solicitadas pelo usuario continuam com progresso e bloqueios.
- Agrupa mensagens consecutivas do Registro em lotes de 85 ms, sem descartar linhas; a fila existe apenas em memoria.
- Recalcula barra do Registro so quando ha mudanca de metricas, limitando invalidacoes em cascata.
- Teste no Windows verifica composicao global desativada, 70 linhas preservadas em um unico lote, 21 resolucoes e 6 transicoes para a bandeja.
- Nao altera a classificacao conservadora de residuos, os limites de exclusao nem a deteccao de drivers. Os registros historicos permanecem somente leitura.
- A aprovacao depende de observacao no PC de laboratorio, alem de verificacao posterior no PC FRACO.

## 5.13.10.7 TESTE — pintura unica, botoes proporcionais e sinais de programa ativo
- A janela utiliza WS_EX_COMPOSITED para compor todos os controles antes de apresenta-los; a restauracao evita reorganizacoes intermediarias enquanto esta oculta.
- Nomes das cinco abas passam a ser desenhados em linha unica com tipografia ajustada somente quando necessario, sem reticencias.
- Reduz alturas de abas e dos cartoes de ferramentas, libera largura na pesquisa, evita esticar botoes de acoes ate preencher todo o espaco.
- Remove a quebra manual no nome Limpeza de Disco automatica; tipografia dos botoes se ajusta ao espaco, sem truncar o titulo.
- Os contadores da aba Aplicativos usam os nomes completos quando couberem, eliminando as abreviacoes 'Apps' e 'Atual.' na abertura normal.
- A revisao historica cruza tambem nomes de processos em execucao, inclusive programas portateis como MPC-HC, sem concluir remocao pela ausencia de processo.
- O reconhecimento adicional nao apaga dados e nao confunde executaveis inativos, saves ou pastas remanescentes com prova de desinstalacao.
- Testes verificam estilo nativo de composicao, seis restauracoes com HWND estavel, botoes sem quebras, matriz de resolucoes e verificacoes de residuos somente leitura.
- GOG.com, McAfee e Microvirt foram informados como desinstalados no computador de teste; MPC-HC permanece ativo segundo o usuario. A ferramenta continua exigindo evidencias locais e nao transfere esse estado individual para outros computadores.
- A avaliacao de fluidez real no computador permanece necessaria.

## 5.13.10.6 TESTE — controle de rolagem e informacoes sem cortes
- Corrige rolagem fantasma provocada pela margem tecnica de 3 pixels no componente de pagina.
- Os cartoes de limpeza, reparo e discos deixam de ter barras internas ao serem incorporados numa pagina; se o conteudo for maior, apenas a pagina rola.
- A altura do painel de discos e calculada conforme a quantidade de unidades, atualizada quando a leitura termina, sem comprimir cartoes.
- Reparo e energia ocupam menos altura e evitam a barra externa quando a pagina cabe na area visivel.
- Botoes de disco e margens do painel de limpeza foram reduzidos sem remover funcionalidades; os limites de seguranca permanecem os mesmos.
- Os quatro atalhos da aba Inicializacao e sistema usam legendas curtas que cabem; as explicacoes completas ficam em dicas de acessibilidade.
- Resumo de aplicativos mostra sempre quantidade e atualizacoes, sem cortar a legenda apos o primeiro numero.
- Testes automatizados em 21 resolucoes e na referencia 1440x900 checam se existe rolagem duplicada ou fantasma.
- Bandeja, ausencia de maximizar, limite de 60 por cento e diagnostico local permanecem inalterados.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem classificados de modo conservador e so leitura, sem exclusao automatica.
- Aprovacao final depende da verificacao visual real, especialmente ao alternar de aba e revisar varias unidades de disco.

## 5.13.10.5 TESTE — bandeja fluida, mais informacao por tela e diagnostico
- Nao alternar ShowInTaskbar ao minimizar/restaurar: Hide e Show preservam o mesmo HWND e evitam repintura por recriacao.
- Suspende reorganizacoes de paginas e barras de rolagem durante transicoes de bandeja; amostragem de processos e consultas continuam ativas.
- Registra eventos de visibilidade e criacao/destruicao de janela no diagnostico ja existente, para diferenciar repaint de novo processo.
- Interface mais compacta: margens menores, cabecalho e comandos baixos, abas estreitas em duas linhas e log preservado.
- Linhas de aplicativos com 67 px, texto com quebra em vez de reticencias, dicas com identidade/versao integrais.
- Ferramentas com cartoes de 244x124 px e ajuste para 3 colunas quando houver largura; botoes aceitam multiplas linhas.
- Processos, inicializacao, atualizacoes do Windows e planos de energia usam cartoes menos altos e espacos internos menores.
- Reduz alturas minimas de paginas, conservando rolagem quando necessaria para nao esconder botoes.
- Testes de seis ciclos de minimizacao/restauracao sem mudar o HWND, alem da matriz de resolucoes e testes de seguranca anteriores.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem historicos e somente leitura; nenhuma exclusao automaticamente autorizada.
- A ausencia de duplicacao de APP.INICIALIZACAO no log anterior nao confirma a causa do desaparecimento, motivo pelo qual foram adicionados marcadores de ciclo de janela.
- Validacao visual no PC real e testes de longos nomes/monitores continuam necessarios.

## 5.13.10.4 TESTE — janela discreta e bandeja do Windows
- Em monitores a partir de 1366x768 (area util correspondente), a janela fica abaixo de 60% da area util; resolucoes menores mantem excecao para usabilidade.
- Tamanho inicial compacto de 875x745 ou menor quando necessario, sem aumentar fontes ou botoes conforme a resolucao.
- Removida a funcao e o botao de maximizar. Barra superior escura personalizada, com somente minimizar e fechar, incluindo dicas e realce ao passar o mouse.
- Minimizar oculta o programa da area de trabalho/barra de tarefas e mostra o icone de bandeja sem interromper tarefas, consultas nem a coleta periodica de CPU/RAM.
- Menu de bandeja Abrir e Sair; duplo clique restaura. Fechar encerra o aplicativo, diferentemente de Minimizar.
- Quando Sair e solicitado durante operacao protegida, solicita cancelamento seguro quando disponivel e espera a operacao terminar antes de encerrar.
- Nenhum arquivo de configuracao ou residuo novo e persistido por esses controles.
- Testes de layout e bandeja automatizados no Windows; validacao visual real continua necessaria.

## 5.13.10.3 TESTE — janela inicial compacta como a referencia 1440x900
- Substitui o tamanho inicial de 1250x980 pela dimensao de referencia 875x745: em monitores grandes, a janela deixa espaco livre ao redor.
- Abre centralizada dentro da area util do monitor; em telas pequenas limita ao espaco disponivel, sem perder navegacao, rolamento ou o registro.
- Mantem fontes, botoes e proporcoes visuais do layout atual sem ampliar a interface com a resolucao fisica (4K, 5K, 8K).
- Mantem suporte a redimensionamento manual, troca de monitor e escalonamento DPI existente.
- Acrescenta validacao explicita da referencia 1440x900, alem das 21 resolucoes anteriores.
- Ajusta os testes de regressao que ainda exigiam indevidamente a abertura em dimensoes grandes.
- Esta e uma compilacao de teste; a avaliacao visual no Windows real continua necessaria.

## 5.13.10.2 TESTE — consistencia de interface e registro
- Corrige o registro excessivamente baixo e reserva espaco real para diagnosticos em qualquer altura util.
- Usa largura e altura preferidas limitadas em telas 4K/8K; nao dimensiona fontes ou botoes de acordo com a resolucao nominal.
- Quebra os nomes das abas em duas linhas quando necessario, em vez de truncar o nome das funcionalidades.
- Ajusta o resumo da aba Aplicativos ao espaco, preservando a informacao completa em uma dica acessivel.
- Botoes de acoes de aplicativos permitem duas linhas para nao ocultar seus titulos.
- Testes com 21 resolucoes e simulacoes geometricas para 100%, 125%, 150% e 200% de DPI.
- A auditoria historica permanece informativa e somente leitura. GOG.com, McAfee, Microvirt e MPC-HC nao sao candidatos a exclusao automatica.
- Continuam necessarios testes visuais no equipamento real, principalmente 800x600 e DPI alto.

## 5.13.10.1 TESTE — resolucoes, DPI e multiplos monitores
- Matriz de verificacao automatizada com as 21 resolucoes fornecidas: 16:9, ultrawide, 4:3 e 16:10, incluindo 800x600 e 8K.
- A janela inicial permanece inteiramente dentro da area util de cada monitor; o tamanho minimo deixa de impor 970x690 em monitores maiores.
- Reposicionamento e reducao da janela ao trocar para monitores menores, alterar resolucao ou mudar DPI, sem armazenar preferencias no disco.
- Cabecalho e registro cedem espaco para a area de trabalho em alturas reduzidas.
- Abas com largura adaptavel, altura escalada por DPI e dicas mostrando o titulo completo quando o texto precisa ser encurtado.
- Cartoes de ferramentas ajustam sua largura ao espaco disponivel, sem criar barra horizontal nativa.
- Testes de geometria, navegacao e foco nao executam manutencao do sistema; as resolucoes altas nao geram capturas gigantes.
- A aprovacao final depende dos testes de CI no Windows e de validacao visual em equipamentos reais, incluindo 125%, 150%, 200% e mais de um monitor.

## 5.13.3 TESTE — precisão no inventário de resíduos
- Filtra namespaces compartilhados do Registro e componentes do Windows que estavam aparecendo como resíduos.
- A auditoria geral só apresenta candidatos com evidências de dados de aplicativo cruzadas com outra fonte independente; um único caminho, atalho ou vista do Registro não basta.
- Mantém candidatos como não confirmados, sem qualquer exclusão automática, e preserva a varredura histórica específica.
- Testes com casos extraídos do diagnóstico v5.13.2, incluindo Netease, dotnet, Classes, Windows Defender e K-Lite.
- Esta versão ainda exige validação da CI e testes no Windows real antes de aprovação.

## 5.12.3 — Instalador e distribuição
- Preparação de instalador Inno Setup para Windows x64 com consentimento UAC.
- Aplicativo .NET autocontido: não exige SDK no computador do usuário final.
- Atalhos no Menu Iniciar e opção de atalho na área de trabalho.
- Integração do ícone GF e preparação para futura assinatura digital.
- Sem criação automática de exceções no antivírus ou regras de firewall.

## 5.12.2 — Reparo de rede e interface
- O botão Reparar rede depende dos resultados do diagnóstico e solicita confirmação antes de agir.
- A redefinição completa da rede permanece separada do reparo específico.
- Ajuste da largura do botão Atualizar discos.

## 5.12.1 — Diagnóstico de rede
- Diagnóstico consultivo de adaptador, gateway, DNS e conectividade TCP/443.
- Resultados mostrados na aba Reparo e rede e registrados no log.
- Cartão Criar ponto de restauração com confirmação.
- Remoção de atalhos redundantes na interface.

## 5.12 — Reparação do Windows
- Fluxo de reparo com DISM RestoreHealth e depois SFC Scannow, com confirmação e UAC.
- Registro dos resultados e códigos de saída das verificações.

## Histórico anterior
As versões 5.4 a 5.11 introduziram progressivamente a interface escura, listas de aplicativos, ferramentas de limpeza, diagnóstico e gerenciamento do sistema. A documentação atual e o código-fonte representam o estado funcional da versão 5.12.3.

> **Observação:** estas são notas de desenvolvimento; compilações e testes de compatibilidade devem ser feitos no Windows antes de publicar binários para usuários finais.

