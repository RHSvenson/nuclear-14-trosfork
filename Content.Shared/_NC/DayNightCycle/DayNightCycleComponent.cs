// #Misfits Change - Reworked to use IGameTiming-based deterministic cycle (no per-frame dirty spam)
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NC14.DayNightCycle
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class DayNightCycleComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("cycleDuration")]
        public float CycleDurationMinutes { get; set; } = 60f; // Default cycle duration is 60 minutes

        /// <summary>
        /// Offset into the cycle (0–1) applied at startup so the world begins at
        /// "early morning" rather than midnight.
        /// </summary>
        [DataField("startOffset")]
        [AutoNetworkedField]
        public float StartOffset { get; set; } = 0.2f; // Start at 20% (early morning)

        [DataField("timeEntries")]
        public List<TimeEntry> TimeEntries { get; set; } = new()
        {
            new() { Time = 0.00f, ColorHex = "#121224" }, // Midnight       – dark navy, never fully black
            new() { Time = 0.04f, ColorHex = "#18182D" }, // Very early night
            new() { Time = 0.08f, ColorHex = "#4A3420" }, // Early dawn      – first warm hint
            new() { Time = 0.17f, ColorHex = "#7A5C34" }, // Dawn            – amber glow
            new() { Time = 0.25f, ColorHex = "#A87448" }, // Sunrise         – warm orange
            new() { Time = 0.33f, ColorHex = "#D4A85C" }, // Early morning   – golden
            new() { Time = 0.42f, ColorHex = "#E8C070" }, // Mid-morning     – bright gold
            new() { Time = 0.50f, ColorHex = "#F8D880" }, // Noon            – peak brightness, warm white-gold
            new() { Time = 0.58f, ColorHex = "#F0C870" }, // Early afternoon – slightly softer
            new() { Time = 0.67f, ColorHex = "#CCA050" }, // Late afternoon  – deepening gold
            new() { Time = 0.75f, ColorHex = "#B07840" }, // Sunset          – warm orange
            new() { Time = 0.83f, ColorHex = "#7A4A2C" }, // Dusk            – deep amber-red
            new() { Time = 0.92f, ColorHex = "#241B38" }, // Early night     – blue-purple
            new() { Time = 1.00f, ColorHex = "#121224" }  // Back to Midnight
        };
    }

    [DataDefinition, NetSerializable, Serializable]
    public sealed partial class TimeEntry
    {
        [DataField("colorHex")]
        public string ColorHex { get; set; } = "#FFFFFF";

        [DataField("time")]
        public float Time { get; set; } // Normalized time (0-1)
    }
}