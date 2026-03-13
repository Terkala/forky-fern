using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Companion;

/// <summary>
/// Picks a position near the FollowTarget for companion positioning. Uses the follow target as origin
/// to find an accessible spot nearby (avoid occlusion, prefer safe spots).
/// </summary>
public sealed partial class PickCompanionPositionOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfinding = default!;

    [DataField("rangeKey", required: true)]
    public string RangeKey = "FollowCloseRange";

    [DataField("targetCoordinates")]
    public string TargetCoordinates = "FollowIdleTarget";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.FollowTarget, out var followTarget, _entManager))
            return (false, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        if (range == 0f)
            range = 3f;

        var followTargetEntity = followTarget.EntityId;
        if (!_entManager.EntityExists(followTargetEntity))
            return (false, null);

        var path = await _pathfinding.GetRandomPath(
            followTargetEntity,
            range,
            cancelToken,
            flags: _pathfinding.GetFlags(blackboard));

        if (path.Result != PathResult.Path || path.Path.Count == 0)
            return (false, null);

        var targetCoords = path.Path.Last().Coordinates;

        return (true, new Dictionary<string, object>
        {
            { TargetCoordinates, targetCoords }
        });
    }
}
