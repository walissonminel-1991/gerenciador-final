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
