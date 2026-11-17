# GERENCIADOR FINAL — DRIVERS F4.2 — Windows 11 ARM64 read-only

Data: 2026-11-16. PR #80 Draft, branch `experimento-atualizador-drivers-wua-20261114`.

## Estado confirmado F4.1
- SHA tecnico: `9137b8e0673bdb46c0bea5a9f86b723f81fe543f`; checkpoint documental `750836863b579014b2344bf80de38a17f1280843`.
- Windows 2022 `43673851211` e integridade `43673851191`: SUCCESS no HEAD documental.
- Consulta WUA `43672915368`: Windows Server 2022 (59 PnP, 0 ofertas) e Server 2025 (66 PnP, 0 ofertas), ambos `NO_OFFERS`.
- Nenhum download ou instalacao; F4.1 nao homologa alteracao real de driver.

## Hipotese funcional F4.2

Runners oficiais padrao do GitHub incluem `windows-11-arm`, Windows 11 ARM64, gratuitos em repositorios publicos:
- https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job
- https://github.com/actions/runner-images/blob/main/images/windows/Windows11-VS2026-Arm64-Readme.md

Um novo job unicamente consultivo, iniciado por evento `pull_request` **da PR interna #80**, executa `--f42-win11-arm-probe` e reutiliza o motor WUA F4.1. Propriedades exigidas: repositorio, PR, base, head, runner GitHub-hosted Windows ARM64, Windows build >= 22000, .NET ARM64, SDK 10.0.402, sem tokens de escrita. Relatorio sanitizado com SHA do checkout, run, tentativa, arquitetura, contagens, UpdateID/revisao e HMAC efemero de DeviceId. Nao exporta IDs de dispositivos brutos.

Esse caminho **nao contem** `DriverUpdatePilot.Execute`, `Download`, `Install`, EULA automatica, reinicio ou alteracao de hardware. O codigo de consulta nao e substituto para a homologacao em Windows 11 x64.

## Gates e interpretacao

`NO_OFFERS`: busca WUA concluida sem ofertas; `NO_ELIGIBLE_OFFERS`: ofertas presentes, nenhuma aprovada pelo piloto; `ELIGIBLE_OFFER`: candidata aprovada *somente antes de download*; `QUERY_FAILED`: pesquisa inconclusiva. No ultimo caso a etapa de consulta falha. EULA ja aceita e exigencia de entrada unica continuam mandatarios.

Instalacao real: **NO-GO**. Um runner GitHub-hosted descartavel nao equivale a snapshot reversivel comprovado, e Windows 11 ARM64 nao atesta x64. Uma futura VM Windows 11 x64 com dispositivo nao critico e snapshot host-side verificavel requer autorizacao especifica para instalacao, sem usar PC FORTE/FRACO.

## Validacao e escopo

- PR #79 / main inalteradas; PR #80 Draft sem merge; roadmap 27/42, G1–G5 abertos.
- Validacao planejada: jobs existentes Windows 2022/2025, regressao Drivers 24/24, G1.10–G1.23, F1–F4.1, mutacoes 36/36, WinForms, seguranca, consentimento, compilacao x64 e integridade externa.
- Evidencia F4.2 dependera de job real `drivers-wua-f42` na PR #80; nao registrar como executado antes da CI.
- Nenhum backend de instalacao, journal, G5, D1, Worker, Cron, Secrets, BETA ou release foi alterado.

## Execucao real F4.2 (primeiro SHA)

- Commit tecnico testado: `637fca203b2622d0439b74e53262b872c1fddcb8`; arvore de merge PR `585cea139a50f99b537eacafb494a4e9c7dd775e`.
- [Run GitHub 43865809692 / job Windows 11 ARM64 133293935202](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43865809692/job/133293935202): job `completed/success`, .NET 10 compilado e cliente WUA consultado online.
- Windows 11 build `10.0.26200.0`, `Arm64`; `PnpEntities=67`, `SignedRows=68`, `InventoryItems=68`, `PresentItems=67`.
- `WuaOffered=1`, `WuaMatches=1`, `WuaUnmatched=0`, `Eligible=0`, `Verdict=NO_ELIGIBLE_OFFERS`; `Stage=COMPLETE`, `FailureCode=null`, `HResult=null`.
- Oferta oficial associada: UpdateID `9243f9e1-976d-4a72-ae67-2163e58cf5e3`, revisao `1`; `PilotClass=null`, `Status=REJECTED_PILOT_GATE`. Classe fora da lista restrita; **nao autorizar instalacao**.
- [Artefato 14225961028](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43865809692/artifacts/14225961028) preserva evidencia sanitizada, com token HMAC apenas naquele relatorio.
- Nenhuma atualizacao/driver foi baixado ou instalado. Nao houve EULA, reboot, ou rollback. Windows 11 ARM64 nao comprova aptidao para Windows 11 x64.
- No primeiro run, o gate de seguranca `test_actions_fixadas_e_sem_credenciais_persistentes` detectou corretamente um YAML novo fora da allowlist de CI. Revisao explicitamente autoriza somente o novo arquivo `drivers-wua-f42-win11-arm.yml` e a chamada local do mesmo, mantendo a negativa para qualquer outro arquivo/action.

O sucesso global das CIs no commit de correcao da allowlist deve ser checado em execucoes novas. O checkpoint nao representa homologacao de instalacao nem snapshot restauravel.
