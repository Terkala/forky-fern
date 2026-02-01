using Content.Shared.Medical.Cybernetics.Modules;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Module component for Jaws of Life cyber-tool that provides prying capability.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JawsOfLifeModuleComponent : SpecialModuleComponent
{
    public JawsOfLifeModuleComponent()
    {
        ModuleType = SpecialModuleType.Tool;
        ToolQuality = "Prying";
    }

    /// <summary>
    /// Speed multiplier for prying operations (1.5x default = 50% faster).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PryingSpeed = 1.5f;

    /// <summary>
    /// Whether this tool can pry powered doors.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PryPowered = true;
}
