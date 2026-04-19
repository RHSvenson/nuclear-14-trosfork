using Content.Server.Emp;
using Content.Shared._Misfits.C27;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

// #Misfits Add - Server EMP handler for the C-27 humanoid robot species. Subscribes to
// EmpPulseEvent on entities carrying MisfitsC27Component and applies Shock damage scaled by
// the pulse's energy budget plus the configured stun. Server-only because EMP damage and
// status effects are authoritative on the server.
namespace Content.Server._Misfits.C27;

// #Misfits Add - C-27 humanoid robot EMP handler. Spec: EMP pulses drain power cells AND inflict
// posibrain damage; optional PA-style stun. We model the posibrain damage as Shock damage to the
// chassis (the brain is an organ inside the body — damaging the mob propagates through the
// damageable). Battery drain is left to the existing Battery / power-cell EmpPulseEvent
// subscribers — if the C-27 ever gets a power cell slot, it will already be handled.
public sealed class MisfitsC27EmpSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MisfitsC27Component, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<MisfitsC27Component> ent, ref EmpPulseEvent args)
    {
        // Scale damage by pulse energy: a stronger EMP fries the posibrain harder.
        var energyMultiplier = args.EnergyConsumption / 1000f;
        var totalShock = ent.Comp.EmpShockDamage + ent.Comp.EmpDamagePerKiloJoule * energyMultiplier;

        // Apply the shock as a single damage spec — no need to allocate via prototype lookups
        // for every pulse, but we do need a DamageSpecifier with the correct type lookup.
        if (_proto.TryIndex<DamageTypePrototype>("Shock", out var shockProto))
        {
            var damage = new DamageSpecifier(shockProto, totalShock);
            _damageable.TryChangeDamage(ent, damage, ignoreResistances: true, origin: null);
        }

        // Mark Affected so the EMP visual effect spawns over the chassis.
        args.Affected = true;

        // Optional PA-style stun: sets the EmpDisabled component so the mob is locked out of
        // interactions for the pulse duration. EmpSystem.DoEmpEffects handles the actual
        // EnsureComp<EmpDisabledComponent> when args.Disabled is true.
        if (ent.Comp.ApplyEmpStun)
            args.Disabled = true;

        _popup.PopupEntity(Loc.GetString("c27-emp-hit"), ent, ent);
    }
}
