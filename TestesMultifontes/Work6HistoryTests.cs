using GerenciadorAplicativos;
internal static class Work6HistoryTests
{
    private sealed class Clock : TimeProvider
    {
        internal long Seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
    internal static void Run(Action<bool,string> check)
    {
        var clock = new Clock(); var history = new UpdateResultHistory(clock);
        var apps=Enumerable.Range(0,8).Select(i=>new InstalledApp("Fixture "+i,"1.0","Editor")).ToArray();
        UpdateMatch Candidate(InstalledApp x) => new(x,new("fixture",x.Name,"2.0",x.Publisher),MatchState.Candidate);
        var good=apps.Select(Candidate).ToArray(); history.Apply(apps,good,false);
        foreach(int available in new[]{6,2,0}) {
            var same=apps.Select(a=>a with {}).ToArray();
            var partial=same.Select((a,i)=>i<available ? Candidate(a) : new UpdateMatch(a,null,MatchState.Unmatched)).ToArray();
            var result=history.Apply(same,partial,true);
            check(result.Count(x=>x.State==MatchState.Candidate)==8 && result.All(x=>x.RequiresReview && x.IsStale) &&
                result.Where((x,i)=>!ReferenceEquals(x.Installed,same[i])).Count()==0,
                "WORK6 consulta parcial "+available+"/8 preserva oito evidências antigas e bloqueia ação direta");
        }
        clock.Seconds=1801;
        var none=apps.Select(a=>new UpdateMatch(a,null,MatchState.Unmatched)).ToArray();
        check(history.Apply(apps,none,true).All(x=>x.State==MatchState.Unmatched),
            "WORK6 falhas sucessivas não renovam TTL de evidência antiga");
        history.Apply(apps,good,false);
        var changed=apps.Select((x,i)=>i==0 ? x with { Version="2.0" } : x).ToArray();
        check(history.Apply(changed,changed.Select(x=>new UpdateMatch(x,null,MatchState.Unmatched)).ToArray(),true).All(x=>!x.IsStale),
            "WORK6 troca do inventário invalida histórico; não transfere candidato de outra instalação");
        history.Apply(apps,good,false);
        check(history.Apply(apps,none,false).All(x=>x.State==MatchState.Unmatched && !x.IsStale),
            "WORK6 nova consulta completa substitui evidência antiga");
        bool rejected=false;
        try { history.Apply(changed,good,true); } catch (ArgumentException) { rejected=true; }
        check(rejected,"WORK6 recusa resultado vinculado a snapshot estranho");
    }
}
