using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Base component for special cyber-limb modules that provide tool or utility functionality.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public partial class SpecialModuleComponent : Component
{
    /// <summary>
    /// Type of special module (Tool, Utility, or BioBattery).
    /// </summary>
    [DataField, AutoNetworkedField]
    public SpecialModuleType ModuleType;

    /// <summary>
    /// Tool quality for tool modules (e.g., "Prying", "Screwing", "Cutting").
    /// Only used when ModuleType is Tool.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype>? ToolQuality;

    /// <summary>
    /// Cooldown time between activations.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ActivationCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Last time this module was activated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastActivation = TimeSpan.Zero;
}

/// <summary>
/// Types of special modules that can be installed in cyber-limbs.
/// </summary>
public enum SpecialModuleType : byte
{
    Tool,
    Utility,
    BioBattery
}
