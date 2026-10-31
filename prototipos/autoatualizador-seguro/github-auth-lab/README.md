# GERENCIADOR FINAL — D1: manifesto de Release autenticado por TUF (somente laboratório)

**Objetivo:** ligar os metadados consultivos de D0 a um manifesto autenticado que fixa o nome do único pacote portátil, repositório, versão/tag, canal estável, arquitetura, tamanho e SHA-256. Nenhum download real é feito.

A implementação reutiliza a **raiz pública Ed25519 de teste** de `fixtures/bootstrap.test-only.json`, as seeds PÚBLICAS DE TESTE de `generate_fixtures.py` e os métodos Python-TUF 7.0.1 já utilizados na Fase 1. Os signatários de teste estão no repositório e **não são credenciais para publicação**, nem protegidos de adversários. Não reutilizar para uso operacional.

Contrato assinado experimental: `GF-RELEASE-MANIFEST.json` como único alvo de TUF Targets, vinculado por tamanho/SHA-256 em Targets, Snapshot e Timestamp, com expiração verificada. O próprio JSON do manifesto (também autenticado) exige correspondência estrita com a tag candidata descoberta em D0, impede downgrade, restringe `win-x64` e o nome exato de arquivo, e rejeita bytes hipotéticos divergentes. Os bytes de payload são **texto inerte, não ZIP, EXE, DLL ou instalador**. As saídas preservam `can_install=false` e `operational_ready=false`.

Limitações deliberadas: o verificador TUF desta prova valida uma cadeia de laboratório de uma tentativa; não demonstra a persistência/antirrollback TUF entre tentativas, a proteção das chaves privadas de produção, o momento de publicação de uma Release nem a identidade dos bytes realmente executados. Não oferece API operacional, atualização automática ou recuperação.

Testes: caso positivo sintético mais negativos para falsificação de Targets/Timestamp/Snapshot, raiz incompatível, metadados expirados, versão/tag divergente, caminho hostil, tamanho/hash do payload, flags test_only, JSON duplicado e assinatura de signatário não autorizado. Resultado auditável na CI `gf-github-auth-d1-lab.yml`.

Próximo passo: **D1.1** integrar verificação de confiança persistente e ligação à descoberta D0 sem elevar autoridade; depois D2 download + preparação em espaço isolado, somente com pacote autenticado e testes; D3 troca segura preservando B+A e Opção B. Nenhuma Release oficial foi publicada para teste real.
