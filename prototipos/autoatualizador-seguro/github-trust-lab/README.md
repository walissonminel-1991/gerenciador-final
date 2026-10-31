# D1.1 — descoberta D0 + histórico TUF persistente em laboratório

Este laboratório compila a fonte original do avaliador C# D0, sem modificar o produto, e fornece um JSON de Release fictícia. O resultado D0 é **apenas consultivo**. A validação de manifesto utiliza o contrato D1; a cadeia criptográfica e a persistência das versões de papéis TUF são verificadas pelo cliente python-tuf 7.0.1 em diretório descartável no Windows CI.

Duas instâncias de Updater compartilham a mesma pasta de metadados: primeira versão 1, atualização de metadados para 2, raiz 1 mantida. O verificador compara o manifesto autenticado com a tag efetivamente devolvida pelo parser C# do D0 e com tamanho e SHA-256 de bytes inertes já em memória. O fetcher nunca usa rede e rejeita destinos fora do host de laboratório.

Negativos: replay da versão antiga, mix-and-match de Snapshot e Targets, manifesto obsoleto, divergência D0/D1, draft, chaves JSON duplicadas, nenhuma versão nova, Timestamp expirado/ausente, corrupção de Targets, arquitetura/repositório incorretos e downgrade. Falha deve bloquear.

**Limites:** a persistência testada só vale enquanto os arquivos do diretório permanecem íntegros; um adversário com permissão de modificá-los pode reverter o conjunto completo. Isso **não comprova H1**, resistência ao rollback same-user, cortes de energia, durabilidade transacional, execução em processo separado nem READY. Chaves de assinatura são seeds públicas de teste; nenhuma autoridade de produção, download real ou instalação é autorizada. Nenhum arquivo de produto foi alterado.
