# GERENCIADOR FINAL — CHAT ALTO — D2.4.4: consolidação adversarial de evidências

**Data de referência:** 30/10/2026 em Fortaleza; execução registrada em 31/10/2026 UTC. **Classificação:** somente laboratório de bytes inertes em Windows Actions descartável, **NÃO** homologação operacional.

## 1. Identidade e proveniência

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `2fff1bacaf3949a62c28062e031e29cf98f2f968`.
- SHA técnico **D2.4.4 congelado**: `8eca802788a97b8ac22dfc0210b88c25e44aab15`.
- [CI Windows D2.4.4, run 41162059466](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41162059466), job `124421556427`, tentativa 1, `completed/success`, `head_sha` idêntico ao SHA técnico.
- D2.4.3: [CI 41161161598](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41161161598), `SUCCESS`, técnico `cbcfa630dd79e160807023eccbc6b5a6fe571462`.
- D2.4.2 no SHA D2.4.3: [CI 41161161924](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41161161924), `SUCCESS`.
- Regressões da integração antiga no novo incremento: [CI 41162036830](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41162036830) e [CI 41162036828](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41162036828), ambas `SUCCESS` no SHA `dbeebac1ba2ef28885d48940de4fac944cc4b8ee`. A consolidação D2.4.4 recompilou e executou tudo no SHA final, portanto não depende apenas dessas regressões antigas.

## 2. Mudança precisa

**Exatamente dois arquivos adicionados** desde o checkpoint de entrada, confirmado por comparação GitHub; nenhuma modificação no código TUF, C# coordenador, HttpClient, NTFS ou GF distribuído:

1. `prototipos/autoatualizador-seguro/github-d24-integrated/d244_consolidated_gate.py`: um **validador de evidências**, não um novo downloader nem cliente TUF. Executa os programas existentes D2, D2.3, D2.4.2 e D2.4.3 no mesmo runner. Exige exatamente um relatório por suíte e compara **conjunto exato de campos, tipos nativos e valores esperados**, inclusive booleanos estritos (`false` não é `0`). O parsing rejeita chaves JSON duplicadas, campo ausente/extra, troca de tipos, valores forjados e dados anexados após JSON.
2. `.github/workflows/gf-d244-consolidated-lab.yml`: workflow Windows 2022, .NET 10 e Python 3.13 fixados, bibliotecas TUF de **TESTE** pinadas, checkout sem credenciais persistentes, `contents:read`. Compila D0, transportador .NET D2.3 e coordenador D2.4 experimental fora do projeto principal, e executa o gate Python com uma confirmação adicional em PowerShell. Nenhum artefato executável de GF é publicado.

A CI consolidada tem um **filtro de paths abrangente** para `prototipos/autoatualizador-seguro/**` e para todos os quatro workflows predecessores além do próprio D2.4.4. O próprio gate verifica que **20 dependências específicas** (código D0, D1, D1.1, D1.2, D2, D2.3, coordenador C#, verificadores Python, bootstrap, gerador de fixtures e workflows) estão incluídas nesse gatilho. Isso **não altera os filtros antigos individualmente**: o workflow novo fornece uma regressão integrada adicional no mesmo commit.

## 3. Resultado observado e contraprovas

O job `124421556427` emite quatro linhas `GF_D244_VERIFIED_*`, que foram comparadas em estrutura e tipo com contratos fixos, e a linha final:

```json
{
  "schema":"gf-d244-strict-inert-evidence/1",
  "status":"passed",
  "independent_test_suites_executed":["D2","D23","D242","D243"],
  "required_trigger_dependencies_covered":20,
  "schema_mutations_rejected":285,
  "exact_field_sets_and_types_enforced":true,
  "duplicate_missing_extra_and_forged_fields_refused":true,
  "tuf_replay_with_intact_history_blocked":true,
  "full_history_rewind_reaccepted_old_version":true,
  "full_history_rewind_is_security_counterexample":true,
  "same_user_rollback_resistance_proven":false,
  "same_user_ntfs_toctou_resistance_proven":false,
  "cooperative_lock_is_security_boundary":false,
  "production_signing_authority":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false,
  "real_external_downloads":0,
  "real_executable_installs":0,
  "product_modified":false
}
```

**Suites executadas no mesmo SHA:**

- **D2:** 2 preparos inertes aprovados e 21 recusas; D0 verdadeiro + metadados TUF de TESTE + integridade de bytes, sem rede externa.
- **D2.3:** 3 transferências HTTP reais exclusivamente em loopback, 29 negativas, 31 conexões locais, 28 relacionadas a falhas, 2 canários preexistentes com identidade/bytes preservados e deadline cooperativo.
- **D2.4.2:** 1 sessão positiva C# → Python-TUF → mesmo transportador .NET, 15 sessões negativas, 7 recibos sintéticos recusados, bloqueio da antiga entrada `--d24`.
- **D2.4.3:** duas gerações TUF entre processos Windows, replay antigo recusado com histórico íntegro, concorrência cooperativa recusada, cache parcial contido em HOLD, **rewind integral do trust store aceita novamente v1**, seis observações NTFS (hardlink, junction, symlink, substituição de caminho, conteúdo alterado mantendo ID e handle íntegro), sentinela preservada. `--d243` observa e **não chama Stage**.

**285 mutações adversariais do contrato de relatório foram recusadas**, incluindo remoção de cada campo, adulteração de cada valor, tipo incompatível para cada campo, chave extra, chave duplicada e material JSON após o documento. Esses testes comprovam o comportamento deste gate sobre os casos exercitados, **não** tornam o emissor ou runner invioláveis.

## 4. Limitações de segurança deliberadamente preservadas

- **H1 continua com contraexemplo reproduzido:** com histórico v2 intacto, replay de v1 é rejeitado; após trocar o trust store inteiro por cópia v1, v1 volta a ser aceito. Não existe estado antirrollback imutável/durável comprovado no modelo de mesmo usuário.
- H2/H3, TOCTOU NTFS mesmo usuário, substituição de processo/interpretador, ACL/handle universal, crash/power loss e vínculo criptográfico dos bytes realmente executados **não estão comprovados**.
- O mutex é **apenas cooperativo**; não é autoridade de segurança contra processo adversarial da mesma conta.
- Os testes usam raiz TUF e assinaturas deriváveis de seeds **públicas de laboratório**. Não existe chave privada operacional, download HTTPS de GitHub/CDN, autenticação de redirects reais, pacote executável validado nem instalação.
- O resultado de `GF_D244_RESULT` é uma **evidência declarada e testada**, não substitui auditoria de código independente ou validação física.
- O workflow depende das ações externas normais de setup de SDK/dependências, mas **nenhum asset executável do GF** é solicitado, baixado ou instalado pelo laboratório.

## 5. Próxima ação autorizada: WORK independente somente leitura

**Congelar código no SHA técnico D2.4.4 e enviar auditoria WORK, sem iniciar D3.** O revisor deve buscar contraexemplos independentes em fonte e logs, não apenas certificar o relatório. Avaliar lacunas remanescentes de persistência TUF, links internos de root history, concorrência, IPC Python/C#, parse do recibo, verificação do arquivo realmente executado, critérios de HOLD e gatilhos/gates da CI.

**Missão resumida para WORK:** repositório `walissonminel-1991/gerenciador-final`, branch `experimento-g5-ipc-chat-alto-20261029`, SHA técnico `8eca802788a97b8ac22dfc0210b88c25e44aab15`, CI D2.4.4 `41162059466` SUCCESS (job `124421556427`). Ler este checkpoint, os checkpoints D2.4.2/D2.4.3, diff desde `2fff1bac...`, e inspecionar fontes reais; comparar reivindicações com evidências, classificar falhas comprovadas versus hipóteses, fornecer opções mínimas de correção sem custo e sem duplicar TUF ou downloader. **Somente leitura, sem commits, execução de produto, merges ou promoção.**

## 6. Estado final

**Opção B; Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas; HOLD = contenção, não recuperação.** D3 continua bloqueada. Sem mudanças em main, PR #65 Draft, BETA .66, Cloudflare, GF distribuído, dados pessoais, credenciais, Release, merge ou deploy.
