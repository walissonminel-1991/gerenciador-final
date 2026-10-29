# Autoatualizador GF — Etapa 11: confiança TUF persistente

29/10/2026, America/Sao_Paulo. **Etapa 11 concluída no escopo experimental.
G2 permanece blocked; zero dos oito gates operacionais fechado. Etapa 12 NÃO
INICIADA.** PR #65 aberta/Draft, branch `work-integracao-final-20261027`.
Entrada obrigatória confirmada: `2cf5a8fb8addb94da4050667dc8af16d71e413b1`.
Código validado: `8dc843ff69b66f55f6e87fab93bd9c71c1242ec5`. O SHA do
encerramento documental posterior consta na descrição da PR e na entrega.
Contrato gf-self-update/1 e código funcional da BETA .66 preservados.

## Implementação e resultado

[Componente e execução](../prototipos/autoatualizador-seguro/stage11/README.md):
`DurableEpochCache` registra bootstrap, admissão da tentativa, cada raiz/papel
autenticado e término em arquivos imutáveis encadeados. Recibos separados
identificam perda parcial de registros. O cache auxiliar não fornece pisos:
é reconstruído a partir do diário. Leases exclusivos, ancestrais/diretórios
retidos e criação sem sobrescrever reutilizam a guarda NTFS aprovada.

A confiança avança antes de o cache retornar ao cliente, sem esperar pacote,
instalação ou confirmação de versão. A recuperação revalida assinaturas,
referências, sequência, instalação, época e UTC registrados. Escrita parcial,
registro corrompido ou avanço autenticado ainda sem registro mantém o bloqueio;
não existe reset genérico de confiança para contornar falha.

O novo `TrustIntegratedPipeline` compõe os recursos e o journal da Etapa 9 com
a transação/seletor da Etapa 2 e o diário TUF novo. Os campos de cache legado
ficam vazios e não são autoridade. Não há migração automática de cache legado
populado. Confirmação, piso da versão do aplicativo e rollback inerte continuam
no diário de instalação. Os quatro ensaios dessa composição conservaram os
pisos TUF após confirmação ou falha de instalação.

A [run Windows 40859356385](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40859356385),
número 2/tentativa 1, aprovou **45/45 casos novos** no mesmo código, com zero
warning/erro de compilação. SDK 10.0.402, runtime do harness 10.0.14, restore
com lock/auditoria. A fonte TUF fixada, seus 20 arquivos e o patch da Etapa 8
permanecem iguais; referências Tuf 1.1.1 e BC 2.7.0 preservadas. Não houve build
ou execução GF, upload de artefato, teste físico ou repetição das suítes 1–10.

## Aceitação técnica do G2

| Requisito técnico | Implementação existente | Alteração realizada | Teste/evidência | Resultado | Limitação restante |
| --- | --- | --- | --- | --- | --- |
| Raiz e intermediárias | Etapas 8/9: cadeia verificada; cache checkpointado ao final | Evento persistente por raiz N+1, threshold antigo e novo; revalidação da cadeia ao abrir | PERSIST-CHAIN; INTEROP-EXPIRED-INTERMEDIATE; três CRASH de ROOT | passed | Flush/processo não prova energia ou armazenamento físico |
| Timestamp/snapshot/targets | Cache agrupado após alvo completo | Registro por papel antes de retornar ao cliente, com referências e pisos | PERSIST-ON-METADATA-FAILURE; nove CRASH dos três papéis | passed | Registro parcial sem prova continua blocked |
| Rotação de timestamp/snapshot | EpochGuardedCache da Etapa 10 recusava mudanças | Nova época somente a partir de raiz autenticada; invalida ambos os papéis e estabelece referências novas | INTEROP-ROTATE-TIMESTAMP/SNAPSHOT | passed implementação; blocked interoperabilidade | Dois resultados divergem da referência; sem adoção operacional |
| Troca isolada de targets | Mudanças recusadas na Etapa 10 | Revalida chave nova mantendo pisos e referências, sem reset | INTEROP-ROTATE-TARGETS-HIGH/LOW | passed nos dois clientes | Apenas perfil fechado; outros perfis não qualificados |
| Antirreplay e cache restaurado | Pisos globais/cache único da Etapa 9 | Pisos ligados à época e diário; hints não autoritativos | OLD-VALID-METADATA; RESTORED-CACHE-HINTS; RECORD-MISSING/STALE/CORRUPT | passed | Restauração integral do domínio continua sendo G1 blocked |
| Admissão, UTC e concorrência | Cliente único/UTC fixo da Etapa 8 | Admissão persistente, UTC registrado e lease de confiança | UTC-FLOOR; CONCURRENCY; UNCHANGED-TIMESTAMP | passed | Provas anteriores de relógio reutilizadas, não reexecutadas |
| Autenticação→persistência | Falhas intermediárias não qualificadas | Tentativa fica cercada por bloqueio se avanço não foi registrado ou escrita é parcial | FAIL-AUTH-BEFORE-PERSIST; FAIL-PARTIAL-RECORD; UNKNOWN-CRASH-STAYS-BLOCKED | passed na recusa segura | Não infere término de I/O por ausência de processo/lock |
| Recuperação transacional | Recuperação de estado inerte nas Etapas 2/9 | Registro completo pode ser promovido/selado mediante prova supervisionada ligada a hash e sequência | 12 cortes: flushed/promoted/committed × quatro papéis | passed | Somente filho mantido pelo harness e fronteira síncrona concluída |
| Confiança separada da instalação | Metadados compartilhavam checkpoint do estado integrado | Diário TUF independente; transação original preserva seu próprio piso de versão | INTEGRATION-CONFIRM/BEFORE-INTENT/INTENT-PERSISTED/SELECTOR-REPLACED | passed | Sem ciclo GF real, IPC, migração ou recuperação de instalação por crash na composição nova |
| Interoperabilidade | Fixtures próprias, sem comparação externa | Oráculo Python e bytes públicos externos fixados | 15 comparações; EXTERNAL-CURATED-BYTES | 13 passed; 2 blocked | Não é suíte integral de conformance |
| Revisão/manutenção | Sem responsáveis/aceites demonstrados | Código, formato, hashes, divergências e requisitos preparados para avaliação | Este relatório, README e recibos | blocked | Falta revisor independente e aceite de responsabilidade de manutenção |

Os IDs abreviados da tabela têm prefixo `U11-`; resultados integrais, tempos,
versões e observações constam no [JSON Windows](evidencias/autoatualizador-etapa11-windows.json).
O formato de diário é interno de laboratório, não uma alteração do contrato
operacional. A única âncora admitida pelo cache novo é a fixture pública já
existente; nenhuma raiz operacional, chave ou cerimônia foi criada.

## Rotação e interoperabilidade: duas divergências abertas

Os cenários próprios usam serialização e assinatura nativas do **python-tuf
7.0.1**, securesystemslib 1.3.1, cryptography 50.0.2 e urllib3 2.8.0, somente
com seeds públicas preexistentes. O `Updater` Python sem alterações executou
refresh/download por fetcher inerte, sem conexão a serviço real. Versões e
hashes das fontes críticas instaladas foram confrontados com o commit
`49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15`.

Outro caso usa bytes públicos **não produzidos pelo nosso gerador**: root,
timestamp, snapshot, targets e file1.txt dos testes desse mesmo commit. Raiz
RSA-PSS, papéis Ed25519 e conteúdo do alvo foram aceitos pelos dois clientes.
Os bytes, hashes e licença estão no catálogo de vetores; esse ensaio é do
cliente/biblioteca bruta. Sua raiz não é admitida como autoridade GF.

As 13 concordâncias de aceitar/recusar incluem cadeia normal, assinatura e
threshold insuficientes/não autorizados, raiz final expirada, intermediária
expirada, intermediária ausente, referência adulterada, timestamp expirado,
troca de chave targets com pisos preservados, chave revogada e vetor externo.
Isso não prova equivalência completa de estado, persistência ou todos os
perfis TUF.

| Cenário após versão 100 | Protótipo .NET | Python Updater fixado | Classificação |
| --- | --- | --- | --- |
| Rotação apenas da chave timestamp | root 2; nova época; papéis 1/1/1 | timestamp 1; snapshot anterior 100; BadVersionNumberError | blocked |
| Rotação apenas da chave snapshot | root 2; nova época; papéis 1/1/1 | timestamp anterior 100; BadVersionNumberError | blocked |

A implementação segue a invalidação de timestamp e snapshot após mudança de
suas chaves prevista na [TUF 1.0.36, §5.3.11](https://theupdateframework.github.io/specification/v1.0.36/).
**Inferência dirigida de fonte**, ainda sem adjudicação independente: o caminho
[`Updater._load_root`](https://github.com/theupdateframework/python-tuf/blob/49eb1da48c58cf0cbb0f2e490f7ad39fbd8c1f15/tuf/ngclient/updater.py#L381)
observado mantém o outro arquivo antigo, que volta a influenciar a validação
nos métodos seguintes. Não se concluiu aqui que a referência ou o protótipo
esteja operacionalmente qualificado. As duas diferenças não foram convertidas
em aprovação. Essas transições são experimentos de fixture, sem autorização
para adoção ou distribuição GF.

## Falha inicial, reutilizações e limites

A [run inicial 40858921622](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40858921622),
código `eecb4c7f835c214f1484bde84cf1120d92362362`, falhou na geração de
vetores, antes de compilar ou executar C#. A desserialização de `SSlibKey`
consumia campos do dicionário de entrada e alterava as definições compartilhadas
de chaves públicas. Corrigido passando cópia profunda. O nome de Process.Start
no helper também foi qualificado preventivamente, sem alegar erro de compilação
que não chegou a ocorrer. Falha, correções e relatório bruto estão no
[recibo único da missão](evidencias/autoatualizador-etapa11.json). Não houve
retry automático nem teste aprovado repetido.

Doze interrupções reais de **processos próprios do harness** foram observadas,
com arquivos inertes e fronteiras completadas. O diário preservou avanços
selados/promovidos e recusou estado desconhecido. Não são testes de energia,
I/O arbitrário, dispositivo travado ou IPC de produto. Recibos locais pertencem
ao mesmo domínio restaurável: não resolvem G1. Contextos/sessões estrangeiras,
consumo operacional de I/O, chaves reintroduzidas em múltiplas épocas, perfis
com delegações/consistent snapshots e conformance integral não foram qualificados.

As provas anteriores de UTC imutável/cliente único, estado/transação e pacote
real GF/PE/unsigned/NTFS foram reutilizadas por hashes e referências, sem executar
suas baterias. O MSBuild apenas avaliou 142 itens Compile GF, zero protótipo.
O controlador descartou seus arquivos gerados; ambos os workflows tiveram
zero artefato. As fixtures inertes restantes pertencem ao runner descartável.

## Preservações e próximo trabalho mínimo

[Matriz G1–G8](evidencias/autoatualizador-etapa11-gates.json): todos **blocked**.
G2 recebeu persistência, recuperação, transições e comparação independente;
G6/G8 receberam novas provas de composição inerte/recusa segura. Nenhum gate
operacional foi promovido por essas subprovas.

Preservados BETA v5.13.11.66, fonte funcional 6df95fdd, entrega privada
40716548530/12828262039 e seu ZIP, PR aberta/Draft, main, onze workflows
anteriores, roadmap **27/42** com os mesmos IDs/done e homologação física parcial.
AVG/Windows, consentimento/privacidade, remoto desativado, PILOT_ENABLED=no,
GITHUB_EXPORT_ENABLED=no, drivers operacionais inativos, VLC só inventário e
WinGet COM consultivo permanecem intactos. Não houve build/execução GF,
manutenção de PC pessoal, credenciais/permissões/infraestrutura privada,
Worker/D1/DNS, chaves operacionais, merge, Release, deploy ou publicação.

**Menor próximo trabalho técnico:** reduzir as duas divergências a vetores
mínimos e obter revisão independente da invalidação normativa e dos pisos por
época, sem iniciar outra bateria ampla. Para eventual fechamento de G2 também
falta responsável de manutenção aceito. O uso operacional ainda depende de
âncora/custódia autorizadas, ciclo/IPC/prontidão/rollback GF e laboratório de
frescor/armazenamento dos demais gates. **Etapa 12 não iniciada. Encerrar após
salvar e verificar esta entrega na PR #65 Draft.**
