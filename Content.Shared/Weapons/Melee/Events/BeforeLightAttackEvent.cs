using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

/// Local event raised on the attacker before a light attack is processed.
[ByRefEvent]
public record struct BeforeLightAttackEvent(EntityUid User, NetCoordinates Coordinates, bool Handled = false);
