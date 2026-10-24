# Revisao de vulnerabilidades — v5.13.11.5 TESTE

Data: 20/10/2026. Base: 4ab088312e8da7823961b4df9f53771d8a275dca, branch do PR #40. Revisao defensiva de codigo; nao e certificacao de seguranca nem prova de ausencia de vulnerabilidades.

## Achados e correcoes

1. **Adulteracao entre download e execucao.** O instalador Win32 era validado no download e mais tarde executado pelo caminho, apos eventual interacao/preflight. Agora o download guarda um comprovante somente em memoria (receita completa e SHA-256 do arquivo efetivamente validado, inclusive MSI extraido). Antes de iniciar, exige esse comprovante de uso unico, revalida SHA-256 e Authenticode e mantem o arquivo aberto sem compartilhar escrita/exclusao ate o processo iniciado terminar. O pacote Terminal tambem permanece aberto durante a entrega ao Windows.
2. **Receita parcialmente confiada nos pontos de execucao.** Os metodos de download/lancamento passam a exigir correspondencia integral com uma receita local aprovada: ID, URL, hash fixado quando existente, argumentos, editor, versao e modo. Defesa em profundidade: o catalogo remoto ja nao fornecia comandos executaveis, portanto isto nao comprova uma via previa de execucao remota por catalogo.
3. **Identidade do cliente no pipe administrativo.** O servidor conferia somente o token enviado no texto. Agora tambem confere o PID do cliente pelo kernel contra o processo que iniciou, antes de ler mensagens; autenticacao sem resposta termina apos cinco segundos. Reduz falsificacao de status e travamento local por conexao indevida. O pipe transporta resultados/logs; esta revisao nao demonstra uma via de comandos arbitrarios pelo pipe.
4. **Temporario previsivel da privacidade.** A escrita usava um nome fixo com truncamento. Agora usa GUID, CreateNew, acesso exclusivo e flush antes da troca atomica. Pastas redirecionadas por links/juncoes sao recusadas antes da gravacao. Leituras usam o tamanho do arquivo aberto com compartilhamento somente de leitura, em vez de depender apenas de uma consulta de tamanho anterior.

5. **Busca de DLLs nativas do proprio GF.** Os imports declarados usam DLLs do Windows, mas nao havia politica explicita de busca. O assembly agora exige System32 com DefaultDllImportSearchPaths, reduzindo a possibilidade de carregar uma DLL homonima da pasta portatil. Isto cobre os P/Invokes do GF, nao o carregamento interno dos instaladores externos.

## Regressões

- Receitas locais validas continuam reconhecidas; mudancas de argumentos, URL, hash, editor, ID e modo sao rejeitadas antes de rede/escrita.
- Instalador sem comprovante da sessao e bloqueado antes de UAC/execucao.
- Fixture modificada depois de calcular o hash e rejeitada.
- No Windows, escrita/exclusao de arquivo aberto para validacao sao recusadas.
- DLL falsa inofensiva ao lado do executavel de teste: a biblioteca efetivamente carregada precisa vir da pasta de sistema.
- Pipe real local: PID correto aceito, PID diferente e pipe desconectado rejeitados, sem elevacao.
- Temporario previsivel preexistente permanece intacto; temporarios exclusivos sao limpos.
- Links de arquivo/pasta sao testados quando o runner permite cria-los; indisponibilidade e registrada como SKIP, nao aprovada.
- Suites consolidadas, compilacao e pacote Windows devem passar nesta revisao; resultados finais ficam no PR #40.

## Limites e proximas verificacoes

A protecao de compartilhamento se aplica ao arquivo aberto; nao elimina todas as corridas envolvendo renomeacao de ancestrais, todos os casos de DLL sideloading ou comprometimento do administrador/kernel. Instaladores que terminam um processo inicial e continuam em outro exigem testes reais; a trava atual dura enquanto o processo iniciado e aguardado. A compatibilidade da trava com cada instalador e com AddPackageAsync deve ser conferida nos PCs, sem executar instalacoes reais na CI desta revisao.

A verificacao de PID foi exercitada sem UAC no runner. O fluxo elevado e elevacao com credenciais de outro administrador ainda precisam de teste real. O timeout de autenticacao nao e um timeout global para tarefas longas de manutencao.

As gravacoes de privacidade continuam precisando de permissoes do Windows. Recusa, pastas redirecionadas ou falhas de disco devem desativar/falhar com aviso; nao ha gravacao alternativa silenciosa.

Permanecem a avaliacao AVG, testes nos dois PCs, limites WMI e ausencia de assinatura de distribuicao. A main continua na candidata v5.13.11.0 ate integracao. Esta revisao nao implanta envio automatico de diagnostico nem manutencao por IA.

## Referencias tecnicas

- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid
- https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0

- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.defaultdllimportsearchpathsattribute?view=net-10.0
