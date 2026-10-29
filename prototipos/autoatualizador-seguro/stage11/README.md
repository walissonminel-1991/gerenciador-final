# Etapa 11 — confiança TUF persistente, isolada

`python prototipos/autoatualizador-seguro/stage11/run_tests.py --report <externo>.json`

Requer Windows/NTFS e SDK 10.0.402. Executa somente os deltas novos; nenhum GF
é compilado/iniciado. Tuf 1.1.1 continua referência: mesma fonte fixada/patch
experimental da Etapa 8; BC 2.7.0, lock e auditoria preservados. Dependências
Python são somente o oráculo de teste, fixadas no workflow e verificadas por
versão/hash de fonte. Arquivos gerados e binários ficam fora do repositório.

## Autoridade e registros

`DurableEpochCache` substitui a recusa simples de `EpochGuardedCache` apenas
nesta nova composição experimental. Usa os mesmos algoritmos/verificadores
TUF, com checagem adicional das transições ao gravar e ao reler cada evento.
A âncora permitida é exclusivamente a raiz pública de fixture preexistente.

O diário `tuf11/records` guarda bootstrap, início, cada raiz/papel autenticado
e término. Cada registro tem instalação, tentativa, UTC capturado, sequência,
época, bytes e encadeamento. `tuf11/seals` fornece recibos separados para detectar
perda parcial de registros. O cache `hints` é reconstruível e nunca fornece
pisos ou autoridade. Diretórios/ancestrais são retidos pela guarda NTFS
inalterada; há lease exclusivo durante leitura/escrita e criação sem overwrite.

Rotação da raiz exige N+1 e threshold antigo e novo. Rotação de timestamp ou
snapshot invalida os dois metadados segundo TUF 1.0.36 §5.3.11; a referência de
targets passa a ser estabelecida pelo snapshot da nova época. O histórico da
época anterior permanece no diário. Troca isolada da chave de targets exige
nova verificação e conserva os pisos: não autoriza reset. O perfil fechado
Ed25519/seis chaves/sem delegações permanece; outros perfis não são qualificados.

Um avanço não depende da instalação: cada papel é persistido antes de o cache
retornar ao cliente. UTC é fixo por tentativa e fica registrado. Falha conhecida
de validação conclui só a tentativa; não desfaz metadados anteriores. Resultado
de escrita desconhecido ou avanço autenticado sem registro mantém a tentativa
bloqueada. Não existe limpar/reiniciar confiança para contornar a recusa.

## Recuperação e composição

Interrupção em fronteira síncrona completada pode ser recuperada com prova do
Process mantido pelo harness, saída observada e ligação ao hash/sequência exatos.
Registro completo pendente pode ser promovido e selado após revalidação;
registro parcial/corrompido, prova de outro escopo ou fronteira desconhecida
continua bloqueado. Os cortes de processo não provam energia/armazenamento ou
término de qualquer I/O de driver. Hashes/recibos locais não resolvem restauração
integral do mesmo domínio: G1 continua aberto.

`TrustIntegratedPipeline` reusa recursos, journal e transações das Etapas 2/9
com arquivos inertes. O diário novo é a autoridade TUF; campos de cache legado
ficam vazios e não são migrados. A confirmação e o piso da versão de aplicação
continuam no diário de instalação original. Rollback inerte por falha síncrona
não altera o diário TUF. Recuperação de instalação por crash nessa nova
composição, migração de cache legado, IPC e ciclo GF ainda exigem qualificação.

Os vetores nativos são criados pelo serializador/assinador Python TUF usando
somente seeds públicas já existentes. Os bytes em `fixtures/upstream-vectors.json`
são de testes públicos do python-tuf no commit indicado, com licença e hashes;
exercitam o cliente/biblioteca separadamente, sem virar âncora GF. Concordâncias
com a referência e divergências são registradas separadamente: nenhuma aprovação
de teste interno equivale a revisão independente ou conformidade integral.
