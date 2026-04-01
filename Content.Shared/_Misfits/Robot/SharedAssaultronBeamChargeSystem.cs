// Shared charge-up gate for the Assaultron beam / tesla weapons.
// Runs on BOTH client (prediction) and server so the client also blocks shots
// during the charge phase, preventing gunshot sound / animation spam.
// Emote broadcasting is handled server-side by AssaultronBeamChargeEmoteSystem.

using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Robot;

public sealed class SharedAssaultronBeamChargeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultronBeamChargeComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<AssaultronBeamChargeComponent, GunShotEvent>(OnGunShot);
    }

    private void OnAttemptShoot(EntityUid uid, AssaultronBeamChargeComponent comp, ref AttemptShootEvent args)
    {
        var now = _timing.CurTime;

        // Still in post-fire cooldown — block the shot silently.
        if (!comp.IsCharging && now < comp.CooldownEndTime)
        {
            args.Cancelled = true;
            return;
        }

        // Charge phase complete — allow the shot through.
        if (comp.IsCharging && now >= comp.ChargeEndTime)
        {
            var preFire = new AssaultronBeamPreFireCheckEvent();
            RaiseLocalEvent(uid, ref preFire);

            // Server-only checks (battery, etc.) can veto the shot here.
            if (preFire.Cancelled)
            {
                comp.IsCharging = false;
                comp.ReadyToFire = false;
                args.Cancelled = true;
                return;
            }

            comp.IsCharging = false;
            comp.ReadyToFire = true;
            // Shot allowed — GunShotEvent handles cooldown and fire emote.
            return;
        }

        // Currently charging but not yet ready — keep blocking.
        if (comp.IsCharging)
        {
            args.Cancelled = true;
            return;
        }

        // Idle state — start the charge-up phase.
        comp.IsCharging = true;
        comp.ChargeEndTime = now + TimeSpan.FromSeconds(comp.ChargeDuration);

        // Notify server to broadcast the charge emote.
        var ev = new AssaultronChargeStartedEvent(comp.ChargeEmoteLocale);
        RaiseLocalEvent(uid, ref ev);

        args.Cancelled = true;
    }

    private void OnGunShot(EntityUid uid, AssaultronBeamChargeComponent comp, ref GunShotEvent args)
    {
        // Shot actually fired — start cooldown.
        comp.ReadyToFire = false;
        comp.CooldownEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CooldownDuration);

        // Notify server to broadcast the fire emote.
        var ev = new AssaultronBeamFiredEvent(comp.FireEmoteLocale);
        RaiseLocalEvent(uid, ref ev);
    }
}
