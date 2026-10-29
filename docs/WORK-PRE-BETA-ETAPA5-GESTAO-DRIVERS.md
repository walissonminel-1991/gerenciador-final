# Pré-BETA — Etapa 5: estudo da gestão de drivers

27/10/2026 (America/Sao_Paulo). Branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Base conferida: `466092ce237eba098ba83612c6b7774ec8226e5e`.
**Estudo concluído; gestão desativada; G1–G5 abertos; Etapa 6 não iniciada.**

## Decisão e resultado concreto

É viável definir o contrato de uma futura operação por dispositivo. A base
atual ainda não permite habilitá-la: falta prova positiva de oferta/versão e
compatibilidade completas (G1), do pacote inteiro (G2) e de recuperação
aplicável ao dispositivo (G4). O resultado desta etapa é este estudo e sua
[evidência estruturada](evidencias/pre-beta-etapa5-estudo-drivers.json).
Não foi introduzido protótipo executável, executor nativo, configuração de
ativação ou botão. O produto continua exclusivamente consultivo.

Somente quatro arquivos Markdown/JSON mudam. Código, testes, workflows,
manifesto, versão e roadmap oficial permanecem nos mesmos blobs da base.
O recibo documental usa `[skip ci]`: não há um novo build da Etapa 5.

## Retomada e evidência aproveitada

A CI [40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517)
foi reconferida na API: código `386e7cd5263266a0478e60139e59b036f1d69e31`,
tentativa 1, success; seis jobs obrigatórios success, dois condicionais skipped.
Seu [recibo](evidencias/pre-beta-etapa4-ci-40565220517.json) continua válido
para esse código: 739 resíduos/desinstalação, 410 atualizadores, 196 multifontes,
36 mutações rejeitadas, 47 MSIX, 30 encerramento e 41 Python/políticas.
Esses números são anteriores, não execuções desta etapa.

PR #64 aberta e Draft, base WORK18 `2bc9f4228fd448445fbf181652e5af93005def20`.
PRs #60/#61/#62/#63 conservam os SHAs do checkpoint; suas CIs foram
reconsultadas, sem reexecução. A #62 conserva a CI principal aprovada e a
falha externa histórica; não foi reinvestigada. Main permanece
`410d65e23546976db456337c71e4705f10217d36`.

## Lacunas entre os dados existentes e uma operação segura

| Entrada atual, lida nesta etapa | O que permite | O que ainda falta para gestão |
| --- | --- | --- |
| `DriverInventoryEntry`: instância, HardwareIDs, presença, fornecedor, versão e INF | Seleção consultiva da instância; conflitos são preservados pela Etapa 2 | Classe/criticidade, estado operacional PnP, dependências e identidade atual revalidados imediatamente antes de qualquer futura escrita |
| `DriverOfficialOffer`: UpdateID/revisão, HardwareID, fornecedor, modelo, título e data | Vínculo exato de oferta para consulta | Versão numérica e compatibilidade completas vinculadas ao pacote; título/data não decidem atualização nem downgrade |
| `DriverStoreEvidence` / `DriverPublishedPackage` | Vínculo instalado ↔ Store e associações observadas | Cobertura suficiente de dependentes; zero associações observadas não comprova desuso ou disponibilidade de reversão |
| `DriverInfVerificationResult` | Hash do INF e verificação nativa de INF/catálogo | Todos os arquivos, pertença ao catálogo, cadeia/política aplicável, origem, SO/arquitetura e dependências; INF assinado não aprova o payload |
| `DriverWindowsHistory` | Eventos explícitos no trecho compreendido do log Windows | Histórico parcial não comprova backup recuperável, saúde atual ou conclusão da operação proposta |
| Relatório e diálogo somente leitura | Consulta oficial e inspeção do INF, sem autorização de escrita | Nenhum contrato existente de instalação, persistência transacional ou recuperação |

Os gates existentes `DriverOfficialMatch.CanInstall`,
`DriverInfVerificationResult.CanInstall/CanMaintain` e
`DriverPublishedPackage.CanMaintain` continuam constantes false. Nenhum
leitor foi promovido a autoridade de instalação.

## APIs oficiais examinadas e decisões de projeto

As conclusões das APIs abaixo vêm da documentação Microsoft consultada em
27/10/2026; as decisões de projeto são inferências deste estudo. Nenhuma API
de escrita foi invocada.

| Fonte primária | Comportamento documentado relevante | Decisão para um projeto futuro |
| --- | --- | --- |
| [S1 — Driver Store](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/driver-store) e [S2 — preinstalação](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/preinstalling-driver-packages) | Staging e instalação são operações distintas; staging copia o pacote validado para o Store. SetupCopyOEMInf pode realizar essa preparação | Staging já é escrita com possíveis efeitos futuros do PnP; não serve como teste de leitura. Não executar nesta missão nem editar arquivos do Store diretamente |
| [S3 — DiInstallDriverW](https://learn.microsoft.com/en-us/windows/win32/api/newdev/nf-newdev-diinstalldriverw) | Pode instalar em dispositivos compatíveis presentes. TRUE também pode significar apenas preinstalação; FORCE_INF pode substituir por opção pior | Não adotar como operação supostamente isolada por instância; não usar FORCE_INF ou instalação em conjunto. Retorno positivo não confirma versão/dispositivo |
| [S4 — DiInstallDevice](https://learn.microsoft.com/en-us/windows/win32/api/newdev/nf-newdev-diinstalldevice) | Recebe dispositivo específico e driver pré-instalado. Sem DriverInfoData, procura o melhor disponível; flags incluem driver nulo e CopyINF | Candidata a estudo futuro com identidade explícita e driver selecionado. Não comprova isolamento de todos os efeitos do pacote; bloquear dependências/efeitos não delimitados. Não usar driver nulo nem ampliar escopo por flags |
| [S5 — DiRollbackDriver](https://learn.microsoft.com/en-us/windows/win32/api/newdev/nf-newdev-dirollbackdriver) | Depende de backup anterior; sem ele retorna ERROR_NO_MORE_ITEMS. Pode remover o pacote substituído se não inbox e sem outros dispositivos. Pode requerer reinício | Reversão não é garantia nem consulta de disponibilidade. Não chamar para sondar suporte; exigir prova e recuperação previamente validadas. Sem rollback automático ou fallback por desinstalação |
| [S6 — DiUninstallDriverW](https://learn.microsoft.com/en-us/windows/win32/api/newdev/nf-newdev-diuninstalldriverw) | Pode remover o pacote de todos os dispositivos que o usam, substituindo-o por outro ou driver nulo | Excluída do desenho de recuperação por dispositivo; remover pacote não equivale a desfazer a operação com segurança |

O tratamento de NeedReboot também integra a futura transação: se o sistema
indicar reinício necessário, manter pendência e registrar a intenção antes
de qualquer solicitação ao usuário. O projeto deverá justificar e homologar
o uso do parâmetro conforme a API; não ignorar a indicação nem prometer
suprimir toda interface nativa. Não houve reinício ou alteração desses fluxos.

## Contrato proposto, sem implementação ou ativação

A separação proposta é entre evidências consultivas, decisão de elegibilidade
e um executor futuro específico. Nesta branch **a decisão de execução é
sempre desativada**, independentemente de consentimento ou dados aparentes.
O contrato abaixo descreve requisitos futuros, não permissões concedidas.

1. **Evidências imutáveis e vinculadas:** instância selecionada, HardwareIDs,
   INF/versão/fornecedor atuais, identidade da oferta, pacote completo com
   digest, destino de SO/arquitetura, dependentes e plano de recuperação.
   Cada leitura informa origem, instante, cobertura e falhas. Sem identidade
   inequívoca, não produzir comando executável.
2. **Decisão conservadora:** G1/G2 comprovados para o mesmo pacote/dispositivo,
   ambiente de teste adequado, recuperação validada e consentimento específico.
   Bloquear dispositivo crítico ou de criticidade desconhecida, escopo
   compartilhado não delimitado, firmware/BIOS e pacote com efeitos adicionais
   não compreendidos. Administrador não substitui nenhum desses requisitos.
3. **Revalidação e exclusão mútua:** reler a instância e o pacote antes de
   qualquer escrita; divergência invalida o consentimento. Uma operação ativa
   ou pendente impede outra tentativa; não usar nome comercial como chave.
4. **Registro durável futuro:** persistir intenção e identidade antes do
   primeiro efeito, com escrita atômica em área privada apropriada ao futuro
   componente privilegiado. Falha em persistir impede início. Depois de queda
   ou timeout, retomar somente reconciliação; não repetir instalação às cegas.
   Local, ACL e fronteira de privilégio ainda exigem projeto próprio; nenhum
   armazenamento ou helper foi criado nesta etapa.
5. **Confirmação independente:** depois da API, recolher a mesma instância,
   INF, versão/fornecedor, assinatura e estado operacional, conferir o pacote
   esperado e os dependentes delimitados. Ausência de leitura, troca de
   instância, conflito ou erro conserva estado inconclusivo. Após reinício,
   repetir essa confirmação antes de declarar conclusão; não reiniciar
   automaticamente. Código de sucesso isolado nunca fecha G3.
6. **Recuperação explícita:** cancelamento anterior ao efeito encerra sem
   escrita. Depois do possível efeito, manter pendência e reconciliar,
   conservando o erro nativo. Só uma nova ação específica, com suporte e
   recuperação validados, poderia pedir reversão. Falha ou indisponibilidade
   não provoca remoção/limpeza, troca forçada ou repetição automática.

Identificadores e provas detalhadas permanecem locais e privados no desenho;
diagnóstico público usa apenas códigos/contagens. Não incluir comandos,
serial, ID de dispositivo, caminhos pessoais, credenciais ou logs brutos.

## Critérios de revisão futura do contrato

São **casos esperados, ainda não executados**, e não testes aprovados nem
evidência de instalação ou rollback. Os IDs também estão no JSON do estudo.

| Caso | Condição hipotética | Resultado obrigatório do desenho |
| --- | --- | --- |
| E5-01 | Todos os metadados parecem completos, gate atual desativado | Desativado; zero chamadas de escrita |
| E5-02 | Oferta exata, só INF/catálogo verificados | Bloqueado por pacote incompleto |
| E5-03 | Inventário parcial, conflito de instância ou criticidade desconhecida | Bloqueado; preservar |
| E5-04 | Dois dispositivos compartilham pacote, impacto não delimitado | Bloqueado; nenhuma operação em lote |
| E5-05 | Pacote/dispositivo muda depois do consentimento | Consentimento invalidado; zero início |
| E5-06 | Sem consentimento ou cancelamento antes do primeiro efeito | Cancelado antes do início; zero escrita |
| E5-07 | Falha de persistência da intenção | Bloqueado antes do início |
| E5-08 | Timeout, queda ou cancelamento após possível efeito | Pendente; apenas reconciliar, sem retry/kill/rollback automático |
| E5-09 | API retorna sucesso, mas INF/versão não correspondem ou saúde é desconhecida | Inconclusivo; nunca concluído por retorno |
| E5-10 | Reinício necessário | Pendente até consentimento de reinício e posterior confirmação; sem reinício automático |
| E5-11 | Backup não demonstrado ou ERROR_NO_MORE_ITEMS | Recuperação indisponível; preservar e informar, sem desinstalar |
| E5-12 | Nova solicitação com transação anterior pendente | Bloqueada até reconciliar a anterior |

## Validação desta entrega e pendências

Validação documental: JSON válido, fontes/casos únicos e referências locais
resolvidas; diff restrito aos quatro documentos, sem whitespace inválido.
Identidade de todos os demais blobs conferida contra a base de retomada.
**Zero testes de produto novos ou reexecutados, zero pacotes baixados e zero
operações de drivers.** A CI da Etapa 4 foi consultada, não reexecutada.

G1/G2 dependem das provas específicas descritas na Etapa 2. G3 exige
instalação e confirmação reais em ambiente autorizado; G4 exige suporte e
recuperação demonstrados, inclusive falha/reinício; G5 depende de G1–G4 e
validação supervisionada inicialmente descartável. Este estudo não conclui
nenhum desses checkpoints. **Roadmap mantido em 27/42, 15 pendentes.**

VLC continua permanentemente excluído, sem teste/verificação/consulta/botão
novo. PCs pessoais, main, BETA privada, Worker/D1/DNS/flags e proteções
preservados. Sem elevação, mudanças de ACL, reparo WinGet, merge, release ou
deploy. Não houve consolidação das evidências físicas da Etapa 6.

Próxima sessão, se solicitada: somente **Etapa 6**, partindo de E1–E5 já
recuperadas e dos relatos, sem repetir bateria completa e sem operar drivers.
Conferir primeiro o HEAD documental desta etapa na PR #64 e a CI do código
386e7cd; não confundir o novo commit documental com uma build validada.
