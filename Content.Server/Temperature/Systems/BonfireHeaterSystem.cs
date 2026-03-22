// #Misfits Change /Fix/ Gate bonfire heating and ambience behind the live fire state.
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Temperature.Components;
using Content.Shared.Audio;
using Content.Shared.Mobs.Components;
using Content.Shared.Placeable;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Temperature;

namespace Content.Server.Temperature.Systems;

/// <summary>
/// Handles <see cref="BonfireHeaterComponent"/> updating and events.
/// </summary>
public sealed class BonfireHeaterSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SolutionHeaterSystem _solutionHeater = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Gate the step-trigger so it only fires when the bonfire is actually lit,
        // and only on mobs — not dropped items or placed food sitting on the surface.
        SubscribeLocalEvent<BonfireHeaterComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    // #Misfits Add: Cancel step-trigger attempts if the bonfire is not on fire or the tripper
    // is not a mob; prevents unconditional burning on an unlit bonfire and avoids igniting
    // food items placed on the surface.
    private void OnStepTriggerAttempt(EntityUid uid, BonfireHeaterComponent comp, ref StepTriggerAttemptEvent args)
    {
        var isHotEvent = new IsHotEvent();
        RaiseLocalEvent(uid, isHotEvent);
        if (!isHotEvent.IsHot || !HasComp<MobStateComponent>(args.Tripper))
            args.Cancelled = true;
    }

    public override void Update(float deltaTime)
    {
        var query = EntityQueryEnumerator<BonfireHeaterComponent, ItemPlacerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var placer))
        {
            var isHotEvent = new IsHotEvent();
            RaiseLocalEvent(uid, isHotEvent);

            _ambientSound.SetAmbience(uid, isHotEvent.IsHot);

            if (TryComp<SolutionHeaterComponent>(uid, out _))
            {
                if (isHotEvent.IsHot)
                    _solutionHeater.TryTurnOn(uid, placer);
                else
                    _solutionHeater.TurnOff(uid);
            }

            if (!isHotEvent.IsHot)
                continue;

            var heatToAdd = comp.BaseHeatMultiplier;
            foreach (var ent in placer.PlacedEntities)
            {
                // #Misfits Fix: Propagate external heat into InternalTemperatureComponent BEFORE
                // ChangeHeat fires OnTemperatureChangeEvent. Cooking construction graphs check
                // internal temperature first, so internal temp must be up-to-date at the moment
                // the event is handled — the upstream conduction loop is disabled in this fork.
                _temperature.ConductToInternalTemperature(ent, deltaTime);
                _temperature.ChangeHeat(ent, heatToAdd, true);
            }
        }
    }
}
