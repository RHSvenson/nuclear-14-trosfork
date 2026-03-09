// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Addictions;

/// <summary>
///     Shared addiction system. Handles applying and suppressing addictions
///     via the StatusEffectsSystem. Server overrides provide update/popup logic.
/// </summary>
public abstract class SharedAddictionSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    /// <summary>
    ///     Status effect key used for the addiction status.
    /// </summary>
    public ProtoId<StatusEffectPrototype> StatusEffectKey = "Addicted";

    /// <summary>
    ///     Server-side time bookkeeping for suppression windows.
    /// </summary>
    protected abstract void UpdateTime(EntityUid uid);

    /// <summary>
    ///     Attempts to apply an addiction to the entity.
    ///     If the entity already has the effect, extends its duration.
    /// </summary>
    public virtual void TryApplyAddiction(EntityUid uid, float addictionTime, StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return;

        UpdateTime(uid);

        if (!_statusEffects.HasStatusEffect(uid, StatusEffectKey, status))
        {
            _statusEffects.TryAddStatusEffect<AddictedComponent>(
                uid,
                StatusEffectKey,
                TimeSpan.FromSeconds(addictionTime),
                false,
                status);
        }
        else
        {
            _statusEffects.TryAddTime(uid, StatusEffectKey, TimeSpan.FromSeconds(addictionTime), status);
        }
    }

    /// <summary>
    ///     Suppresses active addiction symptoms for a duration.
    /// </summary>
    public virtual void TrySuppressAddiction(EntityUid uid, float duration)
    {
        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        UpdateAddictionSuppression(uid, addicted, duration);
    }

    /// <summary>
    ///     Marks the addiction as suppressed and updates the suppression end time.
    /// </summary>
    protected void UpdateAddictionSuppression(EntityUid uid, AddictedComponent component, float duration)
    {
        component.Suppressed = true;
        Dirty(uid, component);
    }
}
