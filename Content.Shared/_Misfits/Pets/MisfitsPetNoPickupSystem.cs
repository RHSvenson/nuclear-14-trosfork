// #Misfits Add - Cancels item pickup for pet ghost role mobs that have Hands
// but should not be able to pick items into inventory slots.
// Pattern follows VentriloquistPuppetSystem (Content.Shared/Puppet/SharedVentriloquistPuppetSystem.cs).

using Content.Shared.Item;

namespace Content.Shared._Misfits.Pets;

/// <summary>
/// Prevents pet mobs with <see cref="MisfitsPetNoPickupComponent"/> from picking up items.
/// They retain the Hands component (for dragging/interaction) but cannot store items.
/// </summary>
public sealed class MisfitsPetNoPickupSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // Cancel any pickup attempt on entities tagged as pet-no-pickup
        SubscribeLocalEvent<MisfitsPetNoPickupComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, MisfitsPetNoPickupComponent component, PickupAttemptEvent args)
    {
        // Pets can drag/pull via Puller but cannot pick items into hand slots
        args.Cancel();
    }
}
