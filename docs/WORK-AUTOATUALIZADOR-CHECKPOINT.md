# Autoatualizador seguro do Gerenciador Final — checkpoint

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
