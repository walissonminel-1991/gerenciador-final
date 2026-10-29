# GERENCIADOR FINAL — CHAT ALTO — Lote 17
## Triagem WORK aplicada: TOCTOU do seletor, recibo local forjado e timeout BUSY/UNKNOWN
**29/10/2026 · protótipo somente INERTE · 3/3 adversariais + 18/18 regressões CI Windows · G1–G8 BLOCKED (0/8)**

### 1. Entrada e restrições
- Repositório: walissonminel-1991/gerenciador-final.
- Branch experimental: experimento-g5-ipc-chat-alto-20261029.
- HEAD inicial confirmado idêntico ao checkpoint L16: e44b07b2d933f23d7c96f1a1adf9e44eb1cdca12 (ahead=0, behind=0).
- Fonte da priorização: parecer independente WORK dos Lotes 11–16 fornecido ao CHAT ALTO no chat (não integrado como revisão formal nem como autorização operacional). As três contraprovas solicitadas foram transpostas para interleavings determinísticos em Windows descartável.
- PR #65 Draft, branch work-integracao-final-20261027, HEAD preservado ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc; main preservada 410d65e23546976db456337c71e4705f10217d36.
- **Nenhum** GF principal compilado/executado, atualização, patch da PR #65, lançamento, deploy, merge, autorização de novos gates, segredo de produção, conta, Cloudflare, domínio, consentimento, telemetria, PC FORTE/FRACO ou WORK adicional.

### 2. Implementação técnica congelada
**Commit de engenharia:** 555f34cf7d8718355416bb49bda81be9af3ca59e.

Seis arquivos alterados/adicionados estritamente no experimento:
1. **MODIFICADO** prototipos/autoatualizador-seguro/m3-l16-signed-stage/L16Boundary.cs.txt. Recibo local deixa de gerar retorno NewInertObservedBlocked no recuperador, mesmo quando consistente: ausência de witness independente implica UnknownBlocked. Retenção de handle WindowsFileGuard do seletor durante a leitura e decisão observacional; conferência de identidade do temporário e do seletor após File.Replace, com marcador UNKNOWN quando a identidade muda; indicação BusyBlocked para lease ocupado; marcador persistente, cooperativo e idempotente para incerteza após timeout; ponto de corte controlado para permitir conclusão tardia do proprietário. Mantida autenticação dos bytes exatos da fixture TUF no staged.
2. **MODIFICADO** prototipos/autoatualizador-seguro/m3-l16-signed-stage/L16Tests.cs.txt. Ajustes mínimos de expectativas da regressão L16 ao resultado conservador: terminal local → UnknownBlocked; lease ativo → BusyBlocked. Exposição do setup de fixtures para reutilização, sem duplicar os motores existentes.
3. **NOVO** prototipos/autoatualizador-seguro/m3-l17-adversarial/L17Tests.cs.txt. Apenas TRÊS cenários adversariais novos, com processos filhos reais e reabertura em outros processos.
4. **NOVO** prototipos/autoatualizador-seguro/m3-l17-adversarial/L17Adversarial.csproj. Runner .NET 10 com fontes dos Stage11/Stage3, NTFS, L15, L16 e fixture L16 recompostas; sem pacote operacional.
5. **NOVO** prototipos/autoatualizador-seguro/m3-l17-adversarial/README.md. Modelo de autoridade e limites.
6. **NOVO** .github/workflows/autoatualizador-m3-l17-adversarial-inerte.yml. Windows-2022, SDK 10.0.402, TUF de laboratório fixado, relatório fail-closed e isolamento de compilação GF. O workflow anterior do L16 também é acionado pela edição dos arquivos existentes.

### 3. Três testes novos — propriedades de segurança e contraexemplos
**L17-01 — TOCTOU na leitura e no temporário do seletor**
- Com o recuperador real parado após observar o seletor, segundo processo tenta gravar/renomear o mesmo arquivo. O handle de leitura WindowsFileGuard impede a ação controlada até o resultado.
- Noutra fixture, o dono grava o seletor temporário, fecha o handle e pausa; segundo processo substitui o temporário por novo arquivo NTFS, com os MESMOS bytes. Após retomar File.Replace, a identidade do seletor resultante não coincide com o identity previamente pinado, causando erro e criação de marcador local de UNKNOWN. Recuperação posterior retorna UnknownBlocked.
- **Limite:** o intervalo entre fechar o temporário e File.Replace continua existindo! Esta correção detecta o fork nessa interleaving. Não equivale a vinculação atômica de objeto por handle, nem impede atacante local de mesmo usuário de remover marcador ou trocar estados inteiros.

**L17-02 — Terminal mínimo forjado depois de selector-replaced**
- A fixture de base, após kill sem terminal, retorna OLD e preserva o seletor anterior.
- Noutra fixture, após kill antes do terminal, o segundo processo cria SOMENTE l16-terminal.fixture.json com schema, txId e hash correto do seletor NEW. A recuperação deixa NEW intocado como dado de laboratório, mas retorna **UnknownBlocked**, não afirma conclusão nem READY.
- **Limite:** não foi criada autoridade de atestação da conclusão. O mesmo resultado UNKNOWN após terminal honesto é uma redução conservadora de disponibilidade. Sem testemunha independente, não é legítimo afirmar que a operação foi autenticamente concluída.

**L17-03 — Proprietário ainda ativo, espera expirada, conclusão tardia**
- Um filho real pausa APÓS File.Replace do seletor, ANTES de terminal, mantendo lease exclusivo. WaitAsync vence em ~80 ms sem matar o dono; recuperador retorna BusyBlocked.
- O supervisor grava um marcador local incerto com CreateNew/WriteThrough/Flush(true). Com dono ainda ativo o recuperador continua BusyBlocked; após sinal de continuação o dono grava seu terminal e termina normalmente.
- Reabrir por novo processo após término retorna **UnknownBlocked repetidamente**. O marcador não é apagado pelo protótipo; continuar o I/O tardiamente não produz autorização, confirmação, READY ou execução GF.
- **Limite:** Busy/UNKNOWN têm semântica cooperativa de fixture, não regra garantida em armazenamento hostil; marcador local não é anticorrupção nem witness independente; Flush(true) não demonstra power loss.

### 4. Duas CIs Windows verificadas na MESMA revisão técnica
- **CI adversarial L17:** https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40953129387
  - HEAD 555f34cf7d8718355416bb49bda81be9af3ca59e.
  - GitHub Actions completed / success. Job three-adversarial-counterexamples.
  - Logs: Build succeeded, 0 Warning(s), 0 Error(s); GF_L17_FINAL_REPORT passed=3 failed=0; 3 linhas PASS, zero FAIL.
  - Flags true: selector_read_pinned_against_same_user, scratch_identity_swap_detected_and_unknown_persisted, forged_terminal_not_independent_commit_authority, busy_while_owner_holds_lease, timeout_unknown_persisted_after_owner_exit, late_owner_completes_after_wait_timeout, original_version_and_markers_preserved.
  - Flags false: arbitrary_toctou_excluded, same_user_tamperproof_storage, independent_freshness_or_witness_present, operational_install_or_ready, power_loss_proven; operational_gates_closed=0.
- **CI de regressão L16:** https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40953129349
  - Mesmo HEAD 555f34cf7d8718355416bb49bda81be9af3ca59e.
  - GitHub Actions completed / success; Build succeeded; 0 Warning(s), 0 Error(s); GF_L16_FINAL_REPORT passed=18 failed=0; 18 PASS e zero FAIL.
  - Verificados bytes exatos TUF/staged, pin e identidade NTFS, kill/restart de processos, preservação de OLD, replays, fork e troca de source; expectativas de resultado terminal local foram alteradas para UNKNOWN, pois não constituem autoridade.
- **Total desta rodada: 21/21 (3 novos + 18 regressões); não somar como homologação nem como tests operacionais.**

### 5. Matriz demonstrado / premissa / pendente
- **D — demonstrado:** contraexemplos adversariais induzidos de maneira determinística em Windows descartável; pin de seletor contra mutações comuns concorrentes; fork de temporário com identidade alterada após substituir; recibo terminal fabricável sem produzir reconhecimento NEW; timeout, BUSY e UNKNOWN separados; efeito tardio do dono não anula marker UNKNOWN em testes cooperativos. Regressão da fronteira TUF de texto staged continuou aprovada.
- **H — premissas:** WindowsFileGuard, File.Replace e handles da CI têm o perfil NTFS do runner. Um mesmo usuário pode, em princípio, alterar estado local, recibos, relógio e marcadores após fechamento dos handles. Não houve processo GF real ou autoridade confiável de commit. As assinaturas Stage11/Stage3 têm chaves só de teste.
- **P — pendente:** fechar ou definir explicitamente como risco residual a janela scratch-close→File.Replace e o namespace do destino; impedir reescrita de todo estado por autoridade de mesmo usuário em produção; witness independente de terminal e frescor G1 após restauração integral; G2 interoperabilidade normativa/revisão/D6-02; G3/G4 release executável GF e proveniência; G5/G6 IPC, READY e ciclo GF; G7 custódia/bootstrap; G8 prazo global, cancelamento de I/O bloqueante, power loss, flush/rename durante falha física, antivírus, ACL e reparse hostis em Program Files. Nenhum gate homologado.

### 6. Próxima decisão
Não introduzir serviço gratuito hipotético, segredo por usuário ou certificado pago para forçar UNKNOWN a NEW. Aceitar formalmente que sem witness independente não se pode reconhecer terminal local como compromisso confiável, e priorizar definição de autoridade e UX de bloqueio/recuperação antes de qualquer integração. WORK já foi usado nesta frente para localizar os contraexemplos; não foi utilizado novamente nesta implementação.

**STATUS: LOTE 17 concluído como protótipo inerte, 21/21 nas duas CIs Windows, 0/8 gates. PR #65/main/BETA .66/infraestrutura preservados.**
