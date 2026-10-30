#!/usr/bin/env python3
"""Win32 NTFS negative provenance fixture. NO installer, updater or maintenance.

Two separately launched writer processes, associated by the TEST with different
transactions, modify the same hard-linked object through different paths.
A deliberately restricted observer sees identical path/file-ID/hash/claimed-
transaction evidence after both operations. This refutes attribution from
those fields alone; it is not a complete kernel provenance or quiescence test.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid

PREFIX = "GF_PHASE1_EFFECT_ATTRIBUTION_CASE "
SCHEMA = "gf-phase1-effect-attribution-alias/1"
BEFORE = b"000000\n"
AFTER = b"111111\n"


def require(cond, cause):
    if not cond:
        raise RuntimeError("GF_PHASE1_ATTRIBUTION_" + cause)


def authorize_runner():
    require(os.name == "nt" and os.getenv("GITHUB_ACTIONS") == "true",
            "WINDOWS_ACTIONS_ONLY")
    runner = os.getenv("RUNNER_TEMP", "")
    require(runner and Path(runner).is_dir(), "RUNNER_TEMP_REQUIRED")
    require(os.getenv("GF_BETA_ENDPOINT", "") == "", "REMOTE_BETA_MUST_BE_OFF")
    return Path(runner).resolve(strict=True)


def file_view(root, claimed):
    names = ("tx-a-view.fixture", "tx-b-view.fixture")
    observations = []
    for name in names:
        path = root / name
        require(path.is_file() and not path.is_symlink(), "EXPECTED_REGULAR_FILE")
        info = path.stat()
        raw = path.read_bytes()
        observations.append({
            "path": name,
            "file_id": [info.st_dev, info.st_ino],
            "link_count": info.st_nlink,
            "length": info.st_size,
            "sha256": hashlib.sha256(raw).hexdigest(),
        })
    require(observations[0]["file_id"] == observations[1]["file_id"],
            "HARDLINK_OBJECT_IDENTITY_DIVERGED")
    require(all(x["link_count"] == 2 and x["file_id"][1] > 0
                for x in observations), "NTFS_LINK_IDENTITY_UNAVAILABLE")
    return {"claimed_transaction": claimed, "files": observations}


def writer(alias):
    authorize_runner()
    require(alias in ("a", "b"), "INVALID_WRITER_ALIAS")
    root_string = os.getenv("GF_PHASE1_PROBE_ROOT", "")
    require(root_string, "WRITER_SCOPE_MISSING")
    root = Path(root_string).resolve(strict=True)
    require(root.parent == Path(os.environ["RUNNER_TEMP"]).resolve(strict=True),
            "WRITER_SCOPE_OUTSIDE_RUNNER_TEMP")
    require((root / "gf-probe-only.fixture").read_bytes() == b"LAB_ONLY\n",
            "WRITER_SCOPE_UNMARKED")
    target = root / ("tx-a-view.fixture" if alias == "a" else "tx-b-view.fixture")
    require(target.is_file() and not target.is_symlink(),
            "WRITER_TARGET_NOT_REGULAR")
    with target.open("r+b", buffering=0) as handle:
        require(handle.read() == BEFORE, "WRITER_BEFORE_STATE_CHANGED")
        handle.seek(0)
        handle.write(AFTER)
        handle.flush()
        os.fsync(handle.fileno())
    return 0


def run_probe():
    runner = authorize_runner()
    with tempfile.TemporaryDirectory(prefix="gf-phase1-attribution-", dir=runner) as temp:
        root = Path(temp).resolve(strict=True)
        require(root.parent == runner, "ROOT_SCOPE_WRONG")
        require(root.stat().st_dev == runner.stat().st_dev, "VOLUME_CHANGED")
        (root / "gf-probe-only.fixture").write_bytes(b"LAB_ONLY\n")
        a = root / "tx-a-view.fixture"
        b = root / "tx-b-view.fixture"
        a.write_bytes(BEFORE)
        require(not b.exists(), "ALIAS_MUST_BE_NEW")
        os.link(a, b)
        require(os.path.samefile(a, b), "HARDLINK_NOT_SAME_OBJECT")
        tx_a, tx_b = str(uuid.uuid4()), str(uuid.uuid4())
        require(tx_a != tx_b, "TRANSACTIONS_NOT_DISTINCT")
        # Claimed transaction is same-user controlled fixture metadata.
        # It is intentionally NOT trusted or issued by an external authority.
        claimed_tx = tx_a
        evidence = []
        actual_origins = []
        for actual_tx, alias in ((tx_a, "a"), (tx_b, "b")):
            with a.open("r+b") as handle:
                handle.seek(0)
                handle.write(BEFORE)
                handle.truncate(len(BEFORE))
                handle.flush()
                os.fsync(handle.fileno())
            require(a.read_bytes() == BEFORE and b.read_bytes() == BEFORE,
                    "RESET_FAILED")
            env = dict(os.environ)
            env["GF_PHASE1_PROBE_ROOT"] = str(root)
            child = subprocess.run(
                [sys.executable, str(Path(__file__).resolve(strict=True)),
                 "--writer", alias],
                cwd=str(root), env=env, stdin=subprocess.DEVNULL,
                capture_output=True, timeout=15, check=False)
            require(child.returncode == 0,
                    "WRITER_PROCESS_FAILED_" + str(child.returncode))
            require(a.read_bytes() == AFTER and b.read_bytes() == AFTER,
                    "ALIAS_EFFECT_NOT_SHARED")
            evidence.append(file_view(root, claimed_tx))
            actual_origins.append(actual_tx)
        require(actual_origins[0] != actual_origins[1],
                "ACTUAL_PROVENANCE_NOT_DISTINCT")
        require(evidence[0] == evidence[1],
                "PROJECTED_EVIDENCE_NOT_INDISTINGUISHABLE")
        # A source marker claimed TX-A while the second, independently
        # launched process really wrote through TX-B's alias.
        forged_attribution_observed = (
            evidence[1]["claimed_transaction"] == tx_a
            and actual_origins[1] == tx_b)
        require(forged_attribution_observed, "FALSE_ATTRIBUTION_NOT_EXERCISED")
        report = {
            "schema": SCHEMA,
            "status": "NEGATIVE_PROVENANCE_COUNTEREXAMPLE",
            "scope": "DISPOSABLE_WINDOWS_NTFS_HARDLINK_FIXTURE",
            "different_writer_processes_executed": True,
            "distinct_test_transaction_origins": True,
            "hardlinks_share_ntfs_file_identity": True,
            "same_projected_evidence_for_distinct_origins": True,
            "same_user_claim_misattributed_other_origin": True,
            "observed_fields": [
                "two_relative_paths", "file_id", "link_count",
                "length", "sha256", "untrusted_claimed_transaction"
            ],
            "out_of_scope_observations": [
                "kernel_etw_provenance", "trusted_msi_activity_id",
                "process_tree_audit", "wall_clock_metadata"
            ],
            "real_msi_or_provider_executed": False,
            "new_gf_execution": False,
            "new_windows_maintenance_executed": False,
            "external_authority_established": False,
            "quiescence_proven": False,
            "automatic_recovery_permitted": False,
            "operational_ready": False,
        }
        print(PREFIX + json.dumps(report, separators=(",", ":"), sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        if len(sys.argv) == 3 and sys.argv[1] == "--writer":
            sys.exit(writer(sys.argv[2]))
        require(len(sys.argv) == 1, "BAD_ARGS")
        sys.exit(run_probe())
    except Exception as exc:
        print(type(exc).__name__ + ": " + str(exc), file=sys.stderr)
        sys.exit(1)
