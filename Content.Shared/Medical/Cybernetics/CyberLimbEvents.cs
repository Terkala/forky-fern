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
