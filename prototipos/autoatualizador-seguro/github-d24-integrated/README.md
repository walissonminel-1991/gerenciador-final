# GERENCIADOR FINAL — D2.4.2: coordenador C# controla o laboratório inerte

Escopo: **Windows GitHub Actions descartável**, raiz TUF de TESTE com seeds públicas; **não é um autoatualizador operacional**.

## Única entrada para preparar bytes

O executável `GF.D24.InertChainLab.exe` aceita `--d242 <diretório-de-fixtures>`. A antiga entrada `--d24 <manifesto> <sha> <tag> <versão>` foi desativada e **não permite preparo**. O coordenador C# cria um identificador de sessão, inicia um processo Python como filho, e utiliza código Python de verificação incorporado como `EmbeddedResource` ao próprio projeto .NET (`d242_tuf_child.py`), fornecido com `python -c`.

O validador filho reaproveita `verify_d0_tuf` e o parser **C# original D0** do laboratório, além do python-tuf. Ele lê o bootstrap TUF em um snapshot de bytes limitado, verifica o SHA-256 pinado da raiz pública e fornece **esses mesmos bytes** ao verificador, sem nova abertura do arquivo de bootstrap. Somente após autenticar os metadados TUF de TESTE e comparar a Release fictícia com o manifesto inerte o filho emite um recibo estruturado para sua sessão.

O coordenador exige sucesso e saída única do processo filho, schema exato, sessão, tag, digest de manifesto refeito sobre os bytes relidos, versão instalada e flags de segurança explícitas. Revalida os campos do manifesto no seu snapshot em memória e então chama **o mesmo `Stage` .NET D2.3**, sem criar um segundo transportador. A rede é exclusivamente `127.0.0.1`; o arquivo é **texto inerte**, não ZIP/EXE. A preparação não autoriza instalação.

## Matriz adversarial

`d24_chain_lab.py` exige: 1 sessão positiva, **15 negativas**, inclusive chamada direta antiga, replay da fixture após trust store criado, manifesto com whitespace e digest novo sem correspondente assinatura TUF, Release errada/draft/assets adulterados, Targets modificado, Timestamp expirado/ausente, repositório e arquitetura assinados incorretos, chave duplicada e interpreter Python indisponível. `--d242-selftest` não prepara bytes e rejeita 7 recibos sintéticos inválidos (sessão/hash discordantes, replay por outra sessão, campos faltantes/extra, tipo e chave duplicada).

O C# não aceita um recibo ou hash arbitrário como argumento de autorização. Contudo, o resultado de sucesso no laboratório **não comprova proteção contra um processo malicioso executado pela mesma conta Windows**, por exemplo substituindo o interpretador Python, bibliotecas, arquivos ou o próprio binário do coordenador. A sessão aleatória não é uma chave de assinatura, e o recibo não constitui IPC autenticado contra esse adversário. As chaves TUF usadas são publicamente deriváveis; não são de produção.

## Limites preservados

O trust store de cada fixture ainda é um bootstrap isolado: D2.4.3 tratará histórico persistente, rollback e controles NTFS. H1/H2/H3 e TOCTOU same-user não foram provados. O protocolo real de GitHub, TLS, CDN, redirects legítimos e dados executáveis não são utilizados. A D2.4.1 mantém a política conservadora de não excluir por nome arquivos de identidade desconhecida e seu deadline cooperativo; nenhuma recuperação automática é feita.

**Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, HOLD como contenção, zero recuperações automáticas.** Não tocar em main, PR #65 Draft, BETA .66, Cloudflare ou GF distribuído; sem instalação, release, merge ou deploy.
