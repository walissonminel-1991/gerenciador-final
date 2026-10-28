# Autoatualizador seguro do Gerenciador Final — checkpoint

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
