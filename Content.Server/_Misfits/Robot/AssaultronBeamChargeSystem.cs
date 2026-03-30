// Assaultron beam charge-up system. When the player holds fire (FullAuto), 
// the first attempt starts a 2-second charge phase with an area emote.
// After the charge completes the next poll allows the shot through, broadcasts 
// a "firing" emote, and enters a 3-second cooldown. Total cycle ≈ 5 seconds.

using Content.Server.Chat.Systems;
using Content.Shared._Misfits.Robot;
using Content.Shared.Chat;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Robot;

public sealed class AssaultronBeamChargeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

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
        if (now < comp.CooldownEndTime)
        {
            args.Cancelled = true;
            return;
        }

        // Charge phase complete — allow the shot through.
        if (comp.IsCharging && now >= comp.ChargeEndTime)
        {
            comp.IsCharging = false;
            comp.ReadyToFire = true;
            // Shot allowed — GunShotEvent will handle the fire emote + cooldown.
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

        _chat.TrySendInGameICMessage(
            uid,
            Loc.GetString(comp.ChargeEmoteLocale),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);

        args.Cancelled = true;
    }

    private void OnGunShot(EntityUid uid, AssaultronBeamChargeComponent comp, ref GunShotEvent args)
    {
        // Shot actually fired — broadcast fire emote and start cooldown.
        comp.ReadyToFire = false;
        comp.CooldownEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CooldownDuration);

        _chat.TrySendInGameICMessage(
            uid,
            Loc.GetString(comp.FireEmoteLocale),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }
}
