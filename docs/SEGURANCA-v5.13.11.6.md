# Reforco de seguranca — v5.13.11.6 TESTE

## Mudancas
- InstallerFileGuard abre os ancestrais da raiz ao destino com OPEN_REPARSE_POINT e BACKUP_SEMANTICS; valida atributos pelo handle, mantendo-os abertos sem compartilhar exclusao/renomeacao. No caminho de leitura/execucao tambem nao compartilha escrita; durante criacao/gravacao, a pasta final compartilha escrita para permitir criar e substituir arquivos filhos. O arquivo final tambem e aberto sem seguir reparse point. Falhas de acesso interrompem a operacao.
- Caminhos de pacotes limitados a unidades locais fixas, sem UNC, namespaces de dispositivo, ADS ou caminhos relativos. Perfis/TEMP redirecionados podem exigir um local convencional; nao ha fallback inseguro.
- Downloads Win32/Terminal prendem o pai antes de criar a pasta, e prendem a pasta durante download/verificacao. Escrita atomica de privacidade prende os ancestrais durante gravacao e substituicao.
- Instaladores Win32 retidos em memoria pela sessao (maximo 32), inclusive apos o processo inicial terminar ou UAC ser cancelado. Seus arquivos temporarios nao sao removidos pelo fluxo de atualizacao enquanto protegidos. Sem exclusao recursiva automatica posterior; limpeza normal de temporarios pode remove-los quando nao estiverem em uso.
- A pasta do pacote precisa conter apenas os nomes esperados; DLL/pasta inesperada bloqueia antes do UAC. WorkingDirectory passa a ser a pasta do sistema.
- Auditoria NuGet de dependencias diretas/transitivas habilitada para todos os projetos; NU1900–NU1905 bloqueiam build/restore. Dependabot configurado semanalmente para NuGet raiz e Actions; ativacao depende da integracao na branch padrao. Nenhuma CVE especifica e declarada corrigida.
- Dois workflows atuais usam checkout v7.0.1, setup-dotnet v6.0.0 e upload-artifact v7.0.2 (Node 24), fixadas por SHA, com credenciais de checkout nao persistidas. Historicos permanecem como evidencia; Dependabot pode propor atualizacoes dos pins.
- NuGet usa fonte explicita oficial tanto para pacotes quanto para auditoria, sem herdar feeds inesperados do ambiente.
- Confirmacao de compartilhamento publico alerta para senhas/tokens/dados pessoais em texto e capturas adicionados pelo usuario.

## Verificacao
A primeira bateria encontrou incompatibilidade entre a trava de escrita da pasta e a gravacao atomica. Corrigida separando o compartilhamento necessario para escrever arquivos filhos da protecao contra renomeacao da pasta. Regressao especifica cobre criacao, move e exclusao de fixture com a pasta presa.
Regressoes Windows: impedir escrita/exclusao do arquivo e renomeacao de ancestral; rejeitar symlink de arquivo/pasta, UNC/dispositivo/ADS/relativo; manter trava apos transferencia para sessao e GC, inclusive contra escrita por outro processo de teste; liberar ancestrais por DisposeAsync; recusar DLL/pasta inesperada junto ao instalador. Testes existentes de assinatura/receita/pipe/privacidade continuam necessarios. CI desta revisao deve ser consultado no PR #40; testes anteriores nao comprovam este commit.

## Limites
A retencao acaba ao encerrar o GF: manter o programa aberto ate finalizar todos os instaladores. Nao e rastreamento completo de subprocessos nem de servicos Windows Installer. Nao protege arquivos extraidos internamente por instaladores, DLLs carregadas depois da verificacao da pasta, outros caminhos controlados pelo fabricante, administrador/kernel comprometido ou injecao no proprio processo. A verificacao de arquivos vizinhos e pontual, nao uma ACL imutavel.
A validacao de handles reduz trocas de nomes/ancestrais; nao equivale a sandbox contra qualquer alteracao de metadados pelo proprietario ou privilegios elevados. Criacao inicial da pasta de privacidade ainda usa verificacoes de nomes; escrita final exige handles.
Auditoria NuGet nao audita automaticamente o runtime embutido no pacote portatil nem componentes do sistema. .NET 10.x vem do SDK atualizado no build; releases anteriores precisam ser recompilados e testados.
UAC, instaladores reais, subprocessos e AVG exigem validacao em PC real. Esta revisao permanece candidata, sem release estavel ou merge automatico.

Referencias tecnicas: Microsoft CreateFileW e GetFileInformationByHandle; nao utilizar nomes de caminhos como prova exclusiva da identidade de arquivos.
