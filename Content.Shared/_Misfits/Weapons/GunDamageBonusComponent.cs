using Content.Shared.Damage;
using Robust.Shared.GameStates;

// #Misfits Add - Gun-side bonus damage and fire cost multiplier for weapons like the Wattz series.
// Adds flat bonus damage on top of whatever the inserted cell's hitscan proto provides,
// and optionally multiplies the cell's fireCost when inserted (e.g. 2.0 = half the shots).

namespace Content.Shared._Misfits.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunDamageBonusComponent : Component
{
    /// <summary>
    /// Flat bonus damage added on top of whatever the cell's hitscan deals.
    /// Applied server-side when the hitscan hits a target.
    /// </summary>
    [DataField("bonusDamage"), AutoNetworkedField]
    public DamageSpecifier? BonusDamage;

    /// <summary>
    /// Multiplier applied to the inserted cell's FireCost.
    /// 1.0 = normal, 2.0 = double cost (half the shots).
    /// Applied when a cell is inserted; restored when ejected.
    /// </summary>
    [DataField("fireCostMultiplier"), AutoNetworkedField]
    public float FireCostMultiplier = 1.0f;

    /// <summary>
    /// Tracks the original FireCost of the currently inserted cell
    /// so it can be restored on ejection.
    /// </summary>
    [ViewVariables]
    public float? OriginalFireCost;
}
