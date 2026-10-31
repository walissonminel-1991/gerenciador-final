# GF D1.2 — continuidade TUF entre processos Windows (somente laboratório)

Esta pesquisa usa a **mesma fonte C# D0** compilada pelo projeto D1.1 e o verificador Python-TUF 7.0.1 com chaves privadas públicas de teste do Stage11. Cada tentativa executa **um novo processo Python**, que consulta um processo separado C# D0, sem rede. O estado de confiança TUF é mantido em um diretório temporário.

O teste observa metadados v1 aceitos, depois v2 aceitos por processo independente, e rejeição do replay v1 quando o diretório confiável permanece íntegro. A associação da Release simulada exige tag exata, uma única entrada com nome, tamanho e URL canônica do asset; os bytes do manifesto são vinculados pelo TUF e comparados ao payload inerte. A URL é tratada como campo de identificação **não como fonte de confiança ou autorização de download**.

Dezoito contraprovas têm cópias isoladas do estado v2. Há ainda um **contraexemplo intencional de segurança**: reverter TODO o diretório confiável para a cópia da v1 permite aceitar novamente metadados v1. Esse sucesso do ataque significa explicitamente que H1 não foi resolvido; não é sucesso de recuperação, instalabilidade ou segurança contra adversário same-user.

Sem simular falta de energia, crash de processo, proteção antirrollback externa, credenciais privadas de produção, download, instalação ou release. READY=false, G1–G8 BLOCKED 0/8, zero autorizações de recuperação. As CI anteriores D0/D1/D1.1 não são substituídas.
