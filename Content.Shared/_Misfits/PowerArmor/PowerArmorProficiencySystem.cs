using Content.Shared.Inventory.Events;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Blocks equipping any item that carries <see cref="N14PowerArmorComponent"/> unless the
///     character also has <see cref="PowerArmorProficiencyComponent"/> (granted by the
///     N14PowerArmorTraining character-creation perk).
///
///     Runs shared (client + server) so inventory prediction cancels the animation immediately.
/// </summary>
public sealed class PowerArmorProficiencySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to equip attempts on power armor items specifically.
        SubscribeLocalEvent<N14PowerArmorComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
    }

    private void OnEquipAttempt(Entity<N14PowerArmorComponent> item, ref BeingEquippedAttemptEvent args)
    {
        // #Misfits Add - salvaged/stripped variants set RequiresProficiency = false; skip the gate.
        if (!item.Comp.RequiresProficiency)
            return;

        // Check whether the character putting on the armor has the proficiency.
        if (HasComp<PowerArmorProficiencyComponent>(args.EquipTarget))
            return;

        // Deny the equip and show a localised popup message.
        args.Reason = "power-armor-proficiency-required";
        args.Cancel();
    }
}
