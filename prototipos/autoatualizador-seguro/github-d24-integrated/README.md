# GERENCIADOR FINAL — D2.4: integração D0 → TUF de teste → .NET D2.3

Este é um fluxo **orquestrado em laboratório Windows descartável**, não um autoatualizador operacional.

O controlador `d24_chain_lab.py` reutiliza diretamente `verify_d0_tuf` da D2, que executa o mesmo parser **C# D0** e verifica com python-tuf os metadados assinados por **chaves públicas de TESTE**. Apenas depois de um resultado positivo o controlador envia ao executável C# o caminho do manifesto, seu SHA-256 e a versão instalada/candidata. A ponte C# lê novamente o arquivo, confere os campos contra um contrato estrito de teste, revalida SHA-256 de bytes inertes e usa **exatamente o mesmo transportador HttpClient .NET da D2.3**, por inclusão de fonte única em `GF.D24.InertChainLab.csproj`. Não há um segundo downloader.

O transporte real fica restrito a 127.0.0.1. A URL GitHub Releases é uma **identidade lógica** de arquivo fictício; não é acessada. Tudo se limita a bytes de texto, diretórios descartáveis e raiz TUF de teste, sem instalador, binário oficial ou publicação.

## Limites e contraexemplo obrigatório

**A passagem entre Python e C# NÃO é uma autoridade segura:** um atacante do mesmo usuário pode chamar diretamente o C# com o manifesto de teste e seu hash conhecidos, sem executar o TUF. O laboratório **demonstra explicitamente essa aceitação**. Portanto, mesmo com CI verde, `secure_cross_process_authority_proven=false`. Este é um resultado para orientar a arquitetura futura, não um defeito autorizado para o produto.

O componente C# não verifica assinaturas TUF por conta própria; a validação ocorre no controlador Python em ambiente confiável **apenas para fins de teste**. Chaves de teste derivadas de seeds públicas não concedem autenticidade de software. H1 rollback integral, H2, H3, NTFS/TOCTOU same-user, Crash/Power-loss, TLS real, redirects CDN e READY continuam bloqueados.

O projeto principal, main, PR #65 Draft, BETA .66 e Cloudflare não podem ser alterados. Sem releases, merge, deploy, executáveis reais, recuperação automática ou modificações de dados pessoais.
