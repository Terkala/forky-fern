namespace Content.Server.NPC.Components;

/// <summary>
/// Tracks door-bypass state for NPCs that can traverse doors (e.g. companions following their owner).
/// </summary>
[RegisterComponent]
public sealed partial class NPCDoorBypassStateComponent : Component
{
    /// <summary>
    /// The door entity we are currently attempting to bypass.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetDoor;

    /// <summary>
    /// Current state of the door bypass attempt.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public NPCDoorBypassState State = NPCDoorBypassState.None;
}

public enum NPCDoorBypassState : byte
{
    None,
    RequestingOpen,
    WaitingForOpen
}
