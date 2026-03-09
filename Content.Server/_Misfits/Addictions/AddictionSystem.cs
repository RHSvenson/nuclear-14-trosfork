// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared._Misfits.Addictions;
using Content.Shared.Dataset;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Addictions;

/// <summary>
///     Server-side addiction system. Handles withdrawal popup effects
///     at random intervals while the entity is addicted and not suppressed.
/// </summary>
public sealed class AddictionSystem : SharedAddictionSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float MinEffectInterval = 10f;
    private const float MaxEffectInterval = 41f;
    private const float SuppressionDuration = 10f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddictedComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, AddictedComponent component, ComponentInit args)
    {
        component.NextEffectTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(MinEffectInterval, MaxEffectInterval));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<AddictedComponent>();

        while (query.MoveNext(out var uid, out var addicted))
        {
            UpdateSuppressed(addicted, curTime);

            if (addicted.Suppressed)
                continue;

            if (addicted.NextEffectTime == null || curTime < addicted.NextEffectTime)
                continue;

            DoAddictionEffect(uid);
            addicted.NextEffectTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(MinEffectInterval, MaxEffectInterval));
        }
    }

    private void UpdateSuppressed(AddictedComponent component, TimeSpan curTime)
    {
        component.Suppressed = component.SuppressionEndTime != null && curTime < component.SuppressionEndTime;
    }

    protected override void UpdateTime(EntityUid uid)
    {
        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        var curTime = _timing.CurTime;
        addicted.LastMetabolismTime = curTime;
        addicted.SuppressionEndTime = curTime + TimeSpan.FromSeconds(SuppressionDuration);
    }

    private void DoAddictionEffect(EntityUid uid)
    {
        var msg = GetRandomPopup();
        if (msg != null)
            _popup.PopupEntity(msg, uid, uid);
    }

    private string? GetRandomPopup()
    {
        if (!_proto.TryIndex<LocalizedDatasetPrototype>("AddictionEffects", out var dataset))
            return null;

        return _random.Pick(dataset.Values);
    }
}
