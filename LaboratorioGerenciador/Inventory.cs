using Microsoft.Win32;

namespace GerenciadorFinal.Laboratorio;

internal static class Inventory
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    internal static IReadOnlyList<InstalledApp> Read()
    {
        var found = new List<InstalledApp>();
        foreach (var location in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32)
        })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(location.Item1, location.Item2);
                using var uninstall = root.OpenSubKey(UninstallKey);
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var item = uninstall.OpenSubKey(name);
                        if (item is null || ReadInt(item, "SystemComponent") == 1) continue;
                        string displayName = ReadString(item, "DisplayName");
                        if (string.IsNullOrWhiteSpace(displayName)) continue;
                        found.Add(new InstalledApp(displayName, ReadString(item, "DisplayVersion"), ReadString(item, "Publisher")));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        // Falhas pontuais de leitura não impedem as demais entradas.
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or PlatformNotSupportedException)
            {
                // Uma visualização do Registro indisponível não impede as outras.
            }
        }
        return found.DistinctBy(x => (x.Name.ToUpperInvariant(), x.Version.ToUpperInvariant(), x.Publisher.ToUpperInvariant()))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string ReadString(RegistryKey key, string name) => key.GetValue(name) as string ?? "";
    private static int ReadInt(RegistryKey key, string name) => key.GetValue(name) is int value ? value : 0;
}
