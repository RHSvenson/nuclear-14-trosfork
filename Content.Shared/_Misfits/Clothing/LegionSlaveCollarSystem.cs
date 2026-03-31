using Content.Shared._Misfits.Clothing;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Construction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Lock;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Misfits.Clothing;

/// <summary>
/// Handles slave collar lock rules, rescue cutting, and generated key assignment.
/// </summary>
public sealed class LegionSlaveCollarSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LegionSlaveCollarComponent, IsUnequippingAttemptEvent>(OnUnequippingAttempt);
        SubscribeLocalEvent<LegionSlaveCollarComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LegionSlaveCollarComponent, LegionSlaveCollarCutDoAfterEvent>(OnCollarCut);
        SubscribeLocalEvent<LegionSlaveCollarComponent, ConstructionCompletedEvent>(OnConstructionCompleted);
    }

    private void OnUnequippingAttempt(Entity<LegionSlaveCollarComponent> ent, ref IsUnequippingAttemptEvent args)
    {
        if (args.Equipment != ent.Owner)
            return;

        if (!TryComp<LockComponent>(ent, out var lockComp) || !lockComp.Locked)
            return;

        // If the remover has valid access (Centurion ID, Legion key, or generated collar key), unlock and proceed.
        if (_lock.TryUnlock(ent, args.Unequipee, lockComp, skipDoAfter: true))
            return;

        args.Cancel();
    }

    private void OnInteractUsing(Entity<LegionSlaveCollarComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<LockComponent>(ent, out var lockComp) || !lockComp.Locked)
            return;

        // Rescue path for non-Legion: cut open the collar with a cutting tool.
        args.Handled = _tools.UseTool(args.Used, args.User, ent, ent.Comp.CutUnlockTime, ent.Comp.CutToolQuality,
            new LegionSlaveCollarCutDoAfterEvent());
    }

    private void OnCollarCut(Entity<LegionSlaveCollarComponent> ent, ref LegionSlaveCollarCutDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<LockComponent>(ent, out var lockComp) || !lockComp.Locked)
            return;

        _lock.Unlock(ent, args.User, lockComp);
    }

    private void OnConstructionCompleted(Entity<LegionSlaveCollarComponent> ent, ref ConstructionCompletedEvent args)
    {
        if (ent.Comp.GeneratedKey || args.UserUid == null)
            return;

        if (ent.Comp.RandomKeyMin > ent.Comp.RandomKeyMax)
            return;

        // Crafted collars receive a unique runtime access tag and a matching key in the crafter's hand.
        var randomKey = _random.Next(ent.Comp.RandomKeyMin, ent.Comp.RandomKeyMax + 1);
        var accessTag = $"{ent.Comp.RandomAccessPrefix}{randomKey}";

        var reader = EnsureComp<AccessReaderComponent>(ent);
        reader.AccessLists.Add(new HashSet<ProtoId<AccessLevelPrototype>> { accessTag });
        Dirty(ent, reader);

        var key = Spawn(ent.Comp.KeyPrototype, _transform.GetMapCoordinates(args.UserUid.Value));
        var keyAccess = EnsureComp<AccessComponent>(key);
        keyAccess.Tags.Clear();
        keyAccess.Tags.Add(accessTag);
        Dirty(key, keyAccess);

        _meta.SetEntityName(key, $"slave collar key #{randomKey}");
        _hands.PickupOrDrop(args.UserUid.Value, key);

        ent.Comp.GeneratedKey = true;
        Dirty(ent);
    }
}
