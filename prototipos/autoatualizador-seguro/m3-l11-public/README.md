# L11 — Contrato público somente leitura (fixture TUF assinada)

Este ensaio usa a cadeia Stage11/Stage3 de **chaves exclusivamente de teste**, com objetos .fixture.txt inertes. Não implementa autoatualizador, downloader, instalação, elevação, execução de comandos, cadastro ou chamada de rede. O host updates.gf.invalid é deliberadamente inexistente (.invalid).

Os 17 cenários verificam que um contrato declarativo não transforma JSON recebido em poderes de shell, escrita fora de escopo, atualização de outros apps, arquitetura/canal incompatível ou rollback sob piso local preservado. A fixture TUF é autenticada ANTES de congelar o vínculo Stage3. A camada L11 não é um verificador TUF independente nem deve ser integrada ao binário GF.

O teste de restauração integral mostra explicitamente o limite: após recuperar um piso antigo juntamente com o verificador, a release anteriormente recusada volta a parecer nova. O caso **passa** somente quando reproduz a limitação, não quando demonstra proteção.

Ainda NÃO provado: servidor real, antirollback G1 após restauração total, genuinidade do instalador inicial, assinaturas operacionais, controle do binário malicioso assinado, reparse-point/TOCTOU reais, ACL/Program Files, NTFS/power loss, timer G8, G5/G6, revisão G2 e D6-02. Todos os gates permanecem bloqueados.

A CI Windows usa SDK e dependências TUF de fixture fixados, faz download somente da fonte pinada de biblioteca e gera metadados assinados **de teste** localmente; não executa o aplicativo GF e checa isolamento pelo item Compile MSBuild. Não copiar este código para o produto.
