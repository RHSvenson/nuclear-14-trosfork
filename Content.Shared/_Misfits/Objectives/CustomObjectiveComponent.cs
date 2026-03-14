// #Misfits Add - Marker component for admin-issued custom (freeform) objectives.
// The entity's EntityName and EntityDescription are set server-side from admin input.
// CustomObjectiveSystem handles ObjectiveGetProgressEvent so progress is always reported (0 = in progress).
using Robust.Shared.GameObjects;

namespace Content.Shared._Misfits.Objectives;

/// <summary>
/// Marks an objective entity as a custom loremaster order.
/// Title and description come from the entity metadata, set server-side by LoreMasterSystem.
/// Progress is always 0f (standing order — no auto-completion).
/// </summary>
[RegisterComponent]
public sealed partial class CustomObjectiveComponent : Component
{
}
