using Content.Shared._Misfits.ChalkboardOrders;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.ChalkboardOrders;

[UsedImplicitly]
public sealed class ChalkboardOrdersBoundUserInterface : BoundUserInterface
{
    private ChalkboardOrdersWindow? _window;

    public ChalkboardOrdersBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChalkboardOrdersWindow>();
        _window.OnAddAnnotation += annotation => SendMessage(new ChalkboardOrdersAddAnnotationMessage(annotation));
        _window.OnRemoveAnnotation += index => SendMessage(new ChalkboardOrdersRemoveAnnotationMessage(index));
        _window.OnClearAnnotations += () => SendMessage(new ChalkboardOrdersClearAnnotationsMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ChalkboardOrdersBoundUserInterfaceState boardState)
            return;

        _window?.SetBoard(boardState.BoardTitle, boardState.BoardTexturePath, boardState.SharedAnnotations);
    }
}
