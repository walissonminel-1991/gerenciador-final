# CHAT ALTO — GERENCIADOR FINAL — D2: aquisição e preparo de bytes INERTES

**Data de referência:** 30/10/2026 (Fortaleza); CI executada em 31/10/2026 UTC. **Natureza:** laboratório Windows descartável; sem instalação, download real ou alteração do GF distribuído.

## 1. Fontes e estado congelado

- Repositório `walissonminel-1991/gerenciador-final`, única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `49b42d67d386579952515db222246eb1a30ee604`.
- **SHA técnico definitivo D2: `4a912555bb73b9e7d65984a2dd4eb41c9815b08b`**.
- [CI Windows 41151421931](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41151421931), job `124388585927`, tentativa 1, `completed/success`, SHA do runner = SHA técnico.
- Primeira [CI 41151239374](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41151239374), job `124388005172`, `completed/success`; após ela foi removida uma função auxiliar de rascunho não utilizada, e a CI foi repetida sem alterar a lógica validada.
- Evidências anteriores: D0 [41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551) SUCCESS (25); D1 [41148601868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148601868) SUCCESS (33); D1.1 [41150329284](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150329284) SUCCESS; D1.2 [41150770479](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150770479) SUCCESS com contraexemplo de rollback integral.

## 2. Arquivos implementados

- `prototipos/autoatualizador-seguro/github-d2-inert/d2_inert_lab.py`: laboratório isolado; importa o **parser C# real de D0** pelo mesmo projeto de testes de D1.1 e reutiliza as rotinas D1, D1.1 e D1.2 de manifesto, assinatura e associação de Release.
- `prototipos/autoatualizador-seguro/github-d2-inert/README.md`: delimitação de segurança, ausência de execução e próximos passos.
- `.github/workflows/gf-github-d2-inert-lab.yml`: runner Windows 2022, fontes e dependências fixadas, `permissions: contents: read`, sem credenciais persistentes, isolamento verificado por `dotnet msbuild -getItem:Compile`. O workflow só consulta/copia **fixtures locais**.

**Não foram alterados** `GerenciadorAplicativos.csproj`, aplicação distribuída, main, PR #65 Draft, BETA .66, Cloudflare, credenciais, infraestrutura de diagnóstico, seletor OLD/NEW, reconciliador, mecanismo de atualização ou código operacional de produção.

## 3. Encadeamento observado

1. Resposta de GitHub Releases **simulada** é interpretada pelo mesmo código C# D0, apenas consultivo.
2. A tag e o asset consultivo são vinculados ao manifesto D1, autenticado pelo python-tuf 7.0.1 com raiz Ed25519 **pública de laboratório** fixada.
3. O cliente TUF confere o alvo de manifesto por nome, tamanho e SHA-256, sem baixar nenhum pacote real.
4. A fonte de bytes `InertChunkSource` possui somente chunks em memória, com URL artificial tratada como marcador de identidade, **nunca como autorização de transporte ou de execução**. São bytes de texto, não ZIP nem executáveis.
5. Antes de gravar, o laboratório exige nome fixo `inert-authorized.fixture.bin`, raiz de teste criada sob `RUNNER_TEMP`, ausência de reparse/symlink no diretório observado, ausência de destino/temporário preexistente e orçamento de bytes limitado.
6. A escrita em arquivo temporário exclusivo calcula tamanho e SHA-256; o arquivo é relido e o digest reconfirmado antes e após a renomeação. Falhas abortam e tentam limpar os artefatos do próprio laboratório; um sentinela externo à pasta de preparo permanece intacto.

Isso prova apenas o comportamento observado da fixture no runner, não uma barreira robusta contra adversário de mesmo usuário ou contra alterações futuras após a verificação.

## 4. Resultado verificável

Log `GF_D2_RESULT` da CI válida:

```json
{
  "schema":"gf-github-d2-inert-acquisition/1",
  "status":"passed",
  "positive_inert_acquisitions":2,
  "adversarial_rejected":21,
  "d0_real_csharp_parser_used":true,
  "manifest_authenticated_with_public_test_key":true,
  "memory_only_source":true,
  "readback_sha_verified":true,
  "outside_stage_sentinel_preserved":true,
  "real_network_requests":0,
  "executable_downloads":0,
  "executable_launches":0,
  "installations":0,
  "product_modified":false,
  "same_user_toctou_resistance_proven":false,
  "full_history_rollback_resistance_proven":false,
  "production_signing_authority":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

Negativos, todos recusados: conteúdo de fonte alterado, truncado, de mesmo tamanho com bytes diferentes, bytes extras, origem vazia, URL hostil/tag errada/redirect, tentativa de travessia de caminho, nome não permitido, destino ou arquivo parcial já existente, adulteração pós-escrita, adulteração pós-releitura, chunk acima do orçamento, diretório externo à área autorizada, Release com tag incompatível, nome/tamanho/URL do asset conflitante, e manifesto divergente da cadeia TUF.

## 5. Limites e falsificações que ainda exigem estudos

- **H1 continua refutado no modelo de laboratório**: D1.2 demonstrou que restaurar todo o histórico confiável v1 após v2 permite aceitar a versão antiga. D2 não altera nem resolve isso.
- O teste lê um fluxo em memória. **Não valida o transporte HTTPS real, redirecionamentos, intermediários, cabeçalhos, troca de certificados, downloads parciais ou assets publicados no GitHub**. A API oficial segue com zero Releases na última verificação de D1.2; não foi realizada nova publicação nesta missão.
- As chaves privadas de teste são derivadas de sementes públicas no repositório; **não existe autoridade de assinatura operacional**. Uma assinatura produzida por essas chaves só autentica a fixture de teste.
- Há verificações limitadas de reparse e releitura de hash; **nenhuma alegação de proteção NTFS/TOCTOU universal**. Um processo do mesmo usuário pode interferir em caminhos, diretórios, handles e arquivos entre syscalls. Também não foram testados crash, corte de energia, persistência durável, instalação, extração ou execução dos bytes preparados.
- O nome do asset na Release fictícia identifica um suposto ZIP portátil, mas o payload deste ensaio é explicitamente **texto inerte**. Não é um produto ou instalador funcional.
- `H2`, `H3`, G1–G8 e READY permanecem sem demonstração operacional.

## 6. Próxima etapa recomendada

**D2.1 — transporte e fonte de objetos controlados em laboratório:** estudar leitura por streaming de um endpoint estritamente restrito e simulado, recusando redirect/host equivocado, Content-Length ausente/incompatível, cortes e fontes alteradas. Continuar com bytes inertes e **não publicar uma Release nem baixar executáveis**. Só depois considerar **D3**: coordenação OLD/NEW inerte com política de HOLD; fazer revisão WORK independente em momento de integração efetiva de controles criptográficos/NTFS/transacionais, não por cada fixture.

## 7. Invariantes

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas. HOLD é contenção, não recuperação.** Preservar Opção B, privacidade, dados pessoais, separação de produto e laboratório. Sem merge, Release, deploy, ativação de backend ou qualquer operação nos PCs FORTE/FRACO.
