using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

public sealed class MageRiftElementalPhaseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageRiftElementalVariantComponent, MageRiftElementalPhaseRequestEvent>(OnPhaseRequest);
        SubscribeLocalEvent<MageRiftElementalPhaseActiveComponent, ComponentShutdown>(OnPhaseShutdown);
        SubscribeLocalEvent<MageRiftElementalPhaseActiveComponent, GettingInteractedWithAttemptEvent>(OnGettingInteracted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<MageRiftElementalPhaseActiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (cur >= comp.EndTime)
                EndPhase(uid, comp);
        }
    }

    private void OnPhaseRequest(Entity<MageRiftElementalVariantComponent> ent, ref MageRiftElementalPhaseRequestEvent args)
    {
        if (HasComp<MageRiftElementalPhaseActiveComponent>(ent.Owner))
            return;

        if (!TryComp<PhysicsComponent>(ent.Owner, out var body))
            return;

        var saved = body.CanCollide;
        _physics.SetCanCollide(ent.Owner, false, body: body);

        var active = AddComp<MageRiftElementalPhaseActiveComponent>(ent.Owner);
        active.SavedCanCollide = saved;
        active.EndTime = _timing.CurTime + args.Duration;
    }

    private void OnPhaseShutdown(Entity<MageRiftElementalPhaseActiveComponent> ent, ref ComponentShutdown args)
    {
        RestoreCollision(ent.Owner, ent.Comp);
    }

    private void OnGettingInteracted(Entity<MageRiftElementalPhaseActiveComponent> ent, ref GettingInteractedWithAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void EndPhase(EntityUid uid, MageRiftElementalPhaseActiveComponent comp)
    {
        RestoreCollision(uid, comp);
        RemComp<MageRiftElementalPhaseActiveComponent>(uid);
    }

    private void RestoreCollision(EntityUid uid, MageRiftElementalPhaseActiveComponent comp)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body))
            return;

        _physics.SetCanCollide(uid, comp.SavedCanCollide, body: body);
    }
}
