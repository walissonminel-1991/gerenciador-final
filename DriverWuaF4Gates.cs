namespace GerenciadorAplicativos;

// Contrato conservador: update WUA unico nao comprova dispositivo unico.
internal static class DriverWuaF4Gates
{
    internal static bool OneEntry(int count) => count == 1;
    internal static bool EntryMatches(DriverUpdatePilotChoice choice,
        string? hardware, string? provider, string? driverClass) =>
        choice is not null && DriverUpdatePilot.AllowedClass(choice.DriverClass) &&
        string.Equals(hardware, choice.HardwareId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(provider, choice.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(driverClass, choice.DriverClass, StringComparison.OrdinalIgnoreCase);
}
