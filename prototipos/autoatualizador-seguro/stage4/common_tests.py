"""Isolated threat-boundary models. Public deterministic fixtures are never production roots."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey


class Blocked(ValueError):
    pass


def require(condition, code):
    if not condition:
        raise Blocked(code)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()


def independent_floor(local, retained, challenge, expected_installation):
    """MODEL ONLY: retained comes from an independently authenticated live witness, not local storage.
    No witness is installed or contacted. Its freshness/authentication is an explicit precondition.
    """
    require(retained is not None, "INDEPENDENT_FRESHNESS_UNAVAILABLE")
    require(retained["challenge"] == challenge and retained["installation"] == expected_installation, "WITNESS_BINDING")
    require(local["sequence"] == retained["sequence"] and local["digest"] == retained["digest"], "RESTORE_OR_INCOMPLETE_TRANSACTION")
    return False  # Even matching model evidence does not create an install capability.


def bootstrap(root_bytes, expected_fingerprint, production=False):
    require(expected_fingerprint is not None, "NO_PRETRUSTED_FINGERPRINT")
    require(hashlib.sha256(root_bytes).hexdigest() == expected_fingerprint, "BOOTSTRAP_ANCHOR_MISMATCH")
    envelope = json.loads(root_bytes)
    signed = envelope["signed"]
    roles, keys = signed["roles"], signed["keys"]
    root_role = roles["root"]
    require(root_role["threshold"] == 2 and len(set(root_role["keyids"])) == 3, "ROOT_QUORUM")
    require(all(roles[r]["threshold"] == 1 and len(roles[r]["keyids"]) == 1 for r in ("targets", "snapshot", "timestamp")), "ROLE_QUORUM")
    role_sets = [set(roles[r]["keyids"]) for r in ("root", "targets", "snapshot", "timestamp")]
    require(sum(len(s) for s in role_sets) == len(set.union(*role_sets)), "ROLE_KEY_REUSE")
    for key in keys.values():
        require(key["keytype"] == "ed25519" and key["scheme"] == "ed25519", "ALGORITHM")
    verified = set()
    for signature in envelope["signatures"]:
        key_id = signature["keyid"]
        if key_id not in root_role["keyids"] or key_id in verified:
            continue
        try:
            Ed25519PublicKey.from_public_bytes(bytes.fromhex(keys[key_id]["keyval"]["public"])).verify(bytes.fromhex(signature["sig"]), canonical(signed))
            verified.add(key_id)
        except Exception:
            pass
    require(len(verified) >= 2, "ROOT_SIGNATURE_QUORUM")
    require(not production, "PUBLIC_TEST_ROOT_NEVER_PRODUCTION")
    return {"fixture_only": True, "installable": False, "distinct_root_signers": len(verified)}


def run(repository, fixtures):
    results, observations = [], []

    def test(name, description, action):
        try:
            action()
            results.append(dict(id=name, description=description, status="passed"))
        except Exception as e:
            results.append(dict(id=name, description=description, status="failed", error=f"{type(e).__name__}: {e}"))

    def refuses(action, code):
        try:
            action()
        except Blocked as e:
            require(str(e) == code, "WRONG_REJECTION:" + str(e))
            return
        raise AssertionError("unsafe acceptance " + code)

    local = dict(sequence=2, digest="2" * 64)
    retained = dict(**local, challenge="fresh-public-fixture-challenge", installation="test-installation")
    args = ("fresh-public-fixture-challenge", "test-installation")
    test("U4-WITNESS-01", "Exact retained floor is fixture-only, never installable", lambda: require(independent_floor(local, retained, *args) is False, "FALSE_INSTALLABLE"))
    test("U4-WITNESS-02", "Missing independent freshness stops; no local reset", lambda: refuses(lambda: independent_floor(local, None, *args), "INDEPENDENT_FRESHNESS_UNAVAILABLE"))
    test("U4-WITNESS-03", "Whole suffix restoration differs from independently retained floor", lambda: refuses(lambda: independent_floor(dict(sequence=1, digest="1" * 64), retained, *args), "RESTORE_OR_INCOMPLETE_TRANSACTION"))
    test("U4-WITNESS-04", "Same sequence with another digest is not approved", lambda: refuses(lambda: independent_floor(dict(sequence=2, digest="f" * 64), retained, *args), "RESTORE_OR_INCOMPLETE_TRANSACTION"))
    test("U4-WITNESS-05", "Stale witness response cannot satisfy a new challenge", lambda: refuses(lambda: independent_floor(local, retained, "new-challenge", args[1]), "WITNESS_BINDING"))
    test("U4-WITNESS-06", "Other installation receipt cannot satisfy this installation", lambda: refuses(lambda: independent_floor(local, retained, args[0], "other-installation"), "WITNESS_BINDING"))
    observations.append(dict(kind="independent_floor_model", authenticated_witness_implemented=False, provisioned=False, power_cut_executed=False, production_gate="blocked", local_same_user_rewrite_resistance=False))

    root = (fixtures / "trusted-root-v1.json").read_bytes()
    fingerprint = "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e"
    test("U4-BOOT-01", "Pretrusted fixture root has two distinct Ed25519 signatures and separate role keys", lambda: require(bootstrap(root, fingerprint)["distinct_root_signers"] >= 2, "BOOTSTRAP"))
    test("U4-BOOT-02", "No trust-on-first-use fingerprint from the imported archive", lambda: refuses(lambda: bootstrap(root, None), "NO_PRETRUSTED_FINGERPRINT"))
    test("U4-BOOT-03", "Replacement root cannot replace the incorporated anchor", lambda: refuses(lambda: bootstrap(root + b" ", fingerprint), "BOOTSTRAP_ANCHOR_MISMATCH"))
    test("U4-BOOT-04", "Public deterministic test root is categorically refused in production mode", lambda: refuses(lambda: bootstrap(root, fingerprint, True), "PUBLIC_TEST_ROOT_NEVER_PRODUCTION"))
    duplicate = json.loads(root); duplicate["signatures"] = [duplicate["signatures"][0]] * 2
    duplicate = canonical(duplicate)
    test("U4-BOOT-05", "Repeated signer never becomes a quorum", lambda: refuses(lambda: bootstrap(duplicate, hashlib.sha256(duplicate).hexdigest()), "ROOT_SIGNATURE_QUORUM"))
    reused = json.loads(root)
    reused["signed"]["roles"]["targets"]["keyids"] = reused["signed"]["roles"]["snapshot"]["keyids"]
    reused = canonical(reused)
    test("U4-BOOT-06", "Role key reuse is refused before a custody plan is considered", lambda: refuses(lambda: bootstrap(reused, hashlib.sha256(reused).hexdigest()), "ROLE_KEY_REUSE"))
    observations.append(dict(kind="bootstrap_dry_run", anchor=fingerprint, root="public-test-only", production_keys_created=False, operational_custodians_enrolled=False, production_gate="blocked"))
    plan = json.loads((Path(__file__).parent / "bootstrap-custody-plan.json").read_text())
    test("U4-CUSTODY-01", "Preparation contains no provisioned root, custodian or production installation permission", lambda: require(
        plan["operational"] is False and plan["production_root"] is None and plan["production_fingerprint"] is None
        and plan["root_threshold"] == 2 and len(plan["root_slots"]) == 3
        and all(x["custodian"] is None and x["offline_medium"] is None and x["restore_drill"] == "not_executed" for x in plan["root_slots"])
        and all(x["key_created"] is False for x in plan["other_roles"].values())
        and plan["private_material_in_repo_or_build_job"] is False and plan["github_access_token_in_exe"] is False
        and plan["beta_66_conversion_allowed"] is False and plan["automatic_silent_install_allowed"] is False, "PREPARATION_EXCEEDED_SCOPE"))

    spec = importlib.util.spec_from_file_location("approved_gf_package_contract", repository / "scripts/verificar_pacote.py")
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    with tempfile.TemporaryDirectory(prefix="gf-u4-gf-recipe-model-") as directory:
        folder = Path(directory)
        version, runtime = "0.0.0.1", "10.0.13"  # Invented metadata model; no GF exe/dll is made.
        libraries = {
            "GerenciadorFinal/" + version: {"runtime": {"GerenciadorFinal.dll": {}}},
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/" + runtime: {"native": {"coreclr.dll": {}}},
            "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/" + runtime: {"runtime": {"System.Windows.Forms.dll": {}}},
            "runtimepack.Microsoft.Windows.SDK.NET.Ref/10.0.26100.57": {"runtime": {"Microsoft.Windows.SDK.NET.dll": {}, "WinRT.Runtime.dll": {}}},
            "YamlDotNet/18.1.0": {"runtime": {"YamlDotNet.dll": {}}},
            "Microsoft.WindowsPackageManager.ComInterop/1.29.380": {},
        }
        target = ".NETCoreApp,Version=v10.0/win-x64"
        deps = dict(runtimeTarget=dict(name=target), libraries={name: {} for name in libraries}, targets={target: libraries})
        runtime_config = dict(runtimeOptions=dict(includedFrameworks=[dict(name=n, version=runtime) for n in ("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App")]))
        files = {n: folder / n for n in ("GerenciadorFinal.deps.json", "GerenciadorFinal.runtimeconfig.json", "BETA-BUILD.json")}
        def write(d=deps, r=runtime_config):
            files["GerenciadorFinal.deps.json"].write_text(json.dumps(d))
            files["GerenciadorFinal.runtimeconfig.json"].write_text(json.dumps(r))
        write()
        test("U4-GF-RECIPE-01", "Approved GF dependency validator accepts closed metadata model without building GF", lambda: require("GerenciadorFinal.dll" in module.declared_binaries(files, version), "GF_RECIPE"))
        def reject_mutation(d, r):
            write(d, r)
            try:
                module.declared_binaries(files, version)
            except ValueError:
                return
            raise AssertionError("GF recipe mutation accepted")
        for ident, description, d, r in (
            ("02", "Undeclared updater dependency is refused", copy.deepcopy(deps), runtime_config),
            ("03", "Host-dependent runtime is refused", deps, dict(runtimeOptions=dict(framework=dict(name="Microsoft.NETCore.App", version=runtime)))),
            ("04", "WinGet package version drift is refused", copy.deepcopy(deps), runtime_config),
        ):
            if ident == "02":
                d["libraries"]["unapproved-updater/1.0.0"] = {}
            if ident == "04":
                d["libraries"]["Microsoft.WindowsPackageManager.ComInterop/1.29.381"] = d["libraries"].pop("Microsoft.WindowsPackageManager.ComInterop/1.29.380")
            test("U4-GF-RECIPE-" + ident, description, lambda d=d, r=r: reject_mutation(d, r))
        metadata = dict(schema=1, version=version, source_commit="a" * 40, channel="BETA", transport_configured=False, endpoint="", notice_version="gf-beta-20261028-r1", notice_sha256="a" * 64, reviewed_notice_sha256="", distribution="requires_owner_approval")
        files["BETA-BUILD.json"].write_text(json.dumps(metadata))
        test("U4-GF-PROFILE-01", "Local beta profile is accepted only without active endpoint", lambda: module.verify_beta_metadata(files, version, "a" * 40))
        def endpoint():
            changed = dict(metadata, endpoint="https://fixture.invalid/ingest")
            files["BETA-BUILD.json"].write_text(json.dumps(changed))
            try:
                module.verify_beta_metadata(files, version, "a" * 40)
            except ValueError:
                return
            raise AssertionError("active endpoint accepted")
        test("U4-GF-PROFILE-02", "Endpoint in local metadata is refused", endpoint)
    observations.append(dict(kind="gf_recipe_scope", verifier_sha256=hashlib.sha256((repository / "scripts/verificar_pacote.py").read_bytes()).hexdigest(), original_verifier_unchanged=True, gf_binary_created=False, full_gf_package_validated=False, metadata_models_only=True))
    return dict(schema=1, stage=4, total=len(results), passed=sum(r["status"] == "passed" for r in results), failed=sum(r["status"] == "failed" for r in results), tests=results, observations=observations)
