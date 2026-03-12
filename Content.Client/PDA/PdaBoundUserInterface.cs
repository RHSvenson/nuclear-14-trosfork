using Content.Client.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client.PDA
{
    [UsedImplicitly]
    public sealed class PdaBoundUserInterface : CartridgeLoaderBoundUserInterface
    {
        [ViewVariables]
        private PdaMenu? _menu;

        public PdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            if (_menu == null)
                CreateMenu();
        }

        private void CreateMenu()
        {
            _menu = this.CreateWindow<PdaMenu>();
            _menu.OpenCenteredLeft();

            // #Misfits Add - Pass the PDA entity UID for health status lookups
            _menu.SetPdaEntity(Owner);

            _menu.FlashLightToggleButton.OnToggled += _ =>
            {
                SendMessage(new PdaToggleFlashlightMessage());
            };

            _menu.EjectIdButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
            };

            _menu.EjectPenButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
            };

            _menu.EjectPaiButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));
            };

            _menu.ActivateMusicButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowMusicMessage());
            };

            _menu.AccessRingtoneButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowRingtoneMessage());
            };

            _menu.ShowUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowUplinkMessage());
            };

            _menu.LockUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaLockUplinkMessage());
            };

            _menu.OnProgramItemPressed += ActivateCartridge;
            _menu.OnInstallButtonPressed += InstallCartridge;
            _menu.OnUninstallButtonPressed += UninstallCartridge;
            _menu.ProgramCloseButton.OnPressed += _ => DeactivateActiveCartridge();

            var borderColorComponent = GetBorderColorComponent();
            if (borderColorComponent == null)
                return;

            _menu.BorderColor = borderColorComponent.BorderColor;
            _menu.AccentHColor = borderColorComponent.AccentHColor;
            _menu.AccentVColor = borderColorComponent.AccentVColor;
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            // #Misfits Add - Set HasId flag BEFORE base.UpdateState, because the base class
            // calls UpdateAvailablePrograms (which checks _hasIdCard) before we reach _menu.UpdateState.
            if (state is PdaUpdateState pdaState)
                _menu?.SetHasIdCard(pdaState.HasId);

            base.UpdateState(state);

            if (state is not PdaUpdateState updateState)
                return;

            _menu?.UpdateState(updateState);
        }

        protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
        {
            _menu?.ProgramView.AddChild(cartridgeUIFragment);
            _menu?.ToProgramView(title ?? Loc.GetString("comp-pda-io-program-fallback-title"));
        }

        protected override void DetachCartridgeUI(Control cartridgeUIFragment)
        {
            if (_menu is null)
                return;

            _menu.ToHomeScreen();
            _menu.HideProgramHeader();
            _menu.ProgramView.RemoveChild(cartridgeUIFragment);
        }

        protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
        {
            _menu?.UpdateAvailablePrograms(programs);
        }

        private PdaBorderColorComponent? GetBorderColorComponent()
        {
            return EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        }
    }
}
