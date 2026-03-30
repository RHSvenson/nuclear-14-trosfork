using Content.Shared._Misfits.Chat.Components;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wieldable.Components;

namespace Content.Server._Misfits.Chat.Systems;

/// <summary>
/// Applies a speech style override when a speaker is actively wielding a megaphone item.
/// </summary>
public sealed class MegaphoneSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnTransformSpeakerName(TransformSpeakerNameEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(args.Sender))
        {
            if (!TryComp<MegaphoneComponent>(held, out var megaphone) ||
                !TryComp<WieldableComponent>(held, out var wieldable) ||
                !wieldable.Wielded)
            {
                continue;
            }

            // Ensure the megaphone style only applies while the item is truly wielded.
            args.SpeechVerb = megaphone.SpeechVerbOverride;
            return;
        }
    }
}
