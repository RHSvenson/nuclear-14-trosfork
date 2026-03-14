// #Misfits Add - Shared network message allowing any client to request the crew roster
// without needing a physical in-game entity. Server picks the station automatically.
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Roster;

/// <summary>
/// Sent by the client (from the chat panel Roster button) to ask the server
/// to open the crew manifest/roster EUI for the requesting player.
/// Works from lobby and in-game since no entity UID is required.
/// </summary>
[Serializable, NetSerializable]
public sealed class MisfitsRosterRequestMessage : EntityEventArgs
{
}
