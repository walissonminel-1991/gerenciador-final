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
| PCI ID Project e USB ID Repository | Catálogos de identificação de hardware incorporados, sob BSD de 3 cláusulas. Revisões e termos abaixo. |
| FreeBSD src, tabela de codecs HDA | Dados de identificação de áudio incorporados sob BSD de 2 cláusulas. Revisão e termos abaixo. |
| Inno Setup | Ferramenta opcional para gerar o instalador, ausente do portátil. [Licença do fornecedor](https://jrsoftware.org/files/is/license.txt). |

O publish inclui os ativos de runtime selecionados pelo SDK e os catálogos
embutidos listados acima. O arquivo GerenciadorFinal.deps.json registra as
dependências de runtime distribuídas.

## Catálogos PCI e USB

Os dados em `Assets/DriverCatalog/` correspondem aos snapshots abaixo,
compactados com `gzip -n -9` e incorporados ao executável como recursos.

| Arquivo | Snapshot | SHA-256 descompactado |
| --- | --- | --- |
| `pci.ids` | [PCI ID Project](https://pci-ids.ucw.cz/), versão 2026.10.31 | `3af2de60a0cb598912d4ede5314fcb36a59593d08fba4f7f26ca4d4e53a7b9ea` |
| `usb.ids` | [USB ID Repository](https://usb-ids.gowdy.us/), versão 2026.06.26 | `34b2b2289508a866cf38b96e54b37ac52c0ab7ce8361a56116af24e3fd094421` |

Origem dos dois snapshots: [hwdata, commit 1cf7032160ad57e2eaad36c4f23eede1edd8203b](https://github.com/vcrhonek/hwdata/tree/1cf7032160ad57e2eaad36c4f23eede1edd8203b).

BSD 3-Clause License

PCI ID Project copyright: Martin Mares and Albert Pool.

USB ID Repository: maintained by Stephen J. Gowdy and updated by its contributors.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software without
   specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS “AS IS”
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

## Catálogo de codecs HDA

`Assets/DriverCatalog/hda.ids` contém pares de fabricante/modelo da tabela
`hdacc_codecs` e das constantes de `hdac.h` do
[FreeBSD src, commit 55b89b7b529f1c37a749adadb11aba4f8e9e51ca](https://github.com/freebsd/freebsd-src/tree/55b89b7b529f1c37a749adadb11aba4f8e9e51ca/sys/dev/sound/pci/hda).
O GF inclui 320 modelos com IDs exatos e sem requisito de revisão. Os IDs
curinga da fonte servem apenas para descrever o fabricante, sem atribuir um
modelo. Duplicatas idênticas foram consolidadas. SHA-256 do recurso em LF
`83ccc7f4f011270189d7fa04bd33718b26d441cc3432323a0be8a29fa6b2d4c1`.

BSD 2-Clause License

Copyright (c) 2006 Stephane E. Potvin <sepotvin@videotron.ca>
Copyright (c) 2006 Ariff Abdullah <ariff@FreeBSD.org>
Copyright (c) 2008-2012 Alexander Motin <mav@FreeBSD.org>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS ``AS IS'' AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
SUCH DAMAGE.

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
