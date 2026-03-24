using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;

namespace Content.Server._CE.MageAscension;

/// <summary>
/// Unopened confluences are interaction/examine blocked for non-mages (full mage-only visibility needs engine support).
/// </summary>
public sealed class MageConfluenceVisibilitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConfluenceComponent, ExamineAttemptEvent>(OnExamineAttempt);
        SubscribeLocalEvent<ConfluenceComponent, GettingInteractedWithAttemptEvent>(OnInteractAttempt);
    }

    private void OnExamineAttempt(Entity<ConfluenceComponent> ent, ref ExamineAttemptEvent args)
    {
        if (ent.Comp.Opened || !ent.Comp.MageOnlyVisibility)
            return;

        if (HasComp<MageOfAscensionComponent>(args.Examiner))
            return;

        args.Cancel();
    }

    private void OnInteractAttempt(Entity<ConfluenceComponent> ent, ref GettingInteractedWithAttemptEvent args)
    {
        if (ent.Comp.Opened || !ent.Comp.MageOnlyVisibility)
            return;

        if (!args.Uid.IsValid())
            return;

        if (HasComp<MageOfAscensionComponent>(args.Uid))
            return;

        args.Cancelled = true;
    }
}
