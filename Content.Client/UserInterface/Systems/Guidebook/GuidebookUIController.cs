using System.Linq;
using Content.Client._Misfits.WebView; // #Misfits Add - WebView guidebook window
using Content.Client.Gameplay;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Guidebook;

public sealed class GuidebookUIController : UIController, IOnStateEntered<LobbyState>, IOnStateEntered<GameplayState>, IOnStateExited<LobbyState>, IOnStateExited<GameplayState>, IOnSystemChanged<GuidebookSystem>
{
    [UISystemDependency] private readonly GuidebookSystem _guidebookSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;

    // #Misfits Removed - replaced by MisfitsGuidebookWebWindow
    // private GuidebookWindow? _guideWindow;
    private MisfitsGuidebookWebWindow? _guideWebWindow; // #Misfits Add - WebView guidebook window
    private MenuButton? GuidebookButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.GuidebookButton;
    // #Misfits Removed - wiki handles its own navigation history
    // private ProtoId<GuideEntryPrototype>? _lastEntry;

    public void OnStateEntered(LobbyState state)
    {
        HandleStateEntered();
    }

    public void OnStateEntered(GameplayState state)
    {
        HandleStateEntered();
    }

    private void HandleStateEntered()
    {
        // #Misfits Removed - old XAML guidebook window
        // DebugTools.Assert(_guideWindow == null);
        // _guideWindow = UIManager.CreateWindow<GuidebookWindow>();
        // _guideWindow.OnClose += OnWindowClosed;
        // _guideWindow.OnOpen += OnWindowOpen;

        // #Misfits Add - WebView guidebook window
        DebugTools.Assert(_guideWebWindow == null);
        _guideWebWindow = UIManager.CreateWindow<MisfitsGuidebookWebWindow>();
        _guideWebWindow.OnClose += OnWindowClosed;
        _guideWebWindow.OnOpen += OnWindowOpen;

        // setup keybinding
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenGuidebook,
                InputCmdHandler.FromDelegate(_ => ToggleGuidebook()))
            .Register<GuidebookUIController>();
    }

    public void OnStateExited(LobbyState state)
    {
        HandleStateExited();
    }

    public void OnStateExited(GameplayState state)
    {
        HandleStateExited();
    }

    private void HandleStateExited()
    {
        // #Misfits Change - dispose WebView guidebook window
        if (_guideWebWindow == null)
            return;

        _guideWebWindow.OnClose -= OnWindowClosed;
        _guideWebWindow.OnOpen -= OnWindowOpen;

        _guideWebWindow.Dispose();
        _guideWebWindow = null;
        CommandBinds.Unregister<GuidebookUIController>();
    }

    public void OnSystemLoaded(GuidebookSystem system)
    {
        _guidebookSystem.OnGuidebookOpen += ToggleGuidebook;
    }

    public void OnSystemUnloaded(GuidebookSystem system)
    {
        _guidebookSystem.OnGuidebookOpen -= ToggleGuidebook;
    }

    internal void UnloadButton()
    {
        if (GuidebookButton == null)
            return;

        GuidebookButton.OnPressed -= GuidebookButtonOnPressed;
    }

    internal void LoadButton()
    {
        if (GuidebookButton == null)
            return;

        GuidebookButton.OnPressed += GuidebookButtonOnPressed;
    }

    private void GuidebookButtonOnPressed(ButtonEventArgs obj)
    {
        ToggleGuidebook();
    }

    private void OnWindowClosed()
    {
        if (GuidebookButton != null)
            GuidebookButton.Pressed = false;

        // #Misfits Removed - XAML GuidebookWindow-specific ReturnContainer / LastEntry tracking
        // if (_guideWindow != null)
        // {
        //     _guideWindow.ReturnContainer.Visible = false;
        //     if (_guideWindow.LastEntry.Id != null)
        //         _lastEntry = _guideWindow.LastEntry;
        // }
    }

    private void OnWindowOpen()
    {
        if (GuidebookButton != null)
            GuidebookButton.Pressed = true;
    }

    /// <summary>
    ///     Opens or closes the guidebook.
    /// </summary>
    /// <param name="guides">What guides should be shown. If not specified, this will instead list all the entries</param>
    /// <param name="rootEntries">A list of guides that should form the base of the table of contents. If not specified,
    /// this will automatically simply be a list of all guides that have no parent.</param>
    /// <param name="forceRoot">This forces a singular guide to contain all other guides. This guide will
    /// contain its own children, in addition to what would normally be the root guides if this were not
    /// specified.</param>
    /// <param name="includeChildren">Whether or not to automatically include child entries. If false, this will ONLY
    /// show the specified entries</param>
    /// <param name="selected">The guide whose contents should be displayed when the guidebook is opened</param>
    public void ToggleGuidebook(
        Dictionary<string, GuideEntry>? guides = null,
        List<string>? rootEntries = null,
        string? forceRoot = null,
        bool includeChildren = true,
        string? selected = null)
    {
        // #Misfits Change - ToggleGuidebook now opens/closes MisfitsGuidebookWebWindow; XAML tree params ignored
        // #Misfits Removed - old GuidebookWindow logic below
        // if (_guideWindow == null) return;
        // if (_guideWindow.IsOpen) { UIManager.ClickSound(); _guideWindow.Close(); return; }
        // ... guides/rootEntries/forceRoot/selected tree processing ...
        // _guideWindow.UpdateGuides(...); _guideWindow.Tree.SetAllExpanded(...); _guideWindow.OpenCenteredRight();

        if (_guideWebWindow == null)
            return;

        if (_guideWebWindow.IsOpen)
        {
            UIManager.ClickSound();
            _guideWebWindow.Close();
            return;
        }

        if (GuidebookButton != null)
            GuidebookButton.SetClickPressed(!_guideWebWindow.IsOpen);

        // Navigate to a specific guide's wiki equivalent if a URL was passed via selected
        if (selected != null && selected.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            _guideWebWindow.OpenUrl(selected);

        _guideWebWindow.OpenCenteredRight();
    }

    public void ToggleGuidebook(
        List<string> guideList,
        List<string>? rootEntries = null,
        string? forceRoot = null,
        bool includeChildren = true,
        string? selected = null)
    {
        Dictionary<string, GuideEntry> guides = new();
        foreach (var guideId in guideList)
        {
            if (!_prototypeManager.TryIndex<GuideEntryPrototype>(guideId, out var guide))
            {
                Logger.Error($"Encountered unknown guide prototype: {guideId}");
                continue;
            }
            guides.Add(guideId, guide);
        }

        ToggleGuidebook(guides, rootEntries, forceRoot, includeChildren, selected);
    }

    // #Misfits Add - Open the WebView guidebook directly to a specific URL (e.g. wiki Rules page)
    public void OpenToUrl(string url)
    {
        if (_guideWebWindow == null)
            return;

        _guideWebWindow.OpenUrl(url);

        if (!_guideWebWindow.IsOpen)
            _guideWebWindow.OpenCenteredRight();
    }

    private void RecursivelyAddChildren(GuideEntry guide, Dictionary<string, GuideEntry> guides)
    {
        foreach (var childId in guide.Children)
        {
            if (guides.ContainsKey(childId))
                continue;

            if (!_prototypeManager.TryIndex<GuideEntryPrototype>(childId, out var child))
            {
                Logger.Error($"Encountered unknown guide prototype: {childId} as a child of {guide.Id}. If the child is not a prototype, it must be directly provided.");
                continue;
            }

            guides.Add(childId, child);
            RecursivelyAddChildren(child, guides);
        }
    }
}
