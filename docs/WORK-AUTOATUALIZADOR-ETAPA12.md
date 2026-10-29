# Autoatualizador GF — Etapa 12: duas divergências TUF isoladas

29/10/2026, America/Sao_Paulo. **Escopo da Etapa 12 concluído. G2=blocked;
oito gates operacionais bloqueados, zero fechado. Etapa 13 não iniciada.**
PR #65 aberta/Draft, branch work-integracao-final-20261027. HEAD obrigatório
`65360953fcc15023a7853e7f3304217fdee67065` conferido antes das alterações e do salvamento.
Código validado `eafec1070bd7c35420741bad4560dc9be4a37d6e`; SHA de encerramento documental registrado na
descrição da PR. Não é uma etapa de homologação física.

## Resultado e causa

As duas diferenças foram reproduzidas e isoladas. **Não foi encontrado defeito
do protótipo .NET nesses vetores; nenhuma correção foi feita nele.** A causa
observada está na invalidação conjunta ausente no caminho da referência Python
fixada. Esta é uma adjudicação técnica do próprio implementador, sustentada por
fonte e ensaios, **não revisão independente**. A referência intacta ainda diverge;
não se declarou interoperabilidade bruta resolvida nem conformance integral.

| Divergência | Antes, Etapa 11 | Agora, referência intacta | Causa isolada / ablação com os mesmos bytes | Decisão |
| --- | --- | --- | --- | --- |
| Troca de timestamp; raiz1→2, papéis100→1 | .NET aceita; Python BadVersionNumberError | Reproduzida: root2, timestamp1, snapshot100 | `_load_snapshot(trusted=True)` recarrega snapshot100; a referência antiga de targets100 recusa targets1. Invalidar ambos os arquivos após autenticar a raiz2 faz o Python aceitar 1/1/1 | .NET preservado; lacuna da referência fixada no caso analisado; comparação bruta blocked |
| Troca de snapshot; raiz1→2, papéis100→1 | .NET aceita; Python BadVersionNumberError | Reproduzida: root2, timestamp100 | `_load_timestamp` recarrega timestamp100, cuja chave permaneceu válida; timestamp1 é recusado. A mesma invalidação conjunta faz o Python aceitar 1/1/1 | .NET preservado; mesma causa normativa; comparação bruta blocked |

No primeiro caso, a mensagem observada é
`Expected targets.json version 1, got 100.`; os números na mensagem do Python
devem ser lidos junto às versões estruturadas/trace, não como prova autônoma.
No segundo, `New timestamp version 1 must be >= 100`.
As chaves antigas do papel que não mudou ainda verificam assinaturas; isso não
autoriza reutilizar seu piso quando a regra de invalidação conjunta se aplica.
Não houve aceitação de conteúdo sem autenticação: a falha reproduzida é de
recuperação/disponibilidade nesses resets legítimos.

## Fundamento e limites da adjudicação

[TUF 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/),
fonte `59e601ed29c0d2e497264ae8b31c11b8ef07df1e`, SHA-256
`d36ada5b8e894edc62239eaac4f4d7c54a5f2aad18a3b658b7d5128f43052170`. §5.3.4–5 e §6.1 exigem continuidade N+1 e thresholds
antigo/novo; §5.3.8 exige persistir a raiz e §5.3.10 confere sua expiração final.
§5.3.11 determina invalidar timestamp **e** snapshot se as chaves de qualquer
um deles mudarem. Os testes de rollback dos §§5.4.3/5.5.5 usam os metadados
confiáveis remanescentes; §5.6.4 vincula targets à versão do snapshot novo.
Portanto, nesses dois vetores, as referências anteriores não impedem 1/1/1
após a rotação autenticada. Sem essa mudança, os pisos continuam obrigatórios.

O [Updater fixado](https://github.com/theupdateframework/python-tuf/blob/49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15/tuf/ngclient/updater.py#L381)
atualiza/persiste as raízes em `_load_root` (381–425), mas não remove ambos
os arquivos. `_load_timestamp` (427–447) e `_load_snapshot` (449–467) recarregam
os caches. O [TrustedMetadataSet](https://github.com/theupdateframework/python-tuf/blob/49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15/tuf/ngclient/_internal/trusted_metadata_set.py#L216)
aplica então os testes de rollback sobre o estado recarregado (249–269,
340–359). Fonte instalada, versão/commit e três hashes são exatamente os da
Etapa 11. Tuf 1.1.1, BC 2.7.0, fonte fixada/20 arquivos e patch da Etapa 8
também permanecem idênticos. Nenhuma dependência foi atualizada.

As ablações excluem diferenças de bytes, UTC, âncora inicial, configurações
relevantes e fixture malformada como causa desses resultados: o cache inicial
tem a mesma raiz1 e papéis100; os metadados novos são idênticos entre clientes;
com cache limpo ambos aceitam; removendo apenas os dois arquivos exigidos após
a mesma raiz autenticada o Python aceita. Casos negativos continuam recusados.
Isso sustenta a classificação **lacuna da implementação Python fixada nesses
caminhos**, sem substituir avaliação externa ou afirmar que toda a implementação
.NET está conforme. Não houve contato com terceiros ou Issue pública.

## Implementação e ensaios dirigidos

[Harness mínimo](../prototipos/autoatualizador-seguro/stage12/README.md): nove
arquivos novos no harness/workflow. Dois vetores principais e controles,
nove cenários ao todo, vinte blobs deduplicados; somente seeds públicas de teste
preexistentes. O catálogo guarda bytes/base64, SHA-256, chaves/thresholds antigos
e novos, versões, referências, alterações autorizadas e resultados esperados.
Os bytes foram gerados nesta missão, com a mesma transição da Etapa 11; não são
falsamente apresentados como recuperação dos bytes históricos.

UTC fixo compartilhado: **2026-10-29T11:31:03Z**, seam de laboratório,
constante em cada tentativa. O catálogo faz roundtrip exato antes dos testes.
O piso UTC persistente original permanece obrigatório. Um replay posterior
dos mesmos bytes no diário .NET exige relógio de laboratório explicitamente
controlado anterior ao UTC registrado; não há bypass do relógio de produção.

`BoundaryProbe` chama o algoritmo upstream intacto e observa cache/versões.
Somente nos modos `diagnostic-*`, uma ablação de teste limitada às duas raízes
de fixture remove timestamp/snapshot depois da autenticação/persistência da
raiz2 exata, comparação das chaves autorizadas e expiração final válida.
**Não é patch da biblioteca, adaptador operacional ou protocolo durável.**
Não remove arquivos nos controles sem rotação ou com threshold insuficiente.

[Run Windows 40864493929](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40864493929), número1/tentativa1,
SHA `eafec1070bd7c35420741bad4560dc9be4a37d6e`: **14/14 casos .NET**, **16/16 observações Python previstas**,
zero warning/erro; SDK10.0.402/runtime10.0.14, restore locked/auditado.
Zero falha nova de CI e zero retry. As duas recusas normativas do Python são
preservadas como `failed` no comportamento analisado e `blocked` na comparação
bruta; o harness aprova sua reprodução esperada, não sua conformidade.

| Ensaio selecionado | Resultado | Limite |
| --- | --- | --- |
| Dois casos originais com cache100 preservado | passed reprodução; Python normativo failed; comparação blocked | Não fechar interoperabilidade bruta |
| Dois caches limpos e duas ablações de invalidação | passed; ambos aceitam 1/1/1 | Ablação não é implementação upstream |
| Quatro cortes .NET: flushed/committed da raiz × dois papéis | passed; época1 reaparece sem pisos antigos, hints100 ignorados e 1/1/1 aceito | Filho mantido, saída observada, fronteira síncrona concluída; não energia |
| Quatro cortes Python antes/depois da invalidação | passed observação; antes continua recusando, depois aceita | Exceções controladas, não crashes de processo nem protocolo de recuperação |
| Avanço 1→2 e replay1 na mesma época, inclusive reopen .NET | passed recusa; pisos2 preservados | Não é antirreplay após restauração integral |
| Chaves revogadas timestamp/snapshot, raiz sem threshold e reset sem rotação | passed recusa; nenhuma invalidação diagnóstica nos dois últimos | Somente perfil fechado e transições especificadas |

As 45 provas da Etapa 11 **não foram executadas novamente**. Apenas as duas
transições abertas e caminhos diretamente ligados foram selecionados; provas
anteriores de pacote/PE/UTC/estado e físicos foram reutilizadas por referência.
Zero build/execução GF ou upload de artefato. A avaliação MSBuild continua com
142 itens Compile GF, zero protótipo. Árvore gerada pelo controlador apagada;
fixtures de NTFS pertencem ao runner descartável.

## G2 e dossiê para decisão externa

A/B: duas divergências reproduzidas e causa identificada. C: nenhuma correção
do protótipo .NET foi necessária nesses vetores; a referência Python permaneceu
intacta. Eventual remediação dela requer decisão e revisão próprias. O acréscimo
é o harness causal. D: vetores e controles satisfizeram as observações descritas. E: a
conduta .NET é coerente com os dispositivos citados **nesses dois vetores**.
F: revisão qualificada independente **não executada**. G: **G2=blocked**.

Faltam exatamente: revisor independente para análise/patches/épocas; responsável
de manutenção com aceite formal; decisão de qualificação/interoperabilidade do
perfil GF diante da referência ainda divergente; aceite da persistência e
recuperação suficientes e autorização de adoção operacional. As demais lacunas
dos gates permanecem: frescor autenticado fora do estado restaurável,
âncora/custódia autorizadas e ciclo/IPC/prontidão/rollback GF qualificados.
Nem recibos locais resolvem G1 nem cortes de processo resolvem energia/I/O.

O dossiê mínimo é este relatório + catálogo exato + resultados/trace + fonte
fixada. Questões ao revisor: confirmar a aplicação de §5.3.11 aos dois casos;
revisar o piso derivado de targets e a recuperação por evento da raiz;
decidir o tratamento aceito da referência divergente antes de adoção.
**Menor próxima ação:** designar esse revisor e o mantenedor e revisar o
dossiê, decidindo eventual remediação/qualificação da referência em missão
própria. Não iniciar integração ou outra suíte ampla por consequência.

## Evidências, arquivos e preservações

[Vetores exatos](evidencias/autoatualizador-etapa12-vetores.json), SHA-256
`79958777a33a6d43cbbe41f3c73882de3f89d7b44cef5af402726df910978fb5`; [resultados e interoperabilidade](evidencias/autoatualizador-etapa12-windows.json),
SHA-256 `02cc2c3d44e3652b3d00b0e9d9dc349434a874eac2f54dec8ea14bd0e9ac941b`; [recibo único](evidencias/autoatualizador-etapa12.json);
[matriz G1–G8](evidencias/autoatualizador-etapa12-gates.json);
[checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md). Recibo registra commits, run,
hashes, fontes, seleção, falhas históricas, não executados e lista de arquivos.

Preservados BETA v5.13.11.66/fonte funcional/canal privado, PR aberta/Draft,
main `410d65e2`, doze workflows anteriores, contrato gf-self-update/1,
**27/42** com mesmos IDs/done, avaliação física parcial, AVG/Windows,
consentimento/privacidade, diagnóstico remoto desativado, PILOT_ENABLED=no,
GITHUB_EXPORT_ENABLED=no, drivers operacionais desativados, VLC inventário e
WinGet COM opcional/consultivo. Sem contas, credenciais, permissões, trust store,
Worker/D1/DNS, infraestrutura privada, chaves operacionais, produto integrado,
distribuição, instalação operacional, merge/Release/deploy/publicação ou PC pessoal.
**Etapa 13 não iniciada. Encerrar após salvar e conferir na PR #65 Draft.**
