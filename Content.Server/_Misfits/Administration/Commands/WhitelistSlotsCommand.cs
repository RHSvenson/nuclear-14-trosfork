// #Misfits Change - Console command to open the job slots admin panel
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Opens the Job Slots panel so admins can view and adjust per-job slot counts
/// on the active station without needing to select a player first.
/// </summary>
[AdminCommand(AdminFlags.Whitelist)]
public sealed class WhitelistSlotsCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "whitelistslots";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var ui = new JobSlotsEui();
        _eui.OpenEui(ui, player);
    }
}
