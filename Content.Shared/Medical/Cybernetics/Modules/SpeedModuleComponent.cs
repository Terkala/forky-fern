using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Module component for speed enhancement modules installed in cyber-legs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpeedModuleComponent : SpecialModuleComponent
{
    public SpeedModuleComponent()
    {
        ModuleType = SpecialModuleType.Utility;
    }

    /// <summary>
    /// Walk speed multiplier (1.1 = 10% faster).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeedMultiplier = 1.1f;

    /// <summary>
    /// Sprint speed multiplier (1.15 = 15% faster).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintSpeedMultiplier = 1.15f;

    /// <summary>
    /// Stand-up speed multiplier (0.8 = 20% faster, since time is multiplied).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StandUpSpeedMultiplier = 0.8f;
}
