// #Misfits Add - Client-side UIController that bridges the Roster button (in ChatBox)
// to the network message. UIControllers are EntitySystem-based and get proper DI,
// which is why the network send lives here rather than directly in the UI widget.
using Content.Shared._Misfits.Roster;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.GameObjects;

namespace Content.Client._Misfits.Roster;

/// <summary>
/// Handles client-side logic for the Crew Roster feature.
/// Provides <see cref="RequestRoster"/> which sends the server request to open
/// the crew manifest EUI for the local player.
/// </summary>
public sealed class RosterUIController : UIController
{
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    /// <summary>
    /// Sends a request to the server to open the crew manifest window for the local player.
    /// Works from both the lobby and in-game since no entity UID is needed.
    /// </summary>
    public void RequestRoster()
    {
        _net.SendSystemNetworkMessage(new MisfitsRosterRequestMessage());
    }
}
