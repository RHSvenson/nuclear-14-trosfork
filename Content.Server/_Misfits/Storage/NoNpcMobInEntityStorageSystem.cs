using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Storage;

/// <summary>
/// Prevents NPC mobs (raiders, animals, etc.) from being inserted into
/// EntityStorage containers such as crates and lockers.
/// Player-controlled entities (ActorComponent present) are still allowed in,
/// preserving normal gameplay for human players and accepted ghost roles.
/// </summary>
public sealed class NoNpcMobInEntityStorageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, InsertIntoEntityStorageAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(EntityUid uid, MobStateComponent component, ref InsertIntoEntityStorageAttemptEvent args)
    {
        // Allow players and inhabited ghost roles through — block pure NPCs.
        if (HasComp<ActorComponent>(uid))
            return;

        args.Cancelled = true;
    }
}
