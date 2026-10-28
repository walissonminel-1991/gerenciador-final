# Etapa 2 — protocolo experimental de estado e recuperação

Contrato externo preservado: `gf-self-update/1`. Formatos internos de fixture:
`gf-state-fixture/2` e `gf-selector-fixture/2`. **Não são formatos de instalação
operacional. Nenhum comando aplica/abre o GF. Etapa 3 não iniciada.**

## Estado persistente e relógio

Uma geração contém identificação aleatória da instalação, contador, hash da
geração anterior, pisos TUF, maior sequência aprovada, maior versão aprovada,
UTC observado, bytes exatos do cache, bundle confirmado e diário transacional.
O cache não vira âncora autoassinada: ao reutilizá-lo o validador da Etapa 1
reconstitui a cadeia desde o bootstrap incorporado. O teste STATE-11 verifica
esse caminho em outro processo. Persistir JSON não comprova assinaturas.

Cada registro é JSON fechado, com payload base64 e SHA-256; leitura limitada
a 2 MiB, profundidade 32, até 64 gerações nesta fixture. Escrita CreateNew em
arquivo pendente, WriteThrough/Flush(true), seguida de promoção sem overwrite.
Não há truncamento de registros anteriores. Geração ausente/desconhecida,
registro parcial, checksum inválido ou arquivo pendente bloqueiam o estado;
nunca se escolhe uma geração antiga para contornar a falha. Não há compactação,
rebootstrap automático ou limpeza de dados. O limite 64 exige encerrar o ensaio,
não autoriza apagar registros para aceitar outra atualização.

SHA/encadeamento detectam acidentes e regressões dentro da cadeia disponível.
**Não autenticam o estado contra processo arbitrário do mesmo usuário,
administrador, restauração integral de snapshot ou remoção do sufixo inteiro
do diário.** Sem contador externo/hardware não se promete antirrollback local
absoluto. Esse limite já integra as ameaças T02/T03 da Etapa 0.

CycleClock captura UTC inicial e contador monotônico do TimeProvider; bloqueia
retrocesso >5 min frente ao último observado, mudança de parede >5 min frente
ao monotônico, orçamento >120 s e expiração na confirmação do commit. O horário
efetivo usa o maior entre parede e início+tempo decorrido. Nenhum ajuste para
trás estende validade ou reduz o piso persistido. Intenção já persistida mantém
o piso mesmo se o alvo expirar antes da troca. A seleção confirmada não depende
do frescor de metadados para mera inspeção. Nenhum relógio Windows é alterado.

**Limite da biblioteca:** Tuf 1.1.1 consulta DateTimeOffset.UtcNow internamente
e não oferece TimeProvider na API fixada. O relógio determinístico desta etapa
cobre a política/commit da fixture; não prova relógio uniforme dentro do cliente
TUF. Resolver/revisar essa fronteira continua gate antes de integrar no produto.

## Seletor, diário e recuperação

Bundles sintéticos são textos já preparados em `versions/<seq>-<sha>/`.
O seletor só contém schema, ID local e BundleRef fechado (seq, versão, hash,
file ID). Não admite path, comando, EXE alternativo ou argumentos livres.
O bundle .66 real nunca é inspecionado, convertido ou substituído.

| Fase | Escrita anterior à ação seguinte | Recuperação explícita |
| --- | --- | --- |
| Sem pendência | Seletor deve coincidir com bundle confirmado | Inspecionar somente confirmado |
| intent | Diário e piso de sequência/versão antes do seletor | Aceitar apenas seletor anterior ou candidato da transação |
| selected | Seletor novo integral; confirmação continua pendente | Nova abertura da fixture exige recuperação, não abre candidato |
| confirmed | Confirmação sintética ligada a tx/nonce/hash | Seleção nova confirmada, sem repetir sinal |
| rollback_intent | Registrar recuperação antes de selecionar anterior | Concluir a mesma recuperação; não criar outra transação |
| rolled_back | Seleção anterior exata; pisos continuam altos | Idempotente; sem retry/novo lançamento |

File.Replace, com backup único e ignoreMetadataErrors=false, só troca o seletor
no mesmo diretório/volume. Não existe fallback que ignore ACL, mate processos,
sobrescreva DLLs em uso ou selecione bundle desconhecido. Falha/corrupção/arquivo
ausente conserva evidência e interrompe o ensaio; não limpa backup para caber.
Handles do próprio seletor ficam fechados na chamada ReplaceFileW; ancestrais
e os dois bundles continuam presos até o registro da transição terminar.

Seletor estranho à transação ou bundle anterior/candidato inconsistente impede
recuperação. A recuperação usa exclusivamente o predecessor registrado, sem
reduzir versões TUF, sequência ou versão máxima aprovada. Dados e consentimento
fora de versions/ não são migrados, restaurados ou apagados.

FixtureReadiness é **somente um objeto de teste**. Vinculação tx/nonce/hash e
uso único são exercitados; identidade de PID/usuário por IPC, prontidão real,
prazo real de 30 s e execução do GF não são implementados ou comprovados.

## NTFS e concorrência

Somente Windows: perfil de volume local fixo NTFS, path <=240, sem ADS/UNC.
Abrir cada ancestral antes do descendente com CreateFileW, OPEN_REPARSE_POINT,
BACKUP_SEMANTICS e compartilhamento de leitura, negando escrita/exclusão.
Validar atributos/tipo, caminho final, volume/FileIdInfo de 128 bits e nlinks=1
para arquivos. Ler/hash no mesmo handle e revalidar; não normalizar uma string
e reabrir o arquivo para decidir quais bytes foram aprovados.

Os handles de ambos os bundles ficam vivos durante intenção/troca/registro.
ACL e proprietário são consultados por GetSecurityInfo; sem alterar permissões.
Política conservadora recusa grants de escrita a SIDs fora do usuário atual,
SYSTEM e Administrators, DACL ausente e ACE não suportada. Testes de ACL hostil
usam descritores em memória, sem aplicar ACL ao sistema. Não há promessa de
resistência a administrador ou código arbitrário do próprio usuário.

Todos os participantes simulados usam o mesmo arquivo de lease persistente,
aberto exclusivamente pelo kernel. O arquivo não é removido e PID não decide
se o lock é válido. No Windows o objeto do lock também é aberto NOFOLLOW,
recusando reparse/hard link. Os ancestrais ficam presos antes dessa abertura.
No Linux, FileShare.None só exercita coordenação entre participantes .NET;
não representa as garantias NTFS.

Seis testes matam **apenas processos filhos criados pela suíte**, em janelas
posteriores a intenção, seletor, seleção e recuperação, e reabrem o estado.
Outros testes injetam erro de escrita/flush e seletor. São provas de crash de
processo e tratamento de I/O: **não são desligamento físico, falha de energia,
disco realmente cheio, flush de diretório/volume ou garantia do hardware**.
ReplaceFileW não tem WRITE_THROUGH suportado. Não abrir volume nem elevar
privilégios para produzir uma alegação artificial de durabilidade.

## Execução reproduzível e limites

```sh
python run_stage2_tests.py --report /temporario/gf-u2-results.json
```

SDK 10.0.402; dependências/lockfile da Etapa 1 intactos. Fixtures assinadas
geradas pelo mesmo assinante independente, com seeds públicas exclusivas de
teste. Diretórios/test DLLs/obj/bin temporários externos; fontes `.cs.txt`.
O runner avalia os itens Compile do GF sem compilá-lo ou executá-lo.

Workflow novo e restrito à branch de validação usa windows-2022 e só lê o
repositório. Resultados ficam nos logs/Markdown/JSON, sem upload de DLL/ZIP,
artefato, Release ou acesso ao canal privado. A atualização final da PR usa
skip ci para não reexecutar builds/instaladores do produto por este ensaio.

Sem extração de ZIP em disco, PE/runtime/licenças reais, integração/UI, IPC
autenticado, limpeza operacional, bootstrap/custódia de produção ou rede de
updates. Estes testes não homologam a BETA, AVG, instalação ou TUF integral.

Fontes primárias consultadas em 28/10/2026:
[CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew),
[GetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle),
[GetFileInformationByHandleEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex),
[ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew),
[FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers),
[GetSecurityInfo](https://learn.microsoft.com/en-us/windows/win32/api/aclapi/nf-aclapi-getsecurityinfo)
e fonte TUF fixada na Etapa 1. Parâmetros/estado acima são escolhas experimentais
do GF, não garantias de durabilidade dadas por essas APIs.
