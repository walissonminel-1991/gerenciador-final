# CHAT ALTO — Gerenciador Final — Verificador GitHub Releases mínimo

**Data documental:** 30/10/2026. **Natureza:** incremento funcional experimental de consulta de versão, sem update operacional.

## 1. Base e escopo

- Repo `walissonminel-1991/gerenciador-final`.
- Única branch escrita: `experimento-g5-ipc-chat-alto-20261029`.
- Base: `427fc6c7a82d559f2cbaa820255a34a57570bd64` (checkpoint DarkProgramList).
- **SHA técnico validado deste incremento:** `3192f427da3e9da2a16dec77c315a9b5af6518f0`.
- Política de etapas: `docs/CHAT-ALTO-DIRECAO-AUTOATUALIZADOR-GITHUB-MINIMO-20261030.md`.
- A CI transacional anteriormente pendente [41145899676](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41145899676) foi consultada e consta **completed/success**, SHA `64fd93d5b49024f42f783906a9fcfd33aebf8110`.
- A consulta oficial `GET /repos/walissonminel-1991/gerenciador-final/releases` retornou **lista vazia**; `releases/latest` retornou **404**. Não foi criada nenhuma Release para esta missão.

## 2. Implementação

`prototipos/autoatualizador-seguro/github-metadata-lab/` contém:
- `GitHubReleaseDiscovery.cs.txt`: consulta HTTPS de endpoint de GitHub Releases estável, repositório fixo e sem redirecionamentos; 404 = ausência de Release; falha de rede/limite de API = `RemoteUnavailable`; valida conteúdo JSON com tamanho máximo 512 KiB, flags booleanas estritas, rejeita propriedades duplicadas e tags de versão não estritas, compara com a versão informada; `NewVersionMetadataOnly` não confere permissão de instalação.
- `Tests.cs.txt`: testes locais com respostas HTTP simuladas, sem rede.
- `GF.GithubMetadataLab.csproj`: projeto .NET 10 autônomo, executável só nos testes.
- `README.md`: escopo e próximos passos.

Workflow `.github/workflows/gf-github-releases-metadata-lab.yml` executa em Windows descartável, sem artefato, release, instalação ou credenciais persistentes. Fontes `.cs.txt` não são compiladas no produto.

## 3. Falha encontrada e correção

CIs intermediárias [41147969573](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41147969573) e [41148013408](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148013408) **FAILURE** na guarda de isolamento: inicialmente criadas como `.cs`, as fontes experimentais eram incluídas pelo glob de compilação do GF principal. Foi **corrigido antes da validação**, renomeando as duas fontes para `.cs.txt` e incluindo-as explicitamente apenas no projeto do laboratório, sem modificar o projeto principal nem executar o GF com protótipo indevido.

## 4. CI válida e resultado

- [Execução 41148135551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41148135551), job `124378083571`.
- `completed/success`, tentativa 1, SHA `3192f427da3e9da2a16dec77c315a9b5af6518f0`.
- Log: `GF_GITHUB_RELEASE_METADATA_LAB` informa `tests_passed=25`, `status=passed`, `scope=OFFLINE_MOCK_GITHUB_READ_ONLY`, `no_download=true`, `no_install=true`, `no_product_changes=true`, `operational_ready=false`, `automatic_recovery_authorized=false`.
- A guarda `-getItem:Compile` atestou zero fontes do protótipo incluídas pelo `GerenciadorAplicativos.csproj`.
- **Limite:** testes de HTTP são mocks offline; a conexão HTTPS real, autenticação de pacote e qualquer instalação não foram exercitadas pela CI.

## 5. Próximo incremento recomendado (D1)

Adicionar contrato de **manifesto autenticado para a Release**, com assinatura e vínculo estrito entre versão, arquitetura, manifesto, caminho autorizado, tamanho e SHA-256 dos bytes esperados, reaproveitando o TUF de laboratório e **sem usar chaves de teste para produção**. Testar falsificações e respostas adversariais em Windows descartável, sem downloads executáveis reais. Somente depois desenvolver D2 (download e preparação) e D3 (troca com coordenador); nunca permitir que tag, JSON da API ou hash não assinado se tornem autoridade de instalação.

**Não solicitar WORK só para testes de metadados; reservar auditoria para integração criptográfica/transacional com evidência real e SHA fixo.**

## 6. Invariantes

A Fase 1 antiga permanece **ABERTA**, `READY=false`, `G1–G8 BLOCKED 0/8`, **zero recuperações automáticas**. HOLD = contenção, não recovery. H1/H2/H3 não comprovados. Sem alteração de main, PR #65 Draft, BETA .66, Cloudflare, binário do GF, chaves privadas, release, merge, deploy, instalador ou PCs pessoais.
