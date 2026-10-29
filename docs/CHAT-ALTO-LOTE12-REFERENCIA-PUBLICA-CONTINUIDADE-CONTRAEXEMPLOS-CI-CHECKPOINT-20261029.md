# GERENCIADOR FINAL — CHAT ALTO — LOTE 12
## Referência pública de continuidade sem cadastro: protótipo inerte, contraexemplos e CI Windows
**29/10/2026 · CHECKPOINT TÉCNICO · G1–G8 BLOCKED (0/8)**

### 1. Preparação, estado e preservação

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva do CHAT ALTO: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial conferido, idêntico ao checkpoint L11: `046173db808429d9c48672c30591ed6333ae3ace`; sem alterações posteriores antes do trabalho.
- Checkpoint L11 lido: `docs/CHAT-ALTO-LOTE11-CONTRATO-PUBLICO-TUF-NEGATIVOS-CI-CHECKPOINT-20261029.md` (17/17, CI 40934244768). Reutilizadas as implementações Stage11/Stage3 e admissão L11, sem alterar seus arquivos.
- Commit técnico L12: **`2d45b898dd1f6fb84c0bdb87aac807be4775a8ab`**.
- Comparação GitHub pós-implementação: **exatamente cinco arquivos ADICIONADOS**, nenhum arquivo preexistente modificado. Exclusivamente 3 arquivos do protótipo, README e workflow Windows.
- PR #65 segue aberta, Draft, não mesclada; branch `work-integracao-final-20261027`, SHA imutável de referência `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc` conferido antes/depois. `main`, BETA .66, Cloudflare Worker/D1, domínio, telemetria, consentimento e PCs pessoais não foram modificados. **WORK não utilizado**.
- Não há novo serviço, endpoint, rede, cliente real, conta, cadastro, segredo de produção, release, deploy, driver, instalador ou execução do produto.

### 2. Arquitetura avaliada: autenticação não é frescor

**Hipótese A — checkpoint estático assinado, público e sem cadastro:** cópia autenticada por chave de laboratório separada do TUF editorial, com `repository/channel`, `PublicationSequence`, `MinimumSequence`, `RootFloor`, hash do alvo e digest dos bytes completos do binding Stage3. Verifica criptograficamente o conteúdo, mas uma publicação estática pode ser reapresentada até expiração; restaurar também o relógio e o histórico local remove as proteções temporais/de piso do cliente. **Sem âncora externa fresca, G1 integral não é resolvido**.

**Hipótese B — resposta a desafio:** emula assinatura RSA-PSS de um nonce aleatório por signatário online em memória, usando chave privada temporária apenas do teste e chave pública pinada no verificador. O nonce recusa replay da *mesma assinatura* sob desafio diferente. **NÃO comprova monotonicidade de um signatário malicioso:** este pode assinar uma regressão com nonce novo, apresentar dois checkpoints conflitantes a clientes diferentes e manter o replay disfarçado. Exigiria backend de assinatura vivo para operar; GitHub Releases ou armazenamento estático não oferecem resposta nova autenticada por nonce. Não foi criado backend nem escolhido fornecedor.

**Observação crítica:** ambas as modalidades devolvem exclusivamente `AuthenticatedStaticOnly` ou `AuthenticatedChallengeOnly`: **nunca** `CanSelect`, `Ready`, `Permit`, instalação ou `Confirm`. Elas não substituem TUF, o verificador inicial confiável, um relógio externo confiável ou custódia e monitor antifork independentes.

### 3. Implementação e isolamento

Novos arquivos:
1. `prototipos/autoatualizador-seguro/m3-l12-public/L12Public.csproj` — `net10.0`, `EnableDefaultItems=false`, fontes explicitamente listadas e novas fontes `.cs.txt` excluídas do GF principal, dependências de fixture Stage11 já pinadas, `IsPublishable=false`.
2. `prototipos/autoatualizador-seguro/m3-l12-public/L12PublicContinuity.cs.txt` — DTO tipado e canonizado; valida assinatura RSA-PSS SHA256, escopo, tamanho, digest Stage3/target, prazo máximo de fixture, nonce, cabeçalho local e piso global. Não contém nenhuma rotina de HTTP, shell ou escrita no Windows.
3. `prototipos/autoatualizador-seguro/m3-l12-public/L12PublicTests.cs.txt` — runner isolado; verificação da cadeia assinada TUF Stage11 e do vínculo Stage3/ZIP + admissão inerte L11 antes dos 28 ensaios de checkpoint.
4. `prototipos/autoatualizador-seguro/m3-l12-public/README.md` — premissas e limitações de autoridade pública, tempo e signatário comprometido.
5. `.github/workflows/autoatualizador-m3-l12-referencia-publica-inerte.yml` — CI Windows 2022, checkout sem credenciais persistentes, SDK 10.0.402, fixtures de teste com Python fixado e isolado, relatório fail-closed, conferência de vazamento de Compile para o produto.

**Chave do ensaio:** RSA-PSS gerada em RAM dentro do runner e destruída ao encerrar o teste. Não é chave de release TUF, não é autoridade real e não foi exportada ao repositório, logs ou distribuição. As chaves Stage11 usadas são exclusivamente de fixture. O ensaio utiliza uma única autoridade pública simulada; não é um protocolo multioperador nem um serviço.

### 4. Resultados comprovados — CI Windows

- Run GitHub Actions: **[40937782769](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40937782769)**.
- HEAD da execução: `2d45b898dd1f6fb84c0bdb87aac807be4775a8ab`.
- Job `public-contract-negative-tests`: **SUCCESS**.
- Build: **0 warnings, 0 errors**.
- Runner: `GF_L12_FINAL_REPORT passed=28 failed=0 gates=0`.
- O workflow exigiu 28 aprovações e bloqueou qualquer relatório que afirmasse uso de rede, chave operacional, compilação/execução do GF, instalação, power loss físico, relógio confiável ou G1 resolvido. `dotnet msbuild -getItem:Compile` não encontrou inclusão dos novos fontes L12 no produto.

**Famílias dos 28 testes:**
- **L12-01 a 05:** assinatura estática observacional, adulteração de assinatura/bytes, chave errada, ausência de resposta.
- **L12-06 a 09:** outro repositório/canal e alteração de hash de alvo/binding Stage3.
- **L12-10 a 12:** piso local íntegro, piso público global de release e piso de raiz.
- **L12-13 a 16:** expiração, `not-before`, janela excessiva, distinção estático ≠ desafio.
- **L12-17 a 20:** desafio assinado em laboratório, nonce reutilizado, rollback do checkpoint e fork com último head local preservado.
- **L12-21 a 22:** **contraexemplos esperados que foram REPRODUZIDOS**: relógio restaurado permite reconhecer referência estática já antiga; restauração integral do piso local permite reconsiderar release previamente bloqueada.
- **L12-23:** se houver **emissor honesto e atualizado** com piso global 4, modelo recusa candidato anterior 2. É prova condicional, não de disponibilidade/honestidade da autoridade real.
- **L12-24 a 25:** **contraexemplos reproduzidos**: autoridade maliciosa assina regressão com nonce novo e duas visões conflitantes. O verificador reconhece assinaturas válidas sem possuir monitor independente; nenhum update é autorizado.
- **L12-26 a 28:** campo extra de comando, modo remoto `execute` e conteúdo assinado não canônico rejeitados.

**Atenção à métrica:** PASS em L12-21/22/24/25 indica que a falha teórica foi reproduzida, **não** que a proteção foi implementada. Não há contagem de gates avançados.

### 5. Matriz demonstrado / hipótese / pendente

**D — efetivamente demonstrado:** em ambiente CI Windows descartável, projeto compilou sem warning/erro, integridade TUF+Stage3 test-only herdada foi exercitada, as 28 expectativas foram satisfeitas, entradas não permitidas foram recusadas, e ataques de restore/clock/operador/visões conflitantes foram reproduzidos como limitações. Seletor e dados inertes permaneceram preservados; não houve contato de rede nem execução do GF.

**H — premissas:** distribuição e verificador inicial íntegros; RSA de fixture sob autoridade honesta apenas nos cenários condicionais; relógio do Windows fiel apenas onde explicitado; signatário online hipotético; input Stage11/Stage3 de testes e sandbox descartável. As assinaturas RSA-PSS experimentais não constituem formato/limiar operacional. Não há autenticidade de uma autoridade externa real.

**P — ainda por demonstrar/aceitar:** confiança inicial e pin independente; rotação/revogação de chave pública e versões de verificadores antigos; antirrollback após restauração integral; relógio confiável; autenticidade/frescor de publicação gratuita sem backend; resposta a A5 com monitor/admin independente; TUF normativo G2 e D6-02; assinatura operacional e cadeia de supply chain; autorização de arquivos próprios/privilegios mínimos; IPC G5/G6, G8, NTFS/ACL/TOCTOU, energia real; homologação física e avaliação humana qualificada.

### 6. Decisão de arquitetura e custo zero

1. **TUF público e distribuidor estático** pode ser a base simples, gratuita, sem conta e sem identificação por instalação. Autentica conteúdo sob a raiz inicial confiável e trata rollback/freeze com estado local + expiração dentro das premissas TUF; NÃO garante rollback após restauração integral do disco/relógio.
2. Um **checkpoint global estático adicional** só acrescenta vantagem real se tiver custódia/assinatura e verificabilidade independentes das que já existem no TUF. Dois arquivos no mesmo GitHub ou dois serviços sob a mesma conta não são independência contra comprometimento do operador.
3. Um **signatário público de desafio** evita replay de resposta literal, mas cria obrigação de serviço online, custódia da chave, disponibilidade e resposta a incidentes. Mesmo sem cadastro e potencialmente sem custo financeiro inicial, **não resolve autoridade comprometida** e não há autorização para implantar.
4. Um **log de transparência** precisa de prova de consistência, checkpoint anterior ou witness/monitor independente, e tratamento de split view; uma URL pública sozinha não garante frescor nem antirrollback. Isso não é requisito aprovado.
5. **Recomendação:** não estender indefinidamente o G1 com novas fixtures equivalentes. Manter duas opções para decisão explícita do mantenedor: (A) escopo público TUF com risco residual integral formalmente aceito, sem proclamar G1 fechado; (B) exigência de monotonicidade mesmo após restore, implicando fonte de continuidade/frescor realmente independente, manutenção e custos operacionais avaliados. Nenhuma opção está homologada nem autoriza ativação.

### 7. Próximo marco

Priorizar testes de restrição do updater aos componentes autorizados do GF (NTFS, reparse/junction/ACL e cancelamento/timeout em Windows descartável) **ou** uma revisão humana de conformidade TUF/G2 e mantenibilidade antes de considerar incorporação. Não publicar código executável, não criar serviço nem usar WORK sem nova decisão do mantenedor. O ciclo seguinte deve aumentar evidência de uma barreira operacional nova, não contar casos adicionais como fechamento de G1.

### 8. Referências

- [TUF Specification 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/): §5.1, timestamp e freeze/rollback, trust root, §5.3.11 da invalidação conjunta de timestamp/snapshot na rotação.
- [TUF Metadata Roles](https://theupdateframework.io/docs/metadata/): timestamp, snapshot e targets.
- [Sigstore Rekor Security Model](https://docs.sigstore.dev/about/security/): limites de verificabilidade e dependência de monitor.
- [Rekor specification — consistency and split view](https://github.com/sigstore/architecture-docs/blob/main/rekor-spec.md): consistência, persistência de checkpoint, monitor e visões divergentes.
- Evidência primária: `docs/CHAT-ALTO-LOTE11-CONTRATO-PUBLICO-TUF-NEGATIVOS-CI-CHECKPOINT-20261029.md` e CI 40937782769.

**STATUS FINAL: Lote 12 experimental concluído; CI 28/28 aprovada; G1–G8 BLOCKED (0/8). WORK não utilizado; PR #65 e main preservadas.**
