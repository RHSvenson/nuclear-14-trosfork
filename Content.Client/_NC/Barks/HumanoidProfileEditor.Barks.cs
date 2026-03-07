using System.Linq;
using Content.Shared._NC.Speech.Synthesis;
using Content.Client._NC.Speech.Synthesis.System;

namespace Content.Client.Lobby.UI; // No, it doesn't need to be changed. It will break the logic.

public sealed partial class HumanoidProfileEditor
{
    private List<BarkPrototype> _barkVoiceList = new();

    private void InitializeBarkVoice()
    {
        _barkVoiceList = _prototypeManager
            .EnumeratePrototypes<BarkPrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => o.Name) // #Misfits Change - Name is raw display text, not a loc key
            .ToList();

        BarkVoiceButton.OnItemSelected += args =>
        {
            BarkVoiceButton.SelectId(args.Id);
            SetBarkVoice(_barkVoiceList[args.Id].ID);
        };

        BarkVoicePlayButton.OnPressed += _ => PlayPreviewBark();
    }

    private void UpdateBarkVoicesControls()
    {
        if (Profile is null)
            return;

        BarkVoiceButton.Clear();

        var firstVoiceChoiceId = 1;
        for (var i = 0; i < _barkVoiceList.Count; i++)
        {
            var voice = _barkVoiceList[i];

            var name = voice.Name; // #Misfits Change - Name is already display text, not a loc key
            BarkVoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;
        }

        var voiceChoiceId = _barkVoiceList.FindIndex(x => x.ID == Profile.BarkVoice);
        if (!BarkVoiceButton.TrySelectId(voiceChoiceId) &&
            BarkVoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetBarkVoice(_barkVoiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayPreviewBark()
    {
        if (Profile is null)
            return;

        _entManager.System<BarkSystem>().RequestPreviewBark(Profile.BarkVoice!);
    }
}