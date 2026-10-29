# Autoatualizador seguro do Gerenciador Final — checkpoint

28/10/2026, America/Sao_Paulo. **Etapa 8 concluída no protótipo isolado.
Zero de oito gates operacionais fechado; oito bloqueados. Etapa 9 NÃO INICIADA.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE8_ISOLATED_G2_G8_DELTAS |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | 13afd9766bd99a54a451865cf76b133eb1b29671 |
| Código / correção dirigida | 8b947d3c54c74af938d8bcfb43463eebe181e2ff / bcc681079a209306132e0f03b5a47c9b5f4ee1af |
| G2 implementado | UTC escalar readonly por tentativa, cliente de uso único, Unchanged encerra ciclo sem alvo, facade sem refresh duplicado |
| G8 implementado | Admissão/ownership antes da factory, tarefa original/lease/recursos retidos, prazo compartilhado e resultado desconhecido bloqueante |
| Windows inicial | Run 40805278830/número 1/tentativa 1: 26/27; falha de sharing no leitor de conferência preservada |
| Correção Windows | Run 40805755390/número 2/tentativa 1: 1/1, somente o caso falho; 26 aprovados não repetidos |
| Cobertura composta | 27 casos distintos com provas pertinentes; não é bateria completa no SHA da correção |
| Isolamento | SDK 10.0.402/runtime 10.0.14, 142 fontes GF/zero protótipo, saídas externas, zero build/execução GF/artifacts |
| Gates | Todos bloqueados; G2 manutenção/revisão/interoperabilidade/estado; G8 barreira durável/armazenamento/pipeline GF; G1/G3–G7 insumos externos |
| Limite da recuperação | Sessão local reiniciada, resultado inicial indisponível não contado; código salvo antes da CI Windows |
| Preservação | .66/canal/proteções/físico parcial/27 de 42 intactos; zero chaves operacionais, integração ou publicação |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA8.md) ·
[Rodadas](evidencias/autoatualizador-etapa8-rodadas.json) ·
[Gates](evidencias/autoatualizador-etapa8-gates.json) ·
[Recibo](evidencias/autoatualizador-etapa8.json).

Contrato gf-self-update/1 intacto. Não promover subprovas de runtime ou TUF de
fixture a homologação operacional. Nenhum ensaio de armazenamento travado,
durabilidade pós-crash, IPC/contexto padrão, pacote/ciclo GF ou custódia novo.
Próxima ação: decisões/insumos D6 pendentes e revisão dos limites concretos em
escopo posterior autorizado. **Parar; não iniciar Etapa 9 ou integrar/publicar.**

## Registro da Etapa 7 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 7 de decisões e revisão concluída.
Zero de oito gates operacionais fechado; oito bloqueados. Etapa 8 NÃO INICIADA.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE7_TARGETED_DECISIONS_AND_REVIEW |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | bee4bebd5c5714bae7430a9249c961e1b85af523 |
| Escopo entregue | Seis decisões de arquitetura, revisão dirigida do relógio TUF e desenho de prazo/ownership/I/O; D6-01 a D6-07 separados de autorização operacional |
| G2 / rota técnica | Fonte MIT já fixada, escalar UTC imutável/factory interna/cliente por tentativa para revisão isolada futura; sem implementação, adoção ou troca de biblioteca |
| G2 / achado novo | Timestamp igual retorna localmente e fluxo externo continua; deve terminar o ciclo. Fixture chama refresh duas vezes; o patch de sete consultas não corrige esses fluxos |
| G8 / desenho | Um prazo, admissão/ownership antes do início, tarefa original e recursos até término; resultado desconhecido bloqueia aplicar/limpar/retry. Factory pode bloquear antes de devolver Task |
| Limites de I/O | ReplaceFile sem cancelamento por essas APIs; CancelIoEx não espera término. Não prometer abort de driver nem durabilidade da quarentena sem G1 |
| D6 ainda dependentes | D6-01 mecanismo/escopo de frescor; D6-02 aceite de manutenção/revisão; D6-03 laboratório; D6-04 autorização/custódia; D6-05 candidato GF; D6-07 implementação/evidência. D6-06 só futuro modo obrigatório |
| Gates | 0/8 fechados; decisões de desenho e achados estáticos não são provas operacionais |
| Verificação | Proveniência/hashes, JSON/links/referências e preservação documental; sem executar suites ou repetir CI |
| Execução | Zero código/build/CI/teste novo ou repetido, chaves operacionais, infraestrutura, integração ou publicação |
| Preservação | .66, canal privado, proteções, avaliação física parcial e 27/42 intactos; históricos preservados |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA7.md) ·
[Revisão TUF](WORK-AUTOATUALIZADOR-ETAPA7-RELOGIO.md) ·
[Prazo/I/O](WORK-AUTOATUALIZADOR-ETAPA7-PRAZO-IO.md) ·
[Decisões e gates](evidencias/autoatualizador-etapa7-decisoes.json) ·
[Recibo](evidencias/autoatualizador-etapa7.json).

Contrato **gf-self-update/1 revisão 1 intacto**. A rota/desenho estão registrados
para revisão isolada futura, sem aprovação presumida de manutenção, laboratório,
custódia, pacote ou recurso externo. G1 forte não recebe solução somente local;
beta-unsigned permanece permitido. Não alterar garantias, chaves ou permissões.

Próxima ação: decisões/insumos específicos da matriz D6 e eventual revisão do
delta em nova missão autorizada. **Parar após salvar/conferir; não iniciar
Etapa 8, criar chaves operacionais, integrar não homologado ou publicar versões.**
Preservar AVG/Windows, remoto bloqueado/flags no, consentimento, drivers inativos,
VLC só inventário, COM consultivo e BETA .66 integral. Nenhum teste físico repetido.

## Registro da Etapa 6 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 6 de preparação concluída.
Oito gates operacionais continuam bloqueados; zero fechado. Etapa 7 NÃO INICIADA.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE6_TECHNICAL_UNBLOCKING_PREPARATION |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | 5f265c7606164120406bf44a5d5bf6b29774cef4 |
| Escopo entregue | Dependências/alternativas/insumos mínimos de G1–G8, cinco achados de preparação e sete registros de decisão |
| Dependência esclarecida | G7a custódia/âncora é insumo de G3; G7b bootstrap final é saída após validação conjunta, sem fechar G7 |
| Perfil esclarecido | Beta-unsigned continua permitido; certificado/publicador obrigatório só condicional, sem exigência nova |
| G2 / G8 | TimeProvider genérico não impõe valor fixo; Task já criada não dá controle de início/drenagem. Critérios definidos, sem implementação/adoção |
| G5 / ambientes | Perfil padrão GitHub Windows administrativo documentado; reaproveitada consulta Default=1/erro 1312. Sem repetir CI nem alterar contexto |
| Decisões pendentes | D6-01 frescor/escopo; D6-02 relógio/manutenção; D6-03 laboratório/contextos; D6-04 custódia/autorização; D6-05 candidato/participantes; D6-07 prazo/I/O. D6-06 só futuro ramo assinado obrigatório |
| Insumos mínimos | E6-STORAGE/FRESHNESS/CLOCK/PACKAGE/PE/CONTEXT/LIFECYCLE/DEADLINE/CUSTODY/BOOTSTRAP; especificados, não coletados como prova operacional nesta etapa |
| Verificação desta etapa | JSON, links, cobertura dos oito gates, grafo sem ciclo e preservação de blobs/42 IDs; verificações documentais, sem novo teste de produto/segurança |
| Execução | Zero código, build, CI, teste novo ou repetido; zero chave operacional/infraestrutura/bootstrap/publicação |
| Preservação | .66/canal privado/proteções/avaliação física parcial/27 de 42 intactos; somente documentação de continuidade |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA6.md) ·
[Dossiê de evidências mínimas](WORK-AUTOATUALIZADOR-ETAPA6-EVIDENCIAS-MINIMAS.md) ·
[Dependências e decisões](evidencias/autoatualizador-etapa6-dependencias.json) ·
[Recibo](evidencias/autoatualizador-etapa6.json).

G1–G8 permanecem bloqueados. As resoluções de preparação removem ambiguidade
sobre ordem/insumos, não substituem armazenamento, frescor, pacote GF autenticado,
IPC padrão/entre sessões, ciclo GF ou cerimônia de custódia por inferência.
Não houve falha do conector; faltam recursos/insumos e decisões específicos.
Nenhuma decisão pendente autoriza mudar contrato, permissões, canal ou chaves.

Próxima ação: prover/decidir os itens específicos D6 em escopo autorizado,
sem repetir testes já feitos ou criar novas missões de auditoria. **Parar após
salvar/conferir; não iniciar Etapa 7, integrar o autoatualizador, criar chaves
operacionais sem autorização específica ou publicar versões.**
Preservar AVG/proteções, remoto bloqueado/flags no, consentimento, drivers
inativos, VLC só inventário, COM consultivo e BETA .66 integral.

## Registro da Etapa 5 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 5 encerrada com evidências isoladas.
Zero de oito gates operacionais fechado; aplicação GF bloqueada. Etapa 6 NÃO INICIADA.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE5_OPERATIONAL_GATE_DISPOSITION |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | b10217552d372a9db55210fe71901b90abc3f159 |
| Código dos dez ensaios | 64743bd45a83daa9bfba56ac06194c2c0b59d6da |
| Consulta adicional G5 | 6712f3da6bda1c06fd693b625383cb7b23706d22; corpo dos dez ensaios intacto |
| Contrato / bibliotecas | gf-self-update/1 intacto; Tuf 1.1.1/BC 2.7.0 fixados, sem adoção do relógio experimental |
| Windows | 10/10, run 40774544098/número 1/tentativa 1/success; job 123120256513; SDK 10.0.402/runtime 10.0.14 |
| Consulta de contexto | Run 40775734566/número 2/tentativa 1/success; job 123124422982; suíte completa skipped, não repetida |
| Linux | Um aprovado, três bloqueados por AF_UNIX/error 13, zero falhas finais; não somar ao Windows |
| Pins / ciclo inerte | 274 arquivos/identidades e 274 write-opens recusados; handles até saída real; confirmação viva e recuperação por deadline retêm piso |
| Prazo real | Padrão 120 s observado em 120,005 s; read cooperativo cancelado. WaitAsync sem token não encerra I/O; conclusão tardia observada |
| IPC / token | Elevado e recusado operacionalmente; consulta read-only tipo Default=1/LinkedToken erro 1312. Sem padrão vinculado ao token consultado; zero handshakes padrão/estrangeiros |
| Gates | Oito bloqueados: armazenamento/frescor; relógio suportado; pacote GF autenticado; política PE; contexto IPC; ciclo GF; custódia; prazo/I/O global |
| Isolamento | 142 fontes Compile GF/zero protótipo, saídas externas; zero build/execução GF e zero artefatos em ambas as runs |
| Preservação | .66/canal privado/AVG/proteções/físico parcial intactos; 27/42, zero repetição física |

[Relatório com decisão por gate](WORK-AUTOATUALIZADOR-ETAPA5.md) ·
[Matriz](evidencias/autoatualizador-etapa5-gates.json) ·
[Windows](evidencias/autoatualizador-etapa5-windows.json) ·
[Contexto](evidencias/autoatualizador-etapa5-contexto.json) ·
[Linux](evidencias/autoatualizador-etapa5-linux.json) ·
[Recibo](evidencias/autoatualizador-etapa5.json).

As subprovas de pins completos e prazo/cancelamento são da sonda e de I/O real
controlado, sem prova de armazenamento travado ou ciclo GF. Criação de nomes
novos continua possível; revalidação recusa o extra, sem homologar loader.
Plano de custódia continua vazio; sem âncora operacional/pacote futuro autenticado,
frescor independente ou controlador externo de falha de armazenamento.
Beta-unsigned continua permitido; não impor certificado/publicador por inferência.

Próxima ação: resolver os pré-requisitos específicos da matriz em escopo
autorizado, sem substituir prova por modelo ou repetir físico. **Não iniciar
Etapa 6, habilitar update GF, integrar componente não homologado, publicar ou
alterar canal/infraestrutura/permissões. Parar após salvar/conferir esta etapa.**
Preservar remoto bloqueado/flags no, AVG, consentimento, drivers inativos,
VLC só inventário, COM consultivo e BETA .66 integral.

## Registro da Etapa 4 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 4 concluída no escopo isolado, com
delimitações verificáveis. Aplicação operacional bloqueada; Etapa 5 NÃO INICIADA.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE4_ISOLATED_VERIFIED_GATE_BOUNDARIES |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | 9dd892259af9297172818d9c3112f3df0721b328 |
| Código validado | dc7ee0a1aa65be20c660c1e0e7527d4e1c05cec4; fechamento posterior só documentação/resultados |
| Contrato / bibliotecas | gf-self-update/1, Tuf 1.1.1/BC 2.7.0 intactos; relógio continua assembly experimental separado |
| Windows | 46/46, run 40769700594, número 2/tentativa 1/success, job 123103247820; SDK 10.0.402/runtime 10.0.14 |
| Linux | 38/38 comuns, runtime 10.0.13; subconjunto dos 46 |
| Pacote | Sonda inerte 0.0.0.1: ZIP 63.940.273 bytes; 274 arquivos/123.062.971 bytes expandidos, extração NTFS e inspeção estática reais |
| Authenticode | WinVerifyTrust offline: unsigned 800b0100, SDK assinado 00000000, adulterado 80096010; publicador GF não provisionado |
| Durabilidade / relógio | Restauração integral reproduz piso 2→1 e relógio local antigo; testemunha independente só modelo, energia não executada |
| IPC / ciclo | READY ligado ao diário, confirmação com filho vivo, recuperação por nonce/saída, adiar sem fechar; somente harness de sonda na elevação herdada |
| Contextos pendentes | Runner elevado recusado operacionalmente; quatro sessões WTS enumeradas, sem handshake estrangeiro ou positivo sem elevação |
| Bootstrap / custódia | Raiz pública de teste validada e bloqueada em produção; plano com slots vazios, sem chaves/âncora/custódia operacional |
| Primeira rodada | 40769018174: 39/40; quatro casos de ciclo não executados. Corrigidos apenas hints de velocidade Deflate; falha preservada |
| Isolamento | 142 itens Compile GF, zero protótipo; zero GF compilado/executado; zero artefatos em ambas as runs |
| Preservação | .66/canal privado/AVG/proteções/físico parcial intactos; roadmap 27/42; zero testes físicos repetidos |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA4.md) ·
[Protocolo](../prototipos/autoatualizador-seguro/stage4/README.md) ·
[Windows](evidencias/autoatualizador-etapa4-windows.json) ·
[Linux](evidencias/autoatualizador-etapa4-linux.json) ·
[Recibo](evidencias/autoatualizador-etapa4.json).

Gates abertos: energia/armazenamento e frescor após restauração integral;
revisão/manutenção/adoção do relógio; pacote GF autenticado e vínculo TUF→ZIP→estado;
publicador/revogação se exigidos; positivo sem elevação e IPC entre sessões/usuários;
ciclo GF real/handles de todos os arquivos; cerimônia de bootstrap/custódia;
deadline global e I/O bloqueante. Modelos e assinatura SDK não fecham esses gates.
O ZIP temporário é de sonda, não entrega GF; não há artefato para distribuir.

Próxima ação: revisão específica dessas evidências e definição dos meios
necessários aos gates, em futura sessão autorizada. **Não iniciar Etapa 5,
integrar no executável principal, publicar ou iniciar homologação física.**
Preservar remoto bloqueado, flags no, consentimento, AVG, drivers inativos,
VLC só inventário, COM consultivo e canal privado. Parar após salvar/conferir.

## Registro da Etapa 3 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 3 concluída no escopo isolado,
com gates remanescentes explícitos. Integração no produto e Etapa 4 NÃO INICIADAS.**

| Campo | Estado vigente |
| --- | --- |
| Missão | GF_SELF_UPDATER_STAGE3_ISOLATED_PENDING_GATES |
| PR / branch | #65 aberta/Draft, work-integracao-final-20261027 |
| Entrada | 41848534602ef2f9bc84fe3e4eb26de3937f48e2 |
| Código validado | 6e17b7268fb1182669e9a2646b34cfb52071dd00; fechamento posterior só documentação/resultados |
| Contrato / bibliotecas | gf-self-update/1 intacto; Tuf 1.1.1 e BC 2.7.0 originais preservados |
| Relógio | Cópia experimental da fonte TUF fixada: 20 arquivos/hash, 7 consultas/bordas, relógio imutável; não adotada no produto |
| Windows | 47/47, run 40764777624, número 3/tentativa 1/success, SDK 10.0.402, runner runtime 10.0.14 |
| Linux | 19/19 comuns, runtime 10.0.13; não somar novamente aos 47 |
| Sonda real | WinForms self-contained 10.0.13, 274 arquivos/123.062.971 bytes, versão inerte 0.0.0.1; não é candidata GF |
| Extração / estática | ZIP de texto CreateNew/handles/hashes; PE/manifesto/runtime/grafo/licenças reais da sonda |
| IPC / saída / prontidão | PID/SID/sessão/elevação/imagem do kernel, pai, nonce/tx, handles presos e saída real; timeout real 30,03 s |
| Durabilidade | 16 imagens: 6 seguras/10 recusas; flush arquivo/diretório aceito, mas sem prova de energia; sufixo integral perdido restaura piso antigo no modelo |
| Contextos não executados | Outro usuário/sessão e positivo sem elevação; runner elevado foi recusado pelo classificador operacional, sem alterar token/ACL |
| Rodadas preservadas | 40763350794 antes da suíte: coletor de notices; 40763986158 30/33 e IPC bloqueado; correções pontuais, zero retries automáticos |
| Isolamento | 142 itens Compile GF, zero do protótipo, ambos ambientes; zero build/execução do GF e zero artefatos nas três runs |
| Preservação | BETA .66/canal privado/AVG/proteções intactos; físico parcial/zero repetição; roadmap 27/42 |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA3.md) ·
[Protocolo de ensaio](../prototipos/autoatualizador-seguro/stage3/README.md) ·
[Windows](evidencias/autoatualizador-etapa3-windows.json) ·
[Linux](evidencias/autoatualizador-etapa3-linux.json) ·
[Recibo](evidencias/autoatualizador-etapa3.json).

Gates abertos: energia/armazenamento real; adoção/manutenção/revisão da mudança
experimental TUF; pacote/extração GF/Authenticode reais; contextos IPC acima;
ciclo de vida operacional e bootstrap/custódia de produção. Flush e relógio do
experimento não autorizam instalação/homologação ou substituição da .66.
As camadas foram ensaiadas separadamente, sem integrar pipeline no produto.

Preservar remoto bloqueado/flags no, AVG, consentimento, drivers inativos,
VLC só inventário e COM opcional/consultivo. Sem PC FORTE, canal privado,
permissões, Worker/D1/DNS, chave operacional, merge/Release/deploy ou publicação.
**Etapa 4 não iniciada. Parar após salvar/conferir esta etapa.**

## Registro da Etapa 2 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 2 concluída no protótipo isolado.
Integração no produto e Etapa 3 NÃO INICIADAS.**

| Campo | Estado vigente |
| --- | --- |
| Missão | `GF_SELF_UPDATER_STAGE2_ISOLATED_STATE_AND_RECOVERY` |
| PR / branch | #65 aberta/Draft, `work-integracao-final-20261027` |
| HEAD de entrada | `a2f4fdaa8e03d6705bb1fb2aea1fb713ec2b2d25` |
| Código final testado | `a57fd7c7677555e3d17fe1c4bb52c31ad43a8bf4`; fechamento posterior só resultados/checkpoint |
| Contrato e bibliotecas | gf-self-update/1 revisão 1; Tuf 1.1.1 + BouncyCastle 2.7.0, lock/âncora de fixture preservados |
| Implementação isolada | Estado/cache/pisos persistentes, relógio conservador, NTFS por handles/IDs, seletor, diário, lease e recuperação de textos inertes |
| Windows descartável | 61/61, 14 NTFS; run 40747226567, número 2/tentativa 1, success; SDK 10.0.402/runtime 10.0.14 |
| Linux | 47/47 comuns, runtime 10.0.13; os mesmos casos não somam novamente aos 61 |
| Regressão da Etapa 1 | 66/66 isolados sobre 5e9961b6; evidência separada, sem testes de produto/físicos |
| Interrupções | 6 janelas distintas de crash de processo, mais erros I/O injetados; sem teste de energia/disco cheio real |
| Primeira rodada preservada | 40746736121: 39/60; compartilhamento de diretório corrigido e assert da recusa NTFS alinhado, sem retry automático |
| Isolamento | 142 itens Compile do produto, zero do protótipo em ambos ambientes; saídas externas; produto não compilado/executado |
| Canal privado / BETA | .66 e run privada 40716548530/artefato 12828262039 intactos; sem acesso operacional, download ou mudança de canal |
| Físico / roadmap | Avaliação parcial preservada; zero testes físicos repetidos; 27/42 |
| Publicação | Nenhum pacote, artefato, Release, merge ou deploy; CI novo só de fixture, contents:read |

[Relatório](WORK-AUTOATUALIZADOR-ETAPA2.md) ·
[Protocolo e limites](../prototipos/autoatualizador-seguro/STATE-PROTOCOL.md) ·
[Windows](evidencias/autoatualizador-etapa2-windows.json) ·
[Linux](evidencias/autoatualizador-etapa2-linux.json) ·
[Recibo](evidencias/autoatualizador-etapa2.json).

Gates restantes: durabilidade contra energia/armazenamento real, relógio uniforme
interno TUF, extração/PE/runtime reais, IPC autenticado/saída/prontidão real,
bootstrap/custódia operacional e integração. Hash local não autentica contra
reescrita integral do próprio usuário; testes não homologam instalação/AVG/TUF.
A próxima etapa exige sessão/instrução própria. **Etapa 3 não iniciada; encerrar.**

Preservar flags no/remoto bloqueado, consentimento, AVG/proteções, drivers
inativos, VLC só inventário e COM opcional/consultivo. Sem PC FORTE, Worker/D1/DNS,
ACLs modificadas, produto ou canal privado. Branch isolada de validação mantida
para evidência; não reexecutar CI do produto por este protótipo/documentação.

## Registro da Etapa 1 — histórico preservado

28/10/2026, America/Fortaleza. **Etapa 1 concluída no protótipo isolado.
Integração no produto e Etapa 2 NÃO INICIADAS.**

| Campo | Estado vigente |
| --- | --- |
| Missão | `GF_SELF_UPDATER_STAGE1_ISOLATED_VALIDATION_PROTOTYPE` |
| PR / branch | #65 aberta/Draft, `work-integracao-final-20261027` |
| HEAD de entrada | `052ed3c8aefc3c90083325401dbf52fbd0de0554` |
| Contrato preservado | `gf-self-update/1`, revisão documental 1 |
| Biblioteca selecionada | Tuf 1.1.1 + BouncyCastle.Cryptography 2.7.0, versões exatas/lockfile |
| Bootstrap/custódia | Modelo definido; nenhuma chave/raiz operacional provisionada |
| Protótipo | Validação de fixtures assinadas e texto inerte; sem rede/aplicar/recuperar; Installable=false |
| Validação final | 66/66 testes de segurança, 42 cenários; SDK 10.0.402/runtime 10.0.13, Linux x64 |
| Isolamento | 142 fontes do produto avaliadas, zero do protótipo; fontes e saídas C# fora do glob do GF |
| BETA preservada | .66, fonte 6df95fdd, build privada 40716548530/artefato 12828262039, sem recompilar/baixar |
| Físico / roadmap | Avaliação parcial preservada; nenhum teste físico repetido; 27/42 |
| Canal privado | Nenhuma alteração de workflow, retenção, artefatos ou acessos; sem acesso operacional nesta etapa |

[Relatório e modelo de custódia](WORK-AUTOATUALIZADOR-ETAPA1.md) ·
[POUF](../prototipos/autoatualizador-seguro/POUF.md) ·
[Resultados](evidencias/autoatualizador-etapa1-testes.json) ·
[Recibo](evidencias/autoatualizador-etapa1.json).

Gates ainda pendentes: persistência/relógio, NTFS/handles/TOCTOU, seletor/diário,
IPC, bundles/PE reais e cerimônia operacional de bootstrap/custódia. Não inferir
homologação TUF integral, Windows, AVG ou aplicação de atualizações a partir dos
testes de fixture. **Etapa 2 não iniciada; encerrar após salvar/conferir.**

Preservar AVG/proteções, flags no e remoto bloqueado, drivers desativados, VLC
somente inventário e COM opcional/consultivo. Nenhuma publicação, build de produto,
merge/Release/deploy ou mudança de Windows/Worker/D1/DNS. O relato físico aceito
na Etapa 9 não precisa ser refeito por esta mudança no protótipo isolado.

## Registro da Etapa 0 — histórico preservado


28/10/2026, America/Fortaleza. **Etapa 0 concluída no contrato/análise de ameaças;
implementação não iniciada.** Uma etapa por sessão, sem avanço automático.

| Campo | Estado vigente |
| --- | --- |
| Missão | `GF_SELF_UPDATER_STAGE0_CONTRACT_AND_THREAT_MODEL` |
| Repositório / PR | `walissonminel-1991/gerenciador-final`, #65 aberta/Draft |
| Branch | `work-integracao-final-20261027` |
| HEAD de entrada | `c701ae16df5c1685967a9b8d299e02e811078f8d` |
| Fonte funcional preservada | `6df95fdd776644cd4b248b5cea61108974ca9ce9` |
| BETA preservada | 5.13.11.66, build privada 40716548530, tentativa 1, artefato 12828262039 |
| Avaliação física vigente | Etapa 9 parcialmente concluída; aceitação funcional limitada e lacunas formais registradas, sem repetição por rotina |
| Contrato | `gf-self-update/1`, revisão documental 1, 16 requisitos e 18 ameaças |
| Aceitação futura | 18 casos especificados, nenhum executado nesta sessão |
| Entrega desta etapa | Somente Markdown/JSON na branch da PR; sem produto, helper, chave, metadado assinado ou pacote novo |
| Canal privado | Sem leitura operacional necessária, sem alteração de workflow, retenção, artefato, permissões ou acesso |
| Roadmap | 27/42, sem promoção |

[Contrato técnico e ameaças](WORK-AUTOATUALIZADOR-ETAPA0.md) ·
[Recibo](evidencias/autoatualizador-etapa0.json) ·
[Avaliação física preservada](WORK-INTEGRACAO-ETAPA9-PC-FORTE-RESULTADOS.md).

Decisões documentadas: autenticação editorial por TUF/âncora incorporada;
versionamento numérico e antirreplay; importação explícita inicial sem token
GitHub; pacote íntegro e perfil local; confirmação antes de aplicar; bundle por
versão com seletor, diário e recuperação; preservação de dados/AVG/permissões.
A .66 atual não recebe esses recursos retroativamente e sua pasta fica inteira.

Próxima etapa proposta, **somente em sessão própria**: Etapa 1 — selecionar e
documentar biblioteca/POUF, modelo de bootstrap/custódia e protótipo isolado de
validação com fixtures. Sem ativar aplicação real, transporte de rede, canal,
assinatura de produção ou trocar bytes da .66 por consequência deste registro.
Detalhes de compartilhamento de handles, durabilidade de seletor/diário e
recuperação no Windows precisam de prova antes de entrar no produto.

Não repetir testes físicos concluídos. Quando existir alteração efetiva no
produto/novo pacote, executar CI do SHA correspondente e somente regressões
afetadas; não usar esta etapa documental para exigir nova manutenção no PC.

Preservar diagnóstico remoto bloqueado, `PILOT_ENABLED=no`,
`GITHUB_EXPORT_ENABLED=no`, consentimento, relatórios, AVG/proteções; VLC somente
inventário sem ações/consultas/testes específicos; WinGet COM opcional,
consultivo e não bloqueante; drivers de instalação/remoção/reversão desativados.
Sem Worker/D1/DNS, merge, Release, deploy, publicação ou alteração do canal
privado. Históricos da integração e da homologação parcial não são reabertos.

Cadência de cinco dias condicionada à aprovação e qualidade; revisão de limpeza
dois dias após publicação planejada, sem adiar expurgo obrigatório. Nenhuma
automação ou mudança de retenção. **Encerrar após salvar e conferir a Etapa 0.**
