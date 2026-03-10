using Content.Server.Carrying;
using Content.Shared.Stunnable;

namespace Content.Server._N14.Carrying;

/// <summary>
/// Applies a timed stun to an entity when it is successfully carried (grabbed)
/// by another entity with <see cref="GrabStunComponent"/>.
/// </summary>
public sealed class GrabStunSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeingCarriedComponent, CarrySuccessEvent>(OnCarrySuccess);
    }

    private void OnCarrySuccess(EntityUid uid, BeingCarriedComponent _, CarrySuccessEvent args)
    {
        // Only stun if the carrier has GrabStunComponent.
        if (!TryComp<GrabStunComponent>(args.Carrier, out var grabStun))
            return;

        _stun.TryStun(uid, grabStun.StunTime, refresh: true);
    }
}
