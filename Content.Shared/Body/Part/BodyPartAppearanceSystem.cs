using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Humanoid;

namespace Content.Shared.Body.Part;

/// <summary>
/// System that integrates body parts with the humanoid appearance system.
/// Hides/shows visual layers when body parts are attached/detached.
/// </summary>
public sealed class BodyPartAppearanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, BodyPartAttachedEvent>(OnBodyPartAttached);
    }

    private void OnBodyPartAttached(Entity<BodyPartComponent> ent, ref BodyPartAttachedEvent args)
    {
        if (args.Body == EntityUid.Invalid)
            return;

        if (TryComp<BodyComponent>(args.Body, out var bodyComp))
        {
            HandleBodyPartAttaching((args.Body, bodyComp), ent);
        }
    }

    /// <summary>
    /// Handles appearance changes when a body part is attached.
    /// Called by BodySystem when it detects a body part is being attached.
    /// </summary>
    public void HandleBodyPartAttaching(Entity<BodyComponent> body, Entity<BodyPartComponent> bodyPart)
    {
        // Get the layers that should be shown for this body part
        var layers = GetLayersForBodyPart(bodyPart.Comp.PartType, bodyPart.Comp.Symmetry);

        // Remove from PermanentlyHidden to show the layers
        if (TryComp<HumanoidAppearanceComponent>(body, out var appearance))
        {
            foreach (var layer in layers)
            {
                appearance.PermanentlyHidden.Remove(layer);
            }
            Dirty(body, appearance);
        }
    }

    /// <summary>
    /// Handles appearance changes when a body part is detached.
    /// Called by BodySystem when it detects a body part is being detached.
    /// </summary>
    public void HandleBodyPartDetaching(Entity<BodyComponent> body, Entity<BodyPartComponent> bodyPart)
    {
        // Get the layers that should be hidden for this body part
        var layers = GetLayersForBodyPart(bodyPart.Comp.PartType, bodyPart.Comp.Symmetry);

        // Add to PermanentlyHidden to hide the layers
        if (TryComp<HumanoidAppearanceComponent>(body, out var appearance))
        {
            foreach (var layer in layers)
            {
                appearance.PermanentlyHidden.Add(layer);
            }
            Dirty(body, appearance);
        }
    }

    /// <summary>
    /// Gets the HumanoidVisualLayers that correspond to a body part type and symmetry.
    /// Arms hide both arm and hand layers, legs hide both leg and foot layers.
    /// </summary>
    private HashSet<HumanoidVisualLayers> GetLayersForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        var layers = new HashSet<HumanoidVisualLayers>();

        switch (partType)
        {
            case BodyPartType.Head:
                layers.Add(HumanoidVisualLayers.Head);
                break;

            case BodyPartType.Torso:
                layers.Add(HumanoidVisualLayers.Chest);
                break;

            case BodyPartType.Arm:
                if (symmetry == BodyPartSymmetry.Left)
                {
                    layers.Add(HumanoidVisualLayers.LArm);
                    layers.Add(HumanoidVisualLayers.LHand);
                }
                else if (symmetry == BodyPartSymmetry.Right)
                {
                    layers.Add(HumanoidVisualLayers.RArm);
                    layers.Add(HumanoidVisualLayers.RHand);
                }
                break;

            case BodyPartType.Leg:
                if (symmetry == BodyPartSymmetry.Left)
                {
                    layers.Add(HumanoidVisualLayers.LLeg);
                    layers.Add(HumanoidVisualLayers.LFoot);
                }
                else if (symmetry == BodyPartSymmetry.Right)
                {
                    layers.Add(HumanoidVisualLayers.RLeg);
                    layers.Add(HumanoidVisualLayers.RFoot);
                }
                break;
        }

        return layers;
    }
}
