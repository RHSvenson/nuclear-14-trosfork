using Content.Shared._Misfits.Expeditions;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.Expeditions;

/// <summary>
/// Client BUI handler for the expedition board.
/// Opens the window and relays launch messages to the server.
/// </summary>
[UsedImplicitly]
public sealed class N14ExpeditionBoardBoundUserInterface : BoundUserInterface
{
    private N14ExpeditionBoardWindow? _window;

    public N14ExpeditionBoardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<N14ExpeditionBoardWindow>();
        _window.OnLaunchExpedition += difficultyId =>
            SendMessage(new N14ExpeditionLaunchMessage(difficultyId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is N14ExpeditionBoardState boardState)
            _window?.UpdateState(boardState);
    }
}
