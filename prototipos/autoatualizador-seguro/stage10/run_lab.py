"""Disposable Windows-only GF byte qualification; no product execution or binary upload."""
import argparse
import copy
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ENTRY = "4c1810a9d49bdb5fa1c6914354b32e5cb4602af9"
VERSION = "5.13.11.66"


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def digest(path):
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def main(report, phase, cases):
    report = report.resolve()
    result = {"schema": "gf-stage10-controller/1", "contract": "gf-self-update/1", "entry_head": ENTRY,
              "status": "not-executed", "phase": phase, "failed": 0, "results": [], "product_executed": False,
              "candidate_uploaded": False, "production_keys_created": False, "old_suites_repeated": False,
              "private_channel_changed": False, "stage11_started": False}
    work_path = None
    data_path = None
    def save():
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    def case(name, action, expected_rejection=False):
        try:
            data = action()
        except (ValueError, KeyError, OSError, TypeError) as e:
            if expected_rejection:
                result["results"].append({"id": name, "status": "passed", "rejection": str(e)}); return
            result["results"].append({"id": name, "status": "failed", "error": type(e).__name__ + ":" + str(e)})
            result["failed"] += 1; return
        if expected_rejection:
            result["results"].append({"id": name, "status": "failed", "error": "adversarial package accepted"}); result["failed"] += 1
        else:
            result["results"].append({"id": name, "status": "passed", "detail": data})
    try:
        if os.name != "nt":
            result["status"] = "blocked"; result["blocker"] = "requires disposable Windows/NTFS; Linux does not qualify GF bytes"; return 2
        dotnet = shutil.which("dotnet")
        if not dotnet:
            result["status"] = "blocked"; result["blocker"] = "SDK 10.0.402 unavailable"; return 2
        env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "PYTHONUTF8": "1"}
        def run(cmd, **kw):
            return subprocess.run(cmd, cwd=REPO, env=env, check=True, **kw)
        def output(cmd):
            return subprocess.check_output(cmd, cwd=REPO, env=env, text=True).strip()
        sdk = output([dotnet, "--version"]); assert sdk == "10.0.402", sdk
        head = output(["git", "rev-parse", "HEAD"])
        # Product recipe/source/notice inputs must be identical to the mandatory base.
        changed = output(["git", "diff", "--name-only", ENTRY, "HEAD"]).splitlines()
        allowed = ("prototipos/autoatualizador-seguro/stage10/", ".github/workflows/autoatualizador-etapa10-laboratorio.yml", "docs/WORK-AUTOATUALIZADOR-ETAPA10", "docs/evidencias/autoatualizador-etapa10", "docs/WORK-AUTOATUALIZADOR-CHECKPOINT.md", "docs/PROGRESSO.json")
        assert all(p.startswith(allowed) for p in changed), changed
        with tempfile.TemporaryDirectory(prefix="gf-u2-u10-") as directory:
            work = Path(directory).resolve(); work_path = work
            assert not work.is_relative_to(REPO)
            (work / ".gf-u2-fixture").write_text("GF-U2-INERT-STATE-TESTS-NO-PRODUCT\n", encoding="utf-8", newline="\n")
            prepare = module(HERE.parent / "stage8/prepare_source.py", "u10_prepare")
            source, provenance = prepare.prepare(work)
            common = ["-p:ArtifactsPath=" + str(work / "tool-build"), "-p:Stage8Source=" + str(source)]
            run([dotnet, "restore", str(HERE / "Stage10.csproj"), *common, "--locked-mode", "--nologo"])
            run([dotnet, "build", str(HERE / "Stage10.csproj"), *common, "-c", "Release", "--no-restore", "--nologo"])
            binaries = list((work / "tool-build/bin").rglob("GF.Updater.Stage10.Lab.dll")); assert len(binaries) == 1
            result["tool_build"] = {"status": "passed", "sdk": sdk, "tuf_source": provenance, "locked_restore": True}
            sandbox_receipt = work / "sandbox-path.txt"
            run([dotnet, str(binaries[0]), "--create-sandbox", str(sandbox_receipt)])
            data_path = Path(sandbox_receipt.read_text()).resolve()
            assert data_path.parent == Path(tempfile.gettempdir()).resolve() and data_path.name.startswith("gf-u2-u10-data-")
            @contextmanager
            def data_scope():
                try: yield data_path
                finally: shutil.rmtree(data_path)
            with data_scope() as data_root:
                product = data_root / "package"
                acl_receipt = work / "acl.json"
                run([dotnet, str(binaries[0]), "--acl-report", str(acl_receipt), str(work), str(data_root)])
                result["acl_context"] = json.loads(acl_receipt.read_text())
                evaluated = json.loads(output([dotnet, "msbuild", "GerenciadorAplicativos.csproj", "-getItem:Compile"]))
                identities = [p["Identity"].replace("\\", "/") for p in evaluated["Items"]["Compile"]]
                assert len(identities) == 142 and not any("prototipos/" in p for p in identities)
                run([dotnet, "publish", "GerenciadorAplicativos.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
                     "-p:PublishSingleFile=false", "-p:DebugType=None", "-p:DebugSymbols=false", "-p:GfBetaProfile=local", "-p:GfSourceCommit=" + ENTRY,
                     "--artifacts-path", str(work / "product-build"), "-o", str(product), "--nologo"])
                result["product_build"] = {"status": "passed", "checkout_head": head, "functional_source_commit": ENTRY, "product_version": VERSION,
                                           "profile": "local", "compile_items": len(identities), "prototype_compile_items": 0, "outputs_outside_repo": True}
                run([dotnet, str(binaries[0]), "--metadata", str(product / "GerenciadorFinal.dll"), ENTRY, str(product / "BETA-BUILD.json")])
                (product / "BUILD-INFO.txt").write_text("\n".join([
                    "Produto: Gerenciador Final", "Uso: LABORATORIO DESCARTAVEL; NAO DISTRIBUIR; NAO EXECUTAR",
                    f"Versao: {VERSION}", f"Commit Git: {head}", f"Commit fonte: {ENTRY}", "Base da PR (quando aplicavel): ",
                    f"Execucao GitHub Actions: {os.environ.get('GITHUB_RUN_ID', '')}", "Perfil BETA: local; receptor remoto ausente",
                    "Assinatura digital: inspecionar no recibo; nenhuma autoria operacional atribuida"]) + "\n", encoding="utf-8")
                def sums():
                    (product / "SHA256SUMS.txt").write_text("".join(f"{digest(p)}  {p.relative_to(product).as_posix()}\n" for p in sorted(product.rglob("*")) if p.is_file() and p.name != "SHA256SUMS.txt"), encoding="utf-8")
                sums()
                verifier = module(REPO / "scripts/verificar_pacote.py", "u10_existing_verifier")
                def verify(): return verifier.verify(product, VERSION, ENTRY, head, beta=True)
                if phase == "full": case("U10-APPROVED-PACKAGE-VERIFIER", verify)
                else: result["new_build_preflight"] = verify()
                recipe = {p.relative_to(product).as_posix(): digest(p) for p in sorted(product.rglob("*")) if p.is_file()}
                if phase == "full":
                    # Every mutation is restored before the next independent check. Never mutates source or old .66 ZIP.
                    def mutate(name, change):
                        path = product / name; before = path.read_bytes() if path.exists() else None
                        try: change(path); return verify()
                        finally:
                            if before is None: path.unlink(missing_ok=True)
                            else: path.write_bytes(before)
                    case("U10-TAMPERED-DLL", lambda: mutate("GerenciadorFinal.dll", lambda p: p.write_bytes(p.read_bytes() + b"tamper")), True)
                    case("U10-MISSING-DEPENDENCY", lambda: mutate("hostfxr.dll", lambda p: p.unlink()), True)
                    case("U10-UNEXPECTED-FILE", lambda: mutate("unexpected.dll", lambda p: p.write_bytes((product / "GerenciadorFinal.dll").read_bytes())), True)
                    case("U10-INCONSISTENT-MANIFEST", lambda: mutate("SHA256SUMS.txt", lambda p: p.write_text(p.read_text().splitlines()[0] + "\n")), True)
                    case("U10-INCOMPATIBLE-VERSION", lambda: verifier.verify(product, "5.13.11.65", ENTRY, head, beta=True), True)
                    # Also reject a missing dependency after a maliciously regenerated checksum manifest.
                    old_sums = (product / "SHA256SUMS.txt").read_bytes(); dependency = product / "hostpolicy.dll"; old_dependency = dependency.read_bytes()
                    try:
                        dependency.unlink(); sums(); case("U10-MISSING-DEPENDENCY-REHASHED", verify, True)
                    finally: dependency.write_bytes(old_dependency); (product / "SHA256SUMS.txt").write_bytes(old_sums)
                    def scan():
                        patterns = [rb"(?:gh[pousr]_[A-Za-z0-9]{36,255}|github_pat_[A-Za-z0-9_]{50,255})", rb"-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----[\r\n]+[A-Za-z0-9+/=\r\n]{128,}-----END"]
                        hits = []
                        for p in product.rglob("*"):
                            if p.is_file():
                                data = p.read_bytes()
                                if any(re.search(pattern, candidate) for pattern in patterns for candidate in (data, data.replace(b"\x00", b""))): hits.append(p.name)
                        if hits: raise ValueError("high-confidence secret marker; matching contents withheld")
                        return {"scanned_files": len(recipe), "findings": 0, "scope": "bounded GitHub-token/private-key patterns; not proof of absence of all secrets"}
                    case("U10-SECRET-SCAN", scan)
                archive = data_root / "gf-lab.zip"
                with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
                    for name in recipe:
                        info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0)); info.compress_type = zipfile.ZIP_DEFLATED; info.create_system = 3; info.external_attr = 0o100600 << 16
                        z.writestr(info, (product / name).read_bytes())
                result["container"] = {"sha256": digest(archive), "bytes": archive.stat().st_size, "file_count": len(recipe),
                                       "expanded_bytes": sum((product / n).stat().st_size for n in recipe), "local_recipe_only": True, "tuf_authorized": False}
                recipe_file = work / "recipe.json"; recipe_file.write_text(json.dumps(recipe))
                fixtures = work / "fixtures"
                signer = module(HERE.parent / "generate_fixtures.py", "u10_public_signer")
                fixture_result = module(HERE.parent / "stage9/make_fixtures.py", "u10_inert_fixtures").generate(fixtures)
                expires = signer.utc(datetime.now(timezone.utc) + timedelta(days=10))
                for role in ("timestamp", "snapshot", "targets"):
                    root = signer.root(2, ["r1", "r2", "r3"], expires, ["r1", "r2"])["signed"]
                    del root["keys"][signer.IDS[role]]
                    root["keys"][signer.IDS["intruder"]] = copy.deepcopy(signer.KEYS["intruder"])
                    root["roles"][role]["keyids"] = [signer.IDS["intruder"]]
                    folder = fixtures / ("epoch-" + role); folder.mkdir()
                    (folder / "2.root.json").write_bytes(signer.wire(signer.envelope(root, ["r1", "r2"])))
                result["tuf_fixtures"] = {**fixture_result, "real_GF_bytes_signed": False, "new_epoch_fixtures": 3, "public_preexisting_seeds_only": True}
                static_report = work / "static-report.json"
                completed = subprocess.run([dotnet, str(binaries[0]), str(data_root), str(product), str(archive), str(recipe_file), str(fixtures), str(static_report), cases], cwd=REPO, env=env)
                if static_report.is_file():
                    result["windows"] = json.loads(static_report.read_text()); result["failed"] += result["windows"]["failed"]
                if completed.returncode and not result.get("windows", {}).get("failed"): raise RuntimeError("Static harness failed before complete report")
                assets = list((work / "product-build").rglob("project.assets.json")); assert len(assets) == 1
                libraries = json.loads(assets[0].read_text())["libraries"]
                result["resolved_packages"] = {k: {"type": v["type"], "sha512": v.get("sha512")} for k, v in libraries.items()}
                result["status"] = "passed" if result["failed"] == 0 else "failed"
    except Exception as e:
        result["status"] = "failed"; result["failed"] += 1; result["controller_error"] = type(e).__name__ + ":" + str(e)
        raise
    finally:
        result["lab_tree_deleted"] = work_path is not None and not work_path.exists() and (data_path is None or not data_path.exists())
        result["acl_modified"] = False
        result["sandbox_creation"] = "DotNET Directory.CreateDirectory inherited ACL; no chmod/SetAccessControl"
        save()
        print("GF_U10_FINAL_REPORT " + json.dumps(result, ensure_ascii=True, separators=(",", ":")), flush=True)
    return 0 if result["status"] == "passed" else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--phase", choices=("full", "windows"), default="full")
    parser.add_argument("--cases", default="")
    args = parser.parse_args()
    raise SystemExit(main(args.report, args.phase, args.cases))
