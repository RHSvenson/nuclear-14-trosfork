using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Misfits.Expeditions;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Content.Server.Decals;

// #Misfits Add - Procedural underground expedition map generator (Vault / Sewer / Metro themes)
// Rewritten: slanted room walls, full WallRock background enclosure, door-at-threshold logic,
//            theme-specific tile variety, furniture dressing, and NPC mob spawning.

namespace Content.Server._Misfits.Expeditions;

/// <summary>
/// EntitySystem that generates procedural underground expedition maps at runtime.
/// Supports three themes: Vault (pre-war concrete), Sewer (brick tunnels + dirty water),
/// Metro (abandoned subway infrastructure).
///
/// Generation pipeline:
///   Phase A  — Room placement (faction hubs → central → standard rooms, theme-sized)
///   Phase B  — Corridor carving (minimum spanning tree, 2-tile-wide L-shaped)
///   Phase B5 — Doorway marking (corridor cells adjacent to room interior)
///   Phase C  — Sewer water-channel carving (Sewer theme only)
///   Phase D  — Tile painting (ALL cells tiled; background uses FloorAsteroidSand)
///   Phase E  — Entity spawning (WallRock fill, slanted room walls, doors, furniture, mobs)
/// </summary>
public sealed class UndergroundExpeditionMapGenerator : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly DecalSystem _decalSystem = default!;

    // ─────────────────────────────────────────────────────────────────────────
    // Tile IDs
    // ─────────────────────────────────────────────────────────────────────────

    // Background tile placed under all empty/wall cells (mostly hidden; seals atmos)
    private const string TileBackground      = "FloorAsteroidSand";

    // Vault floor variants
    private const string TileVaultFloor      = "FloorMetalTunnel";
    private const string TileVaultRusty      = "FloorMetalTunnelRusty";
    private const string TileVaultAlt        = "FloorMetalTunnelWasteland";
    private const string TileVaultConcrete   = "N14FloorConcrete";
    private const string TileRubble          = "FloorRubbleIndoors";
    // #Misfits Add - Extra vault floor variety
    private const string TileVaultSteelDirty = "FloorSteelDirty";
    private const string TileVaultConcDark   = "N14FloorConcreteDark";
    private const string TileVaultIndustrial = "FloorMS13ConcreteIndustrial";

    // Sewer floor variants
    private const string TileSewerDirt       = "FloorDirtIndoors";
    private const string TileSewerDirtNew    = "FloorDirtNew";
    private const string TileSewerGrate      = "FloorMS13MetalGrate";
    private const string TileSewerConcrete   = "FloorMS13Concrete";
    // #Misfits Add - Extra sewer floor variety
    private const string TileSewerCave       = "FloorCave";
    private const string TileSewerBrick      = "FloorMS13BrickConcrete";

    // Metro floor variants
    private const string TileMetroDark       = "FloorMetalGreyDark";
    private const string TileMetroDarkSolid  = "FloorMetalGreyDarkSolid";
    private const string TileMetroGrate      = "FloorMS13MetalGrate";
    private const string TileMetroTile       = "FloorMS13MetalTile";
    private const string TileMetroConcrete   = "N14FloorConcrete";
    // #Misfits Add - Extra metro floor variety
    private const string TileMetroIndustrial = "FloorMS13MetalIndustrial";
    private const string TileMetroSteelDirty = "FloorSteelDirty";
    private const string TileMetroConcAlt    = "FloorMS13ConcreteIndustrialAlt";

    // Shared
    private const string TileWaterDeep       = "WaterDeep";
    private const string TileGrate           = "FloorMS13MetalGrate";

    // ─────────────────────────────────────────────────────────────────────────
    // Wall entity IDs
    // ─────────────────────────────────────────────────────────────────────────

    // #Misfits Fix - Use indestructible variant so players can't break out of the dungeon
    private const string WallRockFill        = "N14WallRockSlantedIndestructible";

    // Slanted room-perimeter walls (auto-sprite to neighbouring walls)
    // #Misfits Change - Upgraded all theme walls to indestructible variants (prevents griefing / dungeon escape)
    private const string WallVaultRoom       = "N14WallConcreteSlantedIndestructible";
    private const string WallVaultHub        = "N14WallBunkerSlantedIndestructible";
    private const string WallSewerRoom       = "N14WallBrickSlantedIndestructible";
    private const string WallSewerHub        = "N14WallBrickGraySlantedIndestructible";
    private const string WallMetroRoom       = "N14WallDungeonSlantedIndestructible";
    private const string WallMetroHub        = "N14WallCombSlantedIndestructible";

    // ─────────────────────────────────────────────────────────────────────────
    // Door entity IDs
    // ─────────────────────────────────────────────────────────────────────────

    // #Misfits Fix - N14DoorVault is 4x4, too big for 2-tile corridors
    private const string DoorVaultHub        = "N14DoorMetalReinforced";
    private const string DoorVaultRoom       = "N14DoorBunker";
    private const string DoorSewerHub        = "N14DoorMakeshift";
    private const string DoorSewerRoom       = "N14DoorRoomRepaired";
    private const string DoorMetroHub        = "N14DoorBunker";
    private const string DoorMetroRoom       = "N14DoorWoodRoom";

    // ─────────────────────────────────────────────────────────────────────────
    // Sewer water entity
    // ─────────────────────────────────────────────────────────────────────────

    private const string WaterSewerEntity    = "N14FloorWaterSewerMedium";

    // ─────────────────────────────────────────────────────────────────────────
    // Furniture entity pools — indexed by (theme, RoomType)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string[] FurnVaultStandard =
    {
        // #Misfits Change - Replaced empty crates with loot variants, added junk piles
        "LockerSteel", "N14LootCrateArmy", "Rack", "TableWood",
        "N14JunkTable", "N14JunkBed1", "N14JunkBed2",
        "N14JunkMachine", "N14JunkCabinet", "N14JunkDresser",
        "N14JunkPile1", "N14JunkPile3", "N14JunkPile5",
    };

    private static readonly string[] FurnVaultHub =
    {
        // #Misfits Change - Authentic N14 hub with barricades, defense, workbenches, and vending
        "N14BarricadeMetal", "N14BarricadeMetalGreen", "N14BarricadeSandbagSingle",
        "N14BarricadeTanktrapRusty", "N14LootCrateMilitary", "N14ShelfMetal",
        "LockerSecurity", "N14LootCrateVaultStandard",
        "N14JunkPile2", "N14JunkPile7",
        // #Misfits Add - Workbenches and vending from Vault.yml analysis
        "N14WorkbenchWeaponbench", "N14WorkbenchAmmobench", "N14VendingMachineNukaCola",
    };

    private static readonly string[] FurnSewerStandard =
    {
        "CrateWooden", "N14JunkTable", "N14JunkBench", "N14JunkBed1",
        "N14JunkToilet", "N14JunkSink", "N14JunkDresser", "N14JunkCabinet",
        // #Misfits Add - Junk pile clutter
        "N14JunkPile4", "N14JunkPile6", "N14JunkPile8",
    };

    private static readonly string[] FurnSewerHub =
    {
        "N14BarricadeMetal", "N14JunkBench", "CrateWooden",
        "N14BarricadeSandbagSingle", "N14JunkTable",
        // #Misfits Add - Fire and workbench from MercerIslandSewers.yml analysis
        "N14BurningBarrel", "N14Bonfire", "N14WorkbenchMetal",
    };

    private static readonly string[] FurnMetroStandard =
    {
        // #Misfits Change - Replaced empty crates with loot variants, added junk piles
        "N14JunkBench", "N14JunkTable", "N14JunkArcade",
        "N14JunkTV", "N14JunkJukebox", "N14LootCrateArmy",
        "N14JunkMachine", "N14JunkDresser",
        "N14JunkPile9", "N14JunkPile10", "N14JunkPile11",
    };

    private static readonly string[] FurnMetroHub =
    {
        // #Misfits Change - Replaced empty crate with loot variant
        "N14BarricadeMetal", "N14JunkBench", "N14LootCrateMilitary",
        "N14BarricadeSandbagSingle", "N14JunkMachine",
        "N14JunkPile12", "N14JunkPile1",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Mob entity pools — (proto, relativeWeight) per theme
    // ─────────────────────────────────────────────────────────────────────────

    // #Misfits Change - Faction sub-groups to prevent inter-faction infighting
    // Each room rolls one sub-group so all mobs in that room share the same faction.
    private static readonly (string, int)[][] MobsVaultGroups =
    {
        // Feral faction group
        new[] { ("N14MobGhoulFeral", 30), ("N14MobGhoulFeralReaver", 20), ("N14MobGhoulFeralRotter", 15) },
        // HostileRobot faction group
        new[] { ("N14MobRobotProtectronHostile", 15), ("N14MobRobotAssaultronHostile", 10) },
    };

    private static readonly (string, int)[][] MobsSewerGroups =
    {
        // WastelandInsect faction group
        new[] { ("N14MobRadroach", 35), ("N14MobRadscorpion", 15), ("N14MobBloatfly", 10) },
        // Feral faction group
        new[] { ("N14MobGhoulFeral", 25), ("N14MobGhoulFeralRotter", 15) },
    };

    private static readonly (string, int)[][] MobsMetroGroups =
    {
        // Feral faction group
        new[] { ("N14MobGhoulFeral", 30), ("N14MobGhoulFeralReaver", 25), ("N14MobGhoulFeralRotter", 10) },
        // Raider faction group
        new[] { ("N14MobRaiderPsycho", 20), ("N14MobRaiderFernMelee", 15) },
    };

    // #Misfits Add - Expanded furniture pools for room type variety
    // Vault room-specific pools
    private static readonly string[] FurnVaultBarracks =
    {
        // #Misfits Change - Authentic N14 sleeping quarters with bunks, footlockers, and storage
        "N14BedWoodBunk", "N14BedDirty", "N14BedMattressDirty2", "N14Bedroll",
        "N14ChairMetalBlue", "N14ChairMetalFolding", "LockerSteel",
        "N14LootCrateFootlocker", "N14JunkDresser",
    };

    private static readonly string[] FurnVaultLab =
    {
        // #Misfits Change - Authentic N14 lab/research entities with server racks and med shelves
        "N14MachineRackServer", "N14MachineRackElectronics", "N14TableDeskMetalDirty",
        "N14ShelfMetalMeds", "N14JunkMachine", "LockerSteel", "N14JunkCabinet",
        "N14LootCrateMilitary",
        // #Misfits Add - Mainframe, modular panels, terminal from Vault.yml analysis
        "N14MachineComputerMainframe", "N14MachineModularMachineDials", "N14ComputerTerminalNew",
    };

    private static readonly string[] FurnVaultArmory =
    {
        // #Misfits Change - Authentic N14 armory with gun cabinets, metal shelves, and barricades
        "N14ClosetGunCabinet", "N14ShelfMetal", "N14LootCrateMilitary",
        "N14LootCrateArmy", "N14BarricadeMetal", "N14BarricadeMetalGreen",
        "N14LootCrateVaultStandard", "LockerSecurity",
        // #Misfits Add - Workbenches from Vault.yml analysis
        "N14WorkbenchWeaponbench", "N14WorkbenchArmorbench",
    };

    private static readonly string[] FurnVaultVault =
    {
        // #Misfits Change - Authentic N14 vault/treasure room with rusted crates and safes
        "N14LootCrateVaultBigRusted", "N14LootCrateVaultLongRusted",
        "N14LootCrateVaultStandard", "N14ClosetSafe", "N14ShelfMetal",
        "N14LootCrateArmy",
    };

    private static readonly string[] FurnVaultOverseer =
    {
        // #Misfits Change - Authentic N14 office/command entities with desk, shelves, and filing
        "N14TableDeskWood", "N14ChairOfficeBlue", "N14ShelfWood1",
        "N14LootFilingCabinet", "N14BookshelfDirty", "N14ClosetGrey1",
        "N14JunkCabinet",
    };

    private static readonly string[] FurnVaultReactor =
    {
        // #Misfits Change - Authentic N14 reactor/power room with generators, tanks, and barrels
        "N14GeneratorPrewar", "N14SubstationRustyDestroyed", "N14StorageTankFullFuel",
        "N14StorageTankWideFullFuel", "N14YellowBarrel", "N14BlackBarrel",
        "N14JunkMachine",
        // #Misfits Add - Reactor-floor generator and vat tanks from Vault.yml analysis
        "N14GeneratorReactorFloor", "N14StorageTankVat",
    };

    // #Misfits Add - Kitchen/cafeteria room type with cooking stations and food prep
    private static readonly string[] FurnVaultKitchen =
    {
        "N14CookingStove", "N14CookingStoveWide", "N14KitchenGrill",
        "N14TableCounterMetal", "N14TableCounterMetalBend", "N14LootClosetFridge",
        "N14JunkMicrowave", "N14ChairDinerBench", "N14ChairMetalFolding",
    };

    // #Misfits Add - Hydroponic farming bays: planters, water storage, and supplies (from Vault.yml)
    private static readonly string[] FurnVaultHydroponics =
    {
        "N14HydroponicsPlanter", "N14ShelfMetalMeds", "N14TableCounterMetal",
        "N14TableCounterMetalBend", "N14LootClosetFridge", "N14JunkMachine",
        "N14BlackBarrel", "N14YellowBarrel", "N14StorageTankVat",
    };

    // #Misfits Add - Recreation room: pool tables, gym equipment, instruments, vending (from Vault.yml)
    private static readonly string[] FurnVaultRecreation =
    {
        "N14TableCasinoPool", "N14TableCasinoCards", "N14FitnessPunchingBag",
        "N14FitnessWeightsBench1", "N14FitnessWeightLifter", "N14InstrumentGuitar",
        "N14GrandfatherClock", "N14VendingMachineNukaCola",
        "N14ChairMetalBlue", "N14ChairDinerBench",
    };

    // #Misfits Add - Improvised sewer survivor camp: bedrolls, fire, basic supplies (from MercerIslandSewers.yml)
    private static readonly string[] FurnSewerCamp =
    {
        "N14BedWood", "N14Bedroll", "N14BurningBarrel", "N14Bonfire",
        "N14ChairWood1", "N14ChairArmchair", "N14JunkTable",
        "N14CrateWooden", "N14JunkBench",
    };

    // #Misfits Add - Floor scatter entity pools per theme: debris placed as entities for visual richness
    // These are N14DecorFloor* entities confirmed from Vault.yml / MercerIslandSewers.yml / SunnyvaleUnderground.yml
    private static readonly string[] FloorScatterVault =
    {
        "N14DecorFloorPaper", "N14DecorFloorPaper1", "N14DecorFloorCardboard",
        "N14DecorFloorTrashbags1", "N14DecorFloorTrashbags2", "N14DecorFloorTrashbags3",
        "N14DecorFloorFood1", "N14DecorFloorFood3", "N14DecorFloorFood6",
        "N14DecorFloorGlass1", "N14DecorFloorSkeleton", "N14DecorFloorSkeletonOver",
        "N14DecorFloorBookPile1", "N14DecorFloorBookPile4", "N14DecorFloorBookstack1",
    };

    private static readonly string[] FloorScatterSewer =
    {
        "N14DecorFloorCardboard", "N14DecorFloorBrickrubble", "N14DecorFloorBrickStack",
        "N14DecorFloorTrashbags1", "N14DecorFloorTrashbags4", "N14DecorFloorTrashbags6",
        "N14DecorFloorFood1", "N14DecorFloorFood2", "N14DecorFloorScrapwood",
        "N14DecorFloorSkeleton", "N14DecorFloorPallet",
    };

    private static readonly string[] FloorScatterMetro =
    {
        "N14DecorFloorPaper", "N14DecorFloorGlass1", "N14DecorFloorCardboard",
        "N14DecorFloorScrapwood", "N14DecorFloorTrashbags2", "N14DecorFloorTrashbags5",
        "N14DecorFloorFood4", "N14DecorFloorFood5", "N14DecorFloorBrickrubble",
        "N14DecorFloorBookPile2",
    };

    // #Misfits Add - Blueprint scatter pool for armory/vault/hub rooms
    // #Misfits Change - Added vault-specific blueprints from Vault.yml analysis (T1-T4 tiers)
    private static readonly string[] BlueprintPool =
    {
        "N14BlueprintVaultWeaponsT1", "N14BlueprintVaultWeaponsT2",
        "N14BlueprintVaultWeaponsT3", "N14BlueprintVaultWeaponsT4",
        "N14BlueprintVaultArmorT1",  "N14BlueprintVaultArmorT2",
        "N14BlueprintVaultAmmoT1",   "N14BlueprintVaultAmmoT2",
        "N14BlueprintNCRWeaponsT1",  "N14BlueprintLegionWeaponsT1",
        "N14BlueprintNCRArmorT1",    "N14BlueprintLegionArmorT1",
    };

    // #Misfits Add - Junk pile scatter pools per theme (for lived-in visual atmosphere)
    // #Misfits Change - Added N14JunkPile1Refilling variants (respawn contents) from MercerIslandSewers.yml
    private static readonly string[] JunkPoolVault =
        { "N14JunkPile1", "N14JunkPile2", "N14JunkPile3", "N14JunkPile4", "N14JunkPile5", "N14JunkPile6",
          "N14JunkPile1Refilling2", "N14JunkPile1Refilling5" };

    private static readonly string[] JunkPoolSewer =
        { "N14JunkPile4", "N14JunkPile5", "N14JunkPile6", "N14JunkPile7", "N14JunkPile8",
          "N14JunkPile1Refilling3", "N14JunkPile1Refilling4", "N14JunkPile1Refilling9" };

    private static readonly string[] JunkPoolMetro =
        { "N14JunkPile7", "N14JunkPile8", "N14JunkPile9", "N14JunkPile10", "N14JunkPile11", "N14JunkPile12",
          "N14JunkPile1Refilling7", "N14JunkPile1Refilling10" };

    // Sewer room-specific pools
    private static readonly string[] FurnSewerTunnel =
    {
        "N14JunkBench", "CrateWooden", "N14JunkTable",
    };

    private static readonly string[] FurnSewerPump =
    {
        // #Misfits Change - Water treatment utility with tanks, barrels, and broken machines
        "N14StorageTankFullFuel", "N14YellowBarrel", "N14MachineWaterTreatmentBroken",
        "Rack", "N14JunkTable", "N14JunkBench", "N14BlackBarrel",
    };

    private static readonly string[] FurnSewerNest =
    {
        // #Misfits Change - Creature lair with dirty bedding and broken-down furniture
        "N14BedDirty", "N14BedMattressDirty2", "N14Bedroll",
        "N14JunkTable", "N14JunkBench", "CrateWooden",
        "N14JunkPile4", "N14JunkPile6",
    };

    private static readonly string[] FurnSewerGrotto =
    {
        "N14JunkBench", "TableWood", "Rack", "N14JunkCabinet",
        "N14JunkDresser", "N14JunkTable",
    };

    private static readonly string[] FurnSewerJunction =
    {
        "N14JunkTable", "CrateWooden", "Rack", "N14JunkBench",
    };

    // Metro room-specific pools
    private static readonly string[] FurnMetroTunnel =
    {
        // #Misfits Change - Replaced empty crate with loot variant, added junk
        "N14JunkBench", "N14LootCrateArmy", "Rack", "N14JunkPile9",
    };

    private static readonly string[] FurnMetroPlatform =
    {
        // #Misfits Change - Metro platform with benches, entertainment, and transit infra
        "N14JunkBench", "N14ChairMetalBlue", "N14ChairMetalGreen",
        "N14TableCounterBar", "N14JunkArcade", "N14JunkTV", "N14JunkJukebox",
        "N14LootCrateArmy", "N14JunkPile10", "N14JunkPile3",
    };

    private static readonly string[] FurnMetroMaintenance =
    {
        // #Misfits Change - Replaced empty crate with loot variant, added junk
        "Rack", "N14JunkMachine", "N14LootCrateArmy", "LockerSteel",
        "N14JunkCabinet", "N14JunkTable", "N14JunkBench",
        "N14JunkPile11", "N14JunkPile6",
    };

    private static readonly string[] FurnMetroDepot =
    {
        // #Misfits Change - Replaced empty crates with loot variants, added junk
        "N14LootCrateMilitary", "Rack", "N14JunkMachine", "N14JunkTable",
        "LockerSteel", "N14LootCrateArmy", "N14JunkBench",
        "N14JunkPile12", "N14JunkPile7",
    };

    private static readonly string[] FurnMetroCommand =
    {
        // #Misfits Change - Replaced empty crate with loot variant
        "TableWood", "LockerSecurity", "Rack", "N14LootCrateMilitary",
        "N14JunkCabinet", "N14JunkTable",
    };

    // #Misfits Add - Decal pools for thematic visual variety (verified IDs from Prototypes/Decals/)
    private static readonly string[] DecalsVault =
    {
        "DirtHeavy", "DirtMedium", "Damaged", "Rust",
        "burnt1", "burnt2", "Remains",
    };

    private static readonly string[] DecalsSewer =
    {
        "DirtHeavy", "DirtLight", "DirtMedium", "Dirt",
        "Damaged", "Rust", "DirtHeavyMonotile",
    };

    private static readonly string[] DecalsMetro =
    {
        "DirtLight", "DirtMedium", "Damaged", "Rust",
        "burnt3", "burnt4", "Remains",
    };

    // #Misfits Add - Hazard entity IDs per theme (verified IDs from Prototypes/)
    // NOTE: RadiationPulse and Acidifier have short lifetimes (~2s); suitable for
    //       flavor spawns but not persistent hazards. Future: create N14 hazard protos.
    private static readonly string[] HazardsVault =
    {
        "RadiationPulse",      // Shimmering radiation anomaly
        "SignRadiation",       // Radiation warning sign
    };

    private static readonly string[] HazardsSewer =
    {
        "Acidifier",           // Acid puddle hazard
        "SignBiohazard",       // Biohazard warning sign
    };

    private static readonly string[] HazardsMetro =
    {
        "RadiationPulse",      // Radiation anomaly
        "SignCorrosives",      // Corrosive materials warning sign
    };

    // =========================================================================
    // Public entry point
    // =========================================================================

    /// <summary>
    /// Generates a complete underground expedition map onto the provided grid.
    /// </summary>
    public void GenerateMap(UndergroundGenParams p, EntityUid gridUid, MapGridComponent grid)
    {
        var rng = new Random(p.Seed);
        int W   = p.GridWidth;
        int H   = p.GridHeight;

        // Phase A: place rooms
        var cellMap = new CellType[W, H];
        var rooms   = new List<RoomDef>();

        PlaceFactionHubs(cellMap, rooms, p, rng, W, H);
        PlaceCentralRoom(cellMap, rooms, p, rng, W, H);
        PlaceStandardRooms(cellMap, rooms, p, rng, W, H);

        // Phase B: 2-tile-wide MST corridors
        CarveCorridors(cellMap, rooms, rng, W, H);

        // Phase B5: identify corridor→room threshold cells — get door entities in Phase E
        var doorways = MarkDoorways(cellMap, W, H);

        // Phase C: sewer water channels (Sewer theme only)
        if (p.Theme == UndergroundTheme.Sewer)
            CarveSewerWaterChannels(cellMap, rng, W, H);

        // Phase D: tile every cell (including background; required for atmos sealing)
        PaintTiles(cellMap, gridUid, grid, p.Theme, rng, W, H);

        // Phase E: spawn all entities
        SpawnEntities(cellMap, rooms, doorways, gridUid, grid, p.Theme, p.DifficultyTier, rng, W, H);
    }

    // =========================================================================
    // Phase A — Room Placement
    // =========================================================================

    private static void PlaceFactionHubs(
        CellType[,] cellMap, List<RoomDef> rooms,
        UndergroundGenParams p, Random rng, int W, int H)
    {
        int hubCount = Math.Min(p.HubCount, 4);

        for (int i = 0; i < hubCount; i++)
        {
            // #Misfits Tweak - Reduced hub size: smaller footprint while still fitting 10+ player characters
            // (7–10 outer → 5–8 interior = 25–64 floor tiles, comfortable for 10 players)
            int hubW = rng.Next(7, 11);
            int hubH = rng.Next(7, 11);
            var (zx0, zy0, zx1, zy1) = GetHubZone(i, W, H);
            int maxX = Math.Max(zx0, zx1 - hubW);
            int maxY = Math.Max(zy0, zy1 - hubH);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                int x = rng.Next(zx0, maxX + 1);
                int y = rng.Next(zy0, maxY + 1);
                var cand = new RoomDef
                {
                    X = x, Y = y, W = hubW, H = hubH,
                    RoomType = RoomType.FactionHub, FactionIndex = i,
                };
                if (rooms.Any(r => r.Overlaps(cand, 2))) continue;
                rooms.Add(cand);
                PaintRoom(cellMap, cand, CellType.FactionHub, W, H);
                break;
            }
        }
    }

    private static (int x0, int y0, int x1, int y1) GetHubZone(int idx, int W, int H)
    {
        const int margin = 3;
        int thirdW = W / 3;
        int thirdH = H / 3;
        return idx switch
        {
            0 => (margin,     margin,      thirdW,     thirdH),
            1 => (W - thirdW, margin,      W - margin, thirdH),
            2 => (W - thirdW, H - thirdH,  W - margin, H - margin),
            3 => (margin,     H - thirdH,  thirdW,     H - margin),
            _ => (margin,     margin,      W / 2,      H / 2),
        };
    }

    private static void PlaceCentralRoom(
        CellType[,] cellMap, List<RoomDef> rooms,
        UndergroundGenParams p, Random rng, int W, int H)
    {
        // Large central room near the grid centre — primary objective area
        int roomW = rng.Next(12, 20);
        int roomH = rng.Next(12, 20);
        int baseCx = W / 2 - roomW / 2;
        int baseCy = H / 2 - roomH / 2;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int x = Math.Clamp(baseCx + rng.Next(-5, 6), 3, W - roomW - 3);
            int y = Math.Clamp(baseCy + rng.Next(-5, 6), 3, H - roomH - 3);
            var cand = new RoomDef { X = x, Y = y, W = roomW, H = roomH, RoomType = RoomType.Central };
            if (rooms.Any(r => r.Overlaps(cand, 2))) continue;
            rooms.Add(cand);
            PaintRoom(cellMap, cand, CellType.Room, W, H);
            return;
        }

        // Fallback: force-place at exact centre
        var fb = new RoomDef
        {
            X = Math.Clamp(baseCx, 3, W - roomW - 3),
            Y = Math.Clamp(baseCy, 3, H - roomH - 3),
            W = roomW, H = roomH, RoomType = RoomType.Central,
        };
        rooms.Add(fb);
        PaintRoom(cellMap, fb, CellType.Room, W, H);
    }

    private static void PlaceStandardRooms(
        CellType[,] cellMap, List<RoomDef> rooms,
        UndergroundGenParams p, Random rng, int W, int H)
    {
        int placed = 0;
        int budget = p.MaxRooms * 6;

        for (int attempt = 0; attempt < budget && placed < p.MaxRooms; attempt++)
        {
            var (rw, rh) = GetRoomDimensions(p.Theme, rng);
            if (rw > W - 6 || rh > H - 6) continue;

            int x = rng.Next(3, W - rw - 3);
            int y = rng.Next(3, H - rh - 3);
            // #Misfits Change - Assign thematic room type instead of generic Standard
            var cand = new RoomDef { X = x, Y = y, W = rw, H = rh, RoomType = GetThematicRoomType(p.Theme, rng) };
            if (rooms.Any(r => r.Overlaps(cand, 2))) continue;
            rooms.Add(cand);
            PaintRoom(cellMap, cand, CellType.Room, W, H);
            placed++;
        }
    }

    /// <summary>
    /// Returns theme-specific room dimensions with shape variation.
    /// Sewer = elongated tunnels; Metro = wide platforms; Vault = varied command rooms.
    /// </summary>
    private static (int w, int h) GetRoomDimensions(UndergroundTheme theme, Random rng)
    {
        switch (theme)
        {
            case UndergroundTheme.Sewer:
            {
                // Mix elongated and square shapes for interesting tunnel feel
                return rng.Next(10) switch
                {
                    < 4 => (rng.Next(5, 9),   rng.Next(12, 22)), // tall narrow tunnel
                    < 8 => (rng.Next(12, 22), rng.Next(5, 9)),   // wide flat tunnel
                    _   => (rng.Next(7,  13), rng.Next(7, 13)),  // square antechamber
                };
            }
            case UndergroundTheme.Metro:
            {
                // Metro platforms — predominantly long
                return rng.Next(10) switch
                {
                    < 5 => (rng.Next(16, 26), rng.Next(6, 10)),  // long E/W platform
                    < 8 => (rng.Next(6,  10), rng.Next(16, 26)), // long N/S platform
                    _   => (rng.Next(10, 16), rng.Next(10, 16)), // standard room
                };
            }
            default: // Vault
            {
                return rng.Next(10) switch
                {
                    < 3 => (rng.Next(6, 9),   rng.Next(6, 9)),  // small storage
                    < 7 => (rng.Next(9, 15),  rng.Next(9, 15)), // medium office/lab
                    _   => (rng.Next(12, 18), rng.Next(10, 16)),// large command room
                };
            }
        }
    }

    private static void PaintRoom(CellType[,] cellMap, RoomDef room, CellType type, int W, int H)
    {
        for (int x = room.X; x < room.X + room.W && x < W; x++)
        for (int y = room.Y; y < room.Y + room.H && y < H; y++)
            cellMap[x, y] = type;
    }

    // =========================================================================
    // Phase B — Corridor Carving (minimum spanning tree, 2-tile-wide)
    // =========================================================================

    private static void CarveCorridors(CellType[,] cellMap, List<RoomDef> rooms, Random rng, int W, int H)
    {
        if (rooms.Count < 2) return;

        var connected   = new List<RoomDef> { rooms[0] };
        var unconnected = new List<RoomDef>(rooms.Skip(1));

        while (unconnected.Count > 0)
        {
            RoomDef? bestFrom = null;
            RoomDef? bestTo   = null;
            float    bestDist = float.MaxValue;

            foreach (var from in connected)
            {
                var (fx, fy) = from.Center;
                foreach (var to in unconnected)
                {
                    var (tx, ty) = to.Center;
                    float dist = (tx - fx) * (tx - fx) + (ty - fy) * (ty - fy);
                    if (dist < bestDist) { bestDist = dist; bestFrom = from; bestTo = to; }
                }
            }

            if (bestFrom == null || bestTo == null) break;

            var (ax, ay) = bestFrom.Center;
            var (bx, by) = bestTo.Center;
            CarveLCorridor(cellMap, ax, ay, bx, by, rng, W, H);

            connected.Add(bestTo!);
            unconnected.Remove(bestTo!);
        }
    }

    private static void CarveLCorridor(CellType[,] cellMap, int ax, int ay, int bx, int by,
                                        Random rng, int W, int H)
    {
        if (rng.Next(2) == 0) { CarveHLine(cellMap, ax, bx, ay, W, H); CarveVLine(cellMap, bx, ay, by, W, H); }
        else                  { CarveVLine(cellMap, ax, ay, by, W, H); CarveHLine(cellMap, ax, bx, by, W, H); }
    }

    private static void CarveHLine(CellType[,] cellMap, int x0, int x1, int y, int W, int H)
    {
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        for (int x = minX; x <= maxX; x++)
        {
            TrySetCorridor(cellMap, x, y,     W, H);
            TrySetCorridor(cellMap, x, y + 1, W, H); // 2-tile wide
        }
    }

    private static void CarveVLine(CellType[,] cellMap, int x, int y0, int y1, int W, int H)
    {
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);
        for (int y = minY; y <= maxY; y++)
        {
            TrySetCorridor(cellMap, x,     y, W, H);
            TrySetCorridor(cellMap, x + 1, y, W, H); // 2-tile wide
        }
    }

    private static void TrySetCorridor(CellType[,] cellMap, int x, int y, int W, int H)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        if (cellMap[x, y] == CellType.Empty)
            cellMap[x, y] = CellType.Corridor;
    }

    // =========================================================================
    // Phase B5 — Doorway Marking
    // =========================================================================

    /// <summary>
    /// Returns the set of corridor cells that sit directly on the room/corridor boundary.
    /// These cells receive door entities instead of wall entities in Phase E.
    /// With 2-tile corridors this naturally creates double doors at each entry.
    /// </summary>
    // #Misfits Fix - Cluster adjacent doorway candidates, keep max 2 per cluster
    // to prevent long walls of doors (7+ N14DoorBunker in a row)
    private static HashSet<(int, int)> MarkDoorways(CellType[,] cellMap, int W, int H)
    {
        // Step 1: Find all corridor cells adjacent to a room/hub
        var candidates = new HashSet<(int, int)>();
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            if (cellMap[x, y] != CellType.Corridor)
                continue;

            if (IsRoomAt(cellMap, x + 1, y, W, H) || IsRoomAt(cellMap, x - 1, y, W, H) ||
                IsRoomAt(cellMap, x, y + 1, W, H) || IsRoomAt(cellMap, x, y - 1, W, H))
            {
                candidates.Add((x, y));
            }
        }

        // Step 2: Flood-fill cluster adjacent candidates
        var visited = new HashSet<(int, int)>();
        var clusters = new List<List<(int, int)>>();

        foreach (var cell in candidates)
        {
            if (visited.Contains(cell)) continue;

            var cluster = new List<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue(cell);
            visited.Add(cell);

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                cluster.Add((cx, cy));
                // Check 4 cardinal neighbours
                (int, int)[] neighbours = { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) };
                foreach (var nb in neighbours)
                {
                    if (candidates.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }
            clusters.Add(cluster);
        }

        // Step 3: From each cluster, keep at most 2 cells (centred)
        var doorways = new HashSet<(int, int)>();
        foreach (var cluster in clusters)
        {
            if (cluster.Count <= 2)
            {
                foreach (var c in cluster)
                    doorways.Add(c);
            }
            else
            {
                // Sort by position, take the 2 most central cells
                cluster.Sort((a, b) => a.Item1 != b.Item1
                    ? a.Item1.CompareTo(b.Item1)
                    : a.Item2.CompareTo(b.Item2));
                int mid = cluster.Count / 2;
                doorways.Add(cluster[mid - 1]);
                doorways.Add(cluster[mid]);
            }
        }

        return doorways;
    }

    private static bool IsRoomAt(CellType[,] cellMap, int x, int y, int W, int H)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return cellMap[x, y] is CellType.Room or CellType.FactionHub;
    }

    // =========================================================================
    // Phase C — Sewer Water Channels
    // =========================================================================

    private static void CarveSewerWaterChannels(CellType[,] cellMap, Random rng, int W, int H)
    {
        int channelCount = rng.Next(2, 5);

        for (int i = 0; i < channelCount; i++)
        {
            bool horizontal = rng.Next(2) == 0;

            if (horizontal)
            {
                int y      = rng.Next(H / 4, H * 3 / 4);
                int xStart = rng.Next(2, W / 4);
                int xEnd   = rng.Next(W * 3 / 4, W - 2);
                for (int x = xStart; x <= xEnd; x++)
                {
                    if (cellMap[x, y] == CellType.Empty)     cellMap[x, y]     = CellType.WaterChannel;
                    if (y + 1 < H && cellMap[x, y + 1] == CellType.Empty) cellMap[x, y + 1] = CellType.WaterChannel;
                }
            }
            else
            {
                int x      = rng.Next(W / 4, W * 3 / 4);
                int yStart = rng.Next(2, H / 4);
                int yEnd   = rng.Next(H * 3 / 4, H - 2);
                for (int y = yStart; y <= yEnd; y++)
                {
                    if (cellMap[x, y] == CellType.Empty)         cellMap[x, y]     = CellType.WaterChannel;
                    if (x + 1 < W && cellMap[x + 1, y] == CellType.Empty) cellMap[x + 1, y] = CellType.WaterChannel;
                }
            }
        }
    }

    // =========================================================================
    // Phase D — Tile Painting
    // =========================================================================

    private void PaintTiles(
        CellType[,] cellMap, EntityUid gridUid, MapGridComponent grid,
        UndergroundTheme theme, Random rng, int W, int H)
    {
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            string tileId = cellMap[x, y] switch
            {
                CellType.Corridor     => GetCorridorTile(theme, rng),
                CellType.Room         => GetRoomTile(theme, rng),
                CellType.FactionHub   => GetHubTile(theme, rng),
                CellType.WaterChannel => TileWaterDeep,
                CellType.Platform     => TileGrate,
                // Empty cells (both room perimeter and background) need a tile so
                // wall entities spawn correctly and atmos cannot escape to void.
                _                     => TileBackground,
            };

            SetTile(gridUid, grid, x, y, tileId);
        }
    }

    // #Misfits Change - Expanded tile variety for all themes
    private static string GetCorridorTile(UndergroundTheme theme, Random rng) => theme switch
    {
        UndergroundTheme.Vault => rng.Next(14) switch
        {
            // #Misfits Fix - Removed TileRubble from Vault corridors (80%+ concrete/metal)
            0 or 1 => TileVaultAlt,
            2 or 3 => TileVaultRusty,
            4      => TileVaultConcrete,
            5      => TileVaultSteelDirty,
            6      => TileVaultIndustrial,
            _      => TileVaultFloor,
        },
        UndergroundTheme.Sewer => rng.Next(14) switch
        {
            0 or 1 => TileSewerGrate,
            2      => TileSewerDirtNew,
            3      => TileSewerCave,
            4      => TileSewerBrick,
            _      => TileSewerDirt,
        },
        UndergroundTheme.Metro => rng.Next(14) switch
        {
            0 or 1 => TileMetroGrate,
            2      => TileMetroTile,
            3      => TileMetroIndustrial,
            4      => TileMetroSteelDirty,
            _      => TileMetroDark,
        },
        _ => TileVaultFloor,
    };

    // #Misfits Change - Expanded room tile variety
    private static string GetRoomTile(UndergroundTheme theme, Random rng) => theme switch
    {
        UndergroundTheme.Vault => rng.Next(14) switch
        {
            // #Misfits Fix - Removed TileRubble from Vault rooms (use concrete/metal 85%+)
            0 or 1 => TileVaultConcrete,
            2 or 3 => TileVaultRusty,
            4      => TileVaultAlt,
            5      => TileVaultSteelDirty,
            6      => TileVaultConcDark,
            7      => TileVaultIndustrial,
            _      => TileVaultFloor,
        },
        UndergroundTheme.Sewer => rng.Next(14) switch
        {
            0 or 1 => TileSewerGrate,
            2      => TileSewerConcrete,
            3      => TileSewerDirtNew,
            4      => TileSewerCave,
            5      => TileSewerBrick,
            _      => TileSewerDirt,
        },
        UndergroundTheme.Metro => rng.Next(14) switch
        {
            0 or 1 => TileMetroGrate,
            2      => TileMetroConcrete,
            3      => TileMetroDarkSolid,
            4      => TileMetroTile,
            5      => TileMetroIndustrial,
            6      => TileMetroSteelDirty,
            7      => TileMetroConcAlt,
            _      => TileMetroDark,
        },
        _ => TileVaultFloor,
    };

    // #Misfits Change - Expanded hub tile variety
    private static string GetHubTile(UndergroundTheme theme, Random rng) => theme switch
    {
        UndergroundTheme.Vault => rng.Next(10) switch
        {
            0      => TileVaultAlt,
            1      => TileVaultConcDark,
            _      => TileVaultFloor,
        },
        UndergroundTheme.Sewer => rng.Next(8) switch
        {
            0      => TileSewerGrate,
            1      => TileSewerBrick,
            _      => TileSewerDirt,
        },
        UndergroundTheme.Metro => rng.Next(8) switch
        {
            0      => TileMetroTile,
            1      => TileMetroIndustrial,
            _      => TileMetroDark,
        },
        _                      => TileVaultFloor,
    };

    // =========================================================================
    // Phase E — Entity Spawning
    // =========================================================================

    private void SpawnEntities(
        CellType[,] cellMap, List<RoomDef> rooms, HashSet<(int, int)> doorways,
        EntityUid gridUid, MapGridComponent grid,
        UndergroundTheme theme, int difficultyTier, Random rng, int W, int H)
    {
        // ── 1. Wall and water pass ─────────────────────────────────────────────
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            var cell = cellMap[x, y];

            if (cell == CellType.WaterChannel)
            {
                SpawnAt(WaterSewerEntity, gridUid, grid, x, y);
                continue;
            }

            if (cell != CellType.Empty)
                continue;

            // Room-perimeter empty cells get slanted theme walls (concrete / brick / dungeon).
            // All other empty cells (corridor edges + far background) get indestructible WallRock.
            // #Misfits Fix - use IsAdjacentToTraversable so corridor edges also get theme walls
            bool adjRoom = IsAdjacentToTraversable(cellMap, x, y, W, H);
            string wallProto = adjRoom
                ? GetRoomWallProto(theme, cellMap, x, y, W, H)
                : WallRockFill;

            SpawnAt(wallProto, gridUid, grid, x, y);
        }

        // ── 2. Door pass: threshold cells between corridor and room ────────────
        foreach (var (dx, dy) in doorways)
        {
            // #Misfits Fix - Skip doors with no corridor on the approach side (prevents dead-end doors vs rock)
            bool hasCorridor =
                (InBounds(dx + 1, dy, W, H) && cellMap[dx + 1, dy] == CellType.Corridor) ||
                (InBounds(dx - 1, dy, W, H) && cellMap[dx - 1, dy] == CellType.Corridor) ||
                (InBounds(dx, dy + 1, W, H) && cellMap[dx, dy + 1] == CellType.Corridor) ||
                (InBounds(dx, dy - 1, W, H) && cellMap[dx, dy - 1] == CellType.Corridor);
            if (!hasCorridor) continue;

            bool isHub = (InBounds(dx + 1, dy, W, H) && cellMap[dx + 1, dy] == CellType.FactionHub)
                       || (InBounds(dx - 1, dy, W, H) && cellMap[dx - 1, dy] == CellType.FactionHub)
                       || (InBounds(dx, dy + 1, W, H) && cellMap[dx, dy + 1] == CellType.FactionHub)
                       || (InBounds(dx, dy - 1, W, H) && cellMap[dx, dy - 1] == CellType.FactionHub);

            SpawnAt(isHub ? GetHubDoor(theme) : GetRoomDoor(theme), gridUid, grid, dx, dy);
        }

        // ── 3. Room dressing: furniture + mobs + lights + decals ──────────────
        // #Misfits Add - Phase E sub-passes for lights and decals
        var decalPool = GetDecalPool(theme);
        
        foreach (var room in rooms)
        {
            DressRoom(room, gridUid, grid, theme, rng);
            SpawnRoomMobs(room, gridUid, grid, theme, difficultyTier, rng);

            // Sub-pass: Lights
            // #Misfits Fix - Mount Vault/Sewer lights on walls facing inward;
            // Metro uses ground-level post lights on random interior tiles.
            int lightCount = GetLightCount(room.RoomType);
            int innerW = room.W - 2;
            int innerH = room.H - 2;
            
            if (innerW > 0 && innerH > 0)
            {
                var taken = new HashSet<(int, int)>();

                if (theme == UndergroundTheme.Metro)
                {
                    // Metro: LightPostSmall is a ground post — random interior placement
                    for (int i = 0; i < lightCount && taken.Count < lightCount; i++)
                    {
                        for (int attempt = 0; attempt < 5; attempt++)
                        {
                            int lx = room.X + 1 + rng.Next(innerW);
                            int ly = room.Y + 1 + rng.Next(innerH);
                            if (taken.Contains((lx, ly))) continue;
                            SpawnLight(gridUid, grid, lx, ly, theme, Direction.South);
                            taken.Add((lx, ly));
                            break;
                        }
                    }
                }
                else
                {
                    // Vault/Sewer: wall-mounted lights face inward from adjacent walls
                    var wallCandidates = new List<(int x, int y, Direction facing)>();
                    for (int lx = room.X + 1; lx < room.X + room.W - 1; lx++)
                    for (int ly = room.Y + 1; ly < room.Y + room.H - 1; ly++)
                    {
                        // Find interior tiles adjacent to a wall (Empty cell)
                        if (InBounds(lx, ly + 1, W, H) && cellMap[lx, ly + 1] == CellType.Empty)
                            wallCandidates.Add((lx, ly, Direction.North));   // wall to north, mount facing north
                        else if (InBounds(lx, ly - 1, W, H) && cellMap[lx, ly - 1] == CellType.Empty)
                            wallCandidates.Add((lx, ly, Direction.South));   // wall to south
                        else if (InBounds(lx + 1, ly, W, H) && cellMap[lx + 1, ly] == CellType.Empty)
                            wallCandidates.Add((lx, ly, Direction.East));    // wall to east
                        else if (InBounds(lx - 1, ly, W, H) && cellMap[lx - 1, ly] == CellType.Empty)
                            wallCandidates.Add((lx, ly, Direction.West));    // wall to west
                    }

                    // Shuffle candidates for randomness
                    for (int i = wallCandidates.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (wallCandidates[i], wallCandidates[j]) = (wallCandidates[j], wallCandidates[i]);
                    }

                    // Pick wall positions with minimum spacing of 3 tiles apart
                    foreach (var (wx, wy, facing) in wallCandidates)
                    {
                        if (taken.Count >= lightCount) break;

                        bool tooClose = false;
                        foreach (var (tx, ty) in taken)
                        {
                            if (Math.Abs(wx - tx) + Math.Abs(wy - ty) < 3)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;

                        SpawnLight(gridUid, grid, wx, wy, theme, facing);
                        taken.Add((wx, wy));
                    }
                }

                // Sub-pass: Decals via DecalSystem (thematic visual storytelling)
                int decalCount = rng.Next(2, 7);
                var decalTaken = new HashSet<(int, int)>();
                for (int i = 0; i < decalCount && decalTaken.Count < decalCount; i++)
                {
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        int dx = room.X + 1 + rng.Next(innerW);
                        int dy = room.Y + 1 + rng.Next(innerH);
                        if (decalTaken.Contains((dx, dy))) continue;
                        if (decalPool.Length > 0)
                        {
                            var decalProto = decalPool[rng.Next(decalPool.Length)];
                            var decalCoords = _mapSystem.GridTileToLocal(gridUid, grid, new Vector2i(dx, dy));
                            _decalSystem.TryAddDecal(decalProto, decalCoords, out _);
                        }
                        decalTaken.Add((dx, dy));
                        break;
                    }
                }
            }
        }

        // ── 4. Exit point pass: spawn expedition exit markers at FactionHub centers ────
        // #Misfits Add - N14ExpeditionExitPoint spawned directly by generator at hub centers
        // (N14ExpeditionSystem.SpawnExitPoints is broken for procedural maps — empty FactionSpawns)
        foreach (var hub in rooms.Where(r => r.RoomType == RoomType.FactionHub))
        {
            var (hubCx, hubCy) = hub.Center;
            SpawnAt("N14ExpeditionExitPoint", gridUid, grid, hubCx, hubCy);
        }

        // ── 5. Large-room sentry guardian pass ──────────────────────────────────
        // #Misfits Add - Rooms larger than 100 tiles have 15% chance for a guarding sentry bot + loot
        foreach (var room in rooms)
        {
            if (room.W * room.H <= 100) continue;
            if (rng.Next(100) >= 15) continue;
            var (scx, scy) = room.Center;
            // Alternate between two sentry variants for variety
            string sentryProto = rng.Next(2) == 0 ? "N14MobRobotSentryBot" : "N14MobRobotSentryBotBallistic";
            SpawnAt(sentryProto, gridUid, grid, scx, scy);
            // Spawn a rusted loot crate beside the sentry as a reward
            if (InBounds(scx + 1, scy, W, H))
                SpawnAt("N14LootCrateVaultBigRusted", gridUid, grid, scx + 1, scy);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wall selection helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the slanted theme wall for an empty cell adjacent to room interior.
    /// Cells adjacent to a FactionHub get the hub wall variant for visual distinction.
    /// </summary>
    private static string GetRoomWallProto(UndergroundTheme theme, CellType[,] cellMap,
                                            int x, int y, int W, int H)
    {
        bool adjHub = (InBounds(x + 1, y, W, H) && cellMap[x + 1, y] == CellType.FactionHub)
                   || (InBounds(x - 1, y, W, H) && cellMap[x - 1, y] == CellType.FactionHub)
                   || (InBounds(x, y + 1, W, H) && cellMap[x, y + 1] == CellType.FactionHub)
                   || (InBounds(x, y - 1, W, H) && cellMap[x, y - 1] == CellType.FactionHub);

        return (theme, adjHub) switch
        {
            (UndergroundTheme.Vault, true)  => WallVaultHub,
            (UndergroundTheme.Vault, false) => WallVaultRoom,
            (UndergroundTheme.Sewer, true)  => WallSewerHub,
            (UndergroundTheme.Sewer, false) => WallSewerRoom,
            (UndergroundTheme.Metro, true)  => WallMetroHub,
            (UndergroundTheme.Metro, false) => WallMetroRoom,
            _                               => WallVaultRoom,
        };
    }

    private static string GetHubDoor(UndergroundTheme theme) => theme switch
    {
        UndergroundTheme.Vault  => DoorVaultHub,
        UndergroundTheme.Sewer  => DoorSewerHub,
        UndergroundTheme.Metro  => DoorMetroHub,
        _                       => DoorVaultHub,
    };

    private static string GetRoomDoor(UndergroundTheme theme) => theme switch
    {
        UndergroundTheme.Vault  => DoorVaultRoom,
        UndergroundTheme.Sewer  => DoorSewerRoom,
        UndergroundTheme.Metro  => DoorMetroRoom,
        _                       => DoorVaultRoom,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Room Dressing — furniture placement
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scatters themed furniture inside the room, 1 tile inset from the perimeter.
    /// Larger rooms and hubs receive more items. Item count is room-type-specific.
    /// #Misfits Change - Item counts now vary by specific room type for thematic variety
    /// </summary>
    private void DressRoom(RoomDef room, EntityUid gridUid, MapGridComponent grid,
                            UndergroundTheme theme, Random rng)
    {
        int innerW = room.W - 2;
        int innerH = room.H - 2;
        if (innerW < 1 || innerH < 1) return;

        // #Misfits Change - Raised all item counts for "lived-in" room feel; new room types added
        int itemCount = room.RoomType switch
        {
            RoomType.FactionHub       => rng.Next(12, 19),
            RoomType.Central          => rng.Next(10, 17),
            RoomType.VaultOverseer    => rng.Next(8, 15),
            RoomType.VaultVault       => rng.Next(8, 15),
            RoomType.VaultArmory      => rng.Next(8, 15),
            RoomType.VaultBarracks    => rng.Next(6, 13),
            RoomType.VaultLab         => rng.Next(6, 13),
            RoomType.VaultKitchen     => rng.Next(6, 13),
            RoomType.VaultHydroponics => rng.Next(6, 12),  // #Misfits Add
            RoomType.VaultRecreation  => rng.Next(8, 14),  // #Misfits Add
            RoomType.VaultReactor     => rng.Next(4, 9),
            RoomType.SewerGrotto      => rng.Next(4, 9),
            RoomType.SewerPump        => rng.Next(4, 9),
            RoomType.SewerNest        => rng.Next(4, 9),
            RoomType.SewerCamp        => rng.Next(5, 10),  // #Misfits Add
            RoomType.SewerJunction    => rng.Next(2, 6),
            RoomType.SewerTunnel      => rng.Next(1, 3),
            RoomType.MetroPlatform    => rng.Next(6, 12),
            RoomType.MetroMaintenance => rng.Next(4, 9),
            RoomType.MetroDepot       => rng.Next(4, 9),
            RoomType.MetroCommand     => rng.Next(6, 12),
            RoomType.MetroTunnel      => rng.Next(1, 3),
            _                         => rng.Next(2, 6),
        };

        var pool  = PickFurniturePool(theme, room.RoomType);
        var taken = new HashSet<(int, int)>();
        int budget = itemCount * 5;

        for (int i = 0; i < budget && taken.Count < itemCount; i++)
        {
            int fx = room.X + 1 + rng.Next(innerW);
            int fy = room.Y + 1 + rng.Next(innerH);
            if (taken.Contains((fx, fy))) continue;

            SpawnAt(pool[rng.Next(pool.Length)], gridUid, grid, fx, fy);
            taken.Add((fx, fy));
        }

        // ── Junk scatter: 1-3 junk piles for a lived-in, grungy atmosphere ─────
        // #Misfits Add - Floor junk scatter gives "world with a past" storytelling feel
        var junkPool = theme switch
        {
            UndergroundTheme.Vault => JunkPoolVault,
            UndergroundTheme.Sewer => JunkPoolSewer,
            UndergroundTheme.Metro => JunkPoolMetro,
            _                      => JunkPoolVault,
        };
        int junkCount = rng.Next(1, 4);
        for (int j = 0; j < junkCount; j++)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int jx = room.X + 1 + rng.Next(innerW);
                int jy = room.Y + 1 + rng.Next(innerH);
                if (taken.Contains((jx, jy))) continue;
                SpawnAt(junkPool[rng.Next(junkPool.Length)], gridUid, grid, jx, jy);
                taken.Add((jx, jy));
                break;
            }
        }

        // ── Floor entity scatter: 3–8 N14DecorFloor* debris entities per room ──────
        // #Misfits Add - Physical floor debris entities (papers, glass, cardboard, skeletons)
        // Sourced from Vault.yml / MercerIslandSewers.yml — every hand-crafted room uses these densely.
        var scatterPool = theme switch
        {
            UndergroundTheme.Vault => FloorScatterVault,
            UndergroundTheme.Sewer => FloorScatterSewer,
            UndergroundTheme.Metro => FloorScatterMetro,
            _                      => FloorScatterVault,
        };
        int scatterCount = rng.Next(3, 9);
        for (int s = 0; s < scatterCount; s++)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int sx = room.X + 1 + rng.Next(innerW);
                int sy = room.Y + 1 + rng.Next(innerH);
                if (taken.Contains((sx, sy))) continue;
                SpawnAt(scatterPool[rng.Next(scatterPool.Length)], gridUid, grid, sx, sy);
                taken.Add((sx, sy));
                break;
            }
        }

        // ── Blueprint scatter: 5% chance in armory/vault/hub rooms ──────────────
        // #Misfits Add - Rare blueprint reward in high-value rooms
        bool isBlueprintRoom = room.RoomType is RoomType.VaultArmory or RoomType.VaultVault or RoomType.FactionHub;
        if (isBlueprintRoom && rng.Next(100) < 5)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int bx = room.X + 1 + rng.Next(innerW);
                int by = room.Y + 1 + rng.Next(innerH);
                if (taken.Contains((bx, by))) continue;
                SpawnAt(BlueprintPool[rng.Next(BlueprintPool.Length)], gridUid, grid, bx, by);
                break;
            }
        }
    }

    private static string[] PickFurniturePool(UndergroundTheme theme, RoomType roomType) =>
        GetFurniturePool(roomType, theme);


    // ─────────────────────────────────────────────────────────────────────────
    // Mob Spawning
    // ─────────────────────────────────────────────────────────────────────────
    // #Misfits Change - Mob spawning now uses room-type-specific spawn counts
    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnRoomMobs(RoomDef room, EntityUid gridUid, MapGridComponent grid,
                                UndergroundTheme theme, int difficultyTier, Random rng)
    {
        // #Misfits Fix - FactionHub rooms are player spawn points; never populate with hostile NPCs
        if (room.RoomType == RoomType.FactionHub) return;

        // Room types vary in how likely they are to have spawning mobs
        int spawnChance = room.RoomType switch
        {
            RoomType.Central         => 100, // primary objective — always contested
            // #Misfits Removed - FactionHub was 90%; now blocked above (player spawn area)
            //RoomType.FactionHub      => 90,
            RoomType.VaultOverseer   => 80,  // guarded command
            RoomType.VaultArmory     => 85,  // contested weapon cache
            RoomType.VaultVault      => 70,  // treasure room (occasional guardian)
            RoomType.SewerNest       => 95,  // creature hive (always spawning)
            RoomType.SewerPump       => 60,  // utility area (occasional)
            RoomType.MetroCommand    => 80,  // control center (guarded)
            RoomType.MetroPlatform   => 70,  // major hub (contested)
            RoomType.MetroDepot      => 75,  // cargo area (scavengers)
            _                        => 50,  // standard room (rare spawning)
        };

        if (rng.Next(100) >= spawnChance) return;

        // Use room-type-specific spawn count helper
        int mobCount = GetMobSpawnCount(room.RoomType, difficultyTier);

        // #Misfits Change - Pick one faction sub-group per room to avoid infighting
        var groups = GetMobGroups(theme);
        var pool   = groups[rng.Next(groups.Length)];
        int total  = pool.Sum(m => m.Item2);

        int innerW = room.W - 2;
        int innerH = room.H - 2;
        if (innerW < 1 || innerH < 1) return;

        var taken = new HashSet<(int, int)>();

        for (int i = 0; i < mobCount; i++)
        {
            // Weighted random pick from pool
            int roll  = rng.Next(total);
            int cumul = 0;
            string mob = pool[0].Item1;
            foreach (var (proto, weight) in pool)
            {
                cumul += weight;
                if (roll < cumul) { mob = proto; break; }
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int mx = room.X + 1 + rng.Next(innerW);
                int my = room.Y + 1 + rng.Next(innerH);
                if (taken.Contains((mx, my))) continue;
                SpawnAt(mob, gridUid, grid, mx, my);
                taken.Add((mx, my));
                break;
            }
        }
    }

    // #Misfits Change - Faction sub-group selector replaces flat GetMobPool
    private static (string, int)[][] GetMobGroups(UndergroundTheme theme) => theme switch
    {
        UndergroundTheme.Vault  => MobsVaultGroups,
        UndergroundTheme.Sewer  => MobsSewerGroups,
        UndergroundTheme.Metro  => MobsMetroGroups,
        _                       => MobsVaultGroups,
    };

    // =========================================================================
    // Tile helper
    // =========================================================================

    private void SetTile(EntityUid gridUid, MapGridComponent grid, int x, int y, string tileId)
    {
        var tileDef = _tileDefManager[tileId];
        _mapSystem.SetTile(gridUid, grid, new Vector2i(x, y), new Tile(tileDef.TileId));
    }

    // =========================================================================
    // Spawn helper
    // =========================================================================

    private void SpawnAt(string proto, EntityUid gridUid, MapGridComponent grid, int x, int y)
    {
        var coords = _mapSystem.GridTileToLocal(gridUid, grid, new Vector2i(x, y));
        Spawn(proto, coords);
    }

    // =========================================================================
    // Grid helpers
    // =========================================================================

    // #Misfits Fix - Renamed and expanded: theme walls now placed adjacent to corridors too,
    // preventing jarring rock walls at corridor edges
    private static bool IsAdjacentToTraversable(CellType[,] cellMap, int x, int y, int W, int H)
    {
        return IsTraversableAt(cellMap, x + 1, y, W, H) || IsTraversableAt(cellMap, x - 1, y, W, H) ||
               IsTraversableAt(cellMap, x, y + 1, W, H) || IsTraversableAt(cellMap, x, y - 1, W, H);
    }

    private static bool IsTraversableAt(CellType[,] cellMap, int x, int y, int W, int H)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return cellMap[x, y] is CellType.Room or CellType.FactionHub or CellType.Corridor;
    }

    private static bool InBounds(int x, int y, int W, int H) =>
        x >= 0 && x < W && y >= 0 && y < H;

    // =========================================================================
    // #Misfits Add - Room Type Variety Helpers
    // =========================================================================

    /// <summary>
    /// Returns a weighted random room type appropriate for the given theme.
    /// Common types are selected more frequently; rare types less so.
    /// </summary>
    private static RoomType GetThematicRoomType(UndergroundTheme theme, Random rng)
    {
        int roll = rng.Next(100);
        return theme switch
        {
            UndergroundTheme.Vault => roll switch
            {
                // #Misfits Change - Added VaultHydroponics (7%) and VaultRecreation (5%); adjusted weights
                < 18 => RoomType.VaultBarracks,
                < 28 => RoomType.VaultKitchen,
                < 35 => RoomType.VaultHydroponics,  // #Misfits Add - 7% hydroponic bay
                < 40 => RoomType.VaultRecreation,   // #Misfits Add - 5% recreation room
                < 55 => RoomType.VaultLab,
                < 68 => RoomType.VaultArmory,
                < 78 => RoomType.VaultVault,
                < 88 => RoomType.VaultOverseer,
                _    => RoomType.VaultReactor,
            },
            UndergroundTheme.Sewer => roll switch
            {
                // #Misfits Change - Added SewerCamp (10%); adjusted other weights
                < 35 => RoomType.SewerTunnel,
                < 50 => RoomType.SewerJunction,
                < 62 => RoomType.SewerGrotto,
                < 72 => RoomType.SewerPump,
                < 82 => RoomType.SewerNest,
                _    => RoomType.SewerCamp,         // #Misfits Add - improvised survivor camp
            },
            UndergroundTheme.Metro => roll switch
            {
                < 30 => RoomType.MetroPlatform,
                < 50 => RoomType.MetroTunnel,
                < 70 => RoomType.MetroMaintenance,
                < 85 => RoomType.MetroDepot,
                _    => RoomType.MetroCommand,
            },
            _ => RoomType.VaultBarracks,
        };
    }

    /// <summary>
    /// Returns the furniture pool for the given room type and theme.
    /// Used by DressRoom() to select furniture entities to spawn.
    /// </summary>
    private static string[] GetFurniturePool(RoomType roomType, UndergroundTheme theme) =>
        (theme, roomType) switch
        {
            // Vault variants
            (UndergroundTheme.Vault, RoomType.VaultBarracks)    => FurnVaultBarracks,
            (UndergroundTheme.Vault, RoomType.VaultKitchen)     => FurnVaultKitchen,
            (UndergroundTheme.Vault, RoomType.VaultHydroponics) => FurnVaultHydroponics, // #Misfits Add
            (UndergroundTheme.Vault, RoomType.VaultRecreation)  => FurnVaultRecreation,  // #Misfits Add
            (UndergroundTheme.Vault, RoomType.VaultLab)         => FurnVaultLab,
            (UndergroundTheme.Vault, RoomType.VaultArmory)      => FurnVaultArmory,
            (UndergroundTheme.Vault, RoomType.VaultVault)       => FurnVaultVault,
            (UndergroundTheme.Vault, RoomType.VaultOverseer)    => FurnVaultOverseer,
            (UndergroundTheme.Vault, RoomType.VaultReactor)     => FurnVaultReactor,
            (UndergroundTheme.Vault, RoomType.FactionHub)       => FurnVaultHub,
            (UndergroundTheme.Vault, _)                         => FurnVaultStandard,

            // Sewer variants
            (UndergroundTheme.Sewer, RoomType.SewerTunnel)   => FurnSewerTunnel,
            (UndergroundTheme.Sewer, RoomType.SewerPump)     => FurnSewerPump,
            (UndergroundTheme.Sewer, RoomType.SewerNest)     => FurnSewerNest,
            (UndergroundTheme.Sewer, RoomType.SewerGrotto)   => FurnSewerGrotto,
            (UndergroundTheme.Sewer, RoomType.SewerJunction) => FurnSewerJunction,
            (UndergroundTheme.Sewer, RoomType.SewerCamp)     => FurnSewerCamp,      // #Misfits Add
            (UndergroundTheme.Sewer, RoomType.FactionHub)    => FurnSewerHub,
            (UndergroundTheme.Sewer, _)                      => FurnSewerStandard,

            // Metro variants
            (UndergroundTheme.Metro, RoomType.MetroTunnel)    => FurnMetroTunnel,
            (UndergroundTheme.Metro, RoomType.MetroPlatform)  => FurnMetroPlatform,
            (UndergroundTheme.Metro, RoomType.MetroMaintenance) => FurnMetroMaintenance,
            (UndergroundTheme.Metro, RoomType.MetroDepot)     => FurnMetroDepot,
            (UndergroundTheme.Metro, RoomType.MetroCommand)   => FurnMetroCommand,
            (UndergroundTheme.Metro, RoomType.FactionHub)     => FurnMetroHub,
            (UndergroundTheme.Metro, _)                       => FurnMetroStandard,

            _ => FurnVaultStandard,
        };

    /// <summary>
    /// Returns the decal pool (cosmetic overlays) for the given theme.
    /// Decals are scattered randomly in rooms for visual storytelling.
    /// </summary>
    private static string[] GetDecalPool(UndergroundTheme theme) => theme switch
    {
        UndergroundTheme.Vault => DecalsVault,
        UndergroundTheme.Sewer => DecalsSewer,
        UndergroundTheme.Metro => DecalsMetro,
        _ => DecalsVault,
    };

    /// <summary>
    /// Returns the hazard pool (dangerous entities) for the given theme.
    /// Hazards are spawned in select room types (labs, nests, etc.).
    /// </summary>
    private static string[] GetHazardPool(UndergroundTheme theme) => theme switch
    {
        UndergroundTheme.Vault => HazardsVault,
        UndergroundTheme.Sewer => HazardsSewer,
        UndergroundTheme.Metro => HazardsMetro,
        _ => HazardsVault,
    };

    /// <summary>
    /// Returns the number of lights to spawn in this room type.
    /// Larger and more important rooms get more lights.
    /// </summary>
    private static int GetLightCount(RoomType roomType) => roomType switch
    {
        RoomType.FactionHub       => 3,
        RoomType.VaultOverseer    => 3,
        RoomType.VaultVault       => 3,
        RoomType.MetroCommand     => 3,
        RoomType.MetroPlatform    => 3,
        RoomType.VaultRecreation  => 3,   // #Misfits Add - brighter rec room
        RoomType.Central          => 2,
        RoomType.VaultBarracks    => 2,
        RoomType.VaultKitchen     => 2,
        RoomType.VaultHydroponics => 2,   // #Misfits Add
        RoomType.VaultLab         => 2,
        RoomType.VaultArmory      => 2,
        RoomType.SewerGrotto      => 2,
        RoomType.SewerPump        => 2,
        RoomType.SewerCamp        => 1,   // #Misfits Add - campfire provides most light
        RoomType.MetroMaintenance => 2,
        RoomType.MetroDepot       => 2,
        _                         => 1,
    };

    /// <summary>
    /// Returns the number of mobs to spawn in this room, scaled by difficulty tier (1-3).
    /// </summary>
    private static int GetMobSpawnCount(RoomType roomType, int difficultyTier) =>
        roomType switch
        {
            // #Misfits Removed - FactionHub case removed; hubs never receive mob spawns (player entry rooms)
            //RoomType.FactionHub      => 2 + difficultyTier,
            RoomType.Central          => 2 + difficultyTier,
            RoomType.VaultOverseer    => 2,
            RoomType.VaultVault       => 1 + (difficultyTier / 2),
            RoomType.VaultKitchen     => 1,
            RoomType.VaultHydroponics => 1,                        // #Misfits Add - occasional wandering ghoul
            RoomType.VaultRecreation  => 1,                        // #Misfits Add
            RoomType.VaultArmory      => 3 + (difficultyTier / 2),
            RoomType.VaultReactor     => 1,
            RoomType.SewerNest        => 4 + difficultyTier,
            RoomType.SewerCamp        => 2 + difficultyTier,       // #Misfits Add - defended survivor camp
            RoomType.SewerPump        => 2,
            RoomType.MetroCommand     => 2 + (difficultyTier / 2),
            RoomType.MetroPlatform    => 2 + (difficultyTier / 2),
            _                         => 1,
        };

    /// <summary>
    /// Spawns a light entity at the given grid coordinate.
    /// Light brightness and color match the theme for visual coherence.
    /// </summary>
    // #Misfits Add - Light entity IDs per theme (all always-powered, no wiring needed)
    private const string LightVault  = "AlwaysPoweredWallLight";  // Clean wall light for pre-war vaults
    private const string LightSewer  = "N14TorchWall";            // Rustic torch for sewer tunnels
    private const string LightMetro  = "LightPostSmall";          // Ground-level post light for metro

    // #Misfits Fix - Accept direction for wall-mounted rotation
    private void SpawnLight(EntityUid gridUid, MapGridComponent grid, int x, int y,
        UndergroundTheme theme, Direction facing)
    {
        var coords = _mapSystem.GridTileToLocal(gridUid, grid, new Vector2i(x, y));
        // #Misfits Add - Theme-appropriate always-powered lights
        string lightProto = theme switch
        {
            UndergroundTheme.Vault => LightVault,
            UndergroundTheme.Sewer => LightSewer,
            UndergroundTheme.Metro => LightMetro,
            _ => LightVault,
        };
        var ent = Spawn(lightProto, coords);
        // Wall-mounted lights need rotation to face away from the wall
        if (theme != UndergroundTheme.Metro)
        {
            var xform = Transform(ent);
            xform.LocalRotation = facing.ToAngle();
        }
    }
}

