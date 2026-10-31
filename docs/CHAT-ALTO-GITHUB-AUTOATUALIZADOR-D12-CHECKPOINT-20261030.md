# GERENCIADOR FINAL — CHAT ALTO — D1.2: processos distintos, vínculo de Release e contraexemplo H1

**Escopo:** somente experimento Windows descartável. D1.2 não instala, baixa, executa nem autoriza pacote real e não fecha a Fase 1.

## 1. Estado verificável

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch autorizada para escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `e7fba77956673ed23d94a24258e0d90c0560ca56`.
- **SHA técnico validado D1.2: `575f271951c89acc357d05da1ddbb2059597bdc2`**.
- [CI 41150770479](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150770479), job `124386486905`, tentativa 1: `completed/success`, `head_sha=575f271951c89acc357d05da1ddbb2059597bdc2`.
- D0: [CI 41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551) SUCCESS, 25 testes. D1: [CI 41148601868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148601868) SUCCESS, 33 adversariais. D1.1: [CI 41150329284](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41150329284) SUCCESS, 14 contraprovas e 2 gerações de metadados.
- Não foi feita nova regressão histórica de 29 casos porque a mudança ficou confinada a um diretório novo de pesquisa e workflow próprio. A homologação transacional histórica continua uma exigência distinta.

## 2. Implementação

Novo diretório `prototipos/autoatualizador-seguro/github-crossprocess-lab/`:
- `d12_crossprocess_lab.py`: controlador Python gera fixtures TUF com as **sementes públicas e determinísticas exclusivamente de TESTE** já existentes no repositório. Executa subprocessos Python **realmente distintos** (21 PIDs de trabalhadores observados), e cada trabalhador chama o executável C# isolado que compila a **mesma fonte D0** do laboratório anterior. Não copia nem modifica a fonte principal do GF.
- Dois trabalhadores sucessivos observam Root/Timestamp/Snapshot/Targets nas versões `[1,1,1,1]` e `[1,2,2,2]`, compartilhando diretório de metadados confiáveis com acesso normal em `RUNNER_TEMP`.
- O vínculo D0/D1.2 compara a tag da Release simulada produzida pelo parser C# D0 com o manifesto autenticado por Python-TUF e seu contrato D1. O inventário consultivo da Release deve conter exatamente o único asset esperado e tamanho consistente; o formato de URL oficial é conferido, mas **a URL não é autoridade criptográfica**. Nenhuma URL é aberta e nenhum objeto remoto é adquirido. O conteúdo do suposto arquivo .zip é texto inerte, **não um ZIP real**.
- Cada contraprova utiliza uma cópia independente do histórico pós-v2 para que um refresh abortado não contamine o caso seguinte.
- `.github/workflows/gf-github-d12-crossprocess-lab.yml`: CI Windows descartável, permissões contents:read, fontes C# experimentais fora do glob do projeto principal, sem release, deploy, download ou instalação.
- `README.md` no diretório novo documenta limites e política de contenção.

## 3. Evidência CI

O log `GF_D12_RESULT` na CI 41150770479 registra:
```json
{
  "schema":"gf-github-d12-crossprocess/1",
  "status":"passed",
  "independent_windows_processes":21,
  "trusted_generations_across_processes":2,
  "intact_history_negative_controls":18,
  "full_history_rewind_accepted":true,
  "rewind_is_security_counterexample":true,
  "same_user_rollback_protection_proven":false,
  "cryptographic_production_authority":false,
  "process_crash_durability_proven":false,
  "test_root_public":true,
  "real_network_requests":0,
  "real_executable_downloads":0,
  "product_modified":false,
  "operational_ready":false,
  "automatic_recovery_authorized":false
}
```

Os 18 casos recusados: replay de Timestamp antigo com cache v2 intacto, Snapshot e Targets de v1 misturados com v3, tag D0 incompatível, Release marcada draft, prerelease, ausência ou duplicação de asset, nome de asset falso, tamanho falso e booleano, URL externa, manifesto de v1 com metadados novos, manifesto assinado de repositório ou arquitetura incorretos, downgrade de versão instalada, Timestamp v3 expirado e alteração de Targets.

## 4. Contraexemplo principal — não confundir com êxito de defesa

1. O processo de teste aceita Root/Timestamp/Snapshot/Targets v1 do laboratório e guarda cópia dos seus arquivos de confiança.
2. Um processo diferente autentica v2, fazendo o histórico local avançar normalmente.
3. Com o histórico v2 intacto, a tentativa de reapresentar v1 é **recusada**.
4. O laboratório substitui **todo o diretório de confiança** por sua cópia antiga v1, sem alterar a versão instalada de referência (continua v5.13.11.65, pois nenhuma instalação ocorreu).
5. Outro processo aceita novamente os metadados v1 e o manifesto v5.13.11.66.

Este último resultado é um **ataque demonstrado contra a hipótese de proteção de rollback integral no modelo de laboratório**, não uma aprovação para uso real. `H1` permanece **não comprovado**. Não se pode inferir autoridade imutável do mesmo usuário, garantia externa ao disco ou recuperação automática a partir deste cache.

## 5. Limites rigorosos

- Os subprocessos reiniciam normalmente; não há interrupção forçada de processo, crash, corte de energia, fsync auditado ou teste de recuperação fria durável.
- O laboratório não impede que o mesmo usuário altere os arquivos, substitua o executável de teste ou assine novos metadados usando as sementes públicas. Nenhuma raiz privada de produção foi criada. As chaves de teste não autorizam software real.
- D0 consulta **JSON de Releases fictícias**, nunca o GitHub durante a CI. A Release e o manifesto não são amarrados a um ativo publicado real. A identidade dos bytes efetivamente executados e a validade de uma Release real não foram provadas.
- A associação de URL/asset é apenas um filtro de metadados não confiáveis; D2 deverá autenticar bytes adquiridos, limites e proteção contra alteração entre verificação e execução, sem confiar somente na URL.
- Não se demonstra H2, H3, rotatividade segura de raízes/epochs de produção, persistência à prova de rollback, READY ou G1–G8.

## 6. Recomendação de próxima ação

**D2 inerte de aquisição/preparação controlada** é possível como experimento sem habilitar instalação: reutilizar o manifesto TUF autenticado, adquirir somente bytes **inertes** através de fetcher artificial ou endpoint bloqueado, verificar tamanho/hash da aquisição, limitar caminhos e provar que não há TOCTOU de fonte ou gravação fora do laboratório. Não declarar D2 pronta para produção até resolver a confiança de origem em produção e a lacuna H1 (ou fixar uma política operacional conservadora que a reconheça sem fingir proteção).

**WORK só após existir uma integração criptográfica/transacional suficientemente significativa e CI verde para tentar refutar suas garantias; não usar WORK para repetir estes testes isolados.**

## 7. Invariantes congelados

**Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8 e zero recuperações automáticas. HOLD é contenção, não recuperação.** Respeitar Opção B, B+A portátil, TUF/NTFS/TOCTOU, privacidade. Sem mudanças em main, PR #65 Draft, BETA .66, GF distribuído, backend, Cloudflare, credenciais, políticas Windows; sem merge, Release ou deploy.
