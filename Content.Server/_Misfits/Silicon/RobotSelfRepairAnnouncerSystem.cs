// #Misfits Change - Server system for robot self-repair announcer popups.
using Content.Shared._Misfits.Silicon;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Silicon;

/// <summary>
/// Shows popup messages when a robot's hull integrity is poor, and when passive self-repair begins.
/// </summary>
public sealed class RobotSelfRepairAnnouncerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RobotSelfRepairAnnouncerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, RobotSelfRepairAnnouncerComponent component, DamageChangedEvent args)
    {
        var curTime = _timing.CurTime;

        if (args.DamageIncreased && args.DamageDelta != null)
        {
            // Only warn when a meaningful hit lands and total damage is at a concerning level
            var delta = args.DamageDelta.GetTotal();
            if (delta < (float) component.DamageDeltaThreshold)
                return;

            if (args.Damageable.TotalDamage < (float) component.MinTotalDamageForHullWarning)
                return;

            if (component.NextHullWarningTime != null && curTime < component.NextHullWarningTime)
                return;

            component.NextHullWarningTime = curTime + component.HullWarningCooldown;
            _popup.PopupEntity(Loc.GetString("robot-self-repair-hull-warning"), uid, PopupType.LargeCaution);
        }
        else if (!args.DamageIncreased)
        {
            // Self-repair announcement: fire when healing while meaningfully damaged
            if (args.Damageable.TotalDamage < (float) component.MinDamageForRepairPopup)
                return;

            if (component.NextRepairAnnounceTime != null && curTime < component.NextRepairAnnounceTime)
                return;

            component.NextRepairAnnounceTime = curTime + component.RepairAnnounceCooldown;
            _popup.PopupEntity(Loc.GetString("robot-self-repair-initiating"), uid, PopupType.Medium);
        }
    }
}
