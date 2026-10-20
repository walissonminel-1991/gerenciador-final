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
