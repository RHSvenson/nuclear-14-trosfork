namespace Content.Server.Carrying;

/// <summary>
/// Raised on the carried entity after a carry is fully set up
/// (both <see cref="CarryingComponent"/> and <see cref="BeingCarriedComponent"/> are initialized).
/// </summary>
public sealed class CarrySuccessEvent : EntityEventArgs
{
    public EntityUid Carrier;
    public EntityUid Carried;
}
