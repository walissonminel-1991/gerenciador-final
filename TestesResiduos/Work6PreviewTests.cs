using GerenciadorAplicativos;
internal static class Work6PreviewTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        if(!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP WORK6 identidade nativa de diretórios exige Windows"); return; }
        string outer=Path.Combine(Path.GetTempPath(),"GF-W6-Preview-"+Guid.NewGuid().ToString("N"));
        string parent=Path.Combine(outer,"parent"), root=Path.Combine(parent,"root");
        Directory.CreateDirectory(root);
        try {
            string first=Path.Combine(root,"uninstall.exe"), second=Path.Combine(root,"helper.exe");
            File.WriteAllText(first,"fixture A não executável"); File.WriteAllText(second,"fixture B não executável");
            var candidate=new ResidualCandidate("Fixture",root,[first,second],0);
            var preview=await ResidualPreview.CaptureAsync(candidate);
            check(preview.Complete && await ResidualPreview.IsUnchangedAsync(preview),"W6-R-CONTROL captura íntegra continua reconhecida");
            var forged=preview with { Files=new[]{preview.Files[0],preview.Files[0]} };
            check(!await ResidualPreview.IsUnchangedAsync(forged),"W6-R-DUP duplicar um estado não autoriza omitir o segundo alvo");
            string moved=root+"-old";
            Directory.Move(root,moved);Directory.CreateDirectory(root);
            File.Move(Path.Combine(moved,"uninstall.exe"),first);File.Move(Path.Combine(moved,"helper.exe"),second);
            check(!await ResidualPreview.IsUnchangedAsync(preview),"W6-R-ROOT pasta substituída invalida prévia mesmo com mesmos arquivos/inodes");
            preview=await ResidualPreview.CaptureAsync(candidate);
            check(preview.Complete,"W6-R-RECAP nova captura explícita reconhece novo diretório");
            string oldParent=parent+"-old";
            Directory.Move(parent,oldParent);Directory.CreateDirectory(parent);
            Directory.Move(Path.Combine(oldParent,"root"),root);
            check(!await ResidualPreview.IsUnchangedAsync(preview),"W6-R-PARENT ancestral substituído invalida prévia mesmo com raiz e arquivos idênticos");
        } finally { Directory.Delete(outer,true); }
    }
}
