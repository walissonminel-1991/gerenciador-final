# GF — Fase 1 — ID do provedor após crash: recuperação de evidência não é autoridade

**Data:** 30/10/2026 · **Natureza:** verificação arquitetural de APIs Windows e pontos do código real, sem ensaio de instalação. **Resultado:** fonte de diagnóstico encontrada, nenhum certificado de no-launch/quiescência.

## 1. Âncoras verificadas antes da escrita

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial consultado: `e9e005114174b6d4020227c7bf138cc068abc15c`.
- Último commit técnico aprovado: `749ef3206eff67ea6118ef405aa6f34cb30ef0e8`.
- Última CI Windows: [41089673054](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41089673054), job `124179955658` `completed/success`; **22/22 PASS_CONTAINMENT, zero recuperações**, ensaio NTFS adicional, separado, aprovado.
- PR #65 conferida: `open/Draft`, `merged=false`.
- Código consultado: `VerifiedUpdaters.cs`, `MsiPackageEvidence.cs`, `WindowsTerminalUpdater.cs`, `PendingDeployment.cs`, `WindowsUpdateSearchPolicy.cs`, `Program.cs` e protótipo `Phase1Recovery.cs.txt`. Não se afirma ter auditado a implementação completa de `WindowsUpdateClient`: a classe não foi encontrada como arquivo isolado nessa branch.
- Restrição: **nenhuma instalação, desinstalação, serviço, modificação de política, Windows Update, MSI real, deployment real ou execução elevada**. Não tocar no reconciliador, GF distribuído, TUF, NTFS, B+A ou infraestrutura.

## 2. Pergunta respondida, com quatro propriedades distintas

É possível obter após um crash algum **registro do provedor Windows** que informe a identidade/resultado da operação? **Às vezes, sim**, dependendo da API e disponibilidade do histórico. Isso NÃO significa que o registro esteja vinculado com segurança a uma única tentativa `L18(tx,manifest,installation)`, seja durável/anti-rollback, comprove despacho exclusivo ou certifique ausência de efeitos futuros.

| Mecanismo | Consulta/identificador depois que o solicitante morre | Garantia que realmente oferece | Falha para autorização L18 |
| --- | --- | --- | --- |
| **WUA / Windows Update** | `IUpdateSearcher.QueryHistory` ou `IUpdateSession3.QueryHistory` (inclusive critério `UpdateID='...'`), `IUpdateHistoryEntry` com `UpdateIdentity`, `Date`, `ClientApplicationID`, `Operation`, `ResultCode`, `HResult` | Evidência retrospectiva de eventos de atualização **por máquina** preservados no histórico disponível | `UpdateID` é identidade de atualização, não de invocação. `ClientApplicationID` de `IUpdateSession` e `IUpdateInstaller` é **read/write pelo próprio cliente**, não autenticação independente da identidade/instância GF; eventos repetidos podem compartilhar todos os identificadores declarados. Histórico consultável não é prova de unicidade, retenção ou quiescência |
| **MSIX / PackageManager** | `DeploymentResult.ActivityId` correlaciona eventos da **operação de deployment**; logs podem ser examinados com `Get-AppxLog -ActivityId` ou listados com `-All` | Diagnóstico específico da implantação quando o ActivityId está disponível; `IsRegistered` diz se pacote ficou plenamente registrado | `ActivityId` é retornado como parte de `DeploymentResult` da operação concluída. Se o processo perde a resposta ou não registra o vínculo confiável com L18, procurar eventos depois não prova **qual** evento deriva de **qual** transação GF. `-All` amplia a busca, não cria o vínculo ausente. Não garante efeitos fora do deployment nem ausência de novas execuções |
| **MSI / Windows Installer** | `MsiInstaller` registra sucessos/falhas/reparos no Event Log. `MsiBeginTransaction` oferece `MSIHANDLE` de transação **multipacote** e evento de mudança de owner | Histórico de produto e, no protocolo multipacote específico, propriedade/rollback da operação sob regras do Windows Installer | Event ID/produto não identifica uma chamada GF única. `MSIHANDLE` não é ID persistente genérico de `msiexec.exe`; a documentação diz que deixar transação sem owner causa rollback **da transação MSI**, não de todos os efeitos de NEW, nem de qualquer execução externa |

Fontes oficiais e escopo:
1. WUA `QueryHistory`: https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-queryhistory e https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesession3-queryhistory
2. WUA `IUpdateHistoryEntry`: https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iupdatehistoryentry-properties
3. WUA `ClientApplicationID` mutável: https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesession-get_clientapplicationid e https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdateinstaller-get_clientapplicationid
4. MSIX `DeploymentResult`: https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.deploymentresult
5. MSIX logs: https://learn.microsoft.com/en-us/powershell/module/appx/get-appxlog?view=windowsserver2025-ps
6. MSI eventos: https://learn.microsoft.com/en-us/windows/win32/msi/event-logging
7. MSI transação e owner: https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msibegintransactiona e https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msijointransaction

## 3. Contraprovas arquiteturais novas (NÃO são casos executados)

**C1 — WUA após crash, cliente declaratório duplicado.**
- Duas tentativas possíveis A/B processam a **mesma atualização**, identificada pelo mesmo `UpdateID`, e podem definir o mesmo `ClientApplicationID` da sessão (`GF` ou outro valor).
- O processo L18 morre antes de registrar que tentativa iniciou qual operação. Um novo processo recebe `QueryHistory` com uma ou mais entradas por atualização e o identificador declaratório.
- A API fornece histórico, **não testemunho não falsificável** de qual tentativa L18 autorizou aquele ato. Resultado `NotProven` para vínculo exclusivo, ainda que a consulta retorne `ResultCode=Succeeded`.
- Restrição factual: não alegar que toda implementação irá sempre gerar registros duplicados; esta é uma **possibilidade de ambiguidade** permitida pelo modelo de cliente e identidade, não um ensaio de WUA.

**C2 — MSIX: operação concluída, recibo perdido.**
- Uma implantação termina ou o solicitante morre antes de obter/persistir `DeploymentResult.ActivityId`. Um reinício da aplicação pode examinar `Get-AppxLog -All`.
- Mesmo se encontrar uma atividade correspondente ao pacote e período, **sem vínculo protegido criado previamente e com exclusividade demonstrada**, a consulta só identifica evento do provedor; não é autoridade da transação L18. Se logs desapareceram, ausência não é prova negativa.
- Não confundir com afirmação absoluta de que `ActivityId` nunca seja recuperável: os logs `-All` existem, mas não estabelecem por si o encadeamento confiável com L18.

**C3 — MSI: owner perdido, rollback limitado ao fornecedor.**
- A morte do owner de uma transação criada por `MsiBeginTransaction` implica, pela documentação, rollback da operação MSI multipacote sem owner.
- Isso **não** afirma que um EXE independente ou custom action assíncrona fora desse contrato não produziu efeitos, nem que OLD pode ser relançado. O GF atual usa processo `msiexec.exe` e não implementa o protocolo multipacote como autoatualizador.

**C4 — Event Log registrado não é testemunha anti-rollback universal.**
- Eventos podem ser limitados pela política de retenção e sobrescritos quando o log atinge o tamanho configurado; ausência pós-crash não demonstra ausência da operação. A documentação também prevê logs cheios, e não há vínculo de execução não contornável só por um Event ID.
- Fonte: https://learn.microsoft.com/en-us/windows/win32/eventlog/event-log-records e https://learn.microsoft.com/en-us/windows/win32/msi/event-logging

**C5 — Autoridade do provedor não se aplica automaticamente ao próprio GF.**
- As APIs MSI, MSIX e WUA foram inspecionadas como fontes de evidência para **operações que o GF, como utilitário, pode solicitar**.
- O autoatualizador **do próprio Gerenciador Final** usa manifesto TUF, pacote OLD/NEW real e coordenador L18. Nenhum ID de fornecedor de atualização de aplicativos terceiros certifica seu **`MAY_LAUNCH` de NEW**, sua execução exclusiva, nem sua quiescência global.

## 4. O que poderia ser empiricamente comprovado sem alterar o SO?

- **Consulta read-only real de histórico** WUA/Event Log no Windows descartável poderia demonstrar que a API retorna registros naquele ambiente (se houver); sem executar uma operação com proveniência independente, **não poderia** comprovar o vínculo entre `txL18` e um evento de instalação nem recuperação após uma falha real do solicitante. Ativação COM/serviço e logs vazios/variáveis ainda criariam dependências ambientais.
- **Testes com identificadores simulados** só validariam regras que nós mesmos escrevemos; não comprovariam serviço Windows, durabilidade ou autoridade. Não acrescentar estes testes à matriz como se fossem cobertura funcional.
- **Teste real de instalação/serviço** seria mudança substancial de escopo e **não está autorizado**. Nem criar MSI inerte com serviço, nem instalar pacote MSIX, nem operar WUA.
- Logo, **nenhuma nova CI nesta rodada**, por ausência de um teste autorizado com ganho suficiente para a segurança transacional.

## 5. Decisão de engenharia e próximo alvo de ganho real

**Veredito:** identificadores e logs pós-crash de MSI, MSIX ou WUA são úteis como **diagnósticos de fornecedor**, não como autoridade independente de no-launch + vínculo causal + quiescência do autoatualizador GF. **A hipótese de que basta consultar ID/resultado do provedor para autorizar OLD está refutada como regra geral.**

**Não implementar** helper, persistência adicional same-user ou recibo "de sucesso" do fornecedor em `ReconcileReal`. Preservar `UnknownBlocked` e `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`.

**Próximo direcionamento mais produtivo:** encerrar por ora esta trilha de investigação MSI/MSIX/WUA como **`NOT_A_RECOVERY_AUTHORITY`**. Voltar ao caminho próprio da transação GF: estabelecer, com um escopo explicitamente limitado, (i) qual processo pode efetivamente iniciar NEW, (ii) onde existe qualquer testemunho durável protegido do ato e (iii) como bloquear recuperação quando o processo morreu ou quando houve efeitos externos. A contraprova de rollback same-user e de lançamento externo de NEW continua válida. Se não for possível conciliar portabilidade/sem admin com H1+H2+H3, manter o mecanismo sem recuperação automática em vez de criar segurança aparente.

**Condição da Fase 1:** ABERTA, `READY=false`, G1–G8 `BLOCKED 0/8`; **22/22 PASS_CONTAINMENT** são a última CI comprovada, não foram executados novos casos. PR #65 Draft intacta. Sem WORK, merge, release, deploy, alterações de produto/BETA, infraestrutura, credenciais, telemetria, contas, serviços ou PCs pessoais.
