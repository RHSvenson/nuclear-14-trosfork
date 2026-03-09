// #Misfits Change - Server-side EUI for the Job Slots admin panel
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Station.Systems;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Administration;

/// <summary>
/// Server-side EUI that exposes per-station job slot counts to admins and lets
/// them increment or decrement individual job slots without needing to select a
/// player first.
/// </summary>
public sealed class JobSlotsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly ISawmill _sawmill;
    private StationJobsSystem StationJobs => _entManager.System<StationJobsSystem>();
    private StationSystem StationSystem => _entManager.System<StationSystem>();

    // Only show Fallout-relevant departments – mirrors WhitelistSearchEui.
    private static readonly HashSet<string> AllowedDepartments = new()
    {
        "BrotherhoodOfSteel",
        "CaesarLegion",
        "NCR",
        "Townsfolk",
        "FEVMutants",
        "Tribe",
        "Robots",
        "Vault",
        "Raider",
    };

    private EntityUid? _station;
    private string? _stationName;

    public JobSlotsEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.job_slots_eui");
    }

    public override void Opened()
    {
        base.Opened();
        RefreshStation();
        StateDirty();
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private void RefreshStation()
    {
        if (Player.AttachedEntity is { Valid: true } attached &&
            StationSystem.GetOwningStation(attached) is { } owning)
        {
            _station = owning;
        }
        else
        {
            var stations = StationSystem.GetStations();
            _station = stations.Count > 0 ? stations[0] : null;
        }

        if (_station is { } uid &&
            _entManager.TryGetComponent<MetaDataComponent>(uid, out var meta))
        {
            _stationName = meta.EntityName;
        }
        else
        {
            _stationName = null;
        }
    }

    public override EuiStateBase GetNewState()
    {
        var canManage = _admin.HasAdminFlag(Player, AdminFlags.Admin);
        var depts = new List<JobSlotDepartmentInfo>();

        foreach (var dept in _proto.EnumeratePrototypes<DepartmentPrototype>()
                     .OrderByDescending(d => d.Weight)
                     .ThenBy(d => d.ID))
        {
            if (!AllowedDepartments.Contains(dept.ID))
                continue;

            // Sort jobs by display weight descending, then ID ascending.
            var sortedRoles = dept.Roles
                .Where(id => _proto.HasIndex<JobPrototype>(id))
                .Select(id => (id, proto: _proto.Index<JobPrototype>(id)))
                .OrderByDescending(t => t.proto.RealDisplayWeight)
                .ThenBy(t => t.proto.ID)
                .ToList();

            if (sortedRoles.Count == 0)
                continue;

            var jobs = new List<JobSlotInfo>();
            foreach (var (jobId, _) in sortedRoles)
            {
                int? slots = 0;
                var hasCfg = false;

                if (_station != null &&
                    StationJobs.TryGetJobSlot(_station.Value, jobId, out var slotData))
                {
                    hasCfg = true;
                    slots = slotData is null ? null : (int) slotData.Value;
                }

                jobs.Add(new JobSlotInfo(jobId, slots, hasCfg));
            }

            depts.Add(new JobSlotDepartmentInfo(dept.ID, jobs));
        }

        return new JobSlotsEuiState(_stationName, canManage, depts);
    }

    // -------------------------------------------------------------------------
    // Message handling
    // -------------------------------------------------------------------------

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Whitelist))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use job slots EUI without Whitelist flag");
            return;
        }

        if (msg is not AdjustJobSlotsMessage adjust)
            return;

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to adjust job slots without Admin flag");
            return;
        }

        HandleAdjust(adjust.Job, adjust.Delta);
    }

    private void HandleAdjust(ProtoId<JobPrototype> job, int delta)
    {
        if (_station == null || !_proto.HasIndex<JobPrototype>(job) || delta == 0)
            return;

        if (!StationJobs.TryAdjustJobSlot(_station.Value, job, delta, createSlot: true, clamp: true))
            return;

        _sawmill.Info($"{Player.Name} ({Player.UserId}) adjusted slots for {job} by {delta:+#;-#} on station {_stationName ?? _station.Value.ToString()}");
        StateDirty();
    }
}
