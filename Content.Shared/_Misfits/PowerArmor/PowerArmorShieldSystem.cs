// #Misfits Add: Shared system for the power armor brace stance hotkey mechanic.
// Activating braces anchors the wearer to the grid (immobile, but can still fire).
// A slight damage modifier is applied while braced.
// The toggle action has a 5-second useDelay so neither state can be exited immediately.

using Content.Shared.Actions;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Toggleable;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Handles the power armor brace-stance toggle.
///     When braced the wearer is anchored (can't move, but can still fire weapons).
///     A 5-second cooldown on the action prototype prevents rapid mode cycling.
/// </summary>
public sealed class PowerArmorBraceSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerArmorBraceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PowerArmorBraceComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<PowerArmorBraceComponent, ComponentRemove>(OnComponentRemove);

        // Track who is wearing the armor so we can unanchor them if needed.
        SubscribeLocalEvent<PowerArmorBraceComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<PowerArmorBraceComponent, GotUnequippedEvent>(OnUnequip);

        // Main hotkey handler.
        SubscribeLocalEvent<PowerArmorBraceComponent, ToggleActionEvent>(OnToggleAction);

        // Apply extra damage resistance while braced.
        // Must run AFTER SharedArmorSystem (so base coefficients have applied) but BEFORE
        // PowerArmorIntegritySystem (so the reduction hits the full damage stream, not just
        // the 10% bleedthrough that the integrity system leaves for the player).
        SubscribeLocalEvent<PowerArmorBraceComponent, InventoryRelayedEvent<DamageModifyEvent>>(
            OnDamageModify,
            after: new[] { typeof(SharedArmorSystem) },
            before: new[] { typeof(PowerArmorIntegritySystem) });
    }

    private void OnMapInit(EntityUid uid, PowerArmorBraceComponent component, MapInitEvent args)
    {
        _actionContainer.EnsureAction(uid, ref component.BraceActionEntity, component.BraceAction);
        Dirty(uid, component);
    }

    private void OnGetItemActions(EntityUid uid, PowerArmorBraceComponent component, GetItemActionsEvent args)
    {
        if (args.SlotFlags != component.RequiredSlot || component.BraceActionEntity == null)
            return;

        args.AddAction(component.BraceActionEntity.Value);
    }

    private void OnEquip(EntityUid uid, PowerArmorBraceComponent component, GotEquippedEvent args)
    {
        // Record who is wearing the armor so we can clean up on component removal.
        component.Wearer = args.Equipee;
        Dirty(uid, component);
    }

    private void OnUnequip(EntityUid uid, PowerArmorBraceComponent component, GotUnequippedEvent args)
    {
        // If the armor is stripped off while braced, silently unanchor the wearer.
        // No chat event — the armor being removed is its own emote.
        if (component.IsBraced)
        {
            component.IsBraced = false;
            _actions.SetToggled(component.BraceActionEntity, false);
            UnanchorWearer(args.Equipee);
        }

        component.Wearer = null;
        Dirty(uid, component);
    }

    private void OnComponentRemove(EntityUid uid, PowerArmorBraceComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(component.BraceActionEntity);

        // Unanchor the wearer if the component is force-removed while braced.
        if (component.IsBraced && component.Wearer != null)
            UnanchorWearer(component.Wearer.Value);
    }

    private void OnToggleAction(EntityUid uid, PowerArmorBraceComponent component, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.IsBraced)
            Unbrace(uid, component, args.Performer);
        else
            Brace(uid, component, args.Performer);

        args.Handled = true;
    }

    private void OnDamageModify(EntityUid uid, PowerArmorBraceComponent component,
        InventoryRelayedEvent<DamageModifyEvent> args)
    {
        // Extra damage reduction is only active when the wearer is braced.
        if (!component.IsBraced)
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.ActiveModifiers);
    }

    private void Brace(EntityUid uid, PowerArmorBraceComponent component, EntityUid user)
    {
        var xform = Transform(user);

        // Must be standing directly on a grid tile to brace (same check as hand-held blocking).
        if (xform.GridUid != xform.ParentUid)
        {
            _popup.PopupClient(Loc.GetString("power-armor-brace-cant-here"), user, user);
            return;
        }

        _transform.AnchorEntity(user, xform);

        if (!xform.Anchored)
        {
            // Tile doesn't support anchoring (e.g. in space).
            _popup.PopupClient(Loc.GetString("power-armor-brace-cant-here"), user, user);
            return;
        }

        component.IsBraced = true;
        _actions.SetToggled(component.BraceActionEntity, true);
        Dirty(uid, component);

        // Server-side chat and audio systems can subscribe to this event.
        RaiseLocalEvent(uid, new PowerArmorBraceActivatedEvent(user, uid));
    }

    private void Unbrace(EntityUid uid, PowerArmorBraceComponent component, EntityUid user)
    {
        component.IsBraced = false;
        _actions.SetToggled(component.BraceActionEntity, false);
        UnanchorWearer(user);
        Dirty(uid, component);

        RaiseLocalEvent(uid, new PowerArmorBraceDeactivatedEvent(user, uid));
    }

    private void UnanchorWearer(EntityUid user)
    {
        var xform = Transform(user);
        if (xform.Anchored)
            _transform.Unanchor(user, xform);
    }
}

// #Misfits Add: Raised on the armor entity when the wearer activates the brace stance via hotkey.
public readonly record struct PowerArmorBraceActivatedEvent(EntityUid User, EntityUid Armor);

// #Misfits Add: Raised on the armor entity when the wearer deactivates the brace stance via hotkey.
public readonly record struct PowerArmorBraceDeactivatedEvent(EntityUid User, EntityUid Armor);

