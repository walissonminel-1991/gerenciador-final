namespace GerenciadorAplicativos;

// Descreve apenas códigos relatados pelo PnP. Não diagnostica hardware,
// versão obsoleta, compatibilidade nem necessidade de atualização.
internal static class DriverPnpGuidance
{
    internal static string Describe(int? code) => code switch
    {
        null => "Estado PnP não informado. Isso não comprova falha, ausência de driver ou dispositivo.",
        0 => "Windows não informou problema PnP. Isso não garante integridade nem versão atualizada.",
        10 => "PnP 10: o Windows informa que o dispositivo não consegue iniciar. Verifique propriedades e eventos; a causa não está comprovada.",
        22 => "PnP 22: dispositivo desativado segundo o Windows. Confira se a desativação foi intencional antes de agir.",
        28 => "PnP 28: o Windows informa drivers não instalados para este dispositivo. Confirme o hardware e uma origem oficial; nenhuma oferta está comprovada.",
        31 => "PnP 31: o Windows não consegue carregar os drivers necessários. Não há prova de qual pacote resolveria o problema.",
        43 => "PnP 43: o Windows interrompeu o dispositivo após um problema relatado. Este código não distingue hardware, firmware ou software.",
        52 => "PnP 52: o Windows não conseguiu verificar a assinatura digital necessária. Não desative a verificação de assinatura automaticamente.",
        > 0 => "Código PnP relatado pelo Windows, sem explicação específica neste catálogo. Consulte as propriedades do dispositivo.",
        _ => "Código PnP não válido para esta consulta. Não inferir falha nem instalar driver."
    };
}
