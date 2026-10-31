"""Disposable Windows-only draft Release package preparation and verification.

This is a test harness, NOT part of the GF application. The test-only TUF
signing root is public; never use this pipeline to authorize production updates.
"""
from __future__ import annotations
import argparse
import hashlib
import zlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import sys
import zipfile

NAMES = (
    "GF-LAB-REAL-PACKAGE-MANIFEST.json",
    "targets.json", "snapshot.json", "timestamp.json",
)
ARCHIVE = "gf-e2e-new-win-x64.zip"
INSTALLATION = "a" * 32
TRANSACTION = "b" * 32
LIMIT = 650 * 1024 * 1024


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def phase1(repo):
    script = repo / "prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py"
    spec = importlib.util.spec_from_file_location("gf_phase1_draft_reuse", script)
    require(spec is not None and spec.loader is not None, "PHASE1_SOURCE_MISSING")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def guard():
    require(os.name == "nt" and os.environ.get("GITHUB_ACTIONS") == "true",
            "DISPOSABLE_WINDOWS_ACTIONS_REQUIRED")
    require(os.environ.get("GITHUB_REPOSITORY") == "walissonminel-1991/gerenciador-final",
            "WRONG_REPOSITORY")


def temp_path(path):
    root = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    target = Path(path).resolve(strict=False)
    require(target != root and target.is_relative_to(root), "NOT_RUNNER_TEMP")
    return target


def zip_package(module, package, result):
    paths = module.checked_files(package)
    require(len(paths) > 40, "NOT_REAL_GF")
    with zipfile.ZipFile(result, "x", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=6, allowZip64=True) as archive:
        for rel, real in paths:
            archive.write(real, arcname=rel)
    require(result.stat().st_size <= LIMIT, "ARCHIVE_TOO_LARGE")


def prepare(module, repo, old, new, target):
    require(not target.exists(), "PREPARE_TARGET_PREEXISTS")
    target.mkdir(parents=True)
    old_files = module.manifest_for(old)
    new_files = module.manifest_for(new)
    require(old_files != new_files and
            old_files["GerenciadorFinal.dll"]["sha256"] !=
            new_files["GerenciadorFinal.dll"]["sha256"], "OLD_NEW_SAME")
    target_meta = target / "signed"
    candidate = module.canonical_manifest(new_files, INSTALLATION, TRANSACTION, old_files)
    proof = module.tuf_sign_and_verify(repo, candidate, target_meta)
    module.tuf_verify_prepared(repo, candidate, target_meta)
    # Published binary is produced AFTER the TUF manifest and before upload.
    # Validation uses signed hashes of all extracted executable files.
    zip_package(module, new, target / ARCHIVE)
    output = target / "upload"
    output.mkdir()
    for name in NAMES:
        shutil.copyfile(target_meta / name, output / name)
    shutil.copyfile(target / ARCHIVE, output / ARCHIVE)
    print("GF_DRAFT_PREPARED " + json.dumps({
        "manifest_sha256": proof["manifest_sha256"],
        "archive_sha256": module.file_hash(target / ARCHIVE),
        "old_file_count": len(old_files), "new_file_count": len(new_files),
        "test_only": True, "operational_ready": False,
    }, sort_keys=True))


def safe_unpack(module, signed_files, path, destination):
    require(not destination.exists(), "EXTRACTION_DEST_PREEXISTS")
    destination.mkdir()
    seen = set()
    size = 0
    with zipfile.ZipFile(path, "r") as source:
        entries = source.infolist()
        require(len(entries) == len(signed_files) and 40 < len(entries) <= 1500,
                "ZIP_FILESET_COUNT")
        for item in entries:
            name = item.filename
            pos = PurePosixPath(name)
            require(not item.is_dir() and not name.startswith("/") and
                    "\\" not in name and ":" not in name and
                    not any(part in ("", ".", "..") for part in pos.parts) and
                    len(pos.parts) <= 15 and
                    name == pos.as_posix() and name.lower() not in seen and
                    name in signed_files, "ZIP_PATH_OR_EXTRA_FILE")
            seen.add(name.lower())
            unix_mode = (item.external_attr >> 16) & 0o170000
            require(unix_mode in (0, 0o100000), "ZIP_SYMLINK_OR_SPECIAL")
            length = signed_files[name]["length"]
            require(item.file_size == length and item.file_size <= LIMIT,
                    "ZIP_SIZE_OR_SIGNED_LENGTH")
            size += item.file_size
            require(size <= LIMIT, "ZIP_INFLATED_SIZE_LIMIT")
            out = destination.joinpath(*pos.parts)
            out.parent.mkdir(parents=True, exist_ok=True)
            with source.open(item) as stream, out.open("xb") as output:
                actual = hashlib.sha256()
                count = 0
                while True:
                    block = stream.read(1024 * 1024)
                    if not block:
                        break
                    count += len(block)
                    require(count <= length, "ZIP_CONTENT_OVERRUN")
                    actual.update(block)
                    output.write(block)
                output.flush()
                os.fsync(output.fileno())
                require(count == length and
                        actual.hexdigest() == signed_files[name]["sha256"],
                        "ZIP_SIGNED_FILE_SHA_MISMATCH")
    require(set(signed_files) == set(x.filename for x in entries),
            "ZIP_SIGNED_FILESET_MISMATCH")
    module.validate_package(destination, signed_files)


def verify(module, repo, old, downloaded, extracted, lab_old_variant=False):
    require(downloaded.is_dir() and old.is_dir(), "VERIFICATION_SOURCE_MISSING")
    for name in (*NAMES, ARCHIVE):
        require((downloaded / name).is_file(), "DRAFT_ASSET_MISSING_" + name)
    signed = (downloaded / NAMES[0]).read_bytes()
    proof = module.tuf_verify_prepared(repo, signed, downloaded)
    manifest = json.loads(signed)
    require(set(manifest) == {"schema", "old_files", "files",
                            "installation", "transaction", "selected_before",
                            "operational_installable"}, "MANIFEST_SCHEMA")
    require(manifest["schema"] == "gf-real-package-lab/3" and
            manifest["operational_installable"] is False and
            manifest["installation"] == INSTALLATION and
            manifest["transaction"] == TRANSACTION and
            manifest["selected_before"] == "old", "DRAFT_SCOPE_MISMATCH")
    require(signed == module.canonical_manifest(manifest["files"],
                                                INSTALLATION, TRANSACTION,
                                                manifest["old_files"]),
            "MANIFEST_NONCANONICAL")
    # An opt-in GF OLD detector build has an extra, test-only source module.
    # Its exact OLD bytes are bound separately by the EXISTING L18
    # PrepareRealAuthorization transaction, not by this historic Draft target.
    # The archived NEW remains authenticated by the already signed Draft TUF
    # target; never reinterpret the Draft's OLD hashes as authorizing this OLD.
    if lab_old_variant:
        current_old = module.manifest_for(old)
        require(current_old != manifest["old_files"] and
                current_old["GerenciadorFinal.dll"]["sha256"] !=
                manifest["old_files"]["GerenciadorFinal.dll"]["sha256"],
                "LAB_OLD_VARIANT_NOT_DISTINCT")
    else:
        module.validate_package(old, manifest["old_files"])
    safe_unpack(module, manifest["files"], downloaded / ARCHIVE, extracted)
    print("GF_DRAFT_AUTHENTICATED " + json.dumps({
        "source": "github_draft_release_assets",
        "tuf_manifest_sha256": proof["manifest_sha256"],
        "downloaded_archive_sha256": module.file_hash(downloaded / ARCHIVE),
        "extracted_files": len(manifest["files"]),
        "old_files_preserved": True, "lab_old_requires_l18_binding": lab_old_variant,
        "new_executable_on_disk_verified": True,
        "test_only": True, "operational_ready": False,
    }, sort_keys=True))
    return manifest


def negative(module, repo, old, downloaded, base, lab_old_variant=False):
    # Both negatives use the exact downloaded asset bytes, NEVER another fake
    # executable. A partial copy represents an interrupted transfer.
    require(not base.exists(), "NEGATIVE_BASE_PREEXISTS")
    base.mkdir()
    failures = []
    binary = (downloaded / ARCHIVE).read_bytes()
    require(len(binary) > 10000, "REAL_ARCHIVE_TOO_SMALL")
    for mode in ("modified", "interrupted"):
        directory = base / mode
        directory.mkdir()
        for name in NAMES:
            shutil.copyfile(downloaded / name, directory / name)
        if mode == "modified":
            data = bytearray(binary)
            # Pick data inside a regular compressed file (not ZIP directory).
            with zipfile.ZipFile(downloaded / ARCHIVE) as z:
                entry = next(x for x in z.infolist() if x.compress_size > 64)
            offset = entry.header_offset + 30 + len(entry.filename.encode("utf-8")) + len(entry.extra) + 8
            require(offset < len(data), "MODIFICATION_OFFSET")
            data[offset] ^= 0x40
            (directory / ARCHIVE).write_bytes(data)
        else:
            (directory / ARCHIVE).write_bytes(binary[:len(binary) // 2])
        try:
            verify(module, repo, old, directory, base / (mode + "-extracted"),
                   lab_old_variant=lab_old_variant)
        except (RuntimeError, ValueError, KeyError, OSError, zipfile.BadZipFile,
                zipfile.LargeZipFile, EOFError, zlib.error) as exc:
            failures.append({"case": mode, "rejected": True,
                             "reason": type(exc).__name__})
        else:
            raise RuntimeError("FALSE_ACCEPT_" + mode)
    require(len(failures) == 2, "NEGATIVE_MATRIX")
    print("GF_DRAFT_NEGATIVES " + json.dumps({
        "tests": failures, "downloaded_asset_executed_on_negative": False,
        "rollback_authorized": False, "operational_ready": False,
    }, sort_keys=True))



def refresh_test_only_roles(module, repo, source, dest):
    """Refresh ONLY test TUF role metadata for the same pinned Draft manifest.

    The manifest and executable are immutable in this operation. These keys
    are public testing keys; version-one metadata is fresh-runner-only and
    must never be used as a production trust update.
    """
    require(source.is_file() and not dest.exists(), "REFRESH_SOURCE_OR_TARGET")
    data = source.read_bytes()
    require(module.digest(data) == "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d", "REFRESH_PINNED_MANIFEST_SHA")
    document = json.loads(data)
    require(document["schema"] == "gf-real-package-lab/3" and
            document["operational_installable"] is False and
            document["selected_before"] == "old" and
            document["installation"] == INSTALLATION and
            document["transaction"] == TRANSACTION and
            data == module.canonical_manifest(document["files"],
                                               INSTALLATION, TRANSACTION,
                                               document["old_files"]),
            "REFRESH_SIGNED_MANIFEST_SCOPE")
    result = module.tuf_sign_and_verify(repo, data, dest)
    module.tuf_verify_prepared(repo, data, dest)
    require(result["manifest_sha256"] == "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d" and
            (dest / NAMES[0]).read_bytes() == data,
            "REFRESH_CHANGED_TARGET")
    print("GF_DRAFT_TUF_REFRESHED " + json.dumps({
        "manifest_sha256": result["manifest_sha256"],
        "roles": ["targets.json", "snapshot.json", "timestamp.json"],
        "test_only": True, "production_tuf": False,
        "release_was_published": False, "package_modified": False
    }, sort_keys=True))

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["prepare", "verify", "negative", "refresh"])
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--old", type=Path, required=True)
    parser.add_argument("--new", type=Path)
    parser.add_argument("--target", type=Path)
    parser.add_argument("--downloaded", type=Path)
    parser.add_argument("--extracted", type=Path)
    parser.add_argument("--manifest-file", type=Path)
    parser.add_argument("--lab-old-variant", action="store_true")
    args = parser.parse_args()
    guard()
    repo = args.repo.resolve(strict=True)
    old = temp_path(args.old)
    module = phase1(repo)
    if args.mode == "refresh":
        require(args.manifest_file is not None and args.target is not None,
                "REFRESH_ARGS")
        refresh_test_only_roles(module, repo, temp_path(args.manifest_file),
                                temp_path(args.target))
    elif args.mode == "prepare":
        require(args.new is not None and args.target is not None, "PREPARE_ARGS")
        prepare(module, repo, old, temp_path(args.new), temp_path(args.target))
    elif args.mode == "verify":
        require(args.downloaded is not None and args.extracted is not None, "VERIFY_ARGS")
        verify(module, repo, old, temp_path(args.downloaded), temp_path(args.extracted),
               lab_old_variant=args.lab_old_variant)
    else:
        require(args.downloaded is not None and args.target is not None, "NEGATIVE_ARGS")
        negative(module, repo, old, temp_path(args.downloaded), temp_path(args.target),
                 lab_old_variant=args.lab_old_variant)


if __name__ == "__main__":
    main()
