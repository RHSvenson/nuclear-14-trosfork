// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared._Misfits.Addictions;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.EntityEffects.Effects.Addiction;

/// <summary>
///     Reagent effect that suppresses active addiction symptoms.
///     Duration scales with reagent quantity.
/// </summary>
[UsedImplicitly]
public sealed partial class SuppressAddiction : EntityEffect
{
    /// <summary>
    ///     Base suppression time in seconds per 1u of reagent metabolized.
    /// </summary>
    [DataField]
    public float Time = 30f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var addictionSys = args.EntityManager.EntitySysManager.GetEntitySystem<SharedAddictionSystem>();

        var time = Time;
        if (args is EntityEffectReagentArgs reagentArgs)
            time *= reagentArgs.Scale.Float();

        addictionSys.TrySuppressAddiction(args.TargetEntity, time);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-addiction-suppression", ("chance", Probability));
    }
}
