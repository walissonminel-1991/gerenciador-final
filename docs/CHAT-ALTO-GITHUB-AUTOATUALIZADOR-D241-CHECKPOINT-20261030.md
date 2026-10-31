# GERENCIADOR FINAL — CHAT ALTO — D2.4.1: preservação conservadora, deadline e evidência de falhas

**Data de referência:** 30/10/2026 no fuso America/Fortaleza; CI registrada em 31/10/2026 UTC. **Natureza:** laboratório inerte Windows descartável, sem integração ao GF distribuído.

## 1. Base e estado verificado

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch de escrita autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial: `1e6884d79a6c6b648dc4b9b8949c6b016e3c9144`.
- Decisão de escopo: `docs/CHAT-ALTO-D24-WORK-DECISAO-CORRETIVA-20261030.md`, aprovada após parecer adversarial WORK D2.4.
- **SHA técnico D2.4.1 validado:** `44f1a39bed2a32bc51f5c54dafeac5196114db75`.
- **Regressão D2.3 D2.4.1:** [GitHub Actions 41157676188](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41157676188), job `124407967880`, tentativa 1, `completed/success`, executou exatamente o SHA técnico final.
- **Regressão integração D2.4:** [GitHub Actions 41157445756](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41157445756), job `124407421674`, `completed/success`, executou SHA `9073fc0d27d7669d074511b2f7b1da9c7136a9e9`. Comparação do SHA dessa CI ao SHA técnico final mostra **somente** modificação posterior do workflow D2.3, não do C# compartilhado ou da ponte D2.4.
- Uma [tentativa D2.3 anterior 41157451148](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41157451148) marcou `FAILURE` no gate após executar o teste porque o workflow exigia simultaneamente contagem antiga 27 e nova 31 para o mesmo campo. A condição antiga foi removida **sem reduzir os critérios novos**; a CI definitiva reportou os dados completos e passou.
- Duas execuções ainda anteriores foram superadas/canceladas por mudanças sucessivas. Não são evidência da validação final.

## 2. Arquivos alterados

Exatamente três arquivos técnicos, verificados por comparação de commits:

1. `prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/HttpClientLoopback.cs.txt`
   - O `Stage` **não apaga mais `inert.partial` ou `inert.fixture.bin` por nome no `finally`**. `FileMode.CreateNew` mantém a regra de não sobrescrever temporário existente e `File.Move(overwrite:false)` não sobrescreve destino existente.
   - Falha deixa quaisquer artefatos de propriedade incerta preservados, sem declará-los instaláveis ou recuperados. Remoção da pasta de **teste** ocorre exclusivamente após assertions, dentro do runner descartável.
   - Novo teste de canário cria temporário ou destino preexistente, mantém handle aberto, usa `GetFileInformationByHandle` em Windows para comparar identificador (volume e file index) antes/depois e exige bytes iguais.
   - `Stage` cria `CancellationTokenSource.CreateLinkedTokenSource`, deadline de **2 segundos**, propagado para `HttpClient.SendAsync(ResponseHeadersRead)`, obtenção do stream, leitura e escrita assíncronas, e releitura assíncrona do SHA-256 antes/depois da promoção. Uma operação síncrona de arquivo ou agendamento do sistema não recebe garantia universal de preempção.
   - Servidor local passou a permitir atraso intencional entre blocos; novos negativos: headers válidos com corpo parado e gotejamento lento, ambos recusados por deadline.
   - Nos testes, contagem de conexões é sempre recolhida no `finally`, inclusive quando `Stage` lança exceção.
2. `prototipos/autoatualizador-seguro/github-d24-integrated/D24Handoff.cs.txt`: recolhe `server.Accepts` no `finally` de `Stage`, para não relatar falsamente zero conexões em uma falha posterior à conexão.
3. `.github/workflows/gf-d23-dotnet-httpclient-lab.yml`: exige relatório de 3/3 positivos, 29/29 negativos, 31 conexões totais, 28 de falhas, dois canários preservados e declarações de prazo/preservação. Removido predicado contraditório que ainda exigia 27 conexões; relatório agora é impresso antes da checagem do gate para diagnóstico.

Não houve edição de D0, Python-TUF, trust store, workflow D2.4, seleção OLD/NEW, ponto de recuperação, fonte principal do GF, main, PR #65, BETA .66, Cloudflare, credenciais ou Release.

## 3. Resultado observado

Log `GF_D23_RESULT` da CI 41157676188:

```json
{
  "schema":"gf-d23-dotnet-httpclient-loopback/1",
  "status":"passed",
  "positive_inert_streams":3,
  "negative_cases_rejected":29,
  "loopback_connections_observed":31,
  "failed_loopback_connections_observed":28,
  "preserved_preexisting_file_ids":2,
  "complete_acquisition_deadline_tested":true,
  "failure_preserves_unknown_file_identity":true,
  "http_client":"dotnet10-System.Net.Http.HttpClient",
  "only_numeric_loopback_connection":true,
  "redirects_disabled":true,
  "cancellation_tests_included":true,
  "inert_sha256_readback_verified":true,
  "outside_staging_sentinel_preserved":true,
  "d0_d1_tuf_chain_verified_in_dotnet_lab":false,
  "production_trust_root":false,
  "github_asset_redirect_supported":false,
  "tls_or_real_github_tested":false,
  "same_user_toctou_resistance_proven":false,
  "full_history_rollback_resistance_proven":false,
  "real_executable_downloads":0,
  "installations":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

O total de negativas cresceu de 25 para 29, **sem substituir testes antigos**. As quatro novas contraprovas foram: canário preexistente no parcial, canário preexistente no destino, corpo parado após headers válidos e corpo entregue lentamente. O teste de identidade usa handle e file ID, além do conteúdo; esses resultados dizem respeito às fixtures controladas, **não a todas as corridas/junctions same-user**.

A CI D2.4 41157445756 manteve o positivo `D0→Python-TUF de TESTE→.NET inerte`, as 12 negativas de preflight, 14 negativas na ponte C#, e **contraexemplo de bypass TUF direto ainda ACEITO**. Ele não foi ocultado nem corrigido nesta etapa.

## 4. Limites, HOLD e autoridade

- A política conservadora de erro **não apaga objetos de propriedade não demonstrada**. Isso pode deixar bytes parciais no estágio isolado, para contenção/diagnóstico. Não existe recovery/rollback automático nem classificação de recuperação. O runner remove seu diretório de teste descartável após validar os canários, o que não valida limpeza operacional em cenários de concorrência.
- Um deadline de 2 s é **cooperativo para as etapas assíncronas**. Não há prova de interrupção preemptiva de `File.Move`, operações NTFS síncronas, threads suspensas, travamentos de kernel, crash do processo ou energia; não prometer que absolutamente toda tentativa termina em 2 s.
- `H1` continua não comprovado: D1.2 demonstrou rewind integral da base confiável. `H2`, `H3`, integridade de identidade de handles durante todo TOCTOU, ancestral junction e manipulação same-user continuam sem prova.
- O D2.4 ainda admite chamada direta de ponte C# com manifesto e hash públicos **sem TUF**. Isso é explicitamente BLOQUEADOR operacional para D2.4.2. Nenhuma assinatura de produção foi criada.
- Nenhum download executável, instalação, acesso externo, alteração do produto distribuído, Release, merge ou deploy.

## 5. Próxima etapa

**D2.4.2 apenas:** coordenador C# único detentor da sessão; validação Python-TUF obrigatória iniciada/gerenciada pelo próprio coordenador; eliminar o bypass por manifesto+hash de origem externa. Não duplicar TUF nem transportador. Corrigir o snapshot de bootstrap TUF lido duas vezes e executar negativas dirigidas de sessão falsa, preflight ausente, manifesto alterado com hash novo, IPC/replay e interrupção, **sem alegar isolamento criptográfico contra o próprio usuário**.

D2.4.3 (persistência e NTFS), D2.4.4 (CI consolidada/WORK) e D3 permanecem etapas distintas. Não usar WORK entre pequenas correções; nova revisão adversarial somente após integração corretiva consolidada.

## 6. Invariantes congelados

**Opção B; Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD somente contenção, não recuperação.** H1/H2/H3 e TOCTOU same-user não comprovados. Sem modificar main, PR #65 Draft, BETA .66, Cloudflare, GF distribuído, credenciais ou dados pessoais.
