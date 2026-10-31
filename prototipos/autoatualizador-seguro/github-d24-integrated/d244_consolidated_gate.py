"""D2.4.4: strict, adversarial consolidated evidence gate, TEST ONLY.

Re-executes existing D2, D2.3, D2.4.2, D2.4.3 on the same Windows SHA.
No new downloader, TUF implementation, update capability or recovery.
A green result cannot prove same-user isolation, H1/H2/H3, NTFS/TOCTOU,
crash durability, executable provenance, or real GitHub TLS/CDN handling.
"""
from __future__ import annotations

import fnmatch
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[3]
LAB=ROOT/"prototipos"/"autoatualizador-seguro"

def fail(reason):
    raise AssertionError("GF_D244_FAIL_"+reason)

def require(ok,reason):
    if not ok: fail(reason)

D2={
    "schema":"gf-github-d2-inert-acquisition/1","status":"passed",
    "adversarial_rejected":21,"positive_inert_acquisitions":2,
    "persistent_tuf_lab_used":True,"d0_real_csharp_parser_used":True,
    "manifest_authenticated_with_public_test_key":True,
    "memory_only_source":True,"readback_sha_verified":True,
    "outside_stage_sentinel_preserved":True,
    "same_user_toctou_resistance_proven":False,
    "production_signing_authority":False,
    "full_history_rollback_resistance_proven":False,
    "real_network_requests":0,"executable_downloads":0,
    "executable_launches":0,"installations":0,"product_modified":False,
    "operational_ready":False,"automatic_recovery_authorized":False
}
D23={
    "schema":"gf-d23-dotnet-httpclient-loopback/1","status":"passed",
    "positive_inert_streams":3,"negative_cases_rejected":29,
    "loopback_connections_observed":31,"failed_loopback_connections_observed":28,
    "preserved_preexisting_file_ids":2,
    "complete_acquisition_deadline_tested":True,
    "failure_preserves_unknown_file_identity":True,
    "http_client":"dotnet10-System.Net.Http.HttpClient",
    "only_numeric_loopback_connection":True,"redirects_disabled":True,
    "cancellation_tests_included":True,"inert_sha256_readback_verified":True,
    "outside_staging_sentinel_preserved":True,
    "d0_d1_tuf_chain_verified_in_dotnet_lab":False,
    "production_trust_root":False,
    "github_asset_redirect_supported":False,"tls_or_real_github_tested":False,
    "same_user_toctou_resistance_proven":False,
    "full_history_rollback_resistance_proven":False,
    "real_executable_downloads":0,"installations":0,"product_modified":False,
    "operational_ready":False,"automatic_recovery_authorized":False
}
D242={
    "schema":"gf-d242-csharp-owned-tuf-session/1","status":"passed",
    "positive_inert_sessions":1,"negative_sessions_rejected":15,
    "synthetic_receipts_rejected":7,
    "embedded_tuf_validator_source":True,
    "csharp_owns_child_and_stage":True,
    "old_direct_manifest_sha_handoff_rejected":True,
    "bootstrap_single_pinned_snapshot_used":True,
    "shared_dotnet_d23_transport":True,"session_replay_rejected":True,
    "secure_against_same_user_process_replacement":False,
    "persistent_tuf_history_rollback_proven":False,
    "same_user_ntfs_toctou_proven":False,"test_signer_public_only":True,
    "external_requests":0,"downloaded_executables":0,
    "installations":0,"product_modified":False,
    "operational_ready":False,"automatic_recovery_authorized":False
}
D243={
    "schema":"gf-d243-persistent-ntfs-lab/1","status":"passed",
    "csharp_controlled_test_tuf_generations":2,
    "independent_windows_coordinator_processes":True,
    "observed_trust_role_generations":[1,2],
    "intact_history_replay_blocked":True,
    "cooperative_concurrency_denied":True,
    "partial_history_held_without_cleanup":True,
    "full_history_rewind_reaccepted_old_version":True,
    "full_history_rewind_is_security_counterexample":True,
    "ntfs_observation_cases":6,
    "reparse_junction_hardlink_and_race_tested":True,
    "external_sentinel_preserved":True,
    "same_user_rollback_resistance_proven":False,
    "same_user_ntfs_toctou_resistance_proven":False,
    "crash_durability_proven":False,
    "production_signing_authority":False,
    "operational_ready":False,"automatic_recovery_authorized":False,
    "real_external_network_requests":0,"real_executable_downloads":0,
    "installations":0,"product_modified":False,
    "staging_or_recovery_performed":False
}
EXPECTED={"GF_D2_RESULT ":D2,"GF_D23_RESULT ":D23,
          "GF_D242_RESULT ":D242,"GF_D243_RESULT ":D243}
REQUIRED_DEPS=[
    ".github/workflows/gf-d244-consolidated-lab.yml",
    ".github/workflows/gf-github-d2-inert-lab.yml",
    ".github/workflows/gf-d23-dotnet-httpclient-lab.yml",
    ".github/workflows/gf-d24-inert-integrated-lab.yml",
    ".github/workflows/gf-d243-persistent-ntfs-lab.yml",
    "prototipos/autoatualizador-seguro/github-metadata-lab/GitHubReleaseDiscovery.cs.txt",
    "prototipos/autoatualizador-seguro/github-trust-lab/GF.D11.D0Probe.csproj",
    "prototipos/autoatualizador-seguro/github-trust-lab/d11_persistent_lab.py",
    "prototipos/autoatualizador-seguro/github-crossprocess-lab/d12_crossprocess_lab.py",
    "prototipos/autoatualizador-seguro/github-auth-lab/d1_release_manifest_lab.py",
    "prototipos/autoatualizador-seguro/github-d2-inert/d2_inert_lab.py",
    "prototipos/autoatualizador-seguro/github-d23-dotnet-loopback/HttpClientLoopback.cs.txt",
    "prototipos/autoatualizador-seguro/github-d24-integrated/D24Handoff.cs.txt",
    "prototipos/autoatualizador-seguro/github-d24-integrated/D243Coordinator.cs.txt",
    "prototipos/autoatualizador-seguro/github-d24-integrated/d242_tuf_child.py",
    "prototipos/autoatualizador-seguro/github-d24-integrated/d243_persistent_child.py",
    "prototipos/autoatualizador-seguro/github-d24-integrated/d243_persistent_lab.py",
    "prototipos/autoatualizador-seguro/github-d24-integrated/d24_chain_lab.py",
    "prototipos/autoatualizador-seguro/fixtures/bootstrap.test-only.json",
    "prototipos/autoatualizador-seguro/generate_fixtures.py"
]

def unique_pairs(pairs):
    obj={}
    for key,value in pairs:
        if key in obj: raise ValueError("DUPLICATE_JSON_KEY")
        obj[key]=value
    return obj

def check_report(raw,expected):
    """Exact key set, type (bool is NOT int), values and duplicate rejection."""
    try:
        got=json.loads(raw,object_pairs_hook=unique_pairs,
                       parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (ValueError,TypeError): return False
    if type(got) is not dict or set(got)!=set(expected): return False
    for key,value in expected.items():
        current=got[key]
        if type(current) is not type(value): return False
        if type(value) is list:
            if len(current)!=len(value): return False
            if any(type(a) is not type(b) or a!=b
                   for a,b in zip(current,value)): return False
        elif current!=value: return False
    return True

def mutate(value):
    if type(value) is bool: return not value
    if type(value) is int: return value+1
    if type(value) is str: return value+"_FORGED"
    if type(value) is list: return value[::-1] if len(value)>1 else [0]
    raise AssertionError("UNEXPECTED_SCHEMA_VALUE_TYPE")

def mutation_selftest():
    rejected=0
    cases=0
    for marker,expected in EXPECTED.items():
        raw=json.dumps(expected,sort_keys=True,separators=(",",":"))
        require(check_report(raw,expected),"VALID_SCHEMA_WAS_REFUSED")
        require(not check_report(raw+"\n{}",expected),"TRAILING_JSON_ACCEPTED")
        rejected+=1
        cases+=1
        first=next(iter(expected))
        duplicate=raw[:-1]+","+json.dumps(first)+":"+json.dumps(expected[first])+"}"
        require(not check_report(duplicate,expected),"DUPLICATE_FIELD_ACCEPTED")
        rejected+=1
        cases+=1
        other=dict(expected);other["surprise"]="unauthorized"
        require(not check_report(json.dumps(other),expected),"EXTRA_FIELD_ACCEPTED")
        rejected+=1
        cases+=1
        for key,value in expected.items():
            absent=dict(expected);del absent[key]
            mutated=dict(expected);mutated[key]=mutate(value)
            wrong=dict(expected)
            wrong[key]=(0 if type(value) is bool else
                        False if type(value) is int else
                        "true" if type(value) is list else
                        0)
            for label,case in (("missing",absent),("altered",mutated),
                               ("type",wrong)):
                cases+=1
                if check_report(json.dumps(case),expected):
                    fail("FALSE_ACCEPT_"+label+"_"+marker.strip()+"_"+key)
                rejected+=1
    require(rejected==cases and rejected>=250,"INCOMPLETE_MUTATION_SWEEP")
    return rejected

def check_triggers():
    file=ROOT/".github"/"workflows"/"gf-d244-consolidated-lab.yml"
    text=file.read_text(encoding="utf-8")
    require("persist-credentials: false" in text and
            "contents: read" in text and
            "runs-on: windows-2022" in text,
            "UNSAFE_CI_PERMISSIONS_OR_RUNNER")
    import re
    parts=text.split("    paths:",1)
    require(len(parts)==2,"NO_PATH_FILTER")
    # The next top-level field ends the push.paths list.
    patterns=[]
    for line in parts[1].splitlines()[1:]:
        match=re.fullmatch(r"\s{6}- '([^']+)'",line)
        if match: patterns.append(match.group(1))
        else: break
    require(bool(patterns),"EMPTY_PATH_FILTER")
    for path in REQUIRED_DEPS:
        require((ROOT/path).is_file(),"MISSING_DEPENDENCY_"+path)
        require(any(fnmatch.fnmatchcase(path,pat) for pat in patterns),
                "UNCOVERED_DEPENDENCY_"+path)
    return len(REQUIRED_DEPS)

def launch(name,arguments,marker,expected):
    run=subprocess.run(arguments,cwd=ROOT,input="",stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,text=True,timeout=150,
        env={**os.environ,"PYTHONDONTWRITEBYTECODE":"1"})
    if run.returncode!=0:
        raise RuntimeError("SUITE_FAILED_"+name+"_EXIT_"+str(run.returncode)+
                           "_ERR_"+run.stderr[-900:])
    lines=[l[len(marker):] for l in run.stdout.splitlines()
           if l.startswith(marker)]
    require(len(lines)==1,"SUITE_RESULT_COUNT_"+name)
    require(check_report(lines[0],expected),"SUITE_RESULT_INVALID_"+name)
    # Print only the typed, verified bounded result; no raw environment.
    print("GF_D244_VERIFIED_"+name+" "+lines[0],flush=True)
    return True

def main(argv):
    require(sys.platform=="win32" and
            os.environ.get("GITHUB_ACTIONS")=="true" and
            os.environ.get("RUNNER_TEMP"),
            "WINDOWS_ACTIONS_ONLY")
    require(len(argv)==4,"D244_CLI")
    temp=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    paths=[Path(x).resolve(strict=True) for x in argv[1:]]
    require(all(p.is_relative_to(temp) for p in paths) and
            [p.name for p in paths]==["GF.D11.D0Probe.exe",
                "GF.D23.HttpClientLab.exe","GF.D24.InertChainLab.exe"],
            "LAB_EXECUTABLE_SCOPE")
    d0,d23,d24=map(str,paths)
    mutations=mutation_selftest()
    deps=check_triggers()
    tests=[
        ("D2",[sys.executable,str(LAB/"github-d2-inert"/"d2_inert_lab.py"),d0],
         "GF_D2_RESULT ",D2),
        ("D23",[d23],"GF_D23_RESULT ",D23),
        ("D242",[sys.executable,str(LAB/"github-d24-integrated"/"d24_chain_lab.py"),
                  d0,d24],"GF_D242_RESULT ",D242),
        ("D243",[sys.executable,str(LAB/"github-d24-integrated"/"d243_persistent_lab.py"),
                  d0,d24],"GF_D243_RESULT ",D243)
    ]
    validated=[]
    for name,args,marker,expected in tests:
        launch(name,args,marker,expected)
        validated.append(name)
    result={
        "schema":"gf-d244-strict-inert-evidence/1",
        "status":"passed",
        "independent_test_suites_executed":validated,
        "required_trigger_dependencies_covered":deps,
        "schema_mutations_rejected":mutations,
        "exact_field_sets_and_types_enforced":True,
        "duplicate_missing_extra_and_forged_fields_refused":True,
        "tuf_replay_with_intact_history_blocked":True,
        "full_history_rewind_reaccepted_old_version":True,
        "full_history_rewind_is_security_counterexample":True,
        "same_user_rollback_resistance_proven":False,
        "same_user_ntfs_toctou_resistance_proven":False,
        "cooperative_lock_is_security_boundary":False,
        "production_signing_authority":False,
        "operational_ready":False,
        "automatic_recovery_authorized":False,
        "real_external_downloads":0,
        "real_executable_installs":0,
        "product_modified":False
    }
    print("GF_D244_RESULT "+json.dumps(result,sort_keys=True),flush=True)
    return 0

if __name__=="__main__":
    raise SystemExit(main(sys.argv))
