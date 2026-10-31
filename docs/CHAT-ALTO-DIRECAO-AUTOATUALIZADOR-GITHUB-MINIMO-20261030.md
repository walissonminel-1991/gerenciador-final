# CHAT ALTO — Reorientação: autoatualizador portátil simples com origem GitHub

**Decisão técnica experimental de 30/10/2026 — não é autorização para atualização operacional.**

## Objetivo do mantenedor

O GF deve buscar uma versão oficial do **próprio GF** exclusivamente no GitHub, verificar a autenticidade e oferecer uma atualização simples ao usuário, sem serviços pagos. Isso não é a atualização de programas de terceiros, não é a coleta de diagnósticos e não permite publicar versões automaticamente.

Os diagnósticos dos avaliadores, mediante consentimento, orientam manutenção; o mantenedor revisa correções e aprova a Release. Só então o programa consulta e, em etapa futura, instala uma nova versão autenticada.

## Constatação atual

A consulta direta ao endpoint GitHub Releases do repositório `walissonminel-1991/gerenciador-final` retornou **0 Releases publicadas** em 30/10/2026; `releases/latest` respondeu 404. A infraestrutura de descoberta operacional não pode oferecer uma atualização real enquanto não houver publicação oficial.

## Trilhas separadas, sem falsa promoção

**Trilha simples de entrega (experimental, incremental):**

1. **D0 — Discovery somente leitura:** endpoint oficial e fixo, canal estável, versão numérica e limites de resposta; 404 significa sem Release, 403/429/timeout não significa sem atualização; nenhum download, execução ou instalação. Protótipo independente em `prototipos/autoatualizador-seguro/github-metadata-lab/`. Apenas simulação offline na CI.
2. **D1 — Autorização da Release:** manifesto e hashes autenticados por uma autoridade criptográfica independente dos campos JSON e links da API. Reutilizar os componentes TUF já investigados, sem copiar chaves privadas de produção para o repositório e sem transformar chaves de teste em autoridade operacional. Rejeitar downgrade, metadados inválidos, expirados, rollback de confiança e divergência de arquitetura.
3. **D2 — Download e preparação:** baixar apenas objetos autorizados, sem executar ou modificar o GF. Verificar hash, tamanho e identidade após aquisição; persistir de forma controlada no espaço portátil, com proteção e limites. Não depender de executar PS1 ou programas auxiliares arbitrários vindos da Release.
4. **D3 — Troca segura:** somente após consentimento do usuário e encerramento verificado do GF, componente portátil separado controla OLD/NEW, journaling, verificação de bytes e reinício. Se o estado for ambíguo, preservar artefatos e bloquear ações automáticas perigosas. Sem reinstalador persistente, serviço, driver ou backend.
5. **D4 — Homologação real:** CI de Windows descartável e avaliação física antes de qualquer integração à distribuição; somente depois habilitar o recurso. Requisito de prova independente de estado **não pode ser falsificado** por PID/HWND/recibo same-user.

**Trilha de pesquisa avançada (congelada para não consumir carga indevidamente):**

- A prova positiva externa de `DarkProgramList` continua `POSITIVE_UI_PROOF_PENDING`.
- H1/H2/H3 (histórico contra rollback same-user, despacho universal de NEW, quiescência de efeitos delegados) continuam sem prova independente.
- A linha experimental B+A/TUF/NTFS/TOCTOU, 29 casos de contenção e os seus SHA/CI permanecem intactos.
- Pesquisas avançadas são acionadas por uma ameaça concreta ou quando forem necessárias à autorização operacional; não fingir que problemas pendentes desapareceram.

## Política de segurança e não escopo

D0 **não autentica bytes** apenas porque a API HTTPS diz que há uma tag; `NewVersionMetadataOnly` nunca significa `CanInstall`. Publicação no GitHub, sem manifesto assinado, é dado de descoberta, não autorização de código executável.

**Fase 1 antiga permanece ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas autorizadas; HOLD é contenção.** Este plano adiciona uma trilha experimental de descoberta, não altera os critérios históricos, não fecha gates e não anuncia produto.

Sem escrita em main, PR #65 Draft, BETA .66, Cloudflare, chaves reais, telemetria, release, deploy, merge ou código distribuído do GF. WORK reservado à revisão de segurança de candidata com integração relevante e evidências verificáveis.
