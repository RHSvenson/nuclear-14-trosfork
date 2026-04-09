using Content.Shared._Misfits.TribalHunt;
using Content.Shared.Destructible;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Handles spawning, tracking, and loot drops for legendary creatures during tribal hunts.
/// </summary>
public sealed partial class LegendaryCreatureSpawnerSystem : EntitySystem
{
    private const string DeathclawPrototype = "N14MobDeathclaw";

    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryCreatureComponent, DestructionEventArgs>(OnCreatureDestroyed);
    }

    /// <summary>
    /// Spawns a legendary creature at a random point on the target map.
    /// </summary>
    public EntityUid? TrySpawnLegendaryCreature(string creatureProto, EntityUid huntSessionId, MapId mapId)
    {
        var mapUid = _mapSystem.GetMap(mapId);
        var spawnCoords = new EntityCoordinates(mapUid, _random.NextVector2(100f, 500f));

        var creature = Spawn(DeathclawPrototype, spawnCoords);

        var legComp = EnsureComp<LegendaryCreatureComponent>(creature);
        legComp.HuntSessionId = huntSessionId;
        legComp.CreatureName = "Deathclaw";
        legComp.LeatherDropCount = 3;
        legComp.RevealLocation = true;
        Dirty(creature, legComp);

        return creature;
    }

    private void OnCreatureDestroyed(EntityUid uid, LegendaryCreatureComponent comp, DestructionEventArgs args)
    {
        for (int i = 0; i < comp.LeatherDropCount; i++)
        {
            SpawnNextToOrDrop("TribalLegendaryLeather", uid);
        }
    }
}
