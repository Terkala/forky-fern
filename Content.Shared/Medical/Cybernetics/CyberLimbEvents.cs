using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Event raised when a module is installed into a cyber-limb.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbModuleInstalledEvent(EntityUid Module, EntityUid CyberLimb);

/// <summary>
/// Event raised when a module is removed from a cyber-limb.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbModuleRemovedEvent(EntityUid Module, EntityUid CyberLimb);

/// <summary>
/// Event raised when a cyber-limb's maintenance panel state changes.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbPanelChangedEvent(EntityUid CyberLimb, bool PanelOpen);

/// <summary>
/// Event raised when a cyber-limb is attached to a body.
/// Raised by BodySystem when it detects a cyber-limb is being attached.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbAttachedEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> CyberLimb);

/// <summary>
/// Event raised when a cyber-limb is detached from a body.
/// Raised by BodySystem when it detects a cyber-limb is being detached.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbDetachedEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> CyberLimb);

/// <summary>
/// Event raised when an ion storm affects entities with cyber-limbs.
/// Raised by IonStormRule when an ion storm occurs.
/// </summary>
[ByRefEvent]
public readonly record struct IonDamageCyberLimbsEvent(EntityUid Body);

/// <summary>
/// Event raised when a battery module is successfully removed from a cyber-limb.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveBatteryModuleDoAfterEvent : SimpleDoAfterEvent
{
}