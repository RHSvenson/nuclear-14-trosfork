// #Misfits Change - Mentor Help UI Controller (like AHelpUIController but for mentor system)
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Administration.Systems;
using Content.Client._Misfits.Administration.Systems;
using Content.Client._Misfits.Administration.UI.MentorHelp;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._Misfits.UserInterface.Systems.MentorHelp;

[UsedImplicitly]
public sealed class MentorHelpUIController : UIController,
    IOnSystemChanged<MentorHelpSystem>,
    IOnStateChanged<GameplayState>,
    IOnStateChanged<LobbyState>
{
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private MentorHelpSystem? _mentorHelpSystem;
    private MenuButton? GameMHelpButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.MHelpButton;
    private Button? LobbyMHelpButton => (UIManager.ActiveScreen as LobbyGui)?.MHelpButton;
    public IMentorHelpUIHandler? UIHelper;

    private bool _hasUnreadMHelp;

    public const string MHelpReceiveSound = "/Audio/Admin/adminhelp_old.ogg";
    public const string MHelpSendSound = "/Audio/Admin/adminhelp_old.ogg";
    public const string MHelpErrorSound = "/Audio/Admin/ahelp_error.ogg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MentorHelpPlayerTypingUpdated>(PeopleTypingUpdated);

        _adminManager.AdminStatusUpdated += OnAdminStatusUpdated;
    }

    public void UnloadButton()
    {
        if (GameMHelpButton != null)
            GameMHelpButton.OnPressed -= MHelpButtonPressed;

        if (LobbyMHelpButton != null)
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
    }

    public void LoadButton()
    {
        if (GameMHelpButton != null)
            GameMHelpButton.OnPressed += MHelpButtonPressed;

        if (LobbyMHelpButton != null)
            LobbyMHelpButton.OnPressed += MHelpButtonPressed;
    }

    private void OnAdminStatusUpdated()
    {
        if (UIHelper is not { IsOpen: true })
            return;
        EnsureUIHelper();
    }

    private void MHelpButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        EnsureUIHelper();
        UIHelper!.ToggleWindow();
    }

    public void OnSystemLoaded(MentorHelpSystem system)
    {
        _mentorHelpSystem = system;
        _mentorHelpSystem.OnMentorHelpTextMessageReceived += ReceivedMentorHelp;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenMentorHelp,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<MentorHelpUIController>();
    }

    public void OnSystemUnloaded(MentorHelpSystem system)
    {
        CommandBinds.Unregister<MentorHelpUIController>();

        DebugTools.Assert(_mentorHelpSystem != null);
        _mentorHelpSystem!.OnMentorHelpTextMessageReceived -= ReceivedMentorHelp;
        _mentorHelpSystem = null;
    }

    private void SetMHelpPressed(bool pressed)
    {
        if (GameMHelpButton != null)
            GameMHelpButton.Pressed = pressed;

        if (LobbyMHelpButton != null)
            LobbyMHelpButton.Pressed = pressed;

        UIManager.ClickSound();
        UnreadMHelpRead();
    }

    private void ReceivedMentorHelp(object? sender, SharedMentorHelpSystem.MentorHelpTextMessage message)
    {
        var localPlayer = _playerManager.LocalSession;
        if (localPlayer == null)
            return;

        EnsureUIHelper();

        if (message.PlaySound && localPlayer.UserId != message.TrueSender && !UIHelper!.IsOpen)
        {
            _audio.PlayGlobal(MHelpReceiveSound, Filter.Local(), false);
            _clyde.RequestWindowAttention();
        }

        if (!UIHelper!.IsOpen)
            UnreadMHelpReceived();

        UIHelper!.Receive(message);
    }

    private void PeopleTypingUpdated(MentorHelpPlayerTypingUpdated args, EntitySessionEventArgs session)
    {
        UIHelper?.PeopleTypingUpdated(args);
    }

    public void EnsureUIHelper()
    {
        var isMentor = _adminManager.HasFlag(AdminFlags.ViewNotes); // #Misfits Change — ViewNotes grants MHelp access

        if (UIHelper != null && UIHelper.IsMentor == isMentor)
            return;

        UIHelper?.Dispose();
        var ownerUserId = _playerManager.LocalUser!.Value;
        UIHelper = isMentor ? new AdminMentorHelpUIHandler(ownerUserId) : new UserMentorHelpUIHandler(ownerUserId);

        UIHelper.SendMessageAction = (userId, textMessage, playSound) => _mentorHelpSystem?.Send(userId, textMessage, playSound);
        UIHelper.InputTextChanged += (channel, text) => _mentorHelpSystem?.SendInputTextUpdated(channel, text.Length > 0);
        UIHelper.OnClose += () => { SetMHelpPressed(false); };
        UIHelper.OnOpen += () => { SetMHelpPressed(true); };
        SetMHelpPressed(UIHelper.IsOpen);
    }

    public void Open()
    {
        var localUser = _playerManager.LocalUser;
        if (localUser == null)
            return;
        EnsureUIHelper();
        if (UIHelper!.IsOpen)
            return;
        UIHelper!.Open(localUser.Value);
    }

    public void Open(NetUserId userId)
    {
        EnsureUIHelper();
        if (!UIHelper!.IsMentor)
            return;
        UIHelper?.Open(userId);
    }

    public void ToggleWindow()
    {
        EnsureUIHelper();
        UIHelper?.ToggleWindow();
    }

    public void PopOut()
    {
        EnsureUIHelper();
        if (UIHelper is not AdminMentorHelpUIHandler helper)
            return;

        if (helper.Window == null || helper.Control == null)
            return;

        helper.Control.Orphan();
        helper.Window.Dispose();
        helper.Window = null;
        helper.EverOpened = false;

        var monitor = _clyde.EnumerateMonitors().First();

        helper.ClydeWindow = _clyde.CreateWindow(new WindowCreateParameters
        {
            Maximized = false,
            Title = "Mentor Message",
            Monitor = monitor,
            Width = 900,
            Height = 500
        });

        helper.ClydeWindow.RequestClosed += helper.OnRequestClosed;
        helper.ClydeWindow.DisposeOnClose = true;

        helper.WindowRoot = _uiManager.CreateWindowRoot(helper.ClydeWindow);
        helper.WindowRoot.AddChild(helper.Control);

        helper.Control.PopOut.Disabled = true;
        helper.Control.PopOut.Visible = false;
    }

    private void UnreadMHelpReceived()
    {
        GameMHelpButton?.StyleClasses.Add(MenuButton.StyleClassRedTopButton);
        LobbyMHelpButton?.StyleClasses.Add(StyleNano.StyleClassButtonColorRed);
        _hasUnreadMHelp = true;
    }

    private void UnreadMHelpRead()
    {
        GameMHelpButton?.StyleClasses.Remove(MenuButton.StyleClassRedTopButton);
        LobbyMHelpButton?.StyleClasses.Remove(StyleNano.StyleClassButtonColorRed);
        _hasUnreadMHelp = false;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (GameMHelpButton != null)
        {
            GameMHelpButton.OnPressed -= MHelpButtonPressed;
            GameMHelpButton.OnPressed += MHelpButtonPressed;
            GameMHelpButton.Pressed = UIHelper?.IsOpen ?? false;

            if (_hasUnreadMHelp)
                UnreadMHelpReceived();
            else
                UnreadMHelpRead();
        }
    }

    public void OnStateExited(GameplayState state)
    {
        if (GameMHelpButton != null)
            GameMHelpButton.OnPressed -= MHelpButtonPressed;
    }

    public void OnStateEntered(LobbyState state)
    {
        if (LobbyMHelpButton != null)
        {
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
            LobbyMHelpButton.OnPressed += MHelpButtonPressed;
            LobbyMHelpButton.Pressed = UIHelper?.IsOpen ?? false;

            if (_hasUnreadMHelp)
                UnreadMHelpReceived();
            else
                UnreadMHelpRead();
        }
    }

    public void OnStateExited(LobbyState state)
    {
        if (LobbyMHelpButton != null)
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
    }
}

// UI handler interface for mentor help
public interface IMentorHelpUIHandler : IDisposable
{
    public bool IsMentor { get; }
    public bool IsOpen { get; }
    public void Receive(SharedMentorHelpSystem.MentorHelpTextMessage message);
    public void Close();
    public void Open(NetUserId netUserId);
    public void ToggleWindow();
    public void PeopleTypingUpdated(MentorHelpPlayerTypingUpdated args);
    public event Action OnClose;
    public event Action OnOpen;
    public Action<NetUserId, string, bool>? SendMessageAction { get; set; }
    public event Action<NetUserId, string>? InputTextChanged;
}

/// <summary>
/// Mentor-side UI handler - shows multi-player conversation interface with player list.
/// </summary>
public sealed class AdminMentorHelpUIHandler : IMentorHelpUIHandler
{
    private readonly NetUserId _ownerId;

    public AdminMentorHelpUIHandler(NetUserId owner)
    {
        _ownerId = owner;
    }

    private readonly Dictionary<NetUserId, MentorHelpPanel> _activePanelMap = new();
    public bool IsMentor => true;
    public bool IsOpen => Window is { Disposed: false, IsOpen: true } || ClydeWindow is { IsDisposed: false };
    public bool EverOpened;

    public MentorHelpWindow? Window;
    public WindowRoot? WindowRoot;
    public IClydeWindow? ClydeWindow;
    public MentorHelpControl? Control;

    public void Receive(SharedMentorHelpSystem.MentorHelpTextMessage message)
    {
        var panel = EnsurePanel(message.UserId);
        panel.ReceiveLine(message);
        Control?.OnMentorHelp(message.UserId);
    }

    private void OpenWindow()
    {
        if (Window == null)
            return;

        if (EverOpened)
            Window.Open();
        else
            Window.OpenCentered();
    }

    public void Close()
    {
        Window?.Close();

        if (ClydeWindow != null)
        {
            ClydeWindow.RequestClosed -= OnRequestClosed;
            ClydeWindow.Dispose();
            if (Control != null)
            {
                foreach (var (_, panel) in _activePanelMap)
                {
                    panel.Orphan();
                }
                Control?.Dispose();
            }
            OnClose?.Invoke();
        }
    }

    public void ToggleWindow()
    {
        EnsurePanel(_ownerId);

        if (IsOpen)
            Close();
        else
            OpenWindow();
    }

    public void PeopleTypingUpdated(MentorHelpPlayerTypingUpdated args)
    {
        if (_activePanelMap.TryGetValue(args.Channel, out var panel))
            panel.UpdatePlayerTyping(args.PlayerName, args.Typing);
    }

    public event Action? OnClose;
    public event Action? OnOpen;
    public Action<NetUserId, string, bool>? SendMessageAction { get; set; }
    public event Action<NetUserId, string>? InputTextChanged;

    public void Open(NetUserId channelId)
    {
        SelectChannel(channelId);
        OpenWindow();
    }

    public void OnRequestClosed(WindowRequestClosedEventArgs args)
    {
        Close();
    }

    private void EnsureControl()
    {
        if (Control is { Disposed: false })
            return;

        Window = new MentorHelpWindow();
        Control = Window.MentorHelp;
        Window.OnClose += () => { OnClose?.Invoke(); };
        Window.OnOpen += () =>
        {
            OnOpen?.Invoke();
            EverOpened = true;
        };

        foreach (var (_, panel) in _activePanelMap)
        {
            if (!Control!.MHelpArea.Children.Contains(panel))
            {
                Control!.MHelpArea.AddChild(panel);
            }
            panel.Visible = false;
        }
    }

    public void HideAllPanels()
    {
        foreach (var panel in _activePanelMap.Values)
        {
            panel.Visible = false;
        }
    }

    public MentorHelpPanel EnsurePanel(NetUserId channelId)
    {
        EnsureControl();

        if (_activePanelMap.TryGetValue(channelId, out var existingPanel))
            return existingPanel;

        _activePanelMap[channelId] = existingPanel = new MentorHelpPanel(text =>
            SendMessageAction?.Invoke(channelId, text, Window?.MentorHelp.PlaySound.Pressed ?? true));
        existingPanel.InputTextChanged += text => InputTextChanged?.Invoke(channelId, text);
        existingPanel.Visible = false;
        if (!Control!.MHelpArea.Children.Contains(existingPanel))
            Control.MHelpArea.AddChild(existingPanel);

        return existingPanel;
    }

    public bool TryGetChannel(NetUserId ch, [NotNullWhen(true)] out MentorHelpPanel? panel) =>
        _activePanelMap.TryGetValue(ch, out panel);

    private void SelectChannel(NetUserId uid)
    {
        EnsurePanel(uid);
        Control!.SelectChannel(uid);
    }

    public void Dispose()
    {
        Window?.Dispose();
        Window = null;
        Control = null;
        _activePanelMap.Clear();
        EverOpened = false;
    }
}

/// <summary>
/// Player-side UI handler - single conversation window with mentors.
/// </summary>
public sealed class UserMentorHelpUIHandler : IMentorHelpUIHandler
{
    private readonly NetUserId _ownerId;

    public UserMentorHelpUIHandler(NetUserId owner)
    {
        _ownerId = owner;
    }

    public bool IsMentor => false;
    public bool IsOpen => _window is { Disposed: false, IsOpen: true };
    private DefaultWindow? _window;
    private MentorHelpPanel? _chatPanel;

    public void Receive(SharedMentorHelpSystem.MentorHelpTextMessage message)
    {
        DebugTools.Assert(message.UserId == _ownerId);
        EnsureInit();
        _chatPanel!.ReceiveLine(message);
        _window!.OpenCentered();
    }

    public void Close()
    {
        _window?.Close();
    }

    public void ToggleWindow()
    {
        EnsureInit();
        if (_window!.IsOpen)
            _window.Close();
        else
            _window.OpenCentered();
    }

    public void PeopleTypingUpdated(MentorHelpPlayerTypingUpdated args)
    {
    }

    public event Action? OnClose;
    public event Action? OnOpen;
    public Action<NetUserId, string, bool>? SendMessageAction { get; set; }
    public event Action<NetUserId, string>? InputTextChanged;

    public void Open(NetUserId channelId)
    {
        EnsureInit();
        _window!.OpenCentered();
    }

    private void EnsureInit()
    {
        if (_window is { Disposed: false })
            return;
        _chatPanel = new MentorHelpPanel(text => SendMessageAction?.Invoke(_ownerId, text, true));
        _chatPanel.InputTextChanged += text => InputTextChanged?.Invoke(_ownerId, text);
        _window = new DefaultWindow()
        {
            TitleClass = "windowTitleAlert",
            HeaderClass = "windowHeaderMentor",
            Title = "Mentor Message",
            MinSize = new Vector2(500, 300),
        };
        _window.OnClose += () => { OnClose?.Invoke(); };
        _window.OnOpen += () => { OnOpen?.Invoke(); };
        _window.Contents.AddChild(_chatPanel);
    }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        _chatPanel = null;
    }
}
