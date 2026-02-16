using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Tags body parts with species and organ category when inserted into body_organs.
/// Covers both initial spawn (EntityTableContainerFill) and mid-round limb attachment.
/// </summary>
public sealed class SurgeryLimbTaggingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, EntGotInsertedIntoContainerMessage>(OnBodyPartInserted);
    }

    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        var body = args.Container.Owner;
        if (!Exists(body))
            return;

        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Category is not { } category)
            return;

        var speciesId = ResolveSpecies(body, ent);
        var comp = EnsureComp<SurgeryBodyPartComponent>(ent);
        comp.SpeciesId = speciesId;
        comp.OrganCategory = category;
        Dirty(ent, comp);
    }

    private ProtoId<Humanoid.Prototypes.SpeciesPrototype> ResolveSpecies(EntityUid body, EntityUid limb)
    {
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            return humanoid.Species;

        return Content.Shared.Humanoid.SharedHumanoidAppearanceSystem.DefaultSpecies;
    }
}
