# Pré-BETA — Etapa 4: desinstalação, pendência e resíduos

27/10/2026; branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Base `9a01aee2d14685efa8b28df63fb406d14fd32625`; CI da Etapa 3 40559631389 conferida.
**Concluída tecnicamente; homologação R4/R6/R7 pendente. Etapa 5 não iniciada.**

## Resultado funcional

Process.Start com shell pode reutilizar um processo e não fornecer um novo
handle. O resultado nulo era tratado como StartFailed, permitindo continuar
o lote e liberando uma nova tentativa na interface. Agora é Pending:
interrompe o lote, bloqueia repetição e não autoriza inspeção/limpeza.
[Contrato oficial de Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0).

A observação posterior foi isolada em PreparedUninstaller.ObserveStartedAsync.
Win32Exception durante espera/leitura do retorno, após processo criado, vira
UninstallObservationException com código nativo. UninstallBatch preserva
Pending, mesmo para 1223, em vez de confundir a falha com recusa inicial de
UAC ou falha de início. Falhas efetivamente lançadas por Process.Start
conservam StartFailed/Cancelled e diagnóstico de antivírus/permissão.
Nenhum processo é encerrado, comando repetido ou UAC solicitado por isso.

UninstallResultSummary.Status é o helper usado pela tela. Pendência tem
prioridade sobre cancelamento da limpeza. Falhas e lotes mistos não anunciam
conclusão; inspeção ausente/inconclusiva e alvo não removido ficam parciais.
NÃO na limpeza informa preservação por escolha, sem falso erro. Resultado
vazio não fabrica sucesso. O resumo detalhado e a escolha SIM/NÃO continuam.

## Controles existentes reaproveitados

| Área | Código / política | Limite preservado |
| --- | --- | --- |
| Comando e modo silencioso | UninstallBatch.Select, SilentUninstallPolicy, RegisteredUninstaller | Comando registrado, identidade/MSI, PE, hosts proibidos, assinatura/advertência e pinning; sem catálogo executável |
| Exclusividade | Revalidação do registro, raiz e desinstalador compartilhados | Não remover uma edição ou dependente compartilhado por semelhança |
| Propriedade residual | Prepare/Capture, contextos e identidade da raiz anterior | Ausência confirmada e inventário completo; sem raiz nova presumida |
| Consentimento | IntegratedResidualBatch | SIM/NÃO explícito; pendente/falhou/cancelou não oferece limpeza |
| Exclusão | IntegratedResidualCleanup, InstallerFileGuard, ResidualRegistryGuard | Hash/identidade/ancestrais, transação de Registro, links, dados pessoais e revalidação final preservados |
| Privilégio/reinício | Protocolo elevado e RebootResidualReview existentes | Sem elevação automática, mudança de ACL ou reinício; falta de acesso não é autorização |

Não foram alteradas políticas de propriedade nem ampliadas extensões/pastas
removíveis. Os dois testes antigos que esperavam StartFailed para retorno
nulo foram corrigidos para Pending, mantendo proibição de limpeza/repetição;
nenhum caso foi removido para aprovar a CI.

## Validação

Projeções de acompanhamento/resumo extraídas sem mudar lógica antes da
comparação: **11/29 aprovadas e 18 falhas → 29/29 aprovadas**, mesmos casos.
Cobrem lote com dois aplicativos, bloqueio após recarga, ausência de limpeza,
erros pós-início, falhas reais pré-início/UAC, recusa, cancelamento e estados
mistos/parciais. São callbacks inertes, não desinstaladores reais.

Local Linux, SDK 10.0.402 verificado por SHA-512 oficial: 29 focais, 41
controles WORK6 do fluxo e build Release/analisadores sem avisos/erros.
A tentativa da suíte completa local parou na asserção preexistente de RAM,
antes dos testes novos; não foi contornada nem modificada. A CI Windows 40565220517
aprovou a suíte completa e os cinco cenários nativos abaixo.

Os nativos criam somente um diretório TEMP com GUID e duas subpastas de
fixtures. Sem registros de programas reais, UAC, aplicativos ou reinício:

1. NÃO: bytes do resíduo e Documents preservados.
2. Mudança após consentimento: hash/identidade revalidados, arquivo preservado.
3. Cancelamento antes de excluir: nada removido.
4. SIM: somente cache.tmp atribuído é removido; Documents e instalação vizinha intactos.
5. Proprietário concorrente na mesma raiz: nenhuma oferta ou exclusão.

A política SyntheticScope é exclusiva de GF_PROGRESSIVE no executável de
testes, restaurada ao final e ausente do produto. O mesmo pipeline de
captura/consentimento/exclusão de produção é usado pelas fixtures.

```sh
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release -- --etapa4
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release -- --work6-uninstall-flow
# Suíte completa e provas nativas: Windows descartável
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release
```

[Evidência](evidencias/pre-beta-etapa4-desinstalacao.json).

## Pendências e continuidade

**R4/R6/R7 permanecem abertos, roadmap 27/42.** E1/E2 têm 130/54 entradas
com desinstalador e 61/40 pré-classificadas silenciosas, não remoções validadas.
Falta prova real antes/depois de desinstalação, preservação de dados e
propriedade que resolva os casos históricos privados. Nenhuma nova coleta
ou operação foi feita nos PCs. Fixtures não equivalem a homologação física.

VLC permanece excluído de testes, verificações, consultas e botões/ações;
nenhum caso novo. Etapas 0–3 preservadas; Etapa 5 não iniciada. Main, PR #40,
BETA/Worker/D1/DNS/flags, dados pessoais e regras de segurança preservados.
Sem merge, deploy, release, instalação/remoção/reversão de driver ou reinício.

## Recibo final da CI

Código `386e7cd5263266a0478e60139e59b036f1d69e31`, [CI 40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517),
tentativa 1, **success**: seis jobs obrigatórios aprovados e dois condicionais
skipped. [Evidência por job](evidencias/pre-beta-etapa4-ci-40565220517.json).

- **739/739 resíduos/desinstalação**, com 34 verificações novas: 29 focais
  e cinco nativas. A asserção RAM existente passou no Windows, sem mudar seu código.
- Nativos em TEMP: NÃO preserva bytes; alteração pós-consentimento é bloqueada;
  cancelamento preserva; SIM remove só cache atribuído com Documents/vizinho
  intactos; dono concorrente impede oferta e exclusão.
- **410 atualizadores, 196 multifontes, 36 mutações rejeitadas, 47 MSIX,
  30 encerramento e 41 Python/políticas**. Interface, drivers 2022/2025 e
  integridade dos demais instaladores aprovados.
- Pacote x64 self-contained validado com 284 hashes pela CI, sem revalidação
  independente do ZIP ou homologação AVG nesta sessão.

Nenhum código mudou após essa CI. O recibo posterior altera somente
Markdown/JSON e usa `[skip ci]`; conferir seu SHA na PR #64 ao retomar.
**R4/R6/R7 abertos; 27/42.** Próxima sessão: somente Etapa 5, limitada a
estudo/protótipo desativado. Nenhuma ação dessa etapa foi executada.
