using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Progress from opened ley confluences vs <see cref="NumberObjectiveComponent.Target"/>.
/// </summary>
[RegisterComponent, Access(typeof(MageOpenLeylinesConditionSystem))]
public sealed partial class MageOpenLeylinesConditionComponent : Component;
