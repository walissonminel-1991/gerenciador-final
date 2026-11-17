namespace GerenciadorAplicativos;

// LAB UX3: só constantes e transformação em texto; jamais acessar hardware,
// WMI, WUA, COM, arquivos, rede, journal ou atualizador.
internal sealed record DriverLabCase(string Key, string Name, string DriverClass,
    bool Staged, bool Present, bool Associated, bool UniqueId,
    string Wua, string Outcome);

internal static class DriverLabSimulation
{
    internal static IReadOnlyList<DriverLabCase> Cases { get; } = Array.AsReadOnly(new[]
    {
        new DriverLabCase("printer-success","Impressora USB: sucesso hipotético","Printer",true,true,true,true,"Oferta fictícia","Verified"),
        new DriverLabCase("camera-download","Webcam USB: download fictício falhou","Image",true,true,true,true,"Oferta fictícia","DownloadFailed"),
        new DriverLabCase("ports-reboot","Adaptador serial USB: reinício hipotético","Ports",true,true,true,true,"Oferta fictícia","PendingReboot"),
        new DriverLabCase("media","Headset USB: somente diagnóstico","MEDIA",true,true,true,true,"Oferta fictícia","Verified"),
        new DriverLabCase("hid","Controle USB: somente diagnóstico","HIDClass",true,true,true,true,"Oferta fictícia","Verified"),
        new DriverLabCase("staged","INF de impressora no Store sem hardware","Printer",true,false,false,true,"Zero ofertas","ConsultOnly"),
        new DriverLabCase("unbound","Câmera sem associação WMI","Image",false,true,false,true,"Consulta parcial","ConsultOnly"),
        new DriverLabCase("nooffers","Impressora: nenhuma oferta observada","Printer",true,true,true,true,"Zero ofertas","ConsultOnly"),
        new DriverLabCase("unqueried","Porta COM: Windows Update não consultado","Ports",true,true,true,true,"Não consultado","ConsultOnly"),
        new DriverLabCase("partial","Câmera: consulta WUA incompleta","Image",true,true,true,true,"Consulta parcial","ConsultOnly"),
        new DriverLabCase("ambiguous","Porta COM: DeviceId ambíguo","Ports",true,true,true,false,"Oferta fictícia","Verified"),
        new DriverLabCase("eula","Impressora: EULA não aceita","Printer",true,true,true,true,"Oferta fictícia","EulaBlocked"),
        new DriverLabCase("revoked","Câmera: revalidação divergente","Image",true,true,true,true,"Oferta fictícia","RevalidationBlocked"),
        new DriverLabCase("cancel","Webcam: cancelamento fictício","Image",true,true,true,true,"Oferta fictícia","Cancelled"),
        new DriverLabCase("journal","Impressora: journal fictício indisponível","Printer",true,true,true,true,"Oferta fictícia","JournalBlocked"),
        new DriverLabCase("uncertain","Porta COM: verificação inconclusiva","Ports",true,true,true,true,"Oferta fictícia","VerificationUncertain"),
        new DriverLabCase("approval","Impressora: consentimento ausente","Printer",true,true,true,true,"Oferta fictícia","ApprovalBlocked")
    });

    internal static string Format(DriverLabCase scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string result;
        if (!DriverUpdatePilot.AllowedClass(scenario.DriverClass))
            result = "BLOQUEADO: classe fora do piloto Printer/Image/Ports; MEDIA e HIDClass só para consulta.";
        else if (!scenario.Present || !scenario.Associated || !scenario.UniqueId)
            result = "BLOQUEADO: dispositivo ausente, vínculo WMI não observado ou DeviceId ambíguo.";
        else if (scenario.Wua != "Oferta fictícia")
            result = "BLOQUEADO: consulta WUA não realizada, parcial ou sem ofertas observadas.";
        else
            result = scenario.Outcome switch
            {
                "Verified" => "VERIFICADO SOMENTE NA FICÇÃO: mesma instância PnP e INF alterado, SEM PROVA REAL.",
                "DownloadFailed" => "FALHA SIMULADA: download não concluído; instalação não iniciada.",
                "PendingReboot" => "REINÍCIO PENDENTE SOMENTE NA FICÇÃO: GF não reiniciou o computador.",
                "EulaBlocked" => "BLOQUEADO NA FICÇÃO: EULA ainda não aceita.",
                "RevalidationBlocked" => "BLOQUEADO NA FICÇÃO: identidade ou oferta mudou na revalidação.",
                "Cancelled" => "CANCELADO NA FICÇÃO: nenhuma instalação iniciada.",
                "JournalBlocked" => "BLOQUEADO NA FICÇÃO: intenção durável indisponível.",
                "VerificationUncertain" => "INCONCLUSIVO NA FICÇÃO: observação PnP não comprova instalação.",
                "ApprovalBlocked" => "BLOQUEADO NA FICÇÃO: consentimento específico ausente.",
                _ => "CONSULTA SIMULADA: nenhuma instalação autorizada."
            };
        return string.Join(Environment.NewLine, new[]
        {
            "GERENCIADOR FINAL — LABORATÓRIO MULTICLASSES",
            "CENÁRIO INTEIRAMENTE FICTÍCIO — SEM CONSULTA AO COMPUTADOR",
            "Cenário: " + scenario.Name,
            "Classe declarada (SIMULADA): " + scenario.DriverClass,
            "Pacote no Driver Store (SIMULADO): " + (scenario.Staged ? "presente" : "não observado"),
            "Dispositivo PnP (SIMULADO): " + (scenario.Present ? "presente" : "ausente"),
            "Driver assinado associado por WMI (SIMULADO): " + (scenario.Associated ? "sim" : "não observado"),
            "Identidade PnP exclusiva (SIMULADA): " + (scenario.UniqueId ? "sim" : "não"),
            "Windows Update (SIMULADO): " + scenario.Wua,
            "Uma oferta fictícia nunca comprova DriverVer, rank, assinatura, compatibilidade ou versão superior.",
            "",
            "FLUXO HIPOTÉTICO — TODAS AS ETAPAS NÃO EXECUTADAS",
            "1. Inventário, PnP, INF e catálogo Store — SIMULADO.",
            "2. Consulta WUA, origem e associação por HardwareID — SIMULADO.",
            "3. Gate de classe, consentimento, EULA e silêncio — SIMULADO.",
            "4. Revalidação da oferta e identidade — NÃO EXECUTADA.",
            "5. Download fictício e nova revalidação — NÃO EXECUTADOS.",
            "6. Journal fictício em memória — NÃO GRAVADO.",
            "7. Instalação — NÃO EXECUTADA.",
            "8. Observação PnP, verificação e reinício — NÃO EXECUTADOS.",
            "",
            "DESFECHO SIMULADO: " + result,
            "NENHUM DRIVER FOI ALTERADO. Sem backend, journal em disco, rede, WMI, WUA ou SetupAPI.",
            "Esta tela não autoriza, executa ou homologa uma atualização real."
        });
    }
}
