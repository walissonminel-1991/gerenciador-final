"""Fase 1: targeted, negative startup side-effect probe on disposable Windows CI.

Not an application READY or absence-of-effects attestation.
No arbitrary Windows process, service, external API or user-owned file is modified.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

from win32_process_observation import observe_real_gf


def require(condition: bool, code: str) -> None:
    if not condition:
        raise AssertionError(code)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> int:
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true",
            "GF_STARTUP_DISPOSABLE_CI_REQUIRED")
    require(len(sys.argv) == 2, "GF_STARTUP_ARGUMENTS")
    exe = Path(sys.argv[1]).resolve(strict=True)
    temp = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    require(exe.name.lower() == "gerenciadorfinal.exe" and
            temp in exe.parents and exe.is_file(), "GF_STARTUP_PUBLISHED_EXE_SCOPE")
    local = Path(os.environ["LOCALAPPDATA"]).resolve(strict=True)
    root = local / "GerenciadorFinal"
    # Fresh hosted runner only: no existing GF state, even a legitimate beta install.
    # Do not delete/overwrite existing state for the sake of a green test.
    require(not root.exists(), "GF_STARTUP_PROFILE_NOT_FRESH")
    root.mkdir()
    sentinel = root / "GF-PHASE1-UNRELATED-SENTINEL.bin"
    pending = root / "RevisaoReinicio.dat"
    sentinel_bytes = b"GF-STARTUP-UNRELATED-LAB-SENTINEL-20261030"
    invalid_pending = b"GF-PHASE1-INVALID-DPAPI-PENDING-DO-NOT-DELETE-OR-REWRITE"
    sentinel.write_bytes(sentinel_bytes)
    pending.write_bytes(invalid_pending)
    initial = {sentinel.name: digest(sentinel.read_bytes()),
               pending.name: digest(pending.read_bytes())}
    proc = None
    observation = {}
    try:
        proc = subprocess.Popen([str(exe), "--tray"], cwd=str(exe.parent),
                                stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                stderr=subprocess.DEVNULL,
                                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
                                env={**os.environ, "GfBetaEndpoint": "",
                                     "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
        observation = observe_real_gf(proc, exe, digest(exe.read_bytes()), seconds=12)
    finally:
        if proc is not None:
            if proc.poll() is None:
                # Only the retained handle of the child launched by this test.
                proc.kill()
                observation["forced_cleanup"] = True
            else:
                observation["forced_cleanup"] = False
            proc.wait(timeout=15)
    require(proc is not None and proc.returncode == 0 and
            observation.get("graceful_exit_observed") and
            observation.get("main_form_message_loop_responded") and
            observation.get("actual_image_path_matches") and
            observation.get("readonly_inventory_search_health", {})
                .get("minimum_readonly_health_contract_observed") and
            not observation.get("forced_cleanup"),
            "GF_STARTUP_FUNCTIONAL_OBSERVATION_NOT_CONFIRMED")
    # These are expected GF-owned local writes. No scan over the whole host is
    # represented as proof of an absence of other side effects.
    diag_dir = root / "Diagnosticos"
    history_dir = root / "Historico"
    diag_files = list(diag_dir.glob("GerenciadorFinal-Diagnostico-*.log")) if diag_dir.is_dir() else []
    history_files = list(history_dir.glob("GF-Historico-*.log")) if history_dir.is_dir() else []
    require(len(diag_files) == 1 and len(history_files) == 1,
            "GF_STARTUP_LOCAL_WRITE_EXPECTATION_NOT_MET")
    require(diag_files[0].is_file() and
            diag_files[0].read_bytes().startswith(b"Gerenciador Final ") and
            history_files[0].is_file() and
            history_files[0].read_bytes().startswith(b"GF-HISTORICO|1|"),
            "GF_STARTUP_WRITE_CONTENT_UNRECOGNIZED")
    require(sentinel.is_file() and pending.is_file() and
            digest(sentinel.read_bytes()) == initial[sentinel.name] and
            digest(pending.read_bytes()) == initial[pending.name],
            "GF_STARTUP_SENTINEL_OR_INVALID_PENDING_MODIFIED")
    # Negative check: invalid DPAPI pending data should be rejected and
    # preserved. This does NOT exercise or approve the valid post-boot review.
    result = {
        "schema": "gf-phase1-startup-effects-lab/1",
        "runner_disposable": True,
        "beta_profile": "local",
        "startup_path": "GerenciadorFinal.exe --tray (real normal Main)",
        "diagnostic_local_write_observed": True,
        "reliability_history_local_write_observed": True,
        "unrelated_sentinel_preserved": True,
        "invalid_pending_postboot_sentinel_preserved": True,
        "valid_postboot_pending_reconciliation_tested": False,
        "functional_search_minimum_observed": True,
        "all_writes_enumerated": False,
        "external_brokers_quiescent_proven": False,
        "cold_recovery_proven": False,
        "operational_ready": False,
        "phase1_complete": False,
        "gates_closed": 0,
    }
    print("GF_PHASE1_STARTUP_EFFECTS_REPORT " + json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, OSError, ValueError, subprocess.SubprocessError) as ex:
        print("GF_PHASE1_STARTUP_EFFECTS_FAIL " + type(ex).__name__ + ":" +
              (str(ex) if isinstance(ex, AssertionError) else "runtime_error"),
              file=sys.stderr)
        sys.exit(1)
