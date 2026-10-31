# CHAT ALTO — GERENCIADOR FINAL — D1: contrato TUF de manifesto GitHub

**Data de referência:** 30/10/2026, com CI final concluída em 31/10/2026 UTC (noite de 30/10 em Fortaleza). **Natureza:** laboratório de criptografia com dados inertes, sem promoção operacional.

## 1. Estado verificado e integridade

- Repo: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- Base documental D0: `3db9c2769206d76548e098d5a9f29c48120450ad`.
- **SHA técnico D1 aprovado:** `9a911e562d065dfc6a5ee81b1c3d5b3292022511`.
- [CI Windows 41148601868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148601868), tentativa 1, job `124379474271`: `completed/success`, HEAD idêntico ao SHA técnico.
- Regressão D0: [CI 41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551) SUCCESS, 25 testes offline.
- Regressão histórica transacional: [CI 41145899676](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41145899676) SUCCESS, SHA `64fd93d5b49024f42f783906a9fcfd33aebf8110`, após a última revisão de estado.
- Consulta ao GitHub em 30/10: **0 Releases publicadas** para o repositório; ausência de uma atualização oficial real para testar.

## 2. Trabalho realizado

Novo laboratório separado `prototipos/autoatualizador-seguro/github-auth-lab/`:

- `d1_release_manifest_lab.py`: constrói um **manifesto fictício** `GF-RELEASE-MANIFEST.json`, com repositório fixo, canal stable, tag/versão, arquitetura `win-x64`, nome único `GerenciadorFinal-portable-win-x64.zip`, comprimento e SHA-256 do payload inerte, e flags obrigatórias `test_only=true` e `operational_installable=false`. Recusa campos extras/ausentes e chaves JSON duplicadas; valida versão maior que a instalada e tag igual à candidata descoberta.
- Reutiliza o verificador **python-tuf 7.0.1**, metadados Ed25519 dos papéis Root/Targets/Snapshot/Timestamp e o bootstrap fixado com SHA-256 `a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e` do laboratório anterior. A cadeia autenticada vincula os bytes do manifesto por tamanhos e hashes; o manifesto vincula os bytes inertes esperados.
- Os signatários são **chaves privadas de TESTE derivadas de seeds publicadas no repositório**, não protegidas; **qualquer pessoa pode assiná-las**. São úteis exclusivamente para demonstrar o comportamento da verificação. Não há raiz, segredo, assinatura ou autorização de produção.
- `.github/workflows/gf-github-auth-d1-lab.yml`: CI Windows descartável, com dependências fixadas, sem download de executável, instalador, pacote publicado, mudanças no GF ou credenciais persistentes.
- `github-auth-lab/README.md`: escopo e limites.

## 3. Testes e falhas

A primeira [CI 41148546083](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148546083) falhou corretamente na asserção da quantidade: eram **33** contraprovas codificadas, mas o teste esperava 32. Não foi necessária alteração dos predicados de segurança. Um commit atualizou o limite do runner e do código; a CI seguinte foi **SUCCESS**.

O log da CI válida contém `GF_GITHUB_AUTH_D1_LAB` com:
- `positive_inert_contract=true`;
- `adversarial_rejected=33`, `unique_adversarial_cases=33`;
- `status=passed`, `scope=PUBLIC_TEST_TUF_CHAIN_AND_INERT_PAYLOAD_ONLY`;
- `metadata_source=PINNED_LOCAL_TEST_ROOT`, `github_live_requests=0`;
- `product_changes=false`, `downloaded_real_executable=false`, `installer_launched=false`;
- `can_install=false`, `operational_ready=false`, `automatic_recovery_authorized=false`.

As contraprovas abrangem: adulteração do manifesto, raiz incorreta, assinatura/bytes de Targets e Timestamp, Snapshot inconsistente, papel ausente, divergência de tag de GitHub, downgrade ou mesma versão, RID incorreto, pacote alterado/truncado, campos do manifesto alterados **mas assinados com a chave de teste autorizada** (rejeição por contrato), JSON duplicado, Timestamp expirado assinado e signatário não autorizado.

## 4. O que a CI NÃO prova

**Esta não é uma atualização funcional do GF.** Não houve publicação de Release, download de pacote real, instalação, transação OLD/NEW ou validação física. Um hash de pacote previsto em manifesto não representa comprovação do executável real até que os bytes sejam efetivamente adquiridos e validados.

A cadeia de uma tentativa em memória não implementa nem prova o estado de confiança persistente do TUF, antirrollback entre tentativas, atualização/rotação da raiz, isolamento de chaves privadas, publicação autenticada real, proteção de I/O delegado, autenticidade de bytes executados ou READY. O exemplo usa um objeto inerte, **não um ZIP válido** — o nome de asset representa somente o formato planejado.

D0 e D1 estão **isolados**. O parâmetro `discovered_tag` no D1 é simulado pelo laboratório; ainda falta construir vínculo efetivo entre a resposta de D0, os metadados oficiais autenticados e o pacote baixado. Não alegar integração fim a fim.

## 5. Próximo incremento recomendado

**D1.1:** conectar a decisão de versão de D0 a uma verificação TUF com estado de confiança persistente, raiz de produção apropriada e antirrollback, usando somente dados de laboratório até haver uma publicação oficial aprovada. Os vínculos devem impedir que uma tag do JSON da Release seja tratada como autoridade; a autoridade do conteúdo precisa vir da cadeia TUF. Testar mix-and-match de Releases, metadata replay, expiração, canais, arquiteturas, alvos extras, limites de tamanho e alterações de SHA. Só então iniciar **D2 — aquisição e preparação de bytes autenticados** sem ativação. Manter WORK para auditoria de integração significativa, não para repetir testes de metadados.

## 6. Invariantes

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas. HOLD é contenção.** Preservar B+A, TUF/NTFS/TOCTOU, H1/H2/H3 ainda bloqueados; não alterar main, PR #65 Draft, BETA .66, GF distribuído, Cloudflare, credenciais, políticas Windows, nem realizar merge, Release ou deploy.
