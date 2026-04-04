using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Gravity;
using Content.Server.Procedural;
using Content.Shared._Misfits.Expeditions;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Procedural;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Expeditions;

/// <summary>
/// Manages the full expedition lifecycle:
/// - BUI open/message handling for the chalkboard board
/// - Launch countdown with group detection
/// - Map loading from salvage YML files
/// - Expedition timer with popup warnings at 5min, 1min, 30sec
/// - Early return via exit point interaction
/// - Force extraction and map cleanup on expiry
/// </summary>
public sealed class N14ExpeditionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GravitySystem _gravity = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    // #Misfits Add - procedural underground expedition generator
    [Dependency] private readonly UndergroundExpeditionMapGenerator _proceduralGen = default!;

    private int _expeditionSeedCounter;

    // #Misfits Tweak - Throttle session-timer scanning. Warnings fire at 10-min, 5-min, 1-min, 30-sec
    // intervals — sub-second resolution is unnecessary. Per-tick scan at 30 Hz is wasteful.
    private float _sessionScanAccumulator;
    private const float SessionScanInterval = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        // BUI events
        SubscribeLocalEvent<N14ExpeditionBoardComponent, AfterActivatableUIOpenEvent>(OnBoardOpened);
        SubscribeLocalEvent<N14ExpeditionBoardComponent, N14ExpeditionLaunchMessage>(OnLaunchMessage);

        // Exit point interaction (press E) and step-on/off trigger (walk over green flare)
        SubscribeLocalEvent<N14ExpeditionExitComponent, ActivateInWorldEvent>(OnExitActivated);
        SubscribeLocalEvent<N14ExpeditionExitComponent, StepTriggeredOnEvent>(OnExitSteppedOn);
        SubscribeLocalEvent<N14ExpeditionExitComponent, StepTriggeredOffEvent>(OnExitSteppedOff);
        SubscribeLocalEvent<N14ExpeditionExitComponent, StepTriggerAttemptEvent>(OnExitStepAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // --- Check pending launch countdowns on boards ---
        var boardQuery = EntityQueryEnumerator<N14ExpeditionBoardComponent, TransformComponent>();
        while (boardQuery.MoveNext(out var uid, out var board, out var xform))
        {
            if (board.PendingLaunchTime == null)
                continue;

            if (now >= board.PendingLaunchTime.Value)
            {
                ExecuteLaunch(uid, board, xform);
                board.PendingLaunchTime = null;
                board.PendingDifficulty = null;
                Dirty(uid, board);
                UpdateBoardUi(uid, board);
            }
        }

        // --- Check active expedition session timers (multi-session) ---
        // #Misfits Tweak - Gate to 2 Hz; warnings fire at ≥30-second intervals so 0.5s granularity
        // is imperceptible. Avoids scanning all expedition maps every tick.
        _sessionScanAccumulator += frameTime;
        if (_sessionScanAccumulator >= SessionScanInterval)
        {
            _sessionScanAccumulator = 0f;
            var expQuery = EntityQueryEnumerator<N14ExpeditionComponent>();
        while (expQuery.MoveNext(out var mapUid, out var expedition))
        {
            // Check each session's timer independently
            for (var i = expedition.Sessions.Count - 1; i >= 0; i--)
            {
                var session = expedition.Sessions[i];
                if (session.Finished)
                    continue;

                var remaining = session.EndTime - now;

                // Chat-based time announcements at every 10-minute interval.
                var remainingMinutes = (int) remaining.TotalMinutes;
                if (remainingMinutes > 0
                    && remainingMinutes % 10 == 0
                    && session.LastChatWarningMinutes != remainingMinutes)
                {
                    session.LastChatWarningMinutes = remainingMinutes;
                    ChatAnnounceToSession(mapUid, session,
                        Loc.GetString("n14-expedition-chat-time",
                            ("minutes", remainingMinutes)));
                }

                // 5-minute warning (popup + chat)
                if (!session.Warned5Min && remaining <= TimeSpan.FromMinutes(5))
                {
                    session.Warned5Min = true;
                    var msg = Loc.GetString("n14-expedition-warning-5min");
                    AnnounceToSession(mapUid, session, msg);
                    ChatAnnounceToSession(mapUid, session, msg);
                }

                // 1-minute warning (popup + chat)
                if (!session.Warned1Min && remaining <= TimeSpan.FromMinutes(1))
                {
                    session.Warned1Min = true;
                    var msg = Loc.GetString("n14-expedition-warning-1min");
                    AnnounceToSession(mapUid, session, msg);
                    ChatAnnounceToSession(mapUid, session, msg);
                }

                // 30-second final warning (popup + chat)
                if (!session.Warned30Sec && remaining <= TimeSpan.FromSeconds(30))
                {
                    session.Warned30Sec = true;
                    var msg = Loc.GetString("n14-expedition-warning-30sec");
                    AnnounceToSession(mapUid, session, msg);
                    ChatAnnounceToSession(mapUid, session, msg);
                }

                // Time's up for this session — force extract only this group
                if (remaining <= TimeSpan.Zero)
                {
                    EndSession(mapUid, expedition, session);
                }
            }

            // If all sessions are finished, check if any players remain on the map.
            // If not, delete the map (it's orphaned).
            if (expedition.Sessions.All(s => s.Finished))
            {
                var mapXform = Transform(mapUid);
                var hasPlayers = false;

                // #Misfits Fix - scoped player check instead of full-server mob scan
                // Previously used EntityQueryEnumerator<MobStateComponent> which scanned
                // every mob on the entire server each frame — O(all mobs) lag source.
                // Now checks only active player sessions (typically <50 entities).
                foreach (var playerSession in _playerManager.Sessions)
                {
                    if (playerSession.AttachedEntity is not { } playerEnt)
                        continue;
                    if (Transform(playerEnt).MapID == mapXform.MapID)
                    {
                        hasPlayers = true;
                        break;
                    }
                }

                if (!hasPlayers)
                {
                    QueueDel(mapUid);
                }
            }
        }
        } // end if (_sessionScanAccumulator >= SessionScanInterval)

        // --- Process exit-zone extraction countdowns (multi-session aware) ---
        var exitQuery = EntityQueryEnumerator<N14ExpeditionExitComponent>();
        while (exitQuery.MoveNext(out var exitUid, out var exit))
        {
            if (exit.PendingExtractions.Count == 0)
                continue;

            if (!TryComp<N14ExpeditionComponent>(exit.ExpeditionMap, out var exp))
            {
                exit.PendingExtractions.Clear();
                continue;
            }

            // Check each pending extraction — extract if countdown elapsed
            var toExtract = new List<EntityUid>();
            foreach (var (entUid, extractTime) in exit.PendingExtractions)
            {
                if (now >= extractTime)
                    toExtract.Add(entUid);
            }

            // When any countdown fires, extract that player
            if (toExtract.Count > 0)
            {
                foreach (var entToExtract in toExtract)
                {
                    // Find which session this entity belongs to
                    N14ExpeditionSession? owningSession = null;
                    foreach (var session in exp.Sessions)
                    {
                        if (session.Players.Contains(entToExtract))
                        {
                            owningSession = session;
                            break;
                        }
                    }

                    if (owningSession == null)
                    {
                        // Not in any session — shouldn't happen, but handle gracefully
                        exit.PendingExtractions.Remove(entToExtract);
                        continue;
                    }

                    // Teleport the extracting entity back to its session's return point
                    if (Exists(entToExtract) && !Deleted(entToExtract))
                    {
                        TeleportEntitySafely(entToExtract, owningSession.ReturnPoint);

                        if (_playerManager.TryGetSessionByEntity(entToExtract, out var retSess))
                        {
                            var returnMsg = Loc.GetString("n14-expedition-returned");
                            _popup.PopupEntity(returnMsg, entToExtract, entToExtract, PopupType.Medium);
                            _chatManager.DispatchServerMessage(retSess, returnMsg);
                        }

                        owningSession.Players.Remove(entToExtract);
                    }

                    exit.PendingExtractions.Remove(entToExtract);
                }
            }
        }
    }

    #region BUI Handlers

    /// <summary>
    /// Sends the current board state when a player opens the expedition UI.
    /// </summary>
    private void OnBoardOpened(EntityUid uid, N14ExpeditionBoardComponent board, AfterActivatableUIOpenEvent args)
    {
        UpdateBoardUi(uid, board);
    }

    /// <summary>
    /// Handles a player clicking "Launch Expedition" in the GUI.
    /// </summary>
    private void OnLaunchMessage(EntityUid uid, N14ExpeditionBoardComponent board, N14ExpeditionLaunchMessage args)
    {
        StartLaunchCountdown(uid, board, args.DifficultyId);
    }

    #endregion

    #region Launch

    /// <summary>
    /// Starts a launch countdown. After the countdown, all mobs in range get teleported.
    /// </summary>
    private void StartLaunchCountdown(EntityUid boardUid, N14ExpeditionBoardComponent board, string difficultyId)
    {
        // Block if busy or cooling down
        if (board.ActiveExpedition != null || board.PendingLaunchTime != null)
            return;

        if (board.CooldownEnd != null && _timing.CurTime < board.CooldownEnd.Value)
            return;

        if (!_proto.TryIndex<N14ExpeditionDifficultyPrototype>(difficultyId, out var diff))
            return;

        if (diff.Maps.Count == 0)
            return;

        board.PendingDifficulty = difficultyId;
        board.PendingLaunchTime = _timing.CurTime + TimeSpan.FromSeconds(board.LaunchCountdownSeconds);
        board.CooldownEnd = null;
        Dirty(boardUid, board);

        // Popup + chat countdown notice to everyone near the board
        var countdownMsg = Loc.GetString("n14-expedition-countdown-start",
            ("seconds", (int) board.LaunchCountdownSeconds),
            ("tier", Loc.GetString(diff.Name)));
        AnnounceNearby(boardUid, board.GatherRadius, countdownMsg);
        ChatAnnounceNearby(boardUid, board.GatherRadius, countdownMsg);

        UpdateBoardUi(boardUid, board);
    }

    /// <summary>
    /// Fires when the countdown hits zero — loads a map (or reuses one) and teleports nearby mobs.
    /// If another board already rolled the same map path, adds a new session to the existing
    /// expedition instead of loading a duplicate.
    /// </summary>
    private void ExecuteLaunch(EntityUid boardUid, N14ExpeditionBoardComponent board, TransformComponent boardXform)
    {
        if (board.PendingDifficulty == null)
            return;

        if (!_proto.TryIndex<N14ExpeditionDifficultyPrototype>(board.PendingDifficulty, out var diff))
            return;

        if (diff.Maps.Count == 0)
            return;

        // Pick a random map entry from the difficulty pool
        var mapEntry = _random.Pick(diff.Maps);
        // #Misfits Add - procedural entries also need a per-launch seed
        var runtimeSeed = (mapEntry.RuntimeDungeon || mapEntry.RuntimeProcedural)
            ? GetRuntimeExpeditionSeed(boardUid, board.PendingDifficulty)
            : 0;
        var mapPath = mapEntry.RuntimeDungeon
            ? $"runtime:{board.PendingDifficulty}:{mapEntry.DungeonConfig}:{runtimeSeed}"
            : mapEntry.RuntimeProcedural
            ? $"procedural:{board.PendingDifficulty}:{mapEntry.ProceduralTheme}:{runtimeSeed}"
            : mapEntry.Path?.ToString() ?? string.Empty;

        // Check if an existing expedition is already using this map path.
        // If so, add a new session to it instead of loading a duplicate.
        EntityUid existingMapUid = EntityUid.Invalid;
        N14ExpeditionComponent? existingExpedition = null;

        var expQuery = EntityQueryEnumerator<N14ExpeditionComponent>();
        while (expQuery.MoveNext(out var uid, out var exp))
        {
            if (exp.MapPath == mapPath && !exp.Sessions.All(s => s.Finished))
            {
                // Found an active expedition using this map — reuse it
                existingMapUid = uid;
                existingExpedition = exp;
                break;
            }
        }

        EntityUid mapUid;
        EntityUid gridUid;

        // Either reuse the existing map or load a new one
        if (existingExpedition != null)
        {
            mapUid = existingMapUid;
            gridUid = existingExpedition.GridUid;
        }
        else
        {
            if (mapEntry.RuntimeDungeon)
            {
                if (mapEntry.DungeonConfig == null
                    || !_proto.TryIndex<DungeonConfigPrototype>(mapEntry.DungeonConfig.Value, out var dungeonConfig))
                {
                    AnnounceNearby(boardUid, board.GatherRadius,
                        Loc.GetString("n14-expedition-launch-failed"));
                    return;
                }

                // Runtime vault generation path: create a fresh map and run DungeonSystem with a per-launch seed.
                var mapId = _mapManager.CreateMap();
                mapUid = _mapManager.GetMapEntityId(mapId);
                var gridEnt = _mapManager.CreateGrid(mapId);
                gridUid = gridEnt.Owner;

                try
                {
                    _dungeon.GenerateDungeonAsync(dungeonConfig, gridUid, gridEnt, Vector2i.Zero, runtimeSeed)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to generate runtime expedition dungeon '{mapEntry.DungeonConfig}' with seed {runtimeSeed}: {e}");
                    if (Exists(mapUid) && !Deleted(mapUid))
                        QueueDel(mapUid);

                    AnnounceNearby(boardUid, board.GatherRadius,
                        Loc.GetString("n14-expedition-launch-failed"));
                    return;
                }

                _gravity.EnableGravity(gridUid);
            }
            // #Misfits Add - runtime procedural underground map generation path
            else if (mapEntry.RuntimeProcedural && mapEntry.ProceduralTheme.HasValue)
            {
                // Create a fresh map and grid, then run the procedural generator
                var mapId  = _mapManager.CreateMap();
                mapUid     = _mapManager.GetMapEntityId(mapId);
                var gridEnt = _mapManager.CreateGrid(mapId); // returns MapGridComponent
                gridUid    = gridEnt.Owner;

                // Build generation parameters from the map entry data fields
                var genParams = new UndergroundGenParams
                {
                    Seed               = runtimeSeed,
                    Theme              = mapEntry.ProceduralTheme.Value,
                    GridWidth          = mapEntry.ProceduralGridSize,
                    GridHeight         = mapEntry.ProceduralGridSize,
                    DifficultyTier     = mapEntry.ProceduralDifficultyTier,
                    MinRooms           = mapEntry.ProceduralMinRooms,
                    MaxRooms           = mapEntry.ProceduralMaxRooms,
                    HubCount           = mapEntry.ProceduralHubCount,
                    FactionSpawnGroups = mapEntry.FactionSpawns ?? new System.Collections.Generic.List<N14FactionSpawnGroup>(),
                };

                try
                {
                    _proceduralGen.GenerateMap(genParams, gridUid, gridEnt);
                }
                catch (Exception e)
                {
                    Log.Error($"Procedural expedition generation failed for theme '{mapEntry.ProceduralTheme}' seed {runtimeSeed}: {e}");
                    if (Exists(mapUid) && !Deleted(mapUid))
                        QueueDel(mapUid);

                    AnnounceNearby(boardUid, board.GatherRadius,
                        Loc.GetString("n14-expedition-launch-failed"));
                    return;
                }

                _gravity.EnableGravity(gridUid);
            }
            else if (mapEntry.Path != null
                     && _mapLoader.TryLoadGrid(mapEntry.Path.Value, out var gridMapResult, out var gridResult))
            {
                mapUid = gridMapResult.Value.Owner;
                gridUid = gridResult.Value.Owner;
            }
            else if (mapEntry.Path != null
                     && _mapLoader.TryLoadMap(mapEntry.Path.Value, out var mapResult, out var grids)
                     && grids is { Count: > 0 })
            {
                mapUid = mapResult.Value.Owner;
                gridUid = grids.First().Owner;
            }
            else
            {
                AnnounceNearby(boardUid, board.GatherRadius,
                    Loc.GetString("n14-expedition-launch-failed"));
                return;
            }

            // Initialize fresh-loaded map.
            // #Misfits Fix - CreateMap() already initializes the map; only YAML-loaded maps need this call.
            // Calling InitializeMap on an already-initialized map throws ArgumentException.
            if (!mapEntry.RuntimeDungeon && !mapEntry.RuntimeProcedural)
                _mapSystem.InitializeMap(mapUid);

            _gravity.EnableGravity(gridUid);
        }

        // Gather all mobs near the board
        var nearby = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(boardXform.Coordinates, board.GatherRadius, nearby);

        // Determine spawn — majority-faction vote or grid origin
        // #Misfits Add - procedural maps spawn everyone at grid centre (central congregation room)
        var spawnCoords = (mapEntry.RuntimeProcedural && mapEntry.ProceduralGridSize > 0)
            ? new EntityCoordinates(gridUid,
                new System.Numerics.Vector2(mapEntry.ProceduralGridSize / 2f, mapEntry.ProceduralGridSize / 2f))
            : ResolveFactionSpawn(mapEntry, nearby, gridUid);

        // Mark this new session
        var expedition = EnsureComp<N14ExpeditionComponent>(mapUid);
        expedition.MapPath = mapPath;
        expedition.GridUid = gridUid;

        // Create new session for this board's group
        var session = new N14ExpeditionSession
        {
            SourceBoard = boardUid,
            ReturnPoint = boardXform.Coordinates,
            EndTime = _timing.CurTime + TimeSpan.FromSeconds(diff.Duration),
            DifficultyId = board.PendingDifficulty,
        };

        // Track which entities belong to this session
        foreach (var ent in nearby)
        {
            session.Players.Add(ent);
        }

        expedition.Sessions.Add(session);

        // Spawn exit points only once per map (not per session)
        if (existingExpedition == null)
        {
            SpawnExitPoints(mapEntry, gridUid, mapUid);
        }

        // Link board to this map
        board.ActiveExpedition = mapUid;
        Dirty(boardUid, board);

        // Teleport all gathered mobs to the expedition
        foreach (var ent in nearby)
        {
            TeleportEntitySafely(ent, spawnCoords);
        }

        // Notify teleported players (popup + chat)
        var launchMsg = Loc.GetString("n14-expedition-launched",
            ("tier", Loc.GetString(diff.Name)));
        AnnounceToSession(mapUid, session, launchMsg);
        ChatAnnounceToSession(mapUid, session, launchMsg);
    }

    private int GetRuntimeExpeditionSeed(EntityUid boardUid, string difficultyId)
    {
        unchecked
        {
            // Stable per-launch within a session while still changing between launches.
            return HashCode.Combine(
                ++_expeditionSeedCounter,
                boardUid.GetHashCode(),
                difficultyId.GetHashCode(),
                _timing.CurTime.GetHashCode());
        }
    }

    #endregion

    #region Expedition End

    /// <summary>
    /// Ends a single session on the expedition map. Only extracts that session's players
    /// back to their return point. Resets the source board. Map is deleted only when
    /// ALL sessions are finished AND no players remain.
    /// </summary>
    private void EndSession(EntityUid mapUid, N14ExpeditionComponent expedition, N14ExpeditionSession session)
    {
        if (session.Finished)
            return;

        session.Finished = true;

        // Announce extraction (popup + chat) to only this session's players
        var extractMsg = Loc.GetString("n14-expedition-extracting");
        AnnounceToSession(mapUid, session, extractMsg);
        ChatAnnounceToSession(mapUid, session, extractMsg);

        // Teleport only this session's players back to their return point
        ReturnSessionPlayers(mapUid, session);

        // Reset the source board so it can be used again
        if (TryComp<N14ExpeditionBoardComponent>(session.SourceBoard, out var board))
        {
            board.ActiveExpedition = null;
            board.CooldownEnd = _timing.CurTime + TimeSpan.FromSeconds(board.CooldownSeconds);
            Dirty(session.SourceBoard, board);
            UpdateBoardUi(session.SourceBoard, board);
        }
    }

    /// <summary>
    /// Teleports only the players in this session back to the return point.
    /// Also extracts nearby items/crates that don't belong to other sessions.
    /// </summary>
    private void ReturnSessionPlayers(EntityUid mapUid, N14ExpeditionSession session)
    {
        // Validate the return point still exists
        if (!Exists(session.ReturnPoint.EntityId) || Deleted(session.ReturnPoint.EntityId))
        {
            Log.Warning($"Session return point entity {session.ReturnPoint.EntityId} is gone — cannot extract.");
            return;
        }

        // Teleport each registered player back
        var playersToReturn = session.Players.ToList();
        foreach (var uid in playersToReturn)
        {
            if (!Exists(uid) || Deleted(uid))
                continue;

            TeleportEntitySafely(uid, session.ReturnPoint);
        }

        session.Players.Clear();
    }

    /// <summary>
    /// Safely teleports an entity to the destination coordinates.
    /// SetCoordinates handles both position and parent re-parenting correctly,
    /// and for player-controlled entities we should NOT call AttachToGridOrMap
    /// as it can detach players from their bodies.
    /// </summary>
    private void TeleportEntitySafely(EntityUid uid, EntityCoordinates destination)
    {
        _xform.SetCoordinates(uid, destination);
    }

    #endregion

    #region Exit Point (Early Return)

    /// <summary>
    /// Spawns random green-flare return markers across the expedition map.
    /// We sample 5-8 points from faction edge spawns so players can exit from
    /// multiple locations instead of walking back to a single entry location.
    /// </summary>
    private void SpawnExitPoints(N14ExpeditionMapEntry mapEntry, EntityUid gridUid, EntityUid mapUid)
    {
        // Keep all exits bound to this expedition map so each player returns to their own board session.
        void SpawnExitAt(Vector2 position)
        {
            var coords = new EntityCoordinates(gridUid, position);
            var exitUid = Spawn("N14ExpeditionExitPoint", coords);
            var exitComp = EnsureComp<N14ExpeditionExitComponent>(exitUid);
            exitComp.ExpeditionMap = mapUid;
        }

        if (mapEntry.FactionSpawns is { Count: > 0 })
        {
            var candidates = mapEntry.FactionSpawns.Select(s => s.Position).ToList();

            var minExits = Math.Min(5, candidates.Count);
            var maxExits = Math.Min(8, candidates.Count);
            var targetExits = maxExits > minExits
                ? _random.Next(minExits, maxExits + 1)
                : maxExits;

            for (var i = 0; i < targetExits && candidates.Count > 0; i++)
            {
                var idx = _random.Next(candidates.Count);
                var pos = candidates[idx];
                candidates.RemoveAt(idx);
                SpawnExitAt(pos);
            }

            return;
        }

        // Fallback for maps without faction spawn metadata: random subset of edge-ish offsets around origin.
        var fallback = new List<Vector2>
        {
            new(-25f, -25f),
            new(-25f, 0f),
            new(-25f, 25f),
            new(0f, -25f),
            new(0f, 25f),
            new(25f, -25f),
            new(25f, 0f),
            new(25f, 25f),
        };

        var fallbackCount = _random.Next(5, 9);
        for (var i = 0; i < fallbackCount && fallback.Count > 0; i++)
        {
            var idx = _random.Next(fallback.Count);
            var pos = fallback[idx];
            fallback.RemoveAt(idx);
            SpawnExitAt(pos);
        }
    }

    /// <summary>
    /// When a player activates (presses E on) the exit point, start the extraction countdown.
    /// </summary>
    private void OnExitActivated(EntityUid uid, N14ExpeditionExitComponent exit, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<N14ExpeditionComponent>(exit.ExpeditionMap, out var expedition))
            return;

        if (expedition.Sessions.All(s => s.Finished))
            return;

        StartExitCountdown(uid, exit, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Allow any entity to trigger the exit by stepping on it (no speed requirement).
    /// </summary>
    private static void OnExitStepAttempt(EntityUid uid, N14ExpeditionExitComponent comp, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    /// <summary>
    /// When a mob walks onto the green flare, start the extraction countdown.
    /// </summary>
    private void OnExitSteppedOn(EntityUid uid, N14ExpeditionExitComponent exit, ref StepTriggeredOnEvent args)
    {
        if (!TryComp<N14ExpeditionComponent>(exit.ExpeditionMap, out var expedition))
            return;

        if (expedition.Sessions.All(s => s.Finished))
            return;

        // Only affect mobs (players/NPCs), not thrown items
        if (!HasComp<MobStateComponent>(args.Tripper))
            return;

        StartExitCountdown(uid, exit, args.Tripper);
    }

    /// <summary>
    /// When a mob steps off the exit flare, cancel their extraction countdown.
    /// </summary>
    private void OnExitSteppedOff(EntityUid uid, N14ExpeditionExitComponent exit, ref StepTriggeredOffEvent args)
    {
        if (!exit.PendingExtractions.Remove(args.Tripper))
            return;

        var cancelMsg = Loc.GetString("n14-expedition-exit-cancelled");
        _popup.PopupEntity(cancelMsg, args.Tripper, args.Tripper, PopupType.Medium);

        // Chat confirmation of cancellation
        if (_playerManager.TryGetSessionByEntity(args.Tripper, out var cancelSess))
            _chatManager.DispatchServerMessage(cancelSess, cancelMsg);
    }

    /// <summary>
    /// Starts (or refreshes) an extraction countdown for the given entity on this exit zone.
    /// </summary>
    private void StartExitCountdown(EntityUid exitUid, N14ExpeditionExitComponent exit, EntityUid who)
    {
        // Already counting down on this exit — don't restart
        if (exit.PendingExtractions.ContainsKey(who))
            return;

        exit.PendingExtractions[who] = _timing.CurTime + TimeSpan.FromSeconds(exit.CountdownSeconds);

        var countdownMsg = Loc.GetString("n14-expedition-exit-countdown",
            ("seconds", (int) exit.CountdownSeconds));
        _popup.PopupEntity(countdownMsg, who, who, PopupType.LargeCaution);

        // Also show in chat so the player has a persistent log entry
        if (_playerManager.TryGetSessionByEntity(who, out var sess))
            _chatManager.DispatchServerMessage(sess, countdownMsg);
    }

    #endregion

    #region Announcements

    /// <summary>
    /// Shows a large popup to every player in the specific session.
    /// </summary>
    private void AnnounceToSession(EntityUid mapUid, N14ExpeditionSession session, string message)
    {
        foreach (var uid in session.Players)
        {
            if (Exists(uid) && !Deleted(uid) && HasComp<MobStateComponent>(uid))
            {
                _popup.PopupEntity(message, uid, uid, PopupType.LargeCaution);
            }
        }
    }

    /// <summary>
    /// Shows a large popup to every mob near a specific entity.
    /// </summary>
    private void AnnounceNearby(EntityUid originUid, float radius, string message)
    {
        var nearby = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(Transform(originUid).Coordinates, radius, nearby);

        foreach (var ent in nearby)
        {
            _popup.PopupEntity(message, ent, ent, PopupType.LargeCaution);
        }
    }

    /// <summary>
    /// Sends a server chat message to every player in the specific session.
    /// </summary>
    private void ChatAnnounceToSession(EntityUid mapUid, N14ExpeditionSession session, string message)
    {
        foreach (var uid in session.Players)
        {
            if (_playerManager.TryGetSessionByEntity(uid, out var playerSession))
                _chatManager.DispatchServerMessage(playerSession, message);
        }
    }

    /// <summary>
    /// Sends a server message to the chatbox of every player near a specific entity.
    /// Used for events near the board (countdown start, launch failed, etc.)
    /// so they appear in the chatbox, not just as a transient popup.
    /// </summary>
    private void ChatAnnounceNearby(EntityUid originUid, float radius, string message)
    {
        var nearby = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(Transform(originUid).Coordinates, radius, nearby);

        foreach (var ent in nearby)
        {
            if (_playerManager.TryGetSessionByEntity(ent, out var session))
                _chatManager.DispatchServerMessage(session, message);
        }
    }

    #endregion

    #region UI State

    /// <summary>
    /// Pushes the current board state to all open UIs.
    /// </summary>
    public void UpdateBoardUi(EntityUid boardUid, N14ExpeditionBoardComponent board)
    {
        var tiers = new List<N14ExpeditionTierInfo>();

        foreach (var proto in _proto.EnumeratePrototypes<N14ExpeditionDifficultyPrototype>()
                     .OrderBy(p => p.SortOrder))
        {
            tiers.Add(new N14ExpeditionTierInfo(
                proto.ID,
                Loc.GetString(proto.Name),
                proto.Color,
                proto.Maps.Count,
                proto.Duration / 60f));
        }

        TimeSpan? expeditionEnd = null;
        if (board.ActiveExpedition != null
            && TryComp<N14ExpeditionComponent>(board.ActiveExpedition.Value, out var exp))
        {
            // Find the active session from this board (if any)
            var boardSession = exp.Sessions.FirstOrDefault(s => s.SourceBoard == boardUid && !s.Finished);
            if (boardSession != null)
            {
                expeditionEnd = boardSession.EndTime;
            }
        }

        var now = _timing.CurTime;
        var onCooldown = board.CooldownEnd != null && now < board.CooldownEnd.Value;

        var state = new N14ExpeditionBoardState(
            board.ActiveExpedition != null,
            expeditionEnd,
            board.PendingLaunchTime,
            tiers,
            onCooldown,
            onCooldown ? board.CooldownEnd : null);

        _ui.SetUiState(boardUid, N14ExpeditionBoardUiKey.Key, state);
    }

    #endregion

    #region Spawn Point Resolution

    /// <summary>
    /// Resolves spawn coordinates using majority-faction voting when the map
    /// defines faction spawn groups. All players (including minority factions)
    /// teleport to the winning faction's location.
    /// Falls back to grid origin if no faction data is defined for the map.
    /// </summary>
    private EntityCoordinates ResolveFactionSpawn(
        N14ExpeditionMapEntry mapEntry,
        HashSet<Entity<MobStateComponent>> nearby,
        EntityUid gridUid)
    {
        // No faction spawn data — use grid origin
        if (mapEntry.FactionSpawns is not { Count: > 0 })
            return new EntityCoordinates(gridUid, Vector2.Zero);

        // Count how many gathered mobs belong to each faction group.
        // Each mob is counted once, in the first matching group.
        var groupCounts = new int[mapEntry.FactionSpawns.Count];

        foreach (var mob in nearby)
        {
            for (var i = 0; i < mapEntry.FactionSpawns.Count; i++)
            {
                var group = mapEntry.FactionSpawns[i];

                if (_factions.IsMemberOfAny(
                        (mob.Owner, (NpcFactionMemberComponent?) null),
                        group.Factions))
                {
                    groupCounts[i]++;
                    break; // each mob counts toward at most one group
                }
            }
        }

        // The group with the most members wins; first group breaks ties.
        var bestIdx = 0;
        for (var i = 1; i < groupCounts.Length; i++)
        {
            if (groupCounts[i] > groupCounts[bestIdx])
                bestIdx = i;
        }

        return new EntityCoordinates(gridUid, mapEntry.FactionSpawns[bestIdx].Position);
    }

    #endregion
}
