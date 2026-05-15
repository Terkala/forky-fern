using Content.Server.Objectives.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed class MageRiftBeastMassacreConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageRiftBeastMassacreConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, MageRiftBeastMassacreConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.Mind);
    }

    private float GetProgress(MindComponent mind)
    {
        if (mind.OwnedEntity is not { } horror)
            return 0f;

        var horrorMap = _xform.GetMapId(horror);

        var total = 0;
        var alive = 0;

        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uidEnt, out _, out var mob, out var xform))
        {
            if (uidEnt == horror)
                continue;

            if (_xform.GetMapId(uidEnt) != horrorMap)
                continue;

            total++;
            if (mob.CurrentState == MobState.Alive)
                alive++;
        }

        if (total == 0)
            return 1f;

        return 1f - (float) alive / total;
    }
}
