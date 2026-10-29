# CHAT ALTO M2 — G6: real process-exit barrier (isolated)

This is the **first subcomponent** of M2, not the GF lifecycle and not an
auto-updater. It proves a narrow necessary condition with owned inert test
processes: before spawning a candidate, every registered predecessor must
actually have exited successfully according to the retained Windows process
handle. "BUSY", an acknowledged command, a child still alive, failed exit,
duplicate PID, deadline expiry or reused approval cannot authorize a candidate.

Real inert processes are started and communicate via redirected standard
input/output solely to exercise *exit behavior*. Their STDIO is **not**
authenticated IPC, does not replace the M1 Named Pipes channel, and is not a
suitable GF control path. There is no GF process discovery, Windows shutdown
integration, GUI-ready proof, multihost app, durability or rollback here.

The child executable is the same test apphost (`GF.Updater.M2.Tests.exe`);
it has no software update or system maintenance capability. Only harness-owned
child processes may be killed in test cleanup. No GF application, BETA package,
private channel, user accounts, permissions, UAC, secrets or infrastructure are
accessed/changed. Artifacts are produced only in GitHub runner temp; no upload.

Runner: `python` is not required. On authorized disposable Windows:

```powershell
dotnet run --project prototipos/autoatualizador-seguro/m2/M2.csproj -- --report "$env:TEMP\\gf-m2.json"
```

The next M2 increment should bind this exit barrier to verified M1 READY for
an inert candidate, and to the existing Stage 2 transaction journal. That is
not included in this subproof. G6 and all G1–G8 stay blocked.

## Integration increment — actual pipe READY after real predecessor exit

`M2Integration` connects the exit barrier to the already isolated Stage 3
`PipeFrames`/`PeerIdentity` identity checks, and invokes the M1 `G5Admission`
**only when the runner truly is non-elevated**. It spawns an inert previous
process, waits for actual OS exit, then spawns an inert candidate and receives
a transaction/nonce-bound READY on a real Named Pipe. Negative tests corrupt
the candidate's nonce and transaction. Runner elevation inherited from GitHub
Actions validates only OS transport/frame mechanics, explicitly **not**
operational admission. The candidate sends READY immediately in a console app;
this is not WinForms HWND readiness, functional health, actual GF executable
identity or durable rollback. M2/G6 remains blocked.

## M2 increment: real WinForms HWND and message loop

The dedicated `GF.Updater.M2.WinFormsProbe.exe` test apphost is produced by
`M2WinFormsProbe.csproj` in a separate temporary folder and is never
the GF product. It creates a hidden WinForms form and only sends its
transaction/nonce-bound READY from `Application.Idle` after entry into
the STA WinForms message loop. The parent verifies a top-level HWND
belonging to the exact candidate PID with Windows APIs after kernel pipe
identity checks. The flow retains the real predecessor-exit gate.

Negative cases exercise a wrong nonce, wrong transaction and a connected
probe withholding READY. The missing-READY case uses a 500 ms real async
read deadline and must refuse to treat silence as success. The candidate
receives CLOSE solely for test cleanup; it has no update or maintenance
capability. The Windows runner checks both `window_readiness_verified`
and `missing_ready_refused` in its JSON report.

**Boundary:** a hidden HWND after message-loop idle is *not* proof of
the main GF's functional health, tray lifecycle, other instances,
workers, durable state, crash recovery, power loss or authenticated
bundle. Elevated CI can test the mechanism without authorizing elevated
operational IPC. Distinct users and sessions and an actual non-elevated
G5 admission remain blocked, as does G6. No WORK, rollout or product
integration is justified solely by these tests.
