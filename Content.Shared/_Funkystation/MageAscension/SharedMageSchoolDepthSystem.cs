using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Keeps <see cref="MageSchoolDepthComponent.DepthBySchool"/> aligned with learned progression skills per mage school.
/// </summary>
public sealed class SharedMageSchoolDepthSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

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

    /// <summary>
    /// Read cached depth for a <see cref="MageSchoolPrototype"/> id (e.g. Elementalism).
    /// </summary>
    public bool TryGetSchoolDepth(EntityUid skillStorageUid, string mageSchoolId, out int depth)
    {
        depth = 0;
        if (!TryComp<MageSchoolDepthComponent>(skillStorageUid, out var comp))
            return false;
        return comp.DepthBySchool.TryGetValue(mageSchoolId, out depth);
    }

    public void Recompute(EntityUid storageUid, CESkillStorageComponent? storage = null)
    {
        if (!Resolve(storageUid, ref storage, false))
            return;

        var comp = EnsureComp<MageSchoolDepthComponent>(storageUid);
        var changed = false;

        foreach (var school in _proto.EnumeratePrototypes<MageSchoolPrototype>())
        {
            if (!_proto.Resolve(school.PackageSkill, out var packageSkill))
                continue;

            if (!storage.AvailableSkillTrees.Contains(packageSkill.Tree))
            {
                if (comp.DepthBySchool.Remove(school.ID))
                    changed = true;
                continue;
            }

            var depth = MageSchoolProgressStatics.CountProgressionLearned(storage.LearnedSkills, school.ID, _proto);
            if (comp.DepthBySchool.TryGetValue(school.ID, out var old) && old == depth)
                continue;

            comp.DepthBySchool[school.ID] = depth;
            changed = true;
        }

        if (changed)
            Dirty(storageUid, comp);
    }
}
