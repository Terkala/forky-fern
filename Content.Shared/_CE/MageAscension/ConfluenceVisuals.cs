using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Appearance for <see cref="Components.ConfluenceComponent"/> world sprite (ley confluence / dormant vs visible after opening).
/// </summary>
[Serializable, NetSerializable]
public enum ConfluenceVisuals : byte
{
    LeyLine,
}

[Serializable, NetSerializable]
public enum ConfluenceLeyLineVisualState : byte
{
    /// <summary>Unopened knot; mages-only interaction until channeled.</summary>
    Dormant,
    /// <summary>Opened — visible to all players; shows harvested fracture animation.</summary>
    Harvested,
}

/// <summary>
/// Raised on the confluence entity after networked <see cref="Components.ConfluenceComponent"/> state is applied (shared physics handler).
/// </summary>
public sealed class ConfluenceStateHandledEvent : EntityEventArgs;
