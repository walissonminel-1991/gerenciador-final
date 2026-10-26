using System.Reflection;
using System.Text.Json;
using GerenciadorAplicativos;

internal static class BetaBuildVerifier
{
    // Le o assembly efetivamente publicado sem iniciar o GF, consultar rede,
    // registrar consentimento ou invocar qualquer metodo do produto carregado.
    internal static void Run(string[] args)
    {
        bool connectedRequired = args[0] == "--verify-connected-build";
        if (args.Length != (connectedRequired ? 4 : 3) || args[2].Length != 40 ||
            args[2].Any(c => !char.IsAsciiHexDigit(c))) throw new InvalidDataException("GF_BETA_BUILD_ARGUMENTS");
        var assembly = Assembly.LoadFile(Path.GetFullPath(args[1]));
        var metadata = assembly.GetCustomAttributesData()
            .Where(a => a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute")
            .Select(a => (Key: a.ConstructorArguments[0].Value as string, Value: a.ConstructorArguments[1].Value as string))
            .ToArray();
        string? One(string key)
        {
            var items = metadata.Where(m => m.Key == key).ToArray();
            if (items.Length > 1) throw new InvalidDataException("GF_BETA_DUPLICATE_METADATA");
            return items.SingleOrDefault().Value;
        }
        string? endpoint = One("GfBetaEndpoint");
        var approved = metadata.Where(m => m.Key == "GfApprovedBetaEndpoint").Select(m => m.Value ?? "");
        bool configured = BetaEndpointPolicy.Validate(endpoint, approved) is not null &&
            One("GfReviewedPrivacyNoticeSha256") == TelemetryConsentPolicy.NoticeHash &&
            One("GfPrivacyNoticeVersion") == TelemetryConsentPolicy.Version;
        if (assembly.GetName().Version?.ToString() != "5.13.11.66" || One("GfBuildChannel") != "BETA" ||
            One("GfSourceCommit") != args[2]) throw new InvalidDataException("GF_BETA_BUILD_ORIGIN");
        if (connectedRequired && (!configured || endpoint != args[3]))
            throw new InvalidDataException("GF_BETA_CONNECTED_BUILD_REQUIRED");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schema = 1, version = assembly.GetName().Version!.ToString(), source_commit = One("GfSourceCommit"),
            channel = "BETA", transport_configured = configured, endpoint = endpoint ?? "",
            notice_version = TelemetryConsentPolicy.Version, notice_sha256 = TelemetryConsentPolicy.NoticeHash,
            reviewed_notice_sha256 = One("GfReviewedPrivacyNoticeSha256") ?? "",
            distribution = "requires_owner_approval"
        }));
    }
}
