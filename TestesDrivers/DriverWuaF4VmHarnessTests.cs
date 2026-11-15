using GerenciadorAplicativos;

internal static class DriverWuaF4VmHarnessTests
{
    internal static void Run()
    {
        static void Check(bool v,string label)
        { if(!v)throw new Exception("F4: "+label); }
        Check(DriverWuaF4VmHarness.LooksVirtualized("Microsoft Corporation","Virtual Machine") &&
            DriverWuaF4VmHarness.LooksVirtualized("VMware","Virtual Platform") &&
            DriverWuaF4VmHarness.LooksVirtualized("innotek GmbH","VirtualBox") &&
            DriverWuaF4VmHarness.LooksVirtualized("QEMU","Standard PC") &&
            !DriverWuaF4VmHarness.LooksVirtualized("Gigabyte","B550") &&
            !DriverWuaF4VmHarness.LooksVirtualized("Microsoft Corporation","Surface Pro") &&
            !DriverWuaF4VmHarness.LooksVirtualized("",""),
            "virtualizacao fail-closed nos PCs comuns");
        for(int i=0;i<6;i++)
        {
            bool[] b=[true,true,true,true,true,true];b[i]=false;
            Check(!DriverWuaF4VmHarness.CanArm(b[0],b[1],b[2],b[3],b[4],b[5]),
                "seis condicoes independentes exigidas");
        }
        Check(DriverWuaF4VmHarness.CanArm(true,true,true,true,true,true) &&
            DriverWuaF4VmHarness.HashDevice("A").Length==16 &&
            DriverWuaF4VmHarness.HashDevice("A")!=
                DriverWuaF4VmHarness.HashDevice("B"),"hash local reduzido");
        var candidate=new DriverUpdatePilotChoice(
            "a4772bc2-fdd9-4c3b-9041-6b7f6730cfb3",1,@"PCI\A",
            @"PCI\VEN_1234&DEV_2345","Fornecedor","oem2.inf",
            "1.0.0.0","Image","Câmera");
        Check(DriverWuaF4Gates.OneEntry(1) &&
            !DriverWuaF4Gates.OneEntry(0) &&
            !DriverWuaF4Gates.OneEntry(2) &&
            DriverWuaF4Gates.EntryMatches(candidate,candidate.HardwareId,
                candidate.Provider,candidate.DriverClass) &&
            !DriverWuaF4Gates.EntryMatches(candidate,"OTHER",candidate.Provider,"Image") &&
            !DriverWuaF4Gates.EntryMatches(candidate,candidate.HardwareId,"OTHER","Image") &&
            !DriverWuaF4Gates.EntryMatches(candidate,candidate.HardwareId,
                candidate.Provider,"Net"),"recusar entradas WUA com identidade distinta");
        Console.WriteLine("OK F4: gates VM, consentimento e pacote unico puramente sinteticos; sem WMI/WUA nativo");
    }
}
