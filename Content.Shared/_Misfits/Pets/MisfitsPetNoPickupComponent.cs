// #Misfits Add - Marker component for pet ghost role mobs that have Hands for dragging
// but should not be able to pick items into inventory slots.
// Paired with MisfitsPetNoPickupSystem which cancels PickupAttemptEvent.

using Robust.Shared.GameObjects;

namespace Content.Shared._Misfits.Pets;

/// <summary>
/// Marks a player-controlled pet mob as unable to pick up items into hand slots.
/// The pet can still drag/pull other entities via the Puller component.
/// </summary>
[RegisterComponent]
public sealed partial class MisfitsPetNoPickupComponent : Component
{
}
