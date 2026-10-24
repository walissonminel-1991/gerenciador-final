using GerenciadorAplicativos;

internal static class LiveSourceValidation
{
    internal static bool Accepts(ProviderScan scan, int expectedSources) =>
        expectedSources > 0 && scan.Findings.Count > 0 &&
        scan.Diagnostics.Count == expectedSources &&
        scan.Diagnostics.Select(d => d.Provider).Distinct(StringComparer.Ordinal).Count() == expectedSources &&
        scan.Diagnostics.All(d => d.Status == "ok");
}
