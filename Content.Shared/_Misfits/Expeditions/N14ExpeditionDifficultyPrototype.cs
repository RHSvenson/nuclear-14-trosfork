using System.Numerics;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.Expeditions;

/// <summary>
/// Defines a category for the N14 expedition system (e.g. Surface / Underground).
/// Each category has a display color, expedition duration, and pool of map entries.
/// Maps can optionally define faction-based spawn overrides — the majority faction
/// among gathered players determines the spawn location for everyone.
/// </summary>
[Prototype("n14ExpeditionDifficulty")]
public sealed partial class N14ExpeditionDifficultyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized display name for this category.
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>
    /// UI color for the category button/label.
    /// </summary>
    [DataField(required: true)]
    public Color Color { get; private set; } = Color.White;

    /// <summary>
    /// How long the expedition lasts, in seconds. Default 30 minutes.
    /// </summary>
    [DataField]
    public float Duration { get; private set; } = 1800f;

    /// <summary>
    /// Map entries available for this category.
    /// A random one is picked each launch.
    /// </summary>
    [DataField(required: true)]
    public List<N14ExpeditionMapEntry> Maps { get; private set; } = new();

    /// <summary>
    /// Display order in the UI (lower = shown first).
    /// </summary>
    [DataField]
    public int SortOrder { get; private set; } = 0;
}

/// <summary>
/// A single map entry in an expedition category.
/// Optionally includes faction-group spawn overrides — when present, the expedition
/// system uses a majority-vote to determine where ALL players spawn.
/// </summary>
[DataDefinition]
public sealed partial class N14ExpeditionMapEntry
{
    /// <summary>
    /// Path to the map file (e.g. /Maps/N14/MercerIslandSewers.yml).
    /// </summary>
    [DataField(required: true)]
    public ResPath Path { get; private set; } = default!;

    /// <summary>
    /// Optional faction-group spawn overrides for this map.
    /// When present, the system counts which group has the most players,
    /// then teleports EVERYONE to that group's position (minority follows majority).
    /// When absent (null), players spawn at grid origin.
    /// </summary>
    [DataField]
    public List<N14FactionSpawnGroup>? FactionSpawns { get; private set; }
}

/// <summary>
/// Maps a set of NPC faction prototype IDs to a grid-local spawn position.
/// Multiple factions can share a group (e.g. NCR + Rangers).
/// Used for majority-vote spawn logic in the expedition system.
/// </summary>
[DataDefinition]
public sealed partial class N14FactionSpawnGroup
{
    /// <summary>
    /// NPC faction prototype IDs that belong to this spawn group.
    /// E.g. ["NCR", "Rangers"] both count toward the NCR group.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<NpcFactionPrototype>> Factions { get; private set; } = new();

    /// <summary>
    /// Grid-local spawn position for this group.
    /// </summary>
    [DataField(required: true)]
    public Vector2 Position { get; private set; }
}
