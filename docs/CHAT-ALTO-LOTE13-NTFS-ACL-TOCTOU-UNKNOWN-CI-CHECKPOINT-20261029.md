# GERENCIADOR FINAL — CHAT ALTO — LOTE 13
## NTFS, ACL, reparse, TOCTOU e recuperação UNKNOWN — checkpoint após CI
**29/10/2026 · Protótipo INERTE · G1–G8 BLOCKED (0/8)**

### 1. Baseline e proteção

- Repo walissonminel-1991/gerenciador-final, branch exclusiva experimento-g5-ipc-chat-alto-20261029.
- HEAD de entrada 6dba3b6332d90ddc45784338c3687187e6c94b7e, diff idêntico, L12 lido e conferido.
- PR #65 aberta, Draft, sem merge, work-integracao-final-20261027 no HEAD ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc.
- Código técnico L13 no commit 40f1db06a80ae0c00447b234d4e2ff59f3531850: cinco arquivos NOVOS, zero alterações em arquivos preexistentes.
- Sem WORK; main, PR #65, BETA .66, Cloudflare, Worker/D1, domínio, telemetria, consentimento, PCs pessoais e credenciais preservados.
- Sem instalador, helper privilegiado, deploy, release, chave operacional, serviço, rede ou build/execução do GF principal.

### 2. Implementação isolada

Arquivos criados no commit técnico:

1. prototipos/autoatualizador-seguro/m3-l13-ntfs/L13Ntfs.csproj — .NET 10 standalone, .cs.txt explicitamente enumerados, EnableDefaultItems=false, IsPublishable=false.
2. L13NtfsBoundary.cs.txt — política pura para caminho FIXO staging/a1b2c3d4.fixture.txt, pedido de elevação recusado; pré-verificação de reparse, depois WindowsFileGuard com handle NOFOLLOW, pin da cadeia de ancestrais, identidade NTFS e inspeção do descritor ACL. Resultado somente InertFileObservedBlocked, sem função que autorize selecionar, confirmar ou instalar.
3. L13NtfsTests.cs.txt — 21 casos de fixture Windows real, sintéticos onde indicado, com seletor e marcadores pessoais fictícios preservados.
4. README.md — contrato experimental, limitações, sem direitos operacionais.
5. .github/workflows/autoatualizador-m3-l13-ntfs-inerte.yml — checkout sem credenciais persistentes, windows-2022, SDK 10.0.402 e test runner totalmente isolado em TEMP; confere compile do produto sem vazamento dos novos fontes.

O L13PendingJournal grava apenas dois recibos JSON locais de fixture, com FileMode.CreateNew, WriteThrough e Flush(true); prepara idempotentemente e mantém fase pending ou unknown. O estado UNKNOWN continua bloqueado mesmo após reapertura ou conclusão tardia de tarefa simulada. Não existe transação multiarquivo nem troca de seletor (mesmo o de fixture).

### 3. CI: evidência rastreável

- GitHub Actions: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40939257601
- HEAD executado: 40f1db06a80ae0c00447b234d4e2ff59f3531850.
- Job ntfs-fixture-negative-tests: SUCCESS.
- Build succeeded com 0 Warning(s), 0 Error(s).
- Report GF_L13_FINAL_REPORT: passed=21, failed=0.
- A CI exige explicitamente os 21 PASS, flags de provas reais de links, hardlink, timeout e reabertura do journal, e flags falsas para propriedades não testadas.

| Cenário | Resultado verificado |
|---|---|
| L13-01 | Pin e leitura de handle real Windows NTFS, sem alterar versão antiga |
| L13-02/03/04/05 | Rejeita path traversal, caminho absoluto de Documents simulado, ADS, nome reservado e subpasta |
| L13-06 | Pedido textual de elevação rejeitado; NÃO demonstra token não-admin real |
| L13-07/08 | Symlink de diretório e symlink de arquivo realmente criados no NTFS de TEMP e recusados |
| L13-09 | Hardlink realmente criado em NTFS; WindowsFileGuard recusa link count maior que um |
| L13-10/11 | Windows SDDL de laboratório: escrita por Everyone rejeitada; escrita por SYSTEM aceita |
| L13-12 | Handle aberto e pinado impede rename concorrente do arquivo observado |
| L13-13 | Após soltar o pin, substituição muda identidade/hash; revalidação rejeita |
| L13-14 | Falta do alvo causa falha segura, sem criação implícita |
| L13-15 | ReadOnly real impede gravação da fixture, sem mudar o conteúdo |
| L13-16/17 | Recibo pending idempotente e conflitos de tx/digest recusados |
| L13-18/19 | UNKNOWN preservado após reabertura e tx errado recusado |
| L13-20 | Timeout da espera NÃO cancela tarefa futura que grava fixture; UNKNOWN segue bloqueado |
| L13-21 | Seletor anterior e marcadores de dados fictícios preservados |

PASS indica cumprimento da asserção delimitada. Não indica robustez geral do NTFS, atomicidade da instalação ou segurança do Windows fora desse ensaio.

### 4. Provas, premissas e pendências

**Demonstrado (D):** teste real em Windows runner de symlinks/hardlink e compartilhamento de handle; verificação de política de ACL via RawSecurityDescriptor SDDL de laboratório; observação de I/O tardio e persistência de UNKNOWN entre objetos; aprovação CI e isolamento de compilação.

**Hipóteses (H):** máquina Windows descartável, diretório temporário confiável e com marcador; WindowsFileGuard preexistente não foi auditado independentemente; adversário sem controle do kernel/administrador; Flush(true) não substitui teste físico de power loss.

**PENDENTE (P):** não houve mutação REAL de ACL, nem verificação de token não-administrador, nem criação explícita de junction (houve symlink), nem acesso a Program Files, nem substituição de árvores completas, nem crash real de processo do atualizador, nem power loss físico, nem trabalho com drivers, antivírus, BETA ou GF instalado. G1 (restore integral), G2 (TUF e D6-02), G5/G6 (processo/READY), G8 (NTFS/I-O/timeout/power loss real) permanecem BLOCKED.

### 5. Consequência arquitetural e próximos passos

Pin e revalidação impedem algumas mutações locais, mas a segurança depende da identidade dos bytes até a fronteira real da ativação. Liberação de handle antes da troca abre janela TOCTOU. Nenhum teste desta rodada oferece atomicidade transacional de árvore ou garantia de recuperar versão íntegra após falha elétrica. A escrita tardia após WaitAsync vencido evidencia que cancelamento da espera NÃO significa que o I/O terminou.

Próxima frente de melhor ganho prático: modelar apenas com textos inertes em TEMP duas versões lado a lado, seletor fictício, cortes de processo antes/depois da promoção e retomada idempotente em uma CI Windows. Depois, a revisão WORK pontual seria mais útil quando existir fluxo técnico integrado sólido; não gastar WORK repetindo fixtures equivalentes.

**STATUS FINAL: Lote 13 experimental concluído com 21/21 testes em CI; G1–G8 = BLOCKED (0/8). Sem merge, release, deploy, serviço, alteração do GF ou WORK.**
