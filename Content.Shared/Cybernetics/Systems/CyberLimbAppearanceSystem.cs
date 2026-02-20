using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Humanoid;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Applies cyber limb appearance (humanoidBaseSprite) to the body when cyber limbs are attached,
/// and reverts to species default when removed.
/// </summary>
public sealed class CyberLimbAppearanceSystem : EntitySystem
{
    private static readonly IReadOnlyDictionary<string, (HumanoidVisualLayers Layer, string Id)[]> CategoryToLayers = new Dictionary<string, (HumanoidVisualLayers Layer, string Id)[]>
    {
        ["ArmLeft"] = [(HumanoidVisualLayers.LArm, "MobCyberLArm"), (HumanoidVisualLayers.LHand, "MobCyberLHand")],
        ["ArmRight"] = [(HumanoidVisualLayers.RArm, "MobCyberRArm"), (HumanoidVisualLayers.RHand, "MobCyberRHand")],
        ["LegLeft"] = [(HumanoidVisualLayers.LLeg, "MobCyberLLeg"), (HumanoidVisualLayers.LFoot, "MobCyberLFoot")],
        ["LegRight"] = [(HumanoidVisualLayers.RLeg, "MobCyberRLeg"), (HumanoidVisualLayers.RFoot, "MobCyberRFoot")],
    };

    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberLimbComponent, OrganGotInsertedEvent>(OnCyberLimbInserted);
        SubscribeLocalEvent<CyberLimbComponent, OrganGotRemovedEvent>(OnCyberLimbRemoved);
    }

    private void OnCyberLimbInserted(Entity<CyberLimbComponent> ent, ref OrganGotInsertedEvent args)
    {
        var body = args.Target;
        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Category is not { } category)
            return;

        var categoryStr = category.ToString();
        if (!CategoryToLayers.TryGetValue(categoryStr, out var layers))
            return;

        if (LifeStage(body) >= EntityLifeStage.Terminating)
            return;

        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
        {
            foreach (var (layer, id) in layers)
            {
                _humanoid.SetBaseLayerId(body, layer, id, humanoid: humanoid);
            }
        }

        if (TryComp<AppearanceComponent>(body, out var appearance))
        {
            foreach (var (layer, _) in layers)
            {
                _appearance.SetData(body, layer, true, appearance);
            }
        }
    }

    private void OnCyberLimbRemoved(Entity<CyberLimbComponent> ent, ref OrganGotRemovedEvent args)
    {
        var body = args.Target;
        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Category is not { } category)
            return;

        var categoryStr = category.ToString();
        if (!CategoryToLayers.TryGetValue(categoryStr, out var layers))
            return;

        if (LifeStage(body) >= EntityLifeStage.Terminating)
            return;

        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
        {
            foreach (var (layer, _) in layers)
            {
                humanoid.CustomBaseLayers.Remove(layer);
            }
            Dirty(body, humanoid);
        }

        if (TryComp<AppearanceComponent>(body, out var appearance))
        {
            foreach (var (layer, _) in layers)
            {
                _appearance.SetData(body, layer, false, appearance);
            }
        }
    }
}
