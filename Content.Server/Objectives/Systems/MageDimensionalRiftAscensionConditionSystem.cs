using Content.Server.Objectives.Components;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed class MageDimensionalRiftAscensionConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageDimensionalRiftAscensionConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, MageDimensionalRiftAscensionConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (TryComp<MageAscensionMindTrackerComponent>(args.MindId, out var tracker) && tracker.CompletedDimensionalRiftAscension)
            args.Progress = 1f;
        else
            args.Progress = 0f;
    }
}
