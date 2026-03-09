// #Misfits Change - Ported from Delta-V addiction system
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Addictions;

/// <summary>
///     Added to an entity when they are currently addicted to a substance.
///     Managed by <see cref="SharedAddictionSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAddictionSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AddictedComponent : Component
{
    /// <summary>
    ///     Whether the addiction symptoms are currently suppressed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Suppressed;

    /// <summary>
    ///     When the addictive substance was last metabolized.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? LastMetabolismTime;

    /// <summary>
    ///     When the next withdrawal effect popup should fire.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? NextEffectTime;

    /// <summary>
    ///     When the current suppression period ends.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? SuppressionEndTime;
}
