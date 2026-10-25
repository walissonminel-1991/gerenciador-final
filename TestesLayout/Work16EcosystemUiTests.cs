using GerenciadorAplicativos;

internal static class Work16EcosystemUiTests
{
    internal static void Run()
    {
        var apps = new List<ProgramEntry>();
        var receipts = new[] { "scoop", "chocolatey" }.Select(provider => new ApplicationProviderResult(provider, "ok",
            [new(new(provider, "fixture-editor", "GF Fixture Editor", "Fixture metadata", "1.0.0", Scope: "user", Managed: true), "synthetic-receipt")]));
        if (EcosystemInventory.AppendManaged(apps, receipts) != 2) throw new Exception("W16 manager receipts did not reach production inventory");
        var capabilities = ApplicationCapabilityInspection.Inspect(apps.ToArray(), true, true);
        for (int i = 0; i < apps.Count; i++)
        {
            if (!ActionableInventory.ShouldShow(apps[i]) || !ActionableInventory.CanManageExternally(apps[i]) || apps[i].CanUninstall ||
                capabilities[i].Uninstall.State != ApplicationCapabilityState.ExternalManagement ||
                capabilities[i].Residual.State != ApplicationCapabilityState.ReadOnly || VerifiedUpdaters.Resolve(apps[i]) is not null)
                throw new Exception("W16 managed inventory/action/capability boundaries diverged");
        }
        var store = new ProgramEntry { Name = "GF Localized Reader", Publisher = "Fixture Vendor", Version = "1.0.0",
            InventoryId = "MSIX|Fixture.Reader", MsixPackageFamilyName = "Fixture.Reader_123abc", InventoryOnly = true,
            IsUpdate = true, AvailableVersion = "2.0.0", UpdateRequiresReview = true };
        var assessment = ApplicationCapabilityInspection.Inspect([store], true, true).Single();
        if (!EcosystemInventory.CanQueryVersions(store) || !ActionableInventory.CanManageWithWindows(store) ||
            VerifiedUpdaters.GetPrimaryAction(store) != "windows-manage" || VerifiedUpdaters.Resolve(store) is not null ||
            assessment.Update.State != ApplicationCapabilityState.AvailableNotAuthorized ||
            assessment.Uninstall.State != ApplicationCapabilityState.ExternalManagement)
            throw new Exception("W16 generic MSIX PFN cannot reach consultation/external management safely");
        Console.WriteLine("W16 UI integration: 2 manager receipts and generic MSIX preserve read-only operation boundaries");
    }
}
