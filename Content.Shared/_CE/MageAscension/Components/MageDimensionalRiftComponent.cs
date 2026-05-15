namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// A dimensional rift a mage pries open with an open grimoire to complete ascension.
/// </summary>
[RegisterComponent]
public sealed partial class MageDimensionalRiftComponent : Component
{
    [DataField]
    public TimeSpan PryDuration = TimeSpan.FromSeconds(22);

    /// <summary>
    /// Bitmask of which progress emotes (stages 0-3) have fired during the current pry attempt.
    /// </summary>
    public byte EmittedEmoteMask;
}
