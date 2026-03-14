// #Misfits Add - Server-side handler for the Roster button request.
// Receives the client's MisfitsRosterRequestMessage, finds the active station,
// and opens the existing CrewManifestEui for the requesting player.
// This reuses all vanilla crew manifest infrastructure (department grouping, icons, etc.).
using Content.Server.CrewManifest;
using Content.Server.Station.Components;
using Content.Shared._Misfits.Roster;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Roster;

public sealed class MisfitsRosterSystem : EntitySystem
{
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Listen for the client's button press; no entity required.
        SubscribeNetworkEvent<MisfitsRosterRequestMessage>(OnRosterRequest);
    }

    private void OnRosterRequest(MisfitsRosterRequestMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not ICommonSession session)
            return;

        // Find the first active station. During lobby/pre-round this will be empty
        // and we silently return — the manifest has nothing to show yet.
        var query = AllEntityQuery<StationDataComponent>();
        while (query.MoveNext(out var stationUid, out _))
        {
            // CrewManifestSystem.OpenEui internally validates that the station
            // has a StationRecordsComponent before doing anything.
            _crewManifest.OpenEui(stationUid, session);
            return; // Only open for the first/main station.
        }
    }
}
