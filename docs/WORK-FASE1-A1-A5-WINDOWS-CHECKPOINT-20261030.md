# WORK — GERENCIADOR FINAL — Checkpoint corretivo A1–A5

Missão: corrigir a candidata experimental na branch autorizada, validar Windows e fornecer evidências para o CHAT ALTO.

**Parecer: correções de interoperabilidade e contenção validadas; incremento ainda incompleto quanto à prova positiva. A prova positiva de item na interface permanece PENDENTE. Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8; zero recuperações automáticas autorizadas.**

## 1. Referências e preservação do trabalho anterior

- Repositório: walissonminel-1991/gerenciador-final.
- Branch exclusiva de escrita: experimento-g5-ipc-chat-alto-20261029.
- HEAD conferido antes da escrita: ecf2259ccad8a9c2705e01519272130a29102e79.
- Candidata anterior preservada: 85a955e7cf8da7c2e5eea6f7e5f4ad810a648cc6.
- Base da auditoria anterior: fd88cd30a2dee25576fc76df6c6c6d06636e0acc.
- **SHA técnico final congelado: e0b8bc414d6756f542b7b3af6e82a820b34aa70b.**
- [Diferenças técnicas deste trabalho](https://github.com/walissonminel-1991/gerenciador-final/compare/ecf2259ccad8a9c2705e01519272130a29102e79...e0b8bc414d6756f542b7b3af6e82a820b34aa70b).

O commit técnico descende do HEAD documental informado. Nenhum trabalho anterior foi revertido. Este checkpoint e seu JSON são documentais; não alteram o SHA técnico validado.

## 2. Causas raiz e correções

| Ocorrência | Causa demonstrada | Correção e alcance |
| --- | --- | --- |
| CI 41141320091, job 124358021285 | Construção direta de winreg.HKEYType sobre o HKEY nativo causa TypeError no Python 3.13, antes da entrada no contexto de limpeza. | Função _create_new_trial_registry_key declara assinaturas Win32, exige REG_CREATED_NEW_KEY antes de qualquer escrita, empresta o inteiro HKEY a winreg e executa RegCloseKey em um único finally. O handle é invalidado localmente após o fechamento. Não há objeto PyHKEY concorrendo pela propriedade do handle. |
| CI intermediária 41140714543, job 124354008051; SHA f97b5dac685008159cfeab5531b1a44fad833a7a | O teste esperava aceitação positiva, mas o observador recusou a prova de item. A expectativa pertence ao código intermediário anterior ao endurecimento atual. | A expectativa antiga não foi restaurada. As duas tentativas atuais verificam contenção, controle negativo funcional e ausência de promoção experimental. A presença real do item continua pendente. |
| Teste de colisão A3 | A verificação do supervisor ocorria depois da remoção da chave pelo próprio teste e aceitava também “chave já ausente”. Isso podia mascarar uma exclusão indevida pelo supervisor. A preparação do teste também usava CreateKeyEx sem exigir criação nova. | Preparação nativa com disposição obrigatória; chamada ao supervisor enquanto a chave estrangeira de teste ainda existe; exigência de HOLD, presença da chave e valor/tipo/conjunto intactos. Teardown somente pelo handle original e com conteúdo exato conferido. |
| Teste de interrupção A4 | Existência do marcador podia preceder JSON completo. A leitura posterior de stdout não tinha limite próprio, apesar do polling anterior. | Acknowledgement ACTIVE após a criação completa, lido por fila/thread com limite de 20 s. Só depois são lidos o marcador e a chave. Nenhum prazo normal foi aumentado. |
| Timeout do observador no C# | O throw imediato por timeout desviava da limpeza supervisionada enquanto o coordenador ainda estava vivo. | Rotina SuperviseRealObserver compartilhada: encerrar o observador possuído, confirmar sua saída, executar a limpeza conservadora e depois rejeitar o timeout. Falha ao comprovar término preserva o estado e bloqueia. O helper de limpeza também é encerrado e aguardado se exceder o prazo. |
| Aceitação do recibo de limpeza | O C# aceitava qualquer saída que contivesse um prefixo diagnóstico com exit code zero. | Exigidos exit code zero e um dos três resultados completos permitidos: NO_MARKER, NOT_CREATED ou CLEANED_EXACT_FIXTURE. HOLD e saídas inconclusivas não autorizam avanço. |
| Cobertura adversarial A5 | As amostras inválidas cobriam apenas unsafe_recovery_after_real_new. | O mesmo predicado tipado valida os 29 resultados transacionais e oito contraprovas: ausente, null, string “false” e true, em cada um dos dois campos obrigatórios. Uma amostra com os dois booleanos false deve ser aceita. |

As duas CIs antigas estavam encerradas e não foram canceladas. Foi iniciada 1 CI nova para este trabalho.

## 3. Arquivos alterados

| Arquivo | Justificativa |
| --- | --- |
| prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py | Propriedade e liberação única do HKEY nativo; criação nova obrigatória; reutilização do conjunto exato de valores da fixture. |
| prototipos/autoatualizador-seguro/phase1-composed/registry_fixture_adversarial_tests.py | Colisão com preservação verificada antes do teardown, criação exclusiva inclusive na preparação, exclusão pelo handle e prontidão limitada por acknowledgement. |
| prototipos/autoatualizador-seguro/phase1-composed/Phase1Recovery.cs.txt | Supervisão compartilhada para saída e timeout; limpeza conservadora após término confirmado; aceitação estrita do resultado da limpeza. |
| prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt | Dois ensaios Windows complementares executando a mesma rotina C# de supervisão: observador morto e observador em timeout, com supervisor vivo. Os 27 cenários históricos foram preservados. |
| .github/workflows/gf-autoatualizador-fase1-real-lab.yml | Contraprovas A5 nos dois campos usando o mesmo predicado do relatório real. |

win32_process_observation.py e o produto não foram modificados neste trabalho. A revisão confirmou DarkProgramList : Control com desenho das linhas via OnPaint/DrawItem, sem uma linha HWND individual já qualificada pelo observador. O caminho Win32 atual não fornece a prova positiva exigida. Não foi demonstrada a impossibilidade de toda observação externa futura; UIA/MSAA, leitura de pixels ou outro mecanismo continuam exigindo qualificação própria. Nenhum booleano de saúde foi relaxado para obter CI verde.

## 4. CI e evidências verificáveis

- Execução: **41143130572**, tentativa **1**, job **124361884219**.
- Status final da execução e do job: **SUCCESS**.
- Runner: Windows Server 2022; Python 3.13; SDK .NET 10.0.402.
- HEAD da CI: **e0b8bc414d6756f542b7b3af6e82a820b34aa70b**.
- Início UTC: 2026-10-30T23:13:14Z; atualização final UTC: 2026-10-30T23:27:49Z.
- Etapas de dependências, verificador sem launch, 11 snapshots, quatro ensaios HKCU, exclusão dos protótipos do produto, superfície delegada, contraprova hardlink NTFS, compilação OLD/NEW e compilação C#: aprovadas.
- Dois ensaios C# de supervisão: aprovados.
- Regressão transacional: **29/29 PASS_CONTAINMENT**, zero falhas; **29/29 UnknownBlocked** no resultado e no replay.
- A5: **8/8 amostras inválidas rejeitadas**, uma forma válida aceita; gate dos 29 resultados aprovado.
- SHA-256 do log textual normalizado para LF: **37990f48c9eb86f8e9ad70da55cfac936d51319a19c02310e01bbde445d3280c** (128027 bytes).

[Commit técnico](https://github.com/walissonminel-1991/gerenciador-final/commit/e0b8bc414d6756f542b7b3af6e82a820b34aa70b) · [CI Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41143130572) · [Job e logs](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41143130572/job/124361884219).

O [JSON de evidências](evidencias/work-fase1-a1-a5-windows-checkpoint-20261030.json) contém a identificação da execução, os resultados extraídos dos logs, a lista dos casos, os hashes SHA-256 dos cinco arquivos técnicos e o hash do texto de log retornado pelo GitHub, normalizado para LF e com uma quebra final. Esse hash identifica o texto coletado; não constitui assinatura independente do GitHub nem testemunha operacional.

## 5. Resultado individual A1–A5

| Área | Resultado verificado | Limite |
| --- | --- | --- |
| A1 | 11 snapshots compartilhados A1/A2 aprovados; item falso, aviso ausente/oculto, eco e STATIC sem vínculo recusados. | Presença de item real continua PENDENTE; modelo de item visível não é prova de GF. |
| A2 | Controle negativo funcional observado nos dois ensaios reais; caso sem seed recusado; ausência não promove New. | A tentativa com seed também é recusada por falta de testemunha de item. |
| A3 | Três ensaios HKCU aprovados: colisão preservada, self-delete pelo HKEY e mutação preservada. | Não prova isolamento global nem atomicidade contra mutação hostil do mesmo objeto. |
| A4 | Encerramento forçado + supervisor Python aprovado; dois cortes com rotina C# real aprovados, incluindo timeout rejeitado após limpeza. | Coordenador vivo; perda do próprio coordenador não ganha garantia de limpeza. |
| A5 | Oito amostras inválidas rejeitadas, amostra válida aceita e os 29 resultados com os dois campos obrigatórios de tipo booleano e valor false. | Validação de forma e valor não transforma diagnóstico em autoridade operacional. |
| 27 históricos | 27/27 PASS_CONTAINMENT; resultado e replay UnknownBlocked. | Contenção; zero recuperações. |
| Dois casos novos | 2/2 PASS_CONTAINMENT; OLD/NEW preservados; nenhum registro positivo aceito; replay UnknownBlocked. | Zero PASS_EXPERIMENTAL_ACTIVATION. |

As 27 contenções históricas mantêm nomes e ordem. Seus corpos não foram relaxados por esta correção. A regressão integrada exige preservação de OLD/NEW e canário, rechecagem dos pins, fences e journal, replay UnknownBlocked, nenhum PASS_RECOVERY_LAB_ONLY e nenhum PASS_EXPERIMENTAL_ACTIVATION.

Nos dois ensaios de interface, a recusa do item esperado e a observação do controle negativo são resultados distintos. A tentativa com seed não se tornou prova positiva por também ser recusada. O resultado PASS_CONTAINMENT nesses casos significa recusa segura; não significa ativação experimental validada.

## 6. Windows real versus modelagem

- **Modelagem em memória:** os 11 snapshots de A1/A2 exercitam o predicado puro. Executá-los em um runner Windows não os transforma em evidência da interface real do GF. Também foram executados localmente por extração AST do mesmo predicado, sem chamadas Win32.
- **Amostras JSON em memória no PowerShell Windows:** oito variantes inválidas e uma válida de A5. O mesmo predicado é aplicado depois aos 29 resultados emitidos pelo C#.
- **Registro real no Windows:** colisão determinística na criação nativa; exclusão com handle aberto e subchave vazia; mutação observável preservada; TerminateProcess do worker seguido da limpeza pelo supervisor Python.
- **Supervisão C# real no Windows:** dois cortes adicionais exercitam a mesma rotina de espera/limpeza de RealOwner com worker Python e HKCU reais. A morte do observador e o timeout mantêm o supervisor vivo. O teste confirma ausência da chave exata e do marcador; o timeout continua rejeitado.
- **GF real:** OLD/NEW compilados em diretórios temporários, processos e pesquisa de interface reais nos cenários integrados. O item individual não foi comprovado. As contraprovas de efeitos externos usam fixtures inertes, sem operações de manutenção do produto.
- **Não ensaiado como garantia:** perda total do coordenador durante a fixture HKCU, reboot, queda de energia, concorrência hostil contínua no mesmo objeto do Registro e isolamento global contra o mesmo usuário.

O ensaio determinístico de colisão comprova o tratamento da disposição nativa; não é uma campanha de stress de todas as possíveis corridas do Registro. A exclusão pelo handle foi verificada no Windows; isso não elimina toda janela de alteração do conteúdo do mesmo objeto entre conferência e exclusão.

## 7. Prova positiva e limitações remanescentes

**POSITIVE_UI_PROOF_PENDING. Não há autorização de PASS_EXPERIMENTAL_ACTIVATION.**

A ausência do aviso vazio, o eco do texto no campo de busca, a contagem genérica de aplicativos e o seed HKCU não provam individualmente a apresentação do aplicativo esperado. A chamada real permanece sem identificador de lista/item qualificado e recusa presença positiva.

O próximo incremento precisa apresentar evidência externa de item real, vinculada ao processo, controle e pesquisa corretos, com observação atual e contraprovas para ausência, texto ecoado, item errado, item oculto e conteúdo desatualizado. Se isso não puder ser obtido sem alterar o produto, o resultado deve continuar pendente.

Continuam sem prova independente H1 (história local resistente à restauração pelo mesmo usuário), H2 (despacho obrigatório de todo NEW) e H3 (quiescência dos efeitos delegados e externos ao Job). Job vazio ou processo encerrado não elimina esses efeitos. Os testes mantêm UnknownBlocked nesses casos.

O marcador/token local de HKCU permite limpeza conservadora no laboratório cooperativo; não é autoridade protegida contra um adversário do mesmo usuário. Desvios observados de valores, tipos ou subchaves exigem preservação. O teste de mutação deixa intencionalmente a chave alterada no runner descartável. Nenhuma varredura por prefixo foi adicionada.

Se o próprio coordenador morrer, sua rotina de supervisão não pode garantir limpeza. Também não se comprovou atomicidade entre a última leitura de conteúdo e a exclusão diante de alteração hostil do mesmo objeto. Não se deve generalizar o resultado da CI para um sandbox.

TUF permanece com raízes e signatários de laboratório. Os controles de manifesto, hashes, identidade NTFS, pins e caminho da imagem permanecem; a CI não estabelece, por isso, garantia completa de todos os bytes efetivamente executados, dependências carregadas ou efeitos externos. Não houve nova autoridade operacional.

## 8. Restrições preservadas

- Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8.
- Zero recuperações automáticas autorizadas; HOLD é contenção.
- B+A portátil, verificação TUF/NTFS/TOCTOU e diário L18 preservados.
- main permanece em 410d65e23546976db456337c71e4705f10217d36.
- PR #65 permanece aberta Draft, com HEAD ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc.
- Nenhuma alteração no produto, BETA .66, serviços, ACL/políticas, TPM ou backend.
- Nenhum merge, release ou deploy. Nenhum pacote executável foi publicado por este trabalho.

## 9. Recomendação ao CHAT ALTO

A candidata pode avançar **somente para o próximo incremento experimental dedicado à prova positiva externa de item**. A CI verde valida as correções e as contenções listadas; não satisfaz a demonstração positiva restante nem encerra o incremento completo ou a Fase 1.

Uma nova auditoria independente WORK é necessária antes de aceitar a demonstração positiva ou concluir a Fase 1. Para evitar consumo prematuro, encaminhar a próxima candidata quando houver observador positivo qualificado, contraprovas dirigidas Windows, SHA congelado, CI e logs. O SHA atual já permite revisão dirigida da propriedade dos handles, da supervisão e do predicado tipado sem repetir a suíte histórica por rotina.

Não foi demonstrado, nos casos executados, um caminho que promova New experimental a rollback ou READY operacional. Isso não é prova universal de ausência de todos os caminhos possíveis.

A revisão independente não deve ser substituída pela CI nem pela autorrevisão do implementador. A aprovação atual é limitada ao comportamento e às contraprovas explicitamente executados. A conclusão da Fase 1 e a saúde positiva permanecem sem aprovação.
