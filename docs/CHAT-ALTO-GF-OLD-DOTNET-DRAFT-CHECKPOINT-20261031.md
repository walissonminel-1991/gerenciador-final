# GERENCIADOR FINAL — CHECKPOINT GF OLD inicia download real de Draft via .NET

**Data:** 31/10/2026 UTC (Fortaleza UTC−3). **Natureza:** Windows descartável, experimental; SEM autoatualização operacional.

## Branch, provas e origem

- Repositório `walissonminel-1991/gerenciador-final`; branch exclusiva `experimento-g5-ipc-chat-alto-20261029`.
- SHA da candidata executada: `076e68ba15abe801b57f2185b73424005d59e093`.
- CI atual: [run 41167267745](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41167267745), job Windows `gf-old-initiated-draft-real`; ao registrar este checkpoint o job ainda estava **in_progress** na etapa de matriz L18. Nunca inferir status final a partir de passos anteriores.
- Draft existente, não publicada: Release ID `429695537`, tag `gf-e2e-c5057bbbaf1d`; confirmado `draft=true`, `published_at=null`.
- Pacote imutável de homologação `gf-e2e-new-win-x64.zip`: asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto assinado `GF-LAB-REAL-PACKAGE-MANIFEST.json`: asset ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Os assets de roles TUF de TESTE foram renovados **dentro da mesma Draft**, sem tocar no ZIP/manifesto: snapshot ID `694316573`, targets ID `694316569`, timestamp ID `694316570`.
- A renovação usa seeds de TESTE publicadas no repositório; não equivale a confiança de produção, antirollback independente ou signing operacional.

## Prova positiva nesta candidata (por etapa GitHub Actions)

1. Exclusion guard confirma `GfOldDraftAcquire.cs.txt` ausente do compile do produto normal, presente exatamente uma vez na compilação `GfE2eLab=true`.
2. GF OLD real `GerenciadorFinal.exe` compila com versão `5.13.11.65`, binário win-x64 completo.
3. O **próprio GF OLD**, no argumento experimental `--gf-e2e-draft-acquire`, usa `System.Net.Http.HttpClient` para consultar a REST Release por ID e baixar os cinco assets: etapa `O proprio GF OLD consulta GitHub Draft e baixa os cinco assets via HttpClient` **SUCCESS** na CI 41167267745.
4. Python-TUF de laboratório autentica os arquivos NEW, descompacta o pacote real baixado e comprova versão `5.13.11.66`: etapa `Revalidar TUF da Draft NEW baixada pelo GF` **SUCCESS** na mesma CI.
5. No checkpoint, etapa `Mesmo coordenador C# L18 executa OLD e NEW vindos do GitHub` **IN_PROGRESS**, não concluída. Consultar run/job/log/JSON antes de declarar aprovação.
6. Testes adversariais da execução atual são posteriores à matriz L18; **não declarar aprovados antes de o job finalizar**.

## Diferenças entre os dois OLDs e vínculo de confiança

O GF OLD da rodada atual inclui **uma entrada compilada apenas para laboratório** que consulta e baixa a Draft. Sua identidade de bytes difere do OLD usado ao assinar originalmente o manifesto da Draft. Assim, na validação da Draft se autentica o NEW, e **o coordenador L18 assina/verifica em separado o OLD real desta rodada e o NEW já autenticado**, usando o mesmo estado de laboratório, pins, diário e seletor C#. Não aceitar o antigo `old_files` da Draft como autorização para a variante atual. Nenhuma segunda máquina de transações.

A descoberta e o download são executados por um subcomando do próprio GF OLD, não pelo `gh release download`. Isso ainda **não** significa verificação automática na abertura normal da interface, nem credencial privada disponível para usuários finais: a Draft só é acessível à CI por token efêmero de curta duração com permissão de repositório. O token não pode ser incorporado ao produto distribuído.

## Histórico de contraexemplos desta rodada

- CI 41166406957: primeira tentativa de download .NET pelo OLD retornou exit 8; diagnóstico posterior identificou `METADATA_HTTP_STATUS_404`.
- CI 41166576350: diagnóstico 404 confirmado na rota por tag com token read.
- CI 41166747762: tentativa de examinar IDs de assets via `gh release view --json assets` bloqueada antes de mutação; trocou para REST.
- CI 41166940541: REST por tag retornou 404; trocou para GET da Release ID `429695537`.
- CI 41167091037: REST por ID acessou a Draft e baixou manifesto, mas a renovação falhou por `NameError: digest`; corrigido com `module.digest`.
- CI 41167267745: etapas de download .NET pelo próprio OLD e autenticação NEW passaram. Não misturar esse sucesso de etapas com conclusão final da CI.
- Não publicar Draft, não criar nova, não mudar ZIP/manifesto, não levar token aos PCs pessoais.

## Contrato rígido

**Objetivo único:** GF antigo detecta a versão nova no GitHub, baixa, autentica, usa L18 para encerrar OLD, ativar NEW e observar interface, com dados preservados; depois negativos adulteração, interrupção e encerramento inconclusivo.

**Persistem:** `main` intocada, PR #65 Draft intocada, BETA distribuída intocada, Cloudflare/telemetria/PCs pessoais intocados, nenhum merge/deploy/publicação. H1/H2/H3 e TOCTOU same-user não comprovados; HOLD/UnknownBlocked nunca autoriza recuperação cega; G1–G8 continuam bloqueados. WORK não deve ser consumido até a candidata de ponta a ponta ou bloqueio crítico fundamentado.

**Próxima ação sem duplicar trabalho:** consultar CI 41167267745 por conclusão e logs. Se L18+negativos passarem, registrar SHA/CI e seguir estritamente para tornar a detecção disparada no fluxo natural de startup da build experimental, com o mesmo cliente .NET e L18. Se falhar, corrigir somente o defeito comprovado no ponto de falha, sem fases/lotes paralelos.
