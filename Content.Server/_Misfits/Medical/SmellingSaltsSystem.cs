// #Misfits Change /Add/ - Smelling salts now perform a long resuscitation interaction instead of injecting a reagent.
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared._Misfits.Medical;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Misfits.Medical;

public sealed class SmellingSaltsSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ResuscitationSystem _resuscitation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SmellingSaltsComponent, Content.Shared.Interaction.AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SmellingSaltsComponent, SmellingSaltsDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, SmellingSaltsComponent component, Content.Shared.Interaction.AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartUse(uid, target, args.User, component);
    }

    public bool TryStartUse(EntityUid uid, EntityUid target, EntityUid user, SmellingSaltsComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_resuscitation.CanResuscitate(target, false, component.CanReviveCrit))
            return false;

        if (component.UseSound != null)
            _audio.PlayPvs(component.UseSound, uid);

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, component.DoAfterDuration, new SmellingSaltsDoAfterEvent(),
            uid, target, uid)
            {
                BlockDuplicate = true,
                BreakOnHandChange = true,
                NeedHand = true,
                BreakOnMove = !component.AllowMovement,
            });

        if (started)
            _resuscitation.SendAttemptEmote(target, uid);

        return started;
    }

    private void OnDoAfter(EntityUid uid, SmellingSaltsComponent component, SmellingSaltsDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is not { } target)
            return;

        if (!_resuscitation.CanResuscitate(target, false, component.CanReviveCrit))
            return;

        args.Handled = true;
        var result = _resuscitation.TryResuscitate(uid,
            target,
            args.User,
            component.ReviveHeal,
            "smelling-salts-revive-do");

        if (result.Rotten)
        {
            _popup.PopupEntity(Loc.GetString("smelling-salts-rotten"), uid, args.User);
        }
        else if (!result.HasMindSession)
        {
            _popup.PopupEntity(Loc.GetString("smelling-salts-no-mind"), uid, args.User);
        }

        QueueDel(uid);
    }
}