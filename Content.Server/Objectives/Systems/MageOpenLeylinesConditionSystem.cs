using Content.Server.Objectives.Components;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed class MageOpenLeylinesConditionSystem : EntitySystem
{
    [Dependency] private readonly NumberObjectiveSystem _number = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageOpenLeylinesConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, MageOpenLeylinesConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.MindId, args.Mind, _number.GetTarget(uid));
    }

    private float GetProgress(EntityUid mindId, MindComponent mind, int target)
    {
        if (target <= 0)
            return 1f;

        var opened = 0;
        if (TryComp<MageAscensionMindTrackerComponent>(mindId, out var tracker))
            opened = tracker.LeylinesOpenedCount;
        else if (mind.OwnedEntity is { } body && TryComp<MageOfAscensionComponent>(body, out var mage))
            opened = mage.ConfluencesOpened;

        if (opened >= target)
            return 1f;

        return (float) opened / target;
    }
}
