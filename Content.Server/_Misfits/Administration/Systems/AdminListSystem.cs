// #Misfits Change - Server-side system for the /admins player command
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Shared.Ghost;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Administration.Systems;

public sealed class AdminListSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAfkManager _afkManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    /// Gathers the list of online admins and mentors and sends it to the requesting player.
    /// Admins are sorted by total permission count (descending).
    /// Mentors are players with MENTORHELP but NOT ADMIN.
    /// Stealth admins are hidden from non-stealth players.
    /// </summary>
    public void SendAdminList(ICommonSession requestor)
    {
        var callerData = _adminManager.GetAdminData(requestor);
        var canSeeStealth = callerData != null && callerData.CanStealth();

        var ev = new AdminListEvent();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status is SessionStatus.Zombie or SessionStatus.Connecting)
                continue;

            if (session.Status == SessionStatus.Disconnected || _afkManager.IsAfk(session))
            {
                ev.AfkPlayers++;
                continue;
            }

            if (session.Status == SessionStatus.InGame && session.AttachedEntity is { Valid: true } attached && HasComp<GhostComponent>(attached))
            {
                ev.GhostPlayers++;
                continue;
            }

            ev.ConnectedPlayers++;
        }

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            var data = _adminManager.GetAdminData(admin);
            if (data == null || !data.Active)
                continue;

            // Hide stealth admins from non-stealth players
            if (data.Stealth && !canSeeStealth)
                continue;

            var permCount = BitOperations.PopCount((uint) data.Flags);

            var entry = new AdminListEvent.AdminEntry
            {
                Name = admin.Name,
                Title = data.Title,
                PermissionCount = permCount,
            };

            // #Misfits Change — Mentor = has ViewNotes but NOT Admin.
            // ViewNotes also gates MHelp routing. MENTORHELP flag is unused.
            if (data.HasFlag(AdminFlags.ViewNotes) && !data.HasFlag(AdminFlags.Admin))
            {
                ev.Mentors.Add(entry);
            }
            else if (data.HasFlag(AdminFlags.Admin))
            {
                ev.Admins.Add(entry);
            }
        }

        // Sort by permission count descending
        ev.Admins.Sort((a, b) => b.PermissionCount.CompareTo(a.PermissionCount));
        ev.Mentors.Sort((a, b) => b.PermissionCount.CompareTo(a.PermissionCount));

        RaiseNetworkEvent(ev, requestor);
    }
}
