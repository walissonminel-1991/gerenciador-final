using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using GerenciadorAplicativos;

internal static class Stage3UpdateQueryTests
{
    private sealed class Feed : HttpMessageHandler
    {
        internal readonly List<string> Platforms = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            string platform = path.Contains("/win64/", StringComparison.Ordinal) ? "win64" : "win";
            if (request.RequestUri.Host != "versionhistory.googleapis.com" ||
                path != "/v1/chrome/platforms/" + platform + "/channels/stable/versions")
                throw new Exception("Etapa 3: requisição fora da fixture");
            lock (Platforms) Platforms.Add(platform);
            string json = "{\"versions\":[{\"name\":\"chrome/platforms/" + platform +
                "/channels/stable/versions/131.0.1.2\",\"version\":\"131.0.1.2\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    internal static void Run()
    {
        ProgramEntry App(int index) => new() { Name = "Google Chrome", Publisher = "Google LLC", Version = "130.0.1.2",
            InventoryId = "LocalMachine|Registry32|etapa3-fixture-" + index };
        var apps = Enumerable.Range(0, 4).Select(App).ToArray();
        var metadata = new InstalledUpdateIdentity.BinaryMetadata(0x8664, PEMagic.PE32Plus, true, false, false,
            "Google Chrome", "Google LLC", apps[0].Version);
        apps[0].QueryArchitecture = InstalledUpdateIdentity.CorrelateBinary(apps[0], metadata);
        apps[1].QueryArchitecture = InstalledUpdateIdentity.CorrelateBinary(apps[1], metadata with { Machine = 0x14c, Magic = PEMagic.PE32 });
        apps[2].QueryArchitecture = InstalledUpdateIdentity.CorrelateBinary(apps[2], metadata with { Machine = 0xaa64 });
        var identities = apps.Select(InstalledUpdateIdentity.Create).ToArray();
        // Para estes nomes exatos, a projeção anterior por sufixos era unknown.
        var before = identities.Select(a => a with { Architecture = "unknown" }).ToArray();
        var feed = new Feed();
        var results = Task.Run(async () =>
        {
            using var source = new OfficialReleaseSources(feed);
            var baseline = await source.QueryAsync(before, CancellationToken.None);
            if (baseline.Findings.Count != 0 || feed.Platforms.Count != 0)
                throw new Exception("Etapa 3: baseline sem arquitetura criou consulta");
            return await source.QueryAsync(identities, CancellationToken.None);
        }).GetAwaiter().GetResult();
        var matches = MultiSourceMatcher.Merge(identities, null, results.Findings);
        if (results.Findings.Count != 2 || matches.Count(m => m.State == MatchState.Candidate) != 2 ||
            feed.Platforms.Count != 2 || !feed.Platforms.Order().SequenceEqual(new[] { "win", "win64" }) ||
            matches.Skip(2).Any(m => m.State == MatchState.Candidate))
            throw new Exception("Etapa 3: projeção, arquitetura e fonte consultiva divergem");
        using var form = new MainForm(startServices: false);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var inventory = (List<ProgramEntry>)typeof(MainForm).GetField("_installed", flags)!.GetValue(form)!;
        inventory.AddRange(apps);
        typeof(MainForm).GetMethod("AplicarCandidatos", flags)!.Invoke(form, [matches]);
        if (apps.Count(a => a.IsUpdate) != 2 || apps.Any(a => a.PackageId.Length > 0 || VerifiedUpdaters.Resolve(a) is not null))
            throw new Exception("Etapa 3: observação consultiva ganhou receita ou se perdeu na tela");
        Console.WriteLine("ETAPA3_UI antes=0 depois=2 controles_sem_candidato=2 consultas_fixture=2 receitas=0 rede_real=0");
    }
}
