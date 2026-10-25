# Componentes e licenças da distribuição portátil

O código e os recursos próprios do Gerenciador Final seguem LICENSE.txt (MIT).
As licenças dos componentes incorporados continuam pertencendo aos respectivos
fornecedores; não são substituídas pela licença do GF. O programa permanece
gratuito e aberto, sem serviço comercial obrigatório.

| Componente | Origem e termos |
| --- | --- |
| .NET Runtime e WinForms | Packs oficiais Microsoft.NETCore.App.Runtime.win-x64 e Microsoft.WindowsDesktop.App.Runtime.win-x64, nas versões registradas em GerenciadorFinal.runtimeconfig.json. Licenças e avisos originais copiados do restore em Licencas/. |
| Binários nativos Microsoft do runtime Windows | A [relação oficial de licenças](https://github.com/dotnet/core/blob/main/license-information-windows.md) distingue componentes MIT de coreclr.dll e Microsoft.DiaSymReader.Native, sob os [termos .NET Library](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm). Não redistribuir separadamente do aplicativo. |
| Microsoft.Windows.SDK.NET.dll e WinRT.Runtime.dll | Projeções C#/WinRT, [código e licença MIT Microsoft](https://github.com/microsoft/CsWinRT). O pack Microsoft.Windows.SDK.NET.Ref também referencia os [termos do Windows SDK](https://learn.microsoft.com/en-us/legal/windows-sdk/license) no seu nuspec. |
| APIs Windows, Store, WMI e Windows Update | Componentes do sistema, não empacotados pelo GF; sujeitos aos termos do Windows. |
| Microsoft.WindowsPackageManager.ComInterop 1.29.380 | Projeção tipada e fábrica nativa x64 oficiais, código MIT: https://github.com/microsoft/winget-cli. Avisos originais do pacote copiados integralmente em Licencas/WinGet/NOTICE.txt. O serviço WinGet/App Installer não é instalado nem redistribuído pelo GF. |
| RuckZuck, GitHub Releases e sites oficiais | Fontes consultadas via HTTPS. Seus catálogos/instaladores não integram este ZIP; cada aplicativo mantém os termos de seu fornecedor. |
| Inno Setup | Ferramenta opcional para gerar o instalador, ausente do portátil. [Licença do fornecedor](https://jrsoftware.org/files/is/license.txt). |

O publish inclui somente os ativos de runtime selecionados pelo SDK; nenhum
SDK, compilador, certificado, instalador de terceiro ou catálogo baixado é
incorporado. O arquivo de dependências GerenciadorFinal.deps.json permite
conferir os componentes e versões realmente distribuídos. Antes de uma release
pública, o mantenedor deve revisar também as exigências dos binários Microsoft;
um resultado verde da CI comprova integridade técnica, não aprovação jurídica.

## Licença C#/WinRT

Origem: https://github.com/microsoft/CsWinRT/blob/master/LICENSE

MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## YamlDotNet 18.1.0

Parser YAML gerenciado, licença MIT. Origem: https://github.com/aaubry/YamlDotNet/tree/v18.1.0

Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013, 2014 Antoine Aubry and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
