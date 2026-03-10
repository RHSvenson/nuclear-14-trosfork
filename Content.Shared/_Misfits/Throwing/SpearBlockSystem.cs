// #Misfits Change Add: Handles deflection of thrown Spear-tagged weapons (spears, javelins, polearms)
// by entities wearing or holding items with SpearBlockComponent (shields, power armor).
//
// Mirrors ReflectSystem's ReflectUserComponent propagation pattern.
// When a hit triggers, the spear embeds into the blocking item (shield/PA) instead of the mob.
// If no blocking item is found as an entity, the spear falls to the ground instead.
using Content.Shared._Misfits.Throwing.Components;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Throwing;

public sealed class SpearBlockSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Propagate SpearBlockUserComponent to the equipee when a SpearBlock item is worn or held.
        SubscribeLocalEvent<SpearBlockComponent, GotEquippedEvent>(OnSpearBlockEquipped);
        SubscribeLocalEvent<SpearBlockComponent, GotUnequippedEvent>(OnSpearBlockUnequipped);
        SubscribeLocalEvent<SpearBlockComponent, GotEquippedHandEvent>(OnSpearBlockHandEquipped);
        SubscribeLocalEvent<SpearBlockComponent, GotUnequippedHandEvent>(OnSpearBlockHandUnequipped);

        // Handle incoming thrown polearms on entities that carry a SpearBlock item.
        SubscribeLocalEvent<SpearBlockUserComponent, ThrowHitByEvent>(OnSpearHitUser);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Clean up temporary ThrownItemImmuneComponent added to prevent spear embedding.
        // The immunity must persist until ThrowDoHitEvent fires (synchronously after ThrowHitByEvent)
        // and is safe to remove on the very next tick.
        var query = EntityQueryEnumerator<SpearBlockCleanupComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemComp<ThrownItemImmuneComponent>(uid);
            RemComp<SpearBlockCleanupComponent>(uid);
        }
    }

    // --- Equipment event handlers (mirror of ReflectSystem pattern) ---

    private void OnSpearBlockEquipped(EntityUid uid, SpearBlockComponent comp, GotEquippedEvent args)
    {
        if (_gameTiming.ApplyingState)
            return;
        EnsureComp<SpearBlockUserComponent>(args.Equipee);
    }

    private void OnSpearBlockUnequipped(EntityUid uid, SpearBlockComponent comp, GotUnequippedEvent args)
    {
        RefreshSpearBlockUser(args.Equipee, uid);
    }

    private void OnSpearBlockHandEquipped(EntityUid uid, SpearBlockComponent comp, GotEquippedHandEvent args)
    {
        if (_gameTiming.ApplyingState)
            return;
        EnsureComp<SpearBlockUserComponent>(args.User);
    }

    private void OnSpearBlockHandUnequipped(EntityUid uid, SpearBlockComponent comp, GotUnequippedHandEvent args)
    {
        RefreshSpearBlockUser(args.User, uid);
    }

    /// <summary>
    /// Removes SpearBlockUserComponent from <paramref name="user"/> unless another equipped item
    /// (other than <paramref name="excluding"/>) still provides SpearBlockComponent.
    /// </summary>
    private void RefreshSpearBlockUser(EntityUid user, EntityUid excluding)
    {
        if (!HasComp<SpearBlockUserComponent>(user))
            return;

        foreach (var ent in _inventorySystem.GetHandOrInventoryEntities(user, SlotFlags.All & ~SlotFlags.POCKET))
        {
            if (ent != excluding && HasComp<SpearBlockComponent>(ent))
                return; // Another item still provides the block.
        }

        RemCompDeferred<SpearBlockUserComponent>(user);
    }

    // --- Deflection handler ---

    private void OnSpearHitUser(EntityUid uid, SpearBlockUserComponent comp, ThrowHitByEvent args)
    {
        // Only block Spear-tagged thrown items (spears, javelins, polearms).
        if (!_tagSystem.HasTag(args.Thrown, "Spear"))
            return;

        // Find the first equipped item providing SpearBlockComponent (shield or PA suit).
        EntityUid? blockEntity = null;
        foreach (var ent in _inventorySystem.GetHandOrInventoryEntities(uid, SlotFlags.All & ~SlotFlags.POCKET))
        {
            if (HasComp<SpearBlockComponent>(ent))
            {
                blockEntity = ent;
                break;
            }
        }

        if (_netManager.IsServer)
        {
            // Prevent normal embed into the mob.
            if (!HasComp<ThrownItemImmuneComponent>(uid))
            {
                AddComp<ThrownItemImmuneComponent>(uid);
                EnsureComp<SpearBlockCleanupComponent>(uid);
            }

            // Redirect embed into the blocking item (shield / PA) instead of falling to ground.
            if (blockEntity.HasValue
                && TryComp<EmbeddableProjectileComponent>(args.Thrown, out var embeddable)
                && embeddable.EmbedOnThrow)
            {
                _projectile.Embed(args.Thrown, blockEntity.Value, args.User, embeddable, args.TargetPart);
            }
        }

        if (!_netManager.IsServer)
            return;

        // Show narrative popup visible to all nearby players.
        var spearName = Name(args.Thrown);
        var targetName = Identity.Name(uid, EntityManager);
        var throwerName = args.User is { } throwerId
            ? Identity.Name(throwerId, EntityManager)
            : Loc.GetString("spear-block-unknown-thrower");

        if (blockEntity.HasValue)
        {
            var shieldName = Name(blockEntity.Value);
            _popup.PopupEntity(
                Loc.GetString("spear-block-embedded",
                    ("thrower", throwerName), ("spear", spearName), ("target", targetName), ("shield", shieldName)),
                uid,
                PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString("spear-block-deflected",
                    ("thrower", throwerName), ("spear", spearName), ("target", targetName)),
                uid,
                PopupType.Medium);
        }
    }
}
