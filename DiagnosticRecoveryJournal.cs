using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GerenciadorAplicativos;

// Apenas registros de tamanho fixo e campos numéricos. Arquivos anteriores
// são tratados como dados não confiáveis: nunca exportar texto lido do disco.
internal enum DiagnosticRecoveryKind : int
{
    Navigation=1, OperationBegin=2, OperationEnd=3, OperationResult=4,
    Failure=5, ProcessSample=6, UiDelay=7, NormalEnd=8
}
internal sealed class DiagnosticRecoveryJournal
{
    private const ulong Magic = 0x314A524447464F4CUL;
    private const int HeaderSize=16, RecordSize=36, Retained=128;
    private const long MaxBytes=4*1024*1024;
    private readonly object _gate=new();
    private FileStream? _coordination, _stream;
    private BinaryWriter? _writer;
    private long _written, _omitted;
    private bool _unavailable;
    private long _lastFlush;
    private RecoveryRead? _previous;

    private sealed record Entry(DiagnosticRecoveryKind Kind,long UtcTicks,long A,long B,long C);
    private sealed record RecoveryRead(string Path,InstallerFileGuard.FileIdentity Identity,
        long Start, long Count, bool Normal, bool Partial, Entry[] Recent);

    internal void Start(string directory)
    {
        lock(_gate)
        {
            if(_coordination is not null||_unavailable)return;
            try
            {
                using var pinned=InstallerFileGuard.PinDirectory(directory,allowLeafWrites:true);
                _coordination=InstallerFileGuard.OpenCoordinationLock(Path.Combine(directory,"GF-recovery-v1.lock"));
                string[] slots=[Path.Combine(directory,"GF-recovery-v1-0.bin"),Path.Combine(directory,"GF-recovery-v1-1.bin")];
                var existing=new List<RecoveryRead>();
                foreach(string path in slots)
                    if(File.Exists(path))existing.Add(Read(path));
                _previous=existing.Where(x=>!x.Normal).OrderByDescending(x=>x.Start).FirstOrDefault();
                string? free=slots.FirstOrDefault(path=>!File.Exists(path));
                if(free is null)
                {
                    // Conservar o último encerramento não confirmado; reutilizar
                    // apenas outro arquivo do formato reconhecido e mesma identidade.
                    var replaced=existing.Where(x=>x.Path!=_previous?.Path).OrderBy(x=>x.Start).First();
                    using(var held=InstallerFileGuard.OpenResidual(replaced.Path,deleteAccess:true))
                    {
                        if(InstallerFileGuard.ReadIdentity(held)!=replaced.Identity)
                            throw new InvalidDataException("Checkpoint alterado.");
                        InstallerFileGuard.MarkResidualForDeletion(held);
                    }
                    free=replaced.Path;
                }
                _stream=new FileStream(free,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,4096);
                _writer=new BinaryWriter(_stream,Encoding.UTF8,leaveOpen:true);
                _writer.Write(Magic);
                _writer.Write(DateTimeOffset.UtcNow.UtcTicks);
                _writer.Flush();
                _stream.Flush(false);
                _lastFlush=Stopwatch.GetTimestamp();
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or InvalidOperationException or NotSupportedException or System.Security.SecurityException)
            { _unavailable=true; CloseCore(); }
        }
    }

    private static RecoveryRead Read(string path)
    {
        using var stream=InstallerFileGuard.OpenResidual(path);
        var identity=InstallerFileGuard.ReadIdentity(stream);
        if(stream.Length<HeaderSize||stream.Length>MaxBytes)
            throw new InvalidDataException("Checkpoint de tamanho inválido.");
        using var reader=new BinaryReader(stream,Encoding.UTF8,leaveOpen:true);
        if(reader.ReadUInt64()!=Magic)throw new InvalidDataException("Formato de checkpoint desconhecido.");
        long start=reader.ReadInt64();
        if(start<DateTimeOffset.UnixEpoch.UtcTicks||start>DateTimeOffset.MaxValue.UtcTicks)
            throw new InvalidDataException("Data de checkpoint inválida.");
        long count=(stream.Length-HeaderSize)/RecordSize;
        bool partial=(stream.Length-HeaderSize)%RecordSize!=0;
        // Limite de memória e tempo: últimas 128 entradas, com contagem total.
        stream.Position=HeaderSize+Math.Max(0,count-Retained)*RecordSize;
        var entries=new List<Entry>();
        for(long i=Math.Max(0,count-Retained);i<count;i++)
        {
            var kind=(DiagnosticRecoveryKind)reader.ReadInt32();
            long utc=reader.ReadInt64(),a=reader.ReadInt64(),b=reader.ReadInt64(),c=reader.ReadInt64();
            if(!Enum.IsDefined(kind)||utc<start||utc>DateTimeOffset.MaxValue.UtcTicks)
            {partial=true;continue;}
            entries.Add(new(kind,utc,a,b,c));
        }
        bool normal=!partial&&entries.LastOrDefault()?.Kind==DiagnosticRecoveryKind.NormalEnd;
        return new(path,identity,start,count,normal,partial,entries.ToArray());
    }

    internal void Record(DiagnosticRecoveryKind kind,long a=0,long b=0,long c=0,bool checkpoint=false)
    {
        lock(_gate)
        {
            if(_writer is null||_stream is null||_unavailable)return;
            // Reservar espaço para marca de encerramento normal.
            if(HeaderSize+(_written+1)*RecordSize>MaxBytes-(kind==DiagnosticRecoveryKind.NormalEnd?0:RecordSize))
            {_omitted++;return;}
            try
            {
                _writer.Write((int)kind);
                _writer.Write(DateTimeOffset.UtcNow.UtcTicks);
                _writer.Write(a);_writer.Write(b);_writer.Write(c);
                _written++;
                long now=Stopwatch.GetTimestamp();
                if(checkpoint||Stopwatch.GetElapsedTime(_lastFlush,now).TotalSeconds>=10)
                {
                    _writer.Flush();
                    _stream.Flush(false);
                    _lastFlush=now;
                }
            }
            catch(Exception ex) when(ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
            {_unavailable=true;}
        }
    }

    internal string FormatPrevious()
    {
        lock(_gate)
        {
            var text=new StringBuilder("=== RECUPERAÇÃO DE SESSÃO ANTERIOR ==="+Environment.NewLine);
            text.AppendLine("Ausência de encerramento confirmado não prova travamento; pode ser desligamento, suspensão ou interrupção externa.");
            if(_unavailable)text.AppendLine("AVISO: checkpoint local indisponível ou interrompido.");
            if(_omitted>0)text.AppendLine($"Checkpoint atual: {_omitted} registros excederam o limite.");
            if(_previous is not{} previous)
            {
                text.AppendLine("Nenhuma sessão anterior interrompida recuperada.");
                return text.ToString();
            }
            text.AppendLine($"Início UTC: {new DateTimeOffset(previous.Start,TimeSpan.Zero):O}; registros físicos: {previous.Count}; últimos {previous.Recent.Length} recuperados.");
            if(previous.Partial)text.AppendLine("AVISO: recuperação parcial; registros inválidos ou truncados foram ignorados.");
            foreach(var entry in previous.Recent)
            {
                string page=entry.A>=0&&entry.A<=7?((V269Page)entry.A).ToString():"N/D";
                string details=entry.Kind switch
                {
                    DiagnosticRecoveryKind.Navigation=>$"aba={page} duracao_ms={entry.B}",
                    DiagnosticRecoveryKind.OperationBegin or DiagnosticRecoveryKind.OperationEnd=>$"aba={page}",
                    DiagnosticRecoveryKind.OperationResult=>$"aba={page} resultado={(entry.B==1?"confirmado":entry.B==0?"não confirmado":"indeterminado")}",
                    DiagnosticRecoveryKind.Failure=>$"hresult=0x{unchecked((uint)entry.A):X8}",
                    DiagnosticRecoveryKind.ProcessSample=>$"cpu_acumulada_ms={entry.A} ram_residente_bytes={entry.B} ram_privada_bytes={entry.C}",
                    DiagnosticRecoveryKind.UiDelay=>$"intervalo_ms={entry.A} esperado_ms={entry.B}",
                    _=>""
                };
                text.AppendLine(FormattableString.Invariant($"{new DateTimeOffset(entry.UtcTicks,TimeSpan.Zero):O} {entry.Kind} {details}"));
            }
            return text.ToString();
        }
    }

    internal void Close(bool normal)
    {
        lock(_gate)
        {
            if(normal)Record(DiagnosticRecoveryKind.NormalEnd,checkpoint:true);
            CloseCore();
        }
    }

    private void CloseCore()
    {
        try{_writer?.Dispose();}catch(IOException){_unavailable=true;}
        finally
        {
            _writer=null;
            try{_stream?.Dispose();}catch(IOException){_unavailable=true;}
            finally{_stream=null;_coordination?.Dispose();_coordination=null;}
        }
    }
}
