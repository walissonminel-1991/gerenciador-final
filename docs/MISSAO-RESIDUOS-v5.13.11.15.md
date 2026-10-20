# Missão de resíduos — v5.13.11.15 TESTE

Base auditada: `7bb263c9b3145ed9e7c1d9b465b23338c08d821c`, PR #40 Draft, branch `piloto-diagnostico-github-v5.13.11.2`. Três workflows de base confirmados com sucesso. Sem alteração da main, envio remoto ou drivers.

## M1 — auditoria e contenção

Falhas confirmadas no código anterior:

1. **Crítica — ampliação do consentimento:** `UninstallResiduals.Clean` e `CleanCandidateFiles` reenumeravam e apagavam a lista nova, inclusive nomes autorizados que não existiam na prévia. Não comparavam conteúdo/identidade dos arquivos aceitos.
2. **Crítica — conteúdo pessoal:** `AdvancedUninstallCleaner.RestrictToAccepted` limitava somente nomes de pastas/chaves. Conteúdo novo/alterado dentro delas podia ser apagado; a ausência no ARP e a receita não provam exclusividade de backups, perfis ou playlists.
3. **Alta — concorrência:** checagem de atributos seguida de `File.Delete`/exclusão de Registro por caminho não mantinha identidade estável. Hard links não eram conferidos; contagem e segunda enumeração podiam divergir.
4. **Alta — contexto incompleto:** validação de ausência usava inventário Win32; jogos/MSIX/portáteis e desinstaladores auxiliares exigem proteção adicional. Não é prova de término do fabricante.
5. **Média — descoberta:** busca geral de primeiro nível perde pastas de produto sob fabricante. Classificação repetia normalização do inventário para cada grupo; erro de leitura de proteções não era sempre propagado como parcial.
6. **Média — apresentação:** deduplicação por nome descartava locais específicos; categoria inferida de texto e primeira evidência podia esconder motivos dos demais itens.

Contenção: removidas as implementações de exclusão dos dois limpadores; os quatro pontos de entrada internos retornam bloqueio, mesmo com allowlist. Fluxos de interface são revisão sem confirmação de apagar. Desinstalador oficial continua com confirmação própria e pode alterar dados por sua própria política. Limpeza de temporários do sistema não faz parte desta mudança.

Testes de preservação substituem expectativas antigas de exclusão; os arquivos sintéticos e a chave de teste HKCU devem permanecer até o teardown dos testes. Sete testes Python de política e três testes Node do protótipo passaram localmente. Regressões C#/Windows dependem do CI desta revisão.

## Pesquisa técnica limitada

Sem copiar código externo. Referências usadas para delimitar a implementação:

- Microsoft: https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations — flags/semântica especial ao abrir links.
- Microsoft: https://learn.microsoft.com/en-us/windows/win32/fileio/hard-links-and-junctions — mais de um nome pode referenciar o mesmo arquivo.
- BCU, manual: https://github.com/BCUninstaller/Bulk-Crap-Uninstaller/blob/master/doc/BCU_manual.html — revisão de vestígios e cautela; confiança não concede permissão no GF.

## Próximos marcos

M2: descoberta direcionada de segundo nível, orçamento/cancelamento e testes. M3: classificação explícita, contexto parcial e evidências por item. M4: prévia/revalidação sem execução destrutiva. M5: regressões Windows, versão, artefato e SHA-256 no mesmo commit aprovado. Não há acesso verificável à franquia Work.

## M2 — descoberta ampliada

M1 aprovado nos três workflows do commit `0f3701e7a9005592c2ec7f7a60274f473d744107`: política 39502667282, Windows 39502666891, pacote 39502667013.

Busca geral agora examina até dois níveis em pastas e SOFTWARE (produto sob fabricante desconhecido), incluindo HKCU32/64 e HKLM32/64. Run e serviços Win32 fornecem pistas de executável local; não executa comandos, não expande variáveis/shell e não considera drivers. Catálogo histórico adiciona perfis VLC/foobar2000, preservando preferências e playlists. Atalhos são enumerados sem execução/resolução de destino.

`ResidualScanBudget`: até 30.000 entradas compartilhadas entre fontes, 2.500 por raiz e 128 filhos por contêiner, 14 segundos cooperativos e token de cancelamento. Não pode interromper uma chamada nativa que já bloqueou. Diretórios redirecionados/ancestrais com reparse e caminhos relativos, UNC, dispositivos, ADS/traversal são recusados. Resultado parcial explicita falta de cobertura; o limite nunca autoriza limpeza.

Validação local: build TestesResiduos sem avisos/erros; nove verificações `--missao` aprovadas no Linux com dados temporários (cancelamento, timeout, limites, produto sob fabricante, playlist, raiz ausente e link/ancestral). Casos adicionais de comandos locais e Registro sintético são Windows-only e aguardam o CI deste marco. Tarefas agendadas, resolução de atalhos/integrações COM e histórico transacional persistente continuam pendentes; não foram declarados implementados.

## M3 — classificação e correlação

Decisão tipada por achado: revisar, preservar dados, preservar compartilhado ou estado desconhecido; nem categoria validada concede capacidade de exclusão. Perfis em AppData, preferências do Registro e dados multimídia ficam preservados. Inventário/proteções parciais convertem todos os relatórios unidos em estado desconhecido. O fluxo pós-desinstalador e a revisão avançada passam a usar a fotografia unificada Win32/MSIX/jogos.

Identificadores MSI, ID de inventário e sobreposição delimitada de caminhos ainda usados bloqueiam falsa confirmação de ausência. Relatórios agora unem locais e motivos, sem descartar evidências por nome; alvo ligado a produtos diferentes passa a compartilhado. Interface mostra decisão e justificativa de cada item. Normalização do inventário realizada uma vez por classificação.

Regressão encontrada pelo CI M2: o validador histórico rejeitava a pasta de três letras `vlc`, fazendo o catálogo inteiro falhar fechado. Corrigida validação mínima para três letras e adicionado teste do carregamento das cinco regras, inclusive no subconjunto local. Não houve desbloqueio de exclusão.

Validação deste marco: aplicação compilada localmente para Windows sem erros (aviso SYSLIB0057 preexistente em VerifiedUpdaters); build dos testes sem avisos/erros. Vinte testes locais específicos aprovados, incluindo 10.000 evidências/1.000 instalações com saída limitada a 100 grupos, conflitos, inventário parcial e MSI/instalação paralela. Sete testes Python aprovados. Repetir suite Windows nesta revisão para encerrar a regressão do catálogo.
