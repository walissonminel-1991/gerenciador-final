# GERENCIADOR FINAL — D2.2: transporte HTTP real, somente loopback

Esta prova utiliza HTTP/1.1 real da biblioteca padrão Python (http.client), em uma conexão TCP exclusivamente com o endereço numérico 127.0.0.1. Um servidor ThreadingHTTPServer escuta somente nesse endereço e responde com bytes INERTES controlados pela CI Windows descartável.

O fluxo preserva as consultas D0 por parser C# e o manifesto D1 autenticado por uma raiz TUF **de laboratório, com chaves privadas derivadas de sementes públicas**, antes da conexão local. A URL simulada da Release GitHub é apenas um identificador de asset esperado: o cliente não acessa esse host e não segue redirects. Um caminho local fixo de teste atende o objeto e o gravador D2 verifica tamanho e hash após gravar e renomear um arquivo de texto inerte sob RUNNER_TEMP.

Respostas adversariais incluem status 301/302/307/308, 206/404/429/500, Content-Length ausente, duplicado, incorreto ou fora do orçamento, tipos inesperados, transfer-encoding e content-encoding, truncamento real de socket, timeout, bytes adulterados e corrupção pós-gravação. Cada tentativa inicia uma conexão TCP nova ao servidor de loopback, mas nenhuma requisição é feita à internet.

**Limites:** a prova não testa TLS, GitHub, CDNs, redirects legítimos de GitHub Releases, autenticação de produção, HTTP operacional .NET, interferência de mesmo usuário no NTFS, crash ou fsync após queda de energia, nem execução dos bytes verificados. O transportador recusa TODO redirect; por isso ainda não serve para obter um asset real de GitHub Releases. O H1 de rollback integral permanece sem solução. READY=false e G1–G8 BLOCKED 0/8. HOLD é contenção, não recovery. Não há instalação, mudança no GF nem download de executável.
