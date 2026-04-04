using System.Collections.Generic;

// #Misfits Add - Shared data types for procedural underground expedition system

namespace Content.Shared._Misfits.Expeditions;

/// <summary>
/// The three visual/structural themes for procedural underground expedition maps.
/// </summary>
public enum UndergroundTheme
{
    /// <summary>Pre-war concrete bunker / Vault-Tec facility.</summary>
    Vault,
    /// <summary>Crumbling brick sewer tunnels with dirty water channels.</summary>
    Sewer,
    /// <summary>Abandoned subway / metro system with metal infrastructure.</summary>
    Metro,
}

/// <summary>
/// Cell types for the procedural 2D grid.
/// <list type="bullet">
/// <item>Empty = void space — no tile, no entity</item>
/// <item>Corridor = carved passage between rooms</item>
/// <item>Room = standard interior room</item>
/// <item>FactionHub = large faction staging hub (one per faction group)</item>
/// <item>WaterChannel = sewer-theme open water trench</item>
/// <item>Platform = grate-floor elevated walkway</item>
/// </list>
/// </summary>
public enum CellType : int
{
    Empty        = 0,
    Corridor     = 1,
    Room         = 2,
    FactionHub   = 3,
    WaterChannel = 4,
    Platform     = 5,
}

/// <summary>
/// Parameters bag carried from <see cref="N14ExpeditionMapEntry"/> into the
/// generator. Constructed in code; not deserialized from YAML.
/// </summary>
public sealed class UndergroundGenParams
{
    /// <summary>RNG seed for deterministic generation.</summary>
    public int Seed { get; set; }

    /// <summary>Visual / structural theme.</summary>
    public UndergroundTheme Theme { get; set; }

    /// <summary>Grid width in tiles (square).</summary>
    public int GridWidth { get; set; } = 80;

    /// <summary>Grid height in tiles (square).</summary>
    public int GridHeight { get; set; } = 80;

    /// <summary>0 = Easy, 1 = Medium, 2 = Hard.</summary>
    public int DifficultyTier { get; set; }

    /// <summary>Minimum number of standard rooms to guarantee.</summary>
    public int MinRooms { get; set; } = 6;

    /// <summary>Maximum number of standard rooms to attempt.</summary>
    public int MaxRooms { get; set; } = 12;

    /// <summary>Number of faction staging hubs (1–4, clamped to 4 corners).</summary>
    public int HubCount { get; set; } = 2;

    /// <summary>
    /// Faction spawn groups from the difficulty prototype.
    /// One hub is allocated per group up to HubCount.
    /// </summary>
    public List<N14FactionSpawnGroup> FactionSpawnGroups { get; set; } = new();
}

/// <summary>
/// Defines a rectangular room on the procedural grid.
/// </summary>
public sealed class RoomDef
{
    /// <summary>Left edge (grid X index, inclusive).</summary>
    public int X { get; set; }

    /// <summary>Bottom edge (grid Y index, inclusive).</summary>
    public int Y { get; set; }

    /// <summary>Width in tiles.</summary>
    public int W { get; set; }

    /// <summary>Height in tiles.</summary>
    public int H { get; set; }

    /// <summary>What kind of room this is. #Misfits Change - Standard removed, default to Central</summary>
    public RoomType RoomType { get; set; } = RoomType.Central;

    /// <summary>
    /// Index of the faction spawn group that "owns" this hub (-1 = no faction).
    /// </summary>
    public int FactionIndex { get; set; } = -1;

    /// <summary>Grid-space center of the room.</summary>
    public (int cx, int cy) Center => (X + W / 2, Y + H / 2);

    /// <summary>
    /// Returns true if this room overlaps <paramref name="other"/> when both
    /// are expanded outward by <paramref name="margin"/> tiles on each side.
    /// margin=2 enforces a minimum 2-tile wall gap between room interiors.
    /// </summary>
    public bool Overlaps(RoomDef other, int margin = 2)
    {
        return !(X >= other.X + other.W + margin ||
                 X + W + margin <= other.X         ||
                 Y >= other.Y + other.H + margin ||
                 Y + H + margin <= other.Y);
    }
}

/// <summary>
/// Classifies what a room is used for during generation and dressing.
/// #Misfits Change - Expanded to 16 thematic variants per Underground Expedition design spec
/// </summary>
public enum RoomType
{
    /// <summary>Large faction staging hub placed at a map corner.</summary>
    FactionHub,

    /// <summary>Central congregation / objective room near the map centre.</summary>
    Central,

    // Vault variants
    /// <summary>Vault dweller housing and barracks.</summary>
    VaultBarracks,

    /// <summary>Kitchen and cafeteria area.</summary>
    // #Misfits Add - VaultKitchen room type for lived-in food/cooking areas
    VaultKitchen,

    /// <summary>Hydroponic farming bays with planters and water storage.</summary>
    // #Misfits Add - VaultHydroponics from Vault.yml analysis
    VaultHydroponics,

    /// <summary>Recreation room: pool tables, fitness equipment, instruments.</summary>
    // #Misfits Add - VaultRecreation from Vault.yml analysis
    VaultRecreation,

    /// <summary>Research and medical laboratory.</summary>
    VaultLab,

    /// <summary>Weapons cache and security armory.</summary>
    VaultArmory,

    /// <summary>Main vault chamber (treasure room, climax).</summary>
    VaultVault,

    /// <summary>Overseer command center.</summary>
    VaultOverseer,

    /// <summary>Reactor room (hazardous, high radiation).</summary>
    VaultReactor,

    // Sewer variants
    /// <summary>Standard tunnel passage.</summary>
    SewerTunnel,

    /// <summary>Pump station and water treatment utility.</summary>
    SewerPump,

    /// <summary>Creature lair or nest (highly hazardous).</summary>
    SewerNest,

    /// <summary>Improvised underground survivor camp with beds, fire, and basic supplies.</summary>
    // #Misfits Add - SewerCamp from MercerIslandSewers.yml analysis
    SewerCamp,

    /// <summary>Natural cavern chamber.</summary>
    SewerGrotto,

    /// <summary>Pipe junction and intersection chamber.</summary>
    SewerJunction,

    // Metro variants
    /// <summary>Passenger station platform.</summary>
    MetroPlatform,

    /// <summary>Maintenance and utility room.</summary>
    MetroMaintenance,

    /// <summary>Control and dispatch center.</summary>
    MetroCommand,

    /// <summary>Cargo and vehicle depot.</summary>
    MetroDepot,

    /// <summary>Transit tunnel passage.</summary>
    MetroTunnel,
}
