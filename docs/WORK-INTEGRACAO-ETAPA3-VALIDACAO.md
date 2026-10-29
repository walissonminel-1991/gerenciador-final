# Integração final — Etapa 3: regressões e CI

27/10/2026, America/Fortaleza. Branch `work-integracao-final-20261027`,
[PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).
**ETAPA 3 CONCLUÍDA TECNICAMENTE. Etapa 4 não iniciada. BETA conectada bloqueada.**

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Recibo por job/artefato](evidencias/integracao-etapa3-ci.json) ·
[Adendo BETA LOCAL](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## Árvore efetivamente validada

| Referência | Identificador |
| --- | --- |
| Entrada documental da Etapa 2 | `89ee12a8322c5872c61c3fd40030490407cbb173` |
| Código integrado na Etapa 2 | `06d7dce0bb47944f1efd18a10785fa77f5f0c8f7` |
| **SHA fonte validado nesta etapa** | **`6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8`** |
| Árvore do SHA fonte | `e5646e6d419b47aa80e337359b1a49c9262ce6f7` |
| Merge de teste automático do GitHub | `3bb5e7b44ccc7c362e32678afa9b99791d460211` |
| Base da PR | `2bc9f4228fd448445fbf181652e5af93005def20` |

O commit desta etapa registra o início da validação em cinco Markdown/JSON,
sem alterar código, testes, workflows, versão ou configuração do produto.
Os workflows foram acionados nesse novo SHA, sem reutilizar aprovações dos pais.
O merge de teste automático tem **a mesma árvore** do SHA fonte; ambos foram
conferidos na API Git. Esse ref de teste não mescla PR nem altera branches.

| Workflow | Run | Resultado | Tentativa |
| --- | --- | --- | --- |
| Compilar Windows (sem assinatura) | [40604593820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593820) | **success** | 1 |
| Integridade externa dos atualizadores | [40604593709](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593709) | **success** | 1 |

São **sete jobs obrigatórios aprovados**, dois condicionais skipped e nenhum
job falho/cancelado. A atestação exclusiva de main e a consulta externa
opcional ficaram skipped conforme suas condições; isso não elimina a
consulta oficial de drivers dos runners obrigatórios. Nenhuma repetição de CI.

## Regressões executadas na composição

| Área | Resultado | Limite da evidência |
| --- | --- | --- |
| Compilação/analisadores | Produto compilado, sem erros | Aviso CS0649 descrito abaixo; não é homologação AVG |
| Atualizadores/autorização/identidade/integridade | **410/410** | Fixtures e validações; não atualizações reais em PCs |
| Multifontes/antifalsos positivos | **196/196** | Arquitetura/canal, cache, cancelamento e falha parcial sob cenários existentes |
| Resíduos/desinstalação | **739/739** | Alvos sintéticos; preservação, consentimento, propriedade e pendência |
| Mutações adversariais | **36/36 rejeitadas** | Cópias descartáveis; sem aceitar erro de compilação como detecção válida |
| MSIX / encerramento seguro | **47/47 / 30/30** | Inventário/fluxos em isolamento; nenhum processo pessoal encerrado |
| Privacidade/consentimento/transporte | **127 verificações** | Handlers fictícios, revogação, locks/processos e falhas; sem rede ao receptor |
| Janela BETA/Shutdown/bandeja | **15/15** | Shutdown simulado na janela; não desligou o SO |
| Receptor/retenção BETA / receptor anterior | **126/126 / 10/10** | SQLite local e APIs fictícias; não Worker/D1/GitHub privados reais |
| Políticas Python/pacote | **47/47** | Configuração e contrato fechado de distribuição |
| Ecossistemas / catálogo / comparação | **54 / 17 / 20** | Identidade e metadados consultivos, sem novos instaladores |
| Interface | **132 combinações resolução/aba**, oito WORK18, 12 ciclos de bandeja | Escalas e viewports sintéticos; DPI/tema físico não homologados |
| Drivers Windows 2022 / 2025 | **173 / 174 verificações** | ABI/fixtures aprovadas; estados oficiais distintos, descritos abaixo |
| Integridade externa | Seis etapas aprovadas | Catálogo autenticado, WORK13, SumatraPDF, 7-Zip, GameInput e Terminal; sem instalar |
| Build Windows x64 | **Aprovado; 286 hashes** | Self-contained, AMD64 e manifesto Win32 conferidos no runner |

As contagens não devem ser somadas: há subconjuntos, repetições da mesma suíte
e execução em mais de um runner. Não equivalem a programas novos homologados
ou operações em computadores pessoais. Os modos da revisão pré-BETA foram
executados pelos despachantes preservados na integração.

A cobertura WORK18 foi relida do artefato: 28 fixtures, ações pré-payload 5→13
contra a base histórica .64, com **zero payload autorizado**, zero operação
de terceiros e zero homologação física. É revalidação da cobertura já existente,
não avanço funcional novo desta etapa.

## Desempenho e interface no runner

O benchmark existente executou quatro cargas, com três amostras por carga,
runtime de testes 10.0.14 e quatro processadores lógicos do runner:

| Apps / evidências | Mediana (ms) | Resultado |
| --- | --- | --- |
| 500 / 5.000 | 59,94 | Assinatura de resultado estável |
| 1.000 / 10.000 | 101,87 | Assinatura estável |
| 1.000 / 20.000 | 218,70 | Assinatura estável |
| 1.000 / 50.000 | 110,89 | Orçamento limitado: 20.000 evidências não examinadas, explicitamente preservadas |

O último cenário processa um escopo limitado e não é comparável como trabalho
integral à linha anterior. Esses tempos não medem PC FORTE/FRACO e não fecham P5.

O artefato de layout contém 83 PNGs. Conferidos seu digest e uma amostra:
WORK18 em 1440×900/escala simulada 200%, diálogo de drivers 500×340 e
confirmação de resíduos. Conteúdo/ações visíveis e rolagem preservados na
amostra; moldura/controles nativos do runner e escala simulada não homologam
tema, foco ou DPI físicos no PC. V5 e provas físicas continuam abertas.

## Limites observados, sem ocultar resultados

- **Windows 2022:** consulta oficial WUA excedeu o prazo nativo; o fluxo
  conservou resultado inconclusivo e não publicou correspondências. O job
  passou nas proteções/fixtures, mas essa consulta não é prova positiva de G1.
- **Windows 2025:** consulta oficial completa, zero ofertas. Também não
  demonstra compatibilidade/assinatura do payload de um driver atualizável real.
- **CS0649, Program.cs:599:** campo `_privacyStatusButton` não recebe atribuição
  no perfil GF_BETA; a atribuição do botão retirado fica sob `#if !GF_BETA`.
  Aviso não bloqueante, com testes BETA aprovados. Não foi alterado o produto
  apenas para esconder o aviso.
- Nenhum defeito funcional bloqueante foi reproduzido pelos cenários executados.
  **Zero correções de produto e zero testes novos** nesta etapa; não houve
  relaxamento de proteção ou remoção de teste para obter aprovação.
- AVG, comportamento físico, DPI/tema e homologação conectada permanecem
  pendentes. Nenhuma assinatura/homologação foi presumida.

A falha externa histórica da #62 continua apenas no histórico. Nenhum alvo
permanentemente excluído foi reinvestigado, consultado ou reexecutado.

## Build interna, manifestos e ausência de distribuição

A CI compilou a fonte .66 para Windows x64, conferiu manifesto/ativação SxS,
AMD64, dependências/licenças e o contrato de pacote; verificou **286 hashes**
antes do upload. A evidência antivírus é de origem/integridade, **não varredura AVG**.

O artefato **BETA-manifesto-6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8**, ID **12779928854**,
contém somente `BETA-BUILD.json`, `BUILD-INFO.txt` e `SHA256SUMS.txt`.
Arquivo transferido e SHA-256 conferido:

`ebe688f662593689c0ba8b0f4d43a33828d8b048d8386eed2be8a09043aa6754`

A conferência separada confirmou os três arquivos, formato/nomes únicos do
manifesto de 286 entradas, hashes dos dois metadados presentes, SHA fonte,
merge/base/run e os dados reais do assembly:

| Campo | Valor observado |
| --- | --- |
| Versão / canal | 5.13.11.66 / BETA |
| source_commit | `6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8` |
| transport_configured | **false** |
| endpoint | Vazio |
| reviewed_notice_sha256 | Vazio |
| Aviso | gf-beta-20261026-r3 |
| notice_sha256 | `f04863df19be3c40100b9471b9c01962b16bdfb73ba7af2fd58e5ba30bc59701` |
| Distribuição | requires_owner_approval |

O recibo preserva hashes do manifesto, arquivos e componentes críticos, IDs
e digests dos artefatos, além dos hashes dos logs decodificados por job.
Artefatos temporários expiram em 03/11/2026.

**Não foi baixado, publicado ou entregue EXE/ZIP BETA.** A conferência fora do
runner se limita aos manifestos/evidências; não é verificação independente
de um pacote executável. A candidata BETA LOCAL da Etapa 8 não foi preparada.

## Preservação e encerramento

Roadmap **27/42**, mesmos 42 IDs/done; histórico físico e candidate/beta
preservados. VLC permanece somente inventário, sem ações/consultas/verificações
ou novos testes específicos. WinGet COM segue opcional, não bloqueante e
consultivo; regressão existente sem investigação/reparo. Instalação/remoção/
reversão de drivers desativadas.

Worker/D1/DNS/Secrets, flags `PILOT_ENABLED=no`/`GITHUB_EXPORT_ENABLED=no`,
endpoint e aprovações não foram alterados. Nenhum PC pessoal acessado,
manutenção, transmissão de diagnóstico, merge, release ou deploy realizado.

O fechamento posterior altera somente cinco Markdown/JSON de continuidade,
com `[skip ci]`, sem mudar código/testes/workflows/configuração validada.
Seu HEAD documental deve ser conferido na PR; a CI aprova o SHA fonte acima,
não uma nova build do recibo.

**Etapa 3 concluída tecnicamente, com limites registrados. Próxima sessão:
somente Etapa 4 — avaliação da candidata integrada. Não iniciada aqui.**
