using System.ComponentModel;

namespace GerenciadorAplicativos;

// Diagnostico de inicializacao do desinstalador fornecido pelo fabricante.
// Nunca repete o comando, nao pede para desativar o antivirus e nao apaga
// entradas de Registro quando um executavel nao pode ser iniciado.
internal static class UninstallLaunchDiagnostics
{
    internal static string Explain(int nativeCode) => nativeCode switch
    {
        2 or 3 => "O Windows não encontrou o desinstalador registrado. Verifique se a pasta do aplicativo ainda existe.",
        5 => "Acesso negado ao desinstalador. Verifique permissões e bloqueios do Windows.",
        740 => "O desinstalador exige elevação. Inicie-o pelo método oficial do fabricante.",
        1223 => "A solicitação do Windows foi cancelada. Nada será removido automaticamente.",
        193 => "O executável registrado não é válido para o Windows atual.",
        1155 => "Não há aplicativo associado ao comando de desinstalação registrado.",
        _ => "O Windows não conseguiu iniciar o desinstalador. Verifique a instalação antes de tentar novamente."
    };

    internal static int NativeCode(Exception exception) =>
        exception is Win32Exception win32 ? win32.NativeErrorCode : 0;
}
