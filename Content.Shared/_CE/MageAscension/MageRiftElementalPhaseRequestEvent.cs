namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Server handles this to apply short-duration wall phasing on the elemental rift horror.
/// </summary>
public sealed class MageRiftElementalPhaseRequestEvent : EntityEventArgs
{
    public TimeSpan Duration = TimeSpan.FromSeconds(10);
}
