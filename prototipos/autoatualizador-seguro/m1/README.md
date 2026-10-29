# CHAT ALTO M1 — G5 IPC experimental (not operational)

This is a narrow **Windows-only** test harness; no GF code, binaries, updates,
telemetry, accounts, ACLs, keys, deployments or releases are changed. Sources are
stored as `.cs.txt` and explicitly included only by `M1.csproj`.

The admission policy reuses the Stage 3 `PipeFrames` and `PeerIdentity` types.
Only the server's retained child process handle, the kernel-reported pipe PID,
token SID/session/elevation and image path are used to observe peer identity.
A trusted expected context contains PID, SID, session, elevation, image, a
fixture SHA-256 for that executable, transaction and nonce. The READY frame must
match its transaction/nonce and can be accepted once before a monotonic deadline.
The Windows client verifies its parent PID, peer image and same-context identity.

**Security boundary:** the executable hash is a *fixture identity*, not a
pinned complete GF installation identity. On-disk file identity, entire runtime
and dependency pinning, TOCTOU, multiple GF instances, workers, shutdown and
elevated UAC worker are not addressed here. The existing Stage 5 pinned bundle
remains separate. No product startup or update command is exposed in this
protocol. A production adapter requires verified installation provenance,
distinct publisher authority and an explicit decision for each participant.

## Narrow validation

The branch-restricted `autoatualizador-m1-ipc-isolado.yml` job uses Windows
2022, SDK 10.0.402, an external temporary build directory and a temporary
JSON report; it compiles/executes **only** `GF.Updater.M1.Tests.exe`.
No artifact upload, credential persistence, GF build or workflow permissions
beyond `contents:read`. It also checks that the product's Compile items do
not include M1 source. Manual command on a disposable authorized Windows machine:

```powershell
dotnet restore prototipos/autoatualizador-seguro/m1/M1.csproj
dotnet run --project prototipos/autoatualizador-seguro/m1/M1.csproj -- --report "$env:TEMP\\gf-m1-report.json"
```

The pure decision tests exercise refusals using invented context values; they
**do not** establish cross-user/session isolation by experiment. Real kernel
handshake cases run only when the runner is genuinely **non-elevated**. An
elevated runner explicitly marks that suite **blocked**, without creating user
accounts, changing privileges or fabricating an unelevated process. Remote-user
and other-session handshakes are **blocked** even when all local cases pass.

G5 remains blocked. This test does not close G1–G8, replace a qualified
independent TUF review, or authorize integration into the BETA .66. The PR #65
Draft, main and the official roadmap remain untouched.
