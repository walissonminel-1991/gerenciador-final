"""Validação estática simples do pacote; NÃO equivale a compilar C# no Windows."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
lab = root / 'LaboratorioGerenciador'
assert lab.is_dir() and (root/'.github/workflows/laboratorio-provedores.yml').is_file()
for file in [lab / 'LaboratorioGerenciador.csproj', root/'TestesMotor/TestesMotor.csproj', lab/'app.manifest']:
    ET.parse(file)
source = {p.name: p.read_text(encoding='utf-8') for p in lab.glob('*.cs')}
assert len(source) >= 6
assert 'RegistryHive.LocalMachine' in source['Inventory.cs']
assert 'RegistryHive.CurrentUser' in source['Inventory.cs']
assert 'GetCatalogAsync' in source['RuckZuckCatalog.cs']
assert 'geturl' in source['RuckZuckCatalog.cs']
assert 'getcatalog' in source['RuckZuckCatalog.cs']
assert 'IsAllowedUri' in source['RuckZuckCatalog.cs']
assert 'MessageBoxButtons.YesNo' in source['LabForm.cs']
assert 'MatchState.Candidate' in source['Matching.cs']
assert 'NormalizePublisher' in source['Matching.cs']
assert 'InvalidDataException' in source['RuckZuckCatalog.cs']
assert 'CancellationTokenSource' in source['LabForm.cs']
assert 'Clipboard.SetText' in source['LabForm.cs']
assert 'File.WriteAllText' not in '\n'.join(source.values())
assert 'Process.Start' not in '\n'.join(source.values())
assert 'winget.exe' not in '\n'.join(source.values()).lower()
assert 'PowerShell' not in '\n'.join(source.values())
assert 'dotnet publish' in (root/'.github/workflows/laboratorio-provedores.yml').read_text()
assert '--self-contained true' in (root/'.github/workflows/laboratorio-provedores.yml').read_text()
print('VALIDAÇÃO ESTÁTICA: 17 verificações concluídas.')
print('AVISO: o código C# não foi compilado nem executado neste ambiente.')
