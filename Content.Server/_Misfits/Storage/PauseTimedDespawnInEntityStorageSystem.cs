using Content.Shared._Misfits.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Spawners;

namespace Content.Server._Misfits.Storage;

public sealed class PauseTimedDespawnInEntityStorageSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<InsideEntityStorageComponent, ComponentStartup>(OnStorageEntered);
        SubscribeLocalEvent<InsideEntityStorageComponent, ComponentShutdown>(OnStorageExited);
    }

    private void OnStorageEntered(EntityUid uid, InsideEntityStorageComponent component, ComponentStartup args)
    {
        if (!TryComp<PauseTimedDespawnInEntityStorageComponent>(uid, out var pauseComp) ||
            !TryComp<TimedDespawnComponent>(uid, out var timedDespawn))
        {
            return;
        }

        // Keep compost from disappearing while a crate is intentionally storing it.
        pauseComp.PausedLifetime = timedDespawn.Lifetime;
        RemComp<TimedDespawnComponent>(uid);
    }

    private void OnStorageExited(EntityUid uid, InsideEntityStorageComponent component, ComponentShutdown args)
    {
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating ||
            !TryComp<PauseTimedDespawnInEntityStorageComponent>(uid, out var pauseComp) ||
            pauseComp.PausedLifetime is not { } pausedLifetime)
        {
            return;
        }

        var timedDespawn = EnsureComp<TimedDespawnComponent>(uid);
        timedDespawn.Lifetime = pausedLifetime;
        pauseComp.PausedLifetime = null;
    }
}