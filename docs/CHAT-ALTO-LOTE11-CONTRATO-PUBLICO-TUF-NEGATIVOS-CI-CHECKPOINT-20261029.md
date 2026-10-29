# GERENCIADOR FINAL — CHAT ALTO — Lote 11: checkpoint de implementação e CI Windows
**29/10/2026 · concluído como protótipo inerte e validado em CI · G1–G8 BLOCKED (0/8)**

## 1. Controle de escopo

- Repositório: walissonminel-1991/gerenciador-final.
- Branch exclusiva do CHAT ALTO: experimento-g5-ipc-chat-alto-20261029.
- HEAD de entrada: 25f38b3bbf70f7e2c92f1114dfccb54d8206de86.
- Documento-base relido naquele commit: docs/CHAT-ALTO-LOTE10-REORIENTACAO-AUTOATUALIZADOR-PUBLICO-CUSTO-ZERO-20261029.md.
- Commit técnico implementado: a2b60b680d30d4ebee3afaa19e14c4f7a83d878d; exatamente cinco arquivos novos e nenhum arquivo preexistente modificado.
- PR #65: aberta, Draft, sem merge, branch work-integracao-final-20261027 em ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc antes e depois da CI.
- Não houve WORK; main, PR #65, BETA .66, Cloudflare, Worker/D1, domínio, consentimento, telemetria, infraestrutura, PCs pessoais e dados reais permanecem preservados.
- Não foram criados segredo, certificado de produção, chave privada operacional, serviço, endpoint, cadastro, inscrição de máquina, instalador, release, deploy ou atualização operacional.

## 2. Artefatos técnicos

Novo projeto independente: prototipos/autoatualizador-seguro/m3-l11-public/L11Public.csproj.

1. L11PublicContract.cs.txt: contrato público exclusivamente declarativo. Depende da verificação TUF assinada em fixture Stage11 e vínculo Stage3 (não substitui o verificador TUF). Congela bytes/digests de vínculo e ZIP, exige schema fechado, canal, arquitetura, sequência, versão, target/hash e único arquivo/ação inertes. Rejeita qualquer comando ou caminho fora do escopo. Retorna apenas RecognizedInertFixtureOnly, nunca capacidade de selecionar, confirmar ou instalar.
2. L11PublicTests.cs.txt: runner com 17 cenários. Usa chaves de laboratório e pacote de texto inerte existente em Stage11/Stage3, operações e Stage2 de fixture. Garante que seletor anterior e marcadores de dados de teste sejam preservados em todos os cenários.
3. README.md: limites e riscos do experimento.
4. .github/workflows/autoatualizador-m3-l11-publico-inerte.yml: CI Windows 2022, SDK .NET 10.0.402, Python de fixture 3.13, bibliotecas fixadas pelo Stage11, fonte TUF pinada, arquivos temporários fora da distribuição e checagem de Compile para não incluir fontes do protótipo no GF.

O host updates.gf.invalid é intencionalmente inexistente e só entra numa verificação de URL por função pura. Nenhuma conexão HTTP/HTTPS foi disparada pelo protótipo. As dependências de build foram obtidas pela CI de maneira isolada, como nos lotes anteriores.

## 3. Evidências da CI

- Run: https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40934244768
- HEAD executado: a2b60b680d30d4ebee3afaa19e14c4f7a83d878d.
- Job public-contract-negative-tests: SUCCESS.
- Log de relatório: GF_L11_FINAL_REPORT passed=17 failed=0 gates=0.
- Log de build: Build succeeded; 0 Warning(s), 0 Error(s).
- O workflow exigiu exatamente 17 aprovações, ausência de flags operacionais indevidas e ausência de fontes L11 na compilação do projeto principal. O job terminou com sucesso.

| Caso | Expectativa demonstrada |
|---|---|
| L11-01 | Cadeia TUF Stage11 assinada de teste e binding Stage3 reconhecidos, resultado SOMENTE inerte |
| L11-02 | Byte de ZIP pós-vínculo alterado: rejeitado |
| L11-03 | ManifestSha do binding Stage3 alterado: rejeitado |
| L11-04 | Campo extra de comando remoto: rejeitado |
| L11-05 | Operação remota de excluir diretório: rejeitada |
| L11-06 | Path traversal: rejeitado |
| L11-07 | Caminho absoluto de Documents: rejeitado |
| L11-08 | Nome de arquivo com Alternate Data Stream: rejeitado textual, não é teste de NTFS real |
| L11-09 | Canal de outro aplicativo: rejeitado |
| L11-10 | Arquitetura errada: rejeitada |
| L11-11 | Piso local preservado impede reaceitar a mesma versão |
| L11-12 | Versão diferente da assinada: rejeitada |
| L11-13 | SHA advisory diferente do pacote autenticado: rejeitado |
| L11-14 | JSON advisory acima do limite: rejeitado |
| L11-15 | URLs de transporte fora da allowlist: rejeitadas sem fazer conexão |
| L11-16 | Contraexemplo: restauração do piso local antigo permite reconsiderar release previamente recusada |
| L11-17 | Mutação de metadados de targets antes da prova TUF: verificação falha |

Todos os 17 casos registraram PASS nos logs; PASS em L11-16 é a **demonstração de uma vulnerabilidade residual do modelo**, não sua eliminação.

## 4. Distinção entre demonstração, premissa e pendência

**D (demonstrado):** rejeição segura de entradas hostis de fixture, encadeamento com TUF de teste existente, ausência de caminho de execução no protótipo, retenção do seletor local e compilação isolada com CI aprovada. Nenhum novo código operacional do GF.

**H (premissas):** o verificador Stage11/Stage3 que fornece prova é honesto; a distribuição inicial da raiz é confiável; as chaves são apenas de teste; a pasta de fixture/runner é controlada. O L11 não é validação normativa independente de TUF.

**P (pendentes):**
- G1: root/binary bootstrap autêntico, proteção contra rollback após restauração integral do disco (reproduzida como falha no L11-16), relógio restaurado, continuidade pública sem cadastro e possibilidade de operador/servidor comprometido.
- G2: adjudicação independente das duas divergências de referência Python/.NET na Etapa 12, rotatividade de papéis e política de custódia, revisor TUF e responsável formal de manutenção D6-02.
- G3/G4: pacote real GF, executáveis, DLLs, proveniência de build/release, runtime e política de assinatura; assinatura correta não prova ausência de malware no código assinado.
- G5/G6: identidade de processo e candidato, READY genuíno, permissões mínimas e eventuais helpers.
- G8: timeouts absolutos, I/O tardio, estados UNKNOWN, NTFS, reparse/junction real, ACL, TOCTOU, crash, reboot, power loss, rollback idempotente.
- Transporte público real: HTTPS, redirecionamentos, hospedagem/cotas gratuitas, cache, freeze, expiração e offline. A política de URI L11 é somente um modelo local.
- Segurança dos arquivos pessoais em atualização real, Windows em Program Files, portable sem admin e proteção contra binário malicioso validamente assinado.
- Revisão independente e homologação em máquinas descartáveis, explicitamente autorizadas no futuro.

## 5. Conclusão e próximo passo

O Lote 11 adicionou implementação **experimental e automatizada** à linha do CHAT ALTO, sem alterar o produto ou oferecer atualização instalável. Alcançou 17/17 casos de CI, 0 warnings, 0 errors, demonstrando o limite de restauração integral sem fingir que G1 foi resolvido.

Próximo Lote recomendado: estudar modelo inerte de referência pública global de continuidade sem cadastro, sob atacante de replay, perda de conexão, relógio incorreto e operador comprometido, com cenários negativos que não declarem antirollback absoluto; em paralelo, projetar próximos testes NTFS/ACL em ambiente descartável autorizado, sem integração. WORK somente num marco de revisão independente após evidência substantiva.

**G1–G8 BLOCKED (0/8), sem merge, release, deploy, WORK ou homologação física.**
