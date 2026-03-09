// #Misfits Change - Client-side EUI for the Job Slots admin panel
using Content.Client.Eui;
using Content.Shared._Misfits.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Client._Misfits.Administration.UI;

public sealed class JobSlotsEui : BaseEui
{
    private readonly ISawmill _sawmill;
    private readonly JobSlotsWindow _window;

    public JobSlotsEui()
    {
        _sawmill = Logger.GetSawmill("admin.job_slots_eui");
        _window = new JobSlotsWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnAdjustJobSlots += (job, delta) => SendMessage(new AdjustJobSlotsMessage(job, delta));
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not JobSlotsEuiState cast)
        {
            _sawmill.Warning($"Unexpected state type: {state.GetType().FullName}");
            return;
        }

        _window.HandleState(cast);
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
        _window.Dispose();
    }
}
