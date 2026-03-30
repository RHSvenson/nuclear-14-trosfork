using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Chat.Components;

/// <summary>
/// Marks an item as a megaphone source and defines the speech verb to use while wielded.
/// </summary>
[RegisterComponent]
public sealed partial class MegaphoneComponent : Component
{
    /// <summary>
    /// The speech verb override applied when the item is actively wielded.
    /// </summary>
    [DataField]
    public ProtoId<SpeechVerbPrototype> SpeechVerbOverride = "MisfitsMegaphone";
}
