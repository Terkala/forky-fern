using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Keeps <see cref="MageElementalismProgressComponent.ElementalismDepth"/> aligned with learned progression skills.
/// </summary>
public sealed class SharedMageElementalismDepthSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<CESkillTreePrototype> MageElementalismTree = new("MageElementalism");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESkillStorageComponent, CESkillLearnedEvent>(OnSkillLearned);
        SubscribeLocalEvent<CESkillStorageComponent, CESkillRemovedEvent>(OnSkillRemoved);
    }

    private void OnSkillLearned(Entity<CESkillStorageComponent> ent, ref CESkillLearnedEvent args)
    {
        Recompute(ent.Owner, ent.Comp);
    }

    private void OnSkillRemoved(Entity<CESkillStorageComponent> ent, ref CESkillRemovedEvent args)
    {
        Recompute(ent.Owner, ent.Comp);
    }

    public void Recompute(EntityUid storageUid, CESkillStorageComponent? storage = null)
    {
        if (!Resolve(storageUid, ref storage, false))
            return;

        if (!storage.AvailableSkillTrees.Contains(MageElementalismTree))
        {
            if (TryComp<MageElementalismProgressComponent>(storageUid, out var existing))
            {
                if (existing.ElementalismDepth != 0)
                {
                    existing.ElementalismDepth = 0;
                    Dirty(storageUid, existing);
                }
            }

            return;
        }

        var depth = MageElementalismProgressStatics.CountProgressionLearned(storage.LearnedSkills, _proto);
        var comp = EnsureComp<MageElementalismProgressComponent>(storageUid);
        if (comp.ElementalismDepth == depth)
            return;

        comp.ElementalismDepth = depth;
        Dirty(storageUid, comp);
    }
}
