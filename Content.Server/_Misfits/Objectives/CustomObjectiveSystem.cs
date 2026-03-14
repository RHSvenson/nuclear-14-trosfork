// #Misfits Add - Server system for custom (freeform) loremaster objectives.
// Handles the ObjectiveGetProgressEvent so that custom orders always report a valid progress
// value (0f = in-progress standing order). Without this handler, GetInfo returns null and the
// objective would silently disappear from the character menu.
using Content.Shared._Misfits.Objectives;
using Content.Shared.Objectives.Components;

namespace Content.Server._Misfits.Objectives;

/// <summary>
/// Drives custom loremaster objectives. These are freeform orders with no completion condition;
/// they stay at 0% progress permanently and are visible in the character C menu.
/// </summary>
public sealed class CustomObjectiveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Report progress so SharedObjectivesSystem.GetInfo doesn't log an error and
        // return null for the objective.  Always 0f — custom orders are standing orders.
        SubscribeLocalEvent<CustomObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, CustomObjectiveComponent comp, ref ObjectiveGetProgressEvent args)
    {
        // 0f = objective is active but not yet complete (standing order).
        args.Progress = 0f;
    }
}
