using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Complete when the mage finishes prying the dimensional rift and passes through.
/// </summary>
[RegisterComponent, Access(typeof(MageDimensionalRiftAscensionConditionSystem))]
public sealed partial class MageDimensionalRiftAscensionConditionComponent : Component;
