# GF D2.3 — laboratorio HttpClient .NET em loopback

Objetivo: validar transporte HTTP/1.1 real com HttpClient no Windows descartavel. O servidor TcpListener aceita conexoes somente em 127.0.0.1. O cliente SocketsHttpHandler bloqueia proxy, desativa redirects/cookies/descompressao e fixa ConnectCallback no IP IPv4 127.0.0.1 e na porta efemera do proprio caso.

Os bytes de teste sao texto inerte, jamais um ZIP/EXE real. O hash esperado e calculado de uma constante de laboratorio dentro do codigo C#; **isto nao e um manifesto TUF autenticamente assinado**. A D2.3 mede especificamente HttpClient, timeout, cancelamento, limites, status, fragmentacao, controle de destino e verificacao SHA-256 apos gravacao. Grava exclusivamente em RUNNER_TEMP. Nenhuma Release externa e consultada.

A fonte C# e .cs.txt para impedir que o projeto principal do GF a inclua; apenas o csproj de laboratorio a compila. Nao ha publicacao, arquivo executavel oferecido a usuarios, deploy nem instalacao.

Limites: autenticação de producao e integracao D0/D1 ao componente .NET ainda pendentes. GitHub Release/CDN, TLS real e seus redirects nao foram testados. Tampouco H1, proteção universal same-user NTFS/TOCTOU, crash/power loss, OLD/NEW ou READY. Fase 1 ABERTA, G1–G8 BLOCKED 0/8, zero recuperacoes automaticas, HOLD somente contencao.
