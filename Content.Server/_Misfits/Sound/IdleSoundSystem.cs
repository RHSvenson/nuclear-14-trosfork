// Misfits Change - System to suppress idle sounds during combat and when dead
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Sound;
using Content.Shared.Sound.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Misfits.Sound;

/// <summary>
/// Temporarily disables <see cref="SpamEmitSoundComponent"/> when an entity with
/// <see cref="IdleSoundComponent"/> performs an attack, then re-enables it after a cooldown.
/// Also permanently disables idle sounds when the entity is no longer alive.
/// </summary>
public sealed class IdleSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedEmitSoundSystem _emitSound = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdleSoundComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<IdleSoundComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMeleeAttack(Entity<IdleSoundComponent> entity, ref MeleeAttackEvent args)
    {
        Suppress(entity);
    }

    private void OnMobStateChanged(Entity<IdleSoundComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
        {
            // Permanently disable idle sounds — the mob is dead or incapacitated.
            entity.Comp.Suppressed = true;
            entity.Comp.CooldownRemaining = 0f;
            _emitSound.SetEnabled((entity.Owner, (SpamEmitSoundComponent?) null), false);
        }
        else
        {
            // Mob came back to life; let idle sounds resume.
            entity.Comp.Suppressed = false;
            _emitSound.SetEnabled((entity.Owner, (SpamEmitSoundComponent?) null), true);
        }
    }

    private void Suppress(Entity<IdleSoundComponent> entity)
    {
        entity.Comp.CooldownRemaining = entity.Comp.CooldownDuration;

        if (entity.Comp.Suppressed)
            return;

        entity.Comp.Suppressed = true;
        _emitSound.SetEnabled((entity.Owner, (SpamEmitSoundComponent?) null), false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IdleSoundComponent>();
        while (query.MoveNext(out var uid, out var idle))
        {
            if (!idle.Suppressed)
                continue;

            idle.CooldownRemaining -= frameTime;

            if (idle.CooldownRemaining > 0f)
                continue;

            // Do not re-enable sounds if the mob is no longer alive.
            if (!_mobState.IsAlive(uid))
                continue;

            idle.Suppressed = false;
            _emitSound.SetEnabled((uid, (SpamEmitSoundComponent?) null), true);
        }
    }
}
