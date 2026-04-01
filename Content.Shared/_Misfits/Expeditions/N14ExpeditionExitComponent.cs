namespace Content.Shared._Misfits.Expeditions;

/// <summary>
/// Placed on an entity inside an expedition map that players can interact with
/// to return to the expedition origin (chalkboard) before the timer expires.
/// Walking onto the exit starts a 10-second countdown; leaving cancels it.
/// Pressing E also starts the countdown.
/// </summary>
[RegisterComponent]
public sealed partial class N14ExpeditionExitComponent : Component
{
    /// <summary>
    /// The expedition map entity this exit belongs to.
    /// Used to look up the return coordinates.
    /// </summary>
    [DataField]
    public EntityUid ExpeditionMap;

    /// <summary>
    /// How many seconds a player must remain on the exit before extraction.
    /// </summary>
    [DataField]
    public float CountdownSeconds = 10f;

    /// <summary>
    /// Radius (in tiles) around the exit point to gather entities for extraction.
    /// A 3×3 tile zone = 1.5 tile radius from center.
    /// Everything non-anchored within this radius gets teleported back with the player.
    /// </summary>
    [DataField]
    public float ExtractionRadius = 1.5f;

    /// <summary>
    /// Tracks pending extraction countdowns per entity.
    /// Key = entity waiting to leave, Value = server time when they get extracted.
    /// Removed when they step off the exit zone.
    /// </summary>
    public Dictionary<EntityUid, TimeSpan> PendingExtractions = new();
}
