// #Misfits Change /Add/ - Shared smelling salts component and do-after event for non-injection resuscitation.
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Medical;

[RegisterComponent]
public sealed partial class SmellingSaltsComponent : Component
{
    [DataField("doAfterDuration")]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(6);

    [DataField("reviveHeal", required: true)]
    public DamageSpecifier ReviveHeal = default!;

    [DataField("allowMovement")]
    public bool AllowMovement = true;

    [DataField("canReviveCrit")]
    public bool CanReviveCrit = true;

    [DataField("useSound")]
    public SoundSpecifier? UseSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

[Serializable, NetSerializable]
public sealed partial class SmellingSaltsDoAfterEvent : SimpleDoAfterEvent
{
}