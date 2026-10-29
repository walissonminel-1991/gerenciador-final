# GERENCIADOR FINAL — CHAT ALTO — G1: contrato experimental de recibo assinado

**29/10/2026 | Subprova criptográfica inerte | G1–G8: 0/8 gates homologados**

## 1. Baseline e evidência de execução

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch isolada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial confirmado: `38fe432533465467986b42c4de0bc23bdc596fdf`.
- Checkpoint de origem lido: `docs/CHAT-ALTO-G1-STAGE2-ACK-BINDING-CHECKPOINT-20261029.md`.
- **Commit com código aprovado na CI:** `beb565fa1e478fb7e1343667138cbecae15f3650`.
- **Windows CI:** [40902657571](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40902657571), SUCCESS, **16/16 PASS, 0 FAIL, 0 warnings, 0 errors**, `windows-2022`, .NET SDK `10.0.402`, restore `--locked-mode`.
- PR #65 Draft verificada antes da edição com HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`. Reconferir após salvar.
- Não houve uso de WORK, merge, release, deploy, criação de serviços, segredos, credenciais persistentes, contas, endpoints ou ativação de telemetria.

## 2. Problema / novidade demonstrada

A subprova Stage2 anterior registrava `g1-ack.fixture.json` com **checksum local**, permitindo apenas verificar integridade acidental e correspondência estrutural entre `intent`, `TxProposal` e recibo. Um mesmo usuário que adulterasse os arquivos poderia reconstruir tal checksum.

Foi criado um **protocolo de resposta assinado totalmente separado** em `prototipos/autoatualizador-seguro/m3-g1-crypto/`:

- `G1CryptoFixture.cs.txt`: `G1SignedResponse`, `G1SignedFixture`, `G1DisposableSigner`, `G1PinnedFixtureVerifier`, `G1RotationGrant`.
- `G1CryptoTests.cs.txt`: 16 testes negativos/positivos de assinatura, replay, autenticação de chave, vínculo Stage2 e rotação.
- `G1Crypto.csproj`: console experimental independente. Compila Stage2 original, `G1Preselect` e modelo G1-TX já estabelecido; nenhuma alteração aos fontes anteriores.
- `.github/workflows/autoatualizador-m3-g1-crypto-isolado.yml`: workflow Windows exclusivo, usando dependências já travadas, valida relatório e confirma que os fontes novos não entraram no `Compile` do executável GF.

**O formato experimental novo NÃO substitui, reescreve ou autentica automaticamente `g1-ack.fixture.json` do teste anterior.** Testes novos tratam respostas assinadas e recepção de uma consulta com desafio, não ativação de release nem testemunha externa real.

## 3. Contrato criptográfico exclusivamente em fixture

1. **Algoritmo:** ECDSA sobre curva NIST P-256, SHA-256, assinatura DER `Rfc3279DerSequence`, APIs .NET `ECDsa.SignData`/`VerifyData`. Não implementar primitives criptográficas próprias.
2. **Chaves:** `G1DisposableSigner` cria par efêmero em RAM por `ECDsa.Create(nistP256)`; nenhuma chave privada é exportada, persistida, enviada ou exibida no relatório. Somente `SubjectPublicKeyInfo` da chave pública pode ser confiado **explicitamente** pelo harness; `KeyId` = SHA-256 do SPKI da chave atual.
3. **Trust bootstrap:** `G1PinnedFixtureVerifier` recebe chave pública fixada pelo próprio harness em memória. **Não lê e não aceita chave pública de mensagem não confiável como nova raiz.** Esse bootstrap **não** prova instalação, enrolamento, rotação ou distribuição operacional segura.
4. **Separação de domínio:** assinatura envolve `Schema=gf-g1-crypto-fixture/1` e propósito `g1-witness-fresh-response-not-update-permit`; concessão de rotação usa outro propósito `g1-witness-key-rotation-not-root-reset`.
5. **Resposta vinculada:** bytes canônicos via `StateCodec.Encode` incluem schema, propósito, época, `KeyId`, desafio, `Stage2Nonce`, digest integral da `intent` e `TxReceipt` com `InstallationId`, `TxId`, checkpoint anterior e checkpoint proposto.
6. **Validação:** `G1SignedFixture.BindOnDisk` abre `FixtureSession` com lease Stage2, confere cadeia e `intent`, reabre predecessor canônico via guardas Stage2, confirma `PreviousDigest`, `Confirmed`, selector antigo e fixa hashes/identidades dos bundles. `G1PinnedFixtureVerifier.Verify` compara época/key ID fixados, desafio fornecido de novo pelo chamador, assinatura e todos os campos do vínculo.
7. **Rotação:** `G1RotationGrant` associa época anterior/seguinte, `KeyId` antigo, `KeyId` novo e chave pública sucessora. **Exige assinatura da chave ativa anterior**, época exatamente `+1`, correspondência do hash SPKI e curva P-256. Após `Rotate`, chaves/épocas antigas são recusadas. Estado de rotação permanece **somente em memória**, não resistente a rollback.
8. **Resultado:** única classificação possível para recibo validado é `G1CryptoAssessment.SignatureMatchesButBlocked`. Não existe `Authorized`, `Activate`, `Confirm`, `ReplaceSelector` ou serviço externo no caminho.

O verificador NÃO comprova frescor global: um witness comprometido pode assinar resposta antiga em desafio novo. O desafio de 128 bits é gerado com `RandomNumberGenerator` no teste, e um registro assinado salvo em disco deve ser recusado com outro desafio. O código de validação não possui âncora de clock, epoch durável, quórum nem compromisso externo.

## 4. Matriz de testes inéditos — 16 PASS

| ID | Caso e resultado |
|---|---|
| CRYPTO-01 | Resposta válida com chave pinada: `SignatureMatchesButBlocked`, seletor antigo, `RECOVERY_REQUIRED` |
| CRYPTO-02 | Assinatura DER adulterada: `G1_CRYPTO_BAD_SIGNATURE` |
| CRYPTO-03 | Assinante desconhecido, ainda que tenha assinatura válida: `G1_CRYPTO_UNTRUSTED_EPOCH_OR_KEY` |
| CRYPTO-04 | Assinante não autorizado falsifica `KeyId` confiado: verificação matemática da assinatura recusa |
| CRYPTO-05 | Mensagem assinada com outra instalação: `G1_CRYPTO_STAGE2_BINDING` |
| CRYPTO-06 | Nonce da transação Stage2 modificado e assinado de novo: recusado por binding |
| CRYPTO-07 | Replay da resposta com nonce de consulta anterior: `G1_CRYPTO_STALE_CHALLENGE` |
| CRYPTO-08 | Digest do checkpoint estável alterado, mas assinado: recusado |
| CRYPTO-09 | Digest da `intent` alterado, mas assinado: recusado |
| CRYPTO-10 | Rotação assinada corretamente pela chave ativa para época 2; nova aceita apenas para diagnóstico, chave/época antiga e replay de concessão recusados |
| CRYPTO-11 | Tentativa de rotação assinada por terceiro não confiado: `G1_ROTATION_BAD_SIGNATURE` |
| CRYPTO-12 | Chave sucessora alterada na concessão, com ou sem nova assinatura por chave ativa: `BAD_SIGNATURE` ou `KEY_MISMATCH` |
| CRYPTO-13 | Resposta antiga assinada e salva em sidecar inerte, reaberta com novo desafio: replay recusado, **sem fallback offline** |
| CRYPTO-14 | `TxId` incompatível, porém assinado: recusado |
| CRYPTO-15 | Uso indevido do propósito de rotação como resposta: recusado |
| CRYPTO-16 | Sem geração Stage2 `intent`, não existe resposta que permita validar operação |

O relatório `G1_CRYPTO_FINAL_REPORT` registrou:
`signed_valid_still_blocked=true`,
`stage2_identity_binding_enforced=true`,
`fresh_challenge_replay_denied=true`,
`untrusted_signer_denied=true`,
`rotation_revoked_prior_epoch=true`,
`invalid_rotation_signature_rejected=true`,
`saved_receipt_not_fresh_without_live_response=true`,
`intent_candidate_still_not_selected=true`,
`private_keys_persisted=false`,
`trust_root_automatically_enrolled=false`,
`authenticated_remote_witness_exists=false`,
`independent_freshness_verified=false`,
`physical_power_loss_tested=false`,
`operational_gates_closed=0`,
`product_built=false`,
`product_executed=false`,
`update_authorization_issued=false`.

## 5. Limitações técnicas e bloqueios que permanecem

- **Nenhum witness autenticado real.** A confiança na chave pública fornecida pelo próprio teste é uma premissa. Não existe infraestrutura independente, root seguro de inscrição inicial, autorização do mantenedor, serviço, conexão, auditoria nem prova de durabilidade remota.
- **Rotação só em RAM.** O conjunto de chaves/epochs autorizados pode ser revertido ao reiniciar o processo. O teste demonstra validação de *grant* na sessão atual, não anti-rollback após reinstalação, snapshot do disco nem comprometimento do assinante antigo.
- **Assinatura não prova frescor em witness mentirosa.** Vincular nonce à assinatura rejeita **replay de mensagens antigas**, não impede que uma fonte maliciosa emita resposta falsa atualizada com nonce novo.
- **Sem contrato operacional de consentimento/política de expiração.** Campos de tempo, sequência de witness durável, deadline remoto e regra de revogação/recuperação de keys não foram tratados. Raiz de confiança *provisionada* não está homologada.
- **Recibo lateral anterior continua não autenticado.** O código de experimento não transformou o checksum `G1AckBody` em assinatura nem substituiu seu tratamento. Não deve integrar um caminho de ativação enquanto essa incompatibilidade existir.
- **Sem seleção/execução.** O fluxo experimental antigo `FixtureSession.Begin` ainda pode mudar `selector` antes de compromisso G1; a subprova não alterou seu caminho nem o executável real.
- **G8/TOCTOU/falha física.** Cancelamento durante I/O síncrono, TOCTOU de arquivos sob o mesmo usuário, `Flush(true)` sob queda elétrica, commit distribuído e rollback integral da witness não foram demonstrados.

**Decisão: G1–G8 continuam BLOCKED, 0/8 gates operacionais aprovados.** Nenhuma evolução funcional de roadmap GF decorre dessas fixtures.

## 6. Próximo ganho real e momento do WORK

Esta rodada fornece um contrato concreto de mensagem, rotação e validação assinado, com matriz negativa verificável em Windows. A próxima pesquisa útil é uma **prova de rollback da raiz/epoch de confiança através de reinício local**, distinguindo proteção de disco e um anchor verdadeiramente independente. Um novo ensaio deve demonstrar algo que os testes desta rodada não cobrem; não repetir assinaturas adulteradas ou replay simples.

**Antes de integração operacional**, requer-se revisão especializada independente das decisões de trust bootstrap, rotação/revogação e freshness anti-rollback — agora com SHAs, arquivos e 16 casos negativos disponíveis. Isso pode justificar **um único WORK de auditoria delimitada** em ponto futuro decidido pelo mantenedor, sem consumi-lo para tarefas mecânicas. Sem autorização explícita, nenhum serviço real ou chave persistente deve ser criado.

Não alterar a PR #65 Draft, main, BETA .66, telemetria, infraestrutura ou PCs pessoais. Sem merge, release ou deploy.
