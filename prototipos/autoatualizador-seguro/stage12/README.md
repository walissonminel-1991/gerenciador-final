# Etapa 12 — reproduções mínimas das duas divergências

`python prototipos/autoatualizador-seguro/stage12/run_tests.py --report <externo>.json`

Windows/NTFS descartável, SDK 10.0.402. Usa exatamente a fonte/patch TUF da
Etapa 8, BC 2.7.0 e Python TUF 7.0.1 fixados na Etapa 11. Nenhuma correção
do `DurableEpochCache`, algoritmo Python ou GF é pressuposta pelo harness.
`StartupObject=Tests12` impede chamar Main/45 testes antigos, cujas definições
de fixture são reutilizadas sem execução. Saídas de build fora do repositório.

São dois vetores principais: raiz1/papéis100 → raiz2 mudando apenas timestamp
ou snapshot → papéis1. Ambos usam as seeds públicas preexistentes. O catálogo
registra todos os bytes/base64/SHA-256, chaves/thresholds, referências, UTC único,
estado e expectativa. Os novos bytes são equivalentes em estrutura aos casos
da Etapa 11; não são falsamente apresentados como bytes históricos recuperados.
`--vectors docs/evidencias/autoatualizador-etapa12-vetores.json` materializa os
bytes salvos sem gerar assinaturas. O relógio registrado é seam de teste: a
reabertura do diário exige bootstrap anterior a esse UTC, portanto um replay
posterior à data deve usar ambiente/relógio de laboratório explicitamente
controlado; não contornar o piso de UTC de produção.

O Python permanece intacto. `BoundaryProbe` observa chamadas locais e versões;
apenas no modo de ablação identificado `diagnostic-*` remove timestamp e
snapshot depois de o próprio Updater autenticar/persistir a raiz2 exata,
comparar as chaves autorizadas e conferir a expiração final. Isso isola §5.3.11.
Não é patch upstream, adaptador de produto, protocolo de durabilidade ou reset
geral. Nenhuma remoção ocorre nos controles sem rotação/threshold suficiente.

14 casos .NET e 16 observações Python verificam cache limpo/preservado,
replay na mesma época, chave revogada, threshold e recuperação. Quatro cortes
.NET matam filhos mantidos pelo harness em gravações síncronas de raiz
completadas; Python usa duas exceções por vetor antes/depois da invalidação.
Essas provas têm escopos distintos; nenhuma equivale a energia real ou término
de I/O arbitrário. Hints antigos não são autoridade no diário .NET.

Sucesso do harness significa observações/asserções previstas satisfeitas.
As recusas Python originais continuam registradas como falha do comportamento
normativo analisado, e a comparação bruta permanece divergente. Nem análise
do próprio implementador nem biblioteca de referência equivalem a revisão
independente/conformance integral. G2 operacional continua bloqueado.
