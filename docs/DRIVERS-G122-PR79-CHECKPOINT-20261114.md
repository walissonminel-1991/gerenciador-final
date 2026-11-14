# GERENCIADOR FINAL — DRIVERS G1.22 — Interleaving durante congelamento

Data: 14/11/2026 · PR #79 aberta, Draft, sem merge · branch drivers-pnp-problemas-consulta-20261113 · base candidata-portatil-pessoal-pc-forte-20261101. Roadmap 27/42; G1–G5 abertos.

## Gate G1.21 de entrada revalidado
SHA 2f0ab9ee637295eef531d9212b82a485544e9048, Windows 43648551904 e integridade 43648551916: ambas completed/SUCCESS, attempt 1 no mesmo SHA. Windows 2022/2025, 24/24 G1.9 por runner, WinForms G1.10–G1.21, 36/36 mutações, regressões gerais, segurança, consentimento, x64, integridade. Comentário #6885341306 consultado. Histórico G1.21 de quatro candidatas Windows FAILURE e seus diagnósticos lido.

## Hipótese adversarial G1.22 (fixture primeiro; produção não alterada)
A cópia G1.21 enumera Items e então Store/Published/WindowsHistory separadamente. Uma fonte com coleções mutáveis pode entregar o snapshot de Items no instante A, mudar a lista original assim que a primeira enumeração termina e entregar Store/Published no instante B. A UI pode publicar evidências de tempos diferentes, embora o snapshot usado pelo histórico seja internamente imutável. A captura real WMI materializa seus arrays antes de disponibilizá-los, mas um provedor sintético compartilhado permite demonstrar a ausência de transação entre coleções.

TestesLayout/DriverG122ConcurrentFreezeTests.cs: lê somente itens sintéticos, estabelece baseline v1 numa pasta temporária nova, usa IReadOnlyList cuja primeira enumeração altera DeviceId/INF/versão e listas Store/Published/WindowsHistory entre as cópias, atualiza a UI WinForms real e exige ausência de publicação e imutabilidade byte a byte do v1. Exercita quatro viewports e refiltro. Proibido DriverHistory.DefaultPath, WMI/WUA/SetupAPI/INF nativo, downloads e manutenção.

## Gate
PENDENTE: primeiramente observar CI vermelha com contraexemplo literal ANTES de corrigir. Depois decidir o menor tratamento fail-closed testável e reexecutar Windows 2022/2025 24/24, G1.10–G1.22, 36/36 mutações, testes visuais/gerais, segurança/privacidade, consentimento, compilação Windows x64 e integridade externa no MESMO SHA. Não confundir ausência de erro com atomicidade sob escrita concorrente arbitrária. GO apenas com duas CIs completed/SUCCESS.

Preservar schema v1, hash SHA-256, lock, 2 MB, retenção 20, atomicidade File.Move, monotonicidade G1.20, guard global DeviceId G1.19, G1.21, segurança/consentimento. CanInstall=false, CanMaintain=false, IsNewerProven=false, CompletePackageVerified=false. WaitAsync cancela espera, não Search COM síncrono. Main, G5, D1, Worker, Cron, Secrets, BETA, releases e PCs pessoais intocados.
