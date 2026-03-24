using System;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Server-only: an <strong>unopened</strong> ley confluence fires one mana mote each <see cref="PulsePeriod"/>
/// toward a random mage or another ley confluence on the same map. Removed when opened.
/// </summary>
[RegisterComponent]
public sealed partial class ConfluenceMotePulseComponent : Component
{
    public TimeSpan NextPulseAt;

    /// <summary>
    /// Interval between mote emissions from this leyline.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan PulsePeriod = TimeSpan.FromSeconds(30);
}
