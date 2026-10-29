# Autoatualizador do GF — Etapa 0: contrato técnico e ameaças

28/10/2026, America/Fortaleza. **Etapa 0 concluída somente no desenho documental.**
Identificador: `GF_SELF_UPDATER_STAGE0_CONTRACT_AND_THREAT_MODEL`.
Contrato: `gf-self-update/1`, revisão documental 1. Nenhum cliente, helper,
chave, manifesto assinado, endpoint ou pacote novo foi produzido.

## 1. Base e limites desta decisão

Entrada: PR #65 aberta/Draft, branch `work-integracao-final-20261027`, HEAD
`c701ae16df5c1685967a9b8d299e02e811078f8d`, árvore
`bdf54e73ee54341ab8f51a14bc54a0f4f61c9aa8`.
O código funcional permanece na fonte `6df95fdd776644cd4b248b5cea61108974ca9ce9`.

A BETA v5.13.11.66 está entregue pelo canal privado existente. Sua build
40716548530, tentativa 1, artefato 12828262039 e ZIP portátil com SHA-256
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`
ficam preservados. Essa identificação vem do checkpoint vigente; não foi
recompilada, baixada novamente nem requalificada nesta Etapa 0.

A [avaliação física](WORK-INTEGRACAO-ETAPA9-PC-FORTE-RESULTADOS.md) é
**parcialmente concluída**, com aceitação funcional limitada e sem novo bug
reproduzível relatado. Seus resultados e lacunas formais permanecem como
registrados. Não repetir F9/T01–T12, manutenção ou consultas do PC por rotina.
Roadmap **27/42**, sem novos checkpoints promovidos.

Esta decisão define requisitos para uma implementação posterior. Não altera
o repositório `gf-beta-entregas`, workflow, artefatos, retenção de sete dias,
acessos, infraestrutura ou configuração conectada. Registro no Git da PR é
documental; não constitui publicação/distribuição de software.

## 2. Contratos verificáveis

"Deve" significa requisito de aceitação futuro, não recurso já implementado.

| ID | Requisito obrigatório |
| --- | --- |
| GFU-C01 | Atualizar somente o bundle do próprio GF, em uma linha BETA Windows x64; sem instalar silenciosamente, alterar aplicativos terceiros, drivers ou Windows. |
| GFU-C02 | Autenticar metadados por raiz de confiança previamente incorporada e assinatura; hash obtido junto de ZIP não estabelece autoria. Falha de confiança impede preparar/aplicar. |
| GFU-C03 | Vincular a assinatura a produto, canal, versão, sequência, perfil, fonte/build, pacote exato, tamanho, hashes e validade; nomes, tags, README e status de CI isolados não autorizam troca. |
| GFU-C04 | Bloquear downgrade/replay/mistura de releases, expiração e versão ambígua. Recuperação local é uma exceção transacional limitada, não uma atualização remota para versão anterior. |
| GFU-C05 | Limitar leitura, descompressão, memória, tempo, caminhos e espaço; nenhuma entrada externa decide comando, diretório de instalação ou nível de privilégio. |
| GFU-C06 | Extrair somente para área nova controlada; validar pacote completo e identidade dos arquivos antes de tocar na seleção ativa. |
| GFU-C07 | Preparar o bundle completo em diretório de versão distinto e trocar somente um seletor local após saída coordenada. Nunca copiar EXE/DLL/runtime parcialmente sobre versão em uso. |
| GFU-C08 | Manter versão anterior íntegra e diário persistente; recuperar após interrupção com estados identificáveis, sem apagar dados e sem presumir sucesso. |
| GFU-C09 | Aplicação, inicializador e helper coordenam exclusividade por instalação; autenticar processo/par IPC e limitar destinos. Não reutilizar o worker administrativo de manutenção para atualizar o GF. |
| GFU-C10 | Exigir ação para consultar/importar, consentimento antes de download pelo cliente e confirmação específica antes de aplicar/fechar/abrir. Cancelar ou adiar preserva a versão ativa. |
| GFU-C11 | Preservar preferências, consentimento, relatórios e arquivos desconhecidos; registros de atualização são locais, mínimos e sem identificadores/credenciais. |
| GFU-C12 | Separar autorização de acesso privado de confiança editorial. Sem PAT, senha, cookie ou chave privada permanente no EXE; sem tornar binário público para viabilizar download. |
| GFU-C13 | A primeira configuração aceita somente perfil local com diagnóstico remoto bloqueado. Um update não promove conectada, consentimento remoto ou permissões por efeito colateral. |
| GFU-C14 | Preservar AVG/proteções e permissões existentes; sem UAC, serviço, tarefa agendada, exclusão antivírus, remoção de marca de origem ou troca no reboot como contorno. |
| GFU-C15 | Estado de confiança, dados e bundles têm ciclos de vida distintos. Expiração de metadados bloqueia atualização, não desativa o aplicativo já instalado. |
| GFU-C16 | .66 não ganha autoatualização retroativa. Primeiro bootstrap será outra entrega manual autorizada; ensaios futuros começam em fixtures e só cobrem deltas pertinentes. |

## 3. Autenticidade, manifesto e bootstrap

Referência normativa escolhida para o protótipo: **TUF 1.0.36**, com os papéis
`root`, `targets`, `snapshot` e `timestamp`; fluxo de validação, JSON canônico,
assinaturas, versões/expiração e rotação seguem a especificação [R1]. Não
reimplementar primitivas criptográficas. Não declarar conformidade TUF sem
vetores interoperáveis e revisão da biblioteca escolhida.

Política própria do GF para esse perfil: Ed25519, sem negociação de algoritmo
por arquivo recebido; raiz com limiar 2 de 3 chaves offline, `targets` com
chave offline separada, demais papéis com chaves distintas. Nenhuma chave é
gerada/instalada agora. A seleção da biblioteca .NET e a demonstração de
custódia/recuperação são gates da etapa seguinte. Não reutilizar credencial
de acesso GitHub como chave editorial.

O bootstrap futuro incorpora a raiz pública inicial e identifica sua versão e
fingerprint no comprovante da entrega manual. Nunca confiar no primeiro
`root.json` vindo do ZIP, navegador ou rede apenas por estar ao lado do EXE.
O fingerprint inicial exige registro previamente confiável do mantenedor;
repeti-lo dentro do mesmo pacote não autentica o bootstrap. Sem essa base,
a adoção futura para, sem descobrir/aceitar outra chave automaticamente.
A .66 atual não contém esse cliente/âncora e continua intocada. Não tratar seus
recibos existentes como metadados assinados nem acrescentar assinatura falsa.

Chaves substituídas e raízes intermediárias seguem [R1]; perda/comprometimento
do limiar raiz exige recuperação por nova entrega manual autenticada, sem
botão "ignorar assinatura". Não colocar chaves privadas de raiz/targets em
repositório, EXE, log ou no mesmo job que compila o produto.

### 3.1 Descrição assinada do alvo

O alvo TUF é o **ZIP portátil interno**, com `length` e `hashes.sha256`.
Sua extensão `custom.gf`, fechada por `schema=gf-self-update/1`, deve conter:

| Grupo | Campos e restrições |
| --- | --- |
| Identidade | `product=gerenciador-final`, `channel=beta`, `architecture=win-x64`; strings fixas, comparação ordinal. |
| Ordenação | `version`: quatro inteiros decimais 0–65535, sem zeros à esquerda; `release_sequence`: inteiro positivo até 2^53−1, crescente e nunca reutilizado. |
| Origem | `source_commit` e `controller_commit`: 40 hex minúsculos; `run_id`, `artifact_id`: strings decimais canônicas; `run_attempt`: inteiro positivo. IDs não são comandos. |
| Perfil | `runtime_profile=local`, `remote_diagnostics=false`, `requires_user_confirmation=true`, `qualification=beta-test-not-homologated`. Outro valor requer contrato/cliente e aprovação próprios. |
| Compatibilidade | `min_updater_protocol`: inteiro; `data_schema`: inteiro compatível; `entrypoint=GerenciadorFinal.exe`. Protocolo superior ou migração de dados incompatível bloqueia aplicação. |
| Conteúdo | `content_manifest_sha256`: hash dos bytes de SHA256SUMS.txt; `file_count` e `expanded_bytes`: inteiros positivos dentro dos limites do cliente. Todos os arquivos devem integrar o contrato fechado derivado das dependências/licenças. |
| Contêiner | `delivery_repository=walissonminel-1991/gf-beta-entregas`; `container_length`, `container_sha256`, `payload_member`, `artifact_expires_at`. O membro é um nome simples permitido, não caminho livre. |
| Assinatura do PE | `authenticode_mode=beta-unsigned` no perfil inicial, com informação visível; um futuro modo obrigatório exige política de publicador previamente incorporada. Falha de verificação de PE assinado nunca vira aceitação como unsigned. |
| Texto | `release_notes`: texto simples UTF-8, até 4 KiB, sem HTML, scripts, comandos ou links executados automaticamente. |

Inteiros de controle não aceitam frações, sinais textuais ou overflow. Recusar
chaves JSON duplicadas em qualquer nível, UTF-8 inválido, campos GF desconhecidos,
schema/protocolo não suportado, assinatura duplicada contada como chave distinta
ou tentativa de alterar limites do cliente. O objeto assinado é interpretado
somente depois de validado conforme [R1].

**Sem ciclo de hash:** os metadados assinados futuros ficam separados do
artefato que descrevem. O contêiner Actions é fechado antes de assinar seu
digest. Não inserir no mesmo ZIP uma assinatura que precise incluir o hash
desse próprio ZIP. Essa organização futura não modifica o canal nesta sessão.

O contêiner atual e o portátil têm hashes diferentes. Primeiro conferir o hash
assinado do contêiner; depois o único membro portátil designado; então o hash
assinado do portátil, seu manifesto de arquivos e conteúdo. Não usar o digest
do contêiner como hash de GerenciadorFinal.exe. Os três recibos existentes
continuam evidências, não executores nem autoridades de confiança.

### 3.2 Versões, tempo e antirreplay

Comparar versão numericamente por tupla de quatro componentes, não como texto
ou semver de três componentes. Exigir versão e sequência superiores às já
aprovadas para instalação. Mesma versão com bytes diferentes é conflito, exige
nova versão; mesma versão/hash ativa resulta em "já instalada", sem escrita.

Manter separados: versões TUF confiáveis, maior sequência de release aprovada
e versão atualmente ativa. Confirmar uma instalação grava intenção e limite de
sequência durável antes de mudar o seletor. Cancelamento não aprova sequência.
Recuperação só pode apontar para o bundle anterior da mesma transação, com
identidade/hashes previamente preservados; não reduz limites de confiança.

Política GF inicial para futuros metadados: validade máxima após emissão de
365 dias para raiz, 14 dias para targets, 7 dias para snapshot e 72 horas para
timestamp; nenhum ciclo utiliza alvo cujo artefato já expirou. Metadados frescos
podem ser necessários entre releases, independentemente da meta de cinco dias.
Esses prazos são tetos editoriais; o cliente também recusa `expires` além do
horário fixo de início mais o teto do papel. Datas exigem UTC com segundos e
sufixo `Z`, sem ambiguidade de formato. No mesmo ciclo todos os papéis usam
o mesmo horário inicial e o fluxo de expiração/rotação de [R1].
Capturar UTC no início; se o relógio retroceder mais de cinco minutos frente ao
último horário confiável persistido, suspender a atualização e informar revisão
manual. Não alterar relógio/Windows. Isso limita replay, sem alegar resistência
a administrador que controle relógio e estado local, nem disponibilidade offline.

## 4. Acesso privado e consentimento

**Primeiro transporte previsto: importação explícita de arquivos previamente
baixados pelo mantenedor no GitHub autenticado**, incluindo metadados futuros
válidos. Navegador cuida da autenticação; o GF não lê sessão/cookies nem pede
PAT. A seleção de arquivo não autoriza aplicá-lo. Não há consulta periódica ou
download automático na configuração inicial.

Artefatos privados exigem acesso autenticado; a API fornece redirecionamento
temporário [R2, R3]. Um link salvo não equivale a endpoint permanente. Se faltar
metadado válido, autorização ou artefato disponível, informar "atualização não
disponível/validável" e manter a versão atual; não publicar fallback, obter
token amplo, renovar retenção ou recriar artefato automaticamente.

Um adaptador de rede futuro fica fora do primeiro incremento: deverá comprovar
autenticação efêmera e escopo mínimo no mesmo canal, HTTPS, origem fixa,
redirecionamentos limitados e destino permitido, sem reenviar Authorization a
outro host, sem aceitar URL de download/linha de comando arbitrária no manifesto
e sem URLs assinadas em logs. Exige desenho próprio antes de habilitação.

Após validação completa, a confirmação deve mostrar versão atual/nova, origem,
perfil local, condição BETA, tamanho, integridade e necessidade de fechar o GF;
opções aplicar, adiar ou cancelar. Solicitar fechamento normal apenas após
confirmação. Manutenção em andamento, worker ativo, outra instância ou contexto
elevado impedem commit. Nunca matar processos terceiros ou usar o consentimento
da atualização como consentimento para diagnóstico remoto.

## 5. Limites e validação do pacote

Limites fixos de `gf-self-update/1`: metadados timestamp até 64 KiB, snapshot
256 KiB, raiz/targets 512 KiB cada, até 32 transições de raiz por tentativa;
contêiner até 256 MiB, portátil até 256 MiB, expansão total até 768 MiB, até
2.048 arquivos e um arquivo até 256 MiB. Contêiner: até oito entradas e um único
ZIP portátil designado; não percorrer arquivos aninhados recursivamente. Toda
entrada aceita tem nome e finalidade previstos pelo contrato do contêiner.

Validar tamanhos reais lidos/extraídos, CRC e hashes em streaming, não apenas
valores de cabeçalho. Orçamento de validação/extração: dois minutos canceláveis;
falta de espaço, timeout ou tamanho excedido abortam antes do commit. Reserva
conservadora: bytes ainda necessários para contêiner + portátil + bundle novo
+ eventual cópia do bundle anterior + 64 MiB; não remover backup para caber.

Recusar Zip Slip (`..`, absoluto, UNC, dispositivo, ADS), nomes reservados,
duplicatas/casefold, separador ambíguo, sufixos ponto/espaço, reparse points,
links simbólicos/hard links, itens especiais e ZIP criptografado. Paths relativos
ASCII portáveis, até 220 caracteres e caminho final até 240; sem habilitar
long paths no Windows. Não aceitar scripts de instalação/migração ou comandos.

Extrair em diretório criado exclusivamente para a operação (`CreateNew`), nunca
truncar arquivo preexistente. Ancestrais, origem e destino precisam de validação
por handle/volume/file ID, com proteção contra troca de identidade e links até
o uso. Normalizar string de caminho uma vez não fecha TOCTOU. Links podem
alterar o destino de APIs de arquivo do Windows [R5].

Conferir SHA256SUMS, conjunto exato de dependências, licenças, recursos, runtime
self-contained, PE AMD64, versão/manifesto Win32 e metadata do perfil. Inspeção
do candidato deve ser estática: não executar EXE, carregar DLL do pacote,
consultar fontes do produto ou iniciar rede para decidir se é seguro. O
verificador existente `scripts/verificar_pacote.py` é referência do contrato de
distribuição; não é cliente de update pronto nem deve ser afrouxado.

## 6. Troca, dados e recuperação

Escolha arquitetural para protótipo: instalação gerenciada futura **em pasta
nova**, com inicializador/helper estáveis fora de `versions/`, bundles completos
em `versions/<sequence>-<digest>/`, estado em `state/` e seletor `active.json`.
Nomes são lógicos, não diretórios criados nesta sessão. A pasta portátil .66
existente fica inteira; não é convertida ou sobrescrita automaticamente.

Primeiro suporte: NTFS em unidade local fixa gravável pelo mesmo usuário,
sem elevação e sem permissões de escrita amplas de outros usuários. Recusar
UNC, mídia removível, pastas redirecionadas/links, Program Files sem direito de
escrita e filesystem fora do perfil; orientar instalação manual em outra pasta,
sem alterar ACLs, proprietário ou configuração do Windows.

Inicializador/helper do bootstrap não se substituem durante esta primeira
transação. Atualização desse componente/protocolo exige outro bootstrap manual.
Manifesto remoto não escolhe caminho do helper, executável alternativo, destino,
argumentos ou pedido de UAC. IPC local tem nonce de uso único, usuário/par PID
verificados pelo kernel e mensagem limitada à operação preparada. Nome de pipe
ou PID passado por argumento isoladamente não autentica o par.

| Estado | Condição e próximo passo | Falha/cancelamento |
| --- | --- | --- |
| Ocioso | Usuário inicia importação/consulta suportada | Sem tarefa ou escrita recorrente |
| Validando | Cadeia de confiança, validade, versão e limites; selecionar apenas bytes autenticados | Não oferecer aplicar; manter ativo |
| Preparando | Área nova, bundle completo, hashes/contratos e espaço conferidos | Cancelar e preservar ativo/anterior |
| Pronto | Mostrar plano completo; aguardar confirmação específica | Adiar/cancelar não fecha GF |
| Aguardando saída | Exclusividade por instalação; fechar normalmente todas as instâncias/filhos conhecidos | Aguardar/revisar; sem force-kill |
| Intenção persistida | Diário com IDs locais, versão/hash anterior/novo, fase, seleção anterior e nonce; gravar/flush antes do seletor | Próxima abertura entra em recuperação |
| Seleção nova | Helper revalida identidades/bundles; trocar somente active.json no mesmo volume | Reconciliar diário e IDs; não presumir onde estão os arquivos |
| Confirmando | Abrir somente se autorizado; candidato devolve prontidão ligada à transação e PID | Sem prontidão em 30 s ou falha: selecionar anterior verificada uma única vez |
| Confirmado | Persistir conclusão; manter backup; nenhuma limpeza de dados do usuário | Não confundir UI pronta com homologação funcional/AVG |
| Recuperação | Validar diário e ambos os bundles; retomar estado seguro, com informação ao usuário | Estado/backup inconsistente: parar e orientar uso manual da .66 preservada |

O seletor é relativo e só aponta para bundle local autenticado e integral.
Inicializador e helper usam a mesma coordenação para impedir nova abertura
durante commit/recuperação. Sem copiar DLLs ao vivo ou renomear duas pastas como
se fossem uma única operação atômica. Estado recuperável significa seleção
determinística de bundle completo, não transação atômica de centenas de arquivos.

`ReplaceFileW` é candidato para o seletor, com erro tratado, backup e mesmo
volume; a API não oferece `REPLACEFILE_WRITE_THROUGH` suportado [R4]. Não usar
flags que ignorem erros de ACL nem prometer durabilidade contra queda de energia
com uma chamada de API. Etapa posterior deverá demonstrar diário/flush,
reconciliação e todas as janelas de falha em ambiente descartável antes de
habilitar aplicação. Locks de leitura não podem impedir o próprio rename:
o protótipo deverá comprovar compartilhamentos/handles corretos sem abrir
janela de troca de caminho.

Uma falha pode restaurar a seleção anterior como recuperação da confirmação
original; não instalar release diferente ou reiniciar outra versão sem o
consentimento correspondente. Reinício após crash apresenta recuperação antes
de executar candidato pendente. Não criar loop automático de retry/rollback.

Preferências/consentimento e diagnósticos permanecem fora dos bundles. A fonte
atual usa LocalAppData para diagnósticos e estado de telemetria, com fallback
de diagnóstico em temporários. Não migrar, apagar, restaurar snapshot antigo
desses dados ou reativar consentimento durante rollback. Migração irreversível
de schema está fora de `gf-self-update/1`.

Manter pelo menos o bundle anterior até confirmação e revisão explícita. Limpeza
futura só pode remover bundles/staging pertencentes ao atualizador, por registro
e identidade verificados; nunca ativos/em uso, arquivos desconhecidos ou a pasta
legada .66. Logs locais de atualização: códigos de etapa/erro, versões, hashes e
ID técnico aleatório por transação, sem usuário, inventário, path pessoal, URL
assinada ou credencial; retenção local proposta de sete dias. Não tocar no
expurgo obrigatório de dados vencidos ou retenções de outros subsistemas.

## 7. Análise de ameaças

Ativos: execução de código, raiz/chaves públicas confiáveis, bytes aprovados,
dados/consentimento locais, versão recuperável e exclusividade do canal privado.
Fronteiras: GitHub/navegador → arquivo importado; metadados → parser/verificador;
ZIP → filesystem; processo GF → helper/inicializador; estado preparado → ativo.
O assinante/editor confiável pode ainda aprovar código defeituoso: assinatura
comprova autorização editorial, não ausência de malware ou defeitos.

Adversário considerado: rede/mirror hostil, arquivo adulterado, replay de release,
processo concorrente sem autoridade e caminhos/conteúdo locais manipuláveis.
Comprometimento do administrador/kernel, do próprio usuário com execução
arbitrária e do limiar de chaves confiáveis é limite de confiança, não proteção
prometida por este desenho. Raiz/estado locais apagados ou adulterados não
autorizam reinicialização silenciosa de confiança.

| ID | Ameaça e impacto | Controle obrigatório | Limite residual |
| --- | --- | --- | --- |
| GFU-T01 | ZIP + hash substituídos; execução arbitrária | C02/C03: cadeia assinada externa ao payload e digest exato | Editor/chave autorizada comprometida continua crítica |
| GFU-T02 | Downgrade, replay ou ordenação textual errada | C04: tupla numérica, sequência e versões confiáveis persistidas | Restauração hostil do estado do mesmo usuário não é excluída pelo SO |
| GFU-T03 | Freeze, relógio incorreto, cache/artefato expirado | C04/C15: prazo, relógio observado, estado e mensagem de indisponibilidade | Offline/relógio não confiável não dão prova de versão mais recente |
| GFU-T04 | Misturar fonte/build, canal, arquitetura ou perfis | C03/C13: identidade fechada e conferir metadata dos bytes | Bundle assinado precisa de revisão de código própria |
| GFU-T05 | Roubo de token GitHub ou exposição de binário | C12: importação por navegador autenticado, sem ler credenciais | Conta/navegador comprometido afeta acesso; não confere assinatura editorial |
| GFU-T06 | URL/redirect hostil, SSRF ou vazamento de Authorization | C12: transporte inicial sem rede; futuro adaptador limitado | Rede automática não está liberada por este contrato |
| GFU-T07 | Zip Slip, ADS, junction, hard link, case collision | C05/C06: nomes fechados, criação exclusiva e identidade por handle | Prova de corrida/NTFS permanece gate de implementação |
| GFU-T08 | ZIP bomb, stream infinito, disco cheio ou metadado gigante | C05: limites de bytes/entradas/tempo/espaço; cancelamento | Pode ocorrer recusa de atualização, mantendo aplicativo atual |
| GFU-T09 | TOCTOU após hash ou troca de diretório/helper | C06/C09: handles/IDs presos, mesma origem e revalidação | String de path ou checagem isolada não basta |
| GFU-T10 | DLL/runtime misturados ou arquivo em uso | C07: bundles separados e único seletor após saída | Suporte restrito à instalação gerenciada futura |
| GFU-T11 | Crash/queda de energia entre intenção e confirmação | C08: diário, backup íntegro, fases/reconciliação sem retry infinito | Durabilidade precisa ser demonstrada; API isolada não a prova |
| GFU-T12 | Duas atualizações, reabertura durante commit, limpeza concorrente | C07/C09/C15/C16: coordenação compartilhada entre todos os participantes | Instância que não conhece protocolo, como .66, não é alvo gerenciado |
| GFU-T13 | IPC/argumento forjado ou privilégio administrativo indevido | C09/C14: identidade do par, operação fixa, contexto não elevado | Sem serviço/UAC para "resolver" acesso negado |
| GFU-T14 | Rollback/limpeza apaga dados, logs ou preferências | C08/C11/C15: só bundles registrados; sem migração irreversível | Sem espaço/backup íntegro a operação para |
| GFU-T15 | Instalação silenciosa ou consentimento reaproveitado | C10: consultar/importar, baixar e aplicar são decisões distintas | Autorizar update não autoriza manutenção nem coleta |
| GFU-T16 | Update ativa remotos, drivers ou muda política de segurança | C01/C13/C14: perfil local fechado e limites incorporados no cliente | Fase conectada exige revisão/autorização própria |
| GFU-T17 | CI, chave editorial ou raiz comprometida; revogação insegura | C02/C03: separação de papéis/custódia, revisão por SHA, bootstrap e rotação | Quórum raiz comprometido exige recuperação fora da cadeia |
| GFU-T18 | AVG bloqueia, assinatura PE falha ou falso sucesso de saúde | C08/C14: manter bloqueio, sem exceção/fallback inseguro, prontidão limitada | TUF/hash/prontidão não equivalem a homologação AVG/física |

## 8. Critérios de aceitação para a próxima etapa, não executados agora

Os casos abaixo são especificações de fixtures/mocks/diretórios descartáveis.
Não são testes aprovados, não somam ao roadmap e não repetem a homologação .66.

| Caso futuro | Evidência exigida | Ameaças |
| --- | --- | --- |
| GFU-V01 | Payload adulterado com manifesto de hashes recalculado continua rejeitado | T01 |
| GFU-V02 | `5.13.11.9` não vence `5.13.11.66`; mesma versão/outros bytes e sequência antiga rejeitadas | T02 |
| GFU-V03 | Expiração, replay e relógio regressivo bloqueiam update sem impedir GF ativo | T03 |
| GFU-V04 | x86, produto/canal/perfil errado e origem divergente não chegam a aplicar | T04/T16 |
| GFU-V05 | Importação não lê token, sessão ou cookie; arquivo sem cadeia confiável falha | T05 |
| GFU-V06 | Mocks de rede não permitem origem livre, redirect perigoso ou repasse de Authorization | T06 |
| GFU-V07 | Traversal/ADS/nomes reservados/duplicatas/links recusados sem escrita externa | T07 |
| GFU-V08 | Limites reais de stream/expansão/disco/timeout são respeitados | T08 |
| GFU-V09 | Troca de origem, ancestrais e helper entre verificar e usar é recusada | T09 |
| GFU-V10 | Arquivo bloqueado nunca produz seleção de bundle parcial | T10 |
| GFU-V11 | Interrupção injetada em cada fase recupera seletor/diário coerentes | T11 |
| GFU-V12 | Dois helpers/instâncias e reabertura concorrente não intercalam commit/limpeza | T12 |
| GFU-V13 | IPC/par PID/usuário falso, argumento livre e contexto elevado recusados | T13 |
| GFU-V14 | Cancelamento/rollback preservam preferências, consentimento e arquivos desconhecidos | T14/T15 |
| GFU-V15 | Assinaturas duplicadas, chave não autorizada, rotação e perda de raiz têm resultado definido | T17 |
| GFU-V16 | Quarentena/erro de confiança PE/ausência de prontidão não viram sucesso | T18 |
| GFU-V17 | Bootstrap sem âncora incorporada e tentativa de converter .66 em alvo gerenciado falham | T01/T12/T17 |
| GFU-V18 | JSON duplicado/inválido, campo extra, overflow e protocolo/schema superior recusados | T01/T04/T08 |

**Gates antes de implementar em produto:** escolher biblioteca/POUF verificável;
comprovar bootstrap e custódia; demonstrar filesystem/IPC/recuperação e rejeições
acima; decidir entrega futura de metadados assinados sem modificar o canal por
inferência; revisar delta de código/novo SHA e CI aplicável antes de gerar outra
candidata. Não criar chave, publicar metadados ou executar esses testes agora.

Cadência preservada: uma linha BETA; meta de cinco dias condicionada à aprovação,
consolidação dos diagnósticos autorizados, triagem, investigação, correções e
testes. Revisão de limpeza dois dias após publicação planejada, independente do
expurgo obrigatório; futuras versões no mesmo canal privado com retenção própria
adequada. Nenhuma agenda, mudança de retenção ou atualização foi configurada.

## 9. Referências e trilha

Fontes primárias consultadas em 28/10/2026. Parâmetros e layout específicos do
GF acima são escolhas deste contrato, não garantias dadas pelas referências.

- R1: [TUF Specification 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/).
- R2: [GitHub — download de artefatos](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).
- R3: [GitHub REST — download de artefato](https://docs.github.com/en/rest/actions/artifacts#download-an-artifact).
- R4: [Microsoft — ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).
- R5: [Microsoft — efeitos de links nas operações de arquivo](https://learn.microsoft.com/en-us/windows/win32/fileio/symbolic-link-effects-on-file-systems-functions).
- R6: [Microsoft — WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust). Se esse modo for utilizado, somente retorno zero comprova sucesso; não usar teste genérico de HRESULT.

Trilha de origem: `BetaRuntimePolicy.cs`, `app.manifest`, `InstallerFileGuard.cs`,
`PipePeerVerifier.cs`, `DiagnosticTrace.cs`, `AnonymousTelemetry.cs`,
`scripts/verificar_pacote.py` e `docs/USO-PORTATIL.md`; leitura dirigida, sem
execução. Helpers existentes são referências de restrições, não autorização para
adaptar limpeza, elevar o atualizador ou reaproveitar exclusão residual.

[Checkpoint do autoatualizador](WORK-AUTOATUALIZADOR-CHECKPOINT.md) ·
[Checkpoint da integração](WORK-INTEGRACAO-CHECKPOINT.md) ·
[Recibo da Etapa 0](evidencias/autoatualizador-etapa0.json).
