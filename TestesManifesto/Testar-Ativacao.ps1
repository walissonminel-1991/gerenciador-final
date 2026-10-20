# Verifica a identidade do manifesto e pede ao Windows para gerar
# o contexto de ativacao do EXE publicado (detecta ERROR_SXS_* antes de entregar).
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceManifest = Join-Path $root 'app.manifest'
$projectPath = Join-Path $root 'GerenciadorAplicativos.csproj'
$exe = (Resolve-Path $ExecutablePath).Path

[xml]$manifest = Get-Content -LiteralPath $sourceManifest -Raw
$identity = $manifest.SelectSingleNode("/*[local-name()='assembly']/*[local-name()='assemblyIdentity']")
if ($null -eq $identity) { throw 'Manifesto sem assemblyIdentity.' }
$version = $identity.GetAttribute('version')
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Versao Win32 invalida '$version': exigidos quatro componentes (ex.: 5.12.13.1)."
}
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$packageVersion = [string]$project.Project.PropertyGroup.Version
if ($packageVersion -ne $version) {
    throw "Versao do projeto ($packageVersion) difere da identidade do manifesto ($version)."
}
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Executavel nao encontrado: $exe" }

# CreateActCtxW solicita ao proprio Windows a leitura do recurso RT_MANIFEST, ID 1,
# embutido pelo SDK no executavel. Um manifesto SxS invalido reprova o build.
if (-not ('GerenciadorFinalManifestActivation' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class GerenciadorFinalManifestActivation
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ACTCTX
    {
        public uint cbSize;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpSource;
        public ushort wProcessorArchitecture;
        public ushort wLangId;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpAssemblyDirectory;
        public IntPtr lpResourceName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpApplicationName;
        public IntPtr hModule;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateActCtxW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateActCtx(ref ACTCTX context);

    [DllImport("kernel32.dll", EntryPoint = "ReleaseActCtx")]
    private static extern void ReleaseActCtx(IntPtr context);

    public static int Validate(string exePath)
    {
        ACTCTX context = new ACTCTX();
        context.cbSize = (uint)Marshal.SizeOf<ACTCTX>();
        context.dwFlags = 0x8; // ACTCTX_FLAG_RESOURCE_NAME_VALID
        context.lpSource = exePath;
        context.lpResourceName = new IntPtr(1); // RT_MANIFEST: ID 1 do executavel
        IntPtr handle = CreateActCtx(ref context);
        if (handle == new IntPtr(-1)) return Marshal.GetLastWin32Error();
        ReleaseActCtx(handle);
        return 0;
    }
}
'@
}
$code = [GerenciadorFinalManifestActivation]::Validate($exe)
if ($code -ne 0) {
    throw "Windows recusou o manifesto embutido (CreateActCtx erro $code / 0x$($code.ToString('X8')))."
}
Write-Host "OK: manifesto Win32 $version, identidade do projeto e ativacao SxS do executavel."
