# GERENCIADOR FINAL — Fase 1 — Avaliação de helper temporário sob identidade distinta

**2026-10-30 · PARECER DE ARQUITETURA, não protótipo funcional · sem novo ensaio Windows**

## 1. Âncora obrigatória

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de partida conferido: `f3a2586739dc056a406f0e5bf8c01fd3b552110b`.
- Último commit técnico validado: `f250be5a32a9706551051da1b0b082f57b383d15`.
- CI Windows [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779), job `124123487779`, `completed/success`: **20/20 PASS_CONTAINMENT; zero recuperações automáticas**. Caso `same-user-full-state-rollback-after-new` retorna `UnknownBlocked`, inclusive no replay.
- Contraprova [41071494214](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41071494214), `failure`: o código anterior aceitou `OldRecoveredNoReady` com NEW executada e seletor/diário restaurados. A falha final era um assert da fixture e **não** uma barreira de segurança.
- PR #65 consultada: **open/Draft, unmerged**. Não alterar nem promover.
- Fontes de código examinadas: `Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt`, `LabProcessJob.cs.txt`, `Phase1Bridge.cs.txt` e checkpoint `CHAT-ALTO-FASE1-AUTORIDADE-LANCAMENTO-DECISAO-20261030.md`.
- A barreira atual `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` permanece **obrigatória**. READY=false; Fase 1 ABERTA; G1–G8 BLOCKED 0/8.

## 2. Pergunta exata: o que o helper provaria?

Um helper transitório só agregará segurança se satisfizer **duas propriedades independentes**:

**H1 — autoridade de estado anti-rollback:** um processo não autorizado, inclusive após a morte do helper, NÃO consegue substituir a sequência de decisões por uma versão válida antiga. Exigir ledger protegido por uma identidade principal distinta e acesso com semântica de recuperação e persistência especificadas; proteção de ACL enquanto um handle está aberto não é, por si, prova de power-loss.

**H2 — autoridade de lançamento não contornável:** nenhuma execução de NEW que possa provocar os efeitos relevantes acontece fora da autorização registrada. Isso não é garantido por criar um helper privilegiado e pedir que o GF coopere; é necessário enforcement real de permissões/política de execução e delimitação explícita do que é `NEW` (EXE, cópias, DLLs, host process, caminhos alternativos, brokers).

**H3 — efeitos pós-lançamento**, separado: mesmo com H1 e H2, um ledger de `MAY_LAUNCH` não comprova que os efeitos de NEW cessaram. Após autorização para criação de processo, retorno a OLD requer quiescência independente de processos/descendentes, brokers externos, serviços e I/O tardio; até existir, retornar `UnknownBlocked`.

**Resultado:** o helper elevado poderia contribuir parcialmente para H1 sob um adversário que permanece em um token menos privilegiado. Ele não estabelece H2 ou H3 sozinho.

## 3. Distinções de identidade, UAC e instalação portátil

| Caso | Ledger em área protegida | Execução exclusiva de NEW | Viabilidade/limite |
| --- | --- | --- | --- |
| Usuário padrão sem credenciais administrativas | Não pode configurar helper elevado ou área protegida | Não | Precisaria de outro principal/credenciais; degradação segura, sem autoatualização autorizada |
| Usuário padrão informa credenciais de outra conta administradora | Separação de SIDs pode proteger do usuário padrão se owner/DACL/parent paths forem corretamente definidos | Não; copiar/abrir EXE ou usar outro caminho ainda pode executar | Hipótese restrita, com UAC e artefatos privilegiados; não homologada |
| Usuário administrador com UAC e token filtrado | MIC distingue integridade média de alta enquanto essa barreira é mantida | Não; usuário pode consentir em novas elevações e executar fora do helper | Mesmo SID administrativo não é fronteira anti-administrador hostil; UAC não deve ser confundido com isolamento de identidade |
| UAC desativado, conta incorporada elevada, privilégios equivalentes | Não há separação média/alta presumível | Não | Bloqueio, jamais supor segurança pelo nome do helper |
| Pacote portátil em NTFS, programa e ledger no mesmo diretório gravável | ACL local pode auxiliar integridade acidental | Não | Owner do diretório/arquivos pode ajustar DACL; preferir nunca presumir `WRITE_DAC` seguro |
| Pacote portátil em exFAT/FAT32/pendrive | Não existe suporte às ACLs NTFS para ownership dos arquivos do pacote | Não | Proteção teria de ser externa ao volume portátil, violando a expectativa de zero estado persistente |

A UI de UAC pode solicitar **consentimento** ao administrador ou **credenciais** ao usuário padrão, conforme política; operação normal sem elevação não deve depender de consentimento. Executar o GF inteiro elevado para tornar a NEW inacessível ao usuário criaria superfície de risco maior: o helper, se existisse, teria de ser mínimo e realizar somente ação privilegiada estritamente validada.

**Portátil não implica estado privilegiado persistente permitido:** registrar e limpar tarefa/serviço/ledger em `ProgramData` ou no sistema deixa rastros mesmo se o EXE portátil for removido; plano de migração/remoção e tratamento de falhas são requisitos próprios, ainda ausentes.

## 4. Desenhos concretos avaliados

### A. Segundo executável no perfil do mesmo usuário, sem UAC
**REPROVADO.** Mesmo dono do arquivo, token e diretórios. Usar nome de processo, mutex, hash, chave embutida, IPC autenticado, outro PID ou dois diários não impede rollback; perda do auxiliar remove qualquer testemunha volátil.

### B. Helper elevado somente durante a atualização (UAC `runas`), sem serviço persistente
**H1 PARCIAL sob um usuário padrão com credenciais de outra identidade; H2/H3 REPROVADOS.** Uma pasta/ledger sob owner administrador e DACL adequada pode não ser gravável pelo processo padrão, mas o usuário comum pode executar NEW por outro caminho se o EXE estiver disponível. Para administrador com UAC, não alegar resistência contra ele mesmo ao aprovar nova elevação. Outra falha: se o helper é carregado de diretório mutável same-user, o caminho, dependências e conteúdo têm de ser autenticados e protegidos contra swap/TOCTOU e injeção; um helper elevado que consome payload local é risco de escalada. Não criar ou executar.

### C. Tarefa agendada on-demand sob conta protegida
**REPROVADO como solução única.** Embora o serviço Windows existente execute tarefas em outros contextos, o usuário criador normalmente pode modificar a sua tarefa. Tarefa com owner/DACL de outro principal exige implantação/credenciais/controle apropriados. Persistência de configuração, órfãos e possibilidade de executar NEW diretamente permanecem. A criação de uma tarefa não é prova de exclusividade de lançamento.

### D. Serviço temporário SCM sob conta privilegiada, instalado/desinstalado na atualização
**REPROVADO para escopo atual.** Instalação e controle exigem privilégios; processo que morreu antes de remover serviço pode deixar registro/ledger. Reinício, UAC, DACL e IPC criam manutenção. Serviço temporário não impede execução paralela de NEW. Serviço rodando só durante a atualização não equivale à autoridade persistente após reinício.

### E. Helper protegido + política de execução do SO (App Control for Business / AppLocker)
**A ÚNICA FAMÍLIA CONCEITUAL QUE PODERIA SATISFAZER H2 sob precondições gerenciadas**, mas NÃO foi homologada: deve haver política aplicada pelo SO cobrindo toda cópia/variante de NEW e caminhos alternativos, não simplesmente um hash ou ACL na pasta. Alterar política de dispositivos pessoais para um updater gratuito é invasivo, pode causar bloqueios de outras aplicações e não serve como dependência silenciosa de um EXE portátil. App Control é suportado em diferentes edições com diferenças de capacidade/gerenciamento; não afirmar que está indisponível no Home, nem supor que o GF pode instalá-lo sem configuração explícita de segurança.

### F. Helper elevado + conteúdo criptografado/entregue just-in-time
**REPROVADO como prova de exclusividade.** A recuperação dos bytes em memória/disco cria a possibilidade de extração, cópia, execução direta ou caminhos equivalentes; proteção do segredo não garante que a execução passa pelo ledger. Também não delimita efeitos depois do processo terminar.

## 5. Gap de crash, reinício e durabilidade

O único protocolo conservador para um **eventual** broker protegido começaria com `Prepare` e verificação de B+A/TUF/NTFS, depois gravação monotônica protegida `MAY_LAUNCH(tx, manifest, generation)` **antes** de permitir `CreateProcess`, e somente depois lançamento por handle/identidade verificados. O exemplo é uma **máquina de estados conceitual**, não uma proposta de API funcional aprovada.

| Corte | Interpretação segura |
| --- | --- |
| Antes de `MAY_LAUNCH`, ledger íntegro e H2 comprovado | Pode inferir no-launch *somente no escopo exclusivo e autenticado do broker*; não em toda a máquina sem enforcement |
| Depois de registrar `MAY_LAUNCH`, antes de chamar `CreateProcess` | **UNKNOWN/BLOCKED**, mesmo se o launch não ocorreu |
| Durante/após `CreateProcess`, helper ou supervisor morre | **UNKNOWN/BLOCKED**; saída/Job vazio não apaga possíveis efeitos |
| Ledger travado, apagado, reinstalado, rollback, volume não durável | **UNKNOWN/BLOCKED**, não reconstruir a partir de recibo do usuário |
| Reinício ou power-loss antes de drenagem de I/O | **UNKNOWN/BLOCKED** até prova própria de persistência/estado e efeitos |
| READY reportado só por NEW ou receipt same-user | Não é autoridade independente; READY=false |

A ausência de registro só pode ser prova negativa se o registro for **duravelmente íntegro, anti-rollback e obrigatório** para todo lançamento relevante. A durabilidade de bytes de arquivo e `Flush(true)` não estabelece, por si, durabilidade física de diretório, transação de `CreateProcess`, ausência de broker externo ou proteção contra administrador.

## 6. Superfície privilegiada e critérios de revisão antes de qualquer protótipo

Qualquer eventual helper exigiria, ANTES de ativação funcional:

1. **Modelo de ameaça fixo**: usuário padrão, administrador local, UAC habilitado/desabilitado, volume NTFS vs exFAT, processos hostis do mesmo SID vs outro SID. Não alegar resistência contra administrador hostil se for necessário dar-lhe poder de alterar política.
2. **Distribuição e identidade dos binários**: trust anchor real, código e dependências protegidos antes da elevação, TUF aprovado em produção, verificação de handles reais antes/durante launch, e revogação de versão. Chaves do laboratório não são autoridade operacional.
3. **Canal de comando**: Named Pipe com descriptor explícito, restrição a cliente local, sem acesso de rede/anônimo, verificação de SID/token e autorização por operação, protocolo de tamanho limitado e anti-replay; não confiar em argumentos de linha de comando, caminho ou hash informado por cliente. Evitar decisões elevadas em paths mutáveis e symlinks/reparse points.
4. **Sem confused deputy**: comandos permitidos *somente* `prepare/arm/launch/query` da transação autenticada; nenhuma execução arbitrária, path arbitrário, exclusão, shell ou elevação generalista; direito de ler ledger não equivale a direito de alterar.
5. **Despacho obrigatório**: teste de `CreateProcess` direto de NEW, cópia para outra pasta, carregamento por host, execução por tarefa e lançamento por outra sessão/usuário. Se alguma execução que produz efeitos puder ocorrer sem ledger, H2 falha.
6. **Persistência, limpeza e privilégios**: ledger/owner/ACL/SACL de acordo com SO, proteção de diretórios-pai, migração de transação, nenhuma perda do bloqueio ao apagar o portátil, comportamento após crash/power-loss, UAC cancelado, ausência de credenciais e rollback da atualização.
7. **Teste que pode refutar a tese**: NEW executa, todo estado gravável same-user é restaurado ao snapshot pré-NEW, helper e supervisor morrem, e um novo processo consulta o estado. Resultado aceitável é `UnknownBlocked` salvo prova independente anti-rollback e de exclusividade; jamais retornar `OldRecoveredNoReady` apenas pelo ledger.

## 7. Compatibilidade com os compromissos do GF

- **Gratuidade:** as APIs Windows são nativas, mas custo de desenvolvimento, manutenção, troubleshooting e suporte não é zero.
- **Portabilidade sem instalação persistente:** helper/tarefa/ledger privilegiado não se encaixa automaticamente no requisito; um binário portátil com estado protegido fora da sua pasta torna-se dependente de implantação gerenciada.
- **Público com baixo conhecimento:** credenciais UAC, mudanças de políticas da máquina, serviços órfãos e recuperação manual aumentam risco operacional.
- **Sem serviço GF permanente:** um helper efêmero é teoricamente compatível com essa restrição estrita, mas não com H1/H2/H3 conjuntamente nas condições atuais.
- **Sem novos riscos ao computador:** não ativar configurações App Control ou ACLs sistêmicas por experimento; nenhum efeito em PC pessoal.

## 8. Decisão definitiva desta rodada e opção de pesquisa

**Rejeitar implementação nesta fase**: helper temporário sob outra identidade NÃO é uma autoridade suficiente contra rollback same-user e bypass sem uma política de lançamento não contornável. Não existe prova de que H1+H2+H3 sejam satisfeitos sob o requisito portátil geral. Mesmo quando H1 pode ser alcançada numa conta padrão, H2 e H3 permanecem abertos.

**Não abrir funcionalidade, não conceder READY e não mudar o coordenador.** Preservar `f250be5a32a9706551051da1b0b082f57b383d15`, 20/20 contenções, `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` e `UnknownBlocked`.

**Se o mantenedor desejar flexibilizar requisitos mais tarde**, a única próxima pesquisa razoável é uma prova de conceito isolada de fronteira entre conta padrão e conta administradora distinta, SOMENTE em runner Windows descartável e inicialmente apenas para verificar H1 e demonstrar *bypass* de H2, sem executar alteração de política de máquina nem operar a BETA. **Não pré-autorizar** criação de serviço/tarefa, UAC, política de execução ou mudanças nos PCs.

**Mudanças nesta rodada:** somente este documento de arquitetura. Sem nova CI Windows, sem teste funcional, sem código técnico modificado, sem WORK, merge, release, deploy, alterações na `main`, PR #65 Draft, BETA .66, credenciais, telemetria, infraestrutura ou PCs. **Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8.**

## Referências técnicas oficiais consultadas

- Microsoft Learn — [How User Account Control works](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/how-it-works).
- Microsoft Learn — [Mandatory Integrity Control](https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control).
- Microsoft Learn — [Owner of a New Object](https://learn.microsoft.com/en-us/windows/win32/secauthz/owner-of-a-new-object).
- Microsoft Learn — [Security Contexts for Running Tasks](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks).
- Microsoft Learn — [Named Pipe Security and Access Rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights).
- Microsoft Learn — [App Control for Business feature availability](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/feature-availability).
- Microsoft Learn — [App Control Managed Installer security considerations](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/configure-appcontrol-managed-installer).
- Microsoft Learn — [File System Functionality Comparison](https://learn.microsoft.com/en-us/windows/win32/fileio/filesystem-functionality-comparison).
