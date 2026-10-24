"""Verify a published directory without executing it; Python standard library only."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import struct
from urllib.parse import unquote, urlsplit


REQUIRED = frozenset({
    "GerenciadorFinal.exe", "GerenciadorFinal.dll", "GerenciadorFinal.deps.json",
    "GerenciadorFinal.runtimeconfig.json", "coreclr.dll", "hostfxr.dll",
    "System.Windows.Forms.dll", "Microsoft.Windows.SDK.NET.dll", "WinRT.Runtime.dll",
    "BUILD-INFO.txt", "LICENSE.txt", "LEIA-ME.md", "PRIVACY.md",
    "THIRD-PARTY-NOTICES.md", "Licencas/DotNET/LICENSE.txt",
    "Licencas/DotNET/THIRD-PARTY-NOTICES.txt", "Licencas/WindowsDesktop/LICENSE.txt",
})


def portable_name(name: str) -> bool:
    parts = name.split("/")
    return bool(name) and all(
        part not in {"", ".", ".."} and not part.endswith((" ", "."))
        and not re.search(r'[\\:\x00-\x1f<>"|?*]', part)
        for part in parts
    ) and not PurePosixPath(name).is_absolute()


def verify(root: Path, version: str, source: str, build: str, base: str = "") -> dict:
    if not re.fullmatch(r"\d+(?:\.\d+){3}", version):
        raise ValueError("versao esperada invalida")
    if any(not re.fullmatch(r"[a-f0-9]{40}", sha) for sha in (source, build)):
        raise ValueError("SHA esperado invalido")
    if base and not re.fullmatch(r"[a-f0-9]{40}", base):
        raise ValueError("SHA base invalido")
    if not root.is_dir() or root.is_symlink():
        raise ValueError("diretorio de distribuicao invalido")
    files = {}
    folded = set()
    for path in root.rglob("*"):
        name = path.relative_to(root).as_posix()
        if path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction()):
            raise ValueError("link no pacote: " + name)
        if not portable_name(name) or name.casefold() in folded:
            raise ValueError("nome ambiguo no pacote: " + name)
        folded.add(name.casefold())
        if path.is_dir():
            continue
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError("arquivo vazio ou especial: " + name)
        if name.startswith(".") or any(part.startswith(".") for part in path.relative_to(root).parts):
            raise ValueError("arquivo oculto inesperado: " + name)
        # SDK output only: runtime binaries plus the explicit notices/manifests.
        if name not in REQUIRED | {"SHA256SUMS.txt"} and not (
            ("/" not in name and path.suffix.lower() in {".dll", ".exe"})
            or re.fullmatch(r"[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*/[A-Za-z0-9_.]+\.resources\.dll", name)
        ):
            raise ValueError("arquivo inesperado: " + name)
        files[name] = path
    missing = REQUIRED - files.keys()
    if missing:
        raise ValueError("arquivos obrigatorios ausentes: " + ", ".join(sorted(missing)))
    if "SHA256SUMS.txt" not in files:
        raise ValueError("manifesto de integridade ausente")
    checksums = {}
    for line in files["SHA256SUMS.txt"].read_text(encoding="utf-8-sig").splitlines():
        match = re.fullmatch(r"([a-f0-9]{64})  (.+)", line)
        if not match or not portable_name(match[2]) or match[2] in checksums:
            raise ValueError("entrada invalida ou duplicada no manifesto")
        checksums[match[2]] = match[1]
    if checksums.keys() != files.keys() - {"SHA256SUMS.txt"}:
        raise ValueError("manifesto nao cobre exatamente todos os arquivos")
    for name, digest in checksums.items():
        with files[name].open("rb") as stream:
            if hashlib.file_digest(stream, "sha256").hexdigest() != digest:
                raise ValueError("hash divergente: " + name)
    for name, path in files.items():
        if path.suffix.lower() != ".md":
            continue
        for link in re.findall(r'!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)',
                               path.read_text(encoding="utf-8-sig")):
            target = urlsplit(link.strip("<>"))
            if target.scheme or target.netloc or not target.path:
                continue
            resolved = (path.parent / unquote(target.path)).resolve()
            if not resolved.is_relative_to(root.resolve()) or not resolved.is_file():
                raise ValueError("link interno ausente ou externo ao pacote: " + name)
    info = files["BUILD-INFO.txt"].read_text(encoding="utf-8-sig").splitlines()
    for line in (f"Versao: {version}", f"Commit fonte: {source}", f"Commit Git: {build}",
                 f"Base da PR (quando aplicavel): {base}"):
        key = line.split(":", 1)[0] + ":"
        if [entry for entry in info if entry.startswith(key)] != [line]:
            raise ValueError("origem/versao divergente: " + key)
    with files["GerenciadorFinal.exe"].open("rb") as stream:
        header = stream.read(64)
        if len(header) != 64 or header[:2] != b"MZ":
            raise ValueError("executavel sem cabecalho PE")
        offset = struct.unpack_from("<I", header, 60)[0]
        if offset < 64 or offset > files["GerenciadorFinal.exe"].stat().st_size - 6:
            raise ValueError("offset PE invalido")
        stream.seek(offset)
        if stream.read(6) != b"PE\0\0\x64\x86":
            raise ValueError("executavel nao e PE AMD64")
    options = json.loads(files["GerenciadorFinal.runtimeconfig.json"].read_text(encoding="utf-8"))["runtimeOptions"]
    frameworks = options.get("includedFrameworks", [])
    if options.get("framework") or options.get("frameworks") or not {
        "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"
    }.issubset({f["name"] for f in frameworks}):
        raise ValueError("distribuicao nao e self-contained WinForms")
    return {"files": len(files), "verified_hashes": len(checksums),
            "expanded_bytes": sum(p.stat().st_size for p in files.values()),
            "version": version, "source": source, "build": build,
            "runtime": frameworks, "architecture": "AMD64"}


def verify_metadata(root: Path) -> dict:
    import xml.etree.ElementTree as ET
    project = ET.parse(root / "GerenciadorAplicativos.csproj").getroot().findtext("PropertyGroup/Version")
    if not project or not re.fullmatch(r"\d+(?:\.\d+){3}", project):
        raise ValueError("versao de projeto invalida")
    manifest = ET.parse(root / "app.manifest").getroot().find("{urn:schemas-microsoft-com:asm.v1}assemblyIdentity")
    if manifest is None or manifest.get("version") != project:
        raise ValueError("manifesto e projeto divergentes")
    progress = json.loads((root / "docs/PROGRESSO.json").read_text(encoding="utf-8"))
    candidate = progress["candidate"]
    if candidate["version"] != project + " TESTE" or candidate["status"] not in {"pending", "approved"}:
        raise ValueError("candidata divergente ou estado invalido")
    readme = (root / "README.md").read_text(encoding="utf-8")
    roadmap = (root / "docs/ROADMAP.md").read_text(encoding="utf-8")
    changelog = (root / "CHANGELOG.md").read_text(encoding="utf-8")
    for text, pattern, label in [
        (readme, r"Candidata TESTE: v(\d+(?:\.\d+){3})", "README"),
        (roadmap, r"\*\*v(\d+(?:\.\d+){3}) TESTE", "ROADMAP"),
        (changelog, r"^## (?:v)?(\d+(?:\.\d+){3}) TESTE", "CHANGELOG"),
    ]:
        match = re.search(pattern, text, re.MULTILINE)
        if match is None or match[1] != project:
            raise ValueError(label + " anuncia outra candidata")
    approved = progress["version"].removesuffix(" TESTE")
    if progress["ci_status"] != "success" or tuple(map(int, approved.split('.'))) > tuple(map(int, project.split('.'))):
        raise ValueError("aprovacao invalida")
    artifact = progress["artifact"]
    source = progress["code_commit"]
    if not re.fullmatch(r"[0-9a-f]{40}", source) or artifact["source_commit"] != source or artifact["file_version"] != approved:
        raise ValueError("origem/versao do pacote aprovado divergente")
    name = "GerenciadorFinal-v" + approved + "-" + source[:8] + "-TESTE-win-x64"
    run = progress["ci_url"]
    if not re.fullmatch(r"https://github.com/walissonminel-1991/gerenciador-final/actions/runs/\d+", run):
        raise ValueError("CI sem origem aprovada")
    readme_current = readme[readme.index("Candidata TESTE:"):].split("\n\n", 1)[0]
    roadmap_current = roadmap[roadmap.index("**v"):].split("\n\n", 1)[0]
    if run not in readme_current or run not in roadmap_current or (candidate["status"] == "approved" and artifact["url"] not in readme_current):
        raise ValueError("documentacao aponta para CI/pacote divergente")
    if progress["build_url"] != run or progress["policy_url"] != run or artifact["url"] != run + "/artifacts/" + str(artifact["id"]) or progress["artifact_url"] != artifact["url"]:
        raise ValueError("referencias de CI/artefato divergentes")
    if artifact["name"] != name or progress["artifact_name"] != name or not re.fullmatch(r"[0-9a-f]{64}", artifact["zip_sha256"]):
        raise ValueError("identidade/hash do pacote divergente")
    if candidate["status"] == "approved" and approved != project:
        raise ValueError("candidata declarada aprovada sem pacote correspondente")
    tasks = {t["id"]: t for section in progress["sections"] for t in section["tasks"]}
    for checkpoint in ("U8", "R7", "R4"):
        if tasks[checkpoint]["done"]:
            raise ValueError("homologacao fisica " + checkpoint + " requer evidencia e revisao explicita desta trava")
    if candidate["winget_execution_enabled"] or candidate["physical_validation"] != "pending" or not (root / candidate["report"]).is_file():
        raise ValueError("contratos da candidata divergentes")
    return {"candidate": candidate["version"], "status": candidate["status"], "approved": progress["version"], "source": source}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--metadata", action="store_true")
    parser.add_argument("--version")
    parser.add_argument("--source")
    parser.add_argument("--build")
    parser.add_argument("--base", default="")
    args = parser.parse_args()
    try:
        if args.metadata:
            result = verify_metadata(args.directory)
        else:
            if not all((args.version, args.source, args.build)):
                parser.error("pacote exige --version, --source e --build")
            result = verify(args.directory, args.version, args.source, args.build, args.base)
        print(json.dumps(result, indent=2))
    except (ValueError, KeyError, OSError, TypeError) as error:
        parser.exit(1, "Pacote reprovado: " + str(error) + "\n")
