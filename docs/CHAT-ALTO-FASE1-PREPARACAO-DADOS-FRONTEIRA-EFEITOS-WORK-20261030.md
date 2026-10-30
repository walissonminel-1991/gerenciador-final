# Fase 1 — preparação de dados e fronteira de efeitos no L18

30/10/2026, America/Fortaleza. Missão WORK de desenvolvimento experimental autorizada pelo mantenedor. **Fase 1 aberta; READY=false; G1–G8 BLOCKED 0/8; nenhuma recuperação automática autorizada.**

## Âncoras e escopo

Repositório `walissonminel-1991/gerenciador-final`; única branch escrita: `experimento-g5-ipc-chat-alto-20261029`.

- HEAD inicial confirmado: `0ca6dd56b3e7237358092cffe4c8729f9315f122`, árvore `d8dbd8ce9b13198e016e0c354f936d1ef9c3135f`; checkout limpo e sem AGENTS.md encontrado.
- Último código anterior: `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`. Run anterior `41089673054`, job `124179955658`, completed/success, relatório estruturado reconferido: 22 PASS_CONTAINMENT, zero falhas/recuperações.
- Commit técnico desta missão: `5aa02f7decf93abfb7d03fa64c2ad9f5f057b96a`; árvore `cad6c142432e0201dcaa8920e5b89553d413460a`.
- PR #65 conferida antes da escrita: aberta, Draft, merged=false, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`; nenhuma escrita nessa PR/branch.
- HEAD documental de encerramento: o commit que contém este checkpoint e seu recibo; seu SHA completo é entregue no relatório final do chat, sem autorreferência impossível dentro do próprio commit.

Ponto de partida: [contrato e limites LC01–LC03](CHAT-ALTO-FASE1-CONTRATO-LANCAMENTO-L18-LIMITES-20261030.md). O contrato integral `L18-GF-LAUNCH/0` continua pesquisa não operacional: esta implementação cobre somente a separação local de preparação e observação, não o testemunho W protegido.

## Implementação

Quatro arquivos técnicos foram alterados. Phase1Bridge.cs.txt, LabProcessJob.cs.txt, o workflow, os caminhos relevantes do L18, Program.cs, DiagnosticTrace.cs e LocalReliabilityHistory.cs foram examinados. Código compartilhado L18–L22 e proteções TUF/B+A/NTFS permanecem intactos.

| Arquivo | Comportamento implementado |
| --- | --- |
| `prototipos/autoatualizador-seguro/phase1-composed/Phase1Recovery.cs.txt` | Saída `prepare-only` no mesmo RealOwner; cortes `prepare-before-verify`/`prepare-after-verify`; revalidação de TUF/pacotes por `--recovery-verify`, pins e manifesto; saída anterior a Switching/observação/fences; guarda do fence correspondente antes de iniciar observador funcional; corte após fence OLD e antes da primeira observação |
| `prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py` | Guarda de APIs Python de lançamento nos caminhos `--preflight-only` e `--recovery-verify`; ensaio de rejeição de subprocess.Popen |
| `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt` | Quatro cenários adicionais; nenhum dos 22 anteriores removido; reabertura e replay em novos processos C#; inspeção de seletor, diário, fences, observações e pins |
| `.github/workflows/gf-autoatualizador-fase1-real-lab.yml` | Executa o ensaio de API; exige 26 contenções e os campos específicos dos quatro novos casos; preserva todas as asserções anteriores |

O modo de preparação é uma entrada de **laboratório**, via o mesmo `--real-owner ... prepare-only` lançado no Job pelo supervisor existente. Não é uma nova opção do GF distribuído nem um comando de retomada de instalação. Não se criou segundo seletor/coordenador.

O setup existente copia os pacotes, cria fixtures assinadas com chaves públicas de teste e vincula o manifesto ao contexto. RealOwner mantém gate, contexto e pins NTFS, grava BeginCore/OLD/1 e, no modo novo, revalida os dados e retorna. Os cortes “before/after verify” delimitam essa **reverificação**, depois do setup autorizado e das primeiras verificações C#; não representam ausência de toda validação anterior.

O preparo escreve objetos da tentativa: diário L18 e seletor OLD/1, além das cópias e metadados de teste do setup. Ele não é uma operação sem qualquer escrita. A fase herdada `old-verified` pertence ao modelo de fixture L18; não certifica que o GF OLD real tenha sido executado ou esteja operacionalmente saudável.

No caminho normal, OLD continua sendo executado antes de NEW. O fence OLD continua anterior à observação e o NEW continua anterior ao lançamento. A nova guarda recusa observar um slot sem seu fence com conteúdo esperado, antes de criar o Python observador. O ensaio `before-old-observation` interrompe o owner após gravar o fence OLD e antes de executar OLD.

**Nenhuma decisão de ReconcileReal foi relaxada.** Preparação concluída, saída zero, diário válido e OLD/1 não concedem autoridade após término/crash. Continua `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`.

## Evidência e classificação

**Run Windows [41098083325](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41098083325), job `124208946182`, tentativa 1, completed/success, SHA técnico `5aa02f7decf93abfb7d03fa64c2ad9f5f057b96a`. Compilação do coordenador: zero warnings/erros.** Foram lidas 26 linhas individuais de casos e o relatório final: **26/26 PASS_CONTAINMENT, zero falhas, zero recuperações**, 26 nomes únicos, todos os 22 anteriores preservados. Os quatro novos confirmaram UnknownBlocked no primeiro reconciliador e no replay, seletor OLD/1 preservado e nenhum fence NEW. O ensaio separado de APIs Python também aprovou. Uma única CI foi iniciada nesta missão; não houve retry nem segunda bateria. Gate de fonte e contraprova NTFS do workflow também aprovaram.

A árvore técnica manteve os 852 caminhos herdados: 848 blobs intactos, quatro modificados e nenhuma adição/remoção. A documentação de encerramento acrescenta somente este checkpoint e o recibo. Main foi observada em `410d65e23546976db456337c71e4705f10217d36`; PR #65 foi reconferida aberta/Draft, sem merge e no mesmo HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.

| Caso novo | Fronteira exercitada | Decisão exigida |
| --- | --- | --- |
| `prepare-only` | Verificação de dados concluída; owner sai normalmente; nenhum fence/observação GF nessa rota | UnknownBlocked; replay bloqueado; seletor OLD/1 intacto |
| `prepare-before-verify` | Owner interrompido antes da reverificação subordinada | UnknownBlocked; replay bloqueado; nenhum fence |
| `prepare-after-verify` | Owner interrompido depois da reverificação e rechecagem dos pins | UnknownBlocked; replay bloqueado; nenhum fence |
| `before-old-observation` | Fence OLD gravado e owner interrompido antes da observação funcional | UnknownBlocked; replay bloqueado; OLD/1 intacto, nenhum fence NEW |

Detalhe importante do replay: na fase Intent, o modelo herdado não permite transição direta para Unknown; RememberUnknown grava o bloqueador conservador separado. Assim, os três casos `prepare-*` recusam primeiro por `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` e no replay por `REAL_UNREADABLE_HISTORY_BLOCKED`. Isso é a política existente, não um novo estado de recuperação. No caso de fronteira OLD, Switching admite Unknown e o motivo original permanece no replay. As asserções verificam essa diferença explicitamente.

O teste Python tenta subprocess.Popen de um Python inerte e exige rejeição pelo audit hook antes da criação. A guarda cobre as APIs listadas no código; não é confinamento de código nativo/ctypes, de dependências arbitrárias ou de outros processos. Ausência de relatórios/fences sozinha também não autentica história: a evidência dos casos novos combina rota de código auditada, guarda de API, inspeção das fixtures e revalidação em Windows. **Não houve rastreamento independente universal da criação de processos.**

Separação de evidências:

- Revisão de fonte: confirma a ordem das chamadas, saída anterior à observação e escritas preexistentes em Program/diagnósticos antes da UI.
- Checagem local Linux: sintaxe Python/YAML, diff --check e teste sintético das funções exatas da guarda extraídas por AST; sem Windows/GF. Não havia SDK .NET local; compilação qualificada é a do runner Windows.
- Windows descartável: compilação/execução da suíte; casos anteriores incluem GF OLD/NEW real e lançamento NEW fora do coordenador. Os quatro novos não fazem observação funcional do GF; seus crashes são de processos do laboratório.
- Analogias de broker/provedor e hard links usam fixtures inertes. Não executam MSI/WUA/MSIX/manutenção real e não recebem autoridade por isso.

## Limitações preservadas

| Propriedade | Estado após a mudança |
| --- | --- |
| H1 — histórico durável anti-rollback | Não demonstrada; diário, seletor e fences continuam restauráveis pelo mesmo usuário |
| H2 — despacho obrigatório/domínio de efeitos isolado | Não demonstrada; NEW externo continua fora da guarda do owner |
| H3 — quiescência de efeitos externos | Não demonstrada; broker/handoff/worker tardio continuam contraprovas |
| Autoridade causal por caminho/File ID/hash/ID declarado | Não estabelecida; contraprova NTFS anterior preservada |
| READY/G1–G8/encerramento da Fase 1 | false / BLOCKED 0/8 / aberta |
| Reboot, power loss, durabilidade física de diretório ou perda do supervisor | Não qualificados por estes cortes de processos |

A preparação local segura não implica “a máquina nunca executou NEW”. Ela não é prova de H1/H2/H3, anti-rollback ou sandbox geral de I/O. O novo guard Python não protege credenciais nem converte dependência hostil em código confinado. A verificação continua usando o verificador e dependências confiados do laboratório, sem carregar código do pacote candidato como teste funcional.

Não se implementaram helper, serviço/tarefa privilegiada, ACL/política de execução, backend, chaves de produção ou telemetria. Main, PR #65 Draft, BETA .66, Cloudflare/D1, credenciais, Windows pessoal e fonte operacional do GF permanecem fora das alterações. Sem merge, release, deploy, promoção, artefato binário publicado ou integração operacional.

## Retomada útil

Não repetir os 26 casos nem reabrir MSI/MSIX/WUA como autoridades sem propriedade nova. Não ampliar a guarda de API e chamar isso de sandbox. Não transformar um recibo de preparo em permissão para retornar a OLD.

Uma mudança capaz de alterar a conclusão precisa especificar e refutar, em Windows descartável: (1) testemunho cuja autoridade sobreviva à restauração do estado local; (2) domínio concreto de efeitos que OLD/NEW e execuções externas não possam alcançar sem controle; (3) critério independente de conclusão dos efeitos. Se exigir nova autoridade privilegiada, política Windows ou backend, apresentar primeiro o desenho e obter autorização específica; nada disso foi implementado aqui.

Enquanto não houver esse mecanismo, a próxima ação do CHAT ALTO é avaliar esta melhoria local e escolher uma hipótese arquitetural falsificável, mantendo a recuperação bloqueada. Evitar outra bateria ampla sem mudança de mecanismo.

Recibo estruturado: [fase1-preparacao-dados-fronteira-efeitos-work-20261030.json](evidencias/fase1-preparacao-dados-fronteira-efeitos-work-20261030.json).
