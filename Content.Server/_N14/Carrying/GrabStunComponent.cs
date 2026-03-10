namespace Content.Server._N14.Carrying;

/// <summary>
/// When this entity successfully carries (grabs) another entity, the carried
/// entity is stunned for <see cref="StunTime"/>.
/// </summary>
[RegisterComponent]
public sealed partial class GrabStunComponent : Component
{
    [DataField("stunTime")]
    public TimeSpan StunTime = TimeSpan.FromSeconds(5);
}
