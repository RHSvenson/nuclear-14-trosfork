// #Misfits Change /Add/ - Shared server-side resuscitation helper for defibrillators and smelling salts.
using Content.Server.Atmos.Rotting;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Medical;

public sealed class ResuscitationSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly RottingSystem _rotting = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public void SendAttemptEmote(EntityUid target, EntityUid item)
    {
        _chat.TrySendInGameICMessage(target,
            Loc.GetString("resuscitation-attempt-emote", ("item", Name(item))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
    }

    public bool CanResuscitate(EntityUid target, bool targetCanBeAlive = false, bool canReviveCrit = true, MobStateComponent? mobState = null)
    {
        if (!Resolve(target, ref mobState, false))
            return false;

        if (!targetCanBeAlive && _mobState.IsAlive(target, mobState))
            return false;

        if (!targetCanBeAlive && !canReviveCrit && _mobState.IsCritical(target, mobState))
            return false;

        return true;
    }

    public ResuscitationResult TryResuscitate(
        EntityUid source,
        EntityUid target,
        EntityUid user,
        DamageSpecifier reviveHeal,
        string? reviveDoKey = null,
        MobStateComponent? mobState = null,
        MobThresholdsComponent? thresholds = null)
    {
        if (!Resolve(target, ref mobState, ref thresholds, false))
            return default;

        if (_rotting.IsRotten(target))
            return new ResuscitationResult(false, true, false);

        var revived = false;
        if (_mobState.IsDead(target, mobState))
            _damageable.TryChangeDamage(target, reviveHeal, true, origin: source);

        if (_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold) &&
            TryComp<DamageableComponent>(target, out var damageableComponent) &&
            damageableComponent.TotalDamage < threshold)
        {
            _mobState.ChangeMobState(target, MobState.Critical, mobState, user);
            revived = true;

            if (!string.IsNullOrWhiteSpace(reviveDoKey))
            {
                _chat.TrySendInGameDoMessage(target,
                    Loc.GetString(reviveDoKey, ("target", target)),
                    ChatTransmitRange.Normal,
                    hideLog: true,
                    ignoreActionBlocker: true);
            }
        }

        ICommonSession? session = null;
        if (_mind.TryGetMind(target, out _, out var mind) &&
            mind.Session is { } playerSession)
        {
            session = playerSession;

            if (mind.CurrentEntity != target)
                _euiManager.OpenEui(new ReturnToBodyEui(mind, _mind), session);
        }

        return new ResuscitationResult(revived, false, session != null);
    }
}

public readonly record struct ResuscitationResult(bool Revived, bool Rotten, bool HasMindSession);