#!/usr/bin/env python3
"""Mesmo probe consultivo em dois snapshots; fixtures, sem rede/instalação."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
BASE = "2bc9f4228fd448445fbf181652e5af93005def20"
SOURCES = [Path(e.attrib["Include"][3:]) for e in
           ET.parse(ROOT / "TestesMultifontes/TestesMultifontes.csproj").iter("Compile")]
SOURCES.append(Path("CatalogoIdentidades.json"))
PROBE = r'''
using System.Net;
using System.Text.Json;
using GerenciadorAplicativos;
var chrome = new InstalledApp("Google Chrome", "155.0.8000.1", "Google LLC", Architecture: "x64");
InstalledApp[] apps = [chrome, chrome with { Architecture = "x86" },
    new("Mozilla Firefox", "139.0", "Mozilla Corporation"),
    chrome with { Architecture = "unknown" }, chrome with { Architecture = "arm64" },
    chrome with { Name = "Google Chrome Beta" }, chrome with { Publisher = "Fornecedor falso" }];
var transport = new Fixture();
using var source = new OfficialReleaseSources(transport);
var scan = await source.QueryAsync(apps, default);
var matches = MultiSourceMatcher.Merge(apps, null, scan.Findings);
Console.WriteLine(JsonSerializer.Serialize(new {
    sample_size = apps.Length, findings = scan.Findings.Count,
    requests = transport.Requests, candidates = matches.Count(m => m.State == MatchState.Candidate),
    chrome_candidates = matches.Count(m => m.Installed.Name == "Google Chrome" && m.State == MatchState.Candidate),
    excluded_candidates = matches.Skip(3).Count(m => m.State == MatchState.Candidate),
    observed_sources = scan.Diagnostics.Select(d => d.Provider).Order().ToArray()
}));
sealed class Fixture : HttpMessageHandler {
    internal int Requests;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        Interlocked.Increment(ref Requests);
        var url = request.RequestUri!;
        string json;
        if (url.Host == "product-details.mozilla.org") json = "{\"LATEST_FIREFOX_VERSION\":\"140.0.1\"}";
        else if (url.Host == "versionhistory.googleapis.com") {
            string platform = url.AbsolutePath.Contains("/win64/") ? "win64" : "win";
            string version = "156.0.8078.48";
            json = JsonSerializer.Serialize(new { versions = new[] { new {
                name = "chrome/platforms/" + platform + "/channels/stable/versions/" + version, version } } });
        } else throw new InvalidDataException("Origem não prevista no probe.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
'''


def read_snapshot(path, ref):
    if ref is None:
        # O checkout Windows usa CRLF; comparar os blobs de texto canônicos LF.
        return (ROOT / path).read_bytes().replace(b"\r\n", b"\n")
    return subprocess.check_output(["git", "show", f"{ref}:{path.as_posix()}"], cwd=ROOT)


def measure(dotnet, ref):
    env = os.environ.copy()
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    hashes = {}
    with tempfile.TemporaryDirectory(prefix="gf-source-probe-") as temporary:
        folder = Path(temporary)
        for path in SOURCES:
            content = read_snapshot(path, ref)
            (folder / path.name).write_bytes(content)
            hashes[path.as_posix()] = hashlib.sha256(content).hexdigest()
        (folder / "global.json").write_bytes((ROOT / "global.json").read_bytes())
        (folder / "Probe.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup>
<EmbeddedResource Include="CatalogoIdentidades.json" LogicalName="GerenciadorFinal.ApplicationIdentityCatalog" />
</ItemGroup></Project>''')
        (folder / "Program.cs").write_text(PROBE)
        result = subprocess.run([dotnet, "run", "--project", "Probe.csproj", "-c", "Release"],
                                cwd=folder, env=env, text=True, capture_output=True, timeout=120)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        payload = json.loads(result.stdout.strip().splitlines()[-1])
        return {"ref": ref or "working-tree", "source_sha256": hashes, "measurement": payload}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    before, after = measure(args.dotnet, BASE), measure(args.dotnet, None)
    assert before["measurement"]["sample_size"] == after["measurement"]["sample_size"] == 7
    assert before["measurement"]["excluded_candidates"] == after["measurement"]["excluded_candidates"] == 0
    report = {"baseline": BASE, "scope": "synthetic-consultative-components-only", "before": before,
              "after": after, "real_pc_measurement": False, "payloads_executed": 0,
              "limitations": ["Duas arquiteturas Chrome são uma família, não dois produtos novos.",
                              "A tela atual não fornece arquitetura comprovada para o nome simples Google Chrome."]}
    text = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.write_text(text)
    print(text)


if __name__ == "__main__":
    main()
