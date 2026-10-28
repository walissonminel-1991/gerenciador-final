# Etapa 6 — dossiê mínimo para desbloqueio

Este documento prepara a revisão de evidências futuras. **Não é manifesto de
release, extensão de gf-self-update/1, autorização operacional ou ensaio
executado.** Nenhuma entrada abaixo está marcada como coletada nesta etapa.
O relatório e a matriz de dependências indicam o que depende de decisão externa.

## Registro comum a toda evidência

Registrar ID E6, gate/condição, fonte/commit/tree, hash dos arquivos de entrada,
versões de ferramenta/OS/arquitetura, ambiente descartável e perfil de privilégio,
run/tentativa, procedimento efetivamente executado, observação bruta, resultado
esperado/obtido, artefatos/hash/tamanho quando existentes e limites não ensaiados.
Separar passed, failed, blocked e not-executed. Autor/parecer por papel ou alias
autorizado, sem publicar identidade pessoal desnecessária. Hash de observação
garante rastreabilidade, não autoria editorial ou frescor por si só.

Usar apenas dados inertes/autorizados. Sem privadas de assinatura, recuperação,
senha de conta, PAT, cookie, URL temporária de download ou inventário pessoal.
Receber material público de confiança não autoriza aplicá-lo; assinatura do
próprio pacote não autentica o fingerprint inicial. O dossiê deve apontar o
registro previamente confiável que estabelece essa base.

## E6-STORAGE — durabilidade

Pré-condição: VM descartável identificada tecnicamente, disco NTFS persistente
e operador/controlador externo de falha autorizado. Registrar imagem e disco
de origem, virtualização/controladora/cache e escopo da afirmação: falha virtual
ensaiada não homologa toda pilha física de hardware. Preservar a .66 e dados reais
fora do laboratório. Não habilitar privilégios/AV exclusion para o aplicativo.

O operador deve cortar a execução da VM sem encerramento normal e reiniciar
**do disco persistido**, sem restaurar RAM/checkpoint que esconda a perda. No
Hyper-V, desligamento normal/Force e TurnOff têm semânticas distintas; somente
um procedimento externo apropriado serve ao cenário escolhido. A documentação
da API não comprova que o corte foi realizado, nem prova perda de energia do host.
Nenhum comando de VM foi executado nesta etapa.

Cobrir janelas entre intenção/flush/publicação do registro/seletor/seleção e
confirmação, além de recuperação interrompida; associar as janelas ao código
fixado, sem inventar equivalência com os seis crashes de processo antigos.
Para cada corte: diário/seletor/pisos/IDs/hashes antes e depois, evento externo,
leitura após reinício e ausência de execução do candidato pendente antes da
reconciliação. Dados e sequência não podem retroceder por recuperação local.
Erro de I/O/disco cheio deve ser real no volume de teste quando declarado como
tal; exceção injetada fica identificada como injeção, não como falha do dispositivo.

## E6-FRESHNESS — restauração integral

Fixar primeiro a decisão D6-01. Identificar explicitamente o que é restaurado:
diário, seletor, cache TUF, bundles, bootstrap e relógio. Identificar a referência
de confiança que **não** volta com o snapshot. Se tudo que o verificador observa
for idêntico ao passado válido, não há observação local que distinga os estados.
Hash, MAC ou assinatura de um recibo antigo não acrescentam prova de novidade.

Uma alternativa independente, inclusive manual autenticada, precisa demonstrar
autoria previamente confiável, vínculo à instalação/escopo e frescor não
reutilizável, bem como recusa de recibo antigo/ausente/de outro escopo. Esse é
um critério de revisão, não um novo formato de testemunha adotado no cliente.
Desenhar/autorizar mecanismo e eventual mudança de contrato antes de implementá-lo;
nenhum serviço ou rede automática é imposto ao transporte offline existente.

Sem tal referência não declarar antirreplay de restauração integral resolvido.
Falha detectada impede atualização e não reinicializa confiança; a aplicação já
instalada não é desativada. O limite de comprometimento do usuário/admin do
contrato não fecha por inferência o requisito mais forte solicitado ao gate.

## E6-CLOCK — adoção do relógio

Definir implementação e responsabilidade de manutenção: biblioteca suportada
com semântica necessária, ou fonte fixada/adaptação pequena com licença/locks,
proveniência e revisão próprias. Não se selecionou biblioteca nova nesta sessão.
Revisor deve avaliar fluxo/cache/rotação/expiração do SHA exato, não só contar
sete substituições de UtcNow. Não alegar conformidade TUF integral sem evidência.

O ciclo captura UTC uma única vez e fornece valor imutável a todos os papéis;
instanciação de provider fica sob controle do adaptador. O patch experimental
aceita TimeProvider genérico; sua assinatura não assegura isso. A fixture usa
FrozenValidationClock sealed com valor capturado, que explica a prova anterior,
sem autorizar outros providers. Alternativa futura é uma API por valor fixo ou
factory interna congelada, sujeita à revisão; não alterar fonte nesta preparação.

Expiração na igualdade, tetos dos papéis, limite de artefato, rotação/cache,
retrocesso observado e prazo monotônico precisam continuar válidos no código
escolhido. Reusar evidência anterior somente com blobs/semântica idênticos;
testar depois apenas o delta e lacunas, sem repetir bateria por rotina.
Preflight externo não substitui horário interno uniforme.

## E6-PACKAGE e E6-PE — pacote real e política

Pré-condições: raiz pública operacional estabelecida fora do pacote (G7a),
metadados de todos os papéis e recipe/manifesto autenticados, candidato GF exato
autorizado e código de validação aprovado. Registrar versão/seq/fonte/controlador/
build, IDs de run/tentativa/artefato, hashes distintos de contêiner e portátil,
manifesto/conjunto e dimensões. Não atribuir versão/candidato nesta etapa.

A evidência deve ligar assinatura a bytes e estado, incluindo limites de JSON/
ZIP/expansão/tempo/espaço, paths/CRC/links/TOCTOU, PE/AMD64/Win32/versionamento,
perfil local/runtime/dependências/licenças e ausência de segredos/endpoints ativos.
Não executar candidato para decidir se é íntegro. Recibos de CI não viram raiz
de autoria. Preservar o verificador aprovado; não criar exceção para fazer passar.

No ramo beta-unsigned, provar a classificação permitida nos bytes GF autenticados;
PE com assinatura presente/falha não vira unsigned. Só um futuro ramo obrigatório
exige publicador incorporado, cadeia/revogação/timestamp e respectivos testes.
Fonte de tempo online/revogação, catálogo e múltiplas assinaturas são explicitados
se fizerem parte da política; não declarar cobertura por cache-only ou assinatura
do SDK. Não tornar certificado comercial pré-requisito da primeira BETA por inferência.

A .66 preservada não é reempacotada, reassinada ou convertida em alvo gerenciado.
O fornecimento de pacote futuro isolado não autoriza Release, distribuição,
mudança de retenção/canal ou integração de componente não homologado.

## E6-CONTEXT — IPC

Disponibilizar conta padrão já preparada em VM descartável e pares reais de
sessões/usuários autorizados. Registrar igualdade/diferença esperada sem expor
SID/nomes em logs públicos; obter os fatos do kernel e ligá-los a processo/PID,
imagem/handles e pai retidos. Conferir cliente e servidor, contexto não elevado,
nonce/transação/perfil/ordem/limites e término efetivo.

Executar positivo padrão e negativos de outro usuário/sessão/elevação/PID,
replay de nonce, pipe preexistente e imagem não autorizada conforme escopo do
delta. Sessão estrangeira deve realmente tentar a fronteira; só enumerá-la não
é ensaio. Não repetir os antigos casos já aprovados sem delta pertinente.
Se o ambiente só oferece token Default elevado sem linked token, registrar
bloqueio e manter proteções. Outra run no mesmo perfil não produz o contexto
faltante. Linux/Wine ou declaração de identidade não substituem o kernel Windows.

## E6-LIFECYCLE e E6-DEADLINE — ensaio conjunto

Usar participantes GF qualificados e pacote autenticado em sandbox gerenciada
separada, somente em futuro escopo autorizado. Mapear launcher/helper, instâncias,
workers e leases; registrar consentimento, adiamento/busy, saída normal, intenção,
seletor, READY de PID/transação reais, confirmação e recuperação exata. Sem matar
GF/terceiros, copiar DLL sobre bundle em uso ou alterar dados/consentimento.

Identidade/hashes/pins devem abranger uso real e cargas tardias. Os 274 pins da
sonda já provam um subgate; não repetir esse número como se fosse nova prova GF.
Criar nome novo continua possível sob a ACL preservada: exigir prova do desenho
que impede conteúdo não autorizado de ser usado, e não somente enumeração antes
ou depois. Não propor ACL mais ampla/UAC como correção.

Para o prazo, distinguir **iniciar operação**, **cancelar a espera**, **terminar
I/O** e **liberar recursos**. ObserveRead(Task) recebe operação já criada; uma
factory interna controlada poderia conferir prazo antes do início, mas ainda
precisa ownership, propagação do token e drenagem. Nenhuma API desse tipo foi
implementada/adotada nesta etapa. Prazo do cliente não prova abort de driver.

O recibo futuro deve mostrar um ciclo/prazo compartilhado nas fases pertinentes
TUF/extração/PE/checagem antes do commit, estado das operações em voo e efeito
de cancelamento/falha. Se não consegue conhecer o término do I/O, não marcar
sucesso, liberar a exclusividade ou iniciar retry que possa intercalar efeitos;
desenhar resultado desconhecido/revisão manual sem baixar pisos ou presumir
rollback completo. Falha real de armazenamento e ausência de efeitos tardios
precisam de E6-STORAGE. Os 120,005 s anteriores não são prova desse pipeline.

## E6-CUSTODY e E6-BOOTSTRAP — insumo e saída

G7a: somente após autorização específica, designar responsáveis/meios para as
três chaves raiz offline/limiar 2 e papéis separados, provar backup/restauração,
rotação com quórum antigo/novo e procedimento de perda do quórum. Não exige três
novos colaboradores GitHub; é custódia criptográfica, não grant de repositório.
Recibo registra material **público**, fingerprint, revisão/versões/validade,
papéis/limiares e referências dos ensaios, nunca seeds ou privadas recuperáveis.

G7b: depois da validação conjunta, registrar bundle manual de bootstrap,
hashes/versão/protocolo de launcher/helper, manifesto asInvoker e âncora pública
incorporada, além da confirmação do fingerprint previamente confiável. Inicializar
em pasta nova; não retrofit da .66, troca silenciosa ou substituição do helper
durante a primeira transação. Pacote validado e metadados assinados não devem
criar ciclo de hash. Nenhuma chave, arquivo de bootstrap ou entrega foi criado.
