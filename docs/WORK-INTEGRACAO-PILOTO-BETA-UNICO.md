# Piloto BETA único — estratégia e plano de entrega privada

28/10/2026, America/Sao_Paulo. Revisão estratégica autorizada pelo mantenedor.
**REVISÃO CONCLUÍDA. ENTREGA EXECUTÁVEL PENDENTE. NENHUMA PUBLICAÇÃO.**
Branch work-integracao-final-20261027, PR #65 aberta/Draft.
Esta sessão não executa build, distribuição, ensaio físico ou ativação conectada.

## Decisão vigente e preservação

Há uma única linha técnica da BETA v5.13.11.66: PR #65/branch de integração.
Abandonar a exigência de desenvolver/manter uma edição BETA LOCAL como produto
paralelo. Não abrir outra linha de produto, não reiniciar integração/auditoria/
correções e preservar as Etapas 1–7 e suas evidências concluídas.

O mantenedor walissonminel-1991 é o primeiro avaliador autorizado, no PC FORTE.
Essa autorização de participação já está dada; não pedir nova confirmação
para reconhecê-la. Ela não é homologação física nem autorização para publicar
ou transferir agora um executável. Outros avaliadores somente depois da
homologação inicial do mantenedor E da validação conectada, com convites/acessos
nominais autorizados em decisão posterior.

A exigência de VM/Sandbox local, a coleta de winver/presença desse recurso e
o plano de compilação no PC pessoal ficam superados como pré-requisitos da
entrega. Não solicitar esses dados nem instalar virtualização/SDK no PC.
O PC FORTE será o ambiente de avaliação autorizada, não de depuração/build.

Os documentos e recibos anteriores permanecem históricos. Esta decisão
substitui suas hipóteses de produto paralelo/infraestrutura pessoal, mantendo
segurança, privacidade, integridade, roadmap e todos os fatos observados.

## Fonte e evidência técnica reconferidas

| Campo | Valor |
| --- | --- |
| HEAD documental de entrada | 56ad2769c6e8565fcb6d26b0047249623bea30bc |
| Árvore de entrada | 6918483485c9137ef4ca1215db2ba39d2ea8288f, 454 blobs |
| Fonte aprovada | 6df95fdd776644cd4b248b5cea61108974ca9ce9 |
| Árvore da fonte | e29acb3619b85050a4cf5becaf81bfa3fdda0bd2, 445 blobs |
| Diferenças posteriores à fonte | 18 Markdown/JSON; nenhuma mudança funcional, arquivo publicado ou remoção |
| CI Windows | [40675784213](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213), completed/success, tentativa 1, head_sha da fonte acima |
| Integridade externa | [40675784155](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155), completed/success, mesma fonte/tentativa |
| Build histórica | 384f0a96acb8ccc5f99cccb0a930d8a9cedc50ad; merge sintético com árvore igual à fonte |
| Evidência de pacote disponível | Artefato 12811032013, somente BETA-BUILD.json/BUILD-INFO.txt/SHA256SUMS.txt |
| ZIP executável / hash / entrega | Indisponíveis; não inferir dos manifestos |

Nenhuma CI foi repetida nesta revisão, e aprovação da fonte não é aprovação
automática dos bytes de uma futura compilação. O manifesto histórico continua
com digest 50b7a8c4d8bc15a826ebe2d54c7ada8f291d6cd2dbd19b8f805572f962c4c35c;
isso não é SHA-256 de ZIP executável.

## Um piloto, com qualificação sequencial

| Fase | Avaliadores | Política / condição |
| --- | --- | --- |
| Preparação da entrega privada | Nenhuma execução física nesta sessão | Fonte consolidada, CI por SHA e destino nominal aprovado |
| Avaliação inicial sem transmissão | Somente mantenedor/PC FORTE | Guardas locais atuais; coleta, pendências de envio e transmissão remota bloqueadas; relatórios locais preservados |
| Qualificação conectada | Ambiente e pessoas especificamente autorizados | Privacidade/consentimento, backend, segurança, retenção e homologação real aprovados; configuração/build rastreáveis e validados |
| Ampliação do mesmo piloto | Outros participantes nominalmente autorizados | Só após homologação inicial do mantenedor E validação conectada; definir acesso, prazo e suporte antes de convidar |

O perfil compilado GF_BETA_LOCAL e o título local existentes são guardas da
configuração inicial já validada, não obrigação de criar produto/branch
paralelos. **Esta revisão documental não remove nem altera esses elementos.**
Qualquer ajuste futuro de identificação, perfil, endpoint ou transporte precisa
de escopo autorizado, análise do delta, testes aplicáveis e CI do SHA exato.

Piloto único não promete que o mesmo ZIP se tornará conectado por consentimento
ou flag. A fonte atual bloqueia o remoto por compilação. Uma configuração/build
posterior poderá ter novos bytes/SHA e deverá comprovar suas próprias aprovações;
não reutilizar hash, homologação AVG ou aprovação do pacote inicial como prova.
Não modificar BETA-APROVACOES.json ou exigir aprovação do backend para permitir
um primeiro ensaio sem transmissão. Também não dispensar essas aprovações para
a fase conectada.

## Bloqueio revisado da Etapa 8

O bloqueio não é falta de produto local paralelo, Windows no PC pessoal,
Sandbox ou 42/42. O problema é **reter, validar e entregar os bytes reais
por um canal privado nominal autorizado**, conservando os controles públicos.

O repositório do produto é público. build-windows.yml continua publicando
somente manifestos BETA; upload executável exige beta != true. A compilação
histórica não deixou EXE/DLL/runtime recuperáveis como artefato. Rerun isolado
desse workflow continuará respeitando o mesmo gate e não resolve a entrega.

O acesso a artefatos GitHub exige autenticação e leitura do repositório;
autenticação em repositório público não é exclusividade do mantenedor.
[Documentação oficial](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).

Não alterar o resultado beta, adicionar upload executável público, esconder
binários em logs/cache ou reinterpretar manifestos como pacote entregue.
Nenhuma dessas ações é parte da proposta.

## Plano concreto PU-ENTREGA-01 — mesma fonte, execução e retenção privadas

### Destino e execução propostos

Usar um **controlador privado de empacotamento/entrega no GitHub**, sem fork ou
código funcional do produto. Preferir um repositório privado já reservado e
autorizado para entregas, se o mantenedor indicar um e a ACL for conferida.
Não reutilizar por conveniência repositório de agregados/diagnósticos ou de
outra finalidade.

Se não houver destino compatível, propor criar
walissonminel-1991/gf-beta-local-entrega como contêiner privado de entrega.
O nome é uma proposta, não recurso criado. Nome ocupado/ACL divergente
interrompem a implantação, sem alterar outro recurso. Um contêiner de CI
não constitui uma segunda linha de desenvolvimento da BETA.

Usar runner hospedado windows-2022 x64, SDK 10.0.402 e os passos já aprovados
de checkout/publish/Win32/assembly/licenças/checksums/verificador fechado.
GitHub provisiona e descarta a VM do job; nada depende de VM/Sandbox pessoal.
[Documentação oficial](https://docs.github.com/en/actions/how-tos/manage-runners/github-hosted-runners/use-github-hosted-runners).

O workflow atual de build não expõe workflow_call. Portanto não alegar que
basta chamá-lo de outro repositório, nem que a build antiga pode ser baixada.
O controlador proposto adapta somente o contexto dos passos de empacotamento
já validados; importa a fonte imutável da PR #65 e não duplica seu código.
Não precisa alterar o workflow público para reter o executável em contexto
privado. O novo controlador terá revisão/validação próprias antes da execução.

### Sequência de implantação e entrega, somente após autorização

1. **Conferir destino nominal e decisão de distribuição inicial.** Acesso
   humano somente ao mantenedor nesta fase; não convidar participantes.
   Verificar privacidade/ACL e integrações existentes. Não conceder acesso
   novo a app/pessoa nem solicitar PAT por consequência implícita.
2. **Instalar controlador privado manual**, proposto como
   .github/workflows/piloto-beta-private.yml no destino aprovado. Apenas
   workflow_dispatch, proprietário como actor/triggering_actor, private=true
   antes do build e de reter o ZIP; permissions: contents: read, ações fixadas
   por SHA, checkout sem credencial persistida, sem secret/token no pacote.
   Uma execução inicial, Windows até 30 min e verificação Linux até 15 min,
   somente cota incluída disponível; sem cobrança/reexecução automática.
3. **Reconferir CI/fonte e compilar a mesma .66.** Fixar
   6df95fdd776644cd4b248b5cea61108974ca9ce9, conferir árvore/checkout limpos,
   versão e SDK; manter configuração atual sem transmissão. Usar a receita
   aprovada self-contained win-x64. Não abrir o GF para testar no runner,
   consultar fontes do produto ou repetir a missão 1–7.
4. **Verificar os bytes novos antes de reter a candidata.** Manifesto Win32/
   CreateActCtxW, assembly local, transporte/endpoint/aprovações conectadas
   ausentes, contrato fechado de dependências/arquitetura/recursos/licenças,
   composição e inspeção de conteúdo/segredos. BETA-BUILD mantém
   distribution=requires_owner_approval; autorização local consta em recibo
   separado, sem falsificar os campos da configuração conectada.
5. **Gerar ZIP real e evidência própria.** BUILD-INFO registra fonte/build
   reais; o SHA do controlador e run/tentativa ficam separados da fonte do
   produto. Gerar SHA256SUMS, ZIP final e SHA-256 do ZIP. Nome proposto:
   GerenciadorFinal-v5.13.11.66-6df95fdd-BETA-TESTE-NAO-HOMOLOGADA-win-x64-rRUN_ID-aATTEMPT.zip.
   Nome é ilustrativo, sem arquivo disponível; a UI atual não foi renomeada.
6. **Reter somente em artefato privado autorizado**, inicialmente provisório.
   Um job Linux separado baixa o artefato dessa mesma execução e reconfirma
   SHA-256 do ZIP, extração segura, composição/hashes e procedência com o
   verificador imutável, sem executar binários. Falha/skip impede declarar
   candidata validada. Recibos externos ficam fora da pasta de contrato fechado.
7. **Disponibilizar ao primeiro avaliador.** Somente após aprovação técnica
   própria e ACL reconferida, informar ao mantenedor o acesso autenticado,
   nome/tamanho/hash do ZIP, manifesto, fonte, build/run e limites.
   Retenção proposta sete dias; distinguir disponível de download confirmado.
   Não registrar URL assinada, token ou dado privado em documento público.
8. **Parar antes dos testes físicos.** Roteiro existente é aproveitado com
   identidade do pacote real, AVG ativo e extração em pasta nova. Sem instalar
   SDK no PC, sobrescrever versão existente ou realizar manutenção destrutiva.
   Ausência de assinatura/AVG prévio deve ser declarada; não exigir homologação
   física do pacote antes de existir um pacote para avaliação autorizada.
   Bloqueio/quarentena não pode ser contornado.

Essa é uma especificação de entrega, não workflow implantado. O conector atual
não expõe criação de repositório ou workflow_dispatch. Após decisão, conferir
a capacidade operacional: a criação/primeiro acionamento podem depender do
mantenedor na interface GitHub ou ferramenta disponível e autorizada.
Autorizar uma ação não comprova que a ferramenta já exista. Não ampliar
permissões para contornar essa limitação.

### Autorizações realmente necessárias para executar o plano

| Decisão | Escopo da autorização futura | Situação nesta revisão |
| --- | --- | --- |
| D1 — destino privado | Nomear e aprovar uso de canal de entregas existente; **criar** o repositório proposto somente se necessário e expressamente aceito | PENDENTE; criação não é pré-requisito universal |
| D2 — controlador e execução | Instalar workflow privado de empacotamento e acionar uma build/verificação da fonte fixa, com limites acima | PENDENTE; nenhum workflow/CI acionado |
| D3 — distribuição inicial | Transferir/reter o ZIP real e recibos no destino aprovado, acesso somente ao primeiro avaliador, prazo definido | PENDENTE; esta sessão autoriza apenas revisão |
| Acesso de app, se indispensável | Somente recurso/escopo nominal que faltar após verificar as permissões existentes | CONDICIONAL; não pedir acesso amplo antecipado |
| Participantes posteriores | Identidade, pacote, prazo, privilégio mínimo e convite/acesso individual | NÃO AUTORIZADO; depende das duas qualificações e decisão própria |
| Piloto conectado | Aprovações de privacidade/backend/segurança/homologação e eventual build/configuração | BLOQUEADO; não incluído em D1–D3 |

Não solicitar de novo a decisão de piloto único, a participação inicial do
mantenedor ou a aprovação técnica já comprovada das Etapas 1–7. D1–D3 podem
ser aceitas juntas em uma decisão específica posterior; não constituem
consentimento remoto nem liberação pública. Não executar nesta sessão.

## Continuidade e limites

A revisão substitui a rota de VM pessoal e o produto local paralelo.
P8-ENTREGA-01 e seus recibos ficam históricos; PU-ENTREGA-01 é a proposta atual.
Não apagar evidências antigas nem converter seus status em entrega realizada.

Estado atual: estratégia atualizada, pacote privado pendente; nome efetivo,
SHA-256 executável e URL privada indisponíveis. Fonte/CI aprovadas, sem nova
build ou CI nesta revisão. AVG/assinatura e homologação física continuam sem
comprovação do novo pacote. Roadmap 27/42, sem promoção.

Preservados AVG e proteções, drivers de instalação/remoção/reversão desativados,
VLC somente inventário sem ações/consultas/testes específicos e WinGet COM
opcional/consultivo. Relatórios locais mantidos. Não ativar Worker/D1/DNS,
alterar PILOT_ENABLED=no/GITHUB_EXPORT_ENABLED=no, envio remoto ou permissões
Windows. Sem deploy, merge, Release ou distribuição nesta sessão.

Próxima sessão: após decisão específica D1–D3 e verificação da capacidade,
implementar/validar a entrega privada da BETA única na Etapa 8. Não iniciar
Etapa 9 automaticamente; depois da entrega efetiva, analisar somente ensaios
manuais autorizados do mantenedor. Ampliação do piloto exige ambas as
qualificações acima e autorização nominal posterior.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Histórico da preparação 8](WORK-INTEGRACAO-ETAPA8-PREPARACAO.md) ·
[Recibo desta decisão](evidencias/integracao-piloto-beta-unico-estrategia.json).

**Parada após a revisão e salvamento. Nenhuma publicação ou teste físico.**
