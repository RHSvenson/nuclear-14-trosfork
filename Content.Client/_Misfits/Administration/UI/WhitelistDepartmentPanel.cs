// #Misfits Change - Department panel for enriched whitelist administration rows
using System.Linq;
using Content.Shared._Misfits.Administration;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.Administration.UI;

public sealed class WhitelistDepartmentPanel : PanelContainer
{
    public Action<ProtoId<JobPrototype>, bool>? OnSetJob;
    public Action<ProtoId<JobPrototype>, string>? OnAddRoleTime;
    public Action<ProtoId<JobPrototype>, int>? OnAdjustJobSlots;

    public WhitelistDepartmentPanel(
        DepartmentPrototype department,
        IPrototypeManager proto,
        HashSet<ProtoId<JobPrototype>> whitelists,
        IReadOnlyDictionary<ProtoId<JobPrototype>, WhitelistJobAdminInfo> adminInfo,
        bool canManagePlaytime)
    {
        Margin = new Thickness(0, 0, 0, 6);
        StyleClasses.Add("BackgroundDark");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };

        var allWhitelisted = department.Roles.All(whitelists.Contains);
        var header = new Button
        {
            Text = Loc.GetString(department.ID),
            ToggleMode = true,
            Pressed = allWhitelisted,
            HorizontalExpand = true,
            Modulate = department.Color,
        };

        var jobs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };

        foreach (var (jobProtoId, job) in department.Roles
                     .Select(id => (Id: id, Job: proto.Index<JobPrototype>(id)))
                     .OrderByDescending(entry => entry.Job.RealDisplayWeight)
                     .ThenBy(entry => entry.Job.ID))
        {
            adminInfo.TryGetValue(jobProtoId, out var info);

            var row = new WhitelistJobRow(
                jobProtoId,
                job,
                whitelists.Contains(jobProtoId),
                info?.RoleTime ?? TimeSpan.Zero,
                canManagePlaytime);

            row.OnSetJob += (id, enabled) => OnSetJob?.Invoke(id, enabled);
            row.OnAddRoleTime += (id, timeString) => OnAddRoleTime?.Invoke(id, timeString);
            row.OnAdjustJobSlots += (id, delta) => OnAdjustJobSlots?.Invoke(id, delta);
            jobs.AddChild(row);
        }

        header.OnPressed += _ =>
        {
            foreach (var id in department.Roles)
            {
                if (whitelists.Contains(id) == header.Pressed)
                    continue;

                OnSetJob?.Invoke(id, header.Pressed);
            }
        };

        root.AddChild(header);
        root.AddChild(jobs);
        AddChild(root);
    }
}