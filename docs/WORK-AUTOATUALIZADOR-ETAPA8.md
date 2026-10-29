# Autoatualizador GF — Etapa 8: implementação isolada dos deltas G2/G8

28/10/2026, America/Sao_Paulo. **Etapa 8 concluída no protótipo isolado.
Zero de oito gates operacionais fechado; oito bloqueados. Etapa 9 NÃO INICIADA.**
PR #65 aberta/Draft, branch `work-integracao-final-20261027`, entrada `13afd9766bd99a54a451865cf76b133eb1b29671`.
Checkpoint e relatórios técnicos da Etapa 7 lidos; contrato gf-self-update/1
revisão 1 intacto. Não houve alteração funcional no executável principal.

## Código entregue e provas direcionadas

Código inicial `8b947d3c54c74af938d8bcfb43463eebe181e2ff`; correção restrita ao leitor de evidência e seleção de
caso no workflow `bcc681079a209306132e0f03b5a47c9b5f4ee1af`. Fontes em
[stage8](../prototipos/autoatualizador-seguro/stage8/README.md), sem substituir
protótipos anteriores. C# versionado como `.cs.txt`, Compile explícito e saídas
externas; produto avaliado com 142 fontes/zero protótipo, sem compilá-lo.

G2: fonte MIT Tuf 1.1.1 no SHA já fixado, BC 2.7.0 com lock/auditoria intactos.
Patch de TufClient, TufClientOptions e TufResults: sete consultas passam a UTC
escalar readonly capturado uma vez por tentativa, default/offset inválidos são
recusados e reutilização/concorrência do cliente são bloqueadas. Timestamp
igual, depois de verificar assinatura, propaga Unchanged ao nível externo:
não percorre snapshot/targets, não busca/lê alvo em cache e acesso a Content
é recusado. A facade chama GetTarget apenas uma vez, sem refresh anterior.
Cache é memória limitada, reancorado na raiz pública fixa de fixture; não é
estado persistente de produto nem autorização para baixar pisos.

G8: ownership/reserva precedem a chamada interna da factory; início síncrono
bloqueado fica fora da espera da interface, porém continua registrado. O dono
observa a tarefa original, retém recursos e lease, propaga token e compartilha
Stopwatch/prazo entre fases. Cancelar espera não equivale a terminar I/O:
resultado desconhecido impede continuação, reconciliação prematura, liberação
e nova admissão. Sucesso/falha tardios permanecem observáveis, sem revalidar
tentativa; reconciliação de fixture fecha a tentativa e só então permite
liberação. Recursos compartilhados por operações são descartados uma vez.
Não existe API de aplicar, seletor GF ou recuperação persistente neste código.

| Execução | Resultado efetivamente observado |
| --- | --- |
| [Run inicial 40805278830](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40805278830) | 26/27; 17 G2 aprovados, nove G8 aprovados e uma falha de leitura de conferência |
| [Run dirigida 40805755390](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40805755390) | 1/1, somente U8-G8-LATE-FILE-WRITE após correção; os 26 aprovados não foram repetidos |
| Evidência composta | 27 casos distintos com resultado positivo pertinente; **não houve bateria completa de 27 no SHA C2** |
| Ambiente | Windows descartável, SDK 10.0.402, runtime .NET 10.0.14; build do ensaio sem warnings/erros |

A falha inicial foi IOException ao abrir ReadAllText enquanto o handle escritor
ainda estava retido. O leitor de conferência agora usa FileShare.ReadWrite;
permanece somente leitura e o escritor/lease continuam retidos. Só esse caso
e o controle para selecionar seu prefixo mudaram; OwnedAttempt, TufAttempt,
patch TUF, fixtures e demais caminhos aprovados são idênticos. A falha e seu
stack original estão preservados no JSON inicial, sem trocar failed por passed.

Provas novas incluem uma captura UTC, uma cadeia de fetches, término em ambos
os entrypoints, concorrência de cliente, igualdade de validade nos caminhos
alterados, cache e cadeia raiz pertinentes; admissão cancelada/concorrente,
factory síncrona, tarefa cooperativa, erro tardio e escrita real de texto
deliberadamente atrasada. Os casos de validade/assinatura exercitam o novo
escalar/atalho, não repetem as suítes antigas. O orçamento curto compartilhado
observou 1.001 s
para duas fases com limite de 1 s. O padrão segue 120 s; a execução antiga de
120 s não foi repetida. Sinais e faults controlados não são hang/queda de energia.

## Limites preservados e bloqueios

G2 continua bloqueado para adoção: faltam aceite de manutenção/revisor,
interoperabilidade/conformidade e adoção conjunta de cache/época/pisos/guarda
pré-commit persistentes. Não se promove o patch a biblioteca de produto.
G8 continua bloqueado: a barreira de resultado desconhecido é de runtime;
falta sobrevivência a crash/restauração, armazenamento/Flush/ReplaceFile
bloqueantes, falhas de disposal e pipeline/ciclo GF qualificado. Callback de
reconciliação lê somente fixture; não é prova de diário operacional.
G1 e G3–G7 continuam bloqueados nos mesmos recursos/insumos externos, sem reteste.

O reset da sessão perdeu o workspace da primeira tentativa local, inclusive
seu resultado. Não foi recuperada contagem ou evidência verificável; essa
tentativa não é declarada aprovada nem falha de segurança. Código recuperado
foi salvo no GitHub antes da validação Windows; não se baixou novamente SDK Linux
nem se atribuiu cobertura Linux inexistente. Runs Windows e falha real são
rastreáveis pelos SHAs, IDs, tentativas e arquivos de resultados.

Os três hashes de fontes TUF modificadas diferem entre Linux/LF e Windows/CRLF.
Conferida equivalência **exata LF→CRLF**, sem diferença de conteúdo; hash do
patch canônico igual e hashes Windows iguais nas duas runs. Parsers/crypto,
licenças e demais 17 arquivos do manifesto de origem intactos. Essa conferência
de proveniência não é homologação nem auditoria nova da biblioteca.

[Windows inicial](evidencias/autoatualizador-etapa8-windows-inicial.json) ·
[Correção](evidencias/autoatualizador-etapa8-windows-correcao.json) ·
[Rodadas e composição](evidencias/autoatualizador-etapa8-rodadas.json) ·
[Gates](evidencias/autoatualizador-etapa8-gates.json) ·
[Recibo](evidencias/autoatualizador-etapa8.json) ·
[Checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).

BETA v5.13.11.66/fonte 6df95fdd776644cd4b248b5cea61108974ca9ce9,
canal privado, artefato 12828262039/run 40716548530/tentativa 1/ZIP SHA-256
8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2
intactos, sem download/rebuild. Roadmap **27/42**, mesmos IDs/done, avaliação
física parcial e históricos preservados; zero repetição física/suítes antigas.
AVG/Windows, dados/consentimento, remoto bloqueado/PILOT_ENABLED=no e
GITHUB_EXPORT_ENABLED=no, VLC só inventário, COM consultivo e drivers inativos
preservados. Sem chaves operacionais, permissões/contas/UAC/ACL, canal privado,
Worker/D1/DNS, integração GF, merge/Release/publicação ou Etapa 9.
**Encerrar após salvar/conferir na PR #65.**
