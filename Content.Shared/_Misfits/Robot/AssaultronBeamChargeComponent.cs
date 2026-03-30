using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Misfits.Robot;

/// <summary>
/// Adds a charge-up phase before the Assaultron's weapon fires.
/// On first shot attempt the system cancels the shot and starts a charge timer.
/// After the charge completes the next shot attempt succeeds. A cooldown then
/// prevents further shots for a configured duration.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AssaultronBeamChargeComponent : Component
{
    /// <summary>How long the charge-up phase lasts before the weapon can fire.</summary>
    [DataField]
    public float ChargeDuration = 2f;

    /// <summary>Cooldown after a successful shot before charging can begin again.
    /// Combined with ChargeDuration this determines the total cycle time (ROF).</summary>
    [DataField]
    public float CooldownDuration = 3f;

    /// <summary>Locale key for the emote broadcast when charging starts.</summary>
    [DataField]
    public string ChargeEmoteLocale = "assaultron-charge-emote";

    /// <summary>Locale key for the emote broadcast when the weapon fires.</summary>
    [DataField]
    public string FireEmoteLocale = "assaultron-beam-fire-emote";

    // --- Runtime state (not serialised) ---

    public bool IsCharging;
    public bool ReadyToFire;
    public TimeSpan ChargeEndTime;
    public TimeSpan CooldownEndTime;
}
