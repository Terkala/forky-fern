using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Restrictions;

/// <summary>
/// Requires at least <see cref="Minimum"/> spent skill points of <see cref="SkillPointType"/> that were
/// spent on skills in <see cref="Tree"/> (path-specific progression gates).
/// </summary>
public sealed partial class NeedMinimumSkillPointsSpentInTree : CESkillRestriction
{
    [DataField(required: true)]
    public ProtoId<CESkillTreePrototype> Tree = default!;

    [DataField(required: true)]
    public ProtoId<CESkillPointPrototype> SkillPointType = default!;

    [DataField(required: true)]
    public int Minimum;

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        if (!skillSystem.TryGetSkillStorage(target, out var storage))
            return false;

        if (!storage.Comp.SkillPoints.TryGetValue(SkillPointType, out _))
            return false;

        return GetSpentInTree(storage.Comp, Tree) >= Minimum;
    }

    public override bool CheckGivenLearnedSkills(
        IEntityManager entManager,
        EntityUid target,
        HashSet<ProtoId<CESkillPrototype>> learnedSkills)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        if (!skillSystem.TryGetSkillStorage(target, out var storage))
            return Minimum <= 0;

        var proto = IoCManager.Resolve<IPrototypeManager>();
        float spent = 0;
        foreach (var id in learnedSkills)
        {
            if (storage.Comp.FreeLearnedSkills.Contains(id))
                continue;

            if (!proto.Resolve(id, out var s))
                continue;

            if (s.Tree != Tree)
                continue;

            if (!proto.Resolve(s.Tree, out var tree))
                continue;

            if (tree.SkillType != SkillPointType)
                continue;

            spent += s.LearnCost;
        }

        return spent >= Minimum;
    }

    private static float GetSpentInTree(CESkillStorageComponent storage, ProtoId<CESkillTreePrototype> tree)
    {
        return storage.SkillPointsSpentByTree.TryGetValue(tree, out var v)
            ? (float)v
            : 0f;
    }

    public override string GetDescription(IEntityManager entManager, IPrototypeManager protoManager)
    {
        if (!protoManager.Resolve(SkillPointType, out var pointProto))
            return string.Empty;

        if (!protoManager.Resolve(Tree, out var treeProto))
            return string.Empty;

        var pointName = Loc.GetString(pointProto.Name);
        var treeName = Loc.GetString(treeProto.Name);
        return Loc.GetString("ce-skill-req-min-skill-points-spent-in-tree",
            ("min", Minimum), ("pointName", pointName), ("treeName", treeName));
    }
}
