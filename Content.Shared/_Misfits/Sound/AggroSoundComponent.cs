// Misfits Change - Component to play an aggro/alert sound when a mob enters combat
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Sound;

/// <summary>
/// When present, plays a sound collection the first time this entity attacks
/// (melee or ranged), then enforces a cooldown before it can play again.
/// Keeps aggro vocalizations separate from idle ambient sounds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AggroSoundComponent : Component
{
    /// <summary>
    /// Sound collection to play when entering combat.
    /// </summary>
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    /// <summary>
    /// Minimum seconds between aggro sound plays. Actual cooldown after each play
    /// is chosen randomly between this and <see cref="CooldownMax"/> so mobs in a
    /// group do not all vocalize in sync.
    /// </summary>
    [DataField]
    public float CooldownMin = 10f;

    /// <summary>
    /// Maximum seconds between aggro sound plays.
    /// </summary>
    [DataField]
    public float CooldownMax = 15f;

    /// <summary>
    /// Time remaining before the aggro sound can play again.
    /// Networked so the client can show the aggro status icon while active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CooldownRemaining;
}
