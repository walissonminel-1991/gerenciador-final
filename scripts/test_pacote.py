"""Adversarial package fixtures: no installer is executed and no personal data is used."""
import hashlib
import json
from pathlib import Path
import struct
import tempfile
import unittest

from verificar_pacote import REQUIRED, verify, verify_metadata, portable_name
import shutil

VERSION, SOURCE, BUILD, BASE = "5.13.11.49", "a" * 40, "b" * 40, "c" * 40


class PacoteTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="GF-Package-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for name in REQUIRED:
            file = self.root / name
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b"synthetic fixture")
        header = bytearray(134)
        header[:2] = b"MZ"
        struct.pack_into("<I", header, 60, 128)
        header[128:] = b"PE\0\0\x64\x86"
        (self.root / "GerenciadorFinal.exe").write_bytes(header)
        self.info = f"Versao: {VERSION}\nCommit fonte: {SOURCE}\nCommit Git: {BUILD}\nBase da PR (quando aplicavel): {BASE}\n"
        (self.root / "BUILD-INFO.txt").write_text(self.info, encoding="utf-8")
        self.options = {"runtimeOptions": {"includedFrameworks": [
            {"name": name, "version": "10.0.13"}
            for name in ("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App")]}}
        (self.root / "GerenciadorFinal.runtimeconfig.json").write_text(json.dumps(self.options))
        libraries = {
            "GerenciadorFinal/" + VERSION: {"runtime": {"GerenciadorFinal.dll": {}}},
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.13": {"native": {"coreclr.dll": {}, "hostfxr.dll": {}}},
            "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.13": {"runtime": {"System.Windows.Forms.dll": {}}},
            "runtimepack.Microsoft.Windows.SDK.NET.Ref/10.0.26100.57": {"runtime": {"Microsoft.Windows.SDK.NET.dll": {}, "WinRT.Runtime.dll": {}}},
            "YamlDotNet/18.1.0": {}
        }
        target = ".NETCoreApp,Version=v10.0/win-x64"
        self.deps = {"runtimeTarget": {"name": target}, "targets": {target: libraries}, "libraries": {key: {} for key in libraries}}
        (self.root / "GerenciadorFinal.deps.json").write_text(json.dumps(self.deps))
        self.hashes()

    def hashes(self):
        lines = [hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.relative_to(self.root).as_posix()
                 for p in sorted(self.root.rglob("*")) if p.is_file() and p.name != "SHA256SUMS.txt"]
        (self.root / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    def check(self):
        return verify(self.root, VERSION, SOURCE, BUILD, BASE)

    def test_complete_package(self):
        self.assertEqual(self.check()["verified_hashes"], len(REQUIRED))

    def test_localized_runtime_resources(self):
        path = self.root / "pt-BR/System.Windows.Forms.resources.dll"
        path.parent.mkdir()
        path.write_bytes(b"synthetic localized resource")
        self.hashes()
        self.assertEqual(self.check()["verified_hashes"], len(REQUIRED) + 1)

    def test_documentation_links_resolve_in_the_package(self):
        file = self.root / "PRIVACY.md"
        for link in ("docs/absent.md", "../outside.md"):
            with self.subTest(link=link):
                file.write_text(f"[More information]({link})", encoding="utf-8")
                self.hashes()
                with self.assertRaisesRegex(ValueError, "link interno"):
                    self.check()

    def test_missing_notices(self):
        for name in ("LICENSE.txt", "Licencas/DotNET/THIRD-PARTY-NOTICES.txt"):
            with self.subTest(name=name):
                path = self.root / name
                original = path.read_bytes()
                path.unlink()
                with self.assertRaisesRegex(ValueError, "obrigatorios ausentes"):
                    self.check()
                path.write_bytes(original)

    def test_corrupt_download(self):
        (self.root / "GerenciadorFinal.dll").write_bytes(b"corruption")
        with self.assertRaisesRegex(ValueError, "hash divergente"):
            self.check()

    def test_unlisted_and_missing_files(self):
        extra = self.root / "unexpected.dll"
        extra.write_bytes(b"stale previous build")
        with self.assertRaisesRegex(ValueError, "exatamente"):
            self.check()
        self.hashes()
        extra.unlink()
        with self.assertRaisesRegex(ValueError, "exatamente"):
            self.check()

    def test_development_file_even_if_hashed(self):
        (self.root / "credentials.pfx").write_bytes(b"synthetic non-secret")
        self.hashes()
        with self.assertRaisesRegex(ValueError, "inesperado"):
            self.check()

    def test_traversal_and_duplicate_checksums(self):
        manifest = self.root / "SHA256SUMS.txt"
        original = manifest.read_text()
        for line in ("0" * 64 + "  ../outside.dll\n", original.splitlines()[0] + "\n"):
            with self.subTest(line=line):
                manifest.write_text(original + line)
                with self.assertRaisesRegex(ValueError, "invalida ou duplicada"):
                    self.check()

    def test_extra_binary_even_if_hashed(self):
        for name in ("GF-Inert-Test-Fixture.dll", "GF-Inert-Test-Fixture.exe"):
            with self.subTest(name=name):
                path = self.root / name
                path.write_bytes(b"inert fixture, not executable")
                self.hashes()
                with self.assertRaisesRegex(ValueError, "declarado|fixture"):
                    self.check()
                path.unlink()

    def test_windows_reserved_names(self):
        for name in ("CON.dll", "aux.txt", "Licencas/NUL", "COM1.exe", "lpt9.dll", "COM¹.dll"):
            with self.subTest(name=name): self.assertFalse(portable_name(name))

    def test_wrong_architecture(self):
        file = self.root / "GerenciadorFinal.exe"
        file.write_bytes(file.read_bytes()[:-2] + b"\x4c\x01")
        self.hashes()
        with self.assertRaisesRegex(ValueError, "AMD64"):
            self.check()

    def test_wrong_or_duplicate_provenance(self):
        file = self.root / "BUILD-INFO.txt"
        for info in (self.info.replace(SOURCE, BUILD), self.info + f"Commit fonte: {SOURCE}\n",
                     self.info.replace(VERSION, "5.13.11.48")):
            with self.subTest(info=info):
                file.write_text(info)
                self.hashes()
                with self.assertRaisesRegex(ValueError, "origem/versao"):
                    self.check()

    def test_framework_dependent_rejected(self):
        self.options["runtimeOptions"]["frameworks"] = self.options["runtimeOptions"].pop("includedFrameworks")
        (self.root / "GerenciadorFinal.runtimeconfig.json").write_text(json.dumps(self.options))
        self.hashes()
        with self.assertRaisesRegex(ValueError, "self-contained"):
            self.check()


class MetadataTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="GF-Metadata-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        repo = Path(__file__).resolve().parents[1]
        for name in ["GerenciadorAplicativos.csproj", "app.manifest", "README.md", "docs/ROADMAP.md", "CHANGELOG.md", "docs/PROGRESSO.json", "docs/MISSAO-WORK13.md"]:
            file = self.root / name
            file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(repo / name, file)

    def test_repository_consistent(self):
        verify_metadata(self.root)

    def test_contradictory_candidate(self):
        for name in ["README.md", "docs/ROADMAP.md", "CHANGELOG.md", "app.manifest"]:
            with self.subTest(name=name):
                path = self.root / name
                original = path.read_text()
                version = verify_metadata(self.root)["candidate"].split()[0]
                path.write_text(original.replace(version, "0.0.0.1"))
                with self.assertRaises(ValueError): verify_metadata(self.root)
                path.write_text(original)

    def test_wrong_approved_artifact(self):
        path = self.root / "docs/PROGRESSO.json"
        original = path.read_text()
        for key, value in [("source_commit", "0" * 40), ("name", "another-package"), ("url", "https://example.com/installer"), ("zip_sha256", "invalid")]:
            with self.subTest(key=key):
                data = json.loads(original); data["artifact"][key] = value
                path.write_text(json.dumps(data))
                with self.assertRaises(ValueError): verify_metadata(self.root)
        path.write_text(original)

    def test_wrong_ci_link_in_documentation(self):
        data = json.loads((self.root / "docs/PROGRESSO.json").read_text())
        for name in ["README.md", "docs/ROADMAP.md"]:
            with self.subTest(name=name):
                path = self.root / name; original = path.read_text()
                path.write_text(original.replace(data["ci_url"], "https://github.com/walissonminel-1991/gerenciador-final/actions/runs/1"))
                with self.assertRaisesRegex(ValueError, "documentacao"): verify_metadata(self.root)
                path.write_text(original)

    def test_physical_checkpoints_remain_open(self):
        path = self.root / "docs/PROGRESSO.json"; original = path.read_text()
        for target in ("U8", "R7", "R4"):
            data = json.loads(original)
            next(t for section in data["sections"] for t in section["tasks"] if t["id"] == target)["done"] = True
            path.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "homologacao fisica"): verify_metadata(self.root)

    def test_winget_real_not_enabled(self):
        path = self.root / "docs/PROGRESSO.json"; data = json.loads(path.read_text())
        data["candidate"]["winget_execution_enabled"] = True; path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "contratos"): verify_metadata(self.root)


if __name__ == "__main__":
    unittest.main()
