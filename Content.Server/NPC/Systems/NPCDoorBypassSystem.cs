using Content.Server.NPC.Components;
using Content.Shared.Doors.Components;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles door interaction for NPCs with NPCDoorBypassStateComponent.
/// Flow: detect door in path → request open (pry, access, etc.) → wait for open → proceed.
/// Integration with NPCSteeringSystem.Obstacles: when path is blocked by a door, the Obstacles
/// logic handles it (interact, pry, smash). This system updates NPCDoorBypassStateComponent state
/// for tracking and clears it when the door opens.
/// </summary>
public sealed partial class NPCDoorBypassSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCDoorBypassStateComponent>();
        while (query.MoveNext(out var uid, out var bypassState))
        {
            if (bypassState.TargetDoor is not { } door)
                continue;

            if (!TryComp<DoorComponent>(door, out var doorComp))
            {
                bypassState.TargetDoor = null;
                bypassState.State = NPCDoorBypassState.None;
                continue;
            }

            if (doorComp.State == DoorState.Open)
            {
                bypassState.TargetDoor = null;
                bypassState.State = NPCDoorBypassState.None;
            }
        }
    }
}
