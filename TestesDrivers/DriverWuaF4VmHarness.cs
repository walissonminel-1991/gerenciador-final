using GerenciadorAplicativos;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

// Compila somente no executavel TestesDrivers, NUNCA no GerenciadorFinal.exe.
// O Actions usa --f4-runner-probe, separado dos argumentos --f4-vm-*.
internal static class DriverWuaF4VmHarness
{
    internal const string SnapshotPhrase = "VM DESCARTAVEL COM SNAPSHOT RESTAURAVEL";
    internal const string InstallPhrase = "AUTORIZO INSTALAR SOMENTE NESTA VM";
    private sealed record Evidence(int Schema, DateTimeOffset TimeUtc,
        string UpdateId, int Revision, string DeviceHash, string State, string ReasonCode);
    internal static bool LooksVirtualized(string? vendor, string? model)
    {
        var text = ((vendor ?? "") + " " + (model ?? "")).ToLowerInvariant();
        return text.Contains("vmware") || text.Contains("virtualbox") ||
            text.Contains("innotek") || text.Contains("qemu") ||
            text.Contains("kvm") || text.Contains("xen") ||
            text.Contains("parallels") ||
            (text.Contains("microsoft corporation") && text.Contains("virtual machine"));
    }
    internal static bool CanArm(bool vm, bool admin, bool interactive,
        bool snapshot, bool identity, bool consent) =>
        vm && admin && interactive && snapshot && identity && consent;
    internal static string HashDevice(string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..16];

    private static bool DetectedVm()
    {
        const string key = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS";
        string vendor = Convert.ToString(Registry.GetValue(key,"SystemManufacturer",null)) ?? "";
        string model = Convert.ToString(Registry.GetValue(key,"SystemProductName",null)) ?? "";
        return LooksVirtualized(vendor,model);
    }
    private static bool Admin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    private static bool Confirm(string expected)
    {
        Console.WriteLine("Digite exatamente: " + expected);
        return Console.ReadLine() == expected;
    }
    private static void Save(DriverUpdatePilotChoice choice, DriverUpdatePilotResult result)
    {
        string dir = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "GerenciadorFinal","wua-f4-vm-evidence");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir,"report-" +
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N")[..8] + ".json");
        var ev = new Evidence(1,DateTimeOffset.UtcNow,choice.UpdateId,
            choice.Revision,HashDevice(choice.DeviceId),
            result.State.ToString(), result.State == DriverUpdatePilotState.Verified
                ? "PNP_CONFIRMED" : "NOT_PROVEN");
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(ev);
        using var f = new FileStream(file, FileMode.CreateNew, FileAccess.Write,
            FileShare.None,4096,FileOptions.WriteThrough);
        f.Write(data); f.Flush(flushToDisk:true);
        Console.WriteLine("F4: evidencia local " + file);
    }
    internal static async Task<int> Run(string mode)
    {
        if (!OperatingSystem.IsWindows() ||
            mode is not ("--f4-vm-probe" or "--f4-vm-install"))
        {
            Console.Error.WriteLine("F4 NO-GO: Windows e modo valido obrigatorios.");
            return 4;
        }
        bool vm;
        try { vm=DetectedVm(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { vm=false; }
        if (!vm)
        {
            Console.Error.WriteLine("F4 NO-GO: VM desconhecida; nenhum WMI/WUA iniciado.");
            return 4;
        }
        bool install = mode=="--f4-vm-install";
        if (install && (Console.IsInputRedirected || !Admin()))
        {
            Console.Error.WriteLine("F4 NO-GO: somente console interativo admin dentro da VM.");
            return 4;
        }
        Console.WriteLine("F4: laboratorio de VM. SMBIOS virtual nao comprova snapshot.");
        if (install && !Confirm(SnapshotPhrase))
        {
            Console.Error.WriteLine("F4 NO-GO: snapshot nao atestado; nenhum WUA iniciado.");
            return 4;
        }
        DriverInventoryResult inventory;
        DriverOfficialResult official;
        try
        {
            inventory = DriverInventory.Capture(CancellationToken.None);
            if (inventory.Partial)
            {
                Console.Error.WriteLine("F4 NO-GO: PnP incompleto.");
                return 5;
            }
            official = await DriverOfficialLookup.Start(inventory,CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine("F4 NO-GO: leitura oficial falhou.");
            return 5;
        }
        if (official.Partial || official.SearchContext is not
            { Source:"Windows Update", Online:true })
        {
            Console.Error.WriteLine("F4 NO-GO: pesquisa WUA incompleta.");
            return 5;
        }
        var choices=official.Matches.Select(m=>
            DriverUpdatePilot.TryPrepare(inventory,official,m,out var c) ? c : null)
            .OfType<DriverUpdatePilotChoice>().Distinct().ToArray();
        Console.WriteLine("F4: ofertas consultivas elegiveis " + choices.Length);
        for(int i=0;i<choices.Length;i++)
        {
            var c=choices[i];
            Console.WriteLine($"{i+1}. {c.DriverClass}: {c.Title} | " +
                $"{c.UpdateId}@{c.Revision} | dispositivo #{HashDevice(c.DeviceId)}");
        }
        if(!install)
        {
            Console.WriteLine("F4 PROBE: nenhuma chamada de download/instalacao.");
            return choices.Length>0 ? 0 : 5;
        }
        if(choices.Length==0)
        {
            Console.Error.WriteLine("F4 NO-GO: nenhuma oferta adequada.");
            return 5;
        }
        Console.WriteLine("Digite numero exato da oferta:");
        if(!int.TryParse(Console.ReadLine(),out int index) ||
            index<1 || index>choices.Length) return 4;
        var choice=choices[index-1];
        bool exact=Confirm(choice.UpdateId) &&
            Confirm(choice.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)) &&
            Confirm(HashDevice(choice.DeviceId));
        bool consent=exact && Confirm(InstallPhrase);
        if(!CanArm(vm,Admin(),!Console.IsInputRedirected,true,exact,consent))
        {
            Console.Error.WriteLine("F4 NO-GO: consentimento ou identidade divergentes.");
            return 4;
        }
        var approval=new DriverUpdatePilotApproval(choice.UpdateId,choice.Revision,
            choice.DeviceId,true,true,DateTimeOffset.UtcNow);
        using var journal=new DriverUpdatePilotFileJournal(
            DriverUpdatePilotFileJournal.SuggestedPath);
        using var source=new CancellationTokenSource();
        ConsoleCancelEventHandler cancel=(_,e)=>
        {
            e.Cancel=true;source.Cancel();
            Console.WriteLine("Cancelamento solicitado; COM sincrono pode prosseguir. Nao repetir.");
        };
        Console.CancelKeyPress+=cancel;
        DriverUpdatePilotResult result;
        try
        {
            result=await DriverUpdatePilot.Execute(choice,approval,
                new DriverWuaNativeBackend(),journal,source.Token,
                progress:msg=>Console.WriteLine("F4: "+msg));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result=new(DriverUpdatePilotState.InstallationUncertain,
                "Falha inesperada; nao repetir sem reconciliar.");
        }
        finally { Console.CancelKeyPress-=cancel; }
        Console.WriteLine("F4 RESULTADO: "+result.State+" | "+result.Reason);
        try { Save(choice,result); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Console.Error.WriteLine("F4: evidencia nao salva; journal permanece."); }
        return result.State==DriverUpdatePilotState.Verified?0:6;
    }
}
