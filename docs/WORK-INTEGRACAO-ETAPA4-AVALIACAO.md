# Integração final — Etapa 4: avaliação da candidata integrada

27/10/2026, America/Fortaleza. Repositório `walissonminel-1991/gerenciador-final`.
Branch `work-integracao-final-20261027`, [PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).

**AVALIAÇÃO CONCLUÍDA. Integração 1–4 encerrada tecnicamente; prosseguimento
documentado para a Etapa 5, somente em outra sessão. BETA LOCAL não preparada;
BETA conectada/pública não liberada.** A missão ampliada 5–9 continua pendente.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Recibo da avaliação](evidencias/integracao-etapa4-avaliacao.json) ·
[Validação executada na Etapa 3](WORK-INTEGRACAO-ETAPA3-VALIDACAO.md) ·
[Adendo BETA LOCAL](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## 1. Identidade da fonte e fundamento da avaliação

| Referência | Identificador / evidência |
| --- | --- |
| Versão da fonte | 5.13.11.66, perfil BETA interna bloqueada |
| Entrada documental desta sessão | `b5c2e0e2c7fefc63896123000c3fa34d8c34a7d5` |
| Código conciliado na Etapa 2 | `06d7dce0bb47944f1efd18a10785fa77f5f0c8f7` |
| **SHA fonte validado** | **`6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8`** |
| Árvore validada / merge de teste | `e5646e6d419b47aa80e337359b1a49c9262ce6f7` / `3bb5e7b44ccc7c362e32678afa9b99791d460211`, mesma árvore |
| Base comum | `2bc9f4228fd448445fbf181652e5af93005def20`, PR #60 |
| CI principal | [40604593820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593820), success, tentativa 1 |
| Integridade externa | [40604593709](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593709), success, tentativa 1 |

As duas CIs e seus jobs foram reconferidos no GitHub: sete jobs obrigatórios
aprovados e dois condicionais skipped conforme desenho, sem reexecução.
A entrada difere do SHA validado apenas nos cinco Markdown/JSON do fechamento
da Etapa 3. Código, testes, workflows, versão e configuração do produto não
mudaram depois da validação. Não foi transferida aprovação das PRs de origem.

A PR #65 reúne em uma fonte rastreável a pré-BETA #64 e o delta BETA #62,
que já contém #61. A revisão Chrome #63 foi preservada na versão corrigida
da #64, sem reimportação. Main, PR #40 e branches de origem permanecem intactas.
O merge de teste automático não mesclou PR nem publicou release.

## 2. Funcionalidades preservadas e limites

| Área | Preservado na composição | Alcance atual |
| --- | --- | --- |
| Inventário | Leitura Win32/MSIX/ecossistemas, identidade, estados parciais e histórico | Regressões aprovadas; cobertura física integral continua pendente em I7 |
| Atualizações consultivas | Multifontes, Chrome Stable revisado, arquitetura local comprovada, cache e cancelamento | Candidato não autoriza execução; API Chrome ao vivo não foi reconsultada nesta integração |
| Atualizações executáveis | Contratos existentes MSI/WiX/NSIS/Inno, identidade, hash, assinatura, consentimento e confirmação posterior | Somente onde os gates da receita/instalação forem satisfeitos; não há garantia de atualização silenciosa universal |
| Desinstalação/resíduos | Pendência segura, bloqueio de repetição, confirmação e revalidação, proteção de dados pessoais/compartilhados | Fixtures aprovadas; sem novo ciclo real de desinstalação nos PCs |
| Drivers | Inventário, IDs, correlação INF/Store/histórico, consulta oficial e verificação consultiva | **Instalar/remover/reverter continuam indisponíveis**; consulta/INF não homologam payload completo |
| Confiabilidade/interface | Histórico/relatórios, progressos e resumos, cache de estados, layout e bandeja | Provas sintéticas; legibilidade/foco/DPI e estabilidade física não homologados |
| BETA .66 | Aviso versionado, consentimento/revogação, coordenação, fechamento/Shutdown, transporte e retenção | Código preservado, com envio bloqueado por configuração e aprovações pendentes |
| Empacotamento | Projeto/manifesto .66, x64 self-contained, licenças, integridade e contrato fechado | Build interna validada; upload BETA limitado a manifestos/evidências |

Seletores PC FORTE/PC FRACO e botão permanente de coleta permanecem ausentes
no perfil GF_BETA; privacidade/revogação acessíveis pela bandeja. Isso não
declara pronto o novo perfil restrito BETA LOCAL do adendo.

## 3. Correções incorporadas

As mudanças abaixo vieram das fontes conciliadas e foram revalidadas juntas.
Esta Etapa 4 não altera código ou cria correções adicionais.

| Origem | Problema tratado | Comportamento preservado |
| --- | --- | --- |
| #64 / consulta Chrome | Respostas JSON ambíguas e versões inválidas | Parser recusa propriedades duplicadas, versão não ASCII e divergência de identidade/canal/plataforma |
| #64 / identidade | Nome exato do Chrome não traz arquitetura no sufixo | Captura consultiva do PE registrado com metadados coerentes; ausência de prova continua unknown |
| #64 / resultados | Consulta parcial/cancelada podia perder candidato ou resumir sucesso incorretamente | Evidência em revisão/stale preservada; falha automática entra no resumo, sem renovar autoridade de execução |
| #64 / drivers | Evidência divergente podia desaparecer na correlação/histórico | Conflitos e cobertura parcial preservados; nenhum descarte ou escrita derivado da incerteza |
| #64 / desinstalação | Ausência de handle ou erro depois de iniciar podia parecer falha inicial | Pending conservado, lote interrompido, repetição/limpeza bloqueadas; resumo distingue falha, pendência, cancelamento e parcial |
| #62 / consentimento | Concorrência/revogação e confirmações antigas podiam comprometer pendência | Lease/locks, geração e revalidação; resposta antiga não apaga relatório novo e instância antiga não recria após revogação |
| #62 / encerramento | Fechamentos concorrentes, Shutdown e recuperação precisavam de coordenação | Tentativa única, cancelamento, retorno após Shutdown cancelado e persistência de melhor esforço sem rede em falha/desligamento |
| #62 / retenção | Falhas distribuídas, expiração, rate limits e migração exigiam recuperação | Código de ledger/reconciliação/retomada e SQLite sintético preservado; operação remota ainda não homologada |
| Integração / CI | Importação literal da #62 retiraria o gate obrigatório WUA dos runners | Restaurado `--official-live` em Windows 2022/2025; gates e evidências preservados, sem habilitar gestão de drivers |

VLC permanece permanentemente somente inventário, sem ações, testes
específicos novos, consultas ou verificações. WinGet COM permanece opcional,
não bloqueante e consultivo; nenhuma investigação ou reparo foi reaberto.

## 4. Cobertura antes e depois

| Área | Referência anterior | Composição validada na Etapa 3 | Interpretação |
| --- | --- | --- | --- |
| Fonte/CI | Duas frentes com CIs próprias, sem prova conjunta | Uma fonte, duas CIs no mesmo SHA | Lacunas de composição e CI própria superadas |
| Atualizadores | 368 na WORK18 .65 | 410 | Cobertura das correções pré-BETA preservada; não são instalações físicas |
| Multifontes | 159 na WORK18 .65 | 196 | Fonte/revisão Chrome e controles incorporados; não cria nova receita executável |
| Resíduos/desinstalação | 707 na base histórica; 705 após adequação do alvo excluído | 739 | 34 verificações adicionadas na pré-BETA sobre o escopo 705; não aumento físico de remoção |
| Políticas/pacote | 41 na base histórica | 47 | Contrato BETA e controles de privacidade/pacote preservados |
| Privacidade/retorno BETA | 127 transporte, 15 janela e 126 receptor/retenção na frente BETA | 127 / 15 / 126 na árvore conjunta | Compatibilidade conjunta confirmada no escopo sintético |
| MSIX / encerramento | 47 / 30 nas fontes | 47 / 30 | Regressões preservadas |
| Mutações | 36 rejeitadas anteriormente | 36 rejeitadas na composição | Proteções continuam detectando mudanças inseguras em cópias descartáveis |
| Interface | Provas separadas das fontes | 132 combinações resolução/aba, oito WORK18, 12 ciclos de bandeja; amostra de 83 PNGs | Escala simulada não substitui DPI/tema/foco físico |
| Drivers | Consulta/ABI somente leitura e evidência parcial | 173 verificações Windows 2022; 174 Windows 2025 | WUA 2022 inconclusiva; 2025 sem ofertas, sem prova positiva G1 |
| Build | Pacotes/manifestos de fontes diferentes | x64/AMD64 e 286 hashes da fonte integrada | Conferência externa somente dos três manifestos; não há pacote local entregue |

As referências WORK18 são históricas, preservadas em PROGRESSO; os números
da composição vêm do recibo da Etapa 3. Subconjuntos, repetições e runners
não são somados. A comparação WORK18 de 28 fixtures, ações 5→13 e zero
payload autorizado foi revalidada; não é ganho novo desta avaliação.
Etapas 3 e 4 não acrescentaram testes ou correções de produto.

## 5. Problemas conhecidos e lacunas

| Achado / lacuna | Evidência | Consequência |
| --- | --- | --- |
| WUA Windows 2022 excedeu o prazo | Resultado inconclusivo sem correspondências publicadas | Causa não atribuída ao GF ou ao serviço sem prova; G1 continua aberto |
| WUA Windows 2025 sem ofertas | Consulta completa com zero ofertas | Não demonstra compatibilidade/assinatura de pacote real |
| CS0649, `Program.cs:599` | Campo do botão retirado em GF_BETA sem atribuição; build sem erros e testes BETA aprovados | Aviso não bloqueante registrado, sem ocultação ou alteração cosmética do produto |
| Auditoria geral 6 e correções 7 | Ainda não executadas | Ausência de falha nos cenários da CI não demonstra ausência universal de defeitos críticos/altos |
| Perfil BETA LOCAL restrito | Ainda não preparado/validado | `transport_configured=false` prova bloqueio do transporte configurado, não toda a política local futura |
| Pacote/AVG/físico | Nenhum EXE/ZIP BETA disponibilizado; AVG não homologado | Não declarar pronta para testar no PC ou distribuir |
| Homologação conectada | Aprovações/aviso/configuração pendentes e sem ensaio real comprovado | Piloto conectado permanece bloqueado; estado remoto não auditado |

O perfil integrado chama `BeginStartupChecks` na abertura e conserva no
recorder os caminhos de consentimento RemoteBeta/persistência local. Portanto,
a preparação local deve verificar seus gatilhos e **bloquear coleta/transmissão
remotas inclusive com preferência antiga, erro e encerramento**; também deve
comprovar que toda consulta externa permitida é HTTPS oficial, estritamente
consultiva e iniciada explicitamente pelo usuário. Não basta omitir endpoint
ou usar como BETA LOCAL a build interna já existente. Nenhuma alteração desse
perfil foi antecipada nesta avaliação.

A falha antiga da #62 permanece histórica e fora do escopo do alvo excluído;
não foi reexecutada nem convertida em novo bloqueio da integração.

## 6. Roadmap ainda aberto

**27/42, com 15 checkpoints pendentes. Todos os 42 valores done preservados.**

| ID | Critério remanescente |
| --- | --- |
| I7 | Conferência de cobertura com referência equivalente nos dois PCs |
| U8 | Instalação e versão final em uso real autorizado; CI não substitui |
| R4 | Homologação física/cobertura do ciclo de limpeza confirmada e revalidada |
| R6 | Evidência de propriedade para casos históricos ambíguos, sem exclusão por semelhança |
| R7 | Desinstalação e preservação de dados nos computadores |
| P5 | Mesma build/condições e amostras comparáveis de abertura/CPU/RAM/repouso |
| P6 | Causa/resolução do incidente AVG e estabilidade do pacote exato |
| V5 | Aprovação visual/fluidez vinculada ao artefato, resolução e DPI nos dois PCs |
| D2 | Cobertura e correlação de dispositivos, pacotes e histórico Windows |
| D4 | Conferência humana da interpretação dos vínculos/atributos/histórico |
| G1 | Oferta/versão oficial e compatibilidade específica, além de consulta sem ofertas/timeout |
| G2 | Origem, integridade, assinatura e compatibilidade do payload completo |
| G3 | Instalação/confirmação de dispositivo/versão — recurso desativado |
| G4 | Reversão suportada/recuperação — recurso desativado |
| G5 | Fluxo controlado de gestão — recurso desativado |

Não é necessário chegar a 42/42 para uma BETA local limitada. Gestão de drivers
pode permanecer desativada, e pendências fora do cenário podem ser explicitadas
sem serem fechadas artificialmente. Nenhuma operação real é solicitada só para
produzir evidência ou aumentar o roadmap.

## 7. Testes físicos mínimos futuros — PC FORTE

Planejamento para a **Etapa 9**, depois da preparação autorizada da Etapa 8;
nenhum acesso ou teste no PC nesta sessão. A base física consolidada E1–E5
tem inventários 169/74 incluindo 27/19 MSIX e encerramentos normais, mas não
contém SHA/hash capaz de homologar a composição atual. E3/E5 repetem eventos.

Reaproveitar provas equivalentes antes de pedir novo teste, vinculando qualquer
resultado ao pacote exato. Não repetir a bateria inteira nem exigir PC FRACO
para iniciar o cenário local do PC FORTE; isso não fecha critérios dos dois PCs.

| Bloco priorizado | Somente o que falta comprovar | Estado desta candidata |
| --- | --- | --- |
| Integridade/AVG | Conferir origem, versão e hashes do pacote; AVG ativo; interromper diante de bloqueio | NÃO EXECUTADO |
| Abrir/fechar/estabilidade | Comportamento da nova build e relatórios locais, sem transmissão remota; não reproduzir incidente para fabricar prova | NÃO EXECUTADO |
| UI/recursos | Legibilidade, foco, rolagem e responsividade no DPI/resolução reais; navegação dos controles afetados | NÃO EXECUTADO |
| Inventário | Conferir ausências/duplicidades em escopo equivalente, aproveitando inventário anterior; não exigir novamente a contagem antiga | NÃO EXECUTADO |
| Consultas de versões | Ação explícita, fontes oficiais permitidas e tratamento de falha/cancelamento; não executar atualização real só para testar | NÃO EXECUTADO |
| Drivers | Leitura de associações/INF/histórico e interpretação de poucos vínculos representativos; consulta externa somente se permitida pelo perfil local | NÃO EXECUTADO |
| Prévias/histórico | Relatórios e prévias disponíveis sem executar desinstalador/exclusão; se dependerem de operação real, registrar NÃO EXECUTADO | NÃO EXECUTADO |
| CPU/RAM em repouso | Completar só amostras faltantes 30/60/120 s nas condições do roteiro, com identidade da mesma build | NÃO EXECUTADO |

T11 (iniciar com Windows) continua não executado; ativação/boot/login e a
validação específica de bandeja/inicialização dependem de autorização própria.
Não reiniciar o PC agora nem repetir T01–T10/T12 sem razão nova. Cada resultado
futuro será APROVADO, REPROVADO, INCONCLUSIVO ou NÃO EXECUTADO. Dados privados
ficam fora da PR pública; novo defeito gera correção específica, sem reiniciar
todas as etapas. Não instalar/remover/reverter drivers reais.

## 8. Requisitos AVG ainda pendentes

Aplicam-se os planos [AVG ativo](AVG-DESENVOLVIMENTO-SEGURO.md),
[compatibilidade](PLANO-COMPATIBILIDADE-ANTIVIRUS.md) e o
[roteiro focal físico](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md).

1. Relacionar detecção original, amostra, hash/commit e eventual resposta do
   fornecedor já disponíveis. Não reenviar amostra ou provocar bloqueio apenas
   para gerar evidência; revisão do incidente/integridade precede teste local.
2. Registrar resultado aplicável ao pacote exato, mantendo AVG/SmartScreen/UAC
   e demais proteções. Não criar exclusões, retirar quarentena para testar,
   ofuscar ou desativar proteção.
3. Evidência de procedência/hash/assinatura da CI não é varredura AVG. O build
   atual não possui assinatura Authenticode comprovada; não declarar assinatura
   ou homologação por atestado de CI, ausência de alerta antigo ou contagem de testes.
4. No futuro teste autorizado, registrar versão/módulo/resultado do AVG de
   forma sanitizada. Se bloquear, preservar o evento e interromper; causa e
   resolução exigem evidência, sem presumir falso positivo.
5. Se bytes mudarem por assinatura, configuração ou empacotamento, refazer
   hashes/verificações do artefato correspondente. Aprovação de bytes anteriores
   não homologa automaticamente o novo pacote.

## 9. Homologação conectada privada — requisitos separados

O [roteiro conectado](BETA-66-HOMOLOGACAO-CONECTADA.md) e
[BETA-APROVACOES](BETA-APROVACOES.json) continuam como referência. Foram
consultadas as configurações versionadas, não o estado operacional remoto.
Campos vazios não demonstram falha de Worker/D1/DNS; demonstram falta de
aprovação/evidência registrada em fonte para liberar esta candidata.

| Condição | Prova já existente | O que continua pendente |
| --- | --- | --- |
| Fonte única e CI própria | PR65/Etapas 2–3, duas CIs no SHA exato | Critérios técnicos de integração atendidos no escopo testado |
| Aviso/configuração | Aviso r3 e guardas preservados; allowlist/hash revisado/endpoint vazios | Responsável/contato, aviso final/transferências/retenção e origem exata revisados/aprovados em fonte |
| Receptor/retenção | 126 testes com SQLite/APIs fictícias | HTTPS/WAF/origem, D1/schema/migração em cópia, falhas/rollback e retenção real comprovados em ambiente autorizado |
| GitHub privado | Motor/ledger e controles sintéticos | Destino/ator/permissões verificados, criar/ler/excluir com confirmação positiva e recuperação no ensaio descartável autorizado |
| Operação | Flags versionadas no e propostas de cron/observação | Monitoramento, backlog/expiração, orçamento/limites efetivos e recuperação comprovados; sem ativação automática |
| Cliente/Windows/AVG | 127 transporte e 15 janela em isolamento | Pacote conectado exato, consentimento/revogação/retorno, falhas reais e AVG em ambiente autorizado |
| Entrega ao grupo/pública | distribution_approved=false | Decisão específica posterior, distinta de CI e do sucesso local |

Nenhuma aprovação é preenchida apenas para superar um gate. Qualquer
configuração/aviso novo exige fonte/CI/build próprios e hashes correspondentes.
Não executar ensaio remoto, migração, deploy, ativação de flags ou convites
nesta missão. A BETA LOCAL é um escopo separado do adendo; não substitui nem
homologa o piloto privado, e seu sucesso não libera publicação pública.

## 10. Condições para a futura BETA LOCAL TESTE

O caminho autorizado pelo adendo permanece sequencial, uma etapa por sessão:

| Etapa | Condição objetiva antes de avançar |
| --- | --- |
| 5 — Organização segura | Auditar/organizar com rastreabilidade; separar correções seguras de propostas destrutivas que exigem autorização específica |
| 6 — Auditoria geral | Matriz de defeitos com localização/reprodução/impacto/evidência, sem acessar PCs ou reabrir fontes excluídas |
| 7 — Correções | Resolver críticos/altos relevantes ao cenário sem contornar proteções; CI aplicável aprovada no novo SHA quando houver mudança |
| 8 — Preparação local | Portátil x64 rastreável, dependências/licenças/recursos, manifesto/SHA-256, BETA LOCAL TESTE — NÃO HOMOLOGADA; perfil restrito comprovado |
| 9 — Homologação física | Execução manual pelo mantenedor e análise dos resultados autorizados; nenhuma liberação conectada/pública automática |

Na Etapa 8, **coleta e transmissão remotas devem permanecer indisponíveis**
inclusive em falha/saída/preferência antiga; nenhum token, credencial ou endpoint
privado ativo. Consultas externas apenas HTTPS oficiais consultivas e iniciadas
pelo usuário. Não depender de Worker/D1/DNS/EU.org. Gestão de drivers continua
desativada; não adicionar serviço persistente, instalador automático ou elevação
obrigatória sem necessidade comprovada.

O contrato atual só conserva manifestos BETA na CI. A entrega privada do pacote
ao mantenedor precisa de forma autorizada e verificável dentro desse contrato.
Se ele impedir o executável, registrar a causa e propor o canal privado adequado;
não remover bloqueios nem publicar Release/ZIP indiscriminadamente. A entrega
deverá explicar retorno à versão anterior, identidade/CI/hash, funções disponíveis/
desativadas, limitações e roteiro mínimo. Nenhum pacote foi gerado nesta etapa.

## 11. Escopo documental e ponto de parada

Esta avaliação atualiza somente checkpoint, matriz, PROGRESSO, ROADMAP,
este relatório e seu recibo JSON. Mantidos código, testes, workflows, versão,
artefatos anteriores, configurações de privacidade e históricos. Nenhuma suíte
de produto ou CI foi repetida; usadas as evidências verificadas por SHA da Etapa 3.
O commit usa `[skip ci]` e não é uma nova build aprovada.

Conferências documentais: seis arquivos, 62 links internos e 15 linhas M01–M15
válidos; somente `integration_final` alterado no PROGRESSO, com 42 estados e
históricos preservados. O verificador existente `verificar_pacote.py --metadata`
aprovou a coerência dos metadados herdados: candidata .66 pending e aprovação
histórica .65. Essa conferência não substitui as CIs próprias da integração.

**Integração 1–4 concluída como registrada. Próxima sessão: somente Etapa 5 do
adendo — organização e limpeza segura do GitHub. Não iniciada aqui.** Sem
merge, release, deploy, distribuição, operação em PC, driver, infraestrutura
remota, envio de diagnóstico ou alteração das flags. Roadmap **27/42**.
