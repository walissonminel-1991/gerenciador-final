using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GerenciadorAplicativos;

internal interface IWinGetInstalledCatalog
{
    Task<IReadOnlyList<ProviderInstallation>> ReadAsync(CancellationToken token);
}

// Typed COM lives in a short-lived copy of our own process. A hung native API
// cannot strand UI threads. Only this read-only child can be terminated.
internal sealed class WinGetComProcessCatalog : IWinGetInstalledCatalog
{
    internal const string Argument = "--gf-winget-read-only";
    public async Task<IReadOnlyList<ProviderInstallation>> ReadAsync(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return [];
        string self = Environment.ProcessPath ?? throw new InvalidOperationException("Missing host");
        if (!Path.GetFileName(self).Equals("GerenciadorFinal.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected host");
        using var process = new Process { StartInfo = new(self) { UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory } };
        process.StartInfo.ArgumentList.Add(Argument);
        if (!process.Start()) throw new IOException("Reader did not start");
        try
        {
            // Read both pipes concurrently and enforce a byte/character ceiling
            // before deserialization. Never log stdout or paths.
            var output = ReadBoundedAsync(process.StandardOutput, 4 * 1024 * 1024, token);
            var error = ReadBoundedAsync(process.StandardError, 4096, token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(token));
            if (process.ExitCode != 0) throw new IOException("COM unavailable");
            return JsonSerializer.Deserialize<ProviderInstallation[]>(await output,
                new JsonSerializerOptions { MaxDepth = 12 }) ?? throw new InvalidDataException("Empty protocol");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(); } // our reader only, never WinGet server/apps
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var text = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer, token)) != 0)
        { if (text.Length + count > limit) throw new InvalidDataException("Protocol limit"); text.Append(buffer, 0, count); }
        return text.ToString();
    }
}

internal sealed class WinGetComDiscoveryProvider(IWinGetInstalledCatalog catalog) : IApplicationDiscoveryProvider
{
    public ProviderDescriptor Descriptor => new("winget", 0, ProviderCapabilities.DetectInstallations |
        ProviderCapabilities.AssociateIdentities, TimeSpan.FromSeconds(10));
    public async Task<ApplicationProviderResult> DiscoverAsync(ProviderQuery query, CancellationToken token)
    {
        var rows = await catalog.ReadAsync(token);
        token.ThrowIfCancellationRequested();
        return new("winget", OperatingSystem.IsWindows() || rows.Count > 0 ? "ok" : "unavailable", rows);
    }
}
