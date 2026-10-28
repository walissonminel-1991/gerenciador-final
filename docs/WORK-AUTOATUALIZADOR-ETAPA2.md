# Autoatualizador GF — Etapa 2: estado, NTFS e recuperação isolados

28/10/2026, America/Fortaleza. **Etapa 2 concluída no protótipo isolado.
Integração no executável e Etapa 3 NÃO INICIADAS.** PR #65 aberta/Draft.
Entrada `a2f4fdaa8e03d6705bb1fb2aea1fb713ec2b2d25`; contrato externo
`gf-self-update/1` da Etapa 0 preservado. Relatório/checkpoint da Etapa 1 lidos.

Código inicial: `5e9961b6fbba938ac8869d41dd9119b56a2057f8`.
Código corrigido e validado: `a57fd7c7677555e3d17fe1c4bb52c31ad43a8bf4`.
O fechamento posterior acrescenta resultados/checkpoint, sem mudar esse código.
Bibliotecas, lockfile, bootstrap público de teste e custódia da Etapa 1 intactos;
nenhuma chave/raiz operacional provisionada.

## Resultado verificável

| Execução | Resultado final | Identificação e limite |
| --- | --- | --- |
| Windows descartável | **61/61**, incluindo **14 NTFS** | [Run 40747226567](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40747226567), número 2/tentativa 1, job 123024125940, SHA a57fd7c7; windows-2022, Windows 10.0.20348, SDK 10.0.402, runtime .NET 10.0.14 |
| Linux descartável | **47/47** casos comuns | Mesmo código final, SDK 10.0.402, runtime .NET 10.0.13; sem alegação NTFS |
| Regressão dirigida da Etapa 1 | **66/66** | Runner/Require compartilhados foram estendidos; suíte isolada verificada sobre 5e9961b6, sem repetir testes do produto/físicos |
| Isolamento | **142 itens Compile do produto, zero do protótipo** | Avaliação MSBuild em ambos os ambientes; produto nunca compilado/executado |

Há **61 casos distintos da Etapa 2**, não 108: os 47 Linux são exercitados
também no Windows. Os 66 históricos/regressão, subcasos e execuções de correção
não são somados aos 61 nem ao roadmap. Build do protótipo sem avisos/erros.
Nenhum EXE/DLL/ZIP foi publicado ou enviado como artefato: lista de artefatos
das duas runs conferida vazia. Resultados JSON vêm do runner/log da execução,
não de uma contagem manual inferida pelo status verde.

[Resultados Windows](evidencias/autoatualizador-etapa2-windows.json) ·
[Linux](evidencias/autoatualizador-etapa2-linux.json) ·
[Regressão Etapa 1](evidencias/autoatualizador-etapa2-regressao-etapa1.json) ·
[Recibo](evidencias/autoatualizador-etapa2.json).

## Implementação e testes dirigidos

Detalhes reproduzíveis, formatos e limites:
[STATE-PROTOCOL](../prototipos/autoatualizador-seguro/STATE-PROTOCOL.md),
[fontes/runner](../prototipos/autoatualizador-seguro/README.md).
Tudo usa marcadores e textos inertes em temporários externos; não há um
instalador, helper ou inicializador operacional para o GF.

| Frente | Prova obtida nesta etapa | Limite preservado |
| --- | --- | --- |
| Estado persistente | Gerações CreateNew encadeadas, cache exato, JSON fechado/limitado, checksum, lacunas/corrupção/pendência recusadas, geração esperada e pisos monotônicos | Checksum não autentica contra reescrita integral pelo próprio usuário; sem fallback para piso antigo, rebootstrap ou compactação |
| TUF persistido | Cadeia assinada da fixture validada, cache salvo e carregado em outro processo; metadado antigo continua rejeitado | Cache não se autentica apenas pelo arquivo; revalidação mantém âncora incorporada, não há raiz operacional |
| Relógio | Início fixo + monotônico, retrocesso/tolerância, saltos durante ciclo, orçamento, expiração inclusive após intenção e piso observado conservador | Tuf 1.1.1 ainda lê relógio de sistema internamente; uniformidade dentro da biblioteca não comprovada |
| NTFS/TOCTOU | Arquivos e ancestrais abertos NOFOLLOW, IDs de 128 bits/volume, nlinks, caminho final, ACL consultiva, hashes no mesmo handle, ambos os bundles presos durante commit; junction/hard link/ADS/troca de identidade recusados | Arquivos de texto já preparados; sem extrair ZIP/PE e sem promessa contra administrador ou código arbitrário do mesmo usuário |
| Seletor/diário | Intenção e piso antes de File.Replace, backup, seleção nova pendente, confirmação sintética vinculada e uso único; erro de compartilhamento não vira fallback que ignore ACL | Nenhum processo GF aberto; sinal de prontidão é objeto de fixture, sem PID/IPC real ou prazo real de 30 s |
| Concorrência | Segundo helper, reabertura e limpeza simulada usam a mesma lease; lock persistente não é removido; kernel libera após crash; hard link do lock recusado | Participantes do protótipo, sem integração ao GF .66 ou implementação de limpeza real |
| Recuperação | Seis janelas distintas com processo filho morto e estado reaberto; rollback apenas ao predecessor da transação, uma recuperação lógica/idempotente, pisos altos preservados; backup inválido/seleção estranha param | Crash de processo e erro de I/O injetado não equivalem a queda de energia ou disco fisicamente cheio |
| Dados/consentimento | Marcadores de preferência, consentimento, legado e arquivo desconhecido preservados em todos os casos de diretório | Dados sintéticos; nenhum dado real ou arquivo da BETA acessado |

Os seis casos CRASH cobrem intenção persistida, seletor novo, seleção registrada,
intenção de recuperação, seletor anterior e recuperação concluída. LOCK-02 usa
novamente uma dessas janelas para provar liberação da lease; isso não cria uma
sétima janela distinta. Somente processos filhos da própria suíte são mortos.

Gerações mantêm separados pisos de metadados, maior sequência/versão aprovada e
bundle confirmado. Cancelar antes de confirmar não aprova release. Recuperar
o predecessor mantém os pisos altos; a candidata que falhou não pode voltar
como atualização nova. Um novo candidato precisa superar o limite aprovado.
Estado faltante/corrompido nunca autoriza reconstrução silenciosa de confiança.

## Defeito encontrado e correção comprovada

A [primeira rodada Windows 40746736121](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40746736121)
teve **39/60**: handles de diretório compartilhavam só leitura e bloqueavam
File.Move na promoção do diário. Os cenários transacionais seguintes falharam
por essa causa; não foram contabilizados como aprovados por equivalência Linux.
Outro assert esperava STATE_SIZE embora o guard NTFS já recusasse como NTFS_SIZE.
[Falhas e diagnóstico preservados](evidencias/autoatualizador-etapa2-windows-primeira-rodada.json).

A correção a57fd7c7 permite SHARE_WRITE nos diretórios, mantendo recusa de DELETE,
NOFOLLOW, IDs, ACL e revalidação. Arquivos dos bundles continuam negando WRITE
e DELETE. O teste confirma que promoção/ReplaceFile funcionam com ancestrais
presos e que escrita/rename do bundle continuam bloqueados durante intenção.
SHARE_WRITE em diretório não é descrito como bloqueio absoluto de mudança de
reparse; os limites de confiança/ACL e diretórios não vazios estão explícitos.
O assert foi alinhado à camada que recusou, preservando a rejeição.

A revisão também acrescentou CLOCK-09: tolerar até cinco minutos de correção
de parede não pode reviver alvo já expirado no piso UTC observado. A rodada
final possui 61 casos por esse novo teste. Não houve retry automático: duas
runs distintas, ambas tentativa 1, a segunda somente após a correção pontual.
Nenhuma suíte/consulta de instaladores ou CI do produto foi reexecutada.

## Segurança e continuidade

Preservada a **BETA v5.13.11.66**, fonte
`6df95fdd776644cd4b248b5cea61108974ca9ce9`, build privada 40716548530,
artefato 12828262039 e SHA-256 do ZIP
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`.
Nenhum download, recompilação, conversão, alteração ou publicação desse pacote.
Repositório privado, workflow, retenção e acessos sem leitura operacional ou
alteração. Main, branches históricas e PRs preservadas; sem merge/Release.

Branch de validação `work-autoatualizador-etapa2-validacao-20261028`, criada no
mesmo encadeamento da PR e mantida para evidência. Workflow novo exclusivo dessa
branch, contents:read, sem upload, token de aplicação, cron, rede de updates ou
dependência do canal privado. Workflows existentes do produto intactos. O
fechamento da PR usa skip ci para evitar nova build por alteração documental.

AVG e proteções Windows intactos, sem teste/quarentena/exceção; permissões só
consultadas no runner, nunca alteradas. O ensaio de fixture não comprova recusa
de elevação/IPC do produto. VLC só inventário sem consultas/ações/testes próprios;
WinGet COM opcional/consultivo/não bloqueante; drivers desativados; remoto
bloqueado, PILOT_ENABLED=no e GITHUB_EXPORT_ENABLED=no. Sem Worker/D1/DNS,
infraestrutura, coleta de dados, manutenção do PC FORTE ou testes físicos.
Avaliação física parcial preservada e roadmap **27/42**, mesmos IDs/done.

Gates restantes: durabilidade contra energia/armazenamento real, relógio uniforme
TUF, extração segura e validação PE/runtime real, IPC autenticado/saída/prontidão,
bootstrap/custódia operacional e integração. Não afirmar homologação TUF integral,
AVG, instalação ou recuperação operacional do GF a partir deste protótipo.
**Etapa 3 não iniciada; parar após salvar e conferir os resultados/checkpoint.**

Fontes primárias e decisões específicas do GF estão separadas no
[protocolo](../prototipos/autoatualizador-seguro/STATE-PROTOCOL.md). Também
consultada a [restrição de reparse em diretório não vazio](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fsa/4aeefef8-92c3-4abc-af7a-a610caf8a165).
[Checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).
