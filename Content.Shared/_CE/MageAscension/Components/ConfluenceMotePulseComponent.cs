using System;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// An <strong>unopened</strong> ley confluence cues periodic mana motes. Scheduling runs on
/// mage clients; the server sets <see cref="NextPulseAt"/> once when spawned.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConfluenceMotePulseComponent : Component
{
    /// <summary>Initial first-pulse time from the server (stagger). Clients advance locally after that.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextPulseAt;

    /// <summary>
    /// Interval between mote emissions from this leyline.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan PulsePeriod = TimeSpan.FromSeconds(30);
}
