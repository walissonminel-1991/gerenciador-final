# GERENCIADOR FINAL — Fase 1 — Atribuição causal de efeitos: duas transações e aliases NTFS

**Data:** 30/10/2026. **Classificação:** ensaio negativo com arquivos inertes, exclusivo de GitHub Actions Windows descartável. Não é implementação de recuperação nem teste de Windows Installer real.

## 1. Âncora técnica confirmada antes de alterar

- Repositório `walissonminel-1991/gerenciador-final`; única branch autorizada `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial `6926a5f8c6fe3e2f6c2ef88da5b996aa48775a36`, consultado no GitHub.
- Último commit técnico validado antes deste ensaio `bf314f17b5aceb98e9f3801fce4fc3a2212c467a`.
- CI Windows anterior [41087305170](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41087305170), job `124171724810`, `completed/success`, 22/22 `PASS_CONTAINMENT`, zero recuperações.
- PR #65 confirmada `open/Draft`, sem merge.
- Barreira de `Phase1Recovery.cs.txt`: `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`, `UnknownBlocked`. **Não foi editada.**

## 2. O novo problema: identidade do objeto não prova origem

A integridade de objeto NTFS (volume e File ID), assinatura, TUF, hash do conteúdo e caminhos canônicos permite verificar **qual objeto** foi lido/alterado, em condições determinadas. Isso não identifica, por si, **quem causou** o efeito, nem associa esse ato a uma única transação do autoatualizador, sobretudo sob múltiplos caminhos e atores.

Um arquivo NTFS pode possuir duas entradas de diretório (*hard links*) para o mesmo File ID. Ambas são nomes da **mesma instância de arquivo** e exibem as alterações de conteúdo feitas através de qualquer alias. Documentação oficial:
- [Hard Links and Junctions](https://learn.microsoft.com/en-us/windows/win32/fileio/hard-links-and-junctions).
- [CreateHardLinkW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createhardlinkw).

A atribuição por `path`, `File ID`, `SHA256` e um `transactionId` *informado pelo mesmo usuário* é insuficiente para decidir causalidade transacional. File ID não identifica o processo originador. Mesmo se nenhuma colisão de hash ocorrer, o caminho pode ser apenas um dos nomes de um objeto compartilhado.

**Não extrapolar:** a demonstração se limita ao conjunto de campos observados; não afirma que todo vestígio de execução do Windows (como ETW autoritativo com cadeia completa) seria idêntico.

## 3. Ensaio novo, distinto dos brokers

Commit técnico de laboratório `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`:

- Novo script `prototipos/autoatualizador-seguro/phase1-composed/effect_attribution_alias_probe.py`. Exige `os.name == 'nt'`, `GITHUB_ACTIONS=true`, `RUNNER_TEMP` e endpoint da BETA desativado. Só cria e elimina artefatos em pasta nova sob `RUNNER_TEMP`.
- Criadas duas transações de teste com identificadores distintos, `txA` e `txB`, e dois caminhos `tx-a-view.fixture` e `tx-b-view.fixture`, vinculados ao **mesmo arquivo físico** por `os.link`/NTFS. Nenhum arquivo do GF distribuído é alterado.
- O teste executa dois processos Python independentes sob o mesmo usuário no runner, um gravando pelo caminho A e outro pelo B, em experiências sequenciais com reset para o mesmo estado inicial, preservando a identidade do arquivo.
- O mesmo `claimed_transaction=txA` (dado de fixture sob controle do usuário) acompanha as duas observações. Em ambas, o observador restrito registra nomes relativos, File ID/volume, número de links, tamanho, SHA-256 e a alegação de transação; as visões devem ser idênticas, apesar das **origens controladas distintas**.
- A segunda execução usa o caminho de `txB` apesar da alegação `txA`. Exige-se o campo `same_user_claim_misattributed_other_origin=true`.
- A classificação não é `RECOVERY` nem `QUIESCENCE`: `NEGATIVE_PROVENANCE_COUNTEREXAMPLE`, `external_authority_established=false`, `quiescence_proven=false`, `automatic_recovery_permitted=false`, `operational_ready=false`.
- Integração limitada a uma etapa adicional em `.github/workflows/gf-autoatualizador-fase1-real-lab.yml`, sem diminuir ou substituir os 22 testes de contenção existentes.

**Evidência separada:** o supervisor sabe qual alias passou ao processo de teste; isso é a variável independente do laboratório e **não** uma autoridade de lançamento do GF em produção. O observador projetado de estado não recebe esse histórico confiável e, portanto, não consegue identificar origem causal só pelos campos que vê.

**Hipótese falsificável:** em um NTFS real do runner, com dois aliases do mesmo File ID, ambas as gravações produzem exatamente a mesma evidência no modelo selecionado; se a CI divergir, deve falhar e a razão precisa ser diagnosticada. Não transformar falha de `os.link`/filesystem em prova de exclusividade de efeitos.

## 4. Classes de evidência para uma atribuição segura

| Evidência | O que pode provar | O que não prova isoladamente |
| --- | --- | --- |
| Manifesto TUF + hash de pacote | Identidade e autenticidade dos bytes autorizados | Quem executou a imagem; efeitos de processos externos |
| Caminho normalizado + volume/File ID | Qual objeto do sistema de arquivos foi acessado | Qual transação provocou a escrita; aliases hardlink |
| `transactionId`, JSON, GUID, recibo do GF | Associação declarada por quem os escreveu | Origem confiável, frescor, exclusividade e anti-rollback same-user |
| PID/árvore/Job/saída do fornecedor | Estado localizado e correntemente observado dos processos incluídos | Trabalho futuro, serviço externo, ação MSI assíncrona |
| ETW `ActivityID`/cadeia de eventos | Pode **correlacionar eventos emitidos por provedores cooperantes** | Completude em crash, cobertura de todos os serviços, prova de não execução; ETW pode perder eventos |
| Identificador do provider protegido, emitido por outro principal | Hipótese de associação mais forte se a autoridade autenticar cada ato | Despacho universal, efeitos delegados fora do contrato, durabilidade e quiescência sem provas adicionais |

Fontes oficiais:
- [ETW About Event Tracing](https://learn.microsoft.com/en-us/windows/win32/etw/about-event-tracing), inclusive **missing events**.
- [ETW Correlation ActivityID/RelatedActivityID](https://learn.microsoft.com/en-us/windows/win32/wes/eventschema-correlation-systempropertiestype-element).
- [Windows Installer Custom Action Return Processing Options](https://learn.microsoft.com/en-us/windows/win32/msi/custom-action-return-processing-options), ações EXE `Async|Continue` (`0xC0`) podem continuar depois do cliente Windows Installer.

## 5. Decisão

**Não atribuir efeitos a uma transação por campo declaratório same-user ou pelas propriedades do arquivo.** Isso não impediria que A e B afetassem o mesmo objeto, nem que um terceiro processo fora do Job atuasse sobre ele. Para um dia permitir recuperação, seriam necessárias proveniência com fronteira de confiança, despacho obrigatório ou garantia explícita mais estreita, persistência anti-rollback, e prova independente de quiescência dos efeitos pertinentes — H1/H2/H3 continuam sem demonstração operacional.

O ensaio não cria autoridade, não escreve em registro/Windows Update, não executa MSI, serviços, UAC, manutenção ou instalação, nem emite telemetria. Ele também não instala/integra o GF e não modifica `Phase1Recovery.cs.txt`.

**CI nova:** [41089673054](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41089673054), associada ao commit `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`. **Status ainda pendente de conferência no momento desta primeira redação.** O checkpoint precisa distinguir resultado do teste de alias do 22/22 já validado anteriormente.

Fase 1 **ABERTA**, `READY=false`, G1–G8 `BLOCKED 0/8`. Sem WORK, merge, release, deploy, alteração da `main`, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PCs.
