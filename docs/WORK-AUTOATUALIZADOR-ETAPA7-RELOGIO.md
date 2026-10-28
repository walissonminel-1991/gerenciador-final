# Etapa 7 — revisão dirigida do relógio TUF

**Inspeção estática, sem execução ou alteração. G2 continua bloqueado.**
Contrato gf-self-update/1 intacto. A numeração normativa citada é da TUF
1.0.36, não dos comentários de seções existentes na biblioteca.

## Fonte e âmbito do parecer

Tuf 1.1.1 já selecionado na Etapa 1; fonte
[`mitchdenny/sigstore-dotnet@c0f5ff9747aa9b00c45a964dca275be4066cd489`](https://github.com/mitchdenny/sigstore-dotnet/tree/c0f5ff9747aa9b00c45a964dca275be4066cd489/src/Tuf).
Não houve pesquisa/troca para uma nova versão ou avaliação de outra biblioteca.

| Arquivo upstream inspecionado | SHA-256 dos bytes originais |
| --- | --- |
| TufClient.cs | 1db047bb34ce7121683f453d6d91b2bbdeb8fe87a5f1968e2ed46f2a10538fbb |
| TufClientOptions.cs | 7c4c378edaccd7ca4cf68fdbbc309a8f493e858be2c1c09ef2f0f52a05c781a5 |

Hashes conferidos com o [manifesto de origem já fixado](../prototipos/autoatualizador-seguro/stage3/tuf-source-manifest.json).
Licença MIT/avisos e locks Tuf 1.1.1/BC 2.7.0 continuam os anteriores; não foram
republicados, alterados ou reavaliados como pacote novo. Esta revisão não é
auditoria integral, parecer independente ou certificação de conformidade TUF.

O [patch](../prototipos/autoatualizador-seguro/stage3/clock-experiment.patch)
e [gerador](../prototipos/autoatualizador-seguro/stage3/prepare_clock.py)
experimentais substituem sete consultas e ajustam igualdade. Não foram
executados novamente. A evidência de execução continua a
[Etapa 3](evidencias/autoatualizador-etapa3-windows.json), com os seus limites.

## Mapa dos pontos de tempo e caminhos

Linhas abaixo referem-se ao TufClient.cs original fixado, antes do patch.
O mapa é uma leitura do fluxo, não sete testes novos.

| Método / linha | Caminho e semântica que a adaptação deve preservar |
| --- | --- |
| EnsureFinalRootValid / 533 | Checagem da raiz final depois da caminhada N+1. Raiz intermediária expirada pode autenticar a próxima; não antecipar recusa por validade |
| TryLoadLocalTimestamp / 641 | Ramo com timestamp já carregado; resultado booleano de frescor não substitui piso/assinatura |
| TryLoadLocalTimestamp / 655 | Timestamp carregado do cache é verificado antes de ser mantido como referência. UpdateTimestampAsync não usa o booleano para decidir parar; EnsureFinalTimestampValid ainda é necessário |
| TryLoadLocalTargets / 719 | Ramo de targets em memória combina versão referida pelo snapshot e validade; caminho de cache em disco usa VerifyTargetsMetadata |
| VerifyTargetsMetadata / 896 | Targets carregados/remotos e papéis delegados passam por assinatura/referência antes da validade; GF continua com perfil fechado sem delegações |
| EnsureFinalTimestampValid / 970 | Timestamp novo e uso posterior em UpdateSnapshotAsync; expiração na igualdade deve recusar |
| EnsureFinalSnapshotValid / 983 | Snapshot novo/em cache e referência exata ao timestamp; nenhuma troca de relógio deve remover essa checagem |

A [TUF 1.0.36, §5.1 e §§5.3–5.6](https://theupdateframework.github.io/specification/v1.0.36/)
fixa o tempo no início e exige expiração estritamente posterior para os papéis
finais. A validade intermediária da cadeia raiz é tratada depois da rotação.
Isso sustenta os ajustes de tempo/igualdade, sem aprovar todos os demais fluxos.

## Achados e disposições

**R7-TUF-01 — imutabilidade e duração da instância.** O patch aceita
`TimeProvider` fornecido pelo chamador. A classe sealed da
[fixture](../prototipos/autoatualizador-seguro/stage3/src/ClockExperimentTests.cs.txt)
retorna valor capturado, mas a API geral também aceita um provider variável.
Além disso, congelar no construtor e reutilizar o cliente em outra tentativa
prolongaria o horário antigo. Nada na assinatura do experimento impõe um cliente
por tentativa. É lacuna de adoção, não defeito afirmado na BETA preservada.

**A7-02 — rota técnica selecionada para futura revisão isolada:** manter a
origem fixada e projetar adaptação mínima por valor UTC copiado para campo
readonly, com factory interna do adaptador e instância exclusiva de uma tentativa.
Não expor provider/callback mutável nem permitir que arquivo importado escolha
o tempo. `required`/`init` sozinhos não validam default/offset/proveniência;
validar a entrada controlada e copiar o escalar uma vez. A referência a
TimeProvider/Stopwatch para orçamento fica fora da decisão de expiração TUF.
Cliente e cache da tentativa não são reutilizados como autoridade de novo ciclo.
O estado confiável persistido continua como insumo revalidado; uma tentativa
nova não reinicializa pisos nem descarta a cadeia para aceitar versões antigas.
Não se criou essa API, assembly ou patch nesta etapa.

**R7-TUF-02 — timestamp igual não termina o ciclo externo.** Na fonte original,
UpdateTimestampAsync, linhas 562–566, retorna ao encontrar a mesma versão;
GetTrustedMetadataCoreAsync, linhas 114–124, continua snapshot/targets. O patch
de relógio não altera esse fluxo. A TUF 1.0.36 §5.4.3.1 prevê encerrar o ciclo
sem erro nesse caso. Há divergência estática do término requerido, sem ensaio
de exploração, afirmação de pacote vulnerável ou correção aplicada.

**A7-03:** a revisão seguinte deve propagar resultado explícito de metadados
inalterados até a chamada externa, sem autorizar nova obtenção/aplicação do alvo
nesse ciclo. Não tratar um `return` local como sucesso completo. Qualificar a
semântica junto do cache é condição para adoção, sem mudar o protocolo GF.

**R7-TUF-03 — o adaptador chama refresh duas vezes.**
[FixtureValidator](../prototipos/autoatualizador-seguro/src/FixtureValidator.cs.txt),
linhas 98 e 106, chama GetTrustedMetadataAsync e GetTargetAsync. Este último
invoca GetTrustedMetadataCoreAsync novamente (linhas 228–240 upstream), que
recarrega a raiz e reseta o estado transitório (428–457). Assim, apenas substituir
as sete consultas não torna o fluxo de fixture um cliente de ciclo único pronto.
Para o futuro desenho, uma entrada de tentativa conduz uma cadeia; consulta de
metadados sem alvo e obtenção de alvo são operações alternativas da facade, não
dois refreshes silenciosos. A implementação do resultado A7-03 deve cobrir os
chamadores reais; trocar a ordem das duas chamadas não resolve por si só.

**R7-TUF-04 — fronteiras que a adaptação do relógio não cobre.** O cliente grava
raiz/timestamp/snapshot/targets no ITufCache durante o fluxo. O experimento usa
cache em memória; a fixture ainda refaz a cadeia a partir da âncora fixa.
Não ligar o cache diretamente a arquivos de produção nem eliminar essa âncora.
Persistência confiável, avanço de versões por época de chaves, invalidação após
rotação e recuperação devem ser revisados em conjunto com G1. Não descartar
avanços de confiança já duráveis por falha posterior, nem confundir versões de
papéis com sequência de release aprovada apenas na confirmação. Esta revisão
não demonstra a correção completa de cache/rotação ou permite baixar pisos.

## Relógio TUF, guarda GF e prazo são responsabilidades distintas

1. A factory interna captura UTC `T0` e início monotônico `M0` antes do primeiro
   trabalho da validação autorizada. Confronta T0 com o último horário confiável:
   retrocesso maior que cinco minutos suspende a atualização, sem mudar Windows.
2. Todos os papéis TUF, inclusive caminhos de cache/rotação, usam **o mesmo T0**;
   não recalcular ao buscar alvo, trocar papel ou aguardar I/O.
3. A guarda GF de pré-commit continua independente: orçamento compartilhado,
   artefato/papéis ainda utilizáveis, relógio observado e piso sem regressão.
   O [CycleClock existente](../prototipos/autoatualizador-seguro/src/CycleClock.cs.txt)
   usa o máximo de parede, T0+tempo monotônico e piso, e recusa deriva excessiva.
   Isso não substitui o tempo interno uniforme nem é adotado no produto aqui.
4. Ticket de validação conceitual liga tentativa, geração de confiança, bytes
   e identidades ao prazo. É estado interno de desenho, não campo novo em
   custom.gf ou manifesto aceito. Expirado/invalidado não vira válido por trocar
   T0. Após espera do usuário ou reinício, exigir nova validação/confirmar plano
   pertinente; nenhuma aplicação a partir de recibo antigo ou retry silencioso.

## Condições ainda não executadas para aceitar o delta

Reaproveitar os resultados antigos apenas para os blobs/caminhos idênticos que
efetivamente cobriram; não reapresentá-los como aprovação da nova API. Na futura
revisão autorizada, delimitar o delta: rejeição de provider/instância reutilizada,
uma captura por tentativa, término externo por timestamp igual, cache e raiz
rotacionada pertinentes, cancelamento antes de qualquer nova chamada e ligação
à guarda/prazo. Casos já provados de igualdade/assinatura não precisam de nova
bateria sem delta que os alcance. Conformidade requer vetores interoperáveis e
revisão, ainda ausentes. Nenhum caso dessa lista foi executado na Etapa 7.

**D6-02 permanece parcialmente pendente:** a rota técnica está escolhida para
desenho isolado; faltam aceite de manutenção pelo mantenedor, revisão do delta,
implementação e evidência. Não houve contato com upstream, designação fictícia
de revisor, alteração de lock ou integração. G2 não foi fechado.
