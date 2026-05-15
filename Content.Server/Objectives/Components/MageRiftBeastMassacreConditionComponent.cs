using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Measures slaughter of humanoid crew on the same map as the rift horror (excluding the horror).
/// </summary>
[RegisterComponent, Access(typeof(MageRiftBeastMassacreConditionSystem))]
public sealed partial class MageRiftBeastMassacreConditionComponent : Component;
