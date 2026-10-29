# Etapa 8 — proposta P8-ENTREGA-01 para entrega privada

28/10/2026, America/Sao_Paulo. Somente resolução da entrega da Etapa 8.
Branch work-integracao-final-20261027, PR #65 aberta/Draft.
**PROPOSTA NÃO AUTORIZADA NEM IMPLANTADA. ENTREGA CONTINUA BLOQUEADA.**
Não há pacote executável, SHA-256 do ZIP executável ou URL de entrega nas referências verificadas.
A Etapa 9 permanece NÃO INICIADA.

## 1. O que foi reconferido nesta retomada

| Referência | Constatação |
| --- | --- |
| HEAD de entrada | 0facf42518f91c60ea9a9055de5ab81683c9d63d |
| Árvore de entrada | 423f540745eb767996347db087b30539211dd7ed; 451 blobs |
| Fonte aprovada e imutável | 6df95fdd776644cd4b248b5cea61108974ca9ce9 |
| Árvore da fonte | e29acb3619b85050a4cf5becaf81bfa3fdda0bd2; 445 blobs |
| Versão | Gerenciador Final v5.13.11.66 — BETA LOCAL TESTE — NÃO HOMOLOGADA |
| Windows | [40675784213](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213), completed/success, tentativa 1, fonte acima |
| Integridade externa | [40675784155](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155), completed/success, mesma fonte/tentativa |
| Build técnica anterior | 384f0a96acb8ccc5f99cccb0a930d8a9cedc50ad; merge sintético com árvore igual à fonte |
| Diferença desde a fonte de CI | Quinze Markdown/JSON de continuidade; nenhuma mudança executável ou remoção |

Checkpoint, matriz e relatório 8 relidos; conferidos projeto, contrato de
pacote, workflow e política de endpoint. Etapas 1–7 não foram repetidas.
A CI citada aprova a fonte, não os bytes de uma futura compilação privada.

O workflow público conserva somente BETA-BUILD.json, BUILD-INFO.txt e
SHA256SUMS.txt. O upload executável exige beta != true. Esse controle continua
intacto, assim como seu teste. Reexecutar o workflow não recupera o pacote
integral anterior nem cria uma via nominal privada.

O artefato 12811032013 continua disponível como evidência, não como programa.
Digest informado pelo GitHub:
50b7a8c4d8bc15a826ebe2d54c7ada8f291d6cd2dbd19b8f805572f962c4c35c.
Esse digest pertence ao ZIP dos três manifestos. Nenhum binário foi baixado,
gerado ou transferido nesta retomada.

## 2. Capacidade, canal e autorização são pendências distintas

| ID | Pendência real | Evidência / consequência |
| --- | --- | --- |
| B8-EXEC-01 | Ambiente Windows descartável acessível | Work atual Linux x86_64; sem dotnet, PowerShell, Docker, QEMU, Wine ou /dev/kvm. Nenhum serviço de execução Windows adicional foi exposto na busca de plugins |
| B8-CANAL-01 | Destino privado autorizado para binários | Busca restrita por repositórios relacionados encontrou somente o produto público e um privado de outra finalidade. Não ler conteúdos nem reutilizar o privado como canal de binários |
| B8-AUTH-01 | Criar destino de entrega | O pedido atual exige autorização explícita; não concedida para o recurso nomeado abaixo |
| B8-AUTH-02 | Instalar e acionar workflow privado | Exige autorização explícita; não concedida. Nenhum workflow foi alterado ou instalado |
| B8-AUTH-03 | Transferir o executável para o artefato privado | Exige autorização explícita; não concedida. Objetivo geral de entrega não é registrado como esse aceite |
| B8-BYTES-01 | ZIP real e verificação de seus bytes | Não existem fora da compilação histórica descartada; nome efetivo, hash e URL permanecem nulos |

A busca não é inventário universal de todos os ambientes/contas do mantenedor.
Permissão administrativa mostrada no catálogo não substitui autorização.
O conector atual permite salvar a documentação, mas não expõe criação de
repositório nem acionamento workflow_dispatch; também não foi usada interface
alternativa para essas operações. Aprovar a proposta não cria essa capacidade:
a implantação/ativação inicial pode exigir ação do mantenedor no GitHub ou
ferramenta posteriormente disponível e autorizada. Não pedir tokens/credenciais
nem ampliar acessos automaticamente.

## 3. Proposta exata submetida ao mantenedor

Nome: **P8-ENTREGA-01**, limitada a esta fonte e versão locais.

| Ação que requer aceite específico | Escopo fechado |
| --- | --- |
| A1 — criar repositório privado de entrega | walissonminel-1991/gf-beta-local-entrega, não fork, sem mudança no repositório do produto. Acesso humano exclusivo do mantenedor; revisar acessos/integrações existentes. Se nome ocupado ou privacidade/acesso divergentes, parar, sem reutilizar ou alterar outro recurso |
| A2 — instalar e executar workflow privado | Somente .github/workflows/beta-local-private.yml no novo repositório, seguindo a especificação abaixo. Uma execução manual, Windows 2022 x64 e verificação Linux; sem gatilhos push/PR/schedule. Usar apenas cota incluída já disponível; sem autorizar cobrança adicional ou reexecução automática |
| A3 — transferir e disponibilizar binário privado | Upload do ZIP e recibos somente como artefato do novo repositório privado; download autenticado exclusivamente pelo mantenedor. Retenção proposta de sete dias. Sem upload no produto público, Release, Pages, link aberto, convite ou transferência a outro serviço/pessoa |

A1–A3 podem ser aceitas conjuntamente ou separadamente. Aceite de uma não
autoriza as outras. Não haverá criação/concessão de acesso humano ou de app
por consequência implícita: necessidade adicional deve identificar recurso e
escopo e receber decisão própria. Não provisionar recursos antes do aceite.

A alternativa não existe hoje. Trata-se de uma rota privada nova proposta,
submetida a decisão local de distribuição, separada do fluxo conectado.
A autorização futura não altera GF_BETA, BETA-APROVACOES.json,
BetaTelemetryPolicy.props nem gates do workflow público.

A documentação oficial confirma que cada job hospedado recebe uma VM nova,
descomissionada ao terminar, e que artefatos exigem autenticação e leitura
do repositório. Por isso a restrição de acesso do repositório privado é parte
do controle, além do guard de quem aciona o workflow:

- [VM por job e descarte](https://docs.github.com/en/actions/how-tos/manage-runners/github-hosted-runners/use-github-hosted-runners).
- [Runners Windows x64 e cota de repositórios privados](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job).
- [Acesso autenticado aos artefatos](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).

## 4. Especificação revisável do futuro workflow privado

Esta seção é especificação, não arquivo de workflow ativo ou código executado.

| Controle | Valor obrigatório |
| --- | --- |
| Repositório/visibilidade | Exatamente walissonminel-1991/gf-beta-local-entrega; private=true reconferido antes de compilar e de transferir |
| Acionamento | Apenas workflow_dispatch pelo mantenedor; github.actor e github.triggering_actor iguais a walissonminel-1991 |
| Decisão local | Referência ao aceite explícito de A1/A2/A3 e ao commit desta proposta; sem esse registro, não iniciar |
| Credenciais | permissions: contents: read; checkout persist-credentials: false; sem PAT, novo secret, OIDC, atestação, token/endpoint em arquivo publicado ou despejo de variáveis |
| Fonte | Checkout do produto público no SHA fixo 6df95fdd776644cd4b248b5cea61108974ca9ce9; conferir git HEAD e fonte rastreada limpa, nunca branch flutuante |
| CI anterior | Reconferir os dois runs acima, conclusão success e head_sha exato; não transferir aprovação de outro SHA |
| Ações | Fixar por SHA as mesmas checkout/setup-dotnet/upload-artifact/download-artifact já usadas no workflow aprovado; registrar os SHAs |
| Compilação | Um job windows-2022, timeout 30 min; SDK 10.0.402 de global.json, perfil local explícito, self-contained win-x64 |
| Verificação independente | Um job ubuntu-24.04, timeout 15 min, somente após o job Windows. Baixar o artefato privado da mesma execução e verificar o ZIP/arquivos sem executar o GF |
| Publicação privada | Somente com A3 aceito e verificações Windows aprovadas; artefato provisório até a segunda verificação aprovar. Se ela falhar, não declarar entrega validada |
| Retenção/cota | Sete dias; verificar cota incluída disponível antes do acionamento. Falta de cota ou cobrança adicional exige nova decisão |
| Escopo | Nenhum ensaio físico, consulta do aplicativo, reexecução de suítes anteriores, driver, atualização, desinstalação, resíduo ou perfil conectado |

A infraestrutura poderá hospedar logs técnicos de compilação, sem dados dos
PCs pessoais. Runner de CI não comprova funcionamento sem administrador no
PC FORTE nem comportamento AVG. Não instalar AVG, alterar UAC/permissões,
desativar proteções ou executar o aplicativo para produzir essa prova.

### Compilar e inspecionar, sem iniciar o aplicativo

No job Windows, após o checkout e conferência das CIs, reproduzir os passos
de empacotamento aprovados, com saída nova fora da fonte. Comandos centrais:

```powershell
$source = '6df95fdd776644cd4b248b5cea61108974ca9ce9'
$out = Join-Path $env:RUNNER_TEMP 'GF-BETA-LOCAL'
if (Test-Path -LiteralPath $out) { throw 'Saida existente; parar sem sobrescrever' }
if ((& git rev-parse HEAD).Trim() -cne $source) { throw 'Fonte divergente' }
if ((& dotnet --version).Trim() -cne '10.0.402') { throw 'SDK divergente' }

dotnet publish GerenciadorAplicativos.csproj -c Release -r win-x64 --self-contained true -p:GfBetaProfile=local -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:GfSourceCommit=$source -o $out
if ($LASTEXITCODE -ne 0) { throw 'Publish reprovado' }

./TestesManifesto/Testar-Ativacao.ps1 -ExecutablePath (Join-Path $out 'GerenciadorFinal.exe')
dotnet build TestesTelemetria/TestesTelemetria.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Inspetor nao compilado' }
$report = & dotnet run --project TestesTelemetria/TestesTelemetria.csproj -c Release --no-build -- --inspect-local-beta-build (Join-Path $out 'GerenciadorFinal.dll') $source
if ($LASTEXITCODE -ne 0) { throw 'Perfil local nao confirmado' }
$report | Set-Content (Join-Path $out 'BETA-BUILD.json') -Encoding utf8
```

A implementação deverá usar ErrorActionPreference=Stop, conferir código de
saída de cada processo nativo e interromper em qualquer erro. O trecho acima
é o núcleo técnico, não um script completo de entrega. Nada foi executado
ou validado com PowerShell nesta sessão.

Conservar a identidade Win32/CreateActCtxW, asInvoker e o inspetor compilado.
Conferir GF_BETA_LOCAL, versão/fonte, transporte false, endpoint e aprovação
conectada vazios. BETA-BUILD.json conserva distribution=requires_owner_approval;
não falsificar esse campo. Guardas locais, título NÃO HOMOLOGADA e recursos
do código aprovado permanecem inalterados.

### Metadados, licenças, conteúdo e ZIP real

1. Gerar BUILD-INFO.txt com versão 5.13.11.66, fonte e Commit Git ambos iguais
   a 6df95fdd776644cd4b248b5cea61108974ca9ce9 para esse checkout direto. Base da PR vazia,
   não fingir o merge 384f0a96. Registrar à parte SHA do workflow privado,
   run_id, run_attempt, imagem e ferramentas, decisão local e identificador
   P8-LOCAL-6df95fdd-rRUN_ID-aATTEMPT. SDK/fonte iguais não prometem hashes
   binários iguais aos da build histórica.
2. Preservar todos os arquivos necessários de runtime, recursos e licenças.
   Conferir as dependências resolvidas e registrar sua versão real; se houver
   mudança no contrato, parar para avaliar, sem remover verificação. Manter
   as declarações honestas de assinatura ausente e AVG não validado.
3. Gerar SHA256SUMS.txt cobrindo exatamente todos os arquivos da pasta,
   exceto o próprio manifesto, com a rotina aprovada do workflow público.
   Não incluir fontes/testes/cache, credenciais/certificados, scripts de
   build, arquivos de configuração privada ou saídas de diagnóstico dos PCs.
   Inspecionar metadados do assembly, lista fechada, textos e conteúdo completo
   disponível antes de transferir; sinais de segredo ou arquivo indevido
   interrompem a entrega. Checksums isolados não provam ausência de segredos.
4. Usar sem alterações o verificador fechado na fonte aprovada:

```powershell
python scripts/verificar_pacote.py $out --version 5.13.11.66 --source $source --build $source --beta
if ($LASTEXITCODE -ne 0) { throw 'Pacote reprovado' }
```

5. Registrar a evidência técnica de assinatura/hash com
   scripts/Registrar-Evidencia-Antivirus.ps1 em arquivo fora da pasta publicada.
   Isso não é varredura AVG. Conservar os avisos obrigatórios na pasta original.
6. Criar ZIP novo somente da pasta validada. Nome proposto, ainda sem arquivo:
   GerenciadorFinal-v5.13.11.66-6df95fdd-BETA-LOCAL-TESTE-NAO-HOMOLOGADA-win-x64-rRUN_ID-aATTEMPT.zip.
   Recibos externos não entram na pasta de contrato fechado.
7. Calcular SHA-256 do ZIP final com Get-FileHash. Recalcular por Python
   hashlib em implementação distinta. Extrair esse mesmo ZIP em diretório
   novo, após rejeitar caminhos de escape, nomes ambíguos, duplicatas,
   links e conteúdo excessivo; aplicar novamente verificar_pacote.py.
   Qualquer compactação/assinatura/alteração posterior exige novo hash e
   verificação. Não usar o digest do invólucro do GitHub como hash do ZIP real.
8. Somente com A3 aceito, transferir o ZIP, seu checksum e recibos para o
   artefato privado provisório da execução. O job Linux deverá verificar o
   checksum real, extração segura e cada arquivo pelo mesmo verificador
   imutável, comparando contagens/bytes/versão/fonte com o recibo Windows.
   Esse job não instala nem executa binários. Ausência, skip ou falha impede
   declarar entrega validada.

O recibo final privado deverá conter: fonte; árvore; versão/perfil; SHA do
workflow; identificador/run/tentativa; resultado Windows e verificação Linux;
SHA-256 do ZIP real; nome/tamanho; arquivo SHA256SUMS; contagens de arquivos e
hashes; runtimes resolvidos; licenças/inspeção de conteúdo; limitações
AVG/assinatura; referência à autorização local; artefato, prazo e confirmação
de acesso autenticado do mantenedor. Nenhum campo pode ser preenchido com
valores de uma build anterior por conveniência.

A referência nominal de artefato só será informada depois de ele realmente
existir e sua restrição ser confirmada. URLs assinadas temporárias, tokens,
caminhos privados e conteúdo bruto não entram no repositório público.
Na PR #65 registrar apenas evidência sanitizada e hashes/proveniência
necessários. Ter link não comprova recebimento: disponibilidade validada e
confirmação do download pelo mantenedor devem ser distinguidas.

## 5. Critério para encerrar o bloqueio

- A1/A2/A3 decididas e a implantação compatível realmente disponível.
- Fonte/CI reconferidas; Windows e verificação independente aprovados nos
  bytes novos; perfil local, manifesto, recursos/licenças/conteúdo conferidos.
- ZIP real existente, nome/SHA-256 próprios, acesso nominal privado comprovado
  e estado de recebimento registrado sem presumir download.
- Checkpoint/matriz atualizados com procedência, limitações e entrega efetiva.
- Parada na Etapa 8. Não pedir execução física nesta sessão e não liberar
  BETA conectada/pública, mesmo se o pacote vier a ser entregue.

AVG permanece ativo no PC; ausência de teste AVG não é aprovação nem
assinatura. Diagnósticos remotos/coleta/pendências de envio bloqueados,
relatórios locais preservados, drivers desativados, WinGet COM consultivo e
VLC somente inventário, sem nova consulta/teste específico. Flags no,
Worker/D1/DNS/EU.org e aprovações conectadas intactos. Roadmap **27/42**.

## 6. Decisão que falta e ponto de retomada

A instrução atual proíbe criar repositórios privados, conceder acessos,
alterar workflows de publicação ou transferir executáveis sem autorização
explícita correspondente. Por isso A1/A2/A3 estão apenas propostas.
Não é bloqueio imposto por skill nem reprovação automática de ferramenta.

Texto para o mantenedor decidir, somente se concordar com esses escopos:

> Autorizo A1, A2 e A3 da proposta P8-ENTREGA-01: criar walissonminel-1991/gf-beta-local-entrega privado, instalar o workflow privado manual especificado e realizar uma compilação Windows x64 da fonte 6df95fdd776644cd4b248b5cea61108974ca9ce9 com verificação independente, usando apenas cota incluída disponível. Autorizo a transferência do ZIP e recibos para artefato desse repositório privado, com acesso humano exclusivo a mim e retenção de sete dias. Não autorizo novos acessos de pessoas/apps, cobrança adicional, alteração no workflow público, Release, BETA conectada ou Etapa 9. Confirmaremos a capacidade de implantação/acionamento antes de executar.

Se preferir disponibilizar uma VM Windows descartável e canal privado já
existentes, identificar os recursos e autorizar nominalmente o build e a
transferência; não presumir equivalência com esta proposta ou reutilizar
outro repositório privado. A decisão precisa incluir o destino efetivo.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Relatório 8 original](WORK-INTEGRACAO-ETAPA8-PREPARACAO.md) ·
[Recibo desta retomada](evidencias/integracao-etapa8-entrega-privada.json).

**BLOQUEADA: proposta salva para decisão; nenhum recurso/canal/workflow
provisionado, binário gerado/transferido ou teste físico iniciado.**

