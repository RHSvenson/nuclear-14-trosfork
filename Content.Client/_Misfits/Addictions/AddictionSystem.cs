// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared._Misfits.Addictions;

namespace Content.Client._Misfits.Addictions;

/// <summary>
///     Client-side addiction system stub. All logic is server-side.
/// </summary>
public sealed class AddictionSystem : SharedAddictionSystem
{
    protected override void UpdateTime(EntityUid uid) { }
}
