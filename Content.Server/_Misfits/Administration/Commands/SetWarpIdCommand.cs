using Content.Server.Administration;
using Content.Server.Warps;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Live admin helper for editing warp links without relying on VV null handling.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class SetWarpIdCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "setwarpid";
    public string Description => "Sets Warper/WarpPoint IDs on an entity independently. Required for bidirectional ladders where each component needs a different ID.";

    // For one-way warps (entity only has one component):
    //   setwarpid <uid> <destination-id>
    //
    // For bidirectional ladders (entity has BOTH WarperComponent AND WarpPointComponent):
    //   setwarpid <uid> <destination-id> <self-id> [location]
    //   - destination-id  = WarperComponent.ID  — where clicking THIS entity sends you
    //   - self-id         = WarpPointComponent.ID — the ID OTHER entities use to land HERE
    //   - location        = optional WarpPoint display name
    //
    // Example ladder pair:
    //   setwarpid 111 bunker_bottom bunker_top    (top: sends down, lands as "bunker_top")
    //   setwarpid 222 bunker_top    bunker_bottom (bottom: sends up, lands as "bunker_bottom")
    public string Help => "setwarpid <uid> <destination-id> [self-point-id] [location]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length > 4)
        {
            shell.WriteError(Help);
            return;
        }

        if (!int.TryParse(args[0], out var netId))
        {
            shell.WriteError($"Invalid entity uid: {args[0]}");
            return;
        }

        var netEntity = new NetEntity(netId);
        if (!_entManager.TryGetEntity(netEntity, out var uid))
        {
            shell.WriteError($"Entity {netId} was not found.");
            return;
        }

        if (uid is not { Valid: true } entity)
        {
            shell.WriteError($"Entity {netId} is not valid.");
            return;
        }

        // arg[1] = where clicking this entity sends the player (WarperComponent.ID)
        var destinationId = args[1];
        // arg[2] = optional: what ID this entity registers as for others to land on (WarpPointComponent.ID)
        // If omitted, WarpPointComponent.ID is also set to destinationId (one-way / single-component case)
        var selfPointId = args.Length >= 3 ? args[2] : destinationId;
        var location    = args.Length == 4 ? args[3] : null;

        var changedAny = false;

        if (_entManager.TryGetComponent<WarperComponent>(entity, out var warper))
        {
            warper.ID = destinationId;
            _entManager.Dirty(entity, warper);
            changedAny = true;
        }

        if (_entManager.TryGetComponent<WarpPointComponent>(entity, out var warpPoint))
        {
            warpPoint.ID = selfPointId;
            if (location != null)
                warpPoint.Location = location;

            _entManager.Dirty(entity, warpPoint);
            changedAny = true;
        }

        if (!changedAny)
        {
            shell.WriteError($"{_entManager.ToPrettyString(entity)} has neither WarperComponent nor WarpPointComponent.");
            return;
        }

        var parts = $"destination='{destinationId}', point='{selfPointId}'";
        if (location != null)
            parts += $", location='{location}'";
        shell.WriteLine($"Updated {_entManager.ToPrettyString(entity)}: {parts}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint("<entity uid>");

        if (args.Length == 2)
            return CompletionResult.FromHint("<warp id>");

        if (args.Length == 3)
            return CompletionResult.FromHint("[location]");

        return CompletionResult.Empty;
    }
}