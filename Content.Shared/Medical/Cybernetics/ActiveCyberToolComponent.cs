using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks the currently active cyber-tool on a user.
/// Prevents multiple cyber-tools from being active simultaneously.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveCyberToolComponent : Component
{
    /// <summary>
    /// Type of tool currently active (e.g., "JawsOfLife", "Screwdriver").
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ToolType = string.Empty;

    /// <summary>
    /// Time when the tool was activated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ActivationTime;

    /// <summary>
    /// Entity UID of the source module that activated this tool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceModule;
}
