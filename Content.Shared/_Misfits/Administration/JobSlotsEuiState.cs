// #Misfits Change - Shared state and messages for the Job Slots EUI
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration;

/// <summary>
/// State sent from server to client for the Job Slots EUI.
/// Contains the active station name and per-department slot data.
/// </summary>
[Serializable, NetSerializable]
public sealed class JobSlotsEuiState : EuiStateBase
{
    /// <summary>
    /// The station whose job slots are being managed, or null if no station is available.
    /// </summary>
    public string? StationName;

    /// <summary>
    /// Whether the opening admin has the Admin flag (required to adjust slots).
    /// </summary>
    public bool CanManageSlots;

    /// <summary>
    /// Per-department list of jobs and their current slot counts.
    /// Already sorted by department weight then department ID, and within each
    /// department by job display weight then job ID.
    /// </summary>
    public List<JobSlotDepartmentInfo> Departments;

    public JobSlotsEuiState(
        string? stationName,
        bool canManageSlots,
        List<JobSlotDepartmentInfo> departments)
    {
        StationName = stationName;
        CanManageSlots = canManageSlots;
        Departments = departments;
    }
}

/// <summary>
/// Slot data for one department, as part of <see cref="JobSlotsEuiState"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class JobSlotDepartmentInfo
{
    public string DepartmentId;
    public List<JobSlotInfo> Jobs;

    public JobSlotDepartmentInfo(string departmentId, List<JobSlotInfo> jobs)
    {
        DepartmentId = departmentId;
        Jobs = jobs;
    }
}

/// <summary>
/// Slot data for a single job inside a department.
/// </summary>
[Serializable, NetSerializable]
public sealed class JobSlotInfo
{
    public ProtoId<JobPrototype> Job;

    /// <summary>
    /// Current slot count, or null if the job is configured as unlimited.
    /// </summary>
    public int? Slots;

    /// <summary>
    /// Whether this job has an entry in the station's slot configuration at all.
    /// If false, the job is known to the department prototype but the station
    /// has no slot row for it, so +/- cannot be used.
    /// </summary>
    public bool HasSlotConfiguration;

    public JobSlotInfo(ProtoId<JobPrototype> job, int? slots, bool hasSlotConfiguration)
    {
        Job = job;
        Slots = slots;
        HasSlotConfiguration = hasSlotConfiguration;
    }
}

/// <summary>
/// Message from client to server to adjust a job's slot count on the active station.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdjustJobSlotsMessage : EuiMessageBase
{
    public ProtoId<JobPrototype> Job;
    public int Delta;

    public AdjustJobSlotsMessage(ProtoId<JobPrototype> job, int delta)
    {
        Job = job;
        Delta = delta;
    }
}
