namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks the currently active cyber-tool on a user.
/// Prevents multiple cyber-tools from being active simultaneously.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed from player.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveCyberToolComponent : Component
{
    /// <summary>
    /// Type of tool currently active (e.g., "JawsOfLife", "Screwdriver").
    /// </summary>
    [DataField]
    public string ToolType = string.Empty;

    /// <summary>
    /// Time when the tool was activated.
    /// </summary>
    [DataField]
    public TimeSpan ActivationTime;

    /// <summary>
    /// Entity UID of the source module that activated this tool.
    /// </summary>
    [DataField]
    public EntityUid? SourceModule;
}
