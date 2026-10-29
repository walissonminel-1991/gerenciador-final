# Etapa 4 — delimitação verificável dos gates

Ensaio isolado de `gf-self-update/1`, a partir de 9dd89225. **Não é cliente de
atualização, bootstrap instalável ou pacote GF.** `Stage4.csproj` e a sonda
`GF.Updater.InertProbe` 0.0.0.1 são os únicos executáveis produzidos em temporários.
Fontes `.cs.txt`, saídas fora do repositório, SDK 10.0.402, locks exatos e
dependências anteriores intactas. Executar `python run_tests.py --report <json>`.
Windows descartável acrescenta NTFS/WinTrust/sonda; Linux só ensaia casos comuns.
Não executar o produto nem baixar o canal privado para reproduzir este protocolo.

## Durabilidade, antirreplay e relógio

Os testes usam o diário real da Etapa 2 para confirmar recuperação exata com
piso retido e reproduzir a restauração **integral** de diário e seletor antigos.
No segundo caso o piso volta de 2 para 1. Restaurar relógio e estado juntos
também é indistinguível para uma política puramente local. Checksums, cadeia,
flush, cópias locais e assinatura de um recibo antigo não provam frescor.

`independent_floor` é apenas um **modelo de pré-condição**: receber um piso
autenticado de fora do domínio restaurado, ligado à instalação e a um desafio
novo. Os testes provam comparação e recusa, não a existência/autenticidade de
um serviço testemunha. Nenhum serviço, TPM, armazenamento protegido, chave,
endpoint ou infraestrutura foi criado. Até um resultado igual retorna falso
para capacidade de instalar. Não acrescentar dependência de rede ao contrato
offline por efeito deste modelo; uma escolha operacional exige decisão futura.

O escopo original não resiste ao administrador ou à reescrita integral pelo
próprio usuário. A delimitação mantém esse limite e o gate de energia real;
nenhum corte de energia, cache de hardware ou disco realmente cheio é simulado
como aprovação. Aplicativo instalado não deve ser desativado por falta de
frescor; atualização automática operacional permanece indisponível.

Reutiliza-se a cópia TUF experimental fixada da Etapa 3, com 20 hashes e sete
consultas/bordas, sem editar pacote/patch. Novos casos ligam validação ao horário
congelado e rechecagem de prazo monotônico/retrocesso antes do commit. O assembly
experimental não tem suporte upstream aprovado; revisão independente, manutenção
e escolha formal de dependência continuam gates de adoção. Não repetir toda a
suíte histórica nem declarar conformidade TUF a partir desses três casos.

## Pacote em streaming e receita GF

`StreamPackage` lê cabeçalhos limitados e autentica ZIP de até 256 MiB,
2.048 arquivos, arquivo até 256 MiB e expansão até 768 MiB; buffer de 64 KiB.
Perfil ZIP estreito: store/deflate, nomes ASCII, sem diretórios explícitos,
extras, comentários, descriptors, ZIP64 ou criptografia. Bits 1/2 de velocidade
do Deflate são aceitos só no método 8 (APPNOTE §4.4.4); flags local/central
continuam idênticos e criptografia continua recusada. Recusa regiões
sobrepostas/prefixos, cabeçalhos divergentes, colisão de case e arquivo/pasta,
links/tipos especiais; CRC32 calculado além dos hashes. Esses limites são
exercitados por casos representativos e valores de cabeçalho; **não foi gerado
um arquivo de 768 MiB nem testado cada ponto extremo**.

O hash do ZIP e o conjunto/hashes expandidos da receita confiável de ensaio são
conferidos antes de escrever staging. Depois, segunda passagem com CreateNew,
diretórios exclusivos, ancestrais NTFS presos, WriteThrough/Flush(true), hashes
na escrita e reabertura protegida. Fonte presa durante ambas as passagens;
reserva inclui duas cópias do ZIP/bundle e 64 MiB. Resultado é somente caminho
de ensaio; falha não apaga staging desconhecido e não muda seletor. Um único orçamento
de dois minutos cobre hash, preflight, extração e reabertura; cancelamento é
checado entre leituras. **Não se afirma o prazo global operacional do contrato**
para o pipeline inteiro TUF/PE/prontidão, nem interrupção de I/O síncrono
bloqueante. Esses limites precisam ser resolvidos antes de adoção.

O bundle completo da sonda Windows é comprimido, extraído e inspecionado com o
verificador estático real da Etapa 3. Trata-se de runtime .NET/WindowsDesktop
real sob nome/versão inertes, nunca de GerenciadorFinal.exe disfarçado.

Em paralelo, importa-se sem mudanças `scripts/verificar_pacote.py` para testar
somente `declared_binaries` e `verify_beta_metadata`: seis famílias de
dependências, versões fixadas YAML/WinGet, self-contained e perfil local.
Modelos JSON inventados não criam DLL/EXE GF. **`verify()` integral, recursos
GF, versão .66, segredos embutidos e ZIP GF real não foram validados nesta
etapa**. Falta um pacote GF futuro autenticado e autorizado para inspeção, sem
reconstruir ou modificar a .66. O verificador original não foi afrouxado.

## Authenticode

`OfflineAuthenticode` chama WinVerifyTrust com UI desativada, estado fechado
em finally e cache-only URL retrieval. Nenhuma consulta de revogação online,
instalação de certificado, trust store ou política Windows é alterada. Só
retorno LONG zero é confiança Windows; não usar SUCCEEDED/HRESULT genérico.
Este ensaio não comprova revogação atual, política de timestamp de assinatura,
assinatura por catálogo, múltiplas assinaturas ou publicador GF.

PE da sonda sem tabela de certificado só passa o perfil de teste beta-unsigned
se WinVerifyTrust também retornar TRUST_E_NOSIGNATURE. PE com tabela/signature
inválida nunca vira unsigned. Cópia do host SDK com assinatura embutida e cópia
adulterada recebem veredictos reais, sem execução. O veredicto limpo é registrado
como observado, inclusive se o cache não permitir confiança. Mesmo zero bloqueia
publicador de produção porque nenhuma política GF foi provisionada. O SDK não
é evidência de assinatura editorial GF; TUF permanece autoridade do pacote.

## IPC e ciclo de vida de sonda

Inventário read-only de token e quantidade de sessões WTS disponíveis, sem
registrar SID/contas ou criar sessões. Elevação bloqueia o classificador antes
de qualquer spawn operacional. Se o runner é elevado, não se reduz token, não
se cria conta/tarefa/serviço e não se contorna UAC para fabricar aprovação.
Uma sessão enumerada não é um handshake em outra sessão. O caminho positivo
sem elevação e o par entre sessões distintas continuam não executados se não
existirem naturalmente; requerem futuramente dois contextos descartáveis
autorizados, sem usar PC FORTE nem alterar suas permissões.

O canal de sonda agora recebe o ID da transação persistida e nonce derivado
por SHA-256 do nonce aleatório do diário e digest do marcador. PID, SID, sessão,
elevação, imagem, pai, FirstPipeInstance/CurrentUserOnly e frames da Etapa 3
continuam exigidos. A confirmação associa o bundle de marcador à receita da
sonda, não a um pacote GF de produção. Fluxos: saída normal da sonda anterior,
intenção/seletor, nova sonda, READY com janela real e confirmação enquanto viva;
nonce incorreto/saída antecipada exigem recuperação exata sem baixar piso;
adiar mantém sonda anterior viva e não grava intenção. Fechamento posterior
da sonda confirmada é só limpeza do ensaio. Handles críticos ficam presos.

O harness nomeado pode exercer essas camadas na elevação herdada para observar
o mecanismo, separadamente do classificador que o bloqueia operacionalmente.
Não existe flag de produto para ignorar elevação. Não são exercitados múltiplas
instâncias GF, workers, manutenção, saúde funcional GF ou launcher real. Apenas
filhos que o próprio harness criou podem ser terminados na limpeza.

## Preparação de bootstrap e custódia

[Plano de preparação](bootstrap-custody-plan.json) contém **slots vazios**, não
uma raiz/manifesto operacional. Três slots de chave raiz em meios independentes,
quórum 2 de 3 offline, targets offline separado e snapshot/timestamp distintos;
nomes, meios e provas de restauração aguardam cerimônia futura autorizada.
A designação de custódia não cria colaboradores ou permissões GitHub.
Privadas jamais entram no build/repositório/log/EXE. Não provisionar tokens
GitHub para assinatura ou transporte. Metadados assinados ficam fora do ZIP,
sem ciclo de hash; fingerprint público inicial deve chegar por registro
previamente autenticado, independente do pacote importado.

Dry-run só verifica a raiz pública determinística já usada nas fixtures:
fingerprint fixo, assinaturas Ed25519 por dois IDs distintos, papéis separados,
recusa TOFU, substituição e reutilização de signer. A raiz é categoricamente
recusada em modo produção. Cerimônia real exige: designar custodiantes, preparar
backup offline e restauração, verificar quórum antigo/novo na rotação, registrar
validade e resposta a perda/comprometimento; perda do quórum exige novo bootstrap
manual autenticado. Não há chave de recuperação que ignore assinatura.

Primeiro bootstrap continua sendo futura entrega manual aprovada em pasta nova,
com launcher/helper estáveis asInvoker, hashes, protocolo e âncora incorporada.
A .66 permanece inteira e sem autoatualização retroativa. Nenhum bootstrap
instalado, Release, publicação, integração de produto ou Etapa 5 nesta etapa.

Fontes primárias: [TUF 1.0.36 §5.1 e §6.1](https://theupdateframework.github.io/specification/v1.0.36/),
[WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust),
[WINTRUST_DATA](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_data),
[WTSEnumerateSessionsW](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsenumeratesessionsw)
[PKWARE APPNOTE 6.3.10 §4.4.4](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT)
e fontes/contratos fixados nas Etapas 0–3. Nenhum contato upstream foi enviado.
