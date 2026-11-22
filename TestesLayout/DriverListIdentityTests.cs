using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Seleção da lista WinForms REAL; todos os registros são sintéticos.
// Nunca executa WMI, SetupAPI, COM, INF, rede ou grava histórico.
internal static class DriverListIdentityTests
{
    internal static void Run()
    {
        var a = new DriverInventoryEntry(@"PCI\G113\A", "Mesmo nome sintético",
            "Fornecedor A", "1.0", "oem11.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1111&DEV_0001"]
        };
        var b = new DriverInventoryEntry(@"PCI\G113\B", "Mesmo nome sintético",
            "Fornecedor B", "2.0", "oem22.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_2222&DEV_0002"]
        };
        var differentInf = b with { InfName = "oem99.inf" };
        var copy = b with { }; // Record.Equals pode aceitar; referência NÃO é a selecionada.
        if (!b.Equals(copy) || ReferenceEquals(b, copy))
            throw new Exception("G1.13: fixture não reproduz a igualdade estrutural enganosa.");

        using var host = new Form
        {
            Text = "G1.13 Identidade sintética",
            ClientSize = new Size(800, 510), ShowInTaskbar = false
        };
        using var list = new DarkDriverList { Dock = DockStyle.Fill };
        host.Controls.Add(list);
        host.Show();
        Application.DoEvents();
        int changes = 0;
        list.SelectionChanged += (_, _) => changes++;
        var key = typeof(DarkDriverList).GetMethod("OnKeyDown",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var mouse = typeof(DarkDriverList).GetMethod("OnMouseDown",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var select = typeof(DarkDriverList).GetMethod("SelectIndex",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        void Down() => key.Invoke(list, [new KeyEventArgs(Keys.Down)]);
        void Click(int index)
        {
            int row = DriverRowGeometry.Height(list.DeviceDpi);
            mouse.Invoke(list, [new MouseEventArgs(MouseButtons.Left, 1,
                Math.Min(20, Math.Max(1, list.ClientSize.Width / 3)),
                index * row + Math.Max(1, row / 2), 0)]);
        }
        void Current(DriverInventoryEntry expected, string context)
        {
            if (!ReferenceEquals(list.SelectedItem, expected) ||
                list.AccessibleDescription?.Contains(expected.DeviceId, StringComparison.Ordinal) != true ||
                list.AccessibleDescription?.Contains(expected.InfName, StringComparison.Ordinal) != true)
                throw new Exception("G1.13: seleção ou descrição acessível errada em " + context);
        }
        void None(string context)
        {
            if (list.SelectedItem is not null ||
                list.AccessibleDescription?.Contains("Nenhum dispositivo selecionado") != true)
                throw new Exception("G1.13: seleção ambígua não foi invalidada em " + context);
        }

        list.SetItems([a, b]);
        Down();
        Current(a, "seta primeiro item");
        Down();
        Current(b, "seta segundo item com nome idêntico");
        int beforeReorder = changes;
        list.SetItems([b, a]);
        Current(b, "ordem invertida sem mudar a identidade");
        if (list.SelectedIndex != 0 || changes != beforeReorder)
            throw new Exception("G1.13: inversão de ordem trocou a identidade da seleção.");

        // Substituir por record clone sem mudar campos não pode transferir foco
        // ou habilitar prova de dispositivo que não foi escolhido explicitamente.
        list.SetItems([copy, a]);
        None("substituição por objeto estruturalmente igual");
        if (changes != beforeReorder + 1)
            throw new Exception("G1.13: troca de identidade não notificou a página.");

        list.SetItems([a, b]);
        select.Invoke(list, [1]);
        Current(b, "seleção antes de duplicar DeviceId");
        list.SetItems([a, b, differentInf]);
        None("duplicata conflitante da mesma instância");

        // A seleção manual é somente visual: anunciar a duplicidade,
        // nunca promover a linha escolhida a prova INF/WUA inequívoca.
        select.Invoke(list, [2]);
        Current(differentInf, "seleção manual conflitante");
        if (list.AccessibleDescription?.Contains("Associação de INF/WUA não é inequívoca") != true)
            throw new Exception("G1.13: duplicata não anunciada à acessibilidade.");
        var duplicatedInventory = new DriverInventoryResult([b, differentInf],
            false, "inventário sintético com DeviceId duplicado");
        var guard = new DriverReportPublicationGuard(duplicatedInventory, differentInf, 1);
        if (guard.IsCurrent(duplicatedInventory, differentInf, 1) ||
            guard.CanPublishInf(duplicatedInventory, differentInf, 1,
                CancellationToken.None, new DriverInfVerificationResult(DriverInfTrust.VerifiedInf,
                    new string('A', 64), "catalogo.cat", "Fornecedor de teste")))
            throw new Exception("G1.13: duplicata recebeu falsa autoridade INF.");

        list.SetItems([differentInf, b]);
        None("reordenação de duplicatas conflitantes");

        // Mesmo objeto repetido em duas posições: o FindIndex antigo retornava
        // a primeira linha, mesmo quando a segunda havia sido selecionada.
        list.SetItems([b, b]);
        select.Invoke(list, [1]);
        if (list.SelectedIndex != 1) throw new Exception("G1.13: pré-condição duplicata idêntica");
        list.SetItems([b, b]);
        None("repetição idêntica por referência");

        list.SetItems([a, b]);
        Click(1); // evento MouseDown do controle real
        Current(b, "clique do mouse");
        list.SetItems([a]);
        None("filtro excluindo seleção");

        // Refaça seleção com troca de tamanho e scroll sem reconectar dispositivo
        // de mesmo nome/INF; não equivale a teste de DPI físico diferente.
        foreach (var size in new[] { new Size(540, 320), new Size(500, 210),
            new Size(1200, 720), new Size(1450, 820) })
        {
            list.Size = size;
            list.SetItems([a, b]);
            select.Invoke(list, [1]);
            Current(b, $"viewport {size.Width}x{size.Height}");
            list.SetItems([b, a]);
            Current(b, $"reordenação viewport {size.Width}x{size.Height}");
            list.SetItems([]);
            None($"vazio viewport {size.Width}x{size.Height}");
        }
        if (changes < 10) throw new Exception("G1.13: eventos de seleção insuficientes.");
        Console.WriteLine("OK G1.13: referência única, nomes iguais, duplicatas DeviceId, teclado/mouse e acessibilidade");
        host.Close();

        // UX: ações são hit-testadas sobre a mesma geometria pintada, sem
        // botões por dispositivo, WUA, PnPUtil ou escrita no Windows.
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        foreach (int dpi in new[] { 96, 120, 144, 192 })
        foreach (int logicalWidth in new[] { 350, 500, 1200 })
        {
            int width = DriverRowGeometry.Scale(logicalWidth, dpi);
            var row = new Rectangle(0, 0, width, DriverRowGeometry.Height(dpi, width));
            var uninstall = DriverRowGeometry.Uninstall(row, dpi, font);
            int minText = TextRenderer.MeasureText("Desinstalar", font,
                Size.Empty, TextFormatFlags.NoPadding).Width;
            if (!row.Contains(uninstall) ||
                uninstall.IntersectsWith(DriverRowGeometry.Status(row, dpi)) ||
                uninstall.Width < minText ||
                !row.Contains(DriverRowGeometry.Update(row, dpi, font)) ||
                DriverRowGeometry.Update(row, dpi, font).IntersectsWith(uninstall))
                throw new Exception($"UX Drivers: desinstalação lateral cortada/sobreposta em {logicalWidth}, DPI {dpi}");
        }
        using var actionHost = new Form
        {
            ClientSize = new Size(500, 500), ShowInTaskbar = false
        };
        using var actionsList = new DarkDriverList { Dock = DockStyle.Fill };
        actionHost.Controls.Add(actionsList);
        actionHost.Show();
        Application.DoEvents();
        actionsList.SetItems([a, b]);
        actionsList.SetActionPolicy(true, _ => new DriverSilentUninstallDecision(true, "Fixture"));
        if (DarkDriverList.RemovalActionLabel(true) != "Desinstalar" ||
            DarkDriverList.RemovalActionLabel(false) != "Gerenciar")
            throw new Exception("UX Drivers: rota protegida rotulada como desinstalação direta.");
        int requested = 0;
        DriverInventoryEntry? lastRequested = null;
        DriverCardAction lastAction = DriverCardAction.Details;
        actionsList.ActionRequested += (item, action) =>
        {
            requested++;
            lastRequested = item;
            lastAction = action;
        };
        void ClickUninstall(int index)
        {
            int width = Math.Max(1, actionsList.ClientSize.Width - 20);
            int height = DriverRowGeometry.Height(actionsList.DeviceDpi, width);
            var row = new Rectangle(0, index * height, width, height);
            var target = DriverRowGeometry.Uninstall(row, actionsList.DeviceDpi, font);
            mouse.Invoke(actionsList, [new MouseEventArgs(MouseButtons.Left, 1,
                target.Left + target.Width / 2, target.Top + target.Height / 2, 0)]);
        }
        // Ação de atualizar só existe para DeviceId explicitamente habilitado.
        // Um badge WUA consultivo, isoladamente, nunca dispara atualização.
        var updateRow = new Rectangle(0, 0,
            Math.Max(1, actionsList.ClientSize.Width - 20),
            DriverRowGeometry.Height(actionsList.DeviceDpi,
                Math.Max(1, actionsList.ClientSize.Width - 20)));
        var updateTarget = DriverRowGeometry.Update(updateRow, actionsList.DeviceDpi, font);
        void ClickUpdate() => mouse.Invoke(actionsList,
            [new MouseEventArgs(MouseButtons.Left, 1,
                updateTarget.Left + updateTarget.Width / 2,
                updateTarget.Top + updateTarget.Height / 2, 0)]);
        ClickUpdate();
        if (requested != 0) throw new Exception("UX Drivers: oferta consultiva habilitou atualizar.");
        actionsList.SetEligibleUpdateDevices([a.DeviceId]);
        ClickUpdate();
        if (requested != 1 || !ReferenceEquals(lastRequested, a) ||
            lastAction != DriverCardAction.Update)
            throw new Exception("UX Drivers: atualizar não preservou a identidade elegível.");
        actionsList.SetEligibleUpdateDevices(Array.Empty<string>());
        requested = 0;
        ClickUninstall(0);
        if (requested != 1 || !ReferenceEquals(lastRequested, a) ||
            lastAction != DriverCardAction.Uninstall)
            throw new Exception("UX Drivers: clique não preservou identidade do cartão.");
        ClickUninstall(1);
        if (requested != 2 || !ReferenceEquals(lastRequested, b) ||
            lastAction != DriverCardAction.Uninstall)
            throw new Exception("UX Drivers: desinstalação não identificou objeto selecionado.");
        // O botão existe também no cartão de dispositivo protegido.
        // O handler de produção é responsável pelo encaminhamento manual;
        // o motor de remoção continua recusando a operação direta.
        actionsList.SetActionPolicy(true, _ =>
            new DriverSilentUninstallDecision(false, "Dispositivo protegido"));
        if (actionsList.AccessibleDescription?.Contains(
                "Gerenciar: encaminha ao Gerenciador de Dispositivos", StringComparison.Ordinal) != true ||
            actionsList.AccessibleDescription?.Contains(
                "sem remoção automática pelo GF", StringComparison.Ordinal) != true)
            throw new Exception("UX Drivers: ação de protegido promete desinstalação em vez de gestão manual.");
        ClickUninstall(0);
        if (requested != 3 || !ReferenceEquals(lastRequested, a) ||
            lastAction != DriverCardAction.Uninstall || actionsList.CanUninstall(a))
            throw new Exception("UX Drivers: ação manual em protegido foi omitida ou autorizou remoção.");
        actionsList.SetActionPolicy(false, _ =>
            new DriverSilentUninstallDecision(true, "Fixture"));
        ClickUninstall(0);
        if (requested != 3)
            throw new Exception("UX Drivers: ação foi emitida após revogar autoridade.");
        actionHost.Close();
        var priority = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { b.DeviceId };
        var ordered = DriverListFilters.Apply([a, b], "Todos", "", priority);
        if (!ReferenceEquals(ordered[0], b) || !ReferenceEquals(ordered[1], a))
            throw new Exception("UX Drivers: oferta elegível não ficou no topo.");
        Console.WriteLine("OK UX Drivers: geometria DPI, atualização elegível, ordenação e revogação do clique");
    }
}
