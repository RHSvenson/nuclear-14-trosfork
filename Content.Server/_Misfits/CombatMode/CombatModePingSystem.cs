// Misfits Change - Plays a short local-area ping when a player or NPC activates combat mode.
// Anti-spam cooldown prevents rapidly toggling the sound by flicking combat mode on and off.
// Cleans up stale entries for deleted entities to prevent memory leaks over long rounds.
using Content.Server.NPC.HTN;
using Content.Shared.CombatMode;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.CombatMode;

/// <summary>
/// Raised on an entity (server-only) when its combat mode transitions from OFF to ON.
/// </summary>
public sealed class CombatModeActivatedEvent : EntityEventArgs { }

/// <summary>
/// Plays a positional ping sound (audible within voice range, ~10 tiles) whenever
/// an entity activates combat mode. For NPCs the cooldown is longer to avoid audio spam
/// flooding the network send buffer during large engagements.
/// <para>
/// A per-entity cooldown blocks the sound from replaying if combat mode is toggled rapidly.
/// Stale dictionary entries are pruned periodically to prevent memory leaks on long rounds.
/// </para>
/// </summary>
public sealed class CombatModePingSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Seconds a player must wait before the ping can play again after being triggered.
    private const float PingCooldownSeconds = 3f;

    // #Misfits Tweak - NPCs get a longer cooldown to avoid flooding the network with audio
    // packets when dozens of mobs aggro simultaneously.
    private const float NpcPingCooldownSeconds = 10f;

    // Voice range in world units — matches SharedChatSystem.VoiceRange (10).
    private const float PingMaxDistance = 10f;

    // The sound that plays on combat mode activation.
    private const string PingSound = "/Audio/Effects/toggleoncombat.ogg";

    // Per-entity timestamp of when the ping last played, used to enforce the cooldown.
    private readonly Dictionary<EntityUid, TimeSpan> _lastPingTime = new();

    // #Misfits Fix - Accumulator for periodic cleanup of stale entries (dead/deleted entities).
    private float _cleanupAccumulator;
    private const float CleanupIntervalSeconds = 60f;

    public override void Initialize()
    {
        base.Initialize();

        // Misfits Fix - Subscribe to our custom event (raised by CombatModeSystem.SetInCombatMode)
        // instead of (CombatModeComponent, ToggleCombatActionEvent), which is exclusively owned
        // by SharedCombatModeSystem and cannot have a second subscriber.
        SubscribeLocalEvent<CombatModeComponent, CombatModeActivatedEvent>(OnCombatModeActivated);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _lastPingTime.Clear();
    }

    // #Misfits Fix - Periodically prune entries for entities that no longer exist.
    // Prevents the dictionary from growing unbounded during 3+ hour rounds with mob churn.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _cleanupAccumulator += frameTime;
        if (_cleanupAccumulator < CleanupIntervalSeconds)
            return;

        _cleanupAccumulator -= CleanupIntervalSeconds;

        // Remove entries for entities that have been deleted.
        var toRemove = new List<EntityUid>();
        foreach (var uid in _lastPingTime.Keys)
        {
            if (!Exists(uid))
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            _lastPingTime.Remove(uid);
    }

    private void OnCombatModeActivated(EntityUid uid, CombatModeComponent comp, CombatModeActivatedEvent args)
    {
        var isNpc = HasComp<HTNComponent>(uid);

        // Use a longer cooldown for NPCs to avoid saturating the network send buffer.
        var cooldown = isNpc ? NpcPingCooldownSeconds : PingCooldownSeconds;

        // Enforce anti-spam cooldown.
        var now = _timing.CurTime;
        if (_lastPingTime.TryGetValue(uid, out var lastPing) &&
            (now - lastPing).TotalSeconds < cooldown)
            return;

        _lastPingTime[uid] = now;

        // Play positional ping audible within local/voice range (~10 tiles).
        _audio.PlayPvs(
            new SoundPathSpecifier(PingSound),
            uid,
            AudioParams.Default.WithMaxDistance(PingMaxDistance));
    }
}
