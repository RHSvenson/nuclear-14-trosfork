// #Misfits Change - Emits popup warnings when a robot's hull is damaged and when self-repair begins.
namespace Content.Shared._Misfits.Silicon;

/// <summary>
/// Attached to player synthetic robots. Emits a hull-integrity warning popup when significant
/// damage is taken, and a self-repair announcement when passive healing begins.
/// </summary>
[RegisterComponent]
public sealed partial class RobotSelfRepairAnnouncerComponent : Component
{
    /// <summary>
    /// Minimum damage delta per event needed to trigger the hull integrity warning.
    /// </summary>
    [DataField]
    public float DamageDeltaThreshold = 10f;

    /// <summary>
    /// Minimum total damage (absolute, out of the dead threshold) before the hull warning fires.
    /// Prevents warnings when the robot is barely scratched.
    /// </summary>
    [DataField]
    public float MinTotalDamageForHullWarning = 30f;

    /// <summary>
    /// Cooldown between repeated hull integrity warnings.
    /// </summary>
    [DataField]
    public TimeSpan HullWarningCooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Minimum total damage required to trigger the self-repair announcement popup.
    /// Prevents the popup from firing when the robot is near-full health.
    /// </summary>
    [DataField]
    public float MinDamageForRepairPopup = 20f;

    /// <summary>
    /// Cooldown between self-repair announcement popups.
    /// </summary>
    [DataField]
    public TimeSpan RepairAnnounceCooldown = TimeSpan.FromSeconds(30);

    // Runtime state - not serialized
    public TimeSpan? NextHullWarningTime;
    public TimeSpan? NextRepairAnnounceTime;
}
