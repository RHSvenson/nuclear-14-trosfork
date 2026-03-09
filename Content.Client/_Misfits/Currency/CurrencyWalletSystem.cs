// #Misfits Change - Client-side currency wallet system
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.Currency;

/// <summary>
/// Handles opening the currency wallet window and processing withdrawal requests on the client.
/// </summary>
public sealed class CurrencyWalletSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private CurrencyWalletWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CurrencyWalletStateMessage>(OnCurrencyWalletState);
    }

    private void OnCurrencyWalletState(CurrencyWalletStateMessage msg)
    {
        EnsureWindow();

        _window!.UpdateState(msg.Bottlecaps, msg.NCRDollars, msg.LegionDenarii, msg.PrewarMoney);
        _window.OpenCentered();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = new CurrencyWalletWindow();
        _window.OnWithdrawRequest += OnWithdrawRequest;
    }

    private void OnWithdrawRequest(CurrencyType type, int amount)
    {
        RaiseNetworkEvent(new WithdrawCurrencyRequest
        {
            CurrencyType = type,
            Amount = amount,
        });
    }
}
