# GERENCIADOR FINAL — Fase 1 — H3 parcial: fronteira local de efeitos por token Windows de baixa integridade

**Data:** 30/10/2026. **Resultado:** `PARTIAL_LOCAL_EFFECT_BOUNDARY_PROVEN` em fixture inerte, Windows GitHub Actions. **H3 global permanece não demonstrada.** Nenhuma mudança ao GF distribuído ou ao C# L18.

## 1. Evidências, commits e preservação do escopo

- Repositório: `walissonminel-1991/gerenciador-final`, única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD inicial previamente confirmado: `058a274258afb9ceafade293fbfaa95866bb2e16`.
- Último commit técnico L18 validado (inalterado): `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6`; CI L18 [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790), job `124252340866`, `SUCCESS`, **27/27 PASS_CONTAINMENT**, zero recuperações. **Essa suíte NÃO foi repetida** nesta rodada.
- Novo ensaio separado:
  - Commit técnico inicial `4a8fa48f288e4ed5ef10126dedef4f9f52a60c61`.
  - [Run 41113846186](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41113846186), job `124262837161`: **FAILURE**. O filho LOW foi criado e o programa alcançou uma exceção `OSError` durante a tentativa de escrita, mas a guarda exigia `winerror==5` e o Python retornou o código de diagnóstico `CHILD_FAILED_low_84`. Esse resultado **não foi classificado como isolamento comprovado**.
  - Commit corretivo `4499fef1d8e44085406d7e3ffecadaade5f545b0`: aceitar exclusivamente `winerror==5` **ou** `PermissionError` com `errno==13 (EACCES)`. O Python pode mapear `ERROR_ACCESS_DENIED` na camada CRT para `EACCES` sem preservar `winerror`. Nenhum outro erro, falha de criação de processo ou escrita bem-sucedida foi convertido em prova.
  - [Run final 41114014768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41114014768), job `124263420669`, `attempt 1`, **completed/SUCCESS** no commit corretivo. O log bruto contém o relatório estruturado `GF_PHASE1_H3_LOW_TOKEN_REPORT` e **zero** `##[error]`.
- Novos caminhos exclusivos: `prototipos/autoatualizador-seguro/h3-token-scope/low_integrity_effect_probe.py` e `.github/workflows/gf-fase1-h3-low-token-scope.yml`.
- O workflow antigo `gf-autoatualizador-fase1-real-lab.yml` observa `phase1-composed/**`, portanto **não foi acionado** por este novo diretório. Nenhum teste OLD/NEW nem TUF precisa ser reexecutado para esta prova inerte.
- Sem execução do GF real, MSI, MSIX, WUA, drivers, tarefas, serviços, TPM, alteração de ACL, políticas persistentes ou privilégios elevados.

## 2. Mecanismo e contraprova controlada

**Mecanismo real do Windows:** `OpenProcessToken` no próprio processo de teste; `CreateRestrictedToken` para obter dois tokens derivados com os mesmos parâmetros `DISABLE_MAX_PRIVILEGE`; no segundo, `SetTokenInformation(TokenIntegrityLevel)` com SID LOW `S-1-16-4096`. `CreateProcessAsUserW` iniciou filhos Python sem herança de handles, sem janela e sem UAC. A inspeção do SID no próprio filho confirmou seu nível efetivo.

**Fixture:** diretório novo e marcado `gf-phase1-h3-il-*` dentro do `RUNNER_TEMP` do runner Windows descartável. Um arquivo inerte era legível e gravável pelo filho controle de integridade normal/higher, mas não pelo filho LOW. Todas as operações foram confinadas a essa fixture; a raiz foi excluída no encerramento do processo pai.

**Sequência observada no run aprovado:**
1. A fonte base do token do runner possuía integridade no mínimo MEDIUM.
2. Filho com token restrito e nível original foi realmente criado; leu o arquivo, abriu para escrita e alterou o primeiro byte. O pai verificou a alteração.
3. O pai restaurou a fixture aos bytes originais.
4. Filho com outro token restrito equivalente, mas com SID LOW 4096, foi realmente criado. Confirmou o nível de integridade e conseguiu ler os bytes originais. A tentativa `open('r+b')` foi negada pelo SO; o pai confirmou que o arquivo permaneceu idêntico.
5. Um segundo filho de controle com nível original voltou a gravar no mesmo arquivo. Isso demonstra expressamente que o processo LOW **não impede** outro processo de maior integridade, mesmo do usuário, de modificar o recurso.

**Saída estruturada observada:** `status=PARTIAL_LOCAL_EFFECT_BOUNDARY_PROVEN`; `restricted_token_created=true`, `low_integrity_sid_verified_in_child=true`, `same_restricted_token_without_il_reduction_wrote_target=true`, `low_integrity_child_read_target=true`, `low_integrity_write_denied_by_os=true`, `protected_target_unchanged_after_denied_write=true`, `unrestricted_or_higher_integrity_alternative_still_possible=true`.

**Campos negativos exigidos e aprovados pelo workflow:** `h1_proven=false`, `h2_proven=false`, `h3_global_proven=false`, `same_user_external_launch_blocked=false`, `provider_broker_effects_bounded=false`, `auto_recovery_allowed=false`, `operational_ready=false`, `windows_policy_changed=false`, `acl_changed=false`, `real_gf_executed=false`, `service_or_installer_executed=false`.

## 3. Qual hipótese a prova realmente distingue?

- **H3-local (confirmada):** para o **processo efetivamente iniciado com token LOW**, o Windows pode impedir escrita em um objeto do usuário de integridade superior enquanto mantém a leitura permitida. Trata-se de uma restrição **de acesso àquele objeto específico**, não de um inventário completo de efeitos.
- **H1 (não provada):** a fixture ou seus recibos do usuário continuam restauráveis. Não existe testemunha protegida contra replay de toda a história L18.
- **H2 (não provada):** a conta pode iniciar NEW ou qualquer outro processo **fora** do token LOW; o controle após o ensaio demonstra que a política aplicada a um filho não é política global do usuário/máquina.
- **H3-global (não provada):** Windows brokers/serviços, RPC, processos exteriores, chaves de Registro, rede, escrita em objetos LOW, efeitos delegados e execução tardia não foram contidos nem enumerados. A experiência de isolamento no arquivo inerte **não** representa quiescência total.
- **Processo GF real (não provado):** não sabemos se o GF consegue realizar sua observação de saúde útil sob LOW; não executar ou alterar o aplicativo sem desenho específico. Funcionalidades podem exigir acesso de escrita ao perfil, processos COM ou recursos não disponíveis nessa condição. A Microsoft recomenda executar processos restritos em desktop separado para evitar mensagens de janela; este ensaio **não** criou UI e não testou a segurança de UI interativa.
- **Operacional (bloqueado):** nenhuma mudança ao `ReconcileReal`, seletor, fences, C# L18, TUF/B+A/NTFS/TOCTOU ou produto; nenhum `OLD` recuperado, `READY` ou G1–G8 promovido.

## 4. Por que valeu um ensaio e qual o próximo gate?

A hipótese é fisicamente diferente das 27 contenções anteriores: **o kernel impõe a negativa de escrita a um filho com token reduzido**, enquanto um filho controle com direitos equivalentes fora da redução escreve. Diferentemente de hashes e recibos same-user, essa não é uma decisão do Python ou do reconciliador; a negativa acontece antes da gravação. O teste não precisa de instalador nem alterações persistentes do Windows.

**Próximo gate possível, sem autorização para integrar:** construir uma matriz **somente leitura** dos recursos realmente utilizados pela verificação funcional do GF, verificando se algum trecho do contrato de saúde exige escrita, COM ou brokers que invalidariam o uso de LOW. Não repetir o ensaio local. Só propor uma observação real com token reduzido se houver domínio de efeitos definido e um resultado funcional falsificável; proibir inferência global de isolamento. H2 continuaria faltando mesmo que o GF pudesse executar sob LOW.

Fontes técnicas:
- Microsoft: `CreateRestrictedToken` e tokens restritos: https://learn.microsoft.com/en-us/windows/win32/secauthz/restricted-tokens
- Microsoft: `CreateProcessAsUser` com token restrito: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw
- Microsoft: Mandatory Integrity Control e NO_WRITE_UP: https://learn.microsoft.com/en-us/windows/win32/secauthz/mandatory-integrity-control

**Estado:** Fase 1 **ABERTA**, `READY=false`, G1–G8 `BLOCKED 0/8`. `UnknownBlocked` e `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` preservados. Sem WORK, merge, release, deploy, integração operacional, serviço/helper/tarefa privilegiada, ACL/política persistente, TPM, backend, Cloudflare/D1, telemetria ou PCs pessoais.
