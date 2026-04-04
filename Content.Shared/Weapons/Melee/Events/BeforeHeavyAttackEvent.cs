using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

/// Local event raised on the attacker before a heavy attack is processed.
[ByRefEvent]
public record struct BeforeHeavyAttackEvent(EntityUid User, NetCoordinates Coordinates, NetEntity Weapon, bool Handled = false);
